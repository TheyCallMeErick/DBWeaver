using System.Text.RegularExpressions;
using DBWeaver.Core;
using DBWeaver.Nodes;

namespace DBWeaver.Ddl;

public sealed partial class DdlGraphCompiler
{
    private void ValidateStaticRules(
        IReadOnlyList<NodeInstance> createTableOutputs,
        IReadOnlyList<NodeInstance> createTypeOutputs,
        IReadOnlyList<NodeInstance> createSequenceOutputs,
        IReadOnlyList<NodeInstance> createTableAsOutputs,
        IReadOnlyList<NodeInstance> createViewOutputs,
        IReadOnlyList<NodeInstance> alterViewOutputs)
    {
        ValidateCreateTypeOutputConnections(createTypeOutputs);
        ValidateCreateSequenceOutputConnections(createSequenceOutputs);
        ValidateCreateTableAsOutputConnections(createTableAsOutputs);
        ValidateCreateTableOutputConnections(createTableOutputs);
        ValidateCreateViewOutputConnections(createViewOutputs);
        ValidateAlterViewOutputConnections(alterViewOutputs);
        ValidateCreateTableHasColumns(createTableOutputs);
        ValidateColumnDefinitionNames();
        ValidateViewDefinitionNamesAndSelects();
        ValidateIdentityTypeCompatibility();
        ValidatePrimaryKeyMultiplicity();
        ValidateForeignKeyColumnSymmetry();
        ValidateForeignKeyCycles();
        ValidateProviderSpecificWarnings();
        ValidateEnumProviderWarnings();
        ValidatePostgresEnumTypeOutputs(createTypeOutputs);
        ValidateViewProviderWarnings();
        ValidateIdempotentModeWarnings(createTableOutputs, createTypeOutputs, createSequenceOutputs, createViewOutputs);
        ValidateLengthSensitiveTypes();
        ValidateDuplicateTableNames();
    }

    private void ValidateCreateTableAsOutputConnections(IReadOnlyList<NodeInstance> createTableAsOutputs)
    {
        foreach (NodeInstance output in createTableAsOutputs)
        {
            Connection? queryConn = _graph.GetSingleInputConnection(output.Id, "source_query");
            Connection? tableConn = _graph.GetSingleInputConnection(output.Id, "source_table");
            string selectSql = ReadParam(output, "SelectSql", "");

            if (queryConn is not null)
            {
                AddError(
                    "E-DDL-CREATETABLEAS-SOURCEQUERY-FORBIDDEN",
                    "Canvas DDL isolado: source_query nao pode receber conexao direta de nos de query.",
                    output.Id
                );
                continue;
            }

            if (tableConn is null && string.IsNullOrWhiteSpace(selectSql))
            {
                AddError(
                    "E-DDL-CREATETABLEAS-NO-SOURCE",
                    "CreateTableAsOutput requer source_table ou SelectSql.",
                    output.Id
                );
            }
        }
    }

    private void ValidateCreateSequenceOutputConnections(IReadOnlyList<NodeInstance> createSequenceOutputs)
    {
        foreach (NodeInstance output in createSequenceOutputs)
        {
            Connection? conn = _graph.GetSingleInputConnection(output.Id, "seq");
            if (conn is not null)
                continue;

            AddError(
                "E-DDL-OUTPUT-SEQUENCE-NOT-CONNECTED",
                "CreateSequenceOutput deve estar conectado a um SequenceDefinition",
                output.Id
            );
        }
    }

    private void ValidateIdempotentModeWarnings(
        IReadOnlyList<NodeInstance> createTableOutputs,
        IReadOnlyList<NodeInstance> createTypeOutputs,
        IReadOnlyList<NodeInstance> createSequenceOutputs,
        IReadOnlyList<NodeInstance> createViewOutputs)
    {
        foreach (NodeInstance output in createTableOutputs.Concat(createTypeOutputs).Concat(createSequenceOutputs).Concat(createViewOutputs))
        {
            DdlIdempotentMode mode = ReadIdempotentMode(output);

            if (mode == DdlIdempotentMode.DropAndCreate)
            {
                AddWarning(
                    "W-DDL-IDEMPOTENT-DROPANDCREATE",
                    "IdempotentMode=DropAndCreate inclui DROP destrutivo no script.",
                    output.Id
                );
            }

            if (output.Type == NodeType.CreateTypeOutput && mode == DdlIdempotentMode.DropAndCreate)
            {
                AddWarning(
                    "W-DDL-IDEMPOTENT-DROPTYPE-CASCADE",
                    "DROP TYPE IF EXISTS pode impactar colunas dependentes; avalie uso de CASCADE/ordem de execução.",
                    output.Id
                );
            }

            if (output.Type != NodeType.CreateViewOutput || mode != DdlIdempotentMode.IfNotExists)
                continue;

            if (_provider is DatabaseProvider.MySql or DatabaseProvider.SqlServer)
            {
                AddWarning(
                    "W-DDL-IDEMPOTENT-IFNOTEXISTS-VIEW-UNSUPPORTED",
                    $"Provider {_provider} não suporta CREATE VIEW IF NOT EXISTS nativo; modo será degradado.",
                    output.Id
                );
            }
        }
    }

