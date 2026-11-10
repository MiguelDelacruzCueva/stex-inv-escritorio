using System.Windows;
using STEX.Inv.Desktop.Core.Interfaces;

namespace STEX.Inv.Desktop.UI.Views;

public partial class SelectionDialog : Window
{
    public SelectionDialog(string title, string message, IReadOnlyList<SelectionItem> items)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        ItemsList.ItemsSource = items;
    }

    public Guid? SelectedId { get; private set; }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        if (ItemsList.SelectedItem is not SelectionItem item)
        {
            MessageBox.Show(
                this,
                "Selecciona una opción para continuar.",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SelectedId = item.Id;
        DialogResult = true;
    }
}
