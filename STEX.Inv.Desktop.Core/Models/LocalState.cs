using System.Text.Json.Serialization;

namespace STEX.Inv.Desktop.Core.Models;

/// <summary>
/// Estado local completo del equipo, cifrado en disco con DPAPI.
/// </summary>
public sealed class LocalState
{
    public DeviceSession? Device { get; set; }

    public List<Operario> Operarios { get; set; } = new();

    public List<CampanaResumen> Campanas { get; set; } = new();

    public List<Material> Materiales { get; set; } = new();

    public List<ConteoLinea> Conteos { get; set; } = new();

    public int MaterialesCount { get; set; }

    public DateTimeOffset? LastSyncUtc { get; set; }

    public string? LastSyncError { get; set; }

    public Dictionary<Guid, PinAttempt> PinAttempts { get; set; } = new();
}

public sealed class CampanaResumen
{
    public Guid Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public DateTimeOffset? FechaInicio { get; set; }

    public DateTimeOffset? FechaFin { get; set; }

    [JsonIgnore]
    public string RangoFechas
    {
        get
        {
            if (FechaInicio is null && FechaFin is null)
            {
                return "Sin fechas definidas";
            }

            if (FechaInicio is null)
            {
                return $"Hasta {FechaFin!.Value.ToString("dd MMM yyyy")}";
            }

            if (FechaFin is null)
            {
                return $"Desde {FechaInicio.Value.ToString("dd MMM yyyy")}";
            }

            return $"{FechaInicio.Value.ToString("dd MMM yyyy")} — {FechaFin.Value.ToString("dd MMM yyyy")}";
        }
    }
}

public sealed class PinAttempt
{
    public int FailedCount { get; set; }

    public DateTimeOffset? LockedUntilUtc { get; set; }
}
