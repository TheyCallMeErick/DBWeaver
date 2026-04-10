using DBWeaver.UI.Services.Connection;

namespace DBWeaver.UI.Services.ConnectionManager;

public interface IConnectionProfileStore
{
    IReadOnlyList<ConnectionProfile> LoadProfiles(CredentialVaultStore credentialVault);

    void PersistProfiles(IEnumerable<ConnectionProfile> profiles, CredentialVaultStore credentialVault);
}

