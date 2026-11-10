using ClosedXML.Excel;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;
using STEX.Inv.Desktop.Infrastructure.Services;

namespace STEX.Inv.Desktop.Tests;

public sealed class BookExportServiceTests
{
    [Fact]
    public async Task ExportBookAsync_WritesTheInventoryTemplate()
    {
        var folder = Path.Combine(
            Path.GetTempPath(),
            "stex-tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var book = new BookExportData(
                Guid.NewGuid(),
                "Campaña prueba",
                "OPERARIO TEST",
                new DateTimeOffset(2026, 11, 8, 10, 30, 0, TimeSpan.Zero),
                new[]
                {
                    new ConteoLinea
                    {
                        Tipo = "Hilo",
                        MaterialDescripcion = "Material A",
                        KardexNumero = "K-1",
                        MaterialCodigo = "COD-1",
                        Unidad = "KG",
                        Presentacion = "Bultos",
                        PesoNeto = 12.5m,
                        Observacion = "CRUDO-APJL",
                        CreatedAtUtc = new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.Zero)
                    },
                    new ConteoLinea
                    {
                        Tipo = "Crudo",
                        MaterialDescripcion = "Material B",
                        KardexNumero = "K-2",
                        MaterialCodigo = "COD-2",
                        Unidad = "UN",
                        Presentacion = "Cajas",
                        PesoNeto = 3m,
                        Observacion = "SALDOS",
                        CreatedAtUtc = new DateTimeOffset(2026, 11, 8, 9, 0, 0, TimeSpan.Zero)
                    }
                });

            var service = new BookExportService();
            var path = await service.ExportBookAsync(book, "ALMACEN TEST", folder);

            Assert.True(File.Exists(path));

            using var workbook = new XLWorkbook(path);
            var sheet = workbook.Worksheet(1);

            Assert.Equal("TEJEDURIA", sheet.Cell("A1").GetString());
            Assert.Equal("ALMACEN TEST", sheet.Cell("B1").GetString());
            Assert.Equal("FECHA", sheet.Cell("D1").GetString());
            Assert.Equal("dd/MM/yyyy", sheet.Cell("E1").Style.DateFormat.Format);
            Assert.Equal("PERSONAL DEL INVENTARIO", sheet.Cell("A2").GetString());
            Assert.Equal("OPERARIO TEST", sheet.Cell("B2").GetString());

            var headers = new[]
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

            for (var i = 0; i < headers.Length; i++)
            {
                Assert.Equal(headers[i], sheet.Cell(5, i + 1).GetString());
            }

            Assert.Equal("Hilo", sheet.Cell(6, 1).GetString());
            Assert.Equal("Material A", sheet.Cell(6, 2).GetString());
            Assert.Equal("K-1", sheet.Cell(6, 3).GetString());
            Assert.Equal("COD-1", sheet.Cell(6, 4).GetString());
            Assert.Equal("KG", sheet.Cell(6, 5).GetString());
            Assert.Equal("Bultos", sheet.Cell(6, 6).GetString());
            Assert.Equal(12.5, sheet.Cell(6, 7).GetDouble());
            Assert.Equal("CRUDO-APJL", sheet.Cell(6, 8).GetString());
            Assert.Equal("Crudo", sheet.Cell(7, 1).GetString());

            Assert.Equal(
                XLColor.FromHtml("#D9D9D9"),
                sheet.Cell(5, 1).Style.Fill.BackgroundColor);
            Assert.True(sheet.Cell(5, 1).Style.Font.Bold);
            Assert.Equal(
                XLBorderStyleValues.Thin,
                sheet.Cell(5, 1).Style.Border.TopBorder);
            Assert.Equal(
                XLBorderStyleValues.Thin,
                sheet.Cell(6, 1).Style.Border.TopBorder);
            Assert.Equal(
                XLBorderStyleValues.Thin,
                sheet.Cell(6, 1).Style.Border.RightBorder);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
