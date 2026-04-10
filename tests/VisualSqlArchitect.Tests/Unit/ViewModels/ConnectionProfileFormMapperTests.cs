using DBWeaver.UI.Services.ConnectionManager;
using DBWeaver.UI.Services.Benchmark;
using System.ComponentModel;
using DBWeaver.Core;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class ConnectionProfileFormMapperTests
{
    [Fact]
    public void CreateNew_UsesExpectedDefaults()
    {
        var mapper = new ConnectionProfileFormMapper(new FakeLocalizationService());

        ConnectionProfileFormData data = mapper.CreateNew();

        Assert.False(string.IsNullOrWhiteSpace(data.Id));
        Assert.Equal("New Connection", data.Name);
        Assert.Equal(DatabaseProvider.Postgres, data.Provider);
        Assert.Equal("localhost", data.Host);
        Assert.Equal(5432, data.Port);
        Assert.Equal(string.Empty, data.Database);
        Assert.Equal(string.Empty, data.Username);
        Assert.Equal(string.Empty, data.Password);
        Assert.False(data.UseIntegratedSecurity);
        Assert.Equal(30, data.TimeoutSeconds);
    }

    [Fact]
    public void FromProfile_And_ToProfile_RoundTripValues()
    {
        var mapper = new ConnectionProfileFormMapper(new FakeLocalizationService());
        var profile = new ConnectionProfile
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Local",
            Provider = DatabaseProvider.MySql,
            Host = "db.local",
            Port = 3306,
            Database = "sales",
            Username = "user",
            Password = "secret",
            UseIntegratedSecurity = true,
            TimeoutSeconds = 42,
        };

        ConnectionProfileFormData data = mapper.FromProfile(profile);
        ConnectionProfile mapped = mapper.ToProfile(data);

        Assert.Equal(profile.Id, mapped.Id);
        Assert.Equal(profile.Name, mapped.Name);
        Assert.Equal(profile.Provider, mapped.Provider);
        Assert.Equal(profile.Host, mapped.Host);
        Assert.Equal(profile.Port, mapped.Port);
        Assert.Equal(profile.Database, mapped.Database);
        Assert.Equal(profile.Username, mapped.Username);
        Assert.Equal(profile.Password, mapped.Password);
        Assert.Equal(profile.UseIntegratedSecurity, mapped.UseIntegratedSecurity);
        Assert.Equal(profile.TimeoutSeconds, mapped.TimeoutSeconds);
    }

    private sealed class FakeLocalizationService : ILocalizationService
    {
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public string CurrentCulture => "en-US";
        public string CurrentLanguageLabel => "English";

        public string this[string key] => key == "connection.new" ? "New Connection" : key;

        public bool ToggleCulture() => false;
        public bool SetCulture(string culture) => false;
    }
}


