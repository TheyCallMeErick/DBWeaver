using System.Collections.ObjectModel;

namespace DBWeaver.UI.Services.ConnectionManager;

public sealed class ConnectionProfileLifecycleService : IConnectionProfileLifecycleService
{
    public ConnectionProfileSaveResult Save(
        ObservableCollection<ConnectionProfile> profiles,
        ConnectionProfile profile,
        string? activeProfileId)
    {
        ConnectionProfile? existing = profiles.FirstOrDefault(p => p.Id == profile.Id);
        if (existing is null)
        {
            profiles.Add(profile);
        }
        else
        {
            int idx = profiles.IndexOf(existing);
            profiles[idx] = profile;
        }

        bool activeProfileAffected = string.Equals(activeProfileId, profile.Id, StringComparison.Ordinal);
        return new ConnectionProfileSaveResult(profile, activeProfileAffected);
    }

    public ConnectionProfileDeleteResult Delete(
        ObservableCollection<ConnectionProfile> profiles,
        ConnectionProfile? selectedProfile,
        string? activeProfileId)
    {
        if (selectedProfile is null)
            return new ConnectionProfileDeleteResult(false, null, activeProfileId, true, string.Empty);

        string? nextActive = string.Equals(selectedProfile.Id, activeProfileId, StringComparison.Ordinal)
            ? null
            : activeProfileId;

        profiles.Remove(selectedProfile);

        return new ConnectionProfileDeleteResult(
            Deleted: true,
            RemovedProfileId: selectedProfile.Id,
            NextActiveProfileId: nextActive,
            IsEditing: false,
            TestStatus: string.Empty);
    }
}

