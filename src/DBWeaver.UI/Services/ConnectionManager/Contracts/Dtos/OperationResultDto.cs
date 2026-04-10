namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public sealed record OperationResultDto<T>(
    bool Success,
    ConnectionOperationSemanticErrorCode SemanticErrorCode,
    string UserMessage,
    T? Payload,
    string? TechnicalError,
    string? CorrelationId);