    private void ValidateCreateViewOutputConnections(IReadOnlyList<NodeInstance> createViewOutputs)
    {
        foreach (NodeInstance output in createViewOutputs)
        {
            Connection? conn = _graph.GetSingleInputConnection(output.Id, "view");
            if (conn is not null)
                continue;

            AddError(
                "E-DDL-OUTPUT-VIEW-NOT-CONNECTED",
                "CreateViewOutput deve estar conectado a um ViewDefinition",
                output.Id
            );
        }
    }

    private void ValidateAlterViewOutputConnections(IReadOnlyList<NodeInstance> alterViewOutputs)
    {
        foreach (NodeInstance output in alterViewOutputs)
        {
            Connection? conn = _graph.GetSingleInputConnection(output.Id, "view");
            if (conn is not null)
                continue;

            AddError(
                "E-DDL-ALTERVIEW-OUTPUT-NOT-CONNECTED",
                "AlterViewOutput deve estar conectado a um ViewDefinition",
                output.Id
            );
        }
    }

    private void ValidateCreateTypeOutputConnections(IReadOnlyList<NodeInstance> createTypeOutputs)
    {
        foreach (NodeInstance output in createTypeOutputs)
        {
            Connection? conn = _graph.GetSingleInputConnection(output.Id, "type_def");
            if (conn is not null)
                continue;

            AddError(
                "E-DDL-OUTPUT-TYPEDEF-NOT-CONNECTED",
                "Nó de saída de tipo não conectado a nenhum EnumTypeDefinition",
                output.Id
            );
        }
    }

    private void ValidateCreateTableOutputConnections(IReadOnlyList<NodeInstance> createTableOutputs)
    {
        foreach (NodeInstance output in createTableOutputs)
        {
            Connection? conn = _graph.GetSingleInputConnection(output.Id, "table");
            if (conn is not null)
                continue;

            AddError(
                "E-DDL-OUTPUT-TABLE-NOT-CONNECTED",
                "Nó de saída não conectado a nenhuma tabela",
                output.Id
            );
        }
    }

