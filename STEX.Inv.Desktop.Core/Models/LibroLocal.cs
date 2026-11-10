namespace STEX.Inv.Desktop.Core.Models;

/// <summary>
/// Libro de conteo local. El identificador remoto se asigna recién al subir,
/// para que dos borradores del mismo operario no se mezclen.
/// </summary>
public sealed class LibroLocal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CampanaId { get; set; }

    public string CampanaNombre { get; set; } = string.Empty;

    public Guid OperarioId { get; set; }

    public string OperarioNombre { get; set; } = string.Empty;

    public Guid? LibroRemotoId { get; set; }

    public EstadoLibroLocal Estado { get; set; } = EstadoLibroLocal.Borrador;

    public DateTimeOffset CreadoAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EnviadoAtUtc { get; set; }

    public Guid? ReemplazaA { get; set; }
}

public enum EstadoLibroLocal
{
    Borrador,
    Enviado,
    Parcial,
    Reemplazado
}
