namespace DBWeaver.UI.Services.Benchmark;

public readonly record struct BenchmarkRunContextCreationResult(
    BenchmarkRunContext? Context,
    string? RejectionMessage)
{
    public bool CanStart => Context.HasValue;
}

