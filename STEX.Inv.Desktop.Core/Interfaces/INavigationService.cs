using STEX.Inv.Desktop.Core.ViewModels;

namespace STEX.Inv.Desktop.Core.Interfaces;

public interface INavigationService
{
    Task NavigateToAsync<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase;
}
