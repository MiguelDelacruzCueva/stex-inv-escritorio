using System.Windows;
using STEX.Inv.Desktop.Core.Interfaces;

namespace STEX.Inv.Desktop.UI.Services;

public sealed class MessageDialogService : IDialogService
{
    public bool Confirm(string title, string message)
    {
        return MessageBox.Show(
                   message,
                   title,
                   MessageBoxButton.YesNo,
                   MessageBoxImage.Question,
                   MessageBoxResult.No)
               == MessageBoxResult.Yes;
    }
}
