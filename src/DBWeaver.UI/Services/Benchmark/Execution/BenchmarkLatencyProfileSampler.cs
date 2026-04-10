namespace DBWeaver.UI.Services.Benchmark;

public sealed class BenchmarkLatencyProfileSampler : IBenchmarkLatencyProfileSampler
{
    public const double SpikeProbability = 0.10;
    public const int BaseMinDelayMs = 30;
    public const int BaseMaxExclusiveDelayMs = 80;
    public const int SpikeMinDelayMs = 200;
    public const int SpikeMaxExclusiveDelayMs = 600;

    public int SampleDelayMs(Random random)
    {
        if (random.NextDouble() < SpikeProbability)
            return random.Next(SpikeMinDelayMs, SpikeMaxExclusiveDelayMs);

        return random.Next(BaseMinDelayMs, BaseMaxExclusiveDelayMs);
    }
}

