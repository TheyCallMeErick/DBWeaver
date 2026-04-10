namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public enum ConnectionUrlParseStatusDto
{
    Idle = 0,
    Parsing,
    Success,
    Partial,
    Failed
}
