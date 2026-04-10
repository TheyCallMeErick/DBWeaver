using DBWeaver.Metadata;
using Xunit;

namespace DBWeaver.Tests.Unit.Metadata;

public sealed class CanvasTableTrackerTests
{
    [Fact]
    public void AddAndContains_AreCaseInsensitive()
    {
        ICanvasTableTracker tracker = new CanvasTableTracker();

        tracker.Add("public.orders");

        Assert.True(tracker.Contains("PUBLIC.ORDERS"));
        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void Remove_DeletesExistingEntry()
    {
        ICanvasTableTracker tracker = new CanvasTableTracker();
        tracker.Add("public.orders");

        tracker.Remove("public.orders");

        Assert.False(tracker.Contains("public.orders"));
        Assert.Equal(0, tracker.Count);
    }

    [Fact]
    public async Task Snapshot_IsStableDuringConcurrentWrites()
    {
        ICanvasTableTracker tracker = new CanvasTableTracker();
        Task[] adds = Enumerable
            .Range(0, 50)
            .Select(i => Task.Run(() => tracker.Add($"public.t{i}")))
            .ToArray();

        await Task.WhenAll(adds);
        IReadOnlyList<string> snapshot = tracker.Snapshot();

        Assert.Equal(50, snapshot.Count);
        Assert.Equal(50, tracker.Count);
    }
}
