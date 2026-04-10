using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public interface IExplainNodeToStepMapper
{
    IReadOnlyList<ExplainStep> Map(IReadOnlyList<ExplainNode> nodes);
}

public sealed class ExplainNodeToStepMapper : IExplainNodeToStepMapper
{
    public IReadOnlyList<ExplainStep> Map(IReadOnlyList<ExplainNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var steps = new List<ExplainStep>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            ExplainNode node = nodes[i];
            steps.Add(
                new ExplainStep
                {
                    NodeId = node.NodeId,
                    ParentNodeId = node.ParentNodeId,
                    StepNumber = i + 1,
                    Operation = node.NodeType,
                    Detail = node.Detail,
                    RelationName = node.RelationName,
                    IndexName = node.IndexName,
                    Predicate = node.Predicate,
                    StartupCost = node.StartupCost,
                    EstimatedCost = node.EstimatedCost,
                    EstimatedRows = node.EstimatedRows,
                    ActualStartupTimeMs = node.ActualStartupTimeMs,
                    ActualTotalTimeMs = node.ActualTotalTimeMs,
                    ActualLoops = node.ActualLoops,
                    ActualRows = node.ActualRows,
                    IndentLevel = node.IndentLevel,
                    IsExpensive = node.IsExpensive,
                    AlertLabel = node.AlertLabel,
                }
            );
        }

        return steps;
    }
}



