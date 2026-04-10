using System.Text.Json;
using DBWeaver.UI.Services.Connection;

namespace DBWeaver.UI.Services.ConnectionManager;

public sealed class ConnectionProfileStore(string? profilesFilePath = null) : IConnectionProfileStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _profilesFilePath =
        string.IsNullOrWhiteSpace(profilesFilePath)
            ? Path.Combine(global::DBWeaver.UI.AppConstants.AppDataDirectory, "connections.json")
            : profilesFilePath;

    public IReadOnlyList<ConnectionProfile> LoadProfiles(CredentialVaultStore credentialVault)
    {
        try
        {
            if (!File.Exists(_profilesFilePath))
                return [];

            string json = File.ReadAllText(_profilesFilePath);
            List<ConnectionProfile>? profiles = JsonSerializer.Deserialize<List<ConnectionProfile>>(json, JsonOpts);
            if (profiles is null)
                return [];

            bool migratedLegacyInlinePassword = false;
            var loaded = new List<ConnectionProfile>(profiles.Count);
            foreach (ConnectionProfile profile in profiles)
            {
                string? vaultSecret = credentialVault.TryGetSecret(profile.Id);
                if (!string.IsNullOrEmpty(vaultSecret))
                {
                    profile.Password = vaultSecret;
                    loaded.Add(profile);
                    continue;
                }

                string legacy = profile.WithUnprotectedPassword().Password;
                if (!string.IsNullOrEmpty(legacy))
                {
                    credentialVault.SaveSecret(profile.Id, legacy);
                    profile.Password = legacy;
                    migratedLegacyInlinePassword = true;
                }
                else
                {
                    profile.Password = string.Empty;
                }

                loaded.Add(profile);
            }

            if (migratedLegacyInlinePassword)
                PersistProfiles(loaded, credentialVault);

            return loaded;
        }
        catch
        {
            return [];
        }
    }

    public void PersistProfiles(IEnumerable<ConnectionProfile> profiles, CredentialVaultStore credentialVault)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_profilesFilePath)!);

            List<ConnectionProfile> source = [.. profiles];
            foreach (ConnectionProfile profile in source)
            {
                if (profile.UseIntegratedSecurity || !profile.RememberPassword)
                    credentialVault.RemoveSecret(profile.Id);
                else
                    credentialVault.SaveSecret(profile.Id, profile.Password);
            }

            List<ConnectionProfile> persistedProfiles = source
                .Select(p => new ConnectionProfile
                {
                    Id = p.Id,
                    Name = p.Name,
                    Provider = p.Provider,
                    Host = p.Host,
                    Port = p.Port,
                    Database = p.Database,
                    Username = p.Username,
                    Password = string.Empty,
                    RememberPassword = p.RememberPassword,
                    UseSsl = p.UseSsl,
                    TrustServerCertificate = p.TrustServerCertificate,
                    UseIntegratedSecurity = p.UseIntegratedSecurity,
                    TimeoutSeconds = p.TimeoutSeconds,
                })
                .ToList();

            File.WriteAllText(_profilesFilePath, JsonSerializer.Serialize(persistedProfiles, JsonOpts));
        }
        catch
        {
            // best-effort persistence
        }
    }
}

