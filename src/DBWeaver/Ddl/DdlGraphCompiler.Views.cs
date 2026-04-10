using System.Text.Json;
using DBWeaver.Core;
using DBWeaver.Nodes;
using DBWeaver.QueryEngine;

namespace DBWeaver.Ddl;

public sealed partial class DdlGraphCompiler
{
    private CreateViewExpr CompileCreateViewDefinition(NodeInstance viewNode, DdlIdempotentMode idempotentMode)
    {
        string schema = ResolveTextFromInputOrParameter(viewNode, "schema_text", "Schema");
        if (string.IsNullOrWhiteSpace(schema))
            schema = "public";

        string viewName = ResolveTextFromInputOrParameter(viewNode, "view_name_text", "ViewName");
        bool orReplace = ReadBoolParam(viewNode, "OrReplace", false);
        bool isMaterialized = ReadBoolParam(viewNode, "IsMaterialized", false);
        string selectSql = ResolveViewSelectSql(viewNode);

        if (_provider != DatabaseProvider.Postgres && isMaterialized)
            throw new InvalidOperationException("Materialized view é suportada apenas no PostgreSQL.");

        if (_provider == DatabaseProvider.Postgres && isMaterialized && orReplace)
            throw new InvalidOperationException("PostgreSQL não suporta CREATE OR REPLACE MATERIALIZED VIEW.");

        if (string.IsNullOrWhiteSpace(viewName))
            throw new InvalidOperationException("ViewDefinition requires ViewName.");

        return new CreateViewExpr(schema, viewName, orReplace, isMaterialized, selectSql, idempotentMode);
    }

    private AlterViewExpr CompileAlterViewDefinition(NodeInstance viewNode)
    {
        string schema = ResolveTextFromInputOrParameter(viewNode, "schema_text", "Schema");
        if (string.IsNullOrWhiteSpace(schema))
            schema = "public";

        string viewName = ResolveTextFromInputOrParameter(viewNode, "view_name_text", "ViewName");
        string selectSql = ResolveViewSelectSql(viewNode);

        if (string.IsNullOrWhiteSpace(viewName))
            throw new InvalidOperationException("ViewDefinition requires ViewName.");

        return new AlterViewExpr(schema, viewName, selectSql);
    }

    private static string NormalizeViewSelect(NodeInstance viewNode)
    {
        string selectSql = ReadParam(viewNode, "SelectSql", "");
        if (string.IsNullOrWhiteSpace(selectSql))
            throw new InvalidOperationException("ViewDefinition requires SelectSql.");

        return selectSql.Trim().TrimEnd(';');
    }

    private string ResolveViewSelectSql(NodeInstance viewNode)
    {
        if (TryCompileViewSubgraphSql(viewNode, out string? sql) && !string.IsNullOrWhiteSpace(sql))
            return sql!;

        string wiredSql = ResolveTextFromInputOrParameter(viewNode, "select_sql_text", "SelectSql");
        if (!string.IsNullOrWhiteSpace(wiredSql))
            return wiredSql.Trim().TrimEnd(';');

        return NormalizeViewSelect(viewNode);
    }

    private bool TryCompileViewSubgraphSql(NodeInstance viewNode, out string? sql)
    {
        sql = null;
        string payload = ReadParam(viewNode, "ViewSubgraphGraphJson", "");
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        NodeGraph? subgraph;
        try
        {
            subgraph = JsonSerializer.Deserialize<NodeGraph>(payload);
        }
        catch
        {
            AddError(
                "E-DDL-VIEW-SUBGRAPH-JSON",
                "ViewDefinition.ViewSubgraphGraphJson inválido.",
                viewNode.Id
            );
            return false;
        }

        if (subgraph is null || subgraph.Nodes.Count == 0)
        {
            AddError(
                "E-DDL-VIEW-SUBGRAPH-EMPTY",
                "Subcanvas de view está vazio.",
                viewNode.Id
            );
            return false;
        }

        try
        {
            var queryService = QueryGeneratorService.Create(_provider);
            GeneratedQuery generated = queryService.Generate(subgraph);
            sql = generated.Sql.Trim().TrimEnd(';');
            return true;
        }
        catch (Exception ex)
        {
            AddError(
                "E-DDL-VIEW-SUBGRAPH-COMPILE",
                ex.Message,
                viewNode.Id
            );
            return false;
        }
    }
}
