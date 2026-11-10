using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Core.Validation;

/// <summary>
/// Reglas de vigencia compartidas por las pantallas que eligen campaña.
/// </summary>
public static class CampaignRules
{
    public static bool IsOpenAndInRange(CampanaResumen campaign, DateTimeOffset now)
    {
        return string.Equals(campaign.Estado, "Abierta", StringComparison.OrdinalIgnoreCase)
               && (campaign.FechaInicio is null || now >= campaign.FechaInicio)
               && (campaign.FechaFin is null || now <= campaign.FechaFin);
    }

    public static IEnumerable<CampanaResumen> Available(
        IEnumerable<CampanaResumen> campaigns,
        DateTimeOffset now) =>
        campaigns.Where(c => IsOpenAndInRange(c, now));
}
