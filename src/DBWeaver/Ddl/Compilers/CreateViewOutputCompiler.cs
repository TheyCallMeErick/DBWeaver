using DBWeaver.Nodes;

namespace DBWeaver.Ddl.Compilers;

internal sealed class CreateViewOutputCompiler : IDdlOutputCompiler
{
    public NodeType OutputType => NodeType.CreateViewOutput;

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
                Connection? input = context.Graph.GetSingleInputConnection(outputNode.Id, "view");
                if (input is null)
                    continue;

                NodeInstance viewNode = context.Graph.NodeMap[input.FromNodeId];
                if (viewNode.Type != NodeType.ViewDefinition)
                {
                    context.AddError(
                        "E-DDL-OUTPUT-VIEW-TYPE",
                        "CreateViewOutput.view deve vir de ViewDefinition.",
                        outputNode.Id
                    );
                    continue;
                }

                statements.Add(context.CompileCreateViewDefinition(viewNode, idempotentMode));
            }
            catch (Exception ex)
            {
                context.AddError("E-DDL-COMPILE-CREATEVIEW", ex.Message, outputNode.Id);
            }
        }
    }
}
