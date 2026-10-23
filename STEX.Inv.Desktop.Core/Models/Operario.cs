using System.Text.Json.Serialization;

namespace STEX.Inv.Desktop.Core.Models;

/// <summary>
/// Operario de planta adscrito al almacén del equipo.
/// El PIN nunca se conserva en claro: solo su hash PBKDF2 y la sal.
/// </summary>
public sealed class Operario
{
    public Guid Id { get; set; }

    public string NombreCompleto { get; set; } = string.Empty;

    public Guid AlmacenId { get; set; }

    public string PinSalt { get; set; } = string.Empty;

    public string PinHash { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    [JsonIgnore]
    public bool TienePin => !string.IsNullOrWhiteSpace(PinHash) && !string.IsNullOrWhiteSpace(PinSalt);

    [JsonIgnore]
    public string Iniciales
    {
        get
        {
            var parts = NombreCompleto
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return parts.Length switch
            {
                0 => "?",
                1 => parts[0][..1].ToUpperInvariant(),
                _ => $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
            };
        }
    }
}
