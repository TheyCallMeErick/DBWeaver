namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public interface IConnectionValidationService
{
    ConnectionValidationResultDto Validate(
        ConnectionDetailsDto details,
        ProviderCapabilityDto capability);
}
