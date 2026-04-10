
namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationPivotValidator(DatabaseProvider provider)
{
    private readonly DatabaseProvider _provider = provider;

    public void Validate(NodeViewModel resultOutputNode, List<string> errors)
    {
        string mode = resultOutputNode.Parameters.TryGetValue("pivot_mode", out string? modeRaw)
            ? (modeRaw ?? "NONE").Trim().ToUpperInvariant()
            : "NONE";

        if (mode is not ("PIVOT" or "UNPIVOT"))
            return;

        if (_provider != DatabaseProvider.SqlServer)
        {
            errors.Add("PIVOT/UNPIVOT is currently applied only for SQL Server. Configuration will be ignored for this provider.");
            return;
        }

        string config = resultOutputNode.Parameters.TryGetValue("pivot_config", out string? configRaw)
            ? (configRaw ?? string.Empty).Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(config))
        {
            errors.Add("Pivot mode is enabled but 'pivot_config' is empty.");
            return;
        }

        if (config.Contains(';', StringComparison.Ordinal))
            errors.Add("Pivot configuration contains ';'. Use only the PIVOT/UNPIVOT body expression.");
    }
}



