using DBWeaver.Core;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Integration.ManualPlan;

internal static class ManualPlanTemplateSqlGenerator
{
    public static string GenerateSql(DatabaseProvider provider, string templateName)
    {
        using var canvas = new CanvasViewModel();

        QueryTemplate template = QueryTemplateLibrary.All.First(t =>
            string.Equals(t.Name, templateName, StringComparison.Ordinal)
        );

        canvas.LoadTemplate(template);
        canvas.LiveSql.Provider = provider;
        canvas.LiveSql.Recompile();

        return canvas.LiveSql.RawSql;
    }
}
