using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.Text.Json;

namespace VisualSqlArchitect.UI.Services;

/// <summary>
/// Cross-platform credential protection.
/// Encrypted values are stored as "enc:&lt;base64&gt;" so that legacy plaintext
/// profiles are detected and transparently migrated on the next save.
///
/// Encryption: AES-256-GCM with a key derived from the current machine name
/// and OS user name via HKDF-SHA-256.  This ties the ciphertext to this
/// user account on this machine — the same security boundary as OS-level
/// user-scope DPAPI on Windows — without requiring platform-specific APIs.
/// </summary>
public static class CredentialProtector
{
    private const string Prefix = "enc:";
    private const string DpapiPrefix = "dpapi:";

    // AES-GCM constants (fixed by the NIST standard; hardcoding for clarity)
    private const int NonceLength = 12;
    private const int TagLength   = 16;
    private const int KeyLength   = 32; // AES-256
    private const int InstallationSecretLength = 32;

    private static readonly Lazy<byte[]> _installationSecret = new(LoadOrCreateInstallationSecret);
    private static readonly Lazy<ISecretProtector> _activeProtector = new(CreateDefaultProtector);

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> and returns an "enc:…" string.
    /// Returns <paramref name="plaintext"/> unchanged if it is null/empty.
    /// Throws if encryption fails — callers must not silently store plaintext as fallback.
    /// </summary>
    public static string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return plaintext;
        return _activeProtector.Value.Protect(plaintext);
    }

    /// <summary>
    /// Decrypts an "enc:…" value produced by <see cref="Protect"/>.
    /// If <paramref name="value"/> does not start with the prefix it is returned
    /// as-is (backward-compatible with legacy plaintext profiles).
    /// Returns empty string if the ciphertext is corrupt.
    /// </summary>
    public static string Unprotect(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Legacy plaintext — no prefix means it was saved before this feature
        if (!value.StartsWith(Prefix, StringComparison.Ordinal) && !value.StartsWith(DpapiPrefix, StringComparison.Ordinal))
            return value;

        try
        {
            if (value.StartsWith(DpapiPrefix, StringComparison.Ordinal))
            {
                byte[] cipherBlob = Convert.FromBase64String(value[DpapiPrefix.Length..]);
                return WindowsDpapiSecretProtector.UnprotectBytes(cipherBlob);
            }

            byte[] aesBlob = Convert.FromBase64String(value[Prefix.Length..]);
            return Encoding.UTF8.GetString(AesDecrypt(aesBlob));
        }
        catch
        {
            // Corrupt or tampered ciphertext — return empty rather than a garbage string
            return string.Empty;
        }
    }

    // ── AES-256-GCM ──────────────────────────────────────────────────────────

    private static byte[] AesEncrypt(byte[] plaintext)
    {
        byte[] key   = DeriveKey();
        byte[] nonce = new byte[NonceLength];
        RandomNumberGenerator.Fill(nonce);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag        = new byte[TagLength];

        using var aes = new AesGcm(key, TagLength);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        // Wire format: [nonce (12 B)] | [tag (16 B)] | [ciphertext (n B)]
        byte[] blob = new byte[NonceLength + TagLength + ciphertext.Length];
        nonce     .CopyTo(blob, 0);
        tag       .CopyTo(blob, NonceLength);
        ciphertext.CopyTo(blob, NonceLength + TagLength);
        return blob;
    }

    private static byte[] AesDecrypt(byte[] blob)
    {
        if (blob.Length < NonceLength + TagLength)
            throw new CryptographicException("Ciphertext blob is too short.");

        byte[] key        = DeriveKey();
        byte[] nonce      = blob[..NonceLength];
        byte[] tag        = blob[NonceLength..(NonceLength + TagLength)];
        byte[] ciphertext = blob[(NonceLength + TagLength)..];
        byte[] plaintext  = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, TagLength);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    private static ISecretProtector CreateDefaultProtector()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsDpapiSecretProtector();
        return new PortableAesSecretProtector();
    }

    // ── Key derivation ────────────────────────────────────────────────────────

    /// <summary>
    /// Derives a 256-bit AES key from the current OS user identity (machine name
    /// + user name).  This means the encrypted value is only decryptable by the
    /// same OS user on the same machine — equivalent to user-scope DPAPI.
    /// </summary>
    private static byte[] DeriveKey()
    {
        string material = $"{Environment.MachineName}\0{Environment.UserName}\0VisualSqlArchitect";
        byte[] materialBytes = Encoding.UTF8.GetBytes(material);
        byte[] ikmInput = new byte[materialBytes.Length + _installationSecret.Value.Length];
        Buffer.BlockCopy(materialBytes, 0, ikmInput, 0, materialBytes.Length);
        Buffer.BlockCopy(_installationSecret.Value, 0, ikmInput, materialBytes.Length, _installationSecret.Value.Length);

        byte[] ikm  = SHA256.HashData(ikmInput);
        byte[] salt = "VSA.CredentialStore.v1"u8.ToArray();
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, KeyLength, salt);
    }

    private static byte[] LoadOrCreateInstallationSecret()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VisualSqlArchitect"
        );
        string path = Path.Combine(dir, ".credential_salt.bin");

        // Existing file: reading must succeed — returning random bytes here would silently
        // invalidate every credential previously encrypted with the persisted key.
        if (File.Exists(path))
        {
            byte[] existing = File.ReadAllBytes(path);
            if (existing.Length == InstallationSecretLength)
                return existing;
        }

        // No valid file yet — this is a fresh install or the file was removed.
        // If the write fails, an ephemeral key is acceptable: there are no prior
        // encrypted credentials to invalidate on a first-run scenario.
        try
        {
            Directory.CreateDirectory(dir);
            byte[] secret = RandomNumberGenerator.GetBytes(InstallationSecretLength);
            File.WriteAllBytes(path, secret);
            return secret;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CredentialProtector] Failed to persist installation secret: {ex.Message}. Encrypted credentials will not survive restarts.");
            return RandomNumberGenerator.GetBytes(InstallationSecretLength);
        }
    }

    private interface ISecretProtector
    {
        string Protect(string plaintext);
    }

    private sealed class WindowsDpapiSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext)
        {
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] protectedBytes = ProtectBytes(plaintextBytes);
            return DpapiPrefix + Convert.ToBase64String(protectedBytes);
        }

        private static byte[] ProtectBytes(byte[] plaintextBytes)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("DPAPI is only available on Windows.");

            return ProtectedData.Protect(plaintextBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        }

        public static string UnprotectBytes(byte[] protectedBytes)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("DPAPI is only available on Windows.");

            byte[] plaintext = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintext);
        }
    }

    private sealed class PortableAesSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext)
        {
            byte[] cipherBlob = AesEncrypt(Encoding.UTF8.GetBytes(plaintext));
            return Prefix + Convert.ToBase64String(cipherBlob);
        }
    }
}

