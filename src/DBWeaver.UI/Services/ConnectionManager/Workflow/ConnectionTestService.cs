using DBWeaver.Core;
using DBWeaver.UI.Services.ConnectionManager.Contracts;

namespace DBWeaver.UI.Services.ConnectionManager;

public sealed class ConnectionTestService : IConnectionTestService
{
    private readonly IConnectionTestExecutor _connectionTestExecutor;
    private readonly IConnectionValidationService _validationService;
    private readonly IProviderCapabilityService _providerCapabilityService;
    private readonly IConnectionUrlParserService _urlParserService;

    public ConnectionTestService(
        IConnectionTestExecutor connectionTestExecutor,
        IConnectionValidationService validationService,
        IProviderCapabilityService providerCapabilityService,
        IConnectionUrlParserService urlParserService)
    {
        _connectionTestExecutor = connectionTestExecutor;
        _validationService = validationService;
        _providerCapabilityService = providerCapabilityService;
        _urlParserService = urlParserService;
    }

    public async Task<OperationResultDto<ConnectionTestResultDto>> TestAsync(
        ConnectionDetailsDto details,
        CancellationToken cancellationToken = default)
    {
        ProviderCapabilityDto? capability = await _providerCapabilityService.GetCapabilityAsync(details.Provider, cancellationToken);
        if (capability is null)
        {
            return Fail(
                ConnectionOperationSemanticErrorCode.NotFound,
                "Unsupported provider.",
                new ConnectionTestResultDto(
                    Status: ConnectionTestStatusDto.Unavailable,
                    SummaryMessage: "Unsupported provider.",
                    TechnicalDetails: null,
                    LatencyMs: null,
                    ProviderErrorCode: null,
                    TestedAt: DateTimeOffset.UtcNow));
        }

        ConnectionValidationResultDto validation = _validationService.Validate(details, capability);
        if (!validation.IsValid)
        {
            string message = validation.Errors.FirstOrDefault()?.Message ?? "Invalid connection details.";
            return Fail(
                ConnectionOperationSemanticErrorCode.ValidationFailed,
                message,
                new ConnectionTestResultDto(
                    Status: ConnectionTestStatusDto.Unavailable,
                    SummaryMessage: message,
                    TechnicalDetails: null,
                    LatencyMs: null,
                    ProviderErrorCode: null,
                    TestedAt: DateTimeOffset.UtcNow));
        }

        OperationResultDto<ConnectionProfile> profileResult = await BuildProfileAsync(details, cancellationToken);
        if (!profileResult.Success || profileResult.Payload is null)
        {
            return Fail(
                profileResult.SemanticErrorCode,
                profileResult.UserMessage,
                new ConnectionTestResultDto(
                    Status: ConnectionTestStatusDto.Unavailable,
                    SummaryMessage: profileResult.UserMessage,
                    TechnicalDetails: profileResult.TechnicalError,
                    LatencyMs: null,
                    ProviderErrorCode: null,
                    TestedAt: DateTimeOffset.UtcNow),
                profileResult.TechnicalError);
        }

        ConnectionProfile profile = profileResult.Payload;
        ConnectionTestResult result;

        try
        {
            result = await _connectionTestExecutor.ExecuteAsync(
                profile.ToConnectionConfig(),
                profile.Provider,
                profile.TimeoutSeconds,
                cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            return Fail(
                ConnectionOperationSemanticErrorCode.Timeout,
                "Connection test was canceled.",
                new ConnectionTestResultDto(
                    Status: ConnectionTestStatusDto.Timeout,
                    SummaryMessage: "Connection test was canceled.",
                    TechnicalDetails: ex.Message,
                    LatencyMs: null,
                    ProviderErrorCode: null,
                    TestedAt: DateTimeOffset.UtcNow),
                ex.Message);
        }
        catch (Exception ex)
        {
            return Fail(
                ConnectionOperationSemanticErrorCode.Unknown,
                "Unexpected error while testing the connection.",
                new ConnectionTestResultDto(
                    Status: ConnectionTestStatusDto.Failure,
                    SummaryMessage: "Unexpected error while testing the connection.",
                    TechnicalDetails: ex.Message,
                    LatencyMs: null,
                    ProviderErrorCode: null,
                    TestedAt: DateTimeOffset.UtcNow),
                ex.Message);
        }

        if (!result.Success)
        {
            ConnectionOperationSemanticErrorCode semanticCode = MapFailureCode(result.ErrorMessage);
            ConnectionTestStatusDto status = semanticCode switch
            {
                ConnectionOperationSemanticErrorCode.AuthenticationFailed => ConnectionTestStatusDto.AuthenticationFailure,
                ConnectionOperationSemanticErrorCode.Timeout => ConnectionTestStatusDto.Timeout,
                _ => ConnectionTestStatusDto.Failure,
            };

            string summaryMessage = result.ErrorMessage ?? "Connection test failed.";
            return Fail(
                semanticCode,
                summaryMessage,
                new ConnectionTestResultDto(
                    Status: status,
                    SummaryMessage: summaryMessage,
                    TechnicalDetails: result.ErrorMessage,
                    LatencyMs: null,
                    ProviderErrorCode: null,
                    TestedAt: DateTimeOffset.UtcNow));
        }

        int? latencyMs = result.Latency is null
            ? null
            : (int)result.Latency.Value.TotalMilliseconds;

        return Ok(new ConnectionTestResultDto(
            Status: ConnectionTestStatusDto.Success,
            SummaryMessage: "Connection test succeeded.",
            TechnicalDetails: null,
            LatencyMs: latencyMs,
            ProviderErrorCode: null,
            TestedAt: DateTimeOffset.UtcNow));
    }

    private async Task<OperationResultDto<ConnectionProfile>> BuildProfileAsync(
        ConnectionDetailsDto details,
        CancellationToken cancellationToken)
    {
        if (details.Mode == ConnectionProviderModeDto.Fields)
        {
            return Ok(ConnectionContractMapper.ToProfile(details));
        }

        ConnectionUrlParseResultDto parsed = await _urlParserService.ParseAsync(details.UrlValue ?? string.Empty, details.Provider, cancellationToken);
        if (parsed.ParseStatus == ConnectionUrlParseStatusDto.Failed)
        {
            return Fail<ConnectionProfile>(
                ConnectionOperationSemanticErrorCode.ParseFailed,
                parsed.UserMessage,
                parsed.TechnicalDetails);
        }

        var mergedFields = new Dictionary<string, string?>(details.FieldValues, StringComparer.OrdinalIgnoreCase);
        foreach (UrlParseFieldTokenDto token in parsed.RecognizedFields)
        {
            mergedFields[token.Key] = token.Value;
        }

        ConnectionOperationSemanticErrorCode parseCode = parsed.ParseStatus == ConnectionUrlParseStatusDto.Partial
            ? ConnectionOperationSemanticErrorCode.ParsePartial
            : ConnectionOperationSemanticErrorCode.None;

        ConnectionProfile profile = ConnectionContractMapper.ToProfile(
            details,
            fieldsOverride: mergedFields,
            providerOverride: parsed.SuggestedProvider ?? details.Provider);

        if (parseCode != ConnectionOperationSemanticErrorCode.None)
        {
            return new OperationResultDto<ConnectionProfile>(
                Success: true,
                SemanticErrorCode: parseCode,
                UserMessage: parsed.UserMessage,
                Payload: profile,
                TechnicalError: parsed.TechnicalDetails,
                CorrelationId: null);
        }

        return Ok(profile);
    }

    private static ConnectionOperationSemanticErrorCode MapFailureCode(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return ConnectionOperationSemanticErrorCode.Unknown;

        string lowered = error.ToLowerInvariant();

        if (lowered.Contains("timeout", StringComparison.Ordinal))
            return ConnectionOperationSemanticErrorCode.Timeout;

        if (lowered.Contains("password", StringComparison.Ordinal)
            || lowered.Contains("authentication", StringComparison.Ordinal)
            || lowered.Contains("login", StringComparison.Ordinal)
            || lowered.Contains("auth", StringComparison.Ordinal))
        {
            return ConnectionOperationSemanticErrorCode.AuthenticationFailed;
        }

        return ConnectionOperationSemanticErrorCode.Unknown;
    }

    private static OperationResultDto<T> Ok<T>(T payload) =>
        new(
            Success: true,
            SemanticErrorCode: ConnectionOperationSemanticErrorCode.None,
            UserMessage: string.Empty,
            Payload: payload,
            TechnicalError: null,
            CorrelationId: null);

    private static OperationResultDto<T> Fail<T>(
        ConnectionOperationSemanticErrorCode code,
        string message,
        T payload,
        string? technicalError = null) =>
        new(
            Success: false,
            SemanticErrorCode: code,
            UserMessage: message,
            Payload: payload,
            TechnicalError: technicalError,
            CorrelationId: null);

    private static OperationResultDto<T> Fail<T>(
        ConnectionOperationSemanticErrorCode code,
        string message,
        string? technicalError = null) =>
        new(
            Success: false,
            SemanticErrorCode: code,
            UserMessage: message,
            Payload: default,
            TechnicalError: technicalError,
            CorrelationId: null);
}
