using DBWeaver.Core;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.QueryPreview;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;

public class QueryCompilationInputStageTests
{
    [Fact]
    public void Execute_WhenNoSourcesExist_ShortCircuitsWithGuidance()
    {
        var stage = CreateStage();
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        QueryCompilationInputStageResult result = stage.Execute(
            new QueryCompilationPipelineContext(canvas, DatabaseProvider.Postgres));

        Assert.True(result.ShouldShortCircuit);
        Assert.Null(result.Snapshot);
        Assert.Contains("Add a table, CTE source, or Subquery node", result.ShortCircuitSql, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Execute_WhenResultNodeMissing_ShortCircuitsWithResultGuidance()
    {
        var stage = CreateStage();
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();
        canvas.Nodes.Add(QueryPreviewTestNodeFactory.NumberTable("public.orders", "id"));

        QueryCompilationInputStageResult result = stage.Execute(
            new QueryCompilationPipelineContext(canvas, DatabaseProvider.Postgres));

        Assert.True(result.ShouldShortCircuit);
        Assert.Null(result.Snapshot);
        Assert.Contains("Add a Result Output node", result.ShortCircuitSql, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Execute_WhenMultipleTopLevelOutputsExist_ShortCircuitsWithAmbiguityGuidance()
    {
        var stage = CreateStage();
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel table = QueryPreviewTestNodeFactory.NumberTable("public.orders", "id");
        NodeViewModel outputA = QueryPreviewTestNodeFactory.Node(NodeType.ResultOutput);
        NodeViewModel outputB = QueryPreviewTestNodeFactory.Node(NodeType.ResultOutput);

        canvas.Nodes.Add(table);
        canvas.Nodes.Add(outputA);
        canvas.Nodes.Add(outputB);
        QueryPreviewTestNodeFactory.Connect(canvas, table, "id", outputA, "column");
        QueryPreviewTestNodeFactory.Connect(canvas, table, "id", outputB, "column");

        QueryCompilationInputStageResult result = stage.Execute(
            new QueryCompilationPipelineContext(canvas, DatabaseProvider.Postgres));

        Assert.True(result.ShouldShortCircuit);
        Assert.Null(result.Snapshot);
        Assert.Contains("Multiple top-level Result Output nodes", result.ShortCircuitSql, StringComparison.OrdinalIgnoreCase);
    }

    private static QueryCompilationInputStage CreateStage() =>
        new(
            ctes => ctes.ToDictionary(
                node => node.Id,
                node => node.Parameters.TryGetValue("name", out string? value)
                    ? value ?? string.Empty
                    : string.Empty,
                StringComparer.OrdinalIgnoreCase),
            (tables, cteSources, subqueries, cteMap) => ("public.orders", null),
            pin => pin.Name == "*",
            name => string.Equals(name, "columns", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "column", StringComparison.OrdinalIgnoreCase));

}
