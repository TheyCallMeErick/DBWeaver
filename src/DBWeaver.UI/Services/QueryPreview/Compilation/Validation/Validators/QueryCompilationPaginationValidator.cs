
namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationPaginationValidator(CanvasViewModel canvas, DatabaseProvider provider)
{
    private readonly CanvasViewModel _canvas = canvas;
    private readonly DatabaseProvider _provider = provider;

    public void Validate(NodeViewModel resultOutputNode, List<string> errors)
    {
        ConnectionViewModel? topConn = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == resultOutputNode
            && c.ToPin?.Name == "top"
            && c.FromPin.Owner.Type == NodeType.Top
        );

        if (topConn is not null)
        {
            NodeViewModel topNode = topConn.FromPin.Owner;
            if (TryResolveTopCount(topNode, out int topCount) && topCount <= 0)
            {
                errors.Add($"TOP/LIMIT value must be greater than 0. Current value: {topCount}. SQL generation is preserved, but the result can be empty.");
            }
        }

        bool hasOrderBy = HasImportedOrderTerms(resultOutputNode);
        int? offset = ResolveResultOffset(resultOutputNode);
        if (offset.HasValue && offset.Value > 0 && !hasOrderBy)
        {
            errors.Add($"OFFSET {offset.Value} without ORDER BY may produce non-deterministic pagination on {_provider}. Add ORDER BY for stable paging.");
        }
    }

    private bool HasImportedOrderTerms(NodeViewModel resultOutputNode)
    {
        bool hasOrderWire = _canvas.Connections.Any(c =>
            c.ToPin?.Owner == resultOutputNode
            && (
                c.ToPin.Name.Equals("order_by", StringComparison.OrdinalIgnoreCase)
                || c.ToPin.Name.Equals("order_by_desc", StringComparison.OrdinalIgnoreCase)
            ));

        if (hasOrderWire)
            return true;

        return resultOutputNode.Parameters.TryGetValue("import_order_terms", out string? importedTerms)
            && !string.IsNullOrWhiteSpace(importedTerms);
    }

    private static int? ResolveResultOffset(NodeViewModel resultOutputNode)
    {
        if (resultOutputNode.Parameters.TryGetValue("offset", out string? offsetRaw)
            && int.TryParse(offsetRaw, out int offsetFromParam))
        {
            return offsetFromParam;
        }

        if (resultOutputNode.Parameters.TryGetValue("import_offset", out string? importOffsetRaw)
            && int.TryParse(importOffsetRaw, out int offsetFromImport))
        {
            return offsetFromImport;
        }

        return null;
    }

    private bool TryResolveTopCount(NodeViewModel topNode, out int topCount)
    {
        ConnectionViewModel? countWire = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == topNode
            && c.ToPin?.Name == "count"
            && c.FromPin.Owner.Type == NodeType.ValueNumber
        );

        if (countWire is not null
            && countWire.FromPin.Owner.Parameters.TryGetValue("value", out string? wiredVal)
            && int.TryParse(wiredVal, out int wiredCount))
        {
            topCount = wiredCount;
            return true;
        }

        if (topNode.Parameters.TryGetValue("count", out string? paramVal)
            && int.TryParse(paramVal, out int paramCount))
        {
            topCount = paramCount;
            return true;
        }

        topCount = default;
        return false;
    }
}
