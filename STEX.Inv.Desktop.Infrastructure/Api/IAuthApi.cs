namespace STEX.Inv.Desktop.Infrastructure.Api;

using Refit;
using STEX.Inv.Desktop.Core.DTOs;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public interface IAuthApi
{
    [Post("/api/auth/login")]
    Task<AuthResponseDto> LoginAsync([Body] LoginRequestDto request);

    [Post("/api/auth/refresh")]
    Task<AuthResponseDto> RefreshAsync([Body] RefreshRequestDto request);
}