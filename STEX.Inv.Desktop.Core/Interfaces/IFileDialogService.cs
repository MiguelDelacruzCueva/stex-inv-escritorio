namespace STEX.Inv.Desktop.Core.Interfaces;

/// <summary>
/// Selección de carpetas para exportar archivos locales.
/// </summary>
public interface IFileDialogService
{
    string? PickFolder(string title, string? initialDirectory = null);
}
