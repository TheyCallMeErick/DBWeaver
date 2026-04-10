namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public enum ConnectionOperationSemanticErrorCode
{
    None = 0,
    ValidationFailed,
    NotFound,
    Conflict,
    ProviderMismatch,
    ParseFailed,
    ParsePartial,
    AuthenticationFailed,
    Timeout,
    OperationBlocked,
    Unknown
}
