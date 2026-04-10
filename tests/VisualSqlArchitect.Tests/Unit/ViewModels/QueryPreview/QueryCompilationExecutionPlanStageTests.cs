using DBWeaver.Nodes;
using DBWeaver.QueryEngine;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.QueryPreview;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;

public class QueryCompilationExecutionPlanStageTests
{
    [Fact]
    public void Execute_BuildsPlanAndAggregatesWarnings()
    {
        var expectedGraph = new NodeGraph();
        var expectedJoin = new JoinDefinition("public.customers", "orders.customer_id", "customers.id");
        var expectedSetOp = new SetOperationDefinition("UNION", "SELECT 1");

        int cteValidationCalls = 0;
        int subqueryValidationCalls = 0;

        var stage = new QueryCompilationExecutionPlanStage(
            (resultNode, ctes, cteMap, includeCtes) => expectedGraph,
            tableNodes => (new List<JoinDefinition> { expectedJoin }, new List<string> { "join-warning" }),
            resultNode => (expectedSetOp, "setop-warning"),
            (graph, cteMap, errors) =>
            {
                cteValidationCalls++;
                errors.Add("cte-warning");
            },
            errors =>
            {
                subqueryValidationCalls++;
                errors.Add("subquery-warning");
            });

        NodeViewModel resultNode = QueryPreviewTestNodeFactory.Node(NodeType.ResultOutput);
        var tableNodes = new List<NodeViewModel>();
        var cteDefinitions = new List<NodeViewModel>();
        var cteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        QueryCompilationExecutionPlanStageResult result = stage.Execute(
            new QueryCompilationExecutionPlanStageInput(
                tableNodes,
                cteDefinitions,
                cteMap,
                resultNode,
                errors));

        Assert.Same(expectedGraph, result.Graph);
        Assert.Single(result.Joins);
        Assert.Equal(expectedJoin, result.Joins[0]);
        Assert.Equal(expectedSetOp, result.SetOperation);
        Assert.Contains("join-warning", result.Errors);
        Assert.Contains("setop-warning", result.Errors);
        Assert.Contains("cte-warning", result.Errors);
        Assert.Contains("subquery-warning", result.Errors);
        Assert.Equal(1, cteValidationCalls);
        Assert.Equal(1, subqueryValidationCalls);
    }

    [Fact]
    public void Execute_WhenSetOperationWarningIsBlank_DoesNotAddWarningError()
    {
        var expectedGraph = new NodeGraph();
        var expectedJoin = new JoinDefinition("public.orders", "orders.customer_id", "customers.id");
        var stage = new QueryCompilationExecutionPlanStage(
            (resultNode, ctes, cteMap, includeCtes) => expectedGraph,
            tableNodes => (new List<JoinDefinition> { expectedJoin }, new List<string>()),
            resultNode => (null, "   "),
            (graph, cteMap, errors) => { },
            errors => { });

        var errors = new List<string> { "seed-error" };
        QueryCompilationExecutionPlanStageResult result = stage.Execute(
            new QueryCompilationExecutionPlanStageInput(
                [],
                [],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                QueryPreviewTestNodeFactory.Node(NodeType.ResultOutput),
                errors));

        Assert.Same(errors, result.Errors);
        Assert.Equal(1, result.Errors.Count(e => e == "seed-error"));
        Assert.DoesNotContain(result.Errors, e => e.Contains("setop", StringComparison.OrdinalIgnoreCase));
        Assert.Null(result.SetOperation);
        Assert.Single(result.Joins);
    }

}

