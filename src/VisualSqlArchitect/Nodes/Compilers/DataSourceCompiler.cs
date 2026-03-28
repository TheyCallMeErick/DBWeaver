using VisualSqlArchitect.Expressions;
using VisualSqlArchitect.Nodes.Definitions;

namespace VisualSqlArchitect.Nodes.Compilers;

/// <summary>
/// Compiles data source nodes: TableSource and Alias.
/// These are the entry points to the query — the FROM clause sources.
/// </summary>
public sealed class DataSourceCompiler : INodeCompiler
{
    public bool CanCompile(NodeType nodeType) => nodeType is NodeType.TableSource or NodeType.Alias;

    public ISqlExpression Compile(NodeInstance node, INodeCompilationContext ctx, string pinName)
    {
        return node.Type switch
        {
            NodeType.TableSource => CompileTableSource(node, pinName),
            NodeType.Alias => CompileAlias(node, ctx),
            _ => throw new NotSupportedException($"Cannot compile {node.Type}"),
        };
    }

    private static ISqlExpression CompileTableSource(NodeInstance node, string pinName)
    {
        if (node.TableFullName is null)
            throw new InvalidOperationException(
                $"TableSource node '{node.Id}' has no TableFullName."
            );

        // Use the last segment after '.' as the alias; full name stays in FROM
        string[] parts = node.TableFullName.Split('.');
        string alias = parts.Last();
        return new ColumnExpr(alias, pinName);
    }

    private static ISqlExpression CompileAlias(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression inner = ctx.ResolveInput(node.Id, "expression");
        string aliasName =
            node.Parameters.TryGetValue("alias", out string? a) && !string.IsNullOrWhiteSpace(a)
                ? a.Trim()
                : "alias";
        return new AliasExpr(inner, aliasName);
    }
}
