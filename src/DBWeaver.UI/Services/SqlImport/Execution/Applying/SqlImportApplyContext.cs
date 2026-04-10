using System.Collections.ObjectModel;
using DBWeaver.UI.Services.SqlImport.Build;
using DBWeaver.UI.Services.SqlImport.Execution.Parsing;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.SqlImport.Execution.Applying;

internal sealed record SqlImportApplyContext(
    SqlImportParsedQuery Query,
    ImportBuildContext CoreContext,
    ObservableCollection<ImportReportItem> Report,
    CancellationToken CancellationToken
);
