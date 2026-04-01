using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.QueryEngine;
using VisualSqlArchitect.Registry;
using VisualSqlArchitect.UI.Serialization;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VisualSqlArchitect.UI.ViewModels.QueryPreview.Services;

internal static class QueryGraphHelpers
{
    internal static string? ResolveTextInput(CanvasViewModel canvas, NodeViewModel node, string pinName)
    {
        ConnectionViewModel? wire = canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == node
            && c.ToPin?.Name == pinName
        );

        if (wire is null)
        {
            if (
                node.PinLiterals.TryGetValue(pinName, out string? literal)
                && !string.IsNullOrWhiteSpace(literal)
            )
            {
                return literal.Trim().Trim('\'', '"').Trim();
            }

            return null;
        }

        if (
            wire.FromPin.Owner.Parameters.TryGetValue("value", out string? value)
            && !string.IsNullOrWhiteSpace(value)
        )
        {
            return value.Trim();
        }

        return null;
    }

    internal static bool LooksLikeSelectStatement(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return false;

        string trimmed = sql.TrimStart();

        if (trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return true;

        if (trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
            return true;

        if (trimmed.StartsWith("(", StringComparison.Ordinal))
        {
            string inner = trimmed[1..].TrimStart();
            return inner.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                || inner.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
