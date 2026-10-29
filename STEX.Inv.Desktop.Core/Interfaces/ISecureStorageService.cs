using System;
using System.Collections.Generic;
using System.Text;

namespace STEX.Inv.Desktop.Core.Interfaces;

public interface ISecureStorageService
{
    Task SaveTokenAsync(string token);
    Task<string?> GetTokenAsync();
    Task ClearTokenAsync();

    Task SaveRefreshTokenAsync(string token);
    Task<string?> GetRefreshTokenAsync();
    Task ClearRefreshTokenAsync();
}
