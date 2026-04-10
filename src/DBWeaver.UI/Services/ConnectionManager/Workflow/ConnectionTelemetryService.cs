using DBWeaver.UI.Services.ConnectionManager.Contracts;
using Microsoft.Extensions.Logging;

namespace DBWeaver.UI.Services.ConnectionManager;

public sealed class ConnectionTelemetryService : IConnectionTelemetryService
{
    private readonly ILogger<ConnectionTelemetryService> _logger;

    public ConnectionTelemetryService(ILogger<ConnectionTelemetryService> logger)
    {
        _logger = logger;
    }

    public Task TrackAsync(
        string eventName,
        IReadOnlyDictionary<string, object?> properties,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connection telemetry event: {EventName} {@Properties}", eventName, properties);
        return Task.CompletedTask;
    }
}
