
namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationWindowFunctionValidator(CanvasViewModel canvas)
{
    private readonly CanvasViewModel _canvas = canvas;

    public void Validate(List<string> errors)
    {
        foreach (NodeViewModel node in _canvas.Nodes.Where(n => n.Type == NodeType.WindowFunction))
        {
            string function = node.Parameters.TryGetValue("function", out string? functionRaw)
                ? (functionRaw ?? "RowNumber").Trim()
                : "RowNumber";

            bool hasValueInput = HasInputConnection(node, "value");
            bool hasOrderInput = HasAnyInputWithPrefix(node, "order_");

            if (RequiresValueInput(function) && !hasValueInput)
            {
                errors.Add($"Window function '{function}' requires a connected 'value' input.");
            }

            if (RequiresOrderInput(function) && !hasOrderInput)
            {
                errors.Add($"Window function '{function}' requires at least one ORDER BY input (order_* pin).");
            }

            if (node.Parameters.TryGetValue("frame", out string? frame)
                && !string.IsNullOrWhiteSpace(frame)
                && !frame.Equals("None", StringComparison.OrdinalIgnoreCase)
                && !hasOrderInput)
            {
                errors.Add("Window frame is configured but no ORDER BY input is connected; frame clause will be ignored.");
            }

            if (node.Parameters.TryGetValue("frame", out string? customFrame)
                && customFrame.Equals("Custom", StringComparison.OrdinalIgnoreCase))
            {
                ValidateWindowFrameOffset(node, "frame_start", "frame_start_offset", errors);
                ValidateWindowFrameOffset(node, "frame_end", "frame_end_offset", errors);
            }

            if (function.Equals("Lag", StringComparison.OrdinalIgnoreCase)
                || function.Equals("Lead", StringComparison.OrdinalIgnoreCase))
            {
                if (node.Parameters.TryGetValue("offset", out string? offsetRaw)
                    && !string.IsNullOrWhiteSpace(offsetRaw)
                    && (!int.TryParse(offsetRaw, out int offset) || offset <= 0))
                {
                    errors.Add($"Window function '{function}' has invalid offset '{offsetRaw}'. Using default offset 1.");
                }
            }

            if (function.Equals("Ntile", StringComparison.OrdinalIgnoreCase)
                && node.Parameters.TryGetValue("ntile_groups", out string? groupsRaw)
                && !string.IsNullOrWhiteSpace(groupsRaw)
                && (!int.TryParse(groupsRaw, out int groups) || groups <= 0))
            {
                errors.Add("Window function 'Ntile' has invalid ntile_groups '" + groupsRaw + "'. Using default 4.");
            }
        }
    }

    private bool HasInputConnection(NodeViewModel node, string pinName) =>
        _canvas.Connections.Any(c =>
            c.ToPin?.Owner == node
            && c.ToPin.Name.Equals(pinName, StringComparison.OrdinalIgnoreCase)
        );

    private bool HasAnyInputWithPrefix(NodeViewModel node, string prefix) =>
        _canvas.Connections.Any(c =>
            c.ToPin?.Owner == node
            && c.ToPin.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        );

    private static bool RequiresValueInput(string functionName)
    {
        return functionName.Equals("Lag", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("Lead", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("FirstValue", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("LastValue", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("SumOver", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("AvgOver", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("MinOver", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("MaxOver", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresOrderInput(string functionName)
    {
        return functionName.Equals("RowNumber", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("Rank", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("DenseRank", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("Ntile", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("Lag", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("Lead", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("FirstValue", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("LastValue", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateWindowFrameOffset(
        NodeViewModel node,
        string boundParam,
        string offsetParam,
        List<string> errors)
    {
        if (!node.Parameters.TryGetValue(boundParam, out string? boundRaw)
            || string.IsNullOrWhiteSpace(boundRaw))
        {
            return;
        }

        string bound = boundRaw.Trim();
        if (bound is not ("Preceding" or "Following"))
            return;

        if (!node.Parameters.TryGetValue(offsetParam, out string? offsetRaw)
            || string.IsNullOrWhiteSpace(offsetRaw)
            || !int.TryParse(offsetRaw, out int offset)
            || offset < 0)
        {
            errors.Add($"Window frame bound '{boundParam}' requires non-negative numeric '{offsetParam}'. Using default 1.");
        }
    }
}



