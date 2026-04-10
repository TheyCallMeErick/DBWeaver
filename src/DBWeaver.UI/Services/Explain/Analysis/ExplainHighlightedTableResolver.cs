using System.Text.RegularExpressions;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public interface IExplainHighlightedTableResolver
{
    string? Resolve(ExplainStep? step);
}

public sealed partial class ExplainHighlightedTableResolver : IExplainHighlightedTableResolver
{
    public string? Resolve(ExplainStep? step)
    {
        if (step is null)
            return null;

        string? fromDetail = ExtractFromDetail(step.Detail);
        if (!string.IsNullOrWhiteSpace(fromDetail))
            return fromDetail;

        return ExtractFromOperation(step.Operation);
    }

    private static string? ExtractFromDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return null;

        Match match = RelationPattern().Match(detail);
        if (!match.Success)
            return null;

        return NormalizeTableName(match.Groups["table"].Value);
    }

    private static string? ExtractFromOperation(string? operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
            return null;

        Match match = OperationOnPattern().Match(operation);
        if (!match.Success)
            return null;

        return NormalizeTableName(match.Groups["table"].Value);
    }

    private static string? NormalizeTableName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string raw = value.Trim();
        if (raw.Length == 0)
            return null;

        string[] parts = raw.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return null;

        for (int i = 0; i < parts.Length; i++)
            parts[i] = parts[i].Trim('"').Trim('`').Trim('[', ']');

        return string.Join('.', parts);
    }

    [GeneratedRegex(@"relation\s*=\s*(?<table>[A-Za-z0-9_.""`\[\]]+)", RegexOptions.IgnoreCase)]
    private static partial Regex RelationPattern();

    [GeneratedRegex(@"\bon\s+(?<table>[A-Za-z0-9_.""`\[\]]+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex OperationOnPattern();
}



