using System.Text.RegularExpressions;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public interface IExplainSqlSafetyEvaluator
{
    bool LooksMutating(string sql);
}

public sealed class ExplainSqlSafetyEvaluator : IExplainSqlSafetyEvaluator
{
    public bool LooksMutating(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return false;

        string normalized = RemoveLeadingComments(sql).TrimStart();
        if (normalized.Length == 0)
            return false;

        var match = Regex.Match(normalized, @"^([A-Z]+)", RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        string verb = match.Groups[1].Value.ToUpperInvariant();
        return verb is "INSERT" or "UPDATE" or "DELETE" or "MERGE" or "ALTER" or "DROP" or "TRUNCATE";
    }

    private static string RemoveLeadingComments(string sql)
    {
        string text = sql;
        bool changed;
        do
        {
            changed = false;
            string trimmed = text.TrimStart();

            if (trimmed.StartsWith("--", StringComparison.Ordinal))
            {
                int newline = trimmed.IndexOf('\n');
                text = newline >= 0 ? trimmed[(newline + 1)..] : string.Empty;
                changed = true;
                continue;
            }

            if (trimmed.StartsWith("/*", StringComparison.Ordinal))
            {
                int end = trimmed.IndexOf("*/", StringComparison.Ordinal);
                text = end >= 0 ? trimmed[(end + 2)..] : string.Empty;
                changed = true;
            }
        } while (changed);

        return text;
    }
}



