using System.Text.RegularExpressions;
using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;
using DBWeaver.UI.Services.SqlImport;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.SqlImport.Build;

internal static class ImportBuildUtilities
{
    public static bool TryResolveExpressionPin(
        string expression,
        IReadOnlyList<ImportFromPart> fromParts,
        IReadOnlyList<NodeViewModel> tableNodes,
        out PinViewModel pin)
    {
        pin = null!;
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        if (!TryResolveSourceAndColumn(expression, fromParts, out int sourceIndex, out string column))
            return false;

        if (sourceIndex < 0 || sourceIndex >= tableNodes.Count)
            return false;

        NodeViewModel node = tableNodes[sourceIndex];
        PinViewModel? match = node.OutputPins.FirstOrDefault(p =>
            p.Name.Equals(column, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            return false;

        pin = match;
        return true;
    }

    public static bool TryResolveSourceAndColumn(
        string expression,
        IReadOnlyList<ImportFromPart> fromParts,
        out int sourceIndex,
        out string column)
    {
        sourceIndex = -1;
        column = string.Empty;

        if (string.IsNullOrWhiteSpace(expression))
            return false;

        string normalized = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(expression);
        string[] segments = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            return false;

        column = segments[^1];
        if (string.IsNullOrWhiteSpace(column))
            return false;

        if (segments.Length == 1)
        {
            if (fromParts.Count == 1)
            {
                sourceIndex = 0;
                return true;
            }

            return false;
        }

        string qualifier = string.Join('.', segments[..^1]);
        sourceIndex = FindSourceIndexForQualifier(qualifier, fromParts);
        if (sourceIndex < 0 && fromParts.Count == 1)
            sourceIndex = 0;
        return sourceIndex >= 0;
    }

    public static bool TryFindSourceIndexForQualifier(
        string qualifier,
        IReadOnlyList<ImportFromPart> fromParts,
        out int sourceIndex)
    {
        sourceIndex = FindSourceIndexForQualifier(qualifier, fromParts);
        return sourceIndex >= 0;
    }

    private static int FindSourceIndexForQualifier(string qualifier, IReadOnlyList<ImportFromPart> fromParts)
    {
        for (int i = 0; i < fromParts.Count; i++)
        {
            ImportFromPart part = fromParts[i];
            string table = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(part.Table);
            string shortTable = table.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault()
                ?? table;

            if (!string.IsNullOrWhiteSpace(part.Alias)
                && qualifier.Equals(part.Alias!.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }

            if (qualifier.Equals(table, StringComparison.OrdinalIgnoreCase)
                || qualifier.Equals(shortTable, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    public static string RewriteQualifierToAlias(
        string expression,
        IReadOnlyList<ImportFromPart> fromParts)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return expression;

        string normalized = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(expression);
        string[] segments = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
            return normalized;

        string column = segments[^1];
        string qualifier = string.Join('.', segments[..^1]);

        foreach (ImportFromPart part in fromParts)
        {
            if (string.IsNullOrWhiteSpace(part.Alias))
                continue;

            string alias = part.Alias!.Trim();
            if (qualifier.Equals(alias, StringComparison.OrdinalIgnoreCase))
                return $"{alias}.{column}";

            string normalizedTable = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(part.Table);
            string shortTable = normalizedTable.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault()
                ?? normalizedTable;

            if (qualifier.Equals(normalizedTable, StringComparison.OrdinalIgnoreCase)
                || qualifier.Equals(shortTable, StringComparison.OrdinalIgnoreCase))
            {
                return $"{alias}.{column}";
            }
        }

        return normalized;
    }

    public static string RewriteKnownQualifiersToAliasInExpression(
        string expression,
        IReadOnlyList<ImportFromPart> fromParts)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return expression;

        return Regex.Replace(
            expression,
            SqlImportIdentifierNormalizer.QualifiedIdentifierPattern,
            match =>
            {
                string normalized = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(match.Value);
                string[] segments = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (segments.Length < 2)
                    return normalized;

                string qualifier = string.Join('.', segments[..^1]);
                string column = segments[^1];
                int sourceIndex = FindSourceIndexForQualifier(qualifier, fromParts);
                if (sourceIndex < 0 || sourceIndex >= fromParts.Count)
                    return normalized;

                string? alias = fromParts[sourceIndex].Alias?.Trim();
                return string.IsNullOrWhiteSpace(alias)
                    ? normalized
                    : $"{alias}.{column}";
            },
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static PinViewModel EnsureOutputColumnPin(NodeViewModel sourceNode, string columnName, PinDataType scalarType = PinDataType.Expression)
    {
        PinViewModel? existing = sourceNode.OutputPins.FirstOrDefault(pin =>
            pin.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        string tableAlias = !string.IsNullOrWhiteSpace(sourceNode.Alias)
            ? sourceNode.Alias!
            : sourceNode.Subtitle?.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault()
              ?? sourceNode.Title;

        var created = new PinViewModel(
            new PinDescriptor(
                columnName,
                PinDirection.Output,
                PinDataType.ColumnRef,
                IsRequired: false,
                Description: $"Inferred column from imported SQL: {columnName}",
                AllowMultiple: false,
                ColumnRefMeta: new ColumnRefMeta(columnName, tableAlias, scalarType, true)),
            sourceNode);

        sourceNode.OutputPins.Add(created);
        return created;
    }

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

    public static string NormalizeJoinType(string rawJoinType)
    {
        string normalized = rawJoinType.ToUpperInvariant();
        if (normalized.Contains("LEFT", StringComparison.Ordinal))
            return "LEFT";
        if (normalized.Contains("RIGHT", StringComparison.Ordinal))
            return "RIGHT";
        if (normalized.Contains("FULL", StringComparison.Ordinal))
            return "FULL";
        if (normalized.Contains("CROSS", StringComparison.Ordinal))
            return "CROSS";
        return "INNER";
    }

    public static bool TryParseSimpleBinaryPredicate(
        string expression,
        out string left,
        out string @operator,
        out string right)
    {
        string candidate = StripOuterParentheses(expression);
        Match match = Regex.Match(
            candidate,
            $@"^\s*(?<left>{SqlImportIdentifierNormalizer.QualifiedIdentifierPattern})\s*(?<op><>|!=|>=|<=|=|>|<)\s*(?<right>{SqlImportIdentifierNormalizer.QualifiedIdentifierPattern})\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            left = string.Empty;
            @operator = string.Empty;
            right = string.Empty;
            return false;
        }

        left = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(match.Groups["left"].Value);
        @operator = match.Groups["op"].Value.Trim();
        right = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(match.Groups["right"].Value);
        return true;
    }

    private static string StripOuterParentheses(string value)
    {
        string result = value?.Trim() ?? string.Empty;
        while (result.Length >= 2 && result[0] == '(' && result[^1] == ')')
        {
            result = result[1..^1].Trim();
        }

        return result;
    }
}
