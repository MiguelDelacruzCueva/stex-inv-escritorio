using STEX.Inv.Desktop.Core.DTOs;
using STEX.Inv.Desktop.Core.Models;
using STEX.Inv.Desktop.Core.ViewModels;

namespace STEX.Inv.Desktop.Tests;

public sealed class SyncViewModelTests
{
    [Fact]
    public async Task Initialize_ShowsPendingBooksAndBlocksPartialDuplicatesAndInvalidCampaigns()
    {
        var now = DateTimeOffset.UtcNow;
        var open = TestData.Campaign("Abierta", now.AddDays(-1), now.AddDays(1));
        var partialCampaign = TestData.Campaign("Parcial", now.AddDays(-1), now.AddDays(1));
        var duplicateCampaign = TestData.Campaign("Duplicada", now.AddDays(-1), now.AddDays(1));
        var closedCampaign = TestData.Campaign(
            "Cerrada",
            now.AddDays(-1),
            now.AddDays(1),
            "Cerrada_Congelada");
        var sentCampaign = TestData.Campaign("Enviada", now.AddDays(-1), now.AddDays(1));
        var operario = TestData.Operator();
        var other = TestData.Operator("Otro");

        var ready = TestData.Book(open, operario, now);
        var partial = TestData.Book(partialCampaign, operario, now, EstadoLibroLocal.Parcial, Guid.NewGuid());
        var duplicateA = TestData.Book(duplicateCampaign, operario, now.AddMinutes(-1));
        var duplicateB = TestData.Book(duplicateCampaign, operario, now);
        var closed = TestData.Book(closedCampaign, operario, now);
        var sent = TestData.Book(sentCampaign, operario, now, EstadoLibroLocal.Enviado, Guid.NewGuid());
        var replaced = TestData.Book(sentCampaign, operario, now, EstadoLibroLocal.Reemplazado);
        var otherBook = TestData.Book(open, other, now);

        var store = new FakeLocalStore();
        store.State.Campanas.AddRange(
            new[] { open, partialCampaign, duplicateCampaign, closedCampaign, sentCampaign });
        store.State.Operarios.AddRange(new[] { operario, other });
        store.State.Libros.AddRange(
            new[] { ready, partial, duplicateA, duplicateB, closed, sent, replaced, otherBook });
        store.State.Conteos.Add(TestData.Line(ready, open, operario, now));
        store.State.Conteos.Add(TestData.Line(partial, partialCampaign, operario, now));
        store.State.Conteos.Add(TestData.Line(duplicateA, duplicateCampaign, operario, now));
        store.State.Conteos.Add(TestData.Line(duplicateB, duplicateCampaign, operario, now));
        store.State.Conteos.Add(TestData.Line(closed, closedCampaign, operario, now));
        store.State.Conteos.Add(TestData.Line(sent, sentCampaign, operario, now, remoteId: Guid.NewGuid()));
        store.State.Conteos.Add(TestData.Line(replaced, sentCampaign, operario, now));
        store.State.Conteos.Add(TestData.Line(otherBook, open, other, now));

        var viewModel = CreateViewModel(store, operario);
        await viewModel.InitializeAsync();

        Assert.True(viewModel.HasPendingBooks);
        Assert.True(viewModel.HasDuplicateDrafts);
        Assert.Equal(5, viewModel.PendingBooks.Count);
        Assert.Equal(5, viewModel.ConteosPendientes);

        var readyItem = Assert.Single(viewModel.PendingBooks, b => b.LibroLocalId == ready.Id);
        Assert.True(readyItem.PuedeSubir);
        Assert.Equal("Listo", readyItem.EstadoTexto);

        var partialItem = Assert.Single(viewModel.PendingBooks, b => b.LibroLocalId == partial.Id);
        Assert.False(partialItem.PuedeSubir);
        Assert.Equal("Parcial", partialItem.EstadoTexto);

        Assert.All(
            viewModel.PendingBooks.Where(
                b => b.LibroLocalId == duplicateA.Id || b.LibroLocalId == duplicateB.Id),
            item =>
            {
                Assert.False(item.PuedeSubir);
                Assert.Equal("Duplicado", item.EstadoTexto);
            });

        var closedItem = Assert.Single(viewModel.PendingBooks, b => b.LibroLocalId == closed.Id);
        Assert.False(closedItem.PuedeSubir);
        Assert.Contains("cerrada", closedItem.Bloqueo);

        Assert.DoesNotContain(viewModel.PendingBooks, b => b.LibroLocalId == sent.Id);
        Assert.DoesNotContain(viewModel.PendingBooks, b => b.LibroLocalId == replaced.Id);
        Assert.DoesNotContain(viewModel.PendingBooks, b => b.LibroLocalId == otherBook.Id);
    }

    [Fact]
    public async Task UploadSelected_SendsOnlySelectedUploadableBooks()
    {
        var now = DateTimeOffset.UtcNow;
        var firstCampaign = TestData.Campaign("Campaña A", now.AddDays(-1), now.AddDays(1));
        var secondCampaign = TestData.Campaign("Campaña B", now.AddDays(-1), now.AddDays(1));
        var operario = TestData.Operator();
        var first = TestData.Book(firstCampaign, operario, now.AddMinutes(-1));
        var second = TestData.Book(secondCampaign, operario, now);

        var store = new FakeLocalStore();
        store.State.Campanas.AddRange(new[] { firstCampaign, secondCampaign });
        store.State.Operarios.Add(operario);
        store.State.Libros.AddRange(new[] { first, second });
        store.State.Conteos.Add(TestData.Line(first, firstCampaign, operario, now.AddMinutes(-1)));
        store.State.Conteos.Add(TestData.Line(second, secondCampaign, operario, now));

        var sync = new FakeWarehouseSyncService
        {
            Result = new SyncResult(SyncStatus.Success, UploadedCount: 1, UploadedBooks: 1)
        };
        var viewModel = CreateViewModel(store, operario, sync);
        await viewModel.InitializeAsync();

        viewModel.PendingBooks.Single(b => b.LibroLocalId == first.Id).IsSelected = true;
        await viewModel.UploadSelectedCommand.ExecuteAsync(null);

        Assert.Equal(new[] { first.Id }, sync.LastBookIds);
    }

    [Fact]
    public async Task SyncCommand_DownloadsCatalogsWithoutUploading()
    {
        var store = new FakeLocalStore();
        var sync = new FakeWarehouseSyncService();
        var viewModel = CreateViewModel(store, TestData.Operator(), sync);

        await viewModel.SyncCommand.ExecuteAsync(null);

        Assert.NotNull(sync.LastBookIds);
        Assert.Empty(sync.LastBookIds!);
    }

    private static SyncViewModel CreateViewModel(
        FakeLocalStore store,
        Operario operario,
        FakeWarehouseSyncService? sync = null,
        FakeDialogService? dialogs = null) =>
        new(
            store,
            sync ?? new FakeWarehouseSyncService(),
            new FakeOperatorSession(operario),
            dialogs ?? new FakeDialogService());
}
