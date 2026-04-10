using System.Collections.Generic;

namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public sealed record ProviderCapabilityDto(
    string Provider,
    bool SupportsUrlMode,
    bool SupportsSsl,
    bool SupportsIntegratedSecurity,
    bool RequiresDatabase,
    IReadOnlyList<string> SupportedUrlSchemes,
    IReadOnlyList<string> RequiredFieldKeys);
