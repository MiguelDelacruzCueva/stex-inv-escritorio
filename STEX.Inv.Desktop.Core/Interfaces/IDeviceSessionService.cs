using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Core.Interfaces;

public interface IDeviceSessionService
{
    Task<DeviceSession?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DeviceSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina token, sesión de equipo, operarios y bloqueos de PIN.
    /// </summary>
    Task UnbindAsync(CancellationToken cancellationToken = default);
}
