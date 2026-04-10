using DBWeaver.UI.Services.Explain;
namespace DBWeaver.Tests.Integration.Explain;

internal static class DockerIntegrationRequirements
{
    private const string EnabledVariable = "VSA_RUN_DB_CONTAINER_TESTS";

    public static bool IsEnabled()
    {
        string? enabled = Environment.GetEnvironmentVariable(EnabledVariable);
        return IsTrue(enabled);
    }

    private static bool IsTrue(string? value) =>
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}

