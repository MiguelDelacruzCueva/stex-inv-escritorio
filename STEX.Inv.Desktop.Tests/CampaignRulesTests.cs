using STEX.Inv.Desktop.Core.Validation;

namespace STEX.Inv.Desktop.Tests;

public sealed class CampaignRulesTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Abierta", -1, 1, true)]
    [InlineData("Abierta", null, null, true)]
    [InlineData("Abierta", 1, 2, false)]
    [InlineData("Abierta", -2, -1, false)]
    [InlineData("Cerrada_Congelada", -1, 1, false)]
    public void IsOpenAndInRange_RespectsStateAndDates(
        string status,
        int? startOffsetDays,
        int? endOffsetDays,
        bool expected)
    {
        var campaign = TestData.Campaign(
            "Prueba",
            startOffsetDays is null ? null : Now.AddDays(startOffsetDays.Value),
            endOffsetDays is null ? null : Now.AddDays(endOffsetDays.Value),
            status);

        Assert.Equal(expected, CampaignRules.IsOpenAndInRange(campaign, Now));
    }

    [Fact]
    public void Available_LeavesOnlyOpenCampaignsInsideTheirRange()
    {
        var campaigns = new[]
        {
            TestData.Campaign("Vigente", Now.AddDays(-1), Now.AddDays(1)),
            TestData.Campaign("Cerrada", Now.AddDays(-1), Now.AddDays(1), "Cerrada_Congelada"),
            TestData.Campaign("Futura", Now.AddDays(1), Now.AddDays(2)),
            TestData.Campaign("Vencida", Now.AddDays(-2), Now.AddDays(-1))
        };

        var available = CampaignRules.Available(campaigns, Now).ToList();

        Assert.Single(available);
        Assert.Equal("Vigente", available[0].Nombre);
    }
}
