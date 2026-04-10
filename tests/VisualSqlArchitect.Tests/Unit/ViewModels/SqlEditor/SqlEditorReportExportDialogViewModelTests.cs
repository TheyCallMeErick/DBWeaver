using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.SqlEditor;

public sealed class SqlEditorReportExportDialogViewModelTests
{
    [Fact]
    public void Constructor_InitializesDefaultsForHtmlReport()
    {
        var sut = new SqlEditorReportExportDialogViewModel("Quarterly Audit");

        Assert.Equal(4, sut.ReportTypes.Count);
        Assert.Equal(SqlEditorReportType.HtmlFullFeature, sut.SelectedType?.Type);
        Assert.Equal("Quarterly_Audit.html", sut.FileName);
        Assert.Equal("Quarterly Audit", sut.Title);
        Assert.Equal(string.Empty, sut.Description);
        Assert.True(sut.IncludeSchema);
        Assert.False(sut.IncludeMetadata);
        Assert.False(sut.UseDashForEmptyFields);
        Assert.False(sut.ShowIncludeNodeDetails);
        Assert.True(sut.CanConfirm);
        Assert.Contains(sut.ReportTypes, option => option.Type == SqlEditorReportType.CsvData);
        Assert.Contains(sut.ReportTypes, option => option.Type == SqlEditorReportType.ExcelWorkbook);
    }

    [Fact]
    public void SelectedType_WhenChangedToJson_UpdatesFileExtensionAndVisibility()
    {
        var sut = new SqlEditorReportExportDialogViewModel("Test");

        sut.FileName = "custom_name";
        sut.SelectedType = sut.ReportTypes.Single(type => type.Type == SqlEditorReportType.JsonContract);

        Assert.Equal("custom_name.json", sut.FileName);
        Assert.True(sut.ShowIncludeNodeDetails);
        Assert.Equal("json", sut.SuggestedExtension);
    }

    [Fact]
    public void BuildRequest_TrimsValuesAndKeepsFlags()
    {
        var sut = new SqlEditorReportExportDialogViewModel("Test")
        {
            Title = "  SQL Export  ",
            Description = "  Support payload  ",
            IncludeSchema = false,
            IncludeNodeDetails = true,
        };

        sut.SelectedType = sut.ReportTypes.Single(type => type.Type == SqlEditorReportType.JsonContract);

        SqlEditorReportExportRequest request = sut.BuildRequest("/tmp/report.json");

        Assert.Equal(SqlEditorReportType.JsonContract, request.ReportType);
        Assert.Equal("/tmp/report.json", request.FilePath);
        Assert.Equal("SQL Export", request.Title);
        Assert.Equal("Support payload", request.Description);
        Assert.False(request.IncludeSchema);
        Assert.True(request.IncludeNodeDetails);
        Assert.False(request.IncludeMetadata);
        Assert.False(request.UseDashForEmptyFields);
    }

    [Fact]
    public void CanConfirm_ReturnsFalseWhenFileNameOrTitleAreBlank()
    {
        var sut = new SqlEditorReportExportDialogViewModel("Test");

        sut.FileName = " ";
        Assert.False(sut.CanConfirm);

        sut.FileName = "report.html";
        sut.Title = " ";
        Assert.False(sut.CanConfirm);
    }
}
