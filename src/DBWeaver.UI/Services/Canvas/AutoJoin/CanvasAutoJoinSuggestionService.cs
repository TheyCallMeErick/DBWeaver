using DBWeaver.Core;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.CanvasKit;
using DBWeaver.Metadata;
using DBWeaver.Nodes;

namespace DBWeaver.UI.Services.Canvas.AutoJoin;

internal sealed class CanvasAutoJoinSuggestionService : ICanvasAutoJoinSuggestionService
{
    public IReadOnlyList<JoinSuggestion> AnalyzeNewTable(string newTableFullName, IReadOnlyCollection<NodeViewModel> nodes)
    {
        if (string.IsNullOrWhiteSpace(newTableFullName))
            return [];

        List<NodeViewModel> tables = nodes
            .Where(n => n.IsTableSource && !string.Equals(n.Subtitle, newTableFullName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (tables.Count == 0)
            return [];

        var detector = new AutoJoinDetector(BuildMetadataFromCanvas(nodes));
        List<string> candidateTables = tables
            .Select(GetTableIdentifier)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidateTables.Count == 0)
            return [];

        return detector.Suggest(newTableFullName, candidateTables);
    }

    public IReadOnlyList<JoinSuggestion> AnalyzeAllTables(IReadOnlyCollection<NodeViewModel> nodes)
    {
        List<NodeViewModel> tables = nodes.Where(n => n.IsTableSource).ToList();
        if (tables.Count < 2)
            return [];

        var detector = new AutoJoinDetector(BuildMetadataFromCanvas(nodes));
        var allSuggestions = new List<JoinSuggestion>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (NodeViewModel t in tables)
        {
            string current = GetTableIdentifier(t);
            if (string.IsNullOrWhiteSpace(current))
                continue;

            IEnumerable<string> others = tables
                .Where(x => x != t)
                .Select(GetTableIdentifier)
                .Where(name => !string.IsNullOrWhiteSpace(name));

            foreach (JoinSuggestion s in detector.Suggest(current, others))
            {
                string key = CanvasAutoJoinSemantics.BuildSuggestionPairKey(
                    s.ExistingTable,
                    s.NewTable,
                    s.LeftColumn,
                    s.RightColumn
                );
                if (seen.Add(key))
                    allSuggestions.Add(s);
            }
        }

        return allSuggestions;
    }

    public IReadOnlyList<JoinSuggestion> AnalyzePair(NodeViewModel left, NodeViewModel right, IReadOnlyCollection<NodeViewModel> nodes)
    {
        string leftId = GetTableIdentifier(left);
        string rightId = GetTableIdentifier(right);
        if (string.IsNullOrWhiteSpace(leftId) || string.IsNullOrWhiteSpace(rightId))
            return [];

        var detector = new AutoJoinDetector(BuildMetadataFromCanvas(nodes));
        List<JoinSuggestion> suggestions = detector.Suggest(leftId, [rightId]).ToList();
        if (suggestions.Count == 0)
            suggestions = detector.Suggest(rightId, [leftId]).ToList();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return suggestions
            .Where(s => seen.Add($"{s.LeftColumn}|{s.RightColumn}|{s.JoinType}"))
            .OrderByDescending(s => s.Score)
            .ToList();
    }

    private static DbMetadata BuildMetadataFromCanvas(IReadOnlyCollection<NodeViewModel> nodes)
    {
        List<TableMetadata> tables = nodes
            .Where(n => n.IsTableSource)
            .Select(BuildTableMetadata)
            .ToList();

        string schema = tables.Count > 0
            ? (tables[0].Schema ?? "public")
            : "public";

        return new DbMetadata(
            DatabaseName: "canvas",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "0",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata(schema, tables)],
            AllForeignKeys: []
        );
    }

    private static TableMetadata BuildTableMetadata(NodeViewModel node)
    {
        string full = node.Subtitle ?? node.Title ?? "unknown.table";
        string[] parts = full.Split('.', 2);
        string schema = parts.Length == 2 ? parts[0] : "public";
        string name = parts.Length == 2 ? parts[1] : full;

        List<ColumnMetadata> cols = node
            .OutputPins
            .Where(p => p.DataType != PinDataType.ColumnSet && p.Name != "*")
            .Select((p, i) =>
            {
                string nativeType = p.DataType switch
                {
                    PinDataType.Number => "int",
                    PinDataType.Text => "varchar",
                    PinDataType.Boolean => "bool",
                    PinDataType.DateTime => "timestamp",
                    PinDataType.Json => "jsonb",
                    _ => "text",
                };
                bool isPk = p.Name.Equals("id", StringComparison.OrdinalIgnoreCase) && i == 0;
                bool isFk = p.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase) && !isPk;
                return new ColumnMetadata(
                    Name: p.Name,
                    DataType: nativeType,
                    NativeType: nativeType,
                    IsNullable: !isPk,
                    IsPrimaryKey: isPk,
                    IsForeignKey: isFk,
                    IsUnique: isPk,
                    IsIndexed: isPk || isFk,
                    OrdinalPosition: i + 1
                );
            })
            .ToList();

        return new TableMetadata(
            Schema: schema,
            Name: name,
            Kind: TableKind.Table,
            EstimatedRowCount: null,
            Columns: cols,
            Indexes: [],
            OutboundForeignKeys: [],
            InboundForeignKeys: []
        );
    }

    private static string GetTableIdentifier(NodeViewModel node)
    {
        if (!string.IsNullOrWhiteSpace(node.Subtitle))
            return node.Subtitle;

        return node.Title ?? string.Empty;
    }
}





