namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public interface IConnectionCatalogService
{
    Task<IReadOnlyList<ConnectionSummaryDto>> ListSummariesAsync(CancellationToken cancellationToken = default);

    Task<OperationResultDto<ConnectionDetailsDto>> GetDetailsAsync(
        string connectionId,
        CancellationToken cancellationToken = default);

    Task<OperationResultDto<ConnectionDetailsDto>> SaveAsync(
        ConnectionDetailsDto details,
        CancellationToken cancellationToken = default);

    Task<OperationResultDto<ConnectionDetailsDto>> DuplicateAsync(
        string connectionId,
        string? newName = null,
        CancellationToken cancellationToken = default);

    Task<OperationResultDto<bool>> DeleteAsync(
        string connectionId,
        CancellationToken cancellationToken = default);
}
