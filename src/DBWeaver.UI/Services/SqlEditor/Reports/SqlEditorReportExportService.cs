using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DBWeaver.Core;
using DBWeaver.UI.Services.Theming;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.SqlEditor.Reports;

public sealed class SqlEditorReportExportService
{
    private const string ReportVersion = "2.2";

    public async Task<string> ExportAsync(
        SqlEditorReportExportContext context,
        SqlEditorReportExportRequest request,
        CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(request.FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        switch (request.ReportType)
        {
          case SqlEditorReportType.HtmlFullFeature:
            await File.WriteAllTextAsync(request.FilePath, BuildHtml(context, request), Encoding.UTF8, cancellationToken);
            break;
          case SqlEditorReportType.JsonContract:
            await File.WriteAllTextAsync(request.FilePath, BuildJson(context, request), Encoding.UTF8, cancellationToken);
            break;
          case SqlEditorReportType.CsvData:
            await File.WriteAllTextAsync(request.FilePath, BuildCsv(context), new UTF8Encoding(true), cancellationToken);
            break;
          case SqlEditorReportType.ExcelWorkbook:
            await File.WriteAllBytesAsync(request.FilePath, BuildExcelWorkbook(context), cancellationToken);
            break;
          default:
            throw new NotSupportedException($"Unsupported report type: {request.ReportType}");
        }

        return Path.GetFullPath(request.FilePath);
    }

    private static string BuildJson(SqlEditorReportExportContext context, SqlEditorReportExportRequest request)
    {
        object payload = BuildJsonPayload(context, request);
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static string BuildCsv(SqlEditorReportExportContext context)
    {
      IReadOnlyList<string> columns = GetDataColumns(context);
      var rows = new List<string>();

      if (columns.Count > 0)
        rows.Add(string.Join(',', columns.Select(EscapeCsvCell)));

      foreach (IReadOnlyDictionary<string, object?> row in context.ResultRows)
      {
        rows.Add(string.Join(',', columns.Select(column => EscapeCsvCell(row.TryGetValue(column, out object? value) ? value : null))));
      }

      return string.Join(Environment.NewLine, rows);
    }

    private static byte[] BuildExcelWorkbook(SqlEditorReportExportContext context)
    {
      IReadOnlyList<string> columns = GetDataColumns(context);

      using var workbook = new XLWorkbook();
      IXLWorksheet worksheet = workbook.Worksheets.Add("Results");

      int currentRow = 1;
      if (columns.Count > 0)
      {
        for (int columnIndex = 0; columnIndex < columns.Count; columnIndex += 1)
          worksheet.Cell(currentRow, columnIndex + 1).Value = columns[columnIndex];

        worksheet.Row(currentRow).Style.Font.Bold = true;
        currentRow += 1;
      }

      foreach (IReadOnlyDictionary<string, object?> row in context.ResultRows)
      {
        for (int columnIndex = 0; columnIndex < columns.Count; columnIndex += 1)
        {
          string column = columns[columnIndex];
          row.TryGetValue(column, out object? value);
          worksheet.Cell(currentRow, columnIndex + 1).SetValue(value?.ToString() ?? string.Empty);
        }

        currentRow += 1;
      }

      worksheet.Columns().AdjustToContents();

      using var stream = new MemoryStream();
      workbook.SaveAs(stream);
      return stream.ToArray();
    }

    private static IReadOnlyList<string> GetDataColumns(SqlEditorReportExportContext context)
    {
      if (context.SchemaColumns.Count > 0)
        return context.SchemaColumns;

      if (context.ResultRows.Count > 0)
        return [.. context.ResultRows[0].Keys];

      return [];
    }

    private static string EscapeCsvCell(object? value)
    {
      string text = value?.ToString() ?? string.Empty;
      if (text.IndexOfAny([',', '"', '\n', '\r']) < 0)
        return text;

      return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static string BuildHtml(SqlEditorReportExportContext context, SqlEditorReportExportRequest request)
    {
        string generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        string generatedAtIso = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
        object meta = BuildMeta(context, request, generatedAt, generatedAtIso);
        object result = BuildExecutionResultObject(context.ExecutionResult);
        bool includeGraphSections = request.IncludeMetadata && request.IncludeNodeDetails;
        object[]? nodes = includeGraphSections ? [] : null;
        object[]? connections = includeGraphSections ? [] : null;
        bool hasSql = !string.IsNullOrWhiteSpace(context.Sql);

        string metaJson = SerializeForInlineJs(meta);
        string resultJson = SerializeForInlineJs(result);
        string schemaJson = SerializeForInlineJs(request.IncludeSchema ? context.SchemaColumns : []);
        string resultRowsJson = SerializeForInlineJs(context.ResultRows);
        string nodesJson = nodes is null ? "null" : SerializeForInlineJs(nodes);
        string connectionsJson = connections is null ? "null" : SerializeForInlineJs(connections);
        string hasSqlJson = hasSql ? "true" : "false";
        string sqlEscaped = EscapeJsTemplateLiteral(context.Sql);

        string title = string.IsNullOrWhiteSpace(request.Title) ? context.TabTitle : request.Title;
        string description = string.IsNullOrWhiteSpace(request.Description)
          ? (request.UseDashForEmptyFields ? "-" : "null")
          : request.Description.Trim();

        return $$"""
<!DOCTYPE html>
<html lang="en" data-theme="dark">
<head>
<meta charset="UTF-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1.0"/>
<title>{{HtmlEncode(title)}} - DBWeaver Report</title>
<style>
* { box-sizing: border-box; margin: 0; padding: 0; }
:root[data-theme="dark"] {
  --bg: {{UiColorConstants.C_090B14.ToLowerInvariant()}};
  --bg-panel: {{UiColorConstants.C_10291A.ToLowerInvariant()}};
  --bg-panel-strong: {{UiColorConstants.C_12172A.ToLowerInvariant()}};
  --bg-surface: {{UiColorConstants.C_1E2A3F.ToLowerInvariant()}};
  --bg-surface-2: {{UiColorConstants.C_252C3F.ToLowerInvariant()}};
  --border: {{UiColorConstants.C_2A3554.ToLowerInvariant()}};
  --border-strong: {{UiColorConstants.C_334164.ToLowerInvariant()}};
  --text: {{UiColorConstants.C_E7ECFF.ToLowerInvariant()}};
  --text-muted: {{UiColorConstants.C_AEB9D9.ToLowerInvariant()}};
  --accent: {{UiColorConstants.C_5B7CFA.ToLowerInvariant()}};
  --accent-2: {{UiColorConstants.C_818CF8.ToLowerInvariant()}};
  --code-bg: {{UiColorConstants.C_0F1220.ToLowerInvariant()}};
  --shadow: 0 4px 24px rgba(0, 0, 0, 0.5);
}
:root[data-theme="light"] {
  --bg: {{UiColorConstants.C_F0F2FA.ToLowerInvariant()}};
  --bg-panel: {{UiColorConstants.C_FFFFFF.ToLowerInvariant()}};
  --bg-panel-strong: {{UiColorConstants.C_FFFFFF.ToLowerInvariant()}};
  --bg-surface: {{UiColorConstants.C_F5F6FB.ToLowerInvariant()}};
  --bg-surface-2: {{UiColorConstants.C_EAECF5.ToLowerInvariant()}};
  --border: {{UiColorConstants.C_D0D4E8.ToLowerInvariant()}};
  --border-strong: {{UiColorConstants.C_B8BDD6.ToLowerInvariant()}};
  --text: {{UiColorConstants.C_1A1D2E.ToLowerInvariant()}};
  --text-muted: {{UiColorConstants.C_4A4F6A.ToLowerInvariant()}};
  --accent: {{UiColorConstants.C_5B7CFA.ToLowerInvariant()}};
  --accent-2: {{UiColorConstants.C_818CF8.ToLowerInvariant()}};
  --code-bg: {{UiColorConstants.C_F7F8FD.ToLowerInvariant()}};
  --shadow: 0 4px 24px rgba(26, 29, 46, 0.1);
}
body {
  font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
  background:
    radial-gradient(circle at top left, rgba(124, 106, 247, 0.13), transparent 34%),
    radial-gradient(circle at 88% 12%, rgba(78, 205, 196, 0.1), transparent 30%),
    var(--bg);
  color: var(--text);
  min-height: 100vh;
  line-height: 1.5;
  letter-spacing: 0.01em;
}
body::before {
  content: '';
  position: fixed;
  inset: 0;
  pointer-events: none;
  background-image: linear-gradient(rgba(255,255,255,0.025) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.025) 1px, transparent 1px);
  background-size: 30px 30px;
  mask-image: linear-gradient(to bottom, rgba(0,0,0,0.28), transparent 88%);
}
header {
  position: sticky;
  top: 0;
  z-index: 2;
  min-height: 56px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 14px;
  padding: 10px 18px;
  border-bottom: 1px solid var(--border);
  background: linear-gradient(180deg, rgba(19, 22, 31, 0.96), rgba(19, 22, 31, 0.82));
  backdrop-filter: blur(16px);
  box-shadow: 0 2px 16px rgba(0, 0, 0, 0.35);
}
.logo {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--accent-2);
}
.logo::before {
  content: '';
  width: 10px;
  height: 10px;
  border-radius: 999px;
  background: linear-gradient(135deg, var(--accent), var(--accent-2));
  box-shadow: 0 0 0 5px rgba(124, 106, 247, 0.12);
}
.header-center { flex: 1; min-width: 0; }
.header-center h1 {
  font-size: 16px;
  font-weight: 800;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.description {
  color: var(--text-muted);
  font-size: 12px;
  margin-top: 3px;
}
.shortcut-hint {
  max-width: 1320px;
  margin: 10px auto 0;
  padding: 0 16px;
}
.shortcut-hint-inner {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  border-radius: 12px;
  border: 1px solid var(--border);
  background: linear-gradient(180deg, rgba(255,255,255,0.02), rgba(255,255,255,0.008));
  color: var(--text-muted);
  font-size: 12px;
}
.shortcut-title {
  color: var(--text);
  font-weight: 700;
  margin-right: 2px;
}
.shortcut-item {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.shortcut-hint kbd {
  font-family: 'JetBrains Mono', 'Cascadia Mono', 'SFMono-Regular', Consolas, monospace;
  font-size: 11px;
  border: 1px solid var(--border-strong);
  border-bottom-width: 2px;
  border-radius: 8px;
  padding: 2px 7px;
  color: var(--text);
  background: var(--bg-surface);
}
main {
  max-width: 1320px;
  margin: 0 auto;
  padding: 20px 16px 28px;
  display: grid;
  gap: 18px;
}
section {
  background: linear-gradient(180deg, var(--bg-panel), var(--bg-panel-strong));
  border: 1px solid var(--border-strong);
  border-radius: 16px;
  overflow: hidden;
  box-shadow: var(--shadow);
}
.section-header {
  padding: 14px 16px;
  border-bottom: 1px solid var(--border);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  background: linear-gradient(180deg, rgba(255,255,255,0.015), transparent);
}
.section-header h2 {
  font-size: 13px;
  text-transform: uppercase;
  letter-spacing: 0.12em;
  color: var(--text-muted);
  display: inline-flex;
  align-items: center;
  gap: 7px;
}
.section-icon {
  width: 16px;
  height: 16px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: var(--accent);
}
.section-icon svg {
  width: 14px;
  height: 14px;
  fill: currentColor;
}
.section-header .controls {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  align-items: center;
  gap: 8px;
}
.section-body {
  padding: 16px;
  max-height: 2200px;
  opacity: 1;
  overflow: hidden;
  transition: max-height 0.24s ease, opacity 0.18s ease, padding 0.18s ease;
}
.section.is-collapsed .section-body {
  max-height: 0;
  opacity: 0;
  padding-top: 0;
  padding-bottom: 0;
  pointer-events: none;
}
.meta-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 10px;
}
.meta-card {
  background: linear-gradient(180deg, var(--bg-surface), var(--bg-surface-2));
  border: 1px solid var(--border);
  border-radius: 12px;
  padding: 11px 12px;
}
.meta-card .label {
  font-size: 10px;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.12em;
}
.meta-card.full-width { grid-column: 1 / -1; }
.meta-card .value {
  font-size: 14px;
  margin-top: 5px;
  word-break: break-word;
}
.kpi-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 10px;
}
.kpi-card {
  background: linear-gradient(180deg, var(--bg-surface), var(--bg-surface-2));
  border: 1px solid var(--border);
  border-radius: 14px;
  padding: 14px;
}
.kpi-card .label {
  color: var(--text-muted);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.12em;
}
.kpi-card .value {
  font-size: 18px;
  font-weight: 800;
  margin-top: 6px;
}
.kpi-card .hint {
  color: var(--text-muted);
  font-size: 12px;
  margin-top: 2px;
}
.banner {
  border-radius: 14px;
  padding: 12px 14px;
  margin-top: 12px;
  border: 1px solid var(--border);
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.04);
}
.banner-ok { background: linear-gradient(135deg, rgba(14,159,110,0.18), rgba(14,159,110,0.08)); border-color: rgba(14,159,110,0.35); }
.banner-warn { background: linear-gradient(135deg, rgba(210,153,34,0.2), rgba(210,153,34,0.08)); border-color: rgba(210,153,34,0.35); }
.banner-err { background: linear-gradient(135deg, rgba(248,81,73,0.22), rgba(248,81,73,0.08)); border-color: rgba(248,81,73,0.38); }
.sql-block {
  background: var(--code-bg);
  border: 1px solid var(--border-strong);
  border-radius: 14px;
  padding: 16px;
  overflow-x: auto;
}
pre {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-word;
  font-family: 'JetBrains Mono', 'Cascadia Mono', 'SFMono-Regular', Consolas, monospace;
  font-size: 12px;
  line-height: 1.7;
}
table { width: 100%; border-collapse: collapse; }
th, td {
  padding: 9px 10px;
  border-bottom: 1px solid var(--border);
  text-align: left;
  vertical-align: top;
}
th {
  color: var(--text-muted);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.12em;
  cursor: pointer;
  user-select: none;
}
th.sorted-asc::after { content: ' ↑'; color: var(--accent); }
th.sorted-desc::after { content: ' ↓'; color: var(--accent); }
.badge {
  border-radius: 999px;
  padding: 3px 10px;
  font-size: 10px;
  border: 1px solid var(--border);
  color: var(--text-muted);
  background: rgba(255,255,255,0.03);
  text-transform: uppercase;
  letter-spacing: 0.08em;
}
.tbl-wrap {
  overflow-x: auto;
  border: 1px solid var(--border-strong);
  border-radius: 14px;
  background: rgba(0,0,0,0.08);
}
.tbl-wrap table thead th {
  position: sticky;
  top: 0;
  background: linear-gradient(180deg, rgba(255,255,255,0.04), rgba(255,255,255,0.02));
  backdrop-filter: blur(8px);
}
.tbl-wrap tbody tr:nth-child(even) td {
  background: rgba(255,255,255,0.02);
}
.status-success { color: {{UiColorConstants.C_2FBF84.ToLowerInvariant()}}; }
.status-warning { color: {{UiColorConstants.C_D9A441.ToLowerInvariant()}}; }
.status-error { color: {{UiColorConstants.C_E16174.ToLowerInvariant()}}; }
.type-null { color: var(--text-muted); font-style: italic; }
.type-text { color: var(--text); }
.type-number { color: var(--accent-2); }
.type-date { color: {{UiColorConstants.C_60A5FA.ToLowerInvariant()}}; }
.type-bool { color: {{UiColorConstants.C_D9A441.ToLowerInvariant()}}; }
.type-success { color: {{UiColorConstants.C_2FBF84.ToLowerInvariant()}}; }
.type-warning { color: {{UiColorConstants.C_D9A441.ToLowerInvariant()}}; }
.type-error { color: {{UiColorConstants.C_E16174.ToLowerInvariant()}}; }
.collapse-btn {
  font-size: 11px;
  border-radius: 999px;
  padding: 6px 12px;
  min-width: 90px;
  transition: transform 0.14s ease, border-color 0.14s ease;
}
.collapse-btn:hover {
  transform: translateY(-1px);
}
.collapse-btn.is-collapsed {
  border-color: var(--border-strong);
}
.filter-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 10px;
}
.tool-groups {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 8px;
}
.tool-toggle {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 10px;
  border-radius: 999px;
}
.btn-icon {
  width: 14px;
  height: 14px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
.btn-icon svg {
  width: 12px;
  height: 12px;
  fill: currentColor;
}
.subpanel {
  border: 1px solid var(--border-strong);
  border-radius: 12px;
  padding: 10px;
  margin-bottom: 10px;
  background: linear-gradient(180deg, rgba(255,255,255,0.02), rgba(255,255,255,0.005));
}
.subpanel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}
.subpanel-title {
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.11em;
  color: var(--text-muted);
}
.subpanel-body {
  margin-top: 8px;
}
.subpanel.is-collapsed .subpanel-body {
  display: none;
}
.filter-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 12px;
}
.acc-empty {
  color: var(--text-muted);
  font-size: 12px;
}
.filter-chip {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 6px 10px;
  border-radius: 999px;
  background: rgba(83, 200, 255, 0.08);
  border: 1px solid rgba(83, 200, 255, 0.18);
}
.chip-col {
  color: var(--accent);
  font-weight: 700;
  font-family: 'Cascadia Mono', 'SFMono-Regular', Consolas, monospace;
}
.chip-op,
.chip-val {
  color: var(--text-muted);
  font-size: 12px;
}
.chip-rm {
  background: transparent;
  border: none;
  color: var(--text-muted);
  cursor: pointer;
  padding: 0;
  font-size: 14px;
  line-height: 1;
}
.chip-rm:hover {
  color: var(--text);
}
.table-input,
.table-select {
  background: var(--bg-surface);
  border: 1px solid var(--border);
  color: var(--text);
  border-radius: 10px;
  padding: 7px 10px;
  min-width: 160px;
}
.pager {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  border-top: 1px solid var(--border);
  margin-top: 10px;
  padding-top: 10px;
}
.pager-info {
  color: var(--text-muted);
  font-size: 12px;
}
.pager-btns {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}
footer {
  text-align: center;
  color: var(--text-muted);
  font-size: 12px;
  padding: 18px 12px 24px;
}
button {
  background: linear-gradient(180deg, var(--bg-surface), var(--bg-surface-2));
  border: 1px solid var(--border);
  color: var(--text);
  border-radius: 10px;
  padding: 6px 12px;
  cursor: pointer;
  transition: transform 0.12s ease, border-color 0.12s ease, background 0.12s ease;
}
button:hover {
  border-color: var(--border-strong);
  transform: translateY(-1px);
}
button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}
@media (max-width: 900px) {
  .section-header {
    align-items: flex-start;
    flex-direction: column;
  }

  .section-header .controls {
    width: 100%;
    justify-content: flex-start;
  }

  .table-input,
  .table-select {
    min-width: 140px;
    flex: 1 1 140px;
  }
}
</style>
</head>
<body>
<header>
  <span class="logo">DBWeaver</span>
  <div class="header-center">
    <h1>{{HtmlEncode(title)}}</h1>
    <p class="description">{{HtmlEncode(description)}}</p>
  </div>
  <button id="btn-theme" type="button">Toggle theme</button>
</header>
<div class="shortcut-hint" aria-label="Keyboard shortcuts">
  <div class="shortcut-hint-inner">
    <span class="shortcut-title">Shortcuts</span>
    <span class="shortcut-item"><kbd>/</kbd> Focus active search</span>
    <span class="shortcut-item"><kbd>Alt</kbd>+<kbd>R</kbd> Results filter</span>
    <span class="shortcut-item"><kbd>Alt</kbd>+<kbd>M</kbd> Metadata filter</span>
    <span class="shortcut-item"><kbd>Alt</kbd>+<kbd>N</kbd> Nodes filter</span>
    <span class="shortcut-item"><kbd>Alt</kbd>+<kbd>C</kbd> Connections filter</span>
  </div>
</div>
<main>
  <section id="s-summary">
    <div class="section-header"><h2>Execution Summary</h2></div>
    <div class="section-body" id="summary-body"></div>
  </section>

  <section id="s-meta">
    <div class="section-header">
      <h2><span class="section-icon" aria-hidden="true"><svg viewBox="0 0 16 16" focusable="false"><path d="M8 1a7 7 0 1 0 0 14A7 7 0 0 0 8 1Zm0 3.2a1 1 0 1 1 0 2 1 1 0 0 1 0-2Zm-1.1 3h2.1v4.5H6.9V7.2Z"/></svg></span>Optional Metadata</h2>
      <div class="controls">
        <input id="metadata-search" class="table-input" type="search" placeholder="Filter metadata"/>
        <select id="metadata-page-size" class="table-select">
          <option value="10">10</option>
          <option value="25">25</option>
          <option value="50">50</option>
        </select>
        <button class="collapse-btn" data-collapse-target="s-meta" type="button">▾ Collapse</button>
      </div>
    </div>
    <div class="section-body">
      {{SqlEditorReportHtmlSections.BuildFilterAndOrderPanels("metadata")}}
      <div id="metadata-table"></div>
    </div>
  </section>

  <section id="s-sql">
    <div class="section-header">
      <h2>SQL Query</h2>
      <div class="controls">
        <span class="badge">Primary Output</span>
        <button id="btn-copy-sql" class="collapse-btn" type="button">Copy SQL</button>
        <button class="collapse-btn" data-collapse-target="s-sql" type="button">▾ Collapse</button>
      </div>
    </div>
    <div class="section-body">
      <div class="sql-block"><pre id="sql-pre"></pre></div>
    </div>
  </section>

  <section id="s-quality">
    <div class="section-header"><h2>Validation Summary</h2></div>
    <div class="section-body" id="quality-body"></div>
  </section>

  {{SqlEditorReportHtmlSections.BuildResultsSection()}}
  <section id="s-nodes">
    <div class="section-header">
      <h2><span class="section-icon" aria-hidden="true"><svg viewBox="0 0 16 16" focusable="false"><path d="M3 2h4v4H3V2Zm6 0h4v4H9V2ZM3 8h4v4H3V8Zm6 0h4v4H9V8Z"/></svg></span>Node Inventory</h2>
      <div class="controls">
        <input id="nodes-search" class="table-input" type="search" placeholder="Filter nodes"/>
        <select id="nodes-page-size" class="table-select">
          <option value="10">10</option>
          <option value="25">25</option>
          <option value="50">50</option>
        </select>
        <button class="collapse-btn" data-collapse-target="s-nodes" type="button">▾ Collapse</button>
      </div>
    </div>
    <div class="section-body">
      {{SqlEditorReportHtmlSections.BuildFilterAndOrderPanels("nodes")}}
      <div id="nodes-body"></div>
    </div>
  </section>

  <section id="s-conns" class="is-collapsed">
    <div class="section-header">
      <h2><span class="section-icon" aria-hidden="true"><svg viewBox="0 0 16 16" focusable="false"><path d="M3 3h4v3H3V3Zm6 0h4v3H9V3ZM6.5 7h3v2h-3V7Zm-3 3h4v3H3v-3Zm6 0h4v3H9v-3Z"/></svg></span>Connection Map</h2>
      <div class="controls">
        <input id="conns-search" class="table-input" type="search" placeholder="Filter connections"/>
        <select id="conns-page-size" class="table-select">
          <option value="10">10</option>
          <option value="25">25</option>
          <option value="50">50</option>
        </select>
        <button class="collapse-btn" data-collapse-target="s-conns" type="button">▸ Expand</button>
      </div>
    </div>
    <div class="section-body">
      {{SqlEditorReportHtmlSections.BuildFilterAndOrderPanels("conns")}}
      <div id="conns-body"></div>
    </div>
  </section>
</main>
<footer id="footer-text"></footer>

<script>
const REPORT_VERSION = "2.2";
const REPORT_META = {{metaJson}};
const EXECUTION_RESULT = {{resultJson}};
const SCHEMA_COLS = {{schemaJson}};
const RESULT_ROWS = {{resultRowsJson}};
const HAS_SQL = {{hasSqlJson}};
const SQL_TEXT = `{{sqlEscaped}}`;
const NODE_ROWS = {{nodesJson}};
const CONN_ROWS = {{connectionsJson}};
</script>
<script>
(function() {
  'use strict';

  var COLLAPSE_STATE_KEY = 'dbw-report-collapse-v1';

  function escHtml(s) {
    return String(s)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  function statusClass(status) {
    if (status === 'error') return 'status-error';
    if (status === 'warning') return 'status-warning';
    return 'status-success';
  }

  var tableState = {
    results: { page: 1, pageSize: 10, sortKey: '', sortDir: 'asc', filter: '', filters: [] },
    nodes: { page: 1, pageSize: 10, sortKey: 'category', sortDir: 'asc', filter: '', filters: [] },
    conns: { page: 1, pageSize: 10, sortKey: 'fromNode', sortDir: 'asc', filter: '', filters: [] },
    metadata: { page: 1, pageSize: 10, sortKey: 'field', sortDir: 'asc', filter: '', filters: [] }
  };

  function textValue(value) {
    if (value == null) {
      return '';
    }

    return String(value);
  }

  function compareValues(a, b) {
    var aText = textValue(a).toLowerCase();
    var bText = textValue(b).toLowerCase();

    if (aText < bText) {
      return -1;
    }

    if (aText > bText) {
      return 1;
    }

    return 0;
  }

  function parseDateKey(value) {
    if (value === null || value === undefined || value === '') {
      return null;
    }

    var date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return null;
    }

    return [
      String(date.getFullYear()).padStart(4, '0'),
      String(date.getMonth() + 1).padStart(2, '0'),
      String(date.getDate()).padStart(2, '0')
    ].join('-');
  }

  function rowKindForColumn(row, columns, key) {
    if (key === 'value' && row && row.kind) {
      return row.kind;
    }

    for (var i = 0; i < columns.length; i += 1) {
      if (columns[i].key === key && columns[i].kind) {
        return columns[i].kind;
      }
    }

    var sample = row ? row[key] : null;
    return valueKind(sample);
  }

  function matchFilterValue(left, op, right, kind) {
    if (kind === 'date') {
      var leftDate = parseDateKey(left);
      var rightDate = parseDateKey(right);

      if (leftDate === null || rightDate === null) {
        return op === '!=' ? leftDate !== rightDate : false;
      }

      if (op === '=') return leftDate === rightDate;
      if (op === '!=') return leftDate !== rightDate;
      if (op === '>') return leftDate > rightDate;
      if (op === '<') return leftDate < rightDate;
      if (op === '>=') return leftDate >= rightDate;
      if (op === '<=') return leftDate <= rightDate;
      return leftDate.indexOf(rightDate) >= 0;
    }

    if (kind === 'number') {
      var leftNumber = Number(left);
      var rightNumber = Number(right);
      if (Number.isNaN(leftNumber) || Number.isNaN(rightNumber)) {
        return op === '!=' ? String(left) !== String(right) : false;
      }

      if (op === '=') return leftNumber === rightNumber;
      if (op === '!=') return leftNumber !== rightNumber;
      if (op === '>') return leftNumber > rightNumber;
      if (op === '<') return leftNumber < rightNumber;
      if (op === '>=') return leftNumber >= rightNumber;
      if (op === '<=') return leftNumber <= rightNumber;
      return String(leftNumber).indexOf(String(rightNumber)) >= 0;
    }

    if (kind === 'bool') {
      var leftBool = String(left).toLowerCase() === 'true';
      var rightBool = String(right).toLowerCase() === 'true';
      if (op === '=') return leftBool === rightBool;
      if (op === '!=') return leftBool !== rightBool;
      return leftBool === rightBool;
    }

    var leftText = textValue(left).toLowerCase();
    var rightText = textValue(right).toLowerCase();

    if (op === '=') return leftText === rightText;
    if (op === '!=') return leftText !== rightText;
    if (op === 'contains') return leftText.indexOf(rightText) >= 0;
    if (op === '>') return leftText > rightText;
    if (op === '<') return leftText < rightText;
    if (op === '>=') return leftText >= rightText;
    if (op === '<=') return leftText <= rightText;
    return leftText.indexOf(rightText) >= 0;
  }

  function matchRowFilter(row, filter, columns) {
    var kind = rowKindForColumn(row, columns, filter.col);
    return matchFilterValue(row[filter.col], filter.op, filter.val, kind);
  }

  function applyFilterSortPage(rows, state, searchableKeys) {
    var filtered = rows;
    if (state.filters && state.filters.length > 0) {
      filtered = rows.filter(function(row) {
        return state.filters.every(function(filter) {
          return matchRowFilter(row, filter, state.columns || []);
        });
      });
    }

    if (state.filter) {
      var needle = state.filter.toLowerCase();
      filtered = filtered.filter(function(row) {
        return searchableKeys.some(function(key) {
          return textValue(row[key]).toLowerCase().indexOf(needle) >= 0;
        });
      });
    }

    filtered = filtered.slice().sort(function(left, right) {
      var cmp = compareValues(left[state.sortKey], right[state.sortKey]);
      return state.sortDir === 'asc' ? cmp : -cmp;
    });

    var total = filtered.length;
    var pageCount = Math.max(1, Math.ceil(total / state.pageSize));
    state.page = Math.min(state.page, pageCount);
    var startIndex = (state.page - 1) * state.pageSize;
    var paged = filtered.slice(startIndex, startIndex + state.pageSize);

    return {
      rows: paged,
      total: total,
      pageCount: pageCount,
      start: total === 0 ? 0 : startIndex + 1,
      end: total === 0 ? 0 : Math.min(startIndex + state.pageSize, total)
    };
  }

  function renderTable(containerId, columns, rawRows, stateKey, searchableKeys) {
    var container = document.getElementById(containerId);
    var state = tableState[stateKey];
    state.columns = columns;
    var result = applyFilterSortPage(rawRows, state, searchableKeys);

    if (result.total === 0) {
      container.innerHTML = '<div class="tbl-wrap"><table><thead><tr>'
        + columns.map(function(col) { return '<th data-key="' + escHtml(col.key) + '">' + escHtml(col.label) + '</th>'; }).join('')
        + '</tr></thead><tbody><tr><td colspan="' + columns.length + '">No rows available.</td></tr></tbody></table></div>';
      return;
    }

    var header = columns.map(function(col) {
      var css = '';
      if (state.sortKey === col.key) {
        css = state.sortDir === 'asc' ? 'sorted-asc' : 'sorted-desc';
      }

      return '<th class="' + css + '" data-key="' + escHtml(col.key) + '">' + escHtml(col.label) + '</th>';
    }).join('');

    var rowsHtml = result.rows.map(function(row) {
      return '<tr>' + columns.map(function(col) {
        var cellValue = row[col.key];
        var kind = rowKindForColumn(row, columns, col.key);
        return '<td class="' + valueClass(kind, cellValue) + '">' + escHtml(formatEmpty(cellValue)) + '</td>';
      }).join('') + '</tr>';
    }).join('');

    var prevDisabled = state.page <= 1 ? 'disabled' : '';
    var nextDisabled = state.page >= result.pageCount ? 'disabled' : '';
    var pageButtonsHtml = buildPageButtons(state.page, result.pageCount).map(function(entry) {
      if (entry == null) {
        return '<span>...</span>';
      }

      var isActive = entry === state.page ? 'active' : '';
      return '<button type="button" class="' + isActive + '" data-page-num="' + entry + '" data-table="' + stateKey + '">' + entry + '</button>';
    }).join('');

    container.innerHTML = '<div class="tbl-wrap"><table><thead><tr>' + header + '</tr></thead><tbody>' + rowsHtml + '</tbody></table></div>'
      + '<div class="pager"><div class="pager-info">Showing ' + result.start + '-' + result.end + ' of ' + result.total + '</div>'
      + '<div class="pager-btns"><button type="button" data-page-action="prev" data-table="' + stateKey + '" ' + prevDisabled + '>Prev</button>'
      + pageButtonsHtml
      + '<button type="button" data-page-action="next" data-table="' + stateKey + '" ' + nextDisabled + '>Next</button></div></div>';

    container.querySelectorAll('th[data-key]').forEach(function(th) {
      th.addEventListener('click', function() {
        var key = th.getAttribute('data-key');
        if (state.sortKey === key) {
          state.sortDir = state.sortDir === 'asc' ? 'desc' : 'asc';
        } else {
          state.sortKey = key;
          state.sortDir = 'asc';
        }

        state.page = 1;
        rerenderTable(stateKey);
      });
    });

    container.querySelectorAll('button[data-page-action]').forEach(function(button) {
      button.addEventListener('click', function() {
        if (button.hasAttribute('disabled')) {
          return;
        }

        var action = button.getAttribute('data-page-action');
        if (action === 'prev' && state.page > 1) {
          state.page -= 1;
          rerenderTable(stateKey);
        }

        if (action === 'next' && state.page < result.pageCount) {
          state.page += 1;
          rerenderTable(stateKey);
        }
      });
    });

    container.querySelectorAll('button[data-page-num]').forEach(function(button) {
      button.addEventListener('click', function() {
        var parsedPage = parseInt(button.getAttribute('data-page-num') || '', 10);
        if (!Number.isFinite(parsedPage) || parsedPage < 1 || parsedPage > result.pageCount) {
          return;
        }

        if (parsedPage === state.page) {
          return;
        }

        state.page = parsedPage;
        rerenderTable(stateKey);
      });
    });
  }

  function tableFilterFields(stateKey) {
    if (stateKey === 'results') {
      var cols = tableState.results.columns || [];
      return cols.map(function(col) {
        return { key: col.key, label: col.label };
      });
    }

    if (stateKey === 'nodes') {
      return [
        { key: 'category', label: 'Category' },
        { key: 'type', label: 'Type' },
        { key: 'title', label: 'Title' },
        { key: 'status', label: 'Status' }
      ];
    }

    if (stateKey === 'conns') {
      return [
        { key: 'fromNode', label: 'From Node' },
        { key: 'fromPin', label: 'From Pin' },
        { key: 'toNode', label: 'To Node' },
        { key: 'toPin', label: 'To Pin' },
        { key: 'dataType', label: 'Data Type' }
      ];
    }

    if (stateKey === 'metadata') {
      return [
        { key: 'field', label: 'Field' },
        { key: 'label', label: 'Label' },
        { key: 'value', label: 'Value' },
        { key: 'kind', label: 'Type' }
      ];
    }

    return [];
  }

  function filterFieldLabel(stateKey, key) {
    var fields = tableFilterFields(stateKey);
    for (var i = 0; i < fields.length; i += 1) {
      if (fields[i].key === key) {
        return fields[i].label;
      }
    }

    return key;
  }

  function setupFilterFieldSelect(stateKey, fieldSelect) {
    if (!fieldSelect) {
      return;
    }

    fieldSelect.innerHTML = '';
    tableFilterFields(stateKey).forEach(function(field) {
      fieldSelect.add(new Option(field.label, field.key));
    });
  }

  function renderFilterChips(stateKey, listId) {
    var list = document.getElementById(listId);
    if (!list) {
      return;
    }

    var state = tableState[stateKey];
    list.innerHTML = '';
    if (!state.filters.length) {
      list.innerHTML = '<div class="acc-empty">No filters active.</div>';
      return;
    }

    state.filters.forEach(function(filter, index) {
      var kind = rowKindForColumn(null, state.columns || [], filter.col);
      var chip = document.createElement('div');
      chip.className = 'filter-chip';
      chip.innerHTML =
        '<span class="chip-col">' + escHtml(filterFieldLabel(stateKey, filter.col)) + '</span>' +
        '<span class="chip-op">' + escHtml(filterOpLabel(filter.op)) + '</span>' +
        '<span class="chip-val">' + escHtml(formatFilterValue(filter.val, kind)) + '</span>' +
        '<button class="chip-rm" type="button" data-filter-index="' + index + '" title="Remove">×</button>';
      list.appendChild(chip);
    });

    list.querySelectorAll('button[data-filter-index]').forEach(function(button) {
      button.addEventListener('click', function() {
        var idx = parseInt(button.getAttribute('data-filter-index') || '', 10);
        if (!Number.isFinite(idx)) {
          return;
        }

        state.filters.splice(idx, 1);
        renderFilterChips(stateKey, listId);
        rerenderTable(stateKey);
      });
    });
  }

  function initFilterControls(stateKey, config) {
    var state = tableState[stateKey];
    var fieldSelect = document.getElementById(config.fieldSelectId);
    var opSelect = document.getElementById(config.operatorSelectId);
    var valueInput = document.getElementById(config.valueInputId);
    var addButton = document.getElementById(config.addButtonId);
    var clearButton = document.getElementById(config.clearButtonId);

    if (!fieldSelect || !opSelect || !valueInput || !addButton || !clearButton) {
      return;
    }

    setupFilterFieldSelect(stateKey, fieldSelect);

    function updateInputType() {
      var kind = rowKindForColumn(null, state.columns || [], fieldSelect.value);
      valueInput.type = kind === 'date' ? 'date' : 'text';
      valueInput.placeholder = kind === 'date' ? 'Select a date' : 'Filter value';
    }

    fieldSelect.addEventListener('change', updateInputType);

    addButton.addEventListener('click', function() {
      var rawValue = valueInput.value;
      if (!rawValue) {
        return;
      }

      var kind = rowKindForColumn(null, state.columns || [], fieldSelect.value);
      state.filters.push({
        col: fieldSelect.value,
        op: opSelect.value,
        val: kind === 'date' ? rawValue.slice(0, 10) : rawValue
      });

      valueInput.value = '';
      state.page = 1;
      renderFilterChips(stateKey, config.filterListId);
      rerenderTable(stateKey);
    });

    clearButton.addEventListener('click', function() {
      state.filters = [];
      state.page = 1;
      renderFilterChips(stateKey, config.filterListId);
      rerenderTable(stateKey);
    });

    valueInput.addEventListener('keydown', function(e) {
      if (e.key === 'Enter') {
        e.preventDefault();
        addButton.click();
      }
    });

    updateInputType();
    renderFilterChips(stateKey, config.filterListId);
  }

  function buildPageButtons(currentPage, pageCount) {
    if (pageCount <= 7) {
      var allPages = [];
      for (var i = 1; i <= pageCount; i += 1) {
        allPages.push(i);
      }

      return allPages;
    }

    var pages = [1];
    var start = Math.max(2, currentPage - 1);
    var end = Math.min(pageCount - 1, currentPage + 1);

    if (currentPage <= 3) {
      start = 2;
      end = 4;
    }

    if (currentPage >= pageCount - 2) {
      start = pageCount - 3;
      end = pageCount - 1;
    }

    if (start > 2) {
      pages.push(null);
    }

    for (var page = start; page <= end; page += 1) {
      pages.push(page);
    }

    if (end < pageCount - 1) {
      pages.push(null);
    }

    pages.push(pageCount);
    return pages;
  }

  function debounce(action, waitMs) {
    var timeoutId = null;

    return function(value) {
      if (timeoutId !== null) {
        clearTimeout(timeoutId);
      }

      timeoutId = setTimeout(function() {
        action(value);
      }, waitMs);
    };
  }

  function rerenderTable(stateKey) {
    if (stateKey === 'results') {
      renderResults();
      return;
    }

    if (stateKey === 'nodes') {
      renderNodes();
      return;
    }

    if (stateKey === 'conns') {
      renderConnections();
      return;
    }

    if (stateKey === 'metadata') {
      renderMetadata();
    }
  }

  function formatEmpty(value) {
    if (value === null || value === undefined || value === '') {
      return REPORT_META.useDashForEmptyFields ? '-' : 'null';
    }

    return String(value);
  }

  function valueKind(value) {
    if (value === null || value === undefined || value === '') {
      return 'null';
    }

    if (typeof value === 'boolean') {
      return 'bool';
    }

    if (typeof value === 'number') {
      return 'number';
    }

    if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}(?:[T\s].*)?$/.test(value)) {
      return 'date';
    }

    return 'text';
  }

  function valueClass(kind, value) {
    if (kind === 'null') return 'type-null';
    if (kind === 'bool') return value ? 'type-success' : 'type-error';
    if (kind === 'number') return 'type-number';
    if (kind === 'date') return 'type-date';
    return 'type-text';
  }

  function renderSummary() {
    var body = document.getElementById('summary-body');
    var summary = REPORT_META.summary || {};
    var cards = [
      { label: 'Status', value: summary.status || 'success', hint: 'Execution outcome', className: statusClass(summary.status || 'success') },
      { label: 'Execution Time', value: summary.executionTimeMs == null ? null : (String(summary.executionTimeMs) + ' ms'), hint: 'Measured duration', className: 'type-number' },
      { label: 'Rows', value: summary.rowCount, hint: 'Returned rows', className: 'type-number' },
      { label: 'Columns', value: REPORT_META.columnCount, hint: 'Detected output columns', className: 'type-number' },
    ];

    var html = '<div class="kpi-grid">' + cards.map(function(card) {
      var className = card.className ? ' ' + card.className : '';
      return '<div class="kpi-card">'
        + '<div class="label">' + escHtml(card.label) + '</div>'
        + '<div class="value' + className + '">' + escHtml(formatEmpty(card.value)) + '</div>'
        + '<div class="hint">' + escHtml(card.hint) + '</div>'
        + '</div>';
    }).join('') + '</div>';

    if (summary.errorMessage) {
      html += '<div class="banner banner-err"><strong>Error:</strong> ' + escHtml(summary.errorMessage) + '</div>';
    }

    body.innerHTML = html;
  }

  function renderMetadataRows() {
        return REPORT_META.metadata || [];
      }

      function filterOpLabel(op) {
        var labels = {
          '=': '=',
          '!=': '≠',
          'contains': '⊃',
          '>': '>',
          '<': '<',
          '>=': '≥',
          '<=': '≤'
        };

        return labels[op] || op;
      }

      function formatFilterValue(value, kind) {
        if (kind === 'date') {
          return value ? String(value).slice(0, 10) : formatEmpty(value);
        }

        return formatEmpty(value);
      }

      function renderMetadataFilters() {
        var list = document.getElementById('metadata-filter-list');
        var state = tableState.metadata;

        list.innerHTML = '';
        if (!state.filters.length) {
          list.innerHTML = '<div class="acc-empty">No metadata filters active.</div>';
          return;
        }

        state.filters.forEach(function(filter, index) {
          var kind = metadataValueKindForField(filter.col);
          var chip = document.createElement('div');
          chip.className = 'filter-chip';
          chip.innerHTML =
            '<span class="chip-col">' + escHtml(filterFieldLabel('metadata', filter.col)) + '</span>' +
            '<span class="chip-op">' + escHtml(filterOpLabel(filter.op)) + '</span>' +
            '<span class="chip-val">' + escHtml(formatFilterValue(filter.val, kind)) + '</span>' +
            '<button class="chip-rm" type="button" data-metadata-filter-index="' + index + '" title="Remove">×</button>';
          list.appendChild(chip);
        });

        list.querySelectorAll('button[data-metadata-filter-index]').forEach(function(button) {
          button.addEventListener('click', function() {
            var index = parseInt(button.getAttribute('data-metadata-filter-index') || '', 10);
            if (!Number.isFinite(index)) {
              return;
            }

            removeMetadataFilter(index);
          });
        });
      }

      function renderMetadata() {
        var section = document.getElementById('s-meta');
        var rows = renderMetadataRows();

        if (!REPORT_META.includeMetadata) {
          section.style.display = 'none';
          return;
        }

        section.style.display = '';

        renderTable(
          'metadata-table',
          [
            { key: 'field', label: 'Field', kind: 'text' },
            { key: 'label', label: 'Label', kind: 'text' },
            { key: 'value', label: 'Value' },
            { key: 'kind', label: 'Type', kind: 'text' }
          ],
          rows,
          'metadata',
          ['field', 'label', 'value', 'kind']);

        renderMetadataFilters();
      }

      function metadataFilterFields() {
        return tableFilterFields('metadata');
      }

      function metadataValueKindForField(field) {
        if (field === 'value' && renderMetadataRows().some(function(row) { return row.kind === 'date'; })) {
          return 'date';
        }

        return 'text';
      }

      function updateMetadataFilterInput() {
        var fieldSelect = document.getElementById('metadata-filter-col');
        var valueInput = document.getElementById('metadata-filter-val');
        if (!fieldSelect || !valueInput) {
          return;
        }

        var kind = metadataValueKindForField(fieldSelect.value);
        valueInput.type = kind === 'date' ? 'date' : 'text';
        valueInput.placeholder = kind === 'date' ? 'Select a date' : 'Filter value';
      }

      function initMetadataFilterControls() {
        var fieldSelect = document.getElementById('metadata-filter-col');
        var opSelect = document.getElementById('metadata-filter-op');
        var valueInput = document.getElementById('metadata-filter-val');
        var addButton = document.getElementById('metadata-add-filter');
        var clearButton = document.getElementById('metadata-clear-filters');

        if (!fieldSelect || !opSelect || !valueInput || !addButton || !clearButton) {
          return;
        }

        fieldSelect.innerHTML = '';
        metadataFilterFields().forEach(function(field) {
          fieldSelect.add(new Option(field.label, field.key));
        });

        fieldSelect.addEventListener('change', updateMetadataFilterInput);

        addButton.addEventListener('click', function() {
          var state = tableState.metadata;
          var rawValue = valueInput.value;
          var kind = metadataValueKindForField(fieldSelect.value);
          if (!rawValue) {
            return;
          }

          state.filters.push({
            col: fieldSelect.value,
            op: opSelect.value,
            val: kind === 'date' ? rawValue.slice(0, 10) : rawValue
          });

          valueInput.value = '';
          renderMetadataFilters();
          rerenderTable('metadata');
        });

        clearButton.addEventListener('click', function() {
          var state = tableState.metadata;
          state.filters = [];
          renderMetadataFilters();
          rerenderTable('metadata');
        });

        valueInput.addEventListener('keydown', function(e) {
          if (e.key === 'Enter') {
            e.preventDefault();
            addButton.click();
          }
        });

        updateMetadataFilterInput();
      }

      function removeMetadataFilter(index) {
        var state = tableState.metadata;
        state.filters.splice(index, 1);
        renderMetadataFilters();
        rerenderTable('metadata');
      }

  function renderSql() {
    var sqlPre = document.getElementById('sql-pre');
    sqlPre.textContent = HAS_SQL ? SQL_TEXT : '-- SQL not available.';
  }

  function renderQuality() {
    var body = document.getElementById('quality-body');
    var status = EXECUTION_RESULT.status || 'success';
    var cssClass = status === 'error' ? 'banner-err' : (status === 'warning' ? 'banner-warn' : 'banner-ok');
    var summary = status === 'error'
      ? 'Generated with errors'
      : (status === 'warning' ? 'Generated with warnings' : 'Query generated successfully');

      var lines = [
        '<div class="banner ' + cssClass + '"><strong>' + escHtml(summary) + '</strong></div>',
        '<div class="meta-grid" style="margin-top:12px">',
        '<div class="meta-card"><div class="label">Status</div><div class="value ' + statusClass(status) + '">' + escHtml(status) + '</div></div>',
        '<div class="meta-card"><div class="label">Error Count</div><div class="value">' + escHtml(String(REPORT_META.errorCount || 0)) + '</div></div>',
        '<div class="meta-card"><div class="label">Warning Count</div><div class="value">' + escHtml(String(REPORT_META.warningCount || 0)) + '</div></div>',
        '</div>'
      ];

      if (EXECUTION_RESULT.errorMessage) {
        lines.push('<div class="banner banner-err"><strong>Error Message:</strong> ' + escHtml(EXECUTION_RESULT.errorMessage) + '</div>');
      }

      body.innerHTML = lines.join('');
  }

  function renderResults() {
    var section = document.getElementById('s-results');
    var body = document.getElementById('results-body');
    if (!body || !section) {
      return;
    }

    if (!RESULT_ROWS || !Array.isArray(RESULT_ROWS) || RESULT_ROWS.length === 0) {
      body.innerHTML = '<span>No query rows available.</span>';
      return;
    }

    var columns = [];
    if (RESULT_ROWS.length > 0) {
      Object.keys(RESULT_ROWS[0]).forEach(function(key) {
        columns.push({
          key: key,
          label: String(key).replace(/_/g, ' ')
        });
      });
    }

    if (columns.length === 0) {
      body.innerHTML = '<span>No query rows available.</span>';
      return;
    }

    var state = tableState.results;
    if (!state.sortKey) {
      state.sortKey = columns[0].key;
    }

    state.columns = columns;

    renderTable(
      'results-body',
      columns,
      RESULT_ROWS,
      'results',
      columns.map(function(col) { return col.key; }));

    renderFilterChips('results', 'results-filter-list');
  }

  function renderNodes() {
    var section = document.getElementById('s-nodes');
    var body = document.getElementById('nodes-body');
    if (!section || !body) {
      return;
    }

    if (!REPORT_META.includeMetadata) {
      section.style.display = 'none';
      return;
    }

    section.style.display = '';

    if (!NODE_ROWS || !Array.isArray(NODE_ROWS) || NODE_ROWS.length === 0) {
      body.innerHTML = '<span>No node details available for SQL editor exports.</span>';
      return;
    }

    renderTable(
      'nodes-body',
      [
        { key: 'category', label: 'Category' },
        { key: 'type', label: 'Type' },
        { key: 'title', label: 'Title' },
        { key: 'status', label: 'Status' }
      ],
      NODE_ROWS,
      'nodes',
      ['category', 'type', 'title', 'status']);

    renderFilterChips('nodes', 'nodes-filter-list');
  }

  function renderConnections() {
    var section = document.getElementById('s-conns');
    var body = document.getElementById('conns-body');
    if (!section || !body) {
      return;
    }

    if (!REPORT_META.includeMetadata) {
      section.style.display = 'none';
      return;
    }

    section.style.display = '';

    if (!CONN_ROWS || !Array.isArray(CONN_ROWS) || CONN_ROWS.length === 0) {
      body.innerHTML = '<span>No connection details available for SQL editor exports.</span>';
      return;
    }

    renderTable(
      'conns-body',
      [
        { key: 'fromNode', label: 'From Node' },
        { key: 'fromPin', label: 'From Pin' },
        { key: 'toNode', label: 'To Node' },
        { key: 'toPin', label: 'To Pin' },
        { key: 'dataType', label: 'Data Type' }
      ],
      CONN_ROWS,
      'conns',
      ['fromNode', 'fromPin', 'toNode', 'toPin', 'dataType']);

    renderFilterChips('conns', 'conns-filter-list');
  }

  function bindTableControls(searchId, pageSizeId, stateKey) {
    var search = document.getElementById(searchId);
    var pageSize = document.getElementById(pageSizeId);
    var state = tableState[stateKey];

    if (search) {
      var updateFilter = debounce(function(rawValue) {
        state.filter = rawValue || '';
        state.page = 1;
        rerenderTable(stateKey);
      }, 120);

      search.addEventListener('input', function() {
        updateFilter(search.value || '');
      });
    }

    if (pageSize) {
      pageSize.addEventListener('change', function() {
        var parsed = parseInt(pageSize.value, 10);
        state.pageSize = Number.isFinite(parsed) && parsed > 0 ? parsed : 10;
        state.page = 1;
        rerenderTable(stateKey);
      });
    }
  }

  function initOrderControls(stateKey, config) {
    var state = tableState[stateKey];
    var fieldSelect = document.getElementById(config.fieldSelectId);
    var dirSelect = document.getElementById(config.directionSelectId);
    var applyButton = document.getElementById(config.applyButtonId);
    var resetButton = document.getElementById(config.resetButtonId);

    if (!fieldSelect || !dirSelect || !applyButton || !resetButton || !state) {
      return;
    }

    function refreshOrderFields() {
      var fields = tableFilterFields(stateKey);
      fieldSelect.innerHTML = '';

      fields.forEach(function(field) {
        fieldSelect.add(new Option(field.label, field.key));
      });

      if (!fields.length) {
        fieldSelect.disabled = true;
        dirSelect.disabled = true;
        applyButton.setAttribute('disabled', 'disabled');
        resetButton.setAttribute('disabled', 'disabled');
        return;
      }

      fieldSelect.disabled = false;
      dirSelect.disabled = false;
      applyButton.removeAttribute('disabled');
      resetButton.removeAttribute('disabled');

      var hasCurrentSort = fields.some(function(field) { return field.key === state.sortKey; });
      if (!hasCurrentSort) {
        state.sortKey = fields[0].key;
      }

      fieldSelect.value = state.sortKey;
      dirSelect.value = state.sortDir || 'asc';
    }

    applyButton.addEventListener('click', function() {
      if (!fieldSelect.value) {
        return;
      }

      state.sortKey = fieldSelect.value;
      state.sortDir = dirSelect.value === 'desc' ? 'desc' : 'asc';
      state.page = 1;
      rerenderTable(stateKey);
    });

    resetButton.addEventListener('click', function() {
      refreshOrderFields();
      state.sortDir = 'asc';
      if (fieldSelect.value) {
        state.sortKey = fieldSelect.value;
      }

      dirSelect.value = 'asc';
      state.page = 1;
      rerenderTable(stateKey);
    });

    refreshOrderFields();
  }

  function initSubpanelCollapsibles() {
    function setSubpanelButtonState(button, isCollapsed) {
      button.textContent = isCollapsed ? '▸ Expand' : '▾ Collapse';
    }

    document.querySelectorAll('[data-subcollapse-target]').forEach(function(button) {
      var target = button.getAttribute('data-subcollapse-target');
      if (!target) {
        return;
      }

      var panel = document.getElementById(target);
      if (!panel) {
        return;
      }

      var isCollapsed = panel.classList.contains('is-collapsed');
      button.setAttribute('aria-expanded', isCollapsed ? 'false' : 'true');
      setSubpanelButtonState(button, isCollapsed);

      button.addEventListener('click', function() {
        panel.classList.toggle('is-collapsed');
        var collapsed = panel.classList.contains('is-collapsed');
        button.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
        setSubpanelButtonState(button, collapsed);
      });
    });
  }

  function initCollapsibles() {
    function loadState() {
      try {
        var raw = localStorage.getItem(COLLAPSE_STATE_KEY);
        if (!raw) {
          return {};
        }

        var parsed = JSON.parse(raw);
        return parsed && typeof parsed === 'object' ? parsed : {};
      } catch (_) {
        return {};
      }
    }

    function saveState(state) {
      try {
        localStorage.setItem(COLLAPSE_STATE_KEY, JSON.stringify(state));
      } catch (_) {
      }
    }

    function setCollapseButtonState(button, isCollapsed) {
      button.textContent = isCollapsed ? '▸ Expand' : '▾ Collapse';
      if (isCollapsed) {
        button.classList.add('is-collapsed');
      } else {
        button.classList.remove('is-collapsed');
      }
    }

    var state = loadState();

    document.querySelectorAll('[data-collapse-target]').forEach(function(button) {
      var target = button.getAttribute('data-collapse-target');
      if (!target) {
        return;
      }

      var section = document.getElementById(target);
      if (!section) {
        return;
      }

      if (state[target] === true) {
        section.classList.add('is-collapsed');
      }

      setCollapseButtonState(button, section.classList.contains('is-collapsed'));

      button.addEventListener('click', function() {
        section.classList.toggle('is-collapsed');
        var isCollapsed = section.classList.contains('is-collapsed');
        setCollapseButtonState(button, isCollapsed);
        state[target] = isCollapsed;
        saveState(state);
      });
    });
  }

  function bindFilterKeyboardShortcuts() {
    function isTypingTarget(target) {
      if (!target || !(target instanceof HTMLElement)) {
        return false;
      }

      var tag = (target.tagName || '').toLowerCase();
      return tag === 'input' || tag === 'textarea' || target.isContentEditable;
    }

    function isSectionExpanded(id) {
      var section = document.getElementById(id);
      return !!section && !section.classList.contains('is-collapsed') && section.style.display !== 'none';
    }

    function focusField(id) {
      var field = document.getElementById(id);
      if (!field || !(field instanceof HTMLElement)) {
        return;
      }

      field.focus();
      if (field instanceof HTMLInputElement) {
        field.select();
      }
    }

    function focusFirstVisibleSearch() {
      var candidates = [
        { section: 's-results', input: 'results-search' },
        { section: 's-meta', input: 'metadata-search' },
        { section: 's-nodes', input: 'nodes-search' },
        { section: 's-conns', input: 'conns-search' }
      ];

      for (var i = 0; i < candidates.length; i += 1) {
        var item = candidates[i];
        if (!isSectionExpanded(item.section)) {
          continue;
        }

        var input = document.getElementById(item.input);
        if (input && input instanceof HTMLElement) {
          input.focus();
          if (input instanceof HTMLInputElement) {
            input.select();
          }
          return;
        }
      }
    }

    document.addEventListener('keydown', function(e) {
      if (e.key === '/' && !e.ctrlKey && !e.metaKey && !e.altKey && !isTypingTarget(e.target)) {
        e.preventDefault();
        focusFirstVisibleSearch();
        return;
      }

      if (!e.altKey || e.ctrlKey || e.metaKey) {
        return;
      }

      var key = (e.key || '').toLowerCase();
      if (key === 'm') {
        e.preventDefault();
        focusField('metadata-filter-val');
        return;
      }

      if (key === 'r') {
        e.preventDefault();
        focusField('results-filter-val');
        return;
      }

      if (key === 'n') {
        e.preventDefault();
        focusField('nodes-filter-val');
        return;
      }

      if (key === 'c') {
        e.preventDefault();
        focusField('conns-filter-val');
      }
    });
  }

  function copySql() {
    var text = SQL_TEXT || '';
    if (!text) {
      return;
    }

    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text);
      return;
    }

    var temp = document.createElement('textarea');
    temp.value = text;
    document.body.appendChild(temp);
    temp.select();
    document.execCommand('copy');
    document.body.removeChild(temp);
  }

  function updateThemeButton() {
    var btn = document.getElementById('btn-theme');
    var current = document.documentElement.getAttribute('data-theme') || 'dark';
    btn.textContent = current === 'dark' ? 'Light' : 'Dark';
  }

  function toggleTheme() {
    var root = document.documentElement;
    var current = root.getAttribute('data-theme') || 'dark';
    var next = current === 'dark' ? 'light' : 'dark';
    root.setAttribute('data-theme', next);
    localStorage.setItem('dbw-theme', next);
    updateThemeButton();
  }

  function loadTheme() {
    var saved = localStorage.getItem('dbw-theme');
    if (saved === 'light' || saved === 'dark') {
      document.documentElement.setAttribute('data-theme', saved);
    }
    updateThemeButton();
  }

  function initFooter() {
    var footer = document.getElementById('footer-text');
    footer.innerHTML = 'Generated by <strong>DBWeaver</strong> · ' + escHtml(REPORT_META.generatedAt || '—') + ' · v' + escHtml(REPORT_VERSION);
  }

  document.addEventListener('DOMContentLoaded', function() {
    renderSummary();
    renderMetadata();
    renderSql();
    renderQuality();
    renderResults();
    renderNodes();
    renderConnections();
    loadTheme();
    initFooter();
    document.getElementById('btn-theme').addEventListener('click', toggleTheme);
    document.getElementById('btn-copy-sql').addEventListener('click', copySql);
    bindTableControls('results-search', 'results-page-size', 'results');
    bindTableControls('metadata-search', 'metadata-page-size', 'metadata');
    bindTableControls('nodes-search', 'nodes-page-size', 'nodes');
    bindTableControls('conns-search', 'conns-page-size', 'conns');
    initMetadataFilterControls();
    initFilterControls('results', {
      fieldSelectId: 'results-filter-col',
      operatorSelectId: 'results-filter-op',
      valueInputId: 'results-filter-val',
      addButtonId: 'results-add-filter',
      clearButtonId: 'results-clear-filters',
      filterListId: 'results-filter-list'
    });
    initFilterControls('nodes', {
      fieldSelectId: 'nodes-filter-col',
      operatorSelectId: 'nodes-filter-op',
      valueInputId: 'nodes-filter-val',
      addButtonId: 'nodes-add-filter',
      clearButtonId: 'nodes-clear-filters',
      filterListId: 'nodes-filter-list'
    });
    initFilterControls('conns', {
      fieldSelectId: 'conns-filter-col',
      operatorSelectId: 'conns-filter-op',
      valueInputId: 'conns-filter-val',
      addButtonId: 'conns-add-filter',
      clearButtonId: 'conns-clear-filters',
      filterListId: 'conns-filter-list'
    });
    initOrderControls('results', {
      fieldSelectId: 'results-order-col',
      directionSelectId: 'results-order-dir',
      applyButtonId: 'results-apply-order',
      resetButtonId: 'results-reset-order'
    });
    initOrderControls('metadata', {
      fieldSelectId: 'metadata-order-col',
      directionSelectId: 'metadata-order-dir',
      applyButtonId: 'metadata-apply-order',
      resetButtonId: 'metadata-reset-order'
    });
    initOrderControls('nodes', {
      fieldSelectId: 'nodes-order-col',
      directionSelectId: 'nodes-order-dir',
      applyButtonId: 'nodes-apply-order',
      resetButtonId: 'nodes-reset-order'
    });
    initOrderControls('conns', {
      fieldSelectId: 'conns-order-col',
      directionSelectId: 'conns-order-dir',
      applyButtonId: 'conns-apply-order',
      resetButtonId: 'conns-reset-order'
    });
    initCollapsibles();
    initSubpanelCollapsibles();
    bindFilterKeyboardShortcuts();
  });
})();
</script>
</body>
</html>
""";
    }

    private static object BuildJsonPayload(SqlEditorReportExportContext context, SqlEditorReportExportRequest request)
    {
        string generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        string generatedAtIso = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
        object meta = BuildMeta(context, request, generatedAt, generatedAtIso);
        object result = BuildExecutionResultObject(context.ExecutionResult);
        bool includeGraphSections = request.IncludeMetadata && request.IncludeNodeDetails;
        object[]? nodes = includeGraphSections ? [] : null;
        object[]? connections = includeGraphSections ? [] : null;
        bool hasSql = !string.IsNullOrWhiteSpace(context.Sql);

        return new
        {
            version = ReportVersion,
            meta,
            sql = context.Sql,
            hasSql,
            result,
            schema = request.IncludeSchema ? context.SchemaColumns : [],
          rows = context.ResultRows,
            nodes,
            connections,
        };
    }

    private static object BuildMeta(
        SqlEditorReportExportContext context,
        SqlEditorReportExportRequest request,
        string generatedAt,
        string generatedAtIso)
    {
        string normalizedStatus = NormalizeStatus(context.ExecutionResult.Status);
        int errorCount = normalizedStatus == "error" ? 1 : 0;
        int warningCount = normalizedStatus == "warning" ? 1 : 0;
        int columnCount = context.SchemaColumns.Count;
        int joinCount = CountMatches(context.Sql, @"\bjoin\b");
        int aggregateCount = CountMatches(context.Sql, @"\b(count|sum|avg|min|max|string_agg|group_concat)\s*\(");
        bool hasSubquery = ContainsSubquery(context.Sql);
        string title = string.IsNullOrWhiteSpace(request.Title) ? context.TabTitle : request.Title;
        string? description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        ConnectionConfig? connection = context.Connection;
        object[] metadataRows = request.IncludeMetadata
          ?
          [
            new { field = "provider", label = "Provider", value = connection?.Provider.ToString(), kind = "text" },
            new { field = "database", label = "Database", value = connection?.Database, kind = "text" },
            new { field = "host", label = "Host", value = connection?.Host, kind = "text" },
            new { field = "dialect", label = "Dialect", value = InferDialect(connection?.Provider), kind = "text" },
            new { field = "executionDate", label = "Execution Date", value = generatedAtIso, kind = "date" },
            new { field = "locale", label = "Locale", value = CultureInfo.CurrentCulture.Name, kind = "text" },
            new { field = "timezone", label = "Timezone", value = TimeZoneInfo.Local.Id, kind = "text" },
            new { field = "engineVersion", label = "Engine Version", value = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "—", kind = "text" },
            new { field = "filePath", label = "File Path", value = context.ActiveFilePath, kind = "text" },
            new { field = "columnCount", label = "Columns", value = columnCount, kind = "number" },
            new { field = "joinCount", label = "Join Count", value = joinCount, kind = "number" },
            new { field = "aggregateCount", label = "Aggregate Count", value = aggregateCount, kind = "number" },
            new { field = "hasSubquery", label = "Has Subquery", value = hasSubquery, kind = "bool" },
            new { field = "warningCount", label = "Warnings", value = warningCount, kind = "number" },
            new { field = "errorCount", label = "Errors", value = errorCount, kind = "number" },
          ]
          : [];

        object summary = new
        {
          status = normalizedStatus,
          success = normalizedStatus == "success",
          executedAt = generatedAt,
          executionTimeMs = context.ExecutionResult.ExecutionTimeMs,
          rowCount = context.ExecutionResult.RowCount,
          sqlLength = context.Sql.Length,
          errorMessage = context.ExecutionResult.ErrorMessage,
        };

        return new
        {
            reportVersion = ReportVersion,
          includeMetadata = request.IncludeMetadata,
          useDashForEmptyFields = request.UseDashForEmptyFields,
            title,
            description,
            generatedAt,
            generatedAtIso,
          summary,
          metadata = metadataRows,
          provider = connection?.Provider.ToString() ?? "—",
          providerVersion = "—",
          database = connection?.Database ?? "—",
          host = connection?.Host ?? "—",
          dialect = InferDialect(connection?.Provider),
          engineVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "—",
          timezone = TimeZoneInfo.Local.Id,
          locale = CultureInfo.CurrentCulture.Name,
          nodeCount = 0,
          connCount = 0,
          columnCount,
          errorCount,
          warningCount,
          orphanCount = 0,
          namingConformance = "—",
          joinCount,
          aggregateCount,
          hasSubquery,
          queryLength = context.Sql.Length,
          rowCount = context.ExecutionResult.RowCount,
          executionTimeMs = context.ExecutionResult.ExecutionTimeMs,
          status = normalizedStatus,
          filePath = context.ActiveFilePath ?? "—",
        };
    }

    private static object BuildExecutionResultObject(SqlEditorReportExecutionResult executionResult)
    {
        return new
        {
            rowCount = executionResult.RowCount,
            executionTimeMs = executionResult.ExecutionTimeMs,
          status = NormalizeStatus(executionResult.Status),
          success = NormalizeStatus(executionResult.Status) == "success",
            errorMessage = executionResult.ErrorMessage,
        };
    }

    private static string InferDialect(DatabaseProvider? provider)
    {
        return provider switch
        {
            DatabaseProvider.Postgres => "postgresql",
            DatabaseProvider.SqlServer => "sqlserver",
            DatabaseProvider.MySql => "mysql",
            DatabaseProvider.SQLite => "sqlite",
            _ => "unknown",
        };
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return "success";

        string normalized = status.Trim().ToLowerInvariant();
        return normalized switch
        {
            "error" => "error",
            "warning" => "warning",
            _ => "success",
        };
    }

    private static int CountMatches(string input, string pattern)
    {
        if (string.IsNullOrWhiteSpace(input))
            return 0;

        return Regex.Matches(input, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count;
    }

    private static bool ContainsSubquery(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return false;

        return Regex.IsMatch(sql, @"\(\s*select\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
               || Regex.IsMatch(sql, @"\bexists\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string SerializeForInlineJs(object value)
    {
        return JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static string EscapeJsTemplateLiteral(string value)
    {
      string escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("$", "\\$", StringComparison.Ordinal);

      // Prevent accidental script tag termination when SQL contains </script>
      return Regex.Replace(escaped, "</script", "<\\/script", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string HtmlEncode(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
    }
}
