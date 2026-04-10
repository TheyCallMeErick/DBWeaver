using System.Text.RegularExpressions;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.SqlImport.Execution.Applying;

internal static class SqlImportClauseApplyUtilities
{
    public static void SafeWire(NodeViewModel from, string fromPin, NodeViewModel to, string toPin, CanvasViewModel canvas)
    {
        PinViewModel? fromResolvedPin =
            from.OutputPins.FirstOrDefault(p => p.Name.Equals(fromPin, StringComparison.OrdinalIgnoreCase))
            ?? from.InputPins.FirstOrDefault(p => p.Name.Equals(fromPin, StringComparison.OrdinalIgnoreCase));

        PinViewModel? toResolvedPin =
            to.InputPins.FirstOrDefault(p => p.Name.Equals(toPin, StringComparison.OrdinalIgnoreCase))
            ?? to.OutputPins.FirstOrDefault(p => p.Name.Equals(toPin, StringComparison.OrdinalIgnoreCase));

        if (fromResolvedPin is null || toResolvedPin is null)
            return;

        if (!toResolvedPin.EvaluateConnection(fromResolvedPin).IsAllowed)
            return;

        var conn = new ConnectionViewModel(fromResolvedPin, default, default) { ToPin = toResolvedPin };
        fromResolvedPin.IsConnected = true;
        toResolvedPin.IsConnected = true;
        canvas.Connections.Add(conn);
    }

    public static bool LooksLikeAggregateExpression(string expression) =>
        Regex.IsMatch(
            expression,
            @"^\s*(COUNT|SUM|AVG|MIN|MAX|STRING_AGG|ARRAY_AGG|JSON_AGG)\s*\(",
            RegexOptions.IgnoreCase
        );

    public static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "...";
}
