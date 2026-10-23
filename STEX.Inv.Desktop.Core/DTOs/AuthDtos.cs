using System.Text.Json.Serialization;

namespace STEX.Inv.Desktop.Core.DTOs;

public class LoginRequestDto
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public UserProfileDto? User { get; set; }
}

public class UserProfileDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("rol")]
    public string Rol { get; set; } = string.Empty;

    [JsonPropertyName("almacen_id")]
    public Guid? AlmacenId { get; set; }

    [JsonPropertyName("almacen_nombre")]
    public string? AlmacenNombre { get; set; }

    [JsonPropertyName("almacen_codigo")]
    public string? AlmacenCodigo { get; set; }
}
