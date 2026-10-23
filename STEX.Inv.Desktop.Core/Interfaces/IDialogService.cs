namespace STEX.Inv.Desktop.Core.Interfaces;

/// <summary>
/// Diálogos de confirmación. La implementación vive en la capa de UI.
/// </summary>
public interface IDialogService
{
    bool Confirm(string title, string message);
}
