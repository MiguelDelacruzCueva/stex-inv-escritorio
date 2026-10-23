using System.Windows;
using System.Windows.Controls;
using STEX.Inv.Desktop.Core.ViewModels;

namespace STEX.Inv.Desktop.UI.Views;

public partial class EnrollmentView : UserControl
{
    public EnrollmentView()
    {
        InitializeComponent();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is EnrollmentViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.Password = passwordBox.Password;
        }
    }
}
