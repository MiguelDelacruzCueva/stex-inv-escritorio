using STEX.Inv.Desktop.Core.Models;
using STEX.Inv.Desktop.Core.Validation;

namespace STEX.Inv.Desktop.Tests;

public sealed class BookRulesTests
{
    [Fact]
    public void HasActiveDraft_IgnoresSentBooksAndExcludedBook()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = TestData.Campaign("Campaña", now.AddDays(-1), now.AddDays(1));
        var operario = TestData.Operator();
        var draft = TestData.Book(campaign, operario, now);
        var sent = TestData.Book(campaign, operario, now, EstadoLibroLocal.Enviado);
        var state = new LocalState();
        state.Libros.AddRange(new[] { draft, sent });

        Assert.True(BookRules.HasActiveDraft(state, campaign.Id, operario.Id));
        Assert.False(BookRules.HasActiveDraft(state, campaign.Id, operario.Id, draft.Id));
        Assert.False(BookRules.HasActiveDraft(state, campaign.Id, Guid.NewGuid()));
    }

    [Fact]
    public void DuplicateDrafts_GroupsOnlyDraftsWithLinesForTheOperator()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = TestData.Campaign("Campaña", now.AddDays(-1), now.AddDays(1));
        var operario = TestData.Operator();
        var other = TestData.Operator("Otro");
        var first = TestData.Book(campaign, operario, now);
        var second = TestData.Book(campaign, operario, now);
        var otherBook = TestData.Book(campaign, other, now);
        var emptyDraft = TestData.Book(campaign, operario, now);
        var state = new LocalState();
        state.Libros.AddRange(new[] { first, second, otherBook, emptyDraft });
        state.Conteos.Add(TestData.Line(first, campaign, operario, now));
        state.Conteos.Add(TestData.Line(second, campaign, operario, now));
        state.Conteos.Add(TestData.Line(otherBook, campaign, other, now));

        var groups = BookRules.DuplicateDrafts(state, operario.Id);

        var group = Assert.Single(groups);
        Assert.Equal(2, group.Count());
        Assert.DoesNotContain(group, book => book.Id == otherBook.Id || book.Id == emptyDraft.Id);
    }
}
