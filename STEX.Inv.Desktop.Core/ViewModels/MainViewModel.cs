using CommunityToolkit.Mvvm.ComponentModel;
using STEX.Inv.Desktop.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using STEX.Inv.Desktop.Core.Interfaces;

namespace STEX.Inv.Desktop.Core.ViewModels;

public partial class MainViewModel : ViewModelBase, INavigationService
{
    private readonly IServiceProvider _serviceProvider;

    // Esta propiedad contendrá la pantalla activa (Login, Dashboard, Catálogo)
    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    public MainViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    // Implementación de la interfaz de navegación desacoplada
    public void NavigateTo<TViewModel>() where TViewModel : ObservableObject
    {
        var viewModel = _serviceProvider.GetService(typeof(TViewModel)) as ViewModelBase;
        if (viewModel != null)
        {
            CurrentViewModel = viewModel;
        }
    }
}