    private void ValidateCreateTableHasColumns(IReadOnlyList<NodeInstance> createTableOutputs)
    {
        var tableIds = createTableOutputs
            .Select(outNode => _graph.GetSingleInputConnection(outNode.Id, "table")?.FromNodeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (string tableId in tableIds)
        {
            int columnCount = _graph.Connections.Count(c =>
                c.ToNodeId == tableId
                && c.ToPinName == "column"
                && _graph.NodeMap.TryGetValue(c.FromNodeId, out NodeInstance? from)
                && from.Type == NodeType.ColumnDefinition
            );

            if (columnCount > 0)
                continue;

            AddError("E-DDL-TABLE-NO-COLUMNS", "Tabela sem colunas definidas", tableId);
        }
    }

    private void ValidateColumnDefinitionNames()
    {
        foreach (NodeInstance col in _graph.Nodes.Where(n => n.Type == NodeType.ColumnDefinition))
        {
            string columnName = ReadParam(col, "ColumnName", "");
            if (!string.IsNullOrWhiteSpace(columnName))
                continue;

            AddError("E-DDL-COLUMN-NAME-BLANK", "Nome de coluna não pode estar em branco", col.Id);
        }
    }

    private void ValidateViewDefinitionNamesAndSelects()
    {
        foreach (NodeInstance view in _graph.Nodes.Where(n => n.Type == NodeType.ViewDefinition))
        {
            string viewName = ReadParam(view, "ViewName", "");
            if (string.IsNullOrWhiteSpace(viewName))
                AddError("E-DDL-VIEW-NAME-BLANK", "Nome da view não pode estar em branco", view.Id);

            string selectSql = ReadParam(view, "SelectSql", "");
            string subgraph = ReadParam(view, "ViewSubgraphGraphJson", "");
            if (string.IsNullOrWhiteSpace(selectSql) && string.IsNullOrWhiteSpace(subgraph))
                AddError("E-DDL-VIEW-SELECT-BLANK", "ViewDefinition requer SELECT compilado", view.Id);
        }
    }

    private void ValidateIdentityTypeCompatibility()
    {
        foreach (NodeInstance col in _graph.Nodes.Where(n => n.Type == NodeType.ColumnDefinition))
        {
            if (!ReadBoolParam(col, "IsIdentity", false))
                continue;

            string dataType = ReadParam(col, "DataType", "INT");
            if (IsIntegerDataType(dataType))
                continue;

            AddError(
                "E-DDL-IDENTITY-NONINTEGER",
                "Identity/AutoIncrement só é válido em colunas inteiras",
                col.Id
            );
        }
    }

    private void ValidatePrimaryKeyMultiplicity()
    {
        foreach (NodeInstance table in _graph.Nodes.Where(n => n.Type == NodeType.TableDefinition))
        {
            int pkCount = _graph.Connections
                .Where(c => c.ToNodeId == table.Id && c.ToPinName == "constraint")
                .Select(c => _graph.NodeMap[c.FromNodeId])
                .Count(n => n.Type == NodeType.PrimaryKeyConstraint);

            if (pkCount <= 1)
                continue;

            AddError(
                "E-DDL-PK-DUPLICATE",
                "Tabela não pode ter mais de uma PK",
                table.Id
            );
        }
    }

    private void ValidateForeignKeyColumnSymmetry()
    {
        foreach (NodeInstance fk in _graph.Nodes.Where(n => n.Type == NodeType.ForeignKeyConstraint))
        {
            int childCount = _graph.GetInputConnections(fk.Id, "child_column").Count;
            int parentCount = _graph.GetInputConnections(fk.Id, "parent_column").Count;
            if (childCount == parentCount)
                continue;

            AddError(
                "E-DDL-FK-COLUMN-COUNT",
                "FK composta: número de colunas filho ≠ colunas pai",
                fk.Id
            );
        }
    }

    private void ValidateForeignKeyCycles()
    {
        var columnToTable = _graph.Connections
            .Where(c => c.ToPinName == "column"
                && _graph.NodeMap.TryGetValue(c.FromNodeId, out NodeInstance? from)
                && from.Type == NodeType.ColumnDefinition
                && _graph.NodeMap.TryGetValue(c.ToNodeId, out NodeInstance? to)
                && to.Type == NodeType.TableDefinition)
            .ToDictionary(c => c.FromNodeId, c => c.ToNodeId, StringComparer.Ordinal);

        var edges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (NodeInstance fk in _graph.Nodes.Where(n => n.Type == NodeType.ForeignKeyConstraint))
        {
            string? childTableId = _graph.Connections
                .FirstOrDefault(c => c.FromNodeId == fk.Id && c.ToPinName == "constraint")
                ?.ToNodeId;
            if (string.IsNullOrWhiteSpace(childTableId))
                continue;

            string? parentTableId = _graph.GetInputConnections(fk.Id, "parent_column")
                .Select(c => c.FromNodeId)
                .Where(columnToTable.ContainsKey)
                .Select(colId => columnToTable[colId])
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(parentTableId) || parentTableId == childTableId)
                continue;

            if (!edges.TryGetValue(childTableId, out HashSet<string>? targets))
            {
                targets = new HashSet<string>(StringComparer.Ordinal);
                edges[childTableId] = targets;
            }

            targets.Add(parentTableId);
        }

        if (!HasDirectedCycle(edges))
            return;

        AddWarning(
            "W-DDL-FK-CYCLE",
            "Dependência circular de FK detectada — verifique a ordem de criação"
        );
    }

