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

        NodeViewModel? where = canvas.Nodes.FirstOrDefault(n => n.Type == NodeType.WhereOutput);
        bool hasSubqueryWhereConnection = canvas.Connections.Any(c =>
            c.FromPin.Owner.Type is NodeType.SubqueryExists or NodeType.SubqueryIn or NodeType.SubqueryScalar
            && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
            && c.ToPin?.Owner == result
            && c.ToPin.Name.Equals("where", StringComparison.OrdinalIgnoreCase));
        if (where is not null)
        {
            bool hasIncomingCondition = canvas.Connections.Any(c =>
                c.ToPin?.Owner == where
                && c.ToPin.Name.Equals("condition", StringComparison.OrdinalIgnoreCase));

            if (!hasSubqueryWhereConnection && hasIncomingCondition)
            {
                Assert.Contains(canvas.Connections, c =>
                    c.FromPin.Owner == where
                    && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
                    && c.ToPin?.Owner == result
                    && c.ToPin.Name.Equals("where", StringComparison.OrdinalIgnoreCase));
            }
        }

        NodeViewModel? subquery = canvas.Nodes.FirstOrDefault(n =>
            n.Type is NodeType.SubqueryExists or NodeType.SubqueryIn or NodeType.SubqueryScalar);
        if (subquery is not null)
        {
            bool hasDirectWhere = canvas.Connections.Any(c =>
                c.FromPin.Owner == subquery
                && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
                && c.ToPin?.Owner == result
                && c.ToPin.Name.Equals("where", StringComparison.OrdinalIgnoreCase));
            bool hasWhereCondition = where is not null && canvas.Connections.Any(c =>
                c.FromPin.Owner == subquery
                && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
                && c.ToPin?.Owner == where
                && c.ToPin.Name.Equals("condition", StringComparison.OrdinalIgnoreCase));

            Assert.True(hasDirectWhere || hasWhereCondition,
                "Subquery result must feed either ResultOutput.where or WhereOutput.condition.");
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
