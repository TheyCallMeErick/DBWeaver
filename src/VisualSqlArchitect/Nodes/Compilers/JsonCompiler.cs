using VisualSqlArchitect.Expressions;
using VisualSqlArchitect.Nodes.Definitions;

namespace VisualSqlArchitect.Nodes.Compilers;

/// <summary>
/// Compiles JSON manipulation nodes: JSON Extract, JSON Array Length.
/// These nodes extract and transform JSON data using SQL JSON functions.
/// </summary>
public sealed class JsonCompiler : INodeCompiler
{
    public bool CanCompile(NodeType nodeType) =>
        nodeType is NodeType.JsonExtract or NodeType.JsonValue or NodeType.JsonArrayLength;

    public ISqlExpression Compile(NodeInstance node, INodeCompilationContext ctx, string pinName)
    {
        return node.Type switch
        {
            NodeType.JsonExtract or NodeType.JsonValue => CompileJsonExtract(node, ctx),
            NodeType.JsonArrayLength => CompileJsonArrayLength(node, ctx),

            _ => throw new NotSupportedException($"Cannot compile {node.Type}"),
        };
    }

    private static ISqlExpression CompileJsonExtract(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression json = ctx.ResolveInput(node.Id, "json");
        string path = node.Parameters.TryGetValue("path", out string? p) ? p : "$";

        // Provider-specific JSON extraction
        return ctx.EmitContext.Provider switch
        {
            VisualSqlArchitect.Core.DatabaseProvider.MySql => new RawSqlExpr(
                $"JSON_EXTRACT({json.Emit(ctx.EmitContext)}, '{path}')",
                PinDataType.Text
            ),

            VisualSqlArchitect.Core.DatabaseProvider.Postgres => new RawSqlExpr(
                $"{json.Emit(ctx.EmitContext)}::jsonb @> '{path}'",
                PinDataType.Text
            ),

            _ => new RawSqlExpr(
                $"JSON_EXTRACT({json.Emit(ctx.EmitContext)}, '{path}')",
                PinDataType.Text
            ),
        };
    }

    private static ISqlExpression CompileJsonArrayLength(
        NodeInstance node,
        INodeCompilationContext ctx
    )
    {
        ISqlExpression json = ctx.ResolveInput(node.Id, "json");
        string path = node.Parameters.TryGetValue("path", out string? p) ? p : "$";

        // Provider-specific array length
        return ctx.EmitContext.Provider switch
        {
            VisualSqlArchitect.Core.DatabaseProvider.MySql => new RawSqlExpr(
                $"JSON_LENGTH({json.Emit(ctx.EmitContext)}, '{path}')",
                PinDataType.Number
            ),

            VisualSqlArchitect.Core.DatabaseProvider.Postgres => new RawSqlExpr(
                $"jsonb_array_length({json.Emit(ctx.EmitContext)})",
                PinDataType.Number
            ),

            _ => new RawSqlExpr($"JSON_LENGTH({json.Emit(ctx.EmitContext)})", PinDataType.Number),
        };
    }
}
