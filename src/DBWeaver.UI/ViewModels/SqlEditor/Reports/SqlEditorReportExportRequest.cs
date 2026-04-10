namespace DBWeaver.UI.ViewModels;

public sealed record SqlEditorReportExportRequest(
    SqlEditorReportType ReportType,
    string FilePath,
    string Title,
    string Description,
    bool IncludeSchema,
    bool IncludeNodeDetails,
    bool IncludeMetadata,
    bool UseDashForEmptyFields
);
