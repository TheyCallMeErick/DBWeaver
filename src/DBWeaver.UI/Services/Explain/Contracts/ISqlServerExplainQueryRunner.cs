using DBWeaver.Core;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public interface ISqlServerExplainQueryRunner
{
    Task<string> ExecuteShowPlanXmlAsync(
        string sql,
        ConnectionConfig connectionConfig,
        CancellationToken ct = default
    );

    Task<string> ExecuteStatisticsXmlAsync(
        string sql,
        ConnectionConfig connectionConfig,
        CancellationToken ct = default
    );
}



