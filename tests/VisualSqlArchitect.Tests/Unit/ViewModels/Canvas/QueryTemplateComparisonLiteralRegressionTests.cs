using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class QueryTemplateComparisonLiteralRegressionTests
{
    [Fact]
    public void Templates_WithComparisonNodes_MustProvideRequiredLiteralOrWire()
    {
        foreach (QueryTemplate template in QueryTemplateLibrary.All)
        {
            var canvas = new CanvasViewModel();
            canvas.LoadTemplate(template);

            foreach (NodeViewModel node in canvas.Nodes.Where(IsComparisonNeedingLiteral))
            {
                foreach (string pin in RequiredLiteralPins(node.Type))
                {
                    bool hasWire = canvas.Connections.Any(c =>
                        c.ToPin is not null
                        && ReferenceEquals(c.ToPin.Owner, node)
                        && string.Equals(c.ToPin.Name, pin, StringComparison.Ordinal)
                    );

                    bool hasLiteral = node.PinLiterals.TryGetValue(pin, out string? value)
                        && !string.IsNullOrWhiteSpace(value);

                    Assert.True(
                        hasWire || hasLiteral,
                        $"Template '{template.Name}' node '{node.Type}' requires value for pin '{pin}' via wire or literal."
                    );
                }
            }
        }
    }

    private static bool IsComparisonNeedingLiteral(NodeViewModel node) =>
        node.Type is NodeType.Equals
            or NodeType.NotEquals
            or NodeType.GreaterThan
            or NodeType.GreaterOrEqual
            or NodeType.LessThan
            or NodeType.LessOrEqual
            or NodeType.Between
            or NodeType.NotBetween;

    private static IReadOnlyList<string> RequiredLiteralPins(NodeType type) =>
        type is NodeType.Between or NodeType.NotBetween
            ? new[] { "low", "high" }
            : new[] { "right" };
}


