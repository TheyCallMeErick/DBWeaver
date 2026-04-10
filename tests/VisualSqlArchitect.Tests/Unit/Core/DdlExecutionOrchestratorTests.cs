namespace DBWeaver.Tests.Unit.Core;

public class DdlExecutionOrchestratorTests
{
    private static ConnectionConfig SqliteMemoryConfig() =>
        new(
            Provider: DatabaseProvider.SQLite,
            Host: "",
            Port: 0,
            Database: ":memory:",
            Username: "",
            Password: "",
            TimeoutSeconds: 30
        );

    [Fact]
    public async Task ExecuteDdlAsync_SingleStatement_Succeeds()
    {
        await using var orchestrator = new SqliteOrchestrator(SqliteMemoryConfig());

        DdlExecutionResult result = await orchestrator.ExecuteDdlAsync("CREATE TABLE users(id INTEGER);");

        Assert.True(result.Success);
        DdlStatementExecutionResult statement = Assert.Single(result.Statements);
        Assert.True(statement.Success);
        Assert.Equal(1, statement.StatementIndex);
    }

    [Fact]
    public async Task ExecuteDdlAsync_StopOnErrorFalse_ContinuesAfterFailure()
    {
        await using var orchestrator = new SqliteOrchestrator(SqliteMemoryConfig());

        string sql = "CREATE TABLE a(id INTEGER); BAD SYNTAX; CREATE TABLE b(id INTEGER);";
        DdlExecutionResult result = await orchestrator.ExecuteDdlAsync(sql, stopOnError: false);

        Assert.False(result.Success);
        Assert.Equal(3, result.Statements.Count);
        Assert.True(result.Statements[0].Success);
        Assert.False(result.Statements[1].Success);
        Assert.True(result.Statements[2].Success);
    }

    [Fact]
    public async Task ExecuteDdlAsync_StopOnErrorTrue_StopsAtFirstFailure()
    {
        await using var orchestrator = new SqliteOrchestrator(SqliteMemoryConfig());

        string sql = "CREATE TABLE c(id INTEGER); BAD SYNTAX; CREATE TABLE d(id INTEGER);";
        DdlExecutionResult result = await orchestrator.ExecuteDdlAsync(sql, stopOnError: true);

        Assert.False(result.Success);
        Assert.Equal(2, result.Statements.Count);
        Assert.True(result.Statements[0].Success);
        Assert.False(result.Statements[1].Success);
    }
}
