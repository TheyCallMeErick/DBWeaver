using Microsoft.Extensions.Logging;

namespace DBWeaver.UI.ViewModels;

public sealed class FireAndForgetSafetyExecutor(ILogger logger) : IFireAndForgetSafetyExecutor
{
    private readonly ILogger _logger = logger;

    public async Task ExecuteSafeAsync(Func<Task> operation, string operationName)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            // expected cancellation path
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in fire-and-forget operation: {Operation}", operationName);
        }
    }
}
