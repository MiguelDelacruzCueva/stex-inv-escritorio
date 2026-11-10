using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Refit;
using STEX.Inv.Desktop.Core.DTOs;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;
using STEX.Inv.Desktop.Infrastructure.Api;

namespace STEX.Inv.Desktop.Infrastructure.Services;

/// <summary>
/// Sube los conteos pendientes y descarga los maestros del almacén autenticado.
/// </summary>
public sealed class WarehouseSyncService : IWarehouseSyncService
{
    private readonly ISyncApi _syncApi;
    private readonly ILocalStore _localStore;
    private readonly IPinHasher _pinHasher;
    private readonly IAuthService _authService;

    public WarehouseSyncService(
        ISyncApi syncApi,
        ILocalStore localStore,
        IPinHasher pinHasher,
        IAuthService authService)
    {
        _syncApi = syncApi;
        _localStore = localStore;
        _pinHasher = pinHasher;
        _authService = authService;
    }

    public async Task<SyncResult> SyncAsync(
        IReadOnlyCollection<Guid>? libroLocalIds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var upload = await UploadPendingAsync(libroLocalIds, cancellationToken);
            if (!upload.IsSuccess)
            {
                // Aunque el envío falle, se intenta refrescar el catálogo para que
                // una campaña cerrada o un permiso cambiado se refleje en la UI.
                try
                {
                    await DownloadCatalogsAsync(cancellationToken);
                }
                catch (Exception)
                {
                    // El error de descarga no debe ocultar el error del envío.
                }

                return await FailAsync(
                    upload.ErrorMessage ?? "No se pudieron subir los conteos pendientes.",
                    SyncStatus.Failed,
                    cancellationToken,
                    upload.Code);
            }

            var download = await DownloadCatalogsAsync(cancellationToken);
            if (!download.IsSuccess)
            {
                return await FailAsync(
                    download.ErrorMessage ?? "El servidor devolvió una respuesta de sincronización inválida.",
                    SyncStatus.Failed,
                    cancellationToken,
                    download.Code);
            }

            return new SyncResult(
                SyncStatus.Success,
                null,
                download.Operarios,
                upload.UploadedCount,
                upload.UploadedBooks);
        }
        catch (UnauthorizedAccessException ex)
        {
            return await FailAsync(
                ex.Message,
                SyncStatus.Unauthorized,
                cancellationToken,
                "SESSION_EXPIRED");
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return await FailAsync(
                "La sesión del almacén venció y no se pudo renovar. Vuelve a vincular el equipo.",
                SyncStatus.Unauthorized,
                cancellationToken,
                "SESSION_EXPIRED");
        }
        catch (ApiException ex)
        {
            var (code, message) = ReadApiError(ex);
            return await FailAsync(
                message ?? $"El servidor respondió con un error ({(int)ex.StatusCode}). Los conteos pendientes se conservan.",
                SyncStatus.Failed,
                cancellationToken,
                code ?? "SERVER_ERROR");
        }
        catch (HttpRequestException)
        {
            return await FailAsync(
                "No se pudo conectar con el servidor. Los conteos pendientes se conservan.",
                SyncStatus.Failed,
                cancellationToken,
                "NETWORK_ERROR");
        }
        catch (TaskCanceledException)
        {
            return await FailAsync(
                "La sincronización tardó demasiado. Los conteos pendientes se conservan.",
                SyncStatus.Failed,
                cancellationToken,
                "TIMEOUT");
        }
        catch (Exception)
        {
            return await FailAsync(
                "No se pudo completar la sincronización. Los conteos pendientes se conservan.",
                SyncStatus.Failed,
                cancellationToken,
                "SYNC_ERROR");
        }
    }

    private async Task<DownloadOutcome> DownloadCatalogsAsync(CancellationToken cancellationToken)
    {
        var response = await ExecuteAuthorizedAsync(
            () => _syncApi.DownloadAsync(cancellationToken));

        if (response is null || !response.Success || response.Data is null)
        {
            return DownloadOutcome.Fail(
                "El servidor devolvió una respuesta de sincronización inválida.",
                "SERVER_ERROR");
        }

        var data = response.Data;
        var almacen = data.Almacenes.FirstOrDefault();

        var operarios = data.Operarios
            .Where(o => o.Id != Guid.Empty && !string.IsNullOrWhiteSpace(o.NombreCompleto))
            .Select(o => MapOperario(o, almacen))
            .ToList();

        var state = await _localStore.LoadAsync(cancellationToken);

        state.Operarios = operarios;
        state.Campanas = data.Campanas
            .Where(c => c.Id != Guid.Empty && !string.IsNullOrWhiteSpace(c.Nombre))
            .Select(c => new CampanaResumen
            {
                Id = c.Id,
                Nombre = c.Nombre,
                FechaInicio = c.FechaInicio,
                FechaFin = c.FechaFin,
                Estado = string.IsNullOrWhiteSpace(c.Estado) ? "Abierta" : c.Estado.Trim()
            })
            .ToList();
        state.Materiales = data.Materiales
            .Where(m => m.Id != Guid.Empty && !string.IsNullOrWhiteSpace(m.Descripcion))
            .Select(m => new Material
            {
                Id = m.Id,
                CodigoInterno = (m.CodigoInterno ?? string.Empty).Trim(),
                KardexNumero = m.KardexNumero?.Trim(),
                Descripcion = (m.Descripcion ?? string.Empty).Trim(),
                Unidad = (m.Unidad ?? string.Empty).Trim(),
                Tipo = m.Tipo?.Trim()
            })
            .ToList();
        state.MaterialesCount = state.Materiales.Count;
        state.LastSyncUtc = DateTimeOffset.UtcNow;
        state.LastSyncError = null;
        state.LastSyncErrorCode = null;

        if (state.Device is not null && almacen is not null)
        {
            state.Device.AlmacenId = almacen.Id;
            state.Device.AlmacenNombre = almacen.NombreLocal ?? almacen.RazonSocial ?? state.Device.AlmacenNombre;
            state.Device.AlmacenCodigo = almacen.Codigo;
        }

        await _localStore.SaveAsync(state, cancellationToken);

        return DownloadOutcome.Ok(operarios.Count);
    }

    private async Task<UploadOutcome> UploadPendingAsync(
        IReadOnlyCollection<Guid>? libroLocalIds,
        CancellationToken cancellationToken)
    {
        var state = await _localStore.LoadAsync(cancellationToken);

        if (LocalBookMigration.EnsureLocalBooks(state))
        {
            await _localStore.SaveAsync(state, cancellationToken);
        }

        var selectedIds = libroLocalIds?.ToHashSet();
        var uploadableBooks = state.Libros
            .Where(b => b.Estado == EstadoLibroLocal.Borrador)
            .Where(b => selectedIds is null || selectedIds.Contains(b.Id))
            .Where(b => state.Conteos.Any(c => c.LibroLocalId == b.Id && c.UploadedAtUtc is null))
            .Where(b => !state.Conteos.Any(c => c.LibroLocalId == b.Id && c.UploadedAtUtc is not null))
            .ToList();

        if (uploadableBooks.Count == 0)
        {
            return UploadOutcome.Ok(0, 0);
        }

        var uploadableIds = uploadableBooks.Select(b => b.Id).ToHashSet();
        var pending = state.Conteos
            .Where(c => c.UploadedAtUtc is null && uploadableIds.Contains(c.LibroLocalId))
            .ToList();

        var almacenId = state.Device?.AlmacenId;
        if (almacenId is null || almacenId == Guid.Empty)
        {
            return UploadOutcome.Fail(
                "No se pudo identificar el almacén. Vuelve a sincronizar el equipo.",
                "WAREHOUSE_MISSING");
        }

        if (pending.Any(c => c.OperarioId == Guid.Empty))
        {
            return UploadOutcome.Fail(
                "Hay un conteo sin operario. Vuelve a registrar ese conteo.",
                "OPERATOR_INVALID");
        }

        var uploadedCount = 0;
        var uploadedBooks = 0;

        foreach (var book in uploadableBooks)
        {
            var batchLines = pending
                .Where(c => c.LibroLocalId == book.Id)
                .ToList();
            if (batchLines.Count == 0)
            {
                continue;
            }

            var validation = ValidateBook(state, book);
            if (validation is { } problem)
            {
                return UploadOutcome.Fail(problem.Message, problem.Code);
            }

            if (book.LibroRemotoId is null)
            {
                book.LibroRemotoId = Guid.NewGuid();
                await _localStore.SaveAsync(state, cancellationToken);
            }

            var remoteLibroId = book.LibroRemotoId.Value;
            var request = new SyncUploadRequestDto
            {
                LibroId = remoteLibroId,
                CampanaId = book.CampanaId,
                AlmacenId = almacenId.Value,
                OperarioId = book.OperarioId,
                Detalles = batchLines
                    .Select(l => new SyncUploadDetalleDto
                    {
                        MaterialId = l.MaterialId,
                        CantidadUnidad = l.Unidades,
                        PesoNeto = l.PesoNeto,
                        Observacion = string.IsNullOrWhiteSpace(l.Observacion)
                            ? null
                            : l.Observacion.Trim(),
                        Presentacion = string.IsNullOrWhiteSpace(l.Presentacion)
                            ? null
                            : l.Presentacion.Trim()
                    })
                    .ToList()
            };

            var response = await ExecuteAuthorizedAsync(
                () => _syncApi.UploadAsync(request, cancellationToken));

            if (response is null || !response.Success)
            {
                return UploadOutcome.Fail(
                    response?.Error ?? "El servidor no confirmó la subida del conteo.",
                    response?.Code ?? "SYNC_ERROR");
            }

            // Se recarga el estado después de la llamada porque renovar la sesión
            // pudo haber guardado una expiración nueva del dispositivo.
            var latest = await _localStore.LoadAsync(cancellationToken);
            var latestBook = latest.Libros.FirstOrDefault(b => b.Id == book.Id);
            var lineIds = batchLines.Select(l => l.Id).ToHashSet();
            var uploadedAt = DateTimeOffset.UtcNow;
            var marked = 0;

            foreach (var line in latest.Conteos.Where(
                         c => lineIds.Contains(c.Id) && c.UploadedAtUtc is null))
            {
                line.UploadedAtUtc = uploadedAt;
                line.LibroId = response.LibroId ?? remoteLibroId;
                marked++;
            }

            if (latestBook is not null)
            {
                latestBook.LibroRemotoId = response.LibroId ?? remoteLibroId;
                latestBook.EnviadoAtUtc = uploadedAt;
                latestBook.Estado = latest.Conteos.Any(
                    c => c.LibroLocalId == book.Id && c.UploadedAtUtc is null)
                    ? EstadoLibroLocal.Parcial
                    : EstadoLibroLocal.Enviado;
            }

            if (marked == 0 && latestBook is null)
            {
                continue;
            }

            await _localStore.SaveAsync(latest, cancellationToken);
            uploadedCount += marked;
            uploadedBooks++;
        }

        return UploadOutcome.Ok(uploadedCount, uploadedBooks);
    }

    private async Task<T> ExecuteAuthorizedAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var refresh = await _authService.RefreshAsync();
            if (!refresh.IsSuccess)
            {
                throw new UnauthorizedAccessException(
                    refresh.ErrorMessage
                    ?? "La sesión del almacén venció. Vuelve a vincular el equipo.");
            }

            return await action();
        }
    }

    private Operario MapOperario(SyncOperarioDto dto, SyncAlmacenDto? almacen)
    {
        var operario = new Operario
        {
            Id = dto.Id,
            NombreCompleto = dto.NombreCompleto.Trim(),
            AlmacenId = dto.AlmacenId != Guid.Empty ? dto.AlmacenId : almacen?.Id ?? Guid.Empty,
            Activo = true
        };

        var pin = dto.PinSeguridad?.Trim();
        if (!string.IsNullOrEmpty(pin))
        {
            operario.PinSalt = _pinHasher.CreateSalt();
            operario.PinHash = _pinHasher.Hash(pin, operario.PinSalt);
        }

        return operario;
    }

    private static (string? Code, string? Message) ReadApiError(ApiException ex)
    {
        if (string.IsNullOrWhiteSpace(ex.Content))
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(ex.Content);
            var root = document.RootElement;
            var code = root.TryGetProperty("code", out var codeElement)
                ? codeElement.GetString()
                : null;
            var message = root.TryGetProperty("error", out var errorElement)
                ? errorElement.GetString()
                : null;

            return (code, message);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static (string Code, string Message)? ValidateBook(LocalState state, LibroLocal book)
    {
        var campana = state.Campanas.FirstOrDefault(c => c.Id == book.CampanaId);
        if (campana is null)
        {
            return ("CAMPAIGN_NOT_FOUND",
                "La campaña del libro ya no está disponible. Actualiza los datos maestros.");
        }

        if (!string.Equals(campana.Estado, "Abierta", StringComparison.OrdinalIgnoreCase))
        {
            return ("CAMPAIGN_CLOSED",
                "La campaña del libro está cerrada. Elige una campaña abierta.");
        }

        var now = DateTimeOffset.UtcNow;
        if (campana.FechaInicio is { } inicio && now < inicio)
        {
            return ("CAMPAIGN_NOT_STARTED",
                "La campaña del libro todavía no ha comenzado.");
        }

        if (campana.FechaFin is { } fin && now > fin)
        {
            return ("CAMPAIGN_FINISHED",
                "La campaña del libro ya terminó.");
        }

        var operario = state.Operarios.FirstOrDefault(o => o.Id == book.OperarioId);
        if (operario is null || !operario.Activo)
        {
            return ("OPERATOR_NOT_ALLOWED",
                "El operario del libro no está activo en este almacén.");
        }

        var almacenId = state.Device?.AlmacenId;
        if (almacenId is { } id
            && id != Guid.Empty
            && operario.AlmacenId != Guid.Empty
            && operario.AlmacenId != id)
        {
            return ("OPERATOR_NOT_ALLOWED",
                "El operario del libro no pertenece al almacén del equipo.");
        }

        return null;
    }

    private async Task<SyncResult> FailAsync(
        string message,
        SyncStatus status,
        CancellationToken cancellationToken,
        string? code = null)
    {
        try
        {
            var state = await _localStore.LoadAsync(cancellationToken);
            state.LastSyncError = message;
            state.LastSyncErrorCode = code;
            await _localStore.SaveAsync(state, cancellationToken);
        }
        catch (Exception)
        {
            // El error de persistencia no debe ocultar el error original.
        }

        return new SyncResult(status, message, Code: code);
    }

    private sealed record DownloadOutcome(
        bool IsSuccess,
        int Operarios = 0,
        string? ErrorMessage = null,
        string? Code = null)
    {
        public static DownloadOutcome Ok(int operarios) =>
            new(true, operarios);

        public static DownloadOutcome Fail(string message, string? code = null) =>
            new(false, 0, message, code);
    }

    private sealed record UploadOutcome(
        bool IsSuccess,
        int UploadedCount,
        int UploadedBooks,
        string? ErrorMessage = null,
        string? Code = null)
    {
        public static UploadOutcome Ok(int uploadedCount, int uploadedBooks) =>
            new(true, uploadedCount, uploadedBooks);

        public static UploadOutcome Fail(string message, string? code = null) =>
            new(false, 0, 0, message, code);
    }
}
