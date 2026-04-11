using System.Data;
using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlEditorMutationExecutionOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenGuardIsDisabled_ExecutesDirectly()
    {
        int executeCalls = 0;
        var sut = new SqlEditorMutationExecutionOrchestrator(
            (sql, _, _, _) =>
            {
                executeCalls++;
                return Task.FromResult(SuccessResult(sql ?? string.Empty));
            },
            _ => MutationGuardResult.Safe(),
            (_, _, _, _, _) => Task.FromResult(SqlMutationDiffPreview.Unavailable("none")));

        SqlEditorMutationExecutionOutcome outcome = await sut.ExecuteAsync(
            "SELECT 1",
            config: null,
            maxRows: 10,
            enforceMutationGuard: false);

        Assert.False(outcome.RequiresConfirmation);
        Assert.True(outcome.Result.Success);
        Assert.Equal(1, executeCalls);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGuardAllows_ExecutesStatement()
    {
        int executeCalls = 0;
        var sut = new SqlEditorMutationExecutionOrchestrator(
            (sql, _, _, _) =>
            {
                executeCalls++;
                return Task.FromResult(SuccessResult(sql ?? string.Empty));
            },
            _ => MutationGuardResult.Safe(),
            (_, _, _, _, _) => Task.FromResult(SqlMutationDiffPreview.Unavailable("none")));

        SqlEditorMutationExecutionOutcome outcome = await sut.ExecuteAsync(
            "UPDATE orders SET status='x' WHERE id=1",
            config: null,
            maxRows: 10,
            enforceMutationGuard: true);

        Assert.False(outcome.RequiresConfirmation);
        Assert.True(outcome.Result.Success);
        Assert.Equal(1, executeCalls);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGuardRequiresConfirmation_ReturnsPendingState()
    {
        var calls = new List<string>();
        MutationGuardResult guard = new()
        {
            IsSafe = false,
            RequiresConfirmation = true,
            Issues = [new MutationGuardIssue(MutationGuardSeverity.Critical, "NO_WHERE", "danger", "fix")],
            CountQuery = "SELECT COUNT(*) FROM orders",
            SupportsDiff = true,
        };

        var sut = new SqlEditorMutationExecutionOrchestrator(
            (sql, _, _, _) =>
            {
                calls.Add(sql ?? string.Empty);
                return Task.FromResult(ScalarResult(sql ?? string.Empty, 5));
            },
            _ => guard,
            (_, _, _, estimatedRows, _) => Task.FromResult(new SqlMutationDiffPreview
            {
                Available = true,
                Message = $"estimated={estimatedRows}",
            }));

        SqlEditorMutationExecutionOutcome outcome = await sut.ExecuteAsync(
            "DELETE FROM orders;",
            config: null,
            maxRows: 10,
            enforceMutationGuard: true);

        Assert.True(outcome.RequiresConfirmation);
        Assert.False(outcome.Result.Success);
        AssertLocalized(
            outcome.Result.ErrorMessage,
            "Confirmacao de mutacao necessaria.",
            "Mutation confirmation required.");
        Assert.NotNull(outcome.ConfirmationState);
        Assert.Equal("DELETE FROM orders;", outcome.ConfirmationState!.StatementSql);
        Assert.Equal(5, outcome.ConfirmationState.EstimatedRows);
        Assert.Contains("estimated=5", outcome.ConfirmationState.DiffPreview.Message);
        Assert.Single(calls);
        Assert.Equal("SELECT COUNT(*) FROM orders", calls[0]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCacheKeyRepeats_UsesCachedEstimate()
    {
        int countQueryCalls = 0;
        MutationGuardResult guard = new()
        {
            IsSafe = false,
            RequiresConfirmation = true,
            Issues = [new MutationGuardIssue(MutationGuardSeverity.Critical, "NO_WHERE", "danger", "fix")],
            CountQuery = "SELECT COUNT(*) FROM orders",
            SupportsDiff = true,
        };

        var sut = new SqlEditorMutationExecutionOrchestrator(
            (sql, _, _, _) =>
            {
                if (string.Equals(sql, "SELECT COUNT(*) FROM orders", StringComparison.Ordinal))
                    countQueryCalls++;
                return Task.FromResult(ScalarResult(sql ?? string.Empty, 5));
            },
            _ => guard,
            (_, _, _, _, _) => Task.FromResult(SqlMutationDiffPreview.Unavailable("none")));

        _ = await sut.ExecuteAsync("DELETE FROM orders;", null, 10, true, "tab-1::delete");
        _ = await sut.ExecuteAsync("DELETE FROM orders;", null, 10, true, "tab-1::delete");

        Assert.Equal(1, countQueryCalls);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCacheExpires_ReexecutesEstimate()
    {
        int countQueryCalls = 0;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        MutationGuardResult guard = new()
        {
            IsSafe = false,
            RequiresConfirmation = true,
            Issues = [new MutationGuardIssue(MutationGuardSeverity.Critical, "NO_WHERE", "danger", "fix")],
            CountQuery = "SELECT COUNT(*) FROM orders",
            SupportsDiff = true,
        };

        var sut = new SqlEditorMutationExecutionOrchestrator(
            (sql, _, _, _) =>
            {
                if (string.Equals(sql, "SELECT COUNT(*) FROM orders", StringComparison.Ordinal))
                    countQueryCalls++;
                return Task.FromResult(ScalarResult(sql ?? string.Empty, 5));
            },
            _ => guard,
            (_, _, _, _, _) => Task.FromResult(SqlMutationDiffPreview.Unavailable("none")),
            nowProvider: () => now,
            estimateCacheTtl: TimeSpan.FromSeconds(2));

        _ = await sut.ExecuteAsync("DELETE FROM orders;", null, 10, true, "tab-1::delete");
        now = now.AddSeconds(3);
        _ = await sut.ExecuteAsync("DELETE FROM orders;", null, 10, true, "tab-1::delete");

        Assert.Equal(2, countQueryCalls);
    }

    [Fact]
    public async Task ConfirmAsync_ExecutesPendingStatement()
    {
        int executeCalls = 0;
        var sut = new SqlEditorMutationExecutionOrchestrator(
            (sql, _, _, _) =>
            {
                executeCalls++;
                return Task.FromResult(SuccessResult(sql ?? string.Empty));
            },
            _ => MutationGuardResult.Safe(),
            (_, _, _, _, _) => Task.FromResult(SqlMutationDiffPreview.Unavailable("none")));

        SqlEditorResultSet result = await sut.ConfirmAsync("UPDATE orders SET status='x';", null, 10);

        Assert.True(result.Success);
        Assert.Equal(1, executeCalls);
    }

    private static SqlEditorResultSet SuccessResult(string sql)
    {
        return new SqlEditorResultSet
        {
            StatementSql = sql,
            Success = true,
            RowsAffected = 1,
            ExecutionTime = TimeSpan.FromMilliseconds(1),
            ExecutedAt = DateTimeOffset.UtcNow,
        };
    }

    private static SqlEditorResultSet ScalarResult(string sql, long value)
    {
        var table = new DataTable();
        table.Columns.Add("count", typeof(long));
        table.Rows.Add(value);
        return new SqlEditorResultSet
        {
            StatementSql = sql,
            Success = true,
            Data = table,
            RowsAffected = 1,
            ExecutionTime = TimeSpan.FromMilliseconds(1),
            ExecutedAt = DateTimeOffset.UtcNow,
        };
    }

    private static void AssertLocalized(string? actual, params string[] expectedValues)
    {
        Assert.NotNull(actual);
        Assert.Contains(actual!, expectedValues);
    }
}
