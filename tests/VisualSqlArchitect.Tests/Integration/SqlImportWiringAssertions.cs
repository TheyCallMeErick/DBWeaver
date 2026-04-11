using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels.Canvas;
using Xunit;

namespace Integration;

internal static class SqlImportWiringAssertions
{
    public static void AssertGraphWiringIfGraphExists(CanvasViewModel canvas)
    {
        NodeViewModel? result = canvas.Nodes.FirstOrDefault(n => n.Type == NodeType.ResultOutput);
        NodeViewModel? columnSet = canvas.Nodes.FirstOrDefault(n => n.Type == NodeType.ColumnSetBuilder);

        if (result is null || columnSet is null)
            return;

        Assert.Contains(canvas.Connections, c =>
            c.FromPin.Owner == columnSet
            && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
            && c.ToPin?.Owner == result
            && c.ToPin.Name.Equals("columns", StringComparison.OrdinalIgnoreCase));

        NodeViewModel? top = canvas.Nodes.FirstOrDefault(n => n.Type == NodeType.Top);
        if (top is not null)
        {
            Assert.Contains(canvas.Connections, c =>
                c.FromPin.Owner == top
                && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
                && c.ToPin?.Owner == result
                && c.ToPin.Name.Equals("top", StringComparison.OrdinalIgnoreCase));
        }

        NodeViewModel? join = canvas.Nodes.FirstOrDefault(n => n.Type == NodeType.Join);
        if (join is not null)
        {
            Assert.Contains(canvas.Connections, c =>
                c.ToPin?.Owner == join
                && c.ToPin.Name.Equals("left", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(canvas.Connections, c =>
                c.ToPin?.Owner == join
                && c.ToPin.Name.Equals("right", StringComparison.OrdinalIgnoreCase));
        }

        bool hasAnyWhereFlow = canvas.Connections.Any(c =>
            c.ToPin?.Owner == result
            && c.ToPin.Name.Equals("where", StringComparison.OrdinalIgnoreCase));

        NodeViewModel? subquery = canvas.Nodes.FirstOrDefault(n =>
            n.Type is NodeType.SubqueryExists or NodeType.SubqueryIn or NodeType.SubqueryScalar);
        if (subquery is not null)
        {
            bool hasDirectWhere = canvas.Connections.Any(c =>
                c.FromPin.Owner == subquery
                && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
                && c.ToPin?.Owner == result
                && c.ToPin.Name.Equals("where", StringComparison.OrdinalIgnoreCase));
            bool hasWhereOutputCondition = canvas.Connections.Any(c =>
                c.FromPin.Owner == subquery
                && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
                && c.ToPin?.Owner?.Type == NodeType.WhereOutput
                && c.ToPin.Name.Equals("condition", StringComparison.OrdinalIgnoreCase));
            bool hasRoutedCondition = canvas.Connections.Any(c =>
                c.FromPin.Owner == subquery
                && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
                && c.ToPin is not null
                && c.ToPin.Owner != result
                && c.ToPin.Name.Equals("condition", StringComparison.OrdinalIgnoreCase));

            Assert.True(hasAnyWhereFlow && (hasDirectWhere || hasWhereOutputCondition || hasRoutedCondition),
                "Subquery result must feed WHERE flow (directly or through condition chain).");
        }

        NodeViewModel? count = canvas.Nodes.FirstOrDefault(n => n.Type == NodeType.CountStar);
        NodeViewModel? comparison = canvas.Nodes.FirstOrDefault(n =>
            n.Type is NodeType.Equals
                or NodeType.NotEquals
                or NodeType.GreaterThan
                or NodeType.GreaterOrEqual
                or NodeType.LessThan
                or NodeType.LessOrEqual);

        if (count is not null && comparison is not null)
        {
            Assert.Contains(canvas.Connections, c =>
                c.FromPin.Owner == count
                && c.FromPin.Name.Equals("count", StringComparison.OrdinalIgnoreCase)
                && c.ToPin?.Owner == comparison
                && c.ToPin.Name.Equals("left", StringComparison.OrdinalIgnoreCase));

            Assert.Contains(canvas.Connections, c =>
                c.FromPin.Owner == comparison
                && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
                && c.ToPin?.Owner == result
                && c.ToPin.Name.Equals("having", StringComparison.OrdinalIgnoreCase));
        }
    }
}
