namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public enum ConnectionSessionStateDto
{
    Inactive = 0,
    Connecting,
    Active,
    Disconnecting,
    Failed
}
