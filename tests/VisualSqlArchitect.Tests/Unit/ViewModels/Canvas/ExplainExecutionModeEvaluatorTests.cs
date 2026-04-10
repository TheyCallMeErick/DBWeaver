using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.Core;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainExecutionModeEvaluatorTests
{
    [Fact]
    public void IsSimulated_ReturnsTrue_WhenConnectionIsNull()
    {
        var sut = new ExplainExecutionModeEvaluator();
        Assert.True(sut.IsSimulated(DatabaseProvider.SQLite, null));
    }

    [Fact]
    public void IsSimulated_ReturnsFalse_WhenSqliteConnectionExists()
    {
        var config = new ConnectionConfig(
            Provider: DatabaseProvider.SQLite,
            Host: string.Empty,
            Port: 0,
            Database: "demo.db",
            Username: string.Empty,
            Password: string.Empty
        );

        var sut = new ExplainExecutionModeEvaluator();
        Assert.False(sut.IsSimulated(DatabaseProvider.SQLite, config));
    }

    [Fact]
    public void IsSimulated_ReturnsFalse_WhenPostgresConnectionExists()
    {
        var config = new ConnectionConfig(
            Provider: DatabaseProvider.Postgres,
            Host: "localhost",
            Port: 5432,
            Database: "demo",
            Username: "user",
            Password: "secret"
        );

        var sut = new ExplainExecutionModeEvaluator();
        Assert.False(sut.IsSimulated(DatabaseProvider.Postgres, config));
    }

    [Fact]
    public void IsSimulated_ReturnsFalse_WhenMySqlConnectionExists()
    {
        var config = new ConnectionConfig(
            Provider: DatabaseProvider.MySql,
            Host: "localhost",
            Port: 3306,
            Database: "demo",
            Username: "user",
            Password: "secret"
        );

        var sut = new ExplainExecutionModeEvaluator();
        Assert.False(sut.IsSimulated(DatabaseProvider.MySql, config));
    }

    [Fact]
    public void IsSimulated_ReturnsFalse_WhenSqlServerConnectionExists()
    {
        var config = new ConnectionConfig(
            Provider: DatabaseProvider.SqlServer,
            Host: "localhost",
            Port: 1433,
            Database: "demo",
            Username: "user",
            Password: "secret"
        );

        var sut = new ExplainExecutionModeEvaluator();
        Assert.False(sut.IsSimulated(DatabaseProvider.SqlServer, config));
    }
}


