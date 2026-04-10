using DBWeaver.Nodes;

namespace DBWeaver.Ddl.Compilers;

internal sealed class CreateTypeOutputCompiler : IDdlOutputCompiler
{
    public NodeType OutputType => NodeType.CreateTypeOutput;

    public void Compile(
        IReadOnlyList<NodeInstance> outputNodes,
        DdlOutputCompilationContext context,
        ICollection<IDdlExpression> statements
    )
    {
        foreach (NodeInstance outputNode in outputNodes)
        {
            try
            {
                DdlIdempotentMode idempotentMode = context.ReadIdempotentMode(outputNode);
                Connection? input = context.Graph.GetSingleInputConnection(outputNode.Id, "type_def");
                if (input is null)
                    continue;

                NodeInstance typeNode = context.Graph.NodeMap[input.FromNodeId];
                if (typeNode.Type != NodeType.EnumTypeDefinition)
                {
                    context.AddError(
                        "E-DDL-OUTPUT-TYPEDEF-TYPE",
                        "CreateTypeOutput.type_def deve vir de EnumTypeDefinition.",
                        outputNode.Id
                    );
                    continue;
                }

                statements.Add(context.CompileEnumTypeDefinition(typeNode, idempotentMode));
            }
            catch (Exception ex)
            {
                context.AddError("E-DDL-COMPILE-CREATETYPE", ex.Message, outputNode.Id);
            }
        }
    }
}
