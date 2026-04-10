namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public interface IConnectionTestService
{
    Task<OperationResultDto<ConnectionTestResultDto>> TestAsync(
        ConnectionDetailsDto details,
        CancellationToken cancellationToken = default);
}
