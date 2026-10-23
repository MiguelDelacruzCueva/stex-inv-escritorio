using System.Text.Json.Serialization;

namespace STEX.Inv.Desktop.Core.Models;

/// <summary>
/// Vinculación persistente del equipo con la cuenta de un almacén.
/// Solo se crea en el enrolamiento y se elimina al desvincular el equipo.
/// </summary>
public sealed class DeviceSession
{
    public DateTimeOffset ExpiresAtUtc { get; set; }

    public Guid? AlmacenId { get; set; }

    public string AlmacenNombre { get; set; } = string.Empty;

    public string AlmacenCodigo { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Rol { get; set; } = string.Empty;

    public DateTimeOffset BoundAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public bool IsTokenExpired => ExpiresAtUtc != default && ExpiresAtUtc <= DateTimeOffset.UtcNow;

    [JsonIgnore]
    public string AlmacenDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(AlmacenNombre))
            {
                return "Almacén";
            }

            return string.IsNullOrWhiteSpace(AlmacenCodigo)
                ? AlmacenNombre
                : $"{AlmacenNombre} · {AlmacenCodigo}";
        }
    }
}
