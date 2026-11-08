using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Core.Interfaces;

public interface IThemeService
{
    ThemeMode Current { get; }

    Task InitializeAsync();

    Task SetThemeAsync(ThemeMode mode);
}
