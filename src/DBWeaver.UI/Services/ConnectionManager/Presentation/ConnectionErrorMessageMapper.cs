using DBWeaver.Core;
using DBWeaver.UI.Services.Localization;

namespace DBWeaver.UI.Services.ConnectionManager;

public sealed class ConnectionErrorMessageMapper(ILocalizationService localization) : IConnectionErrorMessageMapper
{
    private readonly ILocalizationService _localization = localization;

    public string Map(Exception ex, DatabaseProvider provider)
    {
        string msg = ex.Message;

        if (ex is OperationCanceledException or TimeoutException)
            return L("connection.error.timeoutReachability", "Connection timed out - check that the server is reachable and increase the timeout if needed.");

        string lower = msg.ToLowerInvariant();

        if (ContainsAny(lower, "password", "authentication failed", "invalid password", "no pg_hba.conf entry", "login failed", "access denied"))
            return string.Format(
                L("connection.error.authenticationFailedForProvider", "Authentication failed - verify username and password for {0}."),
                provider);

        if (ContainsAny(lower, "does not exist", "unknown database", "database", "catalog"))
            return string.Format(
                L("connection.error.databaseNotFoundForProvider", "Database not found - confirm the database name exists on {0}."),
                provider);

        if (ContainsAny(lower, "name or service not known", "no such host", "getaddrinfo", "nodename nor servname", "server not found", "host", "dns"))
            return L("connection.error.hostNotFound", "Host not found - check the server address and DNS resolution.");

        if (ContainsAny(lower, "connection refused", "refused", "econnrefused"))
            return L(
                "connection.error.portRefused",
                "Port connection refused - check the port number and that the server is running / firewall rules allow access.");

        if (ContainsAny(lower, "ssl", "tls", "certificate", "x509"))
            return L("connection.error.sslTls", "SSL/TLS error - check the server's SSL configuration or disable SSL for local connections.");

        if (ContainsAny(lower, "timeout", "timed out", "deadlock"))
            return L("connection.error.timeoutOverloaded", "Connection timed out - the server may be overloaded or unreachable. Try increasing the timeout.");

        if (ContainsAny(lower, "permission denied", "privilege"))
            return L("connection.error.insufficientPrivileges", "Insufficient privileges - the user may lack permission to connect to this database.");

        return msg.Length > 160 ? msg[..160] + "..." : msg;
    }

    private static bool ContainsAny(string source, params string[] tokens) =>
        tokens.Any(t => source.Contains(t, StringComparison.OrdinalIgnoreCase));

    private string L(string key, string fallback)
    {
        string value = _localization[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}

