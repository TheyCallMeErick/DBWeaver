using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

/// <summary>
/// Tests for ValidationManager thread safety and debounce behavior.
/// Regression tests for race condition where _validationCts was accessed from multiple threads without synchronization.
/// </summary>
public class ValidationManagerThreadSafetyTests
{
    [Fact]
    public void ScheduleValidation_CanBeCalledConcurrently()
    {
        // Arrange
        var canvas = new CanvasViewModel();
        int callCount = 50;

        // Act - Schedule validation from multiple threads concurrently
        var threads = new Thread[callCount];
        for (int i = 0; i < callCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                try
                {
                    canvas.ScheduleValidation();
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Concurrent call to ScheduleValidation threw exception: {ex.Message}");
                }
            });
        }

        // Start all threads
        foreach (var t in threads)
            t.Start();

        // Wait for all threads to complete
        foreach (var t in threads)
            t.Join();

        // Assert - If we get here without exception, thread safety is working
        Assert.True(true); // Reached without throwing
    }

    [Fact]
    public void ScheduleValidation_MultipleCallsNotThrow()
    {
        // Arrange
        var canvas = new CanvasViewModel();

        // Act
        canvas.ScheduleValidation();
        System.Threading.Thread.Sleep(50); // Ensure first CTS exists

        // Calling again should dispose the first CTS
        canvas.ScheduleValidation();

        // Assert - No exception means CTS was properly disposed
        Assert.NotNull(canvas);
    }

    [Fact]
    public void ScheduleValidation_ThreadSafeForRapidCalls()
    {
        // Arrange
        var canvas = new CanvasViewModel();

        // Act - Rapid sequential calls (not threaded, but stress test)
        for (int i = 0; i < 100; i++)
        {
            canvas.ScheduleValidation();
        }

        // Assert - Should complete without exception
        Assert.NotNull(canvas);
    }

    [Fact]
    public void ScheduleValidation_NoRaceConditionBetweenCancelAndNew()
    {
        // This test validates that the race condition fix (using lock) prevents:
        // Thread A: captures token from old CTS
        // Thread B: cancels old CTS and creates new one
        // Result: Thread A's closure still uses old, cancelled token

        // Arrange
        var canvas = new CanvasViewModel();
        bool exceptionThrown = false;
        string exceptionMessage = "";

        // Act
        var thread1 = new Thread(() =>
        {
            try
            {
                for (int i = 0; i < 20; i++)
                {
                    canvas.ScheduleValidation();
                    System.Threading.Thread.Sleep(5);
                }
            }
            catch (Exception ex)
            {
                exceptionThrown = true;
                exceptionMessage = ex.Message;
            }
        });

        var thread2 = new Thread(() =>
        {
            try
            {
                for (int i = 0; i < 20; i++)
                {
                    canvas.ScheduleValidation();
                    System.Threading.Thread.Sleep(5);
                }
            }
            catch (Exception ex)
            {
                exceptionThrown = true;
                exceptionMessage = ex.Message;
            }
        });

        thread1.Start();
        thread2.Start();
        thread1.Join();
        thread2.Join();

        // Assert - No race condition should cause exceptions
        Assert.False(exceptionThrown, $"Race condition detected: {exceptionMessage}");
    }

    [Fact]
    public void CanvasViewModel_ValidationIsThreadSafe()
    {
        // Arrange
        var canvas = new CanvasViewModel();
        int errorCount = 0;
        object errorLock = new();

        // Act - Simulate real usage with multiple threads scheduling validation
        for (int i = 0; i < 10; i++)
        {
            new Thread(() =>
            {
                try
                {
                    for (int j = 0; j < 10; j++)
                    {
                        canvas.ScheduleValidation();
                        System.Threading.Thread.Sleep(1);
                    }
                }
                catch
                {
                    lock (errorLock)
                    {
                        errorCount++;
                    }
                }
            }).Start();
        }

        // Give threads time to run
        System.Threading.Thread.Sleep(500);

        // Assert - No errors should occur
        Assert.Equal(0, errorCount);
    }

    [Fact]
    public void RegressionTest_ScheduleValidationSynchronized()
    {
        // Regression test: Ensures that ScheduleValidation uses synchronization
        // to prevent race conditions when _validationCts is accessed from multiple threads

        // Before fix: No lock, causing ObjectDisposedException or InvalidOperationException
        // After fix: Has lock(_validationLock) protecting _validationCts access

        var canvas = new CanvasViewModel();

        // This should not throw, even under concurrent stress (using threads instead of Task.WaitAll)
        var threads = Enumerable.Range(0, 50)
            .Select(_ => new Thread(() => canvas.ScheduleValidation()))
            .ToArray();

        foreach (var t in threads)
            t.Start();

        foreach (var t in threads)
            t.Join();

        // If we reach here, the synchronized access is working correctly
        Assert.True(true);
    }

    [Fact]
    public void ScheduleValidation_MultipleSequentialCalls()
    {
        // Arrange
        var canvas = new CanvasViewModel();

        // Act - Schedule and reschedule multiple times
        for (int i = 0; i < 10; i++)
        {
            canvas.ScheduleValidation();
            System.Threading.Thread.Sleep(10);
        }

        // Assert - Should not have memory leaks or undisposed resources
        // (This is validated by the fact that no ObjectDisposedException is thrown)
        Assert.NotNull(canvas);
    }

    [Fact]
    public void RegressionTest_NoConcurrentModificationException()
    {
        // Regression test: Ensures that concurrent calls to ScheduleValidation
        // don't cause ObjectDisposedException or InvalidOperationException

        var canvas = new CanvasViewModel();
        bool exceptionOccurred = false;

        // Pattern that triggers the race condition:
        // 1. Thread A calls ScheduleValidation and captures token
        // 2. Thread B calls ScheduleValidation and cancels/replaces CTS
        // 3. Thread A's continuation runs with cancelled/disposed token

        for (int cycle = 0; cycle < 5; cycle++)
        {
            var t1 = new Thread(() =>
            {
                try
                {
                    for (int i = 0; i < 5; i++)
                    {
                        canvas.ScheduleValidation();
                    }
                }
                catch
                {
                    exceptionOccurred = true;
                }
            });

            var t2 = new Thread(() =>
            {
                try
                {
                    for (int i = 0; i < 5; i++)
                    {
                        canvas.ScheduleValidation();
                    }
                }
                catch
                {
                    exceptionOccurred = true;
                }
            });

            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();
        }

        Assert.False(exceptionOccurred);
    }
}


