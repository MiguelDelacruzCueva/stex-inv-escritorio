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
    private readonly IDeviceSessionService _deviceSessionService;

    public AuthService(
        IAuthApi authApi,
        ISecureStorageService secureStorageService,
        IDeviceSessionService deviceSessionService)
    {
        _authApi = authApi;
        _secureStorageService = secureStorageService;
        _deviceSessionService = deviceSessionService;
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

            if (string.IsNullOrWhiteSpace(response.RefreshToken))
            {
                await _secureStorageService.ClearRefreshTokenAsync();
            }
            else
            {
                await _secureStorageService.SaveRefreshTokenAsync(response.RefreshToken);
            }

            return LoginResult.Success(
                response.User,
                ResolveExpiration(response));
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

    public async Task<LoginResult> RefreshAsync()
    {
        try
        {
            var refreshToken = await _secureStorageService.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return LoginResult.Failure(
                    LoginStatus.InvalidCredentials,
                    "La sesión venció. Vuelve a vincular el equipo.");
            }

            var response = await _authApi.RefreshAsync(
                new RefreshRequestDto { RefreshToken = refreshToken });

            if (string.IsNullOrWhiteSpace(response.AccessToken)
                || string.IsNullOrWhiteSpace(response.RefreshToken))
            {
                return LoginResult.Failure(
                    LoginStatus.UnexpectedError,
                    "El servidor no devolvió una sesión válida.");
            }

            await _secureStorageService.SaveTokenAsync(response.AccessToken);
            await _secureStorageService.SaveRefreshTokenAsync(response.RefreshToken);

            var expiresAt = ResolveExpiration(response);
            var device = await _deviceSessionService.GetAsync();
            if (device is not null)
            {
                device.ExpiresAtUtc = expiresAt ?? device.ExpiresAtUtc;
                await _deviceSessionService.SaveAsync(device);
            }

            return LoginResult.Success(null, expiresAt);
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized
                                                    or HttpStatusCode.Forbidden
                                                    or HttpStatusCode.BadRequest)
        {
            return LoginResult.Failure(
                LoginStatus.InvalidCredentials,
                "La sesión venció. Vuelve a vincular el equipo.");
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
                "No se pudo conectar con el servidor.");
        }
        catch (TaskCanceledException)
        {
            return LoginResult.Failure(
                LoginStatus.ConnectionError,
                "La renovación de la sesión tardó demasiado.");
        }
    }

    private static DateTimeOffset? ResolveExpiration(AuthResponseDto response)
    {
        var jwtExpiration = JwtUtils.GetExpirationUtc(response.AccessToken);
        if (jwtExpiration is not null)
        {
            return jwtExpiration;
        }

        return response.ExpiresAt is { } unixSeconds
            ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
            : null;
    }
}