/// <summary>
/// Local credential vault that stores per-profile secrets outside connections.json.
/// Payload is protected using <see cref="CredentialProtector"/> and can be migrated
/// transparently between protection schemes.
/// </summary>
public sealed class CredentialVaultStore
{
    private readonly string _vaultPath;
    public static event Action<string>? WarningRaised;

    public CredentialVaultStore(string? appDataRoot = null)
    {
        string baseDir = string.IsNullOrWhiteSpace(appDataRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VisualSqlArchitect")
            : Path.Combine(appDataRoot, "VisualSqlArchitect");

        _vaultPath = Path.Combine(baseDir, "credentials.vault.json");
    }

    public void SaveSecret(string profileId, string? secret)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return;

        Dictionary<string, string> entries = LoadRawEntries();
        if (string.IsNullOrEmpty(secret))
        {
            entries.Remove(profileId);
        }
        else
        {
            entries[profileId] = CredentialProtector.Protect(secret);
        }

        PersistRawEntries(entries);
    }

    public string? TryGetSecret(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return null;

        Dictionary<string, string> entries = LoadRawEntries();
        if (!entries.TryGetValue(profileId, out string? protectedSecret) || string.IsNullOrWhiteSpace(protectedSecret))
            return null;

        string unprotected = CredentialProtector.Unprotect(protectedSecret);
        return string.IsNullOrEmpty(unprotected) ? null : unprotected;
    }

    public void RemoveSecret(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return;

        Dictionary<string, string> entries = LoadRawEntries();
        if (!entries.Remove(profileId))
            return;

        PersistRawEntries(entries);
    }

    internal Dictionary<string, string> LoadRawEntries()
    {
        try
        {
            if (!File.Exists(_vaultPath))
                return new Dictionary<string, string>(StringComparer.Ordinal);

            string json = File.ReadAllText(_vaultPath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            WarningRaised?.Invoke($"Failed to load credential vault '{_vaultPath}': {ex.Message}");
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private void PersistRawEntries(Dictionary<string, string> entries)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_vaultPath)!);
            string json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_vaultPath, json);
        }
        catch (Exception ex)
        {
            WarningRaised?.Invoke($"Failed to persist credential vault '{_vaultPath}': {ex.Message}");
        }
    }
}
