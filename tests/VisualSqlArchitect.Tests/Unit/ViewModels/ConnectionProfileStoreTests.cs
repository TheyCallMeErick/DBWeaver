using DBWeaver.UI.Services.ConnectionManager;
using DBWeaver.UI.Services.Benchmark;
using System.Text.Json;
using DBWeaver.Core;
using DBWeaver.UI.Services;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class ConnectionProfileStoreTests
{
    [Fact]
    public void PersistProfiles_StripsPasswordsAndStoresSecretInVault()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-profile-store-" + Guid.NewGuid());
        string filePath = Path.Combine(root, "connections.json");

        try
        {
            var store = new ConnectionProfileStore(filePath);
            var vault = new CredentialVaultStore(root);
            var profile = new ConnectionProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Local",
                Provider = DatabaseProvider.Postgres,
                Host = "localhost",
                Port = 5432,
                Database = "db",
                Username = "u",
                Password = "secret",
                UseIntegratedSecurity = false,
                TimeoutSeconds = 30,
            };

            store.PersistProfiles([profile], vault);

            string json = File.ReadAllText(filePath);
            List<ConnectionProfile>? persisted = JsonSerializer.Deserialize<List<ConnectionProfile>>(json);

            Assert.NotNull(persisted);
            Assert.Single(persisted!);
            Assert.Equal(string.Empty, persisted[0].Password);
            Assert.Equal("secret", vault.TryGetSecret(profile.Id));
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void LoadProfiles_MigratesLegacyInlinePasswordToVault()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-profile-load-" + Guid.NewGuid());
        string filePath = Path.Combine(root, "connections.json");

        try
        {
            Directory.CreateDirectory(root);
            var vault = new CredentialVaultStore(root);
            var legacyProfile = new ConnectionProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Legacy",
                Provider = DatabaseProvider.Postgres,
                Host = "localhost",
                Port = 5432,
                Database = "db",
                Username = "u",
                Password = "legacy-secret",
                UseIntegratedSecurity = false,
                TimeoutSeconds = 30,
            };
            File.WriteAllText(filePath, JsonSerializer.Serialize(new List<ConnectionProfile> { legacyProfile }));

            var store = new ConnectionProfileStore(filePath);

            IReadOnlyList<ConnectionProfile> loaded = store.LoadProfiles(vault);

            Assert.Single(loaded);
            Assert.Equal("legacy-secret", loaded[0].Password);
            Assert.Equal("legacy-secret", vault.TryGetSecret(legacyProfile.Id));

            string rewrittenJson = File.ReadAllText(filePath);
            List<ConnectionProfile>? rewritten = JsonSerializer.Deserialize<List<ConnectionProfile>>(rewrittenJson);
            Assert.NotNull(rewritten);
            Assert.Equal(string.Empty, rewritten![0].Password);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // cleanup best-effort
        }
    }
}


