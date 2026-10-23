using CommunityToolkit.Mvvm.ComponentModel;

namespace STEX.Inv.Desktop.Core.ViewModels;

/// <summary>
/// Módulo aún no implementado. Mantiene el shell navegable sin romper el flujo.
/// </summary>
public partial class PlaceholderViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Módulo";

    [ObservableProperty]
    private string _description = "Este módulo está en preparación.";

    public override Task InitializeAsync(object? parameter = null)
    {
        if (parameter is ModuleRequest request)
        {
            Title = request.Title;
            Description = request.Description;
        }

        return Task.CompletedTask;
    }
}
