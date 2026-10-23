using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Core.Interfaces;

/// <summary>
/// Persistencia local cifrada del equipo (DPAPI).
/// </summary>
public interface ILocalStore
{
    Task<LocalState> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(LocalState state, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
