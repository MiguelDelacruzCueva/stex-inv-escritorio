using STEX.Inv.Desktop.Core.Models;
using STEX.Inv.Desktop.Core.ViewModels;

namespace STEX.Inv.Desktop.Tests;

public sealed class LibrosViewModelTests
{
    [Fact]
    public async Task Initialize_FiltersByOperatorAndPagesTwentyBooks()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = TestData.Campaign("Campaña", now.AddDays(-1), now.AddDays(1));
        var operario = TestData.Operator();
        var otherOperator = TestData.Operator("Otro operario");
        var store = new FakeLocalStore();
        store.State.Campanas.Add(campaign);

        for (var i = 0; i < 25; i++)
        {
            var book = TestData.Book(campaign, operario, now.AddMinutes(-i));
            store.State.Libros.Add(book);
            store.State.Conteos.Add(TestData.Line(book, campaign, operario, now.AddMinutes(-i)));
        }

        var otherBook = TestData.Book(campaign, otherOperator, now);
        store.State.Libros.Add(otherBook);
        store.State.Conteos.Add(TestData.Line(otherBook, campaign, otherOperator, now));

        var viewModel = CreateViewModel(store, operario);

        await viewModel.InitializeAsync();

        Assert.Equal(25, viewModel.Libros.Count);
        Assert.Equal(20, viewModel.PagedLibros.Count);
        Assert.Equal(2, viewModel.TotalPages);
        Assert.Equal("Página 1 de 2", viewModel.PageText);
        Assert.True(viewModel.HasMultiplePages);

        viewModel.NextPageCommand.Execute(null);
        Assert.Equal(5, viewModel.PagedLibros.Count);
        Assert.Equal(2, viewModel.CurrentPage);

