using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace STEX.Inv.Desktop.UI;

/// <summary>
/// Ventana principal que aloja las vistas resueltas por el ViewModel de navegación.
/// </summary>
public partial class MainWindow : Window
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            var enabled = 1;

            DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
        }
        catch (Exception)
        {
            // La barra de título oscura es cosmética: si no está disponible, se ignora.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
