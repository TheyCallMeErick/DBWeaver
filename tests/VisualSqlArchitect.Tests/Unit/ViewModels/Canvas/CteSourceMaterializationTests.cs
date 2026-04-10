using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class CteSourceMaterializationTests
{
    [Fact]
    public void CteSource_MaterializesColumns_WhenCteNameMatchesDefinitionWithoutDirectWire()
    {
        var canvas = new CanvasViewModel();
        var table = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        var columnList = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnList), new Point(140, 0));
        var resultOutput = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ResultOutput), new Point(280, 0));
        var cteDefinition = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.CteDefinition), new Point(420, 0));
        var cteSource = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.CteSource), new Point(560, 0));

        cteDefinition.Parameters["name"] = "orders_cte";
        cteSource.Parameters["cte_name"] = "orders_cte";

        canvas.Nodes.Add(table);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(resultOutput);
        canvas.Nodes.Add(cteDefinition);
        canvas.Nodes.Add(cteSource);

        Connect(canvas, table, "id", columnList, "columns");
        Connect(canvas, columnList, "result", resultOutput, "columns");
        Connect(canvas, resultOutput, "result", cteDefinition, "query");

        Assert.Contains(cteSource.OutputPins, pin => string.Equals(pin.Name, "id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cteSource.OutputPins, pin => string.Equals(pin.Name, "*", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CteSource_MaterializesColumns_FromWildcardColumnSetMeta_WhenSourceHasNoScalarPins()
    {
        var canvas = new CanvasViewModel();
        var wildcardOnlySource = new NodeViewModel(
            new NodeDefinition(
                NodeType.SubqueryReference,
                NodeCategory.DataSource,
                "Wildcard Source",
                "Synthetic source with only wildcard metadata",
                [
                    new PinDescriptor(
                        "*",
                        PinDirection.Output,
                        PinDataType.ColumnSet,
                        IsRequired: false,
                        Description: "Wildcard projection",
                        ColumnSetMeta: new ColumnSetMeta(
                            [
                                new ColumnRefMeta("id", "s", PinDataType.Number, true),
                                new ColumnRefMeta("name", "s", PinDataType.Text, true),
                            ]))
                ],
                []),
            new Point(0, 0));
        var resultOutput = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ResultOutput), new Point(140, 0));
        var cteDefinition = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.CteDefinition), new Point(280, 0));
        var cteSource = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.CteSource), new Point(420, 0));

        cteDefinition.Parameters["name"] = "meta_cte";
        cteSource.Parameters["cte_name"] = "meta_cte";

        canvas.Nodes.Add(wildcardOnlySource);
        canvas.Nodes.Add(resultOutput);
        canvas.Nodes.Add(cteDefinition);
        canvas.Nodes.Add(cteSource);

        Connect(canvas, wildcardOnlySource, "*", resultOutput, "columns");
        Connect(canvas, resultOutput, "result", cteDefinition, "query");

        cteSource.SyncCteSourceColumns(canvas.Connections);

        Assert.Contains(cteSource.OutputPins, pin => string.Equals(pin.Name, "id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cteSource.OutputPins, pin => string.Equals(pin.Name, "name", StringComparison.OrdinalIgnoreCase));
        PinViewModel wildcardPin = Assert.Single(cteSource.OutputPins, pin => string.Equals(pin.Name, "*", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(wildcardPin.ColumnSetMeta);
        Assert.Equal(2, wildcardPin.ColumnSetMeta!.Columns.Count);
    }

    private static void Connect(
        CanvasViewModel canvas,
        NodeViewModel fromNode,
        string fromPin,
        NodeViewModel toNode,
        string toPin)
    {
        PinViewModel from = fromNode.OutputPins.First(pin => pin.Name == fromPin);
        PinViewModel to = toNode.InputPins.First(pin => pin.Name == toPin);
        canvas.Connections.Add(new ConnectionViewModel(from, from.AbsolutePosition, to.AbsolutePosition)
        {
            ToPin = to,
        });
    }
}
