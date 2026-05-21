using System.Security.Cryptography;

namespace BackupApp.Core.Services;

public class EncryptionService
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int IvSize = 16;
    private const int Iterations = 100_000;

    public void EncryptFile(string sourcePath, string destPath, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var iv = RandomNumberGenerator.GetBytes(IvSize);
        var key = DeriveKey(password, salt);

        using var inStream = File.OpenRead(sourcePath);
        using var outStream = File.Create(destPath);
        outStream.Write(salt, 0, salt.Length);
        outStream.Write(iv, 0, iv.Length);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        using var cryptoStream = new CryptoStream(outStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
        inStream.CopyTo(cryptoStream);
    }

    public void DecryptFile(string sourcePath, string destPath, string password)
    {
        using var inStream = File.OpenRead(sourcePath);
        var salt = new byte[SaltSize];
        inStream.ReadExactly(salt, 0, SaltSize);
        var iv = new byte[IvSize];
        inStream.ReadExactly(iv, 0, IvSize);

        var key = DeriveKey(password, salt);

        using var outStream = File.Create(destPath);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        using var cryptoStream = new CryptoStream(inStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
        cryptoStream.CopyTo(outStream);
    }

    public byte[] EncryptBytes(byte[] data, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var iv = RandomNumberGenerator.GetBytes(IvSize);
        var key = DeriveKey(password, salt);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        using var ms = new MemoryStream();
        ms.Write(salt, 0, salt.Length);
        ms.Write(iv, 0, iv.Length);
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            cs.Write(data, 0, data.Length);
        return ms.ToArray();
    }

    public byte[] DecryptBytes(byte[] encrypted, string password)
    {
        var salt = encrypted[..SaltSize];
        var iv = encrypted[SaltSize..(SaltSize + IvSize)];
        var cipher = encrypted[(SaltSize + IvSize)..];
        var key = DeriveKey(password, salt);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        using var ms = new MemoryStream(cipher);
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var result = new MemoryStream();
        cs.CopyTo(result);
        return result.ToArray();
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize);
    }
}
