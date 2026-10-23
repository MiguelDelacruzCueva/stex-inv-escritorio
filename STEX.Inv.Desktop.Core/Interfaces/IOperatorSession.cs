using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Core.Interfaces;

/// <summary>
/// Operario autenticado en esta sesión de trabajo (se pierde al cerrar la app).
/// </summary>
public interface IOperatorSession
{
    Operario? Current { get; }

    bool IsSignedIn { get; }

    void SignIn(Operario operario);

    void SignOut();
}
