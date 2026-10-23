using System.Security.Cryptography;
using STEX.Inv.Desktop.Core.Interfaces;

namespace STEX.Inv.Desktop.Infrastructure.Services;

/// <summary>
/// PBKDF2-SHA256 con sal aleatoria por operario. El PIN nunca se guarda en claro.
/// </summary>
public sealed class PinHasher : IPinHasher
{
    private const int Iterations = 120_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string CreateSalt()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(SaltSize));
    }

    public string Hash(string pin, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, saltBytes, Iterations, HashAlgorithmName.SHA256, HashSize);

        return Convert.ToBase64String(hash);
    }

    public bool Verify(string pin, string salt, string hash)
    {
        try
        {
            var computed = Convert.FromBase64String(Hash(pin, salt));
            var expected = Convert.FromBase64String(hash);

            return CryptographicOperations.FixedTimeEquals(computed, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
