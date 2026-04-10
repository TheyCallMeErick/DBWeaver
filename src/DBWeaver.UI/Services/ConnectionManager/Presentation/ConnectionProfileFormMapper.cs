using DBWeaver.Core;
using DBWeaver.UI.Services;
using DBWeaver.UI.Services.Localization;

namespace DBWeaver.UI.Services.ConnectionManager;

public sealed class ConnectionProfileFormMapper(ILocalizationService localization) : IConnectionProfileFormMapper
{
    private readonly ILocalizationService _localization = localization;

    public ConnectionProfileFormData CreateNew() =>
        new(
            Id: Guid.NewGuid().ToString(),
            Name: _localization["connection.new"],
            Provider: DatabaseProvider.Postgres,
            Host: AppConstants.DefaultHost,
            Port: ConnectionProfile.DefaultPort(DatabaseProvider.Postgres),
            Database: string.Empty,
            Username: string.Empty,
            Password: string.Empty,
            RememberPassword: true,
            UseSsl: false,
            TrustServerCertificate: true,
            UseIntegratedSecurity: false,
            TimeoutSeconds: 30,
            ConnectionUrl: string.Empty);

    public ConnectionProfileFormData FromProfile(ConnectionProfile profile) =>
        new(
            Id: profile.Id,
            Name: profile.Name,
            Provider: profile.Provider,
            Host: profile.Host,
            Port: profile.Port,
            Database: profile.Database,
            Username: profile.Username,
            Password: profile.Password,
            RememberPassword: profile.RememberPassword,
            UseSsl: profile.UseSsl,
            TrustServerCertificate: profile.TrustServerCertificate,
            UseIntegratedSecurity: profile.UseIntegratedSecurity,
            TimeoutSeconds: profile.TimeoutSeconds,
            ConnectionUrl: string.Empty);

    public ConnectionProfile ToProfile(ConnectionProfileFormData formData) =>
        new()
        {
            Id = formData.Id,
            Name = formData.Name,
            Provider = formData.Provider,
            Host = formData.Host,
            Port = formData.Port,
            Database = formData.Database,
            Username = formData.Username,
            Password = formData.Password,
            RememberPassword = formData.RememberPassword,
            UseSsl = formData.UseSsl,
            TrustServerCertificate = formData.TrustServerCertificate,
            UseIntegratedSecurity = formData.UseIntegratedSecurity,
            TimeoutSeconds = formData.TimeoutSeconds,
        };
}

