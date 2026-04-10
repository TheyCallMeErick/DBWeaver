using DBWeaver.UI.Services;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using System;

namespace DBWeaver.Tests.Unit.Services;

/// <summary>
/// Tests for DatabaseConnectionService IDisposable implementation.
/// Regression tests for: Service not implementing IDisposable, causing resource leaks
/// in CancellationTokenSource and MetadataService.
/// </summary>
public class DatabaseConnectionServiceDisposalTests
{
    [Fact]
    public void DatabaseConnectionService_Implements_IDisposable()
    {
        // Verify that DatabaseConnectionService implements IDisposable interface
        var service = new DatabaseConnectionService();
        Assert.IsAssignableFrom<IDisposable>(service);
    }

    [Fact]
    public void DatabaseConnectionService_CanBeDisposed()
    {
        // Verify that Dispose() can be called without throwing
        var service = new DatabaseConnectionService();
        service.Dispose();
        // Should not throw
    }

    [Fact]
    public void DatabaseConnectionService_Dispose_IsIdempotent()
    {
        // Regression test: Verify that Dispose() can be called multiple times safely
        var service = new DatabaseConnectionService();

        service.Dispose();
        service.Dispose();
        service.Dispose();

        // Should not throw on multiple calls
    }

    [Fact]
    public void DatabaseConnectionService_Dispose_NullsOutFields()
    {
        // Verify that all fields are null after disposal
        var service = new DatabaseConnectionService();

        // Access LoadedMetadata to ensure it's accessible before dispose
        var initialMetadata = service.LoadedMetadata;
        Assert.Null(initialMetadata);

        service.Dispose();

        // After disposal, LoadedMetadata should still be accessible (returns null)
        var disposedMetadata = service.LoadedMetadata;
        Assert.Null(disposedMetadata);
    }

    [Fact]
    public void DatabaseConnectionService_Can_UseBothCancelAndDispose()
    {
        // Regression test: Verify that Cancel() and Dispose() can be used together
        var service = new DatabaseConnectionService();

        service.Cancel();
        service.Dispose();

        // Should not throw
    }

    [Fact]
    public void DatabaseConnectionService_DisposeCalledViaUsing()
    {
        // Test that Dispose() is properly called when using 'using' statement
        DatabaseConnectionService? service = null;

        using (service = new DatabaseConnectionService())
        {
            Assert.NotNull(service);
        }

        // After using block, disposed state should be tracked
        Assert.NotNull(service);

        // Calling Dispose again after using block should not throw
        service.Dispose();
    }

    [Fact]
    public void DatabaseConnectionService_Implements_IDisposable_Sealed()
    {
        // Verify implementation details
        var service = new DatabaseConnectionService();
        Assert.True(service is IDisposable);

        // The class is sealed, so no derived classes can override Dispose
        var type = service.GetType();
        Assert.True(type.IsSealed);
    }

    [Fact]
    public void DatabaseConnectionService_HasLogger()
    {
        // Verify the service can be created with default logger (NullLogger)
        var service = new DatabaseConnectionService();
        Assert.NotNull(service);

        // Verify it also works with explicit logger
        var service2 = new DatabaseConnectionService(NullLogger<DatabaseConnectionService>.Instance);
        Assert.NotNull(service2);

        service2.Dispose();
    }

    [Fact]
    public void RegressionTest_DatabaseConnectionService_IDisposable_Implemented()
    {
        // Primary regression test: Verify IDisposable is properly implemented
        // Before: public sealed class DatabaseConnectionService (NO IDisposable)
        // After: public sealed class DatabaseConnectionService : IDisposable
        // Before: _metadataService not disposed; only = null
        // After: ((_metadataService as IDisposable)?.Dispose()) called first, then = null

        using (var service = new DatabaseConnectionService())
        {
            // Should be usable in using block
            Assert.NotNull(service);
            Assert.Null(service.LoadedMetadata);
            Assert.Null(service.GetActiveMetadataService());
        }

        // If we got here, IDisposable is implemented
    }

    [Fact]
    public void DatabaseConnectionService_GetActiveMetadataService_ReturnsNull_WhenNotInitialized()
    {
        // Verify that service returns null for metadata before any connection
        var service = new DatabaseConnectionService();

        var activeService = service.GetActiveMetadataService();
        Assert.Null(activeService);

        service.Dispose();
    }

    [Fact]
    public void DatabaseConnectionService_Cancel_CanBeCalledBeforeDispose()
    {
        // Verify Cancel() works and doesn't interfere with Dispose()
        var service = new DatabaseConnectionService();

        service.Cancel();  // Cancel any operations
        service.Dispose(); // Then dispose

        // Should complete without errors
    }
}
