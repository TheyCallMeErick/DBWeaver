using System.Text;

namespace DBWeaver.UI.Services.SqlEditor.Reports;

internal static class SqlEditorReportHtmlSections
{
    public static string BuildResultsSection()
    {
        return """
  <section id="s-results">
    <div class="section-header">
      <h2><span class="section-icon" aria-hidden="true"><svg viewBox="0 0 16 16" focusable="false"><path d="M2 2h12v3H2V2Zm0 4h12v3H2V6Zm0 4h12v3H2v-3Z"/></svg></span>Query Results</h2>
      <div class="controls">
        <input id="results-search" class="table-input" type="search" placeholder="Filter results"/>
        <select id="results-page-size" class="table-select">
          <option value="10">10</option>
          <option value="25">25</option>
          <option value="50">50</option>
        </select>
        <button class="collapse-btn" data-collapse-target="s-results" type="button">▾ Collapse</button>
      </div>
    </div>
    <div class="section-body">
"""
        + BuildFilterAndOrderPanels("results")
        + """
      <div id="results-body"></div>
    </div>
  </section>
""";
    }

    public static string BuildFilterAndOrderPanels(string prefix)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"      <div class=\"subpanel\" id=\"{prefix}-filters-panel\">");
        sb.AppendLine("        <div class=\"subpanel-header\">");
        sb.AppendLine("          <div class=\"subpanel-title\">Filters</div>");
        sb.AppendLine($"          <button class=\"tool-toggle\" type=\"button\" data-subcollapse-target=\"{prefix}-filters-panel\" aria-expanded=\"true\">▾ Collapse</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"subpanel-body\">");
        sb.AppendLine("        <div class=\"filter-toolbar\">");
        sb.AppendLine($"          <select id=\"{prefix}-filter-col\" class=\"table-select\"></select>");
        sb.AppendLine($"          <select id=\"{prefix}-filter-op\" class=\"table-select\">");
        sb.AppendLine("            <option value=\"contains\">Contains</option>");
        sb.AppendLine("            <option value=\"=\">= Equals</option>");
        sb.AppendLine("            <option value=\"!=\">!= Not equal</option>");
        sb.AppendLine("            <option value=\">\">&gt; Greater than</option>");
        sb.AppendLine("            <option value=\"<\">&lt; Less than</option>");
        sb.AppendLine("            <option value=\">=\">&gt;= Greater or equal</option>");
        sb.AppendLine("            <option value=\"<=\">&lt;= Less or equal</option>");
        sb.AppendLine("          </select>");
        sb.AppendLine($"          <input id=\"{prefix}-filter-val\" class=\"table-input\" type=\"text\" placeholder=\"Filter value\"/>");
        sb.AppendLine($"          <button id=\"{prefix}-add-filter\" class=\"collapse-btn\" type=\"button\">Add Filter</button>");
        sb.AppendLine($"          <button id=\"{prefix}-clear-filters\" class=\"collapse-btn\" type=\"button\">Clear Filters</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine($"        <div class=\"filter-list\" id=\"{prefix}-filter-list\"></div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");

        sb.AppendLine($"      <div class=\"subpanel\" id=\"{prefix}-order-panel\">");
        sb.AppendLine("        <div class=\"subpanel-header\">");
        sb.AppendLine("          <div class=\"subpanel-title\">Orders</div>");
        sb.AppendLine($"          <button class=\"tool-toggle\" type=\"button\" data-subcollapse-target=\"{prefix}-order-panel\" aria-expanded=\"true\">▾ Collapse</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"subpanel-body\">");
        sb.AppendLine("        <div class=\"filter-toolbar\">");
        sb.AppendLine($"          <select id=\"{prefix}-order-col\" class=\"table-select\"></select>");
        sb.AppendLine($"          <select id=\"{prefix}-order-dir\" class=\"table-select\">");
        sb.AppendLine("            <option value=\"asc\">Ascending</option>");
        sb.AppendLine("            <option value=\"desc\">Descending</option>");
        sb.AppendLine("          </select>");
        sb.AppendLine($"          <button id=\"{prefix}-apply-order\" class=\"collapse-btn\" type=\"button\">Apply Order</button>");
        sb.AppendLine($"          <button id=\"{prefix}-reset-order\" class=\"collapse-btn\" type=\"button\">Reset Order</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");

        return sb.ToString();
    }
}
