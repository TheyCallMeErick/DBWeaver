using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.SqlImport.Build;

public readonly record struct ImportFromPart(string Table, string? JoinType, string? OnClause);

public readonly record struct ImportSelectTerm(string Expr, string? Alias);

public readonly record struct ImportLayout(double BaseX, double BaseY, double ColGap, double RowGap);

public sealed record ImportBuildInput(
    IReadOnlyList<ImportFromPart> FromParts,
    IReadOnlyList<ImportSelectTerm> SelectedColumns,
    bool IsStar,
    ImportLayout Layout
);

public sealed record ImportBuildContext(
    List<NodeViewModel> TableNodes,
    NodeViewModel ResultNode,
    Dictionary<string, PinViewModel> ProjectedAliases,
    double ResultY,
    int Imported,
    int Partial,
    int Skipped
);

public sealed class ImportModelToCanvasBuilder(CanvasViewModel canvas)
{
    private readonly CanvasViewModel _canvas = canvas;

    public ImportBuildContext BuildCore(
        ImportBuildInput input,
        ObservableCollection<ImportReportItem> report,
        CancellationToken ct
    )
    {
        int imported = 0;
        int partial = 0;
        int skipped = 0;

        ct.ThrowIfCancellationRequested();
        _canvas.Connections.Clear();
        _canvas.Nodes.Clear();
        _canvas.UndoRedo.Clear();

        var tableNodes = new List<NodeViewModel>();
        for (int i = 0; i < input.FromParts.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            string table = input.FromParts[i].Table;
            var pos = new Point(input.Layout.BaseX, input.Layout.BaseY + i * input.Layout.RowGap);

            var catalogEntry = CanvasViewModel.DemoCatalog.FirstOrDefault(t =>
                t.FullName.Equals(table, StringComparison.OrdinalIgnoreCase)
                || t.FullName.EndsWith("." + table, StringComparison.OrdinalIgnoreCase)
            );

            NodeViewModel tableNode =
                catalogEntry != default
                    ? new NodeViewModel(catalogEntry.FullName, catalogEntry.Cols, pos)
                    : new NodeViewModel(table, [], pos);

            _canvas.Nodes.Add(tableNode);
            tableNodes.Add(tableNode);

            report.Add(
                new ImportReportItem(
                    input.FromParts[i].JoinType is not null
                        ? $"{input.FromParts[i].JoinType}: {table}"
                        : $"FROM: {table}",
                    ImportItemStatus.Imported,
                    sourceNodeId: tableNode.Id
                )
            );
            imported++;
        }

        for (int i = 1; i < input.FromParts.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (
                string.IsNullOrWhiteSpace(input.FromParts[i].JoinType)
                || string.IsNullOrWhiteSpace(input.FromParts[i].OnClause)
            )
            {
                continue;
            }

            Match onMatch = Regex.Match(
                input.FromParts[i].OnClause!,
                @"^\s*([\w\.]+)\s*(=|<>|!=|>|>=|<|<=)\s*([\w\.]+)\s*$",
                RegexOptions.IgnoreCase
            );

            if (!onMatch.Success)
                continue;

            NodeViewModel joinNode = new(
                NodeDefinitionRegistry.Get(NodeType.Join),
                new Point(
                    input.Layout.BaseX + input.Layout.ColGap,
                    input.Layout.BaseY + i * input.Layout.RowGap - 80
                )
            );
            joinNode.Parameters["join_type"] = NormalizeJoinType(input.FromParts[i].JoinType!);
            joinNode.Parameters["right_source"] = input.FromParts[i].Table;
            joinNode.Parameters["left_expr"] = onMatch.Groups[1].Value.Trim();
            joinNode.Parameters["operator"] = onMatch.Groups[2].Value.Trim() == "!="
                ? "<>"
                : onMatch.Groups[2].Value.Trim();
            joinNode.Parameters["right_expr"] = onMatch.Groups[3].Value.Trim();
            _canvas.Nodes.Add(joinNode);
        }

        double resultY = input.Layout.BaseY + (input.FromParts.Count - 1) * input.Layout.RowGap / 2.0;
        NodeViewModel result = new(
            NodeDefinitionRegistry.Get(NodeType.ResultOutput),
            new Point(input.Layout.BaseX + input.Layout.ColGap * 3, resultY)
        );
        result.Parameters.Remove("import_order_terms");
        result.Parameters.Remove("import_group_terms");
        _canvas.Nodes.Add(result);

        NodeViewModel columnList = new(
            NodeDefinitionRegistry.Get(NodeType.ColumnSetBuilder),
            new Point(input.Layout.BaseX + input.Layout.ColGap * 2, resultY)
        );
        _canvas.Nodes.Add(columnList);
        SafeWire(columnList, "result", result, "columns");

        var projectedAliases = new Dictionary<string, PinViewModel>(StringComparer.OrdinalIgnoreCase);

        NodeViewModel primaryNode = tableNodes[0];
        if (input.IsStar)
        {
            foreach (PinViewModel pin in primaryNode.OutputPins)
                SafeWire(primaryNode, pin.Name, columnList, "columns");

            report.Add(
                new ImportReportItem(
                    "SELECT *",
                    ImportItemStatus.Imported,
                    "All columns wired",
                    result.Id
                )
            );
            imported++;
        }
        else if (input.SelectedColumns.Count > 0)
        {
            int wired = 0;
            foreach ((string expr, string? alias) in input.SelectedColumns)
            {
                ct.ThrowIfCancellationRequested();
                string colName = expr.Split('.').Last().Trim();
                PinViewModel? pin = tableNodes
                    .SelectMany(n => n.OutputPins)
                    .FirstOrDefault(p => p.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));

                if (pin is not null)
                {
                    SafeWire(pin.Owner, pin.Name, columnList, "columns");
                    if (!string.IsNullOrWhiteSpace(alias))
                        projectedAliases[alias.Trim()] = pin;
                    wired++;
                }
                else
                {
                    report.Add(
                        new ImportReportItem(
                            $"Column: {expr}",
                            ImportItemStatus.Skipped,
                            "Complex expression — wire manually"
                        )
                    );
                    skipped++;
                }
            }

            report.Add(
                new ImportReportItem(
                    $"SELECT ({wired}/{input.SelectedColumns.Count} columns)",
                    wired == input.SelectedColumns.Count
                        ? ImportItemStatus.Imported
                        : ImportItemStatus.Partial,
                    wired == input.SelectedColumns.Count
                        ? null
                        : "Some selected columns could not be auto-wired.",
                    result.Id
                )
            );
            if (wired == input.SelectedColumns.Count)
                imported++;
            else
                partial++;
        }

