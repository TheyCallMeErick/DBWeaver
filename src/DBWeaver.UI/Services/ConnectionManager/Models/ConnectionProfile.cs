using DBWeaver.Core;
using DBWeaver.UI.Services;
using DBWeaver.UI.Services.Connection;

namespace DBWeaver.UI.Services.ConnectionManager.Models;

public sealed class ConnectionProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Connection";
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Postgres;
    public string Host { get; set; } = AppConstants.DefaultHost;
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool RememberPassword { get; set; } = true;
    public bool UseSsl { get; set; }
    public bool TrustServerCertificate { get; set; } = true;
    public bool UseIntegratedSecurity { get; set; }
    public int TimeoutSeconds { get; set; } = 30;

    public static int DefaultPort(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Postgres => 5432,
        DatabaseProvider.MySql => 3306,
        DatabaseProvider.SqlServer => 1433,
        DatabaseProvider.SQLite => 0,
        _ => 5432,
    };

    public ConnectionProfile WithProtectedPassword()
    {
        return new()
        {
            Id = Id,
            Name = Name,
            Provider = Provider,
            Host = Host,
            Port = Port,
            Database = Database,
            Username = Username,
            Password = CredentialProtector.Protect(Password),
            RememberPassword = RememberPassword,
            UseSsl = UseSsl,
            TrustServerCertificate = TrustServerCertificate,
            UseIntegratedSecurity = UseIntegratedSecurity,
            TimeoutSeconds = TimeoutSeconds,
        };
    }

    public ConnectionProfile WithUnprotectedPassword()
    {
        return new()
        {
            Id = Id,
            Name = Name,
            Provider = Provider,
            Host = Host,
            Port = Port,
            Database = Database,
            Username = Username,
            Password = CredentialProtector.Unprotect(Password),
            RememberPassword = RememberPassword,
            UseSsl = UseSsl,
            TrustServerCertificate = TrustServerCertificate,
            UseIntegratedSecurity = UseIntegratedSecurity,
            TimeoutSeconds = TimeoutSeconds,
        };
    }

    public ConnectionConfig ToConnectionConfig() =>
        new(
            Provider,
            Host,
            Port,
            Database,
            Username,
            Password,
            UseIntegratedSecurity,
            TimeoutSeconds,
            BuildExtraParameters());

    private Dictionary<string, string> BuildExtraParameters()
    {
        var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UseSsl"] = UseSsl ? "true" : "false",
            ["TrustServerCertificate"] = TrustServerCertificate ? "true" : "false",
        };

        return extras;
    }
}
