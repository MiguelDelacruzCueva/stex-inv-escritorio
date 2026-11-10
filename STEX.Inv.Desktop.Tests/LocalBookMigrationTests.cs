using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Tests;

public sealed class LocalBookMigrationTests
{
    [Fact]
    public void EnsureLocalBooks_AssignsBookAndBackfillsUnitIdempotently()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = TestData.Campaign("Campaña", now.AddDays(-1), now.AddDays(1));
        var operario = TestData.Operator();
        var materialId = Guid.NewGuid();
        var state = new LocalState();
        state.Materiales.Add(new Material
        {
            Id = materialId,
            CodigoInterno = "COD-1",
            Descripcion = "Material",
            Unidad = "KG",
            Tipo = "Hilo"
        });
        state.Conteos.Add(new ConteoLinea
        {
            CampanaId = campaign.Id,
            CampanaNombre = campaign.Nombre,
            OperarioId = operario.Id,
            OperarioNombre = operario.NombreCompleto,
            MaterialId = materialId,
            MaterialCodigo = "COD-1",
            MaterialDescripcion = "Material",
            Tipo = "Hilo",
            Unidad = string.Empty,
            Unidades = 1,
            PesoNeto = 1m,
            CreatedAtUtc = now
        });

        var changed = LocalBookMigration.EnsureLocalBooks(state);

        Assert.True(changed);
        var book = Assert.Single(state.Libros);
        Assert.Equal(book.Id, state.Conteos[0].LibroLocalId);
        Assert.Equal("KG", state.Conteos[0].Unidad);
        Assert.Equal(now, book.CreadoAtUtc);

        var changedAgain = LocalBookMigration.EnsureLocalBooks(state);

        Assert.False(changedAgain);
        Assert.Single(state.Libros);
    }
}
