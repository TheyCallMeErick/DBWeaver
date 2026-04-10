using DBWeaver.Core;

namespace DBWeaver.UI.Services.ConnectionManager;

public readonly record struct ConnectionProfileFormData(
    string Id,
    string Name,
    DatabaseProvider Provider,
    string Host,
    int Port,
    string Database,
    string Username,
    string Password,
    bool RememberPassword,
    bool UseSsl,
    bool TrustServerCertificate,
    bool UseIntegratedSecurity,
    int TimeoutSeconds,
    string ConnectionUrl);

