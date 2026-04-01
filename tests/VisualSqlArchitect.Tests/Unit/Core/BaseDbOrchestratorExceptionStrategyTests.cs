using System.Data.Common;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.Providers.Dialects;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Core;

public class BaseDbOrchestratorExceptionStrategyTests
{
    [Fact]
    public async Task TestConnectionAsync_WhenOpenConnectionFails_ReturnsFailureResult()
    {
        var sut = new ThrowingOrchestrator();

        ConnectionTestResult result = await sut.TestConnectionAsync();

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task ExecutePreviewAsync_WhenOpenConnectionFails_ReturnsFailureResult()
    {
        var sut = new ThrowingOrchestrator();

        PreviewResult result = await sut.ExecutePreviewAsync("SELECT 1", 10);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Null(result.Data);
    }

    private sealed class ThrowingOrchestrator()
        : BaseDbOrchestrator(new ConnectionConfig(DatabaseProvider.SQLite, "localhost", 0, "x.db", "", ""))
    {
        public override DatabaseProvider Provider => DatabaseProvider.SQLite;

        protected override Task<DbConnection> OpenConnectionAsync(CancellationToken ct) =>
            throw new InvalidOperationException("Simulated open failure");

        protected override ISqlDialect GetDialect() => new SqliteDialect();

        protected override IMetadataQueryProvider GetMetadataQueryProvider() => new SqliteMetadataQueries();
    }
}
