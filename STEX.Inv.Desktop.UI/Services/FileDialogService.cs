using Microsoft.Win32;
using STEX.Inv.Desktop.Core.Interfaces;

namespace STEX.Inv.Desktop.UI.Services;

public sealed class FileDialogService : IFileDialogService
{
    public string? PickFolder(string title, string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = initialDirectory
                ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