    private void ValidateProviderSpecificWarnings()
    {
        if (_provider == DatabaseProvider.SQLite)
        {
            foreach (NodeInstance op in _graph.Nodes.Where(n => n.Type == NodeType.AlterColumnTypeOp))
            {
                AddWarning(
                    "W-DDL-ALTERTYPE-SQLITE",
                    "SQLite não suporta ALTER COLUMN TYPE — considere recriar a tabela",
                    op.Id
                );
            }

            foreach (NodeInstance table in _graph.Nodes.Where(n => n.Type == NodeType.TableDefinition))
            {
                string tableComment = ReadParam(table, "Comment", "");
                if (!string.IsNullOrWhiteSpace(tableComment))
                {
                    AddWarning(
                        "W-DDL-COMMENT-SQLITE",
                        "SQLite não possui suporte nativo a comentários de tabela/coluna.",
                        table.Id
                    );
                }
            }

            foreach (NodeInstance col in _graph.Nodes.Where(n => n.Type == NodeType.ColumnDefinition))
            {
                string columnComment = ReadParam(col, "Comment", "");
                if (!string.IsNullOrWhiteSpace(columnComment))
                {
                    AddWarning(
                        "W-DDL-COMMENT-SQLITE",
                        "SQLite não possui suporte nativo a comentários de tabela/coluna.",
                        col.Id
                    );
                }
            }
        }

        if (_provider == DatabaseProvider.SqlServer)
        {
            foreach (NodeInstance op in _graph.Nodes.Where(n => n.Type == NodeType.DropColumnOp))
            {
                if (!ReadBoolParam(op, "IfExists", false))
                    continue;

                AddWarning(
                    "W-DDL-DROPCOLUMN-IFEXISTS-SQLSERVER",
                    "SQL Server não suporta IF EXISTS em DROP COLUMN — será removido",
                    op.Id
                );
            }
        }

        if (_provider is DatabaseProvider.MySql or DatabaseProvider.SQLite)
        {
            foreach (NodeInstance seq in _graph.Nodes.Where(n => n.Type == NodeType.SequenceDefinition))
            {
                AddWarning(
                    "W-DDL-SEQUENCE-UNSUPPORTED-PROVIDER",
                    $"Provider {_provider} não possui suporte nativo a CREATE SEQUENCE.",
                    seq.Id
                );
            }
        }

        foreach (NodeInstance op in _graph.Nodes.Where(n => n.Type == NodeType.DropTableOp))
        {
            AddWarning(
                "W-DDL-DROPTABLE-DESTRUCTIVE",
                "DROP TABLE é destrutivo e irreversível; confirme antes de executar.",
                op.Id
            );
        }

        if (_provider is DatabaseProvider.MySql or DatabaseProvider.SQLite)
        {
            foreach (NodeInstance op in _graph.Nodes.Where(n => n.Type == NodeType.RenameTableOp))
            {
                string newSchema = ReadParam(op, "NewSchema", "");
                if (string.IsNullOrWhiteSpace(newSchema))
                    continue;

                AddWarning(
                    "W-DDL-RENAMETABLE-NEWSCHEMA-UNSUPPORTED",
                    $"Provider {_provider} não suporta troca de schema no RenameTableOp.",
                    op.Id
                );
            }
        }
    }

    private void ValidateEnumProviderWarnings()
    {
        if (_provider is not DatabaseProvider.SqlServer and not DatabaseProvider.SQLite)
            return;

        foreach (NodeInstance col in _graph.Nodes.Where(n => n.Type == NodeType.ColumnDefinition))
        {
            bool hasTypeDefConnection = _graph.GetSingleInputConnection(col.Id, "type_def") is not null;
            string dataType = ReadParam(col, "DataType", "INT");
            bool usesEnumToken = dataType.Trim().StartsWith("ENUM", StringComparison.OrdinalIgnoreCase);

            if (!hasTypeDefConnection && !usesEnumToken)
                continue;

            AddWarning(
                "W-DDL-ENUM-UNSUPPORTED-PROVIDER",
                $"Provider {_provider} não possui suporte nativo a ENUM.",
                col.Id
            );
        }
    }

