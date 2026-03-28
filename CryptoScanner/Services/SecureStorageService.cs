using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace CryptoScanner.Services;

/// <summary>
/// Encrypts/decrypts API keys using AES-256 with a machine-specific derived key.
/// Works cross-platform (Windows, Linux, macOS) without external dependencies.
/// </summary>
public class SecureStorageService
{
    private readonly string _filePath;

    public SecureStorageService(string? path = null)
    {
        _filePath = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CryptoScanner", "apikey.enc");

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    public void SaveApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            if (File.Exists(_filePath)) File.Delete(_filePath);
            return;
        }

        try
        {
            var encrypted = Encrypt(Encoding.UTF8.GetBytes(apiKey));
            File.WriteAllBytes(_filePath, encrypted);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[SecureStorage] SaveApiKey failed: {ex}");
        }
    }

    public string LoadApiKey()
    {
        try
        {
            if (!File.Exists(_filePath)) return string.Empty;
            var encrypted = File.ReadAllBytes(_filePath);
            var decrypted = Decrypt(encrypted);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[SecureStorage] LoadApiKey failed: {ex}");
            return string.Empty;
        }
    }

    private static byte[] Encrypt(byte[] data)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[data.Length];
        var tag = new byte[16];
        var key = DeriveFallbackKey(salt);

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, data, ciphertext, tag);

        using var ms = new MemoryStream();
        ms.WriteByte(1);
        ms.Write(salt, 0, salt.Length);
        ms.Write(nonce, 0, nonce.Length);
        ms.Write(tag, 0, tag.Length);
        ms.Write(ciphertext, 0, ciphertext.Length);
        return ms.ToArray();
    }

    private static byte[] Decrypt(byte[] encrypted)
    {
        if (encrypted.Length < 45 || encrypted[0] != 1)
            throw new CryptographicException("Unknown encrypted payload format.");

        var salt = encrypted[1..17];
        var nonce = encrypted[17..29];
        var tag = encrypted[29..45];
        var ciphertext = encrypted[45..];
        var plaintext = new byte[ciphertext.Length];
        var key = DeriveFallbackKey(salt);

        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    private static byte[] DeriveFallbackKey(byte[] salt)
    {
        var seed = $"CryptoScanner_{Environment.MachineName}_{Environment.UserName}_v2_secure";
        using var kdf = new Rfc2898DeriveBytes(seed, salt, 100_000, HashAlgorithmName.SHA256);
        return kdf.GetBytes(32);
    }
}
