using CommunityToolkit.Mvvm.ComponentModel;
using STEX.Inv.Desktop.Core.Interfaces;

namespace STEX.Inv.Desktop.Core.ViewModels;

public abstract class ViewModelBase : ObservableValidator, IAsyncInitializable
{
    public virtual Task InitializeAsync(object? parameter = null) => Task.CompletedTask;
}
