using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;

internal static class QueryPreviewTestNodeFactory
{
    public static NodeViewModel Node(NodeType type) =>
        new(NodeDefinitionRegistry.Get(type), new Point(0, 0));

    public static NodeViewModel Table(string tableName, params string[] columns) =>
        new(tableName, columns.Select(c => (c, PinDataType.Number)), new Point(0, 0));

    public static NodeViewModel NumberTable(string tableName, params string[] columns) =>
        Table(tableName, columns);

    public static NodeViewModel TableWithNameText(string tableName, params string[] columns) =>
        new(
            tableName,
            columns.Select(c => (c, c.Equals("name", StringComparison.OrdinalIgnoreCase) ? PinDataType.Text : PinDataType.Number)),
            new Point(0, 0));

    public static NodeViewModel Table(string tableName, params (string Name, PinDataType Type)[] columns) =>
        new(tableName, columns, new Point(0, 0));

    public static void Connect(
        CanvasViewModel canvas,
        NodeViewModel fromNode,
        string fromPin,
        NodeViewModel toNode,
        string toPin)
    {
        PinViewModel from = fromNode.OutputPins.First(p => p.Name == fromPin);
        PinViewModel to = toNode.InputPins.FirstOrDefault(p => p.Name == toPin)
            ?? toNode.OutputPins.First(p => p.Name == toPin);

        canvas.Connections.Add(new ConnectionViewModel(from, from.AbsolutePosition, to.AbsolutePosition)
        {
            ToPin = to,
        });
    }

    public static void ConnectAllowOutputFallback(
        CanvasViewModel canvas,
        NodeViewModel fromNode,
        string fromPin,
        NodeViewModel toNode,
        string toPin) =>
        Connect(canvas, fromNode, fromPin, toNode, toPin);
}
