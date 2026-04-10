using System.Text.Json;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Serialization;

public static partial class CanvasSerializer
{
    private static (NodeViewModel?, string? SkipReason) BuildNodeVm(
        SavedNode sn,
        IReadOnlyDictionary<string, IReadOnlyList<(string Name, PinDataType Type)>>? columnLookup,
        CanvasNodeFamily allowedFamily = CanvasNodeFamily.Any
    )
    {
        if (!Enum.TryParse<NodeType>(sn.NodeType, out NodeType nodeType))
            return (null, $"Unknown node type '{sn.NodeType}' (not supported in this version)");

        if (!IsNodeTypeAllowed(nodeType, allowedFamily))
        {
            string familyName = allowedFamily == CanvasNodeFamily.Ddl ? "DDL" : "Query";
            return (
                null,
                $"Node '{sn.NodeType}' skipped due to canvas family mismatch (target: {familyName})"
            );
        }

        NodeViewModel vm;

        if (nodeType == NodeType.TableSource && sn.TableFullName is not null)
        {
            // Restore columns — prefer persisted columns, fall back to lookup catalog
            IEnumerable<(string, PinDataType)> cols = [];

            IReadOnlyDictionary<string, PinDataType>? lookupColumnTypes = null;
            if (
                columnLookup is not null
                && columnLookup.TryGetValue(
                    sn.TableFullName,
                    out IReadOnlyList<(string Name, PinDataType Type)>? foundLookup
                )
            )
            {
                lookupColumnTypes = foundLookup.ToDictionary(
                    c => c.Name,
                    c => c.Type,
                    StringComparer.OrdinalIgnoreCase
                );
            }

            // First: use persisted columns if available
            if (sn.Columns is { Count: > 0 })
            {
                cols = sn.Columns
                    .Select(c => (
                        c.Name,
                        ResolveSavedColumnType(c, lookupColumnTypes)
                    ))
                    .ToList();
            }
            // Fallback: use lookup catalog
            else if (
                columnLookup is not null
                && columnLookup.TryGetValue(
                    sn.TableFullName,
                    out IReadOnlyList<(string Name, PinDataType Type)>? found
                )
            )
            {
                cols = found;
            }

            vm = new NodeViewModel(sn.TableFullName, cols, new Point(sn.X, sn.Y));
        }
        else
        {
            NodeDefinition def;
            try
            {
                def = NodeDefinitionRegistry.Get(nodeType);
            }
            catch (Exception ex)
            {
                return (null, $"NodeDefinition not found for type '{nodeType}': {ex.Message}");
            }

            vm = new NodeViewModel(def, new Point(sn.X, sn.Y));
        }

        // Override ID to match saved ID (for connection mapping)
        // Since Id is init-only we use a workaround via reflection
        System.Reflection.PropertyInfo? idProp = typeof(NodeViewModel).GetProperty(
            nameof(NodeViewModel.Id)
        );
        if (idProp is null || !idProp.CanWrite)
            return (null, "Could not restore node ID (Id property is not writable).");

        try
        {
            idProp.SetValue(vm, sn.NodeId);
        }
        catch (Exception ex)
        {
            return (null, $"Could not restore node ID '{sn.NodeId}': {ex.Message}");
        }

        // Old files may not contain layer info; defer normalization in Deserialize.
        vm.ZOrder = sn.ZOrder ?? 0;

        vm.Alias = sn.Alias;

        foreach (KeyValuePair<string, string> kv in sn.Parameters)
            vm.Parameters[kv.Key] = kv.Value;

        NormalizeDeserializedNodeParameters(vm);

        foreach (KeyValuePair<string, string> kv in sn.PinLiterals)
            vm.PinLiterals[kv.Key] = kv.Value;

        if (sn.CteSubgraph is not null)
            vm.Parameters[CteSubgraphParameterKey] = JsonSerializer.Serialize(sn.CteSubgraph);

        if (sn.ViewSubgraph is not null)
        {
            if (!string.IsNullOrWhiteSpace(sn.ViewSubgraph.GraphJson))
                vm.Parameters[ViewSubgraphParameterKey] = sn.ViewSubgraph.GraphJson!;

            if (!string.IsNullOrWhiteSpace(sn.ViewSubgraph.EditorCanvasJson))
                vm.Parameters[ViewEditorCanvasParameterKey] = sn.ViewSubgraph.EditorCanvasJson!;

            bool hasFromTable = vm.Parameters.TryGetValue(ViewFromTableParameterKey, out string? existingFrom)
                && !string.IsNullOrWhiteSpace(existingFrom);

            if (!string.IsNullOrWhiteSpace(sn.ViewSubgraph.FromTable)
                && !hasFromTable)
            {
                vm.Parameters[ViewFromTableParameterKey] = sn.ViewSubgraph.FromTable!;
            }
        }

        return (vm, null);
    }