        return new ImportBuildContext(
            tableNodes,
            result,
            projectedAliases,
            resultY,
            imported,
            partial,
            skipped
        );
    }

    private void SafeWire(NodeViewModel from, string fromPin, NodeViewModel to, string toPin)
    {
        PinViewModel? fp =
            from.OutputPins.FirstOrDefault(p =>
                p.Name.Equals(fromPin, StringComparison.OrdinalIgnoreCase)
            )
            ?? from.InputPins.FirstOrDefault(p =>
                p.Name.Equals(fromPin, StringComparison.OrdinalIgnoreCase)
            );
        PinViewModel? tp =
            to.InputPins.FirstOrDefault(p => p.Name.Equals(toPin, StringComparison.OrdinalIgnoreCase))
            ?? to.OutputPins.FirstOrDefault(p =>
                p.Name.Equals(toPin, StringComparison.OrdinalIgnoreCase)
            );

        if (fp is null || tp is null)
            return;
        if (!tp.CanAccept(fp))
            return;

        var conn = new ConnectionViewModel(fp, default, default) { ToPin = tp };
        fp.IsConnected = true;
        tp.IsConnected = true;
        _canvas.Connections.Add(conn);
    }

    private static string NormalizeJoinType(string rawJoinType)
    {
        string normalized = rawJoinType.ToUpperInvariant();
        if (normalized.Contains("LEFT", StringComparison.Ordinal))
            return "LEFT";
        if (normalized.Contains("RIGHT", StringComparison.Ordinal))
            return "RIGHT";
        if (normalized.Contains("FULL", StringComparison.Ordinal))
            return "FULL";
        if (normalized.Contains("CROSS", StringComparison.Ordinal))
            return "CROSS";
        return "INNER";
    }
}
