using STEX.Inv.Desktop.Core.Models;
using STEX.Inv.Desktop.Core.ViewModels;

namespace STEX.Inv.Desktop.Tests;

public sealed class ConteoViewModelTests
{
    [Fact]
    public async Task Initialize_OnlyShowsOpenCampaignsAndEnablesNewBook()
    {
        var now = DateTimeOffset.UtcNow;
        var valid = TestData.Campaign("Vigente", now.AddDays(-1), now.AddDays(1));
        var closed = TestData.Campaign(
            "Cerrada",
            now.AddDays(-1),
            now.AddDays(1),
            "Cerrada_Congelada");
        var future = TestData.Campaign("Futura", now.AddDays(1), now.AddDays(2));
        var expired = TestData.Campaign("Vencida", now.AddDays(-2), now.AddDays(-1));
        var operario = TestData.Operator();

        var store = new FakeLocalStore();
        store.State.Campanas.AddRange(new[] { valid, closed, future, expired });

        var viewModel = new ConteoViewModel(
            store,
            new FakeOperatorSession(operario),
            new FakeWarehouseSyncService());

        await viewModel.InitializeAsync();

        Assert.Single(viewModel.Campanas);
        Assert.Equal(valid.Id, viewModel.Campanas[0].Id);
        Assert.False(viewModel.HasDraftForCampaign);
        Assert.False(viewModel.ContinueCommand.CanExecute(null));
        Assert.True(viewModel.NewBookCommand.CanExecute(null));

        viewModel.NewBookCommand.Execute(null);
        Assert.False(viewModel.IsSelectingCampaign);
    }

    [Fact]
    public async Task Initialize_WithDraft_EnablesContinueAndDisablesNewBook()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = TestData.Campaign("Vigente", now.AddDays(-1), now.AddDays(1));
        var operario = TestData.Operator();
        var book = TestData.Book(campaign, operario, now);

        var store = new FakeLocalStore();
        store.State.Campanas.Add(campaign);
        store.State.Libros.Add(book);
        store.State.Conteos.Add(TestData.Line(book, campaign, operario, now));

        var viewModel = new ConteoViewModel(
            store,
            new FakeOperatorSession(operario),
            new FakeWarehouseSyncService());

        await viewModel.InitializeAsync();

        Assert.True(viewModel.HasDraftForCampaign);
        Assert.True(viewModel.ContinueCommand.CanExecute(null));
        Assert.False(viewModel.NewBookCommand.CanExecute(null));
    }

    [Fact]
    public async Task Initialize_WithoutValidCampaigns_ShowsEmptyState()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new FakeLocalStore();
        store.State.Campanas.Add(TestData.Campaign(
            "Cerrada",
            now.AddDays(-1),
            now.AddDays(1),
            "Cerrada_Congelada"));

        var viewModel = new ConteoViewModel(
            store,
            new FakeOperatorSession(TestData.Operator()),
            new FakeWarehouseSyncService());

        await viewModel.InitializeAsync();

        Assert.Empty(viewModel.Campanas);
        Assert.True(viewModel.HasNoCampaigns);
        Assert.False(viewModel.ContinueCommand.CanExecute(null));
        Assert.False(viewModel.NewBookCommand.CanExecute(null));
    }
}
