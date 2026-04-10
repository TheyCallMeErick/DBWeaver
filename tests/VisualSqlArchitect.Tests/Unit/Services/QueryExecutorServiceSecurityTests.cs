using System.Reflection;
using DBWeaver.Core;
using DBWeaver.UI.Services;
using Xunit;

namespace DBWeaver.Tests.Unit.Services;

public class QueryExecutorServiceSecurityTests
{
    [Theory]
    [InlineData("INSERT INTO users(id) VALUES (1)")]
    [InlineData("UPDATE users SET name = 'x'")]
    [InlineData("DELETE FROM users")]
    [InlineData("DROP TABLE users")]
    [InlineData("SELECT 1; DELETE FROM users")]
    [InlineData("SELECT * FROM users WHERE id = @id")]
    [InlineData("SELECT * FROM users WHERE id = :id")]
    [InlineData("SELECT * FROM users WHERE id = ?")]
    [InlineData("SELECT * FROM users WHERE id = $1")]
    public void ValidatePreviewQuery_RejectsMutatingOrMultiStatementSql(string sql)
    {
        MethodInfo validate = typeof(QueryExecutorService)
            .GetMethod("ValidatePreviewQuery", BindingFlags.NonPublic | BindingFlags.Static)!;

        var ex = Assert.Throws<TargetInvocationException>(() => validate.Invoke(null, [sql]));
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Theory]
    [InlineData("SELECT * FROM users")]
    [InlineData("WITH q AS (SELECT 1) SELECT * FROM q")]
    [InlineData("EXPLAIN SELECT * FROM users")]
    [InlineData("SELECT @@VERSION")]
    public void ValidatePreviewQuery_AcceptsReadOnlyStatements(string sql)
    {
        MethodInfo validate = typeof(QueryExecutorService)
            .GetMethod("ValidatePreviewQuery", BindingFlags.NonPublic | BindingFlags.Static)!;

        Exception? ex = Record.Exception(() => validate.Invoke(null, [sql]));
        Assert.Null(ex);
    }

    [Fact]
    public void WrapWithPreviewLimit_ClampsMaxRows()
    {
        MethodInfo wrap = typeof(QueryExecutorService)
            .GetMethod("WrapWithPreviewLimit", BindingFlags.NonPublic | BindingFlags.Static)!;

        string sqlLow = (string)wrap.Invoke(null, ["SELECT * FROM users", DatabaseProvider.Postgres, 0])!;
        string sqlHigh = (string)wrap.Invoke(null, ["SELECT * FROM users", DatabaseProvider.Postgres, 50000])!;

        Assert.Contains("LIMIT 1", sqlLow);
        Assert.Contains("LIMIT 10000", sqlHigh);
    }

    [Fact]
    public void QueryExecutorService_ExposesConfigurableCommandTimeout()
    {
        var executor = new QueryExecutorService();
        Assert.Equal(300, executor.CommandTimeoutSeconds);

        executor.CommandTimeoutSeconds = 42;
        Assert.Equal(42, executor.CommandTimeoutSeconds);
    }
}
