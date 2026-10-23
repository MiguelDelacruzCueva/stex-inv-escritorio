namespace STEX.Inv.Desktop.Infrastructure.Services;

using System.Net;
using System.Threading.Tasks;
using Refit;
using STEX.Inv.Desktop.Core.DTOs;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Infrastructure.Api;
using STEX.Inv.Desktop.Infrastructure.Security;

public class AuthService : IAuthService
{
    private readonly IAuthApi _authApi;
    private readonly ISecureStorageService _secureStorageService;

    public AuthService(IAuthApi authApi, ISecureStorageService secureStorageService)
    {
        _authApi = authApi;
        _secureStorageService = secureStorageService;
    }

    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        try
        {
            var request = new LoginRequestDto { Email = email, Password = password };
            var response = await _authApi.LoginAsync(request);

            if (string.IsNullOrWhiteSpace(response.AccessToken))
            {
                return LoginResult.Failure(
                    LoginStatus.UnexpectedError,
                    "El servidor no devolvió un token de sesión válido.");
            }

            await _secureStorageService.SaveTokenAsync(response.AccessToken);

            return LoginResult.Success(
                response.User,
                JwtUtils.GetExpirationUtc(response.AccessToken));
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized
                                                    or HttpStatusCode.Forbidden
                                                    or HttpStatusCode.BadRequest)
        {
            return LoginResult.Failure(LoginStatus.InvalidCredentials, "Credenciales inválidas.");
        }
        catch (ApiException ex)
        {
            return LoginResult.Failure(
                LoginStatus.ConnectionError,
                $"El servidor respondió con un error ({(int)ex.StatusCode}).");
        }
        catch (HttpRequestException)
        {
            return LoginResult.Failure(
                LoginStatus.ConnectionError,
                "No se pudo conectar con el servidor. Verifica tu conexión.");
        }
        catch (TaskCanceledException)
        {
            return LoginResult.Failure(
                LoginStatus.ConnectionError,
                "La operación tardó demasiado. Inténtalo de nuevo.");
        }
    }
}
