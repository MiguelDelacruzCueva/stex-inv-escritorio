using System.Text.Json.Serialization;

namespace STEX.Inv.Desktop.Core.Models;

/// <summary>
/// Material maestro descargado para el almacén del equipo.
/// </summary>
public sealed class Material
{
    public Guid Id { get; set; }

    public string CodigoInterno { get; set; } = string.Empty;

    public string? KardexNumero { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    public string? Tipo { get; set; }

    [JsonIgnore]
    public string Display => string.IsNullOrWhiteSpace(CodigoInterno)
        ? Descripcion
        : $"{CodigoInterno} · {Descripcion}";
}
