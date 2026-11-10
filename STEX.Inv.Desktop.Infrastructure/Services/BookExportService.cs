using System.IO;
using ClosedXML.Excel;
using STEX.Inv.Desktop.Core.Interfaces;

namespace STEX.Inv.Desktop.Infrastructure.Services;

public sealed class BookExportService : IBookExportService
{
    private static readonly string[] Headers =
    {
        "TIPO",
        "DESCRIPCION",
        "KARDEX",
        "CODIGO",
        "UNIDAD",
        "PRESENTACION",
        "PESO NETO(KG)",
        "OBSERVACION"
    };

    public Task<string> ExportBookAsync(
        BookExportData book,
        string warehouseName,
        string folder,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(folder);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Inventario");

        sheet.Cell("A1").Value = "TEJEDURIA";
        sheet.Cell("B1").Value = warehouseName;
        sheet.Cell("D1").Value = "FECHA";
        sheet.Cell("E1").Value = book.CreadoAtUtc.ToLocalTime().DateTime;
        sheet.Cell("E1").Style.DateFormat.Format = "dd/MM/yyyy";
        sheet.Cell("A2").Value = "PERSONAL DEL INVENTARIO";
        sheet.Cell("B2").Value = book.OperarioNombre;

        for (var index = 0; index < Headers.Length; index++)
        {
            sheet.Cell(5, index + 1).Value = Headers[index];
        }

        var headerRange = sheet.Range(5, 1, 5, Headers.Length);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        var row = 6;
        foreach (var line in book.Lineas)
        {
            sheet.Cell(row, 1).Value = line.Tipo;
            sheet.Cell(row, 2).Value = line.MaterialDescripcion;
            sheet.Cell(row, 3).Value = line.KardexNumero ?? string.Empty;
            sheet.Cell(row, 4).Value = line.MaterialCodigo;
            sheet.Cell(row, 5).Value = line.Unidad;
            sheet.Cell(row, 6).Value = line.Presentacion;
            sheet.Cell(row, 7).Value = line.PesoNeto;
            sheet.Cell(row, 8).Value = line.Observacion;
            row++;
        }

        var tableRange = sheet.Range(5, 1, Math.Max(5, row - 1), Headers.Length);
        tableRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.RightBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        sheet.Column(1).Width = 12;
        sheet.Column(2).Width = 34;
        sheet.Column(3).Width = 18;
        sheet.Column(4).Width = 18;
        sheet.Column(5).Width = 12;
        sheet.Column(6).Width = 15;
        sheet.Column(7).Width = 15;
        sheet.Column(8).Width = 28;

        var path = Path.Combine(folder, BuildFileName(book, warehouseName));
        workbook.SaveAs(path);

        return Task.FromResult(path);
    }

    private static string BuildFileName(BookExportData book, string warehouseName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safeWarehouse = new string(
            (string.IsNullOrWhiteSpace(warehouseName) ? "ALMACEN" : warehouseName)
            .Select(c => invalid.Contains(c) ? '_' : c)
            .ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(safeWarehouse))
        {
            safeWarehouse = "ALMACEN";
        }

        return $"{safeWarehouse}_{book.CreadoAtUtc.ToLocalTime():yyyyMMdd_HHmm}_" +
               $"{book.LibroLocalId.ToString("N")[..8]}.xlsx";
    }
}
