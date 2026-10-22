namespace STEX.Inv.Desktop.Infrastructure.Services;

using System.Threading.Tasks;
using STEX.Inv.Desktop.Core.DTOs;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Infrastructure.Api;

public class AuthService : IAuthService
{
    private readonly IAuthApi _authApi;
    private readonly ISecureStorageService _secureStorageService;

    public AuthService(IAuthApi authApi, ISecureStorageService secureStorageService)
    {
        _authApi = authApi;
        _secureStorageService = secureStorageService;
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            var request = new LoginRequestDto { Email = email, Password = password };
            var response = await _authApi.LoginAsync(request);

            if (!string.IsNullOrWhiteSpace(response.AccessToken))
            {
                await _secureStorageService.SaveTokenAsync(response.AccessToken);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        await _secureStorageService.ClearTokenAsync();
    }
}