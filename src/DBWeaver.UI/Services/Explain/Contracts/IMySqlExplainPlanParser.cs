using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public interface IMySqlExplainPlanParser
{
    MySqlParsedPlan ParseJson(string rawJson);
    MySqlParsedPlan ParseAnalyze(string rawAnalyzeText);
}



