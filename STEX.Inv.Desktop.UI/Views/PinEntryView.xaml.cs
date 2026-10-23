using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using STEX.Inv.Desktop.Core.ViewModels;

namespace STEX.Inv.Desktop.UI.Views;

public partial class PinEntryView : UserControl
{
    public PinEntryView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Keyboard.Focus(this);
    }

    /// <summary>
    /// Soporte de teclado físico y numérico. La lógica sigue en el ViewModel.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PinEntryViewModel viewModel)
        {
            return;
        }

        var digit = e.Key switch
        {
            >= Key.D0 and <= Key.D9 => (e.Key - Key.D0).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => (e.Key - Key.NumPad0).ToString(),
            _ => null
        };

        if (digit is not null)
        {
            if (viewModel.DigitCommand.CanExecute(digit))
            {
                viewModel.DigitCommand.Execute(digit);
            }

            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Back:
                if (viewModel.BackspaceCommand.CanExecute(null))
                {
                    viewModel.BackspaceCommand.Execute(null);
                }

                e.Handled = true;
                break;

            case Key.Enter:
                if (viewModel.SubmitCommand.CanExecute(null))
                {
                    viewModel.SubmitCommand.Execute(null);
                }

                e.Handled = true;
                break;

            case Key.Escape:
                if (viewModel.BackCommand.CanExecute(null))
                {
                    viewModel.BackCommand.Execute(null);
                }

                e.Handled = true;
                break;
        }
    }
}
