using System;
using System.Collections.Generic;
using System.Text;

namespace STEX.Inv.Desktop.Core.Interfaces;

public interface IAuthService
{
    Task<bool> LoginAsync(string email, string password);
    Task LogoutAsync();
}