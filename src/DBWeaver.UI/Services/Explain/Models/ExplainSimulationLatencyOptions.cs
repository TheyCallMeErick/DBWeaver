using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public interface IExplainSimulationLatencyOptions
{
    int ResolveDelayMs();
}

public sealed class ExplainSimulationLatencyOptions : IExplainSimulationLatencyOptions
{
    public const int BuiltInDefaultDelayMs = 0;
    public const int MaximumDelayMs = 5_000;
    public const string DelayEnvironmentVariable = "VSA_EXPLAIN_SIMULATION_DELAY_MS";

    public int ResolveDelayMs()
    {
        string? fromEnv = Environment.GetEnvironmentVariable(DelayEnvironmentVariable);
        if (int.TryParse(fromEnv, out int parsed))
            return Math.Clamp(parsed, BuiltInDefaultDelayMs, MaximumDelayMs);

        return BuiltInDefaultDelayMs;
    }
}



