using System.Net;
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

    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var upload = await UploadPendingAsync(cancellationToken);
            if (!upload.IsSuccess)
            {
                return await FailAsync(
                    upload.ErrorMessage ?? "No se pudieron subir los conteos pendientes.",
                    SyncStatus.Failed,
                    cancellationToken);
            }

            var response = await ExecuteAuthorizedAsync(
                () => _syncApi.DownloadAsync(cancellationToken));

            if (response is null || !response.Success || response.Data is null)
            {
                return await FailAsync(
                    "El servidor devolvió una respuesta de sincronización inválida.",
                    SyncStatus.Failed,
                    cancellationToken);
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
                    FechaFin = c.FechaFin
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
                    Tipo = m.Tipo?.Trim()
                })
                .ToList();
            state.MaterialesCount = state.Materiales.Count;
            state.LastSyncUtc = DateTimeOffset.UtcNow;
            state.LastSyncError = null;

            if (state.Device is not null && almacen is not null)
            {
                state.Device.AlmacenId = almacen.Id;
                state.Device.AlmacenNombre = almacen.NombreLocal ?? almacen.RazonSocial ?? state.Device.AlmacenNombre;
                state.Device.AlmacenCodigo = almacen.Codigo;
            }

            await _localStore.SaveAsync(state, cancellationToken);

            return new SyncResult(
                SyncStatus.Success,
                null,
                operarios.Count,
                upload.UploadedCount,
                upload.UploadedBooks);
        }
        catch (UnauthorizedAccessException ex)
        {
            return await FailAsync(
                ex.Message,
                SyncStatus.Unauthorized,
                cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return await FailAsync(
                "La sesión del almacén venció y no se pudo renovar. Vuelve a vincular el equipo.",
                SyncStatus.Unauthorized,
                cancellationToken);
        }
        catch (ApiException ex)
        {
            return await FailAsync(
                $"El servidor respondió con un error ({(int)ex.StatusCode}). Los conteos pendientes se conservan.",
                SyncStatus.Failed,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return await FailAsync(
                "No se pudo conectar con el servidor. Los conteos pendientes se conservan.",
                SyncStatus.Failed,
                cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return await FailAsync(
                "La sincronización tardó demasiado. Los conteos pendientes se conservan.",
                SyncStatus.Failed,
                cancellationToken);
        }
        catch (Exception)
        {
            return await FailAsync(
                "No se pudo completar la sincronización. Los conteos pendientes se conservan.",
                SyncStatus.Failed,
                cancellationToken);
        }
    }

    private async Task<UploadOutcome> UploadPendingAsync(CancellationToken cancellationToken)
    {
        var state = await _localStore.LoadAsync(cancellationToken);
        var pending = state.Conteos
            .Where(c => c.UploadedAtUtc is null)
            .ToList();

        if (pending.Count == 0)
        {
            return UploadOutcome.Ok(0, 0);
        }

        var almacenId = state.Device?.AlmacenId;
        if (almacenId is null || almacenId == Guid.Empty)
        {
            return UploadOutcome.Fail(
                "No se pudo identificar el almacén. Vuelve a sincronizar el equipo.");
        }

        // Un libro remoto por campaña y por lote pendiente. Agrupar por LibroId
        // conserva el mismo identificador cuando un envío anterior quedó a medias.
        var batches = pending
            .GroupBy(c => new { c.CampanaId, c.LibroId })
            .Select(g => new UploadBatch(
                g.Key.CampanaId,
                g.Key.LibroId ?? Guid.NewGuid(),
                g.ToList()))
            .ToList();

        var uploadedCount = 0;
        var uploadedBooks = 0;

        foreach (var batch in batches)
        {
            if (batch.Lines.Any(l => l.LibroId != batch.LibroId))
            {
                var pendingState = await _localStore.LoadAsync(cancellationToken);
                var pendingIds = batch.Lines.Select(l => l.Id).ToHashSet();
                var linesToMark = pendingState.Conteos
                    .Where(c => pendingIds.Contains(c.Id))
                    .ToList();

                if (linesToMark.Count > 0)
                {
                    foreach (var line in linesToMark)
                    {
                        line.LibroId = batch.LibroId;
                    }

                    await _localStore.SaveAsync(pendingState, cancellationToken);
                }
            }

            var request = new SyncUploadRequestDto
            {
                LibroId = batch.LibroId,
                CampanaId = batch.CampanaId,
                AlmacenId = almacenId.Value,
                Detalles = batch.Lines
                    .Select(l => new SyncUploadDetalleDto
                    {
                        MaterialId = l.MaterialId,
                        CantidadUnidad = l.Unidades,
                        PesoNeto = l.PesoNeto,
                        Observacion = string.IsNullOrWhiteSpace(l.Observacion)
                            ? null
                            : l.Observacion.Trim()
                    })
                    .ToList()
            };

            var response = await ExecuteAuthorizedAsync(
                () => _syncApi.UploadAsync(request, cancellationToken));

            if (response is null || !response.Success)
            {
                return UploadOutcome.Fail(
                    response?.Error ?? "El servidor no confirmó la subida del conteo.");
            }

            // Se recarga el estado después de la llamada porque renovar la sesión
            // pudo haber guardado una expiración nueva del dispositivo.
            var latest = await _localStore.LoadAsync(cancellationToken);
            var lineIds = batch.Lines.Select(l => l.Id).ToHashSet();
            var uploadedAt = DateTimeOffset.UtcNow;
            var marked = 0;

            foreach (var line in latest.Conteos.Where(
                         c => lineIds.Contains(c.Id) && c.UploadedAtUtc is null))
            {
                line.UploadedAtUtc = uploadedAt;
                if (response.LibroId is { } remoteLibroId)
                {
                    line.LibroId = remoteLibroId;
                }

                marked++;
            }

            if (marked == 0)
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

    private async Task<SyncResult> FailAsync(string message, SyncStatus status, CancellationToken cancellationToken)
    {
        try
        {
            var state = await _localStore.LoadAsync(cancellationToken);
            state.LastSyncError = message;
            await _localStore.SaveAsync(state, cancellationToken);
        }
        catch (Exception)
        {
            // El error de persistencia no debe ocultar el error original.
        }

        return new SyncResult(status, message);
    }

    private sealed record UploadBatch(Guid CampanaId, Guid LibroId, List<ConteoLinea> Lines);

    private sealed record UploadOutcome(
        bool IsSuccess,
        int UploadedCount,
        int UploadedBooks,
        string? ErrorMessage = null)
    {
        public static UploadOutcome Ok(int uploadedCount, int uploadedBooks) =>
            new(true, uploadedCount, uploadedBooks);

        public static UploadOutcome Fail(string message) =>
            new(false, 0, 0, message);
    }
}
