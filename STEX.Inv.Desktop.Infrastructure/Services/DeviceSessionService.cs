using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Infrastructure.Services;

public sealed class DeviceSessionService : IDeviceSessionService
{
    private readonly ILocalStore _localStore;
    private readonly ISecureStorageService _secureStorage;

    public DeviceSessionService(ILocalStore localStore, ISecureStorageService secureStorage)
    {
        _localStore = localStore;
        _secureStorage = secureStorage;
    }

    public async Task<DeviceSession?> GetAsync(CancellationToken cancellationToken = default)
    {
        var state = await _localStore.LoadAsync(cancellationToken);
        return state.Device;
    }

    public async Task SaveAsync(DeviceSession session, CancellationToken cancellationToken = default)
    {
        var state = await _localStore.LoadAsync(cancellationToken);
        var previousWarehouse = state.Device?.AlmacenId;

        // Si el equipo se vincula a otro almacén, la caché anterior deja de ser válida.
        if (previousWarehouse is { } previous
            && session.AlmacenId is { } current
            && previous != current)
        {
            state.Operarios.Clear();
            state.Campanas.Clear();
            state.Materiales.Clear();
            state.Conteos.Clear();
            state.PinAttempts.Clear();
            state.MaterialesCount = 0;
            state.LastSyncUtc = null;
            state.LastSyncError = null;
        }

        state.Device = session;
        await _localStore.SaveAsync(state, cancellationToken);
    }

    public async Task UnbindAsync(CancellationToken cancellationToken = default)
    {
        await _localStore.ClearAsync(cancellationToken);
        await _secureStorage.ClearTokenAsync();
    }
}
