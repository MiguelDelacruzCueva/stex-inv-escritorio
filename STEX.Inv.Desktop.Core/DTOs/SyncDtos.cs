using System.Text.Json.Serialization;

namespace STEX.Inv.Desktop.Core.DTOs;

/// <summary>
/// Contrato de <c>GET /api/sync/download</c>. El backend devuelve únicamente
/// los datos del almacén autenticado.
/// </summary>
public sealed class SyncDownloadResponseDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public SyncDataDto? Data { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; set; }
}

public sealed class SyncDataDto
{
    [JsonPropertyName("campanas")]
    public List<SyncCampanaDto> Campanas { get; set; } = new();

    [JsonPropertyName("almacenes")]
    public List<SyncAlmacenDto> Almacenes { get; set; } = new();

    [JsonPropertyName("materiales")]
    public List<SyncMaterialDto> Materiales { get; set; } = new();

    [JsonPropertyName("operarios")]
    public List<SyncOperarioDto> Operarios { get; set; } = new();
}

public sealed class SyncCampanaDto
{
    [JsonPropertyName("campanas_id")]
    public Guid Id { get; set; }

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("fecha_inicio")]
    public DateTimeOffset? FechaInicio { get; set; }

    [JsonPropertyName("fecha_fin")]
    public DateTimeOffset? FechaFin { get; set; }
}

public sealed class SyncAlmacenDto
{
    [JsonPropertyName("almacen_id")]
    public Guid Id { get; set; }

    [JsonPropertyName("codigo_almacen")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("nro_almacen")]
    public string? Numero { get; set; }

    [JsonPropertyName("razon_social")]
    public string? RazonSocial { get; set; }

    [JsonPropertyName("nombre_local")]
    public string? NombreLocal { get; set; }
}

public sealed class SyncMaterialDto
{
    [JsonPropertyName("material_id")]
    public Guid Id { get; set; }

    [JsonPropertyName("codigo_interno")]
    public string CodigoInterno { get; set; } = string.Empty;

    [JsonPropertyName("kardex_numero")]
    public string? KardexNumero { get; set; }

    [JsonPropertyName("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [JsonPropertyName("tipo")]
    public string? Tipo { get; set; }
}

public sealed class SyncOperarioDto
{
    [JsonPropertyName("operario_id")]
    public Guid Id { get; set; }

    [JsonPropertyName("nombre_completo")]
    public string NombreCompleto { get; set; } = string.Empty;

    [JsonPropertyName("pin_seguridad")]
    public string? PinSeguridad { get; set; }

    [JsonPropertyName("almacen_id")]
    public Guid AlmacenId { get; set; }
}

public enum SyncStatus
{
    Success,
    Unauthorized,
    Failed
}

public sealed record SyncResult(SyncStatus Status, string? ErrorMessage = null, int Operarios = 0)
{
    public bool IsSuccess => Status == SyncStatus.Success;
}
