using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using DBWeaver.UI.Services.Localization;

namespace DBWeaver.UI.ViewModels;

public sealed class SqlEditorReportExportDialogViewModel : ViewModelBase
{
    private SqlEditorReportTypeOption? _selectedType;
    private string _fileName;
    private string _title;
    private string _description;
    private bool _includeSchema;
    private bool _includeNodeDetails;
    private bool _includeMetadata;
    private bool _useDashForEmptyFields;

    public SqlEditorReportExportDialogViewModel(string defaultTitle)
    {
        string sanitizedTitle = string.IsNullOrWhiteSpace(defaultTitle)
            ? L("sqlEditor.export.defaultFileBase", "report")
            : defaultTitle.Trim();

        ReportTypes =
        [
            new SqlEditorReportTypeOption(
                SqlEditorReportType.HtmlFullFeature,
                L("sqlEditor.export.type.html.title", "HTML full-feature report"),
                L("sqlEditor.export.type.html.description", "Standalone, SQL-first HTML artifact for offline audit."),
                "html"),
            new SqlEditorReportTypeOption(
                SqlEditorReportType.JsonContract,
                L("sqlEditor.export.type.json.title", "JSON execution contract"),
                L("sqlEditor.export.type.json.description", "Machine-readable payload with SQL, metadata and execution result."),
                "json"),
            new SqlEditorReportTypeOption(
                SqlEditorReportType.CsvData,
                L("sqlEditor.export.type.csv.title", "CSV data export"),
                L("sqlEditor.export.type.csv.description", "Tabular result data only, suitable for spreadsheet tools."),
                "csv"),
            new SqlEditorReportTypeOption(
                SqlEditorReportType.ExcelWorkbook,
                L("sqlEditor.export.type.xlsx.title", "Excel workbook export"),
                L("sqlEditor.export.type.xlsx.description", "Spreadsheet workbook with query result data only."),
                "xlsx"),
        ];

        _selectedType = ReportTypes[0];
        _fileName = BuildSuggestedFileName(sanitizedTitle, _selectedType.DefaultExtension);
        _title = sanitizedTitle;
        _description = string.Empty;
        _includeSchema = true;
        _includeMetadata = false;
        _useDashForEmptyFields = false;
    }

    public ObservableCollection<SqlEditorReportTypeOption> ReportTypes { get; }

    public SqlEditorReportTypeOption? SelectedType
    {
        get => _selectedType;
        set
        {
            if (!Set(ref _selectedType, value) || value is null)
                return;

            FileName = EnsureExtension(FileName, value.DefaultExtension);
            RaisePropertyChanged(nameof(ShowOptions));
            RaisePropertyChanged(nameof(ShowIncludeSchema));
            RaisePropertyChanged(nameof(ShowIncludeMetadata));
            RaisePropertyChanged(nameof(ShowUseDashForEmptyFields));
            RaisePropertyChanged(nameof(ShowIncludeNodeDetails));
            RaisePropertyChanged(nameof(CanConfirm));
        }
    }

    public string FileName
    {
        get => _fileName;
        set
        {
            if (!Set(ref _fileName, value))
                return;

            RaisePropertyChanged(nameof(CanConfirm));
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            if (!Set(ref _title, value))
                return;

            RaisePropertyChanged(nameof(CanConfirm));
        }
    }

    public string Description
    {
        get => _description;
        set => Set(ref _description, value);
    }

    public bool IncludeSchema
    {
        get => _includeSchema;
        set => Set(ref _includeSchema, value);
    }

    public bool IncludeNodeDetails
    {
        get => _includeNodeDetails;
        set => Set(ref _includeNodeDetails, value);
    }

    public bool IncludeMetadata
    {
        get => _includeMetadata;
        set => Set(ref _includeMetadata, value);
    }

    public bool UseDashForEmptyFields
    {
        get => _useDashForEmptyFields;
        set => Set(ref _useDashForEmptyFields, value);
    }

    public bool ShowOptions => SelectedType?.Type is SqlEditorReportType.HtmlFullFeature or SqlEditorReportType.JsonContract;

    public bool ShowIncludeSchema => SelectedType?.Type is SqlEditorReportType.HtmlFullFeature or SqlEditorReportType.JsonContract;

    public bool ShowIncludeMetadata => SelectedType?.Type is SqlEditorReportType.HtmlFullFeature or SqlEditorReportType.JsonContract;

    public bool ShowUseDashForEmptyFields => SelectedType?.Type is SqlEditorReportType.HtmlFullFeature or SqlEditorReportType.JsonContract;

    public bool ShowIncludeNodeDetails => SelectedType?.Type == SqlEditorReportType.JsonContract;

    public bool CanConfirm =>
        SelectedType is not null
        && !string.IsNullOrWhiteSpace(FileName)
        && !string.IsNullOrWhiteSpace(Title);

    public SqlEditorReportExportRequest BuildRequest(string filePath)
    {
        if (SelectedType is null)
            throw new InvalidOperationException(L("sqlEditor.export.error.typeRequired", "A report type must be selected before export."));

        return new SqlEditorReportExportRequest(
            ReportType: SelectedType.Type,
            FilePath: filePath,
            Title: string.IsNullOrWhiteSpace(Title) ? L("sqlEditor.export.defaultTitle", "SQL Report") : Title.Trim(),
            Description: string.IsNullOrWhiteSpace(Description) ? string.Empty : Description.Trim(),
            IncludeSchema: IncludeSchema,
            IncludeNodeDetails: IncludeNodeDetails,
            IncludeMetadata: IncludeMetadata,
            UseDashForEmptyFields: UseDashForEmptyFields);
    }

    public string SuggestedExtension => SelectedType?.DefaultExtension ?? "html";

    private static string BuildSuggestedFileName(string title, string extension)
    {
        string safeBase = string.Concat(title.Select(ch =>
            char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));

        if (string.IsNullOrWhiteSpace(safeBase))
            safeBase = "report";

        return EnsureExtension(safeBase, extension);
    }

    private static string EnsureExtension(string fileName, string extension)
    {
        string normalizedFileName = string.IsNullOrWhiteSpace(fileName) ? "report" : fileName.Trim();
        string normalizedExtension = extension.TrimStart('.');

        if (normalizedFileName.EndsWith($".{normalizedExtension}", StringComparison.OrdinalIgnoreCase))
            return normalizedFileName;

        return Path.GetFileNameWithoutExtension(normalizedFileName) + "." + normalizedExtension;
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
