using DBWeaver.Nodes;

namespace DBWeaver.Ddl.Compilers;

internal sealed class CreateIndexOutputCompiler : IDdlOutputCompiler
{
    public NodeType OutputType => NodeType.CreateIndexOutput;

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
                Connection? input = context.Graph.GetSingleInputConnection(outputNode.Id, "index");
                if (input is null)
                {
                    context.AddError(
                        "E-DDL-INDEX-OUTPUT-NOT-CONNECTED",
                        "CreateIndexOutput requires a connected 'index' input.",
                        outputNode.Id
                    );
                    continue;
                }

                NodeInstance indexNode = context.Graph.NodeMap[input.FromNodeId];
                if (indexNode.Type != NodeType.IndexDefinition)
                {
                    context.AddError(
                        "E-DDL-INDEX-OUTPUT-TYPE",
                        "CreateIndexOutput.index must come from IndexDefinition.",
                        outputNode.Id
                    );
                    continue;
                }

                statements.Add(context.CompileIndexDefinition(indexNode));
            }
            catch (Exception ex)
            {
                context.AddError("E-DDL-COMPILE-CREATEINDEX", ex.Message, outputNode.Id);
            }
        }
    }
}
