using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Infrastructure.Storage;

/// <summary>
/// Estado local del equipo en un único archivo cifrado con DPAPI (usuario actual).
/// La escritura es atómica para no corromper el archivo ante un corte.
/// </summary>
public sealed class EncryptedJsonLocalStore : ILocalStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("STEX-INV-LOCAL-STORE-v1");
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;

    public EncryptedJsonLocalStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "STEX_INV");

        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "local.dat");
    }

    public async Task<LocalState> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!File.Exists(_filePath))
            {
                return new LocalState();
            }

            var encrypted = await File.ReadAllBytesAsync(_filePath, cancellationToken);
            var plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);

            var state = JsonSerializer.Deserialize<LocalState>(plain, SerializerOptions);

            return state ?? new LocalState();
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or IOException)
        {
            // Archivo corrupto o de otro usuario de Windows: se descarta.
            return new LocalState();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(LocalState state, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            var plain = JsonSerializer.SerializeToUtf8Bytes(state, SerializerOptions);
            var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);

            var tempPath = _filePath + ".tmp";
            await File.WriteAllBytesAsync(tempPath, encrypted, cancellationToken);

            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }

            var tempPath = _filePath + ".tmp";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