    private void ValidatePostgresEnumTypeOutputs(IReadOnlyList<NodeInstance> createTypeOutputs)
    {
        if (_provider != DatabaseProvider.Postgres)
            return;

        HashSet<string> emittedTypeNodeIds =
        [
            .. createTypeOutputs
                .Select(output => _graph.GetSingleInputConnection(output.Id, "type_def")?.FromNodeId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>(),
        ];

        foreach (NodeInstance col in _graph.Nodes.Where(n => n.Type == NodeType.ColumnDefinition))
        {
            Connection? typeConn = _graph.GetSingleInputConnection(col.Id, "type_def");
            if (typeConn is null)
                continue;

            NodeInstance typeNode = _graph.NodeMap[typeConn.FromNodeId];
            if (typeNode.Type != NodeType.EnumTypeDefinition)
                continue;

            if (emittedTypeNodeIds.Contains(typeConn.FromNodeId))
                continue;

            AddError(
                "E-DDL-ENUM-TYPE-NOT-EMITTED",
                "EnumTypeDefinition conectado à coluna requer CreateTypeOutput no PostgreSQL.",
                col.Id
            );
        }
    }

    private void ValidateViewProviderWarnings()
    {
        foreach (NodeInstance view in _graph.Nodes.Where(n => n.Type == NodeType.ViewDefinition))
        {
            bool isMaterialized = ReadBoolParam(view, "IsMaterialized", false);
            bool orReplace = ReadBoolParam(view, "OrReplace", false);

            if (isMaterialized && _provider is DatabaseProvider.SqlServer or DatabaseProvider.MySql or DatabaseProvider.SQLite)
            {
                AddError(
                    "E-DDL-VIEW-MATERIALIZED-UNSUPPORTED",
                    $"Provider {_provider} não suporta MATERIALIZED VIEW.",
                    view.Id
                );
            }

            if (_provider == DatabaseProvider.Postgres && isMaterialized && orReplace)
            {
                AddError(
                    "E-DDL-VIEW-MATERIALIZED-ORREPLACE",
                    "PostgreSQL não suporta CREATE OR REPLACE MATERIALIZED VIEW.",
                    view.Id
                );
            }
        }

        if (_provider == DatabaseProvider.SQLite)
        {
            foreach (NodeInstance output in _graph.Nodes.Where(n => n.Type == NodeType.AlterViewOutput))
            {
                AddWarning(
                    "W-DDL-ALTERVIEW-SQLITE",
                    "SQLite não suporta ALTER VIEW; será emitido DROP VIEW IF EXISTS + CREATE VIEW.",
                    output.Id
                );
            }
        }
    }

    private void ValidateLengthSensitiveTypes()
    {
        foreach (NodeInstance col in _graph.Nodes.Where(n => n.Type == NodeType.ColumnDefinition))
        {
            string dataType = ReadParam(col, "DataType", "INT");
            if (!NeedsLengthButMissing(dataType))
                continue;

            AddWarning(
                "W-DDL-TYPE-LENGTH-MISSING",
                "VARCHAR sem tamanho — padrão será aplicado (1 no SQL Server, sem limite no Postgres)",
                col.Id
            );
        }
    }

    private void ValidateDuplicateTableNames()
    {
        var names = new List<(string NodeId, string Name)>();

        foreach (NodeInstance table in _graph.Nodes.Where(n => n.Type == NodeType.TableDefinition))
        {
            string schema = ReadParam(table, "SchemaName", "public");
            string tableName = ReadParam(table, "TableName", "");
            if (string.IsNullOrWhiteSpace(tableName))
                continue;

            names.Add((table.Id, $"{schema}.{tableName}"));
        }

        foreach (NodeInstance tableRef in _graph.Nodes.Where(n => n.Type == NodeType.TableSource && !string.IsNullOrWhiteSpace(n.TableFullName)))
            names.Add((tableRef.Id, tableRef.TableFullName!));

        foreach (IGrouping<string, (string NodeId, string Name)> dup in names
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1))
        {
            foreach ((string nodeId, _) in dup)
                AddWarning("W-DDL-TABLE-NAME-DUPLICATE", "Nome de tabela duplicado no canvas", nodeId);
        }
    }

    private static bool NeedsLengthButMissing(string dataType)
    {
        string normalized = dataType.Trim();
        if (normalized.Contains('('))
            return false;

        return Regex.IsMatch(normalized, "^(VAR)?CHAR$|^NCHAR$|^NVARCHAR$", RegexOptions.IgnoreCase);
    }

    private static bool IsIntegerDataType(string dataType)
    {
        string normalized = dataType.Trim();
        int idx = normalized.IndexOf('(');
        if (idx > 0)
            normalized = normalized[..idx];

        normalized = normalized.ToUpperInvariant();
        return normalized is "INT" or "INTEGER" or "BIGINT" or "SMALLINT" or "TINYINT";
    }

    private static bool HasDirectedCycle(IReadOnlyDictionary<string, HashSet<string>> edges)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new HashSet<string>(StringComparer.Ordinal);

        bool Dfs(string node)
        {
            if (!visited.Add(node))
                return stack.Contains(node);

            stack.Add(node);
            if (edges.TryGetValue(node, out HashSet<string>? nextNodes))
            {
                foreach (string next in nextNodes)
                {
                    if (Dfs(next))
                        return true;
                }
            }

            stack.Remove(node);
            return false;
        }

        foreach (string node in edges.Keys)
        {
            if (Dfs(node))
                return true;
        }

        return false;
    }

    private void AddWarning(string code, string message, string? nodeId = null)
    {
        _diagnostics.Add(new DdlCompileDiagnostic(code, DdlDiagnosticSeverity.Warning, message, nodeId));
    }

    private void AddError(string code, string message, string? nodeId = null)
    {
        _diagnostics.Add(new DdlCompileDiagnostic(code, DdlDiagnosticSeverity.Error, message, nodeId));
    }
}
