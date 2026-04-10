using System.Collections.Generic;
using DBWeaver.Core;

namespace DBWeaver.UI.ViewModels;

public sealed record SqlEditorReportExportContext(
    string Sql,
    IReadOnlyList<string> SchemaColumns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> ResultRows,
    SqlEditorReportExecutionResult ExecutionResult,
    ConnectionConfig? Connection,
    string? ActiveFilePath,
    string TabTitle
);
