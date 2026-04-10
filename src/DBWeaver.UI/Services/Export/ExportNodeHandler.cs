using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using DBWeaver.Nodes;
using DBWeaver.UI.Services.Theming;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.Export;

/// <summary>
/// Handles the file generation triggered by HtmlExport, JsonExport, CsvExport, and ExcelExport nodes.
///
/// Since the canvas generates SQL (not live query results), the exported files contain:
///   • HTML  — a self-contained HTML page embedding the SQL and a schema table of column aliases
///   • JSON  — a JSON template array: [{"col1": null, "col2": null, ...}]
///   • CSV   — a header row built from the column aliases defined in the connected ResultOutput
/// </summary>
public static class ExportNodeHandler
{
    // ── Public entry points ───────────────────────────────────────────────────

    /// <summary>
    /// Generates an export file for the given export node.
    /// Returns the absolute path of the written file, or null on error.
    /// </summary>
    public static async Task<string?> RunExportAsync(
        CanvasViewModel canvas,
        NodeViewModel exportNode,
        string? overridePath = null
    )
    {
        string filePath =
            overridePath
            ?? (exportNode.Parameters.TryGetValue("file_name", out string? fn) ? fn : null)
            ?? DefaultFileName(exportNode.Type);

        // Resolve columns from the ResultOutput connected upstream
        IReadOnlyList<string> columns = ResolveColumns(canvas, exportNode);
        string sql = canvas.QueryText ?? string.Empty;

        try
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (exportNode.Type == NodeType.ExcelExport)
            {
                byte[] bytes = BuildXlsx(columns, exportNode);
                await File.WriteAllBytesAsync(filePath, bytes);
            }
            else
            {
                string content = exportNode.Type switch
                {
                    NodeType.HtmlExport => BuildHtml(columns, sql, exportNode),
                    NodeType.JsonExport => BuildJson(columns),
                    NodeType.CsvExport => BuildCsv(columns, exportNode),
                    _ => throw new NotSupportedException(),
                };
                await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
            }

            return Path.GetFullPath(filePath);
        }
        catch
        {
            return null;
        }
    }

    // ── Column resolution ─────────────────────────────────────────────────────

    /// <summary>
    /// Walks canvas connections to find the ResultOutput upstream of the export node,
    /// then returns the ordered list of column display names (aliases).
    /// Falls back to an empty list if no ResultOutput is connected.
    /// </summary>
    private static IReadOnlyList<string> ResolveColumns(
        CanvasViewModel canvas,
        NodeViewModel exportNode
    )
    {
        // Find the ResultOutput connected to this export node's "query" input pin
        NodeViewModel? resultOutputNode = canvas
            .Connections.Where(c =>
                c.ToPin?.Owner == exportNode
                && c.ToPin?.Name == "query"
                && c.FromPin.Owner.Type == NodeType.ResultOutput
            )
            .Select(c => c.FromPin.Owner)
            .FirstOrDefault();

        // Fallback: use any ResultOutput on the canvas
        resultOutputNode ??= canvas.Nodes.FirstOrDefault(n => n.Type == NodeType.ResultOutput);

        if (resultOutputNode is null)
            return [];

        // Use the ordered column list if populated
        if (resultOutputNode.OutputColumnOrder.Count > 0)
            return resultOutputNode.OutputColumnOrder.Select(e => e.DisplayName).ToList();

        // Fallback: read aliases from nodes connected to ResultOutput's "columns" input
        return canvas
            .Connections.Where(c =>
                c.ToPin?.Owner == resultOutputNode && c.ToPin?.Name == "columns"
            )
            .Select(c =>
                string.IsNullOrWhiteSpace(c.FromPin.Owner.Alias)
                    ? c.FromPin.Name
                    : c.FromPin.Owner.Alias
            )
            .ToList();
    }

    private static string DefaultFileName(NodeType type) =>
        type switch
        {
            NodeType.HtmlExport => "export.html",
            NodeType.JsonExport => "export.json",
            NodeType.CsvExport => "export.csv",
            NodeType.ExcelExport => "export.xlsx",
            _ => "export.txt",
        };

    // ── HTML builder ──────────────────────────────────────────────────────────

    private static string BuildHtml(IReadOnlyList<string> columns, string sql, NodeViewModel node)
    {
        var sb = new StringBuilder();
        string title = node.Parameters.TryGetValue("file_name", out string? fn)
            ? Path.GetFileNameWithoutExtension(fn)
            : "SQL Export";
        string colsHtml =
            columns.Count > 0
                ? string.Join("", columns.Select(c => $"<th>{HtmlEncode(c)}</th>"))
                : "<th><em>No columns defined</em></th>";
        string sqlHtml = HtmlEncode(sql.Trim());
        string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        sb.Append(
            $$"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1.0"/>
<title>{{title}}</title>
<style>
  :root{--bg:{{UiColorConstants.C_0F1220.ToLowerInvariant()}};--surface:{{UiColorConstants.C_1E2A3F.ToLowerInvariant()}};--border:{{UiColorConstants.C_334155.ToLowerInvariant()}};--text:{{UiColorConstants.C_E6EDF8.ToLowerInvariant()}};--accent:{{UiColorConstants.C_60A5FA.ToLowerInvariant()}};--muted:{{UiColorConstants.C_94A3B8.ToLowerInvariant()}};--code:{{UiColorConstants.C_1E2A3F.ToLowerInvariant()}}}
  body{margin:0;padding:2rem;font-family:'Segoe UI',system-ui,sans-serif;background:var(--bg);color:var(--text)}
  h1{color:var(--accent);margin:0 0 .25rem}
  .meta{color:var(--muted);font-size:.85rem;margin-bottom:1.5rem}
  .section{background:var(--surface);border:1px solid var(--border);border-radius:.5rem;padding:1rem 1.25rem;margin-bottom:1.5rem}
  h2{font-size:1rem;color:var(--accent);margin:0 0 .75rem;text-transform:uppercase;letter-spacing:.05em}
  pre{margin:0;white-space:pre-wrap;word-break:break-all;font-family:'Fira Code',monospace;font-size:.875rem;color:{{UiColorConstants.C_86EFAC.ToLowerInvariant()}}}
  table{width:100%;border-collapse:collapse;font-size:.875rem}
  th{background:{{UiColorConstants.C_0F1220.ToLowerInvariant()}};color:var(--accent);padding:.5rem .75rem;border:1px solid var(--border);text-align:left}
  td{padding:.5rem .75rem;border:1px solid var(--border);color:var(--muted);font-style:italic}
  .badge{display:inline-block;background:{{UiColorConstants.C_1D4ED8.ToLowerInvariant()}};color:{{UiColorConstants.C_E6EDF8.ToLowerInvariant()}};padding:.125rem .5rem;border-radius:999px;font-size:.75rem}
</style>
</head>
<body>
<h1>{{HtmlEncode(title)}}</h1>
<p class="meta">Generated by <strong>DBWeaver</strong> &nbsp;·&nbsp; {{ts}} &nbsp;·&nbsp; <span class="badge">{{columns.Count}} column(s)</span></p>

<div class="section">
  <h2>Generated SQL</h2>
  <pre>{{(
                string.IsNullOrWhiteSpace(sqlHtml)
                    ? "-- No SQL generated yet. Connect nodes to a Result Output node."
                    : sqlHtml
            )}}</pre>
</div>

<div class="section">
  <h2>Output Schema</h2>
  <table>
    <thead><tr>{{colsHtml}}</tr></thead>
    <tbody><tr>{{(
                columns.Count > 0
                    ? string.Join("", columns.Select(_ => "<td>…</td>"))
                    : "<td><em>Run the query to see data</em></td>"
            )}}</tr></tbody>
  </table>
</div>
</body>
</html>
"""
        );
        return sb.ToString();
    }

    private static string HtmlEncode(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    // ── JSON builder ──────────────────────────────────────────────────────────

    private static string BuildJson(IReadOnlyList<string> columns)
    {
        if (columns.Count == 0)
        {
            return JsonSerializer.Serialize(
                new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["_note"] = "No columns defined — connect a Result Output node",
                    },
                },
                new JsonSerializerOptions { WriteIndented = true }
            );
        }

        var template = columns.ToDictionary(c => c, _ => (object?)null);
        Dictionary<string, object?>[] result = new[] { template };
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    // ── CSV builder ───────────────────────────────────────────────────────────

    private static string BuildCsv(IReadOnlyList<string> columns, NodeViewModel node)
    {
        string rawDelim = node.Parameters.TryGetValue("delimiter", out string? d) ? d : ",";
        string delimiter = rawDelim == "\\t" ? "\t" : rawDelim;

        if (columns.Count == 0)
            return $"# No columns defined — connect a Result Output node{Environment.NewLine}";

        string header = string.Join(delimiter, columns.Select(c => CsvEscape(c, delimiter)));
        return header + Environment.NewLine;
    }

    private static string CsvEscape(string value, string delimiter)
    {
        bool needsQuoting =
            value.Contains('"') || value.Contains('\n') || value.Contains(delimiter);
        if (!needsQuoting)
            return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    // ── Excel (XLSX) builder ──────────────────────────────────────────────────

    /// <summary>
    /// Builds an XLSX workbook with a bold header row containing the column names.
    /// If no columns are defined a single descriptive cell is written instead.
    /// Uses ClosedXML — no Excel installation required.
    /// </summary>
    private static byte[] BuildXlsx(IReadOnlyList<string> columns, NodeViewModel node)
    {
        string sheetName =
            node.Parameters.TryGetValue("sheet_name", out string? sn)
            && !string.IsNullOrWhiteSpace(sn)
                ? sn
                : "Sheet1";

        using var workbook = new XLWorkbook();
        IXLWorksheet worksheet = workbook.Worksheets.Add(sheetName);

        if (columns.Count == 0)
        {
            IXLCell cell = worksheet.Cell(1, 1);
            cell.Value = "No columns defined — connect a Result Output node";
            cell.Style.Font.Italic = true;
        }
        else
        {
            for (int i = 0; i < columns.Count; i++)
            {
                IXLCell cell = worksheet.Cell(1, i + 1);
                cell.Value = columns[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml(UiColorConstants.C_1D4ED8);
                cell.Style.Font.FontColor = XLColor.White;
            }

            // Auto-fit column widths to header text
            worksheet.Columns().AdjustToContents();
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
