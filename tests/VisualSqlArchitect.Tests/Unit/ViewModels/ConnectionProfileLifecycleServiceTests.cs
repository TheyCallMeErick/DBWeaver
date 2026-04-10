using DBWeaver.UI.Services.ConnectionManager;
using DBWeaver.UI.Services.Benchmark;
using System.Collections.ObjectModel;
using DBWeaver.Core;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class ConnectionProfileLifecycleServiceTests
{
    [Fact]
    public void Save_NewProfile_AddsAndSelectsProfile()
    {
        var service = new ConnectionProfileLifecycleService();
        var profiles = new ObservableCollection<ConnectionProfile>();
        var profile = BuildProfile("p1");

        ConnectionProfileSaveResult result = service.Save(profiles, profile, activeProfileId: null);

        Assert.Single(profiles);
        Assert.Same(profile, profiles[0]);
        Assert.Same(profile, result.SelectedProfile);
        Assert.False(result.ActiveProfileAffected);
    }

    [Fact]
    public void Save_ExistingProfile_ReplacesById()
    {
        var service = new ConnectionProfileLifecycleService();
        var profiles = new ObservableCollection<ConnectionProfile>
        {
            BuildProfile("p1", name: "Old")
        };
        var updated = BuildProfile("p1", name: "New");

        ConnectionProfileSaveResult result = service.Save(profiles, updated, activeProfileId: "p1");

        Assert.Single(profiles);
        Assert.Equal("New", profiles[0].Name);
        Assert.True(result.ActiveProfileAffected);
    }

    [Fact]
    public void Delete_NullSelection_DoesNothing()
    {
        var service = new ConnectionProfileLifecycleService();
        var profiles = new ObservableCollection<ConnectionProfile>();

        ConnectionProfileDeleteResult result = service.Delete(profiles, null, activeProfileId: "p1");

        Assert.False(result.Deleted);
        Assert.Equal("p1", result.NextActiveProfileId);
    }

    [Fact]
    public void Delete_SelectedActiveProfile_ClearsActiveAndRemovesProfile()
    {
        var service = new ConnectionProfileLifecycleService();
        var selected = BuildProfile("p1");
        var profiles = new ObservableCollection<ConnectionProfile> { selected };

        ConnectionProfileDeleteResult result = service.Delete(profiles, selected, activeProfileId: "p1");

        Assert.True(result.Deleted);
        Assert.Empty(profiles);
        Assert.Null(result.NextActiveProfileId);
        Assert.Equal("p1", result.RemovedProfileId);
        Assert.False(result.IsEditing);
        Assert.Equal(string.Empty, result.TestStatus);
    }

    private static ConnectionProfile BuildProfile(string id, string name = "Local") =>
        new()
        {
            Id = id,
            Name = name,
            Provider = DatabaseProvider.Postgres,
            Host = "localhost",
            Port = 5432,
            Database = "db",
            Username = "u",
            Password = "p",
            TimeoutSeconds = 30,
        };
}


