using VisualSqlArchitect.UI.Services;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Services;

public class CredentialVaultStoreTests
{
    [Fact]
    public void SaveSecret_PersistsProtectedPayload_NotPlaintext()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-vault-tests", Guid.NewGuid().ToString("N"));
        var vault = new CredentialVaultStore(root);

        try
        {
            const string profileId = "profile-1";
            const string secret = "UltraSecretPassword123!";

            vault.SaveSecret(profileId, secret);

            string vaultPath = Path.Combine(root, "VisualSqlArchitect", "credentials.vault.json");
            Assert.True(File.Exists(vaultPath));

            string json = File.ReadAllText(vaultPath);
            Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
            Assert.Contains("profile-1", json, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveSecret_ThenTryGetSecret_RoundTrips()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-vault-tests", Guid.NewGuid().ToString("N"));
        var vault = new CredentialVaultStore(root);

        try
        {
            const string profileId = "profile-2";
            const string secret = "AnotherSecret!";

            vault.SaveSecret(profileId, secret);
            string? loaded = vault.TryGetSecret(profileId);

            Assert.Equal(secret, loaded);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RemoveSecret_DeletesEntry()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-vault-tests", Guid.NewGuid().ToString("N"));
        var vault = new CredentialVaultStore(root);

        try
        {
            const string profileId = "profile-3";
            vault.SaveSecret(profileId, "temp-secret");

            vault.RemoveSecret(profileId);

            Assert.Null(vault.TryGetSecret(profileId));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveSecret_AlwaysPersistsEncryptedValue_NeverPlaintext()
    {
        // Verifies that CredentialProtector.Protect never falls back to returning plaintext.
        // Regression guard for the removed silent-fallback catch in Protect().
        string root = Path.Combine(Path.GetTempPath(), "vsa-vault-tests", Guid.NewGuid().ToString("N"));
        var vault = new CredentialVaultStore(root);

        try
        {
            const string secret = "SensitivePassword!99";
            vault.SaveSecret("p1", secret);

            string vaultPath = Path.Combine(root, "VisualSqlArchitect", "credentials.vault.json");
            string json = File.ReadAllText(vaultPath);

            // The stored value must carry a protection prefix — never plaintext.
            Assert.True(
                json.Contains("enc:", StringComparison.Ordinal) ||
                json.Contains("dpapi:", StringComparison.Ordinal),
                $"Expected protected prefix in vault but got: {json}");
            Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryGetSecret_CorruptCiphertext_ReturnsNullNotPlaintext()
    {
        // Verifies that a tampered/corrupt vault entry returns null rather than
        // silently surfacing plaintext or garbage — protecting against key-mismatch
        // scenarios where the installation secret was lost.
        string root = Path.Combine(Path.GetTempPath(), "vsa-vault-tests", Guid.NewGuid().ToString("N"));
        var vault = new CredentialVaultStore(root);

        try
        {
            // Write a syntactically valid "enc:" entry whose base64 content is corrupt.
            string vaultDir = Path.Combine(root, "VisualSqlArchitect");
            Directory.CreateDirectory(vaultDir);
            string vaultPath = Path.Combine(vaultDir, "credentials.vault.json");
            File.WriteAllText(vaultPath, """{"p1":"enc:dGhpcyBpcyBub3QgdmFsaWQgY2lwaGVydGV4dA=="}""");

            string? result = vault.TryGetSecret("p1");

            // Corrupt ciphertext must not produce a non-empty string that looks like a real password.
            Assert.True(result is null || result == string.Empty,
                $"Expected null/empty for corrupt entry, got: {result}");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadRawEntries_CorruptJson_RaisesWarningAndReturnsEmpty()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-vault-tests", Guid.NewGuid().ToString("N"));
        var vault = new CredentialVaultStore(root);

        try
        {
            string vaultDir = Path.Combine(root, "VisualSqlArchitect");
            Directory.CreateDirectory(vaultDir);
            string vaultPath = Path.Combine(vaultDir, "credentials.vault.json");
            File.WriteAllText(vaultPath, "{ not-valid-json");

            string? warning = null;
            void Handler(string msg) => warning = msg;
            CredentialVaultStore.WarningRaised += Handler;
            try
            {
                string? secret = vault.TryGetSecret("any");
                Assert.Null(secret);
                Assert.False(string.IsNullOrWhiteSpace(warning));
            }
            finally
            {
                CredentialVaultStore.WarningRaised -= Handler;
            }
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
