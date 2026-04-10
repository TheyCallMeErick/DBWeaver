namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public interface IConnectionSessionService
{
    Task<OperationResultDto<ActiveConnectionSessionDto>> ConnectAsync(
        ConnectionDetailsDto details,
        CancellationToken cancellationToken = default);

    Task<OperationResultDto<ActiveConnectionSessionDto>> DisconnectAsync(
        string connectionId,
        CancellationToken cancellationToken = default);

    Task<ActiveConnectionSessionDto> GetActiveSessionAsync(CancellationToken cancellationToken = default);
}
