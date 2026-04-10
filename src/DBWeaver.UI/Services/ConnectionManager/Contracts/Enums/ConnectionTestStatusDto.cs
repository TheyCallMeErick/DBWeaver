namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public enum ConnectionTestStatusDto
{
    NotTested = 0,
    Testing,
    Success,
    Failure,
    AuthenticationFailure,
    Timeout,
    Unavailable
}
