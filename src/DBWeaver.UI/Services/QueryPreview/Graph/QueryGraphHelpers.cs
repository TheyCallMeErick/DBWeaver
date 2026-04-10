
namespace DBWeaver.UI.Services.QueryPreview;

internal static class QueryGraphHelpers
{
    private static readonly IReadOnlyDictionary<string, string[]> LegacyParameterFallbacks =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["query_text"] = ["query", "sql", "sql_text"],
            ["alias_text"] = ["alias"],
            ["operator_text"] = ["operator"],
            ["cte_name_text"] = ["cte_name"],
            ["name_text"] = ["name", "cte_name"],
            ["source_table_text"] = ["source_table", "from_table", "table_full_name", "table"],
        };

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

            if (LegacyParameterFallbacks.TryGetValue(pinName, out string[]? fallbacks))
            {
                foreach (string fallback in fallbacks)
                {
                    if (node.PinLiterals.TryGetValue(fallback, out string? fallbackLiteral)
                        && !string.IsNullOrWhiteSpace(fallbackLiteral))
                    {
                        return fallbackLiteral.Trim().Trim('\'', '"').Trim();
                    }

                    if (node.Parameters.TryGetValue(fallback, out string? parameterValue)
                        && !string.IsNullOrWhiteSpace(parameterValue))
                    {
                        return parameterValue.Trim();
                    }
                }
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


