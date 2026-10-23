using CommunityToolkit.Mvvm.ComponentModel;
using STEX.Inv.Desktop.Core.Interfaces;

namespace STEX.Inv.Desktop.Core.ViewModels;

/// <summary>
/// Host de navegación raíz: alterna entre enrolamiento, selección de operario,
/// PIN y shell. No conoce las vistas XAML (los DataTemplate hacen el mapeo).
/// </summary>
public partial class MainViewModel : ViewModelBase, INavigationService
{
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    public MainViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task NavigateToAsync<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase
    {
        if (_serviceProvider.GetService(typeof(TViewModel)) is not TViewModel viewModel)
        {
            throw new InvalidOperationException(
                $"El ViewModel '{typeof(TViewModel).Name}' no está registrado en el contenedor DI.");
        }

        CurrentViewModel = viewModel;
        await viewModel.InitializeAsync(parameter);
    }
}
