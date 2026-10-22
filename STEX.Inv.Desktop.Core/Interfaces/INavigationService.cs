using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using STEX.Inv.Desktop.Core.ViewModels;

namespace STEX.Inv.Desktop.Core.Interfaces;

public interface INavigationService
{
    void NavigateTo<TViewModel>() where TViewModel : ObservableObject;
}
