namespace STEX.Inv.Desktop.Core.Interfaces;

public interface IPinHasher
{
    string CreateSalt();

    string Hash(string pin, string salt);

    bool Verify(string pin, string salt, string hash);
}
