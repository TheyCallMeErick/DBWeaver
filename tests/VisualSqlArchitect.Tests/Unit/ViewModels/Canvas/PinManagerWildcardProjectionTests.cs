using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class PinManagerWildcardProjectionTests
{
    [Fact]
    public void ConnectPins_WildcardToColumnList_RemovesSameTableColumnWiresAndKeepsOnlyWildcard()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = new("public.orders", [("id", PinDataType.Number), ("customer_id", PinDataType.Number)], new Point(0, 0));
        NodeViewModel columnList = new(NodeDefinitionRegistry.Get(NodeType.ColumnList), new Point(180, 0));

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);

        PinViewModel columnsPin = columnList.InputPins.First(p => p.Name == "columns");
        canvas.ConnectPins(orders.OutputPins.First(p => p.Name == "id"), columnsPin);
        canvas.ConnectPins(orders.OutputPins.First(p => p.Name == "customer_id"), columnsPin);

        Assert.Equal(2, canvas.Connections.Count(c =>
            c.ToPin?.Owner == columnList
            && c.ToPin.Name == "columns"
            && c.FromPin.Owner == orders));

        canvas.UndoRedo.Clear();
        canvas.ConnectPins(orders.OutputPins.First(p => p.Name == "*"), columnsPin);

        List<ConnectionViewModel> remaining = [.. canvas.Connections.Where(c =>
            c.ToPin?.Owner == columnList
            && c.ToPin.Name == "columns"
            && c.FromPin.Owner == orders)];

        Assert.Single(remaining);
        Assert.Equal("*", remaining[0].FromPin.Name);

        canvas.UndoRedo.Undo();

        List<ConnectionViewModel> restored = [.. canvas.Connections.Where(c =>
            c.ToPin?.Owner == columnList
            && c.ToPin.Name == "columns"
            && c.FromPin.Owner == orders)];

        Assert.Equal(2, restored.Count);
        Assert.DoesNotContain(restored, c => c.FromPin.Name == "*");
    }

    [Fact]
    public void ConnectPins_WildcardToColumnList_PreservesConnectionsFromOtherTables()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = new("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        NodeViewModel customers = new("public.customers", [("id", PinDataType.Number)], new Point(0, 90));
        NodeViewModel columnList = new(NodeDefinitionRegistry.Get(NodeType.ColumnList), new Point(240, 0));

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(customers);
        canvas.Nodes.Add(columnList);

        PinViewModel columnsPin = columnList.InputPins.First(p => p.Name == "columns");
        canvas.ConnectPins(orders.OutputPins.First(p => p.Name == "id"), columnsPin);
        canvas.ConnectPins(customers.OutputPins.First(p => p.Name == "id"), columnsPin);

        canvas.ConnectPins(orders.OutputPins.First(p => p.Name == "*"), columnsPin);

        Assert.DoesNotContain(canvas.Connections, c =>
            c.ToPin?.Owner == columnList
            && c.FromPin.Owner == orders
            && c.FromPin.Name == "id");
        Assert.Contains(canvas.Connections, c =>
            c.ToPin?.Owner == columnList
            && c.FromPin.Owner == orders
            && c.FromPin.Name == "*");
        Assert.Contains(canvas.Connections, c =>
            c.ToPin?.Owner == columnList
            && c.FromPin.Owner == customers
            && c.FromPin.Name == "id");
    }
}


