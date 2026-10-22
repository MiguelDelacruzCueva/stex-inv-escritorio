namespace STEX.Inv.Desktop.Infrastructure.Security;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using STEX.Inv.Desktop.Core.Interfaces;

public class DpapiStorageService : ISecureStorageService
{
    private readonly string _filePath;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("STEX-INV-ENTROPY-v1");

    public DpapiStorageService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "STEX_INV");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "session.dat");
    }

    public Task SaveTokenAsync(string token)
    {
        var unencryptedBytes = Encoding.UTF8.GetBytes(token);
        var encryptedBytes = ProtectedData.Protect(unencryptedBytes, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_filePath, encryptedBytes);
        return Task.CompletedTask;
    }

    public Task<string?> GetTokenAsync()
    {
        if (!File.Exists(_filePath)) return Task.FromResult<string?>(null);

        try
        {
            var encryptedBytes = File.ReadAllBytes(_filePath);
            var unencryptedBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Task.FromResult<string?>(Encoding.UTF8.GetString(unencryptedBytes));
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    public Task ClearTokenAsync()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
        return Task.CompletedTask;
    }
}