namespace STEX.Inv.Desktop.Core.Models;

/// <summary>
/// Registro local de conteo. Se conserva el material y el operario como
/// instantánea para que el libro siga siendo legible aunque cambien los maestros.
/// </summary>
public sealed class ConteoLinea
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CampanaId { get; set; }

    public string CampanaNombre { get; set; } = string.Empty;

    public Guid OperarioId { get; set; }

    public string OperarioNombre { get; set; } = string.Empty;

    public Guid MaterialId { get; set; }

    public string MaterialCodigo { get; set; } = string.Empty;

    public string MaterialDescripcion { get; set; } = string.Empty;

    public string? KardexNumero { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public int Unidades { get; set; }

    public string Presentacion { get; set; } = "Bultos";

    public decimal PesoNeto { get; set; }

    public string Observacion { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Libro remoto al que se envió esta línea; permite reintentos idempotentes.</summary>
    public Guid? LibroId { get; set; }

    /// <summary>Se completa solo cuando el servidor confirmó la subida.</summary>
    public DateTimeOffset? UploadedAtUtc { get; set; }
}