    private static void NormalizeDeserializedNodeParameters(NodeViewModel nodeVm)
    {
        nodeVm.Parameters.Remove("set_operator");
        nodeVm.Parameters.Remove("set_query");
        nodeVm.Parameters.Remove("import_order_terms");
        nodeVm.Parameters.Remove("import_group_terms");

        if (nodeVm.Type is NodeType.SubqueryExists or NodeType.SubqueryIn or NodeType.SubqueryScalar or NodeType.SubqueryReference)
            nodeVm.Parameters.Remove("query");
    }

    private static PinDataType ResolveSavedColumnType(
        SavedColumn column,
        IReadOnlyDictionary<string, PinDataType>? lookupColumnTypes
    )
    {
        if (Enum.TryParse<PinDataType>(column.Type, out PinDataType parsedType))
        {
            if (
                parsedType != PinDataType.ColumnRef
                || lookupColumnTypes is null
                || !lookupColumnTypes.TryGetValue(column.Name, out PinDataType resolved)
            )
            {
                return parsedType;
            }

            return resolved;
        }

        if (
            lookupColumnTypes is not null
            && lookupColumnTypes.TryGetValue(column.Name, out PinDataType fromLookup)
        )
        {
            return fromLookup;
        }

        return PinDataType.ColumnRef;
    }

    private static void ApplySavedColumnOrder(NodeViewModel nodeVm, IReadOnlyList<string> savedOrder)
    {
        for (int i = 0; i < savedOrder.Count; i++)
        {
            if (i >= nodeVm.OutputColumnOrder.Count)
                break;

            string key = savedOrder[i];
            int currentIndex = -1;
            for (int idx = 0; idx < nodeVm.OutputColumnOrder.Count; idx++)
            {
                if (nodeVm.OutputColumnOrder[idx].Key != key)
                    continue;

                currentIndex = idx;
                break;
            }

            if (currentIndex < 0 || currentIndex == i)
                continue;

            nodeVm.OutputColumnOrder.Move(currentIndex, i);
        }
    }

    private static bool IsNodeTypeAllowed(NodeType nodeType, CanvasNodeFamily family)
    {
        if (family == CanvasNodeFamily.Any)
            return true;

        bool isDdlNode = IsDdlNodeType(nodeType);
        return family == CanvasNodeFamily.Ddl ? isDdlNode : !isDdlNode;
    }

    private static bool IsDdlNodeType(NodeType nodeType) =>
        nodeType
            is NodeType.TableDefinition
                or NodeType.ColumnDefinition
                or NodeType.PrimaryKeyConstraint
                or NodeType.ForeignKeyConstraint
                or NodeType.UniqueConstraint
                or NodeType.CheckConstraint
                or NodeType.DefaultConstraint
                or NodeType.IndexDefinition
                or NodeType.ViewDefinition
                or NodeType.CreateTableOutput
                or NodeType.EnumTypeDefinition
                or NodeType.CreateTypeOutput
                or NodeType.SequenceDefinition
                or NodeType.CreateSequenceOutput
                or NodeType.CreateTableAsOutput
                or NodeType.CreateViewOutput
                or NodeType.AlterViewOutput
                or NodeType.AlterTableOutput
                or NodeType.CreateIndexOutput
                or NodeType.AddColumnOp
                or NodeType.DropColumnOp
                or NodeType.RenameColumnOp
                or NodeType.RenameTableOp
                or NodeType.DropTableOp
                or NodeType.AlterColumnTypeOp;
}
