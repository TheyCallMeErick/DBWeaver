namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public interface IConnectionUrlParserService
{
    Task<ConnectionUrlParseResultDto> ParseAsync(
        string rawUrl,
        string? selectedProvider,
        CancellationToken cancellationToken = default);
}
