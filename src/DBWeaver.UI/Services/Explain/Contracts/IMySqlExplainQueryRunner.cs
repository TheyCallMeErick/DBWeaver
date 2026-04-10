using DBWeaver.Core;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public interface IMySqlExplainQueryRunner
{
    Task<string> ExecuteFormatJsonAsync(
        string sql,
        ConnectionConfig connectionConfig,
        CancellationToken ct = default
    );

    Task<string> ExecuteAnalyzeAsync(
        string sql,
        ConnectionConfig connectionConfig,
        CancellationToken ct = default
    );
}



