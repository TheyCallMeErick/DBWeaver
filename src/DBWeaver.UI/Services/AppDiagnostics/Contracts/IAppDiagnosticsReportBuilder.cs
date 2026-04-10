using DBWeaver.UI.Services.AppDiagnostics.Models;

namespace DBWeaver.UI.Services.AppDiagnostics.Contracts;

public interface IAppDiagnosticsReportBuilder
{
    string BuildReport(string overallLabel, IEnumerable<AppDiagnosticCategoryViewModel> categories);
}
