namespace STEX.Inv.Desktop.Core.Interfaces;

/// <summary>
/// Opción de un diálogo de selección. Las opciones deshabilitadas se muestran
/// con su motivo, pero no se pueden elegir.
/// </summary>
public sealed record SelectionItem(
    Guid Id,
    string Title,
    string? Subtitle = null,
    bool IsEnabled = true);

/// <summary>
/// Diálogos de confirmación y selección. La implementación vive en la capa de UI.
/// </summary>
public interface IDialogService
{
    bool Confirm(string title, string message);

    Guid? SelectItem(string title, string message, IReadOnlyList<SelectionItem> items);
}
