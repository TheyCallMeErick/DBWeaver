using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public interface ISqlServerExplainPlanParser
{
    SqlServerParsedPlan Parse(string rawXml);
}



