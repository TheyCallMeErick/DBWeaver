using DBWeaver.Core;
using DBWeaver.UI.Services.ConnectionManager.Contracts;

namespace DBWeaver.UI.Services.ConnectionManager;

internal static class ConnectionContractMapper
{
    internal const string HostKey = "host";
    internal const string PortKey = "port";
    internal const string DatabaseKey = "database";
    internal const string UsernameKey = "username";
    internal const string PasswordKey = "password";
    internal const string RememberPasswordKey = "rememberPassword";
    internal const string UseSslKey = "useSsl";
    internal const string TrustServerCertificateKey = "trustServerCertificate";
    internal const string UseIntegratedSecurityKey = "useIntegratedSecurity";
    internal const string TimeoutSecondsKey = "timeoutSeconds";

    public static ConnectionSummaryDto ToSummary(ConnectionProfile profile, bool isActive)
    {
        string summary = profile.Provider == DatabaseProvider.SQLite
            ? profile.Database
            : $"{profile.Host}:{profile.Port}/{profile.Database}";

        return new ConnectionSummaryDto(
            Id: profile.Id,
            Name: profile.Name,
            Provider: profile.Provider.ToString(),
            SummaryText: summary,
            IsFavorite: false,
            IsActive: isActive,
            LastUsedAt: null,
            LastTestStatus: ConnectionTestStatusDto.NotTested,
            LastTestAt: null);
    }

    public static ConnectionDetailsDto ToDetails(ConnectionProfile profile)
    {
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [HostKey] = profile.Host,
            [PortKey] = profile.Port.ToString(),
            [DatabaseKey] = profile.Database,
            [UsernameKey] = profile.Username,
            [PasswordKey] = profile.Password,
            [RememberPasswordKey] = profile.RememberPassword.ToString(),
            [UseSslKey] = profile.UseSsl.ToString(),
            [TrustServerCertificateKey] = profile.TrustServerCertificate.ToString(),
            [UseIntegratedSecurityKey] = profile.UseIntegratedSecurity.ToString(),
            [TimeoutSecondsKey] = profile.TimeoutSeconds.ToString(),
        };

        return new ConnectionDetailsDto(
            Id: profile.Id,
            Name: profile.Name,
            Provider: profile.Provider.ToString(),
            Mode: ConnectionProviderModeDto.Fields,
            FieldValues: fields,
            UrlValue: null,
            Tag: null,
            IsFavorite: false,
            AdvancedOptions: new Dictionary<string, string?>());
    }

    public static ConnectionProfile ToProfile(
        ConnectionDetailsDto details,
        IReadOnlyDictionary<string, string?>? fieldsOverride = null,
        string? providerOverride = null)
    {
        DatabaseProvider provider = ParseProvider(providerOverride ?? details.Provider);
        IReadOnlyDictionary<string, string?> fields = fieldsOverride ?? details.FieldValues;

        int defaultPort = ConnectionProfile.DefaultPort(provider);
        int port = provider == DatabaseProvider.SQLite
            ? 0
            : ParseInt(fields, PortKey, defaultPort);

        bool useIntegratedSecurity = ParseBool(fields, UseIntegratedSecurityKey, false)
            && provider == DatabaseProvider.SqlServer
            && OperatingSystem.IsWindows();

        return new ConnectionProfile
        {
            Id = string.IsNullOrWhiteSpace(details.Id) ? Guid.NewGuid().ToString() : details.Id,
            Name = string.IsNullOrWhiteSpace(details.Name) ? "New Connection" : details.Name.Trim(),
            Provider = provider,
            Host = provider == DatabaseProvider.SQLite
                ? AppConstants.DefaultHost
                : ParseString(fields, HostKey, AppConstants.DefaultHost),
            Port = port,
            Database = ParseString(fields, DatabaseKey, string.Empty),
            Username = ParseString(fields, UsernameKey, string.Empty),
            Password = ParseString(fields, PasswordKey, string.Empty),
            RememberPassword = ParseBool(fields, RememberPasswordKey, true),
            UseSsl = ParseBool(fields, UseSslKey, false),
            TrustServerCertificate = ParseBool(fields, TrustServerCertificateKey, true),
            UseIntegratedSecurity = useIntegratedSecurity,
            TimeoutSeconds = Math.Max(1, ParseInt(fields, TimeoutSecondsKey, 30)),
        };
    }

    public static bool TryParseProvider(string provider, out DatabaseProvider parsed)
    {
        parsed = provider.Trim().ToLowerInvariant() switch
        {
            "postgres" or "postgresql" => DatabaseProvider.Postgres,
            "mysql" => DatabaseProvider.MySql,
            "sqlserver" or "mssql" => DatabaseProvider.SqlServer,
            "sqlite" or "file" => DatabaseProvider.SQLite,
            _ => default,
        };

        return provider.Equals("postgres", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("mysql", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("sqlserver", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("mssql", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("file", StringComparison.OrdinalIgnoreCase);
    }

    private static DatabaseProvider ParseProvider(string provider) =>
        TryParseProvider(provider, out DatabaseProvider parsed)
            ? parsed
            : DatabaseProvider.Postgres;

    private static string ParseString(IReadOnlyDictionary<string, string?> fields, string key, string fallback)
    {
        if (!fields.TryGetValue(key, out string? raw) || string.IsNullOrWhiteSpace(raw))
            return fallback;

        return raw.Trim();
    }

    private static int ParseInt(IReadOnlyDictionary<string, string?> fields, string key, int fallback)
    {
        if (!fields.TryGetValue(key, out string? raw) || !int.TryParse(raw, out int parsed))
            return fallback;

        return parsed;
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string?> fields, string key, bool fallback)
    {
        if (!fields.TryGetValue(key, out string? raw) || string.IsNullOrWhiteSpace(raw))
            return fallback;

        if (bool.TryParse(raw, out bool parsed))
            return parsed;

        return raw.Equals("1", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("on", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("required", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("require", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
}
