using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public interface IExplainSqlPreviewTextResolver
{
    string Resolve(string? sql);
}

public sealed class ExplainSqlPreviewTextResolver : IExplainSqlPreviewTextResolver
{
    public string Resolve(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return "No SQL available.";

        return sql;
    }
}