        viewModel.PreviousPageCommand.Execute(null);
        Assert.Equal(20, viewModel.PagedLibros.Count);
        Assert.Equal(1, viewModel.CurrentPage);
    }

    [Fact]
    public async Task Copy_DialogOffersOnlyOpenCampaignsAndBlocksActiveDraft()
    {
        var now = DateTimeOffset.UtcNow;
        var sourceCampaign = TestData.Campaign("Origen", now.AddDays(-1), now.AddDays(1));
        var destination = TestData.Campaign("Destino", now.AddDays(-1), now.AddDays(1));
        var closed = TestData.Campaign(
            "Cerrada",
            now.AddDays(-1),
            now.AddDays(1),
            "Cerrada_Congelada");
        var future = TestData.Campaign("Futura", now.AddDays(1), now.AddDays(2));
        var operario = TestData.Operator();
        var source = TestData.Book(sourceCampaign, operario, now);
        var store = new FakeLocalStore();
        store.State.Campanas.AddRange(new[] { sourceCampaign, destination, closed, future });
        store.State.Libros.Add(source);
        store.State.Conteos.Add(TestData.Line(source, sourceCampaign, operario, now));

        var dialogs = new FakeDialogService();
        var viewModel = CreateViewModel(store, operario, dialogs: dialogs);
        await viewModel.InitializeAsync();

        await viewModel.CopyBookCommand.ExecuteAsync(viewModel.PagedLibros.Single());

        Assert.Equal(2, dialogs.LastSelectionItems.Count);
        Assert.Contains(
            dialogs.LastSelectionItems,
            item => item.Id == destination.Id && item.IsEnabled);
        Assert.Contains(
            dialogs.LastSelectionItems,
            item => item.Id == sourceCampaign.Id && !item.IsEnabled);
        Assert.DoesNotContain(
            dialogs.LastSelectionItems,
            item => item.Id == closed.Id || item.Id == future.Id);
    }

    [Fact]
    public async Task Copy_ToAnotherCampaign_KeepsSentSource()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = TestData.Campaign("Campaña", now.AddDays(-1), now.AddDays(1));
        var destination = TestData.Campaign("Destino", now.AddDays(-1), now.AddDays(1));
        var operario = TestData.Operator();
        var remoteId = Guid.NewGuid();
        var source = TestData.Book(
            campaign,
            operario,
            now,
            EstadoLibroLocal.Enviado,
            remoteId);
        var sourceLine = TestData.Line(source, campaign, operario, now, remoteId: Guid.NewGuid());

        var store = new FakeLocalStore();
        store.State.Campanas.AddRange(new[] { campaign, destination });
        store.State.Libros.Add(source);
        store.State.Conteos.Add(sourceLine);

        var dialogs = new FakeDialogService { SelectionResult = destination.Id };
        var viewModel = CreateViewModel(store, operario, dialogs: dialogs);
        await viewModel.InitializeAsync();

        var summary = viewModel.PagedLibros.Single();
        Assert.True(viewModel.CopyBookCommand.CanExecute(summary));

        await viewModel.CopyBookCommand.ExecuteAsync(summary);

        var copy = Assert.Single(store.State.Libros, b => b.Id != source.Id);
        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal(destination.Id, copy.CampanaId);
        Assert.Equal(operario.Id, copy.OperarioId);
        Assert.Equal(EstadoLibroLocal.Borrador, copy.Estado);
        Assert.Null(copy.ReemplazaA);
        Assert.Equal(EstadoLibroLocal.Enviado, source.Estado);

        var copiedLine = Assert.Single(store.State.Conteos, c => c.LibroLocalId == copy.Id);
        Assert.Equal(sourceLine.MaterialId, copiedLine.MaterialId);
        Assert.Null(copiedLine.LibroId);
        Assert.Null(copiedLine.UploadedAtUtc);
    }

    [Fact]
    public async Task Copy_ToSameCampaign_ForSentSource_MarksSourceReplaced()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = TestData.Campaign("Campaña", now.AddDays(-1), now.AddDays(1));
        var operario = TestData.Operator();
        var source = TestData.Book(
            campaign,
            operario,
            now,
            EstadoLibroLocal.Enviado,
            Guid.NewGuid());

        var store = new FakeLocalStore();
        store.State.Campanas.Add(campaign);
        store.State.Libros.Add(source);
        store.State.Conteos.Add(TestData.Line(source, campaign, operario, now, remoteId: Guid.NewGuid()));

        var dialogs = new FakeDialogService { SelectionResult = campaign.Id };
        var viewModel = CreateViewModel(store, operario, dialogs: dialogs);
        await viewModel.InitializeAsync();

        await viewModel.CopyBookCommand.ExecuteAsync(viewModel.PagedLibros.Single());

        var copy = Assert.Single(store.State.Libros, b => b.Id != source.Id);
        Assert.Equal(campaign.Id, copy.CampanaId);
        Assert.Equal(EstadoLibroLocal.Reemplazado, source.Estado);
    }

    [Fact]
    public async Task Copy_FromPartial_ToAnotherCampaign_MarksSourceReplaced()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = TestData.Campaign("Campaña", now.AddDays(-1), now.AddDays(1));
        var destination = TestData.Campaign("Destino", now.AddDays(-1), now.AddDays(1));
        var operario = TestData.Operator();
        var source = TestData.Book(
            campaign,
            operario,
            now,
            EstadoLibroLocal.Parcial,
            Guid.NewGuid());

        var store = new FakeLocalStore();
        store.State.Campanas.AddRange(new[] { campaign, destination });
        store.State.Libros.Add(source);
        store.State.Conteos.Add(TestData.Line(source, campaign, operario, now, remoteId: Guid.NewGuid()));

        var dialogs = new FakeDialogService { SelectionResult = destination.Id };
        var viewModel = CreateViewModel(store, operario, dialogs: dialogs);
        await viewModel.InitializeAsync();

        await viewModel.CopyBookCommand.ExecuteAsync(viewModel.PagedLibros.Single());

        var copy = Assert.Single(store.State.Libros, b => b.Id != source.Id);
        Assert.Equal(destination.Id, copy.CampanaId);
        Assert.Equal(EstadoLibroLocal.Reemplazado, source.Estado);
    }

    [Fact]
    public async Task Copy_ToCampaignWithActiveDraft_IsBlocked()
    {
        var now = DateTimeOffset.UtcNow;
        var sourceCampaign = TestData.Campaign("Origen", now.AddDays(-1), now.AddDays(1));
        var destination = TestData.Campaign("Destino", now.AddDays(-1), now.AddDays(1));
        var available = TestData.Campaign("Libre", now.AddDays(-1), now.AddDays(1));
        var operario = TestData.Operator();
        var source = TestData.Book(sourceCampaign, operario, now);
        var existingDraft = TestData.Book(destination, operario, now);

        var store = new FakeLocalStore();
        store.State.Campanas.AddRange(new[] { sourceCampaign, destination, available });
        store.State.Libros.AddRange(new[] { source, existingDraft });
        store.State.Conteos.Add(TestData.Line(source, sourceCampaign, operario, now));
        store.State.Conteos.Add(TestData.Line(existingDraft, destination, operario, now));

        var dialogs = new FakeDialogService { SelectionResult = destination.Id };
        var viewModel = CreateViewModel(store, operario, dialogs: dialogs);
        await viewModel.InitializeAsync();

        var summary = viewModel.PagedLibros.Single(l => l.LibroLocalId == source.Id);
        await viewModel.CopyBookCommand.ExecuteAsync(summary);

        Assert.Equal(2, store.State.Libros.Count);
        Assert.Contains("Ya tienes un borrador", viewModel.StatusMessage);
        Assert.Contains(dialogs.LastSelectionItems, item => item.Id == destination.Id && !item.IsEnabled);
    }

    [Fact]
    public async Task ResolveDuplicateDrafts_KeepsChosenAndMarksOthersReplaced()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = TestData.Campaign("Campaña", now.AddDays(-1), now.AddDays(1));
        var operario = TestData.Operator();
        var first = TestData.Book(campaign, operario, now.AddMinutes(-1));
        var second = TestData.Book(campaign, operario, now);

        var store = new FakeLocalStore();
        store.State.Campanas.Add(campaign);
        store.State.Libros.AddRange(new[] { first, second });
        store.State.Conteos.Add(TestData.Line(first, campaign, operario, now.AddMinutes(-1)));
        store.State.Conteos.Add(TestData.Line(second, campaign, operario, now));

        var dialogs = new FakeDialogService { SelectionResult = first.Id };
        var viewModel = CreateViewModel(store, operario, dialogs: dialogs);
        await viewModel.InitializeAsync();

        Assert.True(viewModel.HasDuplicateDrafts);
        Assert.True(viewModel.ResolveDuplicateDraftsCommand.CanExecute(null));

        await viewModel.ResolveDuplicateDraftsCommand.ExecuteAsync(null);

        Assert.Equal(EstadoLibroLocal.Borrador, first.Estado);
        Assert.Equal(EstadoLibroLocal.Reemplazado, second.Estado);
        Assert.False(viewModel.HasDuplicateDrafts);
    }

    [Fact]
    public async Task Delete_AsksForConfirmationAndRemovesBookAndLines()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = TestData.Campaign("Campaña", now.AddDays(-1), now.AddDays(1));
        var operario = TestData.Operator();
        var book = TestData.Book(campaign, operario, now);
        var store = new FakeLocalStore();
        store.State.Campanas.Add(campaign);
        store.State.Libros.Add(book);
        store.State.Conteos.Add(TestData.Line(book, campaign, operario, now));

        var dialogs = new FakeDialogService { Result = true };
        var viewModel = CreateViewModel(store, operario, dialogs: dialogs);
        await viewModel.InitializeAsync();

        await viewModel.DeleteBookCommand.ExecuteAsync(viewModel.PagedLibros.Single());

        Assert.Empty(store.State.Libros);
        Assert.Empty(store.State.Conteos);
        Assert.Equal(1, dialogs.Calls);
        Assert.Contains("Campaña", dialogs.LastMessage);
        Assert.Contains("Registros: 1", dialogs.LastMessage);
        Assert.Contains("¿Eliminar este libro", dialogs.LastMessage);
    }

    [Fact]
    public async Task Delete_Cancel_KeepsBook()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = TestData.Campaign("Campaña", now.AddDays(-1), now.AddDays(1));
        var operario = TestData.Operator();
        var book = TestData.Book(campaign, operario, now);
        var store = new FakeLocalStore();
        store.State.Campanas.Add(campaign);
        store.State.Libros.Add(book);
        store.State.Conteos.Add(TestData.Line(book, campaign, operario, now));

        var dialogs = new FakeDialogService { Result = false };
        var viewModel = CreateViewModel(store, operario, dialogs: dialogs);
        await viewModel.InitializeAsync();

        await viewModel.DeleteBookCommand.ExecuteAsync(viewModel.PagedLibros.Single());

        Assert.Single(store.State.Libros);
        Assert.Single(store.State.Conteos);
        Assert.Equal(1, dialogs.Calls);
    }

    [Fact]
    public async Task Export_SelectedAndAll_UseTheChosenFolderAndBookOrder()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = TestData.Campaign("Campaña", now.AddDays(-1), now.AddDays(1));
        var operario = TestData.Operator();
        var store = new FakeLocalStore();
        store.State.Campanas.Add(campaign);

        for (var i = 0; i < 3; i++)
        {
            var book = TestData.Book(campaign, operario, now.AddMinutes(-i));
            store.State.Libros.Add(book);
            store.State.Conteos.Add(TestData.Line(book, campaign, operario, now.AddMinutes(-i)));
        }

        var export = new FakeBookExportService();
        var fileDialog = new FakeFileDialogService
        {
            Folder = Path.Combine(Path.GetTempPath(), "stex-tests-export")
        };
        var viewModel = CreateViewModel(
            store,
            operario,
            export: export,
            fileDialog: fileDialog);
        await viewModel.InitializeAsync();

        var selected = viewModel.PagedLibros[1];
        selected.IsSelected = true;

        await viewModel.ExportSelectedCommand.ExecuteAsync(null);
        Assert.Single(export.ExportedBooks);
        Assert.Equal(selected.LibroLocalId, export.ExportedBooks[0].LibroLocalId);
        Assert.Equal(fileDialog.Folder, export.Folders[0]);

        export.ExportedBooks.Clear();
        export.Folders.Clear();

        await viewModel.ExportAllCommand.ExecuteAsync(null);
        Assert.Equal(3, export.ExportedBooks.Count);
        Assert.Equal(
            viewModel.Libros.Select(l => l.LibroLocalId),
            export.ExportedBooks.Select(b => b.LibroLocalId));
    }

    private static LibrosViewModel CreateViewModel(
        FakeLocalStore store,
        Operario operario,
        FakeDialogService? dialogs = null,
        FakeBookExportService? export = null,
        FakeFileDialogService? fileDialog = null) =>
        new(
            store,
            new FakeOperatorSession(operario),
            fileDialog ?? new FakeFileDialogService(),
            export ?? new FakeBookExportService(),
            dialogs ?? new FakeDialogService());
}
