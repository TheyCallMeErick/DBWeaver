using DBWeaver.UI.Services.ConnectionManager.Contracts;

namespace DBWeaver.UI.Services.ConnectionManager;

public sealed class ConnectionSessionService : IConnectionSessionService
{
    private readonly IConnectionTestService _connectionTestService;
    private readonly IConnectionTelemetryService _connectionTelemetryService;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ActiveConnectionSessionDto _activeSession = new(
        ConnectionId: null,
        SessionState: ConnectionSessionStateDto.Inactive,
        StartedAt: null,
        SessionLabel: null);

    public ConnectionSessionService(
        IConnectionTestService connectionTestService,
        IConnectionTelemetryService connectionTelemetryService)
    {
        _connectionTestService = connectionTestService;
        _connectionTelemetryService = connectionTelemetryService;
    }

    public async Task<OperationResultDto<ActiveConnectionSessionDto>> ConnectAsync(
        ConnectionDetailsDto details,
        CancellationToken cancellationToken = default)
    {
        string connectionId = string.IsNullOrWhiteSpace(details.Id)
            ? Guid.NewGuid().ToString()
            : details.Id;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            _activeSession = new ActiveConnectionSessionDto(
                ConnectionId: connectionId,
                SessionState: ConnectionSessionStateDto.Connecting,
                StartedAt: null,
                SessionLabel: details.Name);
        }
        finally
        {
            _gate.Release();
        }

        OperationResultDto<ConnectionTestResultDto> testResult = await _connectionTestService.TestAsync(details, cancellationToken);
        if (!testResult.Success)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                _activeSession = new ActiveConnectionSessionDto(
                    ConnectionId: connectionId,
                    SessionState: ConnectionSessionStateDto.Failed,
                    StartedAt: null,
                    SessionLabel: details.Name);
            }
            finally
            {
                _gate.Release();
            }

            await _connectionTelemetryService.TrackAsync(
                "connection.session.connect.failed",
                new Dictionary<string, object?>
                {
                    ["connectionId"] = connectionId,
                    ["provider"] = details.Provider,
                    ["errorCode"] = testResult.SemanticErrorCode.ToString(),
                },
                cancellationToken);

            return new OperationResultDto<ActiveConnectionSessionDto>(
                Success: false,
                SemanticErrorCode: testResult.SemanticErrorCode,
                UserMessage: testResult.UserMessage,
                Payload: _activeSession,
                TechnicalError: testResult.TechnicalError,
                CorrelationId: null);
        }

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            _activeSession = new ActiveConnectionSessionDto(
                ConnectionId: connectionId,
                SessionState: ConnectionSessionStateDto.Active,
                StartedAt: startedAt,
                SessionLabel: details.Name);
        }
        finally
        {
            _gate.Release();
        }

        await _connectionTelemetryService.TrackAsync(
            "connection.session.connect.succeeded",
            new Dictionary<string, object?>
            {
                ["connectionId"] = connectionId,
                ["provider"] = details.Provider,
            },
            cancellationToken);

        return new OperationResultDto<ActiveConnectionSessionDto>(
            Success: true,
            SemanticErrorCode: ConnectionOperationSemanticErrorCode.None,
            UserMessage: string.Empty,
            Payload: _activeSession,
            TechnicalError: null,
            CorrelationId: null);
    }

    public async Task<OperationResultDto<ActiveConnectionSessionDto>> DisconnectAsync(
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return new OperationResultDto<ActiveConnectionSessionDto>(
                Success: false,
                SemanticErrorCode: ConnectionOperationSemanticErrorCode.ValidationFailed,
                UserMessage: "Connection id is required.",
                Payload: _activeSession,
                TechnicalError: null,
                CorrelationId: null);
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_activeSession.ConnectionId is null)
            {
                return new OperationResultDto<ActiveConnectionSessionDto>(
                    Success: false,
                    SemanticErrorCode: ConnectionOperationSemanticErrorCode.NotFound,
                    UserMessage: "There is no active connection to disconnect.",
                    Payload: _activeSession,
                    TechnicalError: null,
                    CorrelationId: null);
            }

            if (!string.Equals(_activeSession.ConnectionId, connectionId, StringComparison.Ordinal))
            {
                return new OperationResultDto<ActiveConnectionSessionDto>(
                    Success: false,
                    SemanticErrorCode: ConnectionOperationSemanticErrorCode.Conflict,
                    UserMessage: "A different connection is active.",
                    Payload: _activeSession,
                    TechnicalError: null,
                    CorrelationId: null);
            }

            _activeSession = _activeSession with { SessionState = ConnectionSessionStateDto.Disconnecting };
            _activeSession = new ActiveConnectionSessionDto(
                ConnectionId: null,
                SessionState: ConnectionSessionStateDto.Inactive,
                StartedAt: null,
                SessionLabel: null);
        }
        finally
        {
            _gate.Release();
        }

        await _connectionTelemetryService.TrackAsync(
            "connection.session.disconnect",
            new Dictionary<string, object?>
            {
                ["connectionId"] = connectionId,
            },
            cancellationToken);

        return new OperationResultDto<ActiveConnectionSessionDto>(
            Success: true,
            SemanticErrorCode: ConnectionOperationSemanticErrorCode.None,
            UserMessage: string.Empty,
            Payload: _activeSession,
            TechnicalError: null,
            CorrelationId: null);
    }

    public Task<ActiveConnectionSessionDto> GetActiveSessionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_activeSession);
    }
}
