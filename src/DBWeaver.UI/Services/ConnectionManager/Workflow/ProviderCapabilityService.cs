using DBWeaver.UI.Services.ConnectionManager.Contracts;

namespace DBWeaver.UI.Services.ConnectionManager;

public sealed class ProviderCapabilityService : IProviderCapabilityService
{
    private static readonly IReadOnlyList<ProviderCapabilityDto> _capabilities =
    [
        new ProviderCapabilityDto(
            Provider: "Postgres",
            SupportsUrlMode: true,
            SupportsSsl: true,
            SupportsIntegratedSecurity: false,
            RequiresDatabase: true,
            SupportedUrlSchemes: ["postgres", "postgresql"],
            RequiredFieldKeys: [
                ConnectionContractMapper.HostKey,
                ConnectionContractMapper.PortKey,
                ConnectionContractMapper.DatabaseKey,
                ConnectionContractMapper.UsernameKey,
            ]),
        new ProviderCapabilityDto(
            Provider: "MySql",
            SupportsUrlMode: true,
            SupportsSsl: true,
            SupportsIntegratedSecurity: false,
            RequiresDatabase: true,
            SupportedUrlSchemes: ["mysql"],
            RequiredFieldKeys: [
                ConnectionContractMapper.HostKey,
                ConnectionContractMapper.PortKey,
                ConnectionContractMapper.DatabaseKey,
                ConnectionContractMapper.UsernameKey,
            ]),
        new ProviderCapabilityDto(
            Provider: "SqlServer",
            SupportsUrlMode: true,
            SupportsSsl: true,
            SupportsIntegratedSecurity: OperatingSystem.IsWindows(),
            RequiresDatabase: true,
            SupportedUrlSchemes: ["sqlserver", "mssql"],
            RequiredFieldKeys: [
                ConnectionContractMapper.HostKey,
                ConnectionContractMapper.PortKey,
                ConnectionContractMapper.DatabaseKey,
            ]),
        new ProviderCapabilityDto(
            Provider: "SQLite",
            SupportsUrlMode: true,
            SupportsSsl: false,
            SupportsIntegratedSecurity: false,
            RequiresDatabase: true,
            SupportedUrlSchemes: ["sqlite", "file"],
            RequiredFieldKeys: [ConnectionContractMapper.DatabaseKey]),
    ];

    public Task<IReadOnlyList<ProviderCapabilityDto>> ListCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_capabilities);
    }

    public Task<ProviderCapabilityDto?> GetCapabilityAsync(string provider, CancellationToken cancellationToken = default)
    {
        ProviderCapabilityDto? capability = _capabilities.FirstOrDefault(c =>
            string.Equals(c.Provider, provider, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(capability);
    }
}
