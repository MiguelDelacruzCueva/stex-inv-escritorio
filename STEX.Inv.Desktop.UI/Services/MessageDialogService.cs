using System.Windows;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.UI.Views;

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

    public Guid? SelectItem(string title, string message, IReadOnlyList<SelectionItem> items)
    {
        var dialog = new SelectionDialog(title, message, items);

        if (Application.Current?.MainWindow is { } owner
            && !ReferenceEquals(owner, dialog))
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true ? dialog.SelectedId : null;
    }
}
