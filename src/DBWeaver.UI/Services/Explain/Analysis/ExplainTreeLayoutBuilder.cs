using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public sealed class ExplainTreeVisualNode
{
    public required ExplainStep Step { get; init; }
    public required string Operation { get; init; }
    public required string CostText { get; init; }
    public required string AlertLabel { get; init; }
    public required bool HasAlert { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public double Width { get; init; } = 190;
    public double Height { get; init; } = 62;
}

public sealed class ExplainTreeVisualEdge
{
    public required double X1 { get; init; }
    public required double Y1 { get; init; }
    public required double X2 { get; init; }
    public required double Y2 { get; init; }
}

public sealed record ExplainTreeLayoutResult(
    IReadOnlyList<ExplainTreeVisualNode> Nodes,
    IReadOnlyList<ExplainTreeVisualEdge> Edges,
    double CanvasWidth,
    double CanvasHeight
);

public interface IExplainTreeLayoutBuilder
{
    ExplainTreeLayoutResult Build(IReadOnlyList<ExplainStep> steps);
}

public sealed class ExplainTreeLayoutBuilder : IExplainTreeLayoutBuilder
{
    public ExplainTreeLayoutResult Build(IReadOnlyList<ExplainStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        if (steps.Count == 0)
            return new ExplainTreeLayoutResult([], [], 0, 0);

        const double startX = 16;
        const double startY = 12;
        const double columnSpacing = 230;
        const double rowSpacing = 84;
        const double nodeWidth = 190;
        const double nodeHeight = 62;

        var nodes = new List<ExplainTreeVisualNode>(steps.Count);
        var edges = new List<ExplainTreeVisualEdge>(Math.Max(0, steps.Count - 1));

        var parentStack = new Stack<(int Indent, int Index)>();
        var parentByIndex = new int[steps.Count];
        Array.Fill(parentByIndex, -1);

        for (int i = 0; i < steps.Count; i++)
        {
            ExplainStep step = steps[i];
            while (parentStack.Count > 0 && parentStack.Peek().Indent >= step.IndentLevel)
                parentStack.Pop();

            if (parentStack.Count > 0)
                parentByIndex[i] = parentStack.Peek().Index;

            parentStack.Push((step.IndentLevel, i));
        }

        for (int i = 0; i < steps.Count; i++)
        {
            ExplainStep step = steps[i];
            double x = startX + Math.Max(0, step.IndentLevel) * columnSpacing;
            double y = startY + i * rowSpacing;

            nodes.Add(
                new ExplainTreeVisualNode
                {
                    Step = step,
                    Operation = step.Operation,
                    CostText = step.CostText,
                    AlertLabel = step.AlertLabel,
                    HasAlert = step.HasAlert,
                    X = x,
                    Y = y,
                    Width = nodeWidth,
                    Height = nodeHeight,
                }
            );
        }

        for (int childIndex = 0; childIndex < parentByIndex.Length; childIndex++)
        {
            int parentIndex = parentByIndex[childIndex];
            if (parentIndex < 0)
                continue;

            ExplainTreeVisualNode parent = nodes[parentIndex];
            ExplainTreeVisualNode child = nodes[childIndex];
            edges.Add(
                new ExplainTreeVisualEdge
                {
                    X1 = parent.X + parent.Width,
                    Y1 = parent.Y + parent.Height / 2,
                    X2 = child.X,
                    Y2 = child.Y + child.Height / 2,
                }
            );
        }

        double maxRight = nodes.Max(n => n.X + n.Width) + 16;
        double maxBottom = nodes.Max(n => n.Y + n.Height) + 16;
        return new ExplainTreeLayoutResult(nodes, edges, maxRight, maxBottom);
    }
}



