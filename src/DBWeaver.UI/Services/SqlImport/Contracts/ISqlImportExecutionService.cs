using System.Collections.ObjectModel;
using DBWeaver.UI.Services.SqlImport.Execution;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.SqlImport.Contracts;

public interface ISqlImportExecutionService
{
    SqlImportExecutionResult Execute(
        string sql,
        ObservableCollection<ImportReportItem> report,
        CancellationToken cancellationToken
    );
}
