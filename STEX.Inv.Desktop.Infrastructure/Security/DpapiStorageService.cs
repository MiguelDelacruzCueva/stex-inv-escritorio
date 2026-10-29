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
    private readonly string _refreshFilePath;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("STEX-INV-ENTROPY-v1");

    public DpapiStorageService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "STEX_INV");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "session.dat");
        _refreshFilePath = Path.Combine(folder, "session.refresh.dat");
    }

    public Task SaveTokenAsync(string token)
    {
        Save(_filePath, token);
        return Task.CompletedTask;
    }

    public Task<string?> GetTokenAsync() => Task.FromResult(Read(_filePath));

    public Task ClearTokenAsync()
    {
        Clear(_filePath);
        Clear(_refreshFilePath);
        return Task.CompletedTask;
    }

    public Task SaveRefreshTokenAsync(string token)
    {
        Save(_refreshFilePath, token);
        return Task.CompletedTask;
    }

    public Task<string?> GetRefreshTokenAsync() => Task.FromResult(Read(_refreshFilePath));

    public Task ClearRefreshTokenAsync()
    {
        Clear(_refreshFilePath);
        return Task.CompletedTask;
    }

    private static void Save(string path, string value)
    {
        var unencryptedBytes = Encoding.UTF8.GetBytes(value);
        var encryptedBytes = ProtectedData.Protect(unencryptedBytes, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, encryptedBytes);
    }

    private static string? Read(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var encryptedBytes = File.ReadAllBytes(path);
            var unencryptedBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(unencryptedBytes);
        }
        catch
        {
            return null;
        }
    }

    private static void Clear(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
