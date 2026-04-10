using System.Reflection;
using DBWeaver.Core;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public sealed class ExplainPlanViewModelProductionSafetyTests
{
    [Fact]
    public async Task RunExplainAsync_BlocksAnalyze_WhenSqlLooksMutating()
    {
        var canvas = new CanvasViewModel();
        SetLiveSqlRawSql(canvas.LiveSql, "DELETE FROM users WHERE id = 10");
        var executor = new TrackingExplainExecutor();
        var sut = new ExplainPlanViewModel(
            canvas,
            explainExecutor: executor,
            sqlSafetyEvaluator: new ForcedMutatingSafetyEvaluator(alwaysMutating: true));

        sut.IncludeAnalyze = true;

        await sut.RunExplainAsync();

        Assert.Equal(0, executor.CallCount);
        Assert.True(sut.HasError);
        Assert.Contains("Analyze executes the query", sut.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Close_CancelsInFlightRun_WithoutSurfacingError()
    {
        var canvas = new CanvasViewModel();
        SetLiveSqlRawSql(canvas.LiveSql, "SELECT 1");
        var executor = new CancellableExplainExecutor();
        var sut = new ExplainPlanViewModel(
            canvas,
            explainExecutor: executor,
            sqlSafetyEvaluator: new ForcedMutatingSafetyEvaluator(alwaysMutating: false));

        Task run = sut.RunExplainAsync();
        await Task.Delay(50);
        sut.Close();
        await run;

        Assert.True(executor.WasCancelled);
        Assert.False(sut.HasError);
    }

    private static void SetLiveSqlRawSql(LiveSqlBarViewModel liveSql, string sql)
    {
        FieldInfo rawSqlField = typeof(LiveSqlBarViewModel)
            .GetField("_rawSql", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not locate LiveSqlBarViewModel._rawSql field.");
        rawSqlField.SetValue(liveSql, sql);
    }

    private sealed class TrackingExplainExecutor : IExplainExecutor
    {
        public int CallCount { get; private set; }

        public Task<ExplainResult> RunAsync(
            string sql,
            DatabaseProvider provider,
            ConnectionConfig? connectionConfig,
            ExplainOptions options,
            CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(new ExplainResult([], null, null, "{}", IsSimulated: true));
        }
    }

    private sealed class CancellableExplainExecutor : IExplainExecutor
    {
        public bool WasCancelled { get; private set; }

        public async Task<ExplainResult> RunAsync(
            string sql,
            DatabaseProvider provider,
            ConnectionConfig? connectionConfig,
            ExplainOptions options,
            CancellationToken ct = default)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }

            return new ExplainResult([], null, null, "{}", IsSimulated: true);
        }
    }

    private sealed class ForcedMutatingSafetyEvaluator(bool alwaysMutating) : IExplainSqlSafetyEvaluator
    {
        public bool LooksMutating(string sql) => alwaysMutating;
    }
}

