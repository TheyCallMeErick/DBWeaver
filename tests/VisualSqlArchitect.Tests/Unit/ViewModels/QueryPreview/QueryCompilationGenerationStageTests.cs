using DBWeaver.Nodes;
using DBWeaver.QueryEngine;
using DBWeaver.UI.Services.QueryPreview;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;

public class QueryCompilationGenerationStageTests
{
    [Fact]
    public void Execute_WhenGeneratorSucceeds_ReturnsInlinedSql()
    {
        var generator = new FakeGenerator(
            () => new GeneratedQuery("SELECT * FROM t", new Dictionary<string, object?>(), "debug"));

        var stage = new QueryCompilationGenerationStage(
            generator,
            (sql, bindings) => sql + " --inlined",
            ex => ["mapped: " + ex.Message],
            (fromTable, joins) => "fallback");

        var errors = new List<string>();
        QueryCompilationGenerationStageResult result = stage.Execute(
            new QueryCompilationGenerationStageInput(
                "public.orders",
                new NodeGraph(),
                [],
                null,
                errors));

        Assert.Equal("SELECT * FROM t --inlined", result.Sql);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Execute_WhenGeneratorFails_ReturnsFallbackAndMappedErrors()
    {
        var generator = new FakeGenerator(() => throw new InvalidOperationException("boom"));

        var stage = new QueryCompilationGenerationStage(
            generator,
            (sql, bindings) => sql,
            ex => ["mapped: " + ex.Message],
            (fromTable, joins) => "fallback-sql");

        var errors = new List<string>();
        QueryCompilationGenerationStageResult result = stage.Execute(
            new QueryCompilationGenerationStageInput(
                "public.orders",
                new NodeGraph(),
                [new JoinDefinition("t", "a", "b")],
                null,
                errors));

        Assert.Equal("fallback-sql", result.Sql);
        Assert.Contains("mapped: boom", result.Errors);
    }

    [Fact]
    public void Execute_WhenGeneratorSucceeds_PreservesExistingErrorsAndSkipsFallback()
    {
        int fallbackCalls = 0;
        var generator = new FakeGenerator(
            () => new GeneratedQuery("SELECT 42", new Dictionary<string, object?>(), "debug"));

        var stage = new QueryCompilationGenerationStage(
            generator,
            (sql, bindings) => sql,
            ex => ["mapped: " + ex.Message],
            (fromTable, joins) =>
            {
                fallbackCalls++;
                return "fallback";
            });

        var errors = new List<string> { "seed-error" };
        QueryCompilationGenerationStageResult result = stage.Execute(
            new QueryCompilationGenerationStageInput(
                "public.orders",
                new NodeGraph(),
                [],
                null,
                errors));

        Assert.Equal("SELECT 42", result.Sql);
        Assert.Equal(0, fallbackCalls);
        Assert.Single(result.Errors);
        Assert.Contains("seed-error", result.Errors);
    }

    [Fact]
    public void Execute_WhenGeneratorFails_AccumulatesAllMappedErrors()
    {
        var generator = new FakeGenerator(() => throw new InvalidOperationException("boom"));

        var stage = new QueryCompilationGenerationStage(
            generator,
            (sql, bindings) => sql,
            ex => ["mapped-a", "mapped-b"],
            (fromTable, joins) => "fallback-sql");

        var errors = new List<string> { "seed-error" };
        QueryCompilationGenerationStageResult result = stage.Execute(
            new QueryCompilationGenerationStageInput(
                "public.orders",
                new NodeGraph(),
                [],
                null,
                errors));

        Assert.Equal("fallback-sql", result.Sql);
        Assert.Contains("seed-error", result.Errors);
        Assert.Contains("mapped-a", result.Errors);
        Assert.Contains("mapped-b", result.Errors);
    }

    private sealed class FakeGenerator(Func<GeneratedQuery> behavior) : IQueryCompilationSqlGenerator
    {
        private readonly Func<GeneratedQuery> _behavior = behavior;

        public GeneratedQuery Generate(
            string fromTable,
            NodeGraph graph,
            IReadOnlyList<JoinDefinition> joins,
            SetOperationDefinition? setOperation) => _behavior();
    }
}
