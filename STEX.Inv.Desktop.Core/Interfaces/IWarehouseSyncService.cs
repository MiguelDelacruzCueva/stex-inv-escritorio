using STEX.Inv.Desktop.Core.DTOs;

namespace STEX.Inv.Desktop.Core.Interfaces;

/// <summary>
/// Sube los conteos seleccionados (o todos si no se indica selección) y
/// descarga los maestros del almacén autenticado.
/// </summary>
public interface IWarehouseSyncService
{
    Task<SyncResult> SyncAsync(
        IReadOnlyCollection<Guid>? libroLocalIds = null,
        CancellationToken cancellationToken = default);
}
