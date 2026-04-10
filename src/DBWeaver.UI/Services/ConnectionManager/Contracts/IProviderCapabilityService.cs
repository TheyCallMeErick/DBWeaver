namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public interface IProviderCapabilityService
{
    Task<IReadOnlyList<ProviderCapabilityDto>> ListCapabilitiesAsync(CancellationToken cancellationToken = default);

    Task<ProviderCapabilityDto?> GetCapabilityAsync(
        string provider,
        CancellationToken cancellationToken = default);
}
