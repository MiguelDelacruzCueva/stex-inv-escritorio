using STEX.Inv.Desktop.Core.DTOs;

namespace STEX.Inv.Desktop.Core.Interfaces;

public interface IAuthService
{
    /// <summary>
    /// Autentica al usuario contra la API y, si tiene éxito, guarda el token de forma segura.
    /// </summary>
    Task<LoginResult> LoginAsync(string email, string password);

    /// <summary>
    /// Renueva el token de acceso con el refresh token guardado.
    /// </summary>
    Task<LoginResult> RefreshAsync();
}
