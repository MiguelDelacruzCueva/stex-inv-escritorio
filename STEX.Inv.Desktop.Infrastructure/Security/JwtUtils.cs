using System.Text;
using System.Text.Json;

namespace STEX.Inv.Desktop.Infrastructure.Security;

/// <summary>
/// Lectura del payload de un JWT sin validar la firma (solo para conocer la expiración).
/// </summary>
public static class JwtUtils
{
    public static DateTimeOffset? GetExpirationUtc(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));

            using var document = JsonDocument.Parse(json);

            return document.RootElement.TryGetProperty("exp", out var exp)
                ? DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64())
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}
