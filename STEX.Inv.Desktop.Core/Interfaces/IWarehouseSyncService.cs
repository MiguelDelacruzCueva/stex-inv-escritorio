using STEX.Inv.Desktop.Core.DTOs;

namespace STEX.Inv.Desktop.Core.Interfaces;

/// <summary>
/// Sube los conteos pendientes y descarga los maestros del almacén autenticado.
/// </summary>
public interface IWarehouseSyncService
{
    Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default);
}
