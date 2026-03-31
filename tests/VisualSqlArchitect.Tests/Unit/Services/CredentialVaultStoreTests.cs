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
}
