using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Core.Interfaces;

/// <summary>
/// Genera un Excel por libro con la plantilla de inventario.
/// </summary>
public interface IBookExportService
{
    Task<string> ExportBookAsync(
        BookExportData book,
        string warehouseName,
        string folder,
        CancellationToken cancellationToken = default);
}

public sealed record BookExportData(
    Guid LibroLocalId,
    string CampanaNombre,
    string OperarioNombre,
    DateTimeOffset CreadoAtUtc,
    IReadOnlyList<ConteoLinea> Lineas);
