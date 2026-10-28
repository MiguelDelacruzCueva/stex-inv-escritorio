using System.Net;
using System.Threading.Tasks;
using Refit;
using STEX.Inv.Desktop.Core.DTOs;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;
using STEX.Inv.Desktop.Infrastructure.Api;

namespace STEX.Inv.Desktop.Infrastructure.Services;

/// <summary>
/// Descarga los maestros del almacén autenticado y los persiste localmente.
/// </summary>
public sealed class WarehouseSyncService : IWarehouseSyncService
{
    private readonly ISyncApi _syncApi;
    private readonly ILocalStore _localStore;
    private readonly IPinHasher _pinHasher;

    public WarehouseSyncService(ISyncApi syncApi, ILocalStore localStore, IPinHasher pinHasher)
    {
        _syncApi = syncApi;
        _localStore = localStore;
        _pinHasher = pinHasher;
    }

    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _syncApi.DownloadAsync(cancellationToken);

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

            return new SyncResult(SyncStatus.Success, null, operarios.Count);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return await FailAsync(
                "La sesión del almacén venció. Un administrador debe volver a vincular el equipo.",
                SyncStatus.Unauthorized,
                cancellationToken);
        }
        catch (ApiException ex)
        {
            return await FailAsync(
                $"El servidor respondió con un error ({(int)ex.StatusCode}).",
                SyncStatus.Failed,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return await FailAsync(
                "No se pudo conectar con el servidor. Se usarán los datos locales.",
                SyncStatus.Failed,
                cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return await FailAsync(
                "La sincronización tardó demasiado.",
                SyncStatus.Failed,
                cancellationToken);
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
}
