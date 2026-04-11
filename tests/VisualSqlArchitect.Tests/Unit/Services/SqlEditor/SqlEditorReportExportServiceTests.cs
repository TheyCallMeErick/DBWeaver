using System.Text.Json;
using System.Collections.Generic;
using ClosedXML.Excel;
using DBWeaver.Core;
using DBWeaver.UI.Services.SqlEditor.Reports;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlEditorReportExportServiceTests
{
    [Fact]
    public async Task ExportAsync_HtmlFullFeature_ContainsSpecSectionsAndDataConstants()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        try
        {
            var sut = new SqlEditorReportExportService();
            SqlEditorReportExportContext context = BuildContext();
            var request = new SqlEditorReportExportRequest(
                ReportType: SqlEditorReportType.HtmlFullFeature,
                FilePath: path,
                Title: "Users audit",
                Description: "Execution report for support",
                IncludeSchema: true,
                IncludeNodeDetails: false,
                IncludeMetadata: false,
                UseDashForEmptyFields: false);

            string exportedPath = await sut.ExportAsync(context, request);
            string html = await File.ReadAllTextAsync(exportedPath);

            Assert.Contains("id=\"s-meta\"", html, StringComparison.Ordinal);
            Assert.Contains("id=\"s-quality\"", html, StringComparison.Ordinal);
            Assert.Contains("id=\"s-nodes\"", html, StringComparison.Ordinal);
            Assert.Contains("id=\"s-conns\"", html, StringComparison.Ordinal);
            Assert.Contains("id=\"metadata-filter-col\"", html, StringComparison.Ordinal);
            Assert.Contains("id=\"metadata-table\"", html, StringComparison.Ordinal);
            Assert.Contains("\"includeMetadata\":false", html, StringComparison.Ordinal);
            Assert.Contains("const HAS_SQL = true;", html, StringComparison.Ordinal);
            Assert.Contains("const NODE_ROWS = null;", html, StringComparison.Ordinal);
            Assert.Contains("const CONN_ROWS = null;", html, StringComparison.Ordinal);
            Assert.Contains("Copy SQL", html, StringComparison.Ordinal);
            Assert.Contains("Validation Summary", html, StringComparison.Ordinal);
            Assert.Contains("Optional Metadata", html, StringComparison.Ordinal);
            Assert.Contains("data-collapse-target=\"s-meta\"", html, StringComparison.Ordinal);
            Assert.Contains("data-collapse-target=\"s-sql\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("id=\"schema-search\"", html, StringComparison.Ordinal);
            Assert.Contains("id=\"nodes-search\"", html, StringComparison.Ordinal);
            Assert.Contains("id=\"conns-search\"", html, StringComparison.Ordinal);
            Assert.Contains("class=\"pager\"", html, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportAsync_HtmlFullFeature_EscapesHeaderAndScriptBreakingSql()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        try
        {
            var sut = new SqlEditorReportExportService();
            SqlEditorReportExportContext context = BuildContext() with
            {
                Sql = "SELECT '</script><script>alert(1)</script>' as payload, `x`, '$y';"
            };
            var request = new SqlEditorReportExportRequest(
                ReportType: SqlEditorReportType.HtmlFullFeature,
                FilePath: path,
                Title: "<img src=x onerror=alert(1)>",
                Description: "<b>unsafe</b>",
                IncludeSchema: true,
                IncludeNodeDetails: false,
                IncludeMetadata: false,
                UseDashForEmptyFields: true);

            string exportedPath = await sut.ExportAsync(context, request);
            string html = await File.ReadAllTextAsync(exportedPath);

            Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html, StringComparison.Ordinal);
            Assert.Contains("&lt;b&gt;unsafe&lt;/b&gt;", html, StringComparison.Ordinal);
            Assert.Contains("<\\/script><script>alert(1)<\\/script>", html, StringComparison.Ordinal);
            Assert.DoesNotContain("const SQL_TEXT = `SELECT '</script><script>alert(1)</script>'", html, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportAsync_HtmlFullFeature_RendersNullForEmptyDescriptionWhenDashIsDisabled()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        try
        {
            var sut = new SqlEditorReportExportService();
            SqlEditorReportExportContext context = BuildContext();
            var request = new SqlEditorReportExportRequest(
                ReportType: SqlEditorReportType.HtmlFullFeature,
                FilePath: path,
                Title: "Users audit",
                Description: string.Empty,
                IncludeSchema: true,
                IncludeNodeDetails: false,
                IncludeMetadata: false,
                UseDashForEmptyFields: false);

            string exportedPath = await sut.ExportAsync(context, request);
            string html = await File.ReadAllTextAsync(exportedPath);

            Assert.Contains("<p class=\"description\">null</p>", html, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportAsync_JsonContract_ContainsNormalizedMetaAndToggleData()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        try
        {
            var sut = new SqlEditorReportExportService();
            SqlEditorReportExportContext context = BuildContext();
            var request = new SqlEditorReportExportRequest(
                ReportType: SqlEditorReportType.JsonContract,
                FilePath: path,
                Title: "Users audit",
                Description: "Execution report for support",
                IncludeSchema: false,
                IncludeNodeDetails: true,
                IncludeMetadata: true,
                UseDashForEmptyFields: false);

            string exportedPath = await sut.ExportAsync(context, request);
            string payload = await File.ReadAllTextAsync(exportedPath);
            using JsonDocument document = JsonDocument.Parse(payload);
            JsonElement root = document.RootElement;
            JsonElement meta = root.GetProperty("meta");

            Assert.Equal("2.3", root.GetProperty("version").GetString());
            Assert.True(root.GetProperty("hasSql").GetBoolean());
            Assert.Equal("warning", meta.GetProperty("summary").GetProperty("status").GetString());
            Assert.False(meta.GetProperty("summary").GetProperty("success").GetBoolean());
            Assert.Equal(2, meta.GetProperty("columnCount").GetInt32());
            Assert.Equal(1, meta.GetProperty("warningCount").GetInt32());
            Assert.Equal(0, meta.GetProperty("errorCount").GetInt32());
            Assert.True(meta.GetProperty("hasSubquery").GetBoolean());
            Assert.Equal(15, meta.GetProperty("metadata").GetArrayLength());
            Assert.Equal(0, root.GetProperty("schema").GetArrayLength());
            Assert.Equal(0, root.GetProperty("nodes").GetArrayLength());
            Assert.Equal(0, root.GetProperty("connections").GetArrayLength());
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportAsync_CsvData_WritesTabularDataOnly()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        try
        {
            var sut = new SqlEditorReportExportService();
            SqlEditorReportExportContext context = BuildContext();
            var request = new SqlEditorReportExportRequest(
                ReportType: SqlEditorReportType.CsvData,
                FilePath: path,
                Title: "Users audit",
                Description: string.Empty,
                IncludeSchema: false,
                IncludeNodeDetails: false,
                IncludeMetadata: false,
                UseDashForEmptyFields: false);

            string exportedPath = await sut.ExportAsync(context, request);
            string csv = await File.ReadAllTextAsync(exportedPath);

            Assert.Contains("id,name", csv, StringComparison.Ordinal);
            Assert.Contains("1,Alice", csv, StringComparison.Ordinal);
            Assert.Contains("2,Bob", csv, StringComparison.Ordinal);
            Assert.DoesNotContain("SELECT", csv, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportAsync_ExcelWorkbook_WritesTabularDataOnly()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
        try
        {
            var sut = new SqlEditorReportExportService();
            SqlEditorReportExportContext context = BuildContext();
            var request = new SqlEditorReportExportRequest(
                ReportType: SqlEditorReportType.ExcelWorkbook,
                FilePath: path,
                Title: "Users audit",
                Description: string.Empty,
                IncludeSchema: false,
                IncludeNodeDetails: false,
                IncludeMetadata: false,
                UseDashForEmptyFields: false);

            string exportedPath = await sut.ExportAsync(context, request);

            using var workbook = new XLWorkbook(exportedPath);
            IXLWorksheet sheet = workbook.Worksheet("Results");

            Assert.Equal("id", sheet.Cell(1, 1).GetString());
            Assert.Equal("name", sheet.Cell(1, 2).GetString());
            Assert.Equal("1", sheet.Cell(2, 1).GetString());
            Assert.Equal("Alice", sheet.Cell(2, 2).GetString());
            Assert.Equal("2", sheet.Cell(3, 1).GetString());
            Assert.Equal("Bob", sheet.Cell(3, 2).GetString());
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static SqlEditorReportExportContext BuildContext()
    {
        var connection = new ConnectionConfig(
            Provider: DatabaseProvider.Postgres,
            Host: "localhost",
            Port: 5432,
            Database: "dbweaver",
            Username: "app",
            Password: "secret");

        return new SqlEditorReportExportContext(
            Sql: "SELECT u.id FROM users u WHERE EXISTS (SELECT 1 FROM users_archive a WHERE a.id = u.id)",
            SchemaColumns: ["id", "name"],
            ResultRows:
            [
                new Dictionary<string, object?>
                {
                    ["id"] = 1,
                    ["name"] = "Alice"
                },
                new Dictionary<string, object?>
                {
                    ["id"] = 2,
                    ["name"] = "Bob"
                }
            ],
            ExecutionResult: new SqlEditorReportExecutionResult(
                RowCount: 2,
                ExecutionTimeMs: 14,
                Status: "warning",
                ErrorMessage: null),
            Connection: connection,
            ActiveFilePath: "/tmp/report.sql",
            TabTitle: "Users query");
    }
}
