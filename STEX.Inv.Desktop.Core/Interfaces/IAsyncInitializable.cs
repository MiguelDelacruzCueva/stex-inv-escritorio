namespace STEX.Inv.Desktop.Core.Interfaces;

/// <summary>
/// ViewModel que necesita cargar datos antes de mostrarse.
/// </summary>
public interface IAsyncInitializable
{
    Task InitializeAsync(object? parameter = null);
}
