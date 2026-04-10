using DBWeaver.UI.Services.Benchmark;


namespace DBWeaver.Tests.Unit.ViewModels;

public sealed class BenchmarkLatencyProfileSamplerTests
{
    [Fact]
    public void SampleDelayMs_AlwaysFallsInsideExpectedBuckets()
    {
        var sampler = new BenchmarkLatencyProfileSampler();
        var random = new Random(12345);

        for (int i = 0; i < 20000; i++)
        {
            int delay = sampler.SampleDelayMs(random);
            bool inBaseBucket = delay >= BenchmarkLatencyProfileSampler.BaseMinDelayMs
                                && delay < BenchmarkLatencyProfileSampler.BaseMaxExclusiveDelayMs;
            bool inSpikeBucket = delay >= BenchmarkLatencyProfileSampler.SpikeMinDelayMs
                                 && delay < BenchmarkLatencyProfileSampler.SpikeMaxExclusiveDelayMs;
            Assert.True(inBaseBucket || inSpikeBucket, $"Delay {delay} is outside expected buckets");
        }
    }

    [Fact]
    public void SampleDelayMs_WithFixedSeed_ProducesStableSpikeRatio()
    {
        var sampler = new BenchmarkLatencyProfileSampler();
        var random = new Random(42);
        const int sampleCount = 50000;

        int spikeCount = 0;
        for (int i = 0; i < sampleCount; i++)
        {
            int delay = sampler.SampleDelayMs(random);
            if (delay >= BenchmarkLatencyProfileSampler.SpikeMinDelayMs)
                spikeCount++;
        }

        double ratio = (double)spikeCount / sampleCount;
        Assert.InRange(ratio, 0.085, 0.115);
    }
}

