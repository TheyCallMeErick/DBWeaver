namespace DBWeaver.UI.ViewModels;

public interface IFireAndForgetSafetyExecutor
{
    Task ExecuteSafeAsync(Func<Task> operation, string operationName);
}
