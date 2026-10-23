namespace STEX.Inv.Desktop.Core.DTOs;

/// <summary>
/// Resultado tipado de un intento de inicio de sesión. Permite distinguir
/// credenciales inválidas de fallos de red sin usar excepciones como control de flujo.
/// </summary>
public enum LoginStatus
{
    Success,
    InvalidCredentials,
    ConnectionError,
    UnexpectedError
}

public sealed record LoginResult(
    LoginStatus Status,
    string? ErrorMessage = null,
    UserProfileDto? User = null,
    DateTimeOffset? ExpiresAtUtc = null)
{
    public bool IsSuccess => Status == LoginStatus.Success;

    public static LoginResult Success(UserProfileDto? user, DateTimeOffset? expiresAtUtc) =>
        new(LoginStatus.Success, null, user, expiresAtUtc);

    public static LoginResult Failure(LoginStatus status, string errorMessage) =>
        new(status, errorMessage);
}
