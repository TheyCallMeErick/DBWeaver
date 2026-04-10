using DBWeaver.Nodes;

namespace DBWeaver.Ddl.Compilers;

internal sealed class CreateTableOutputCompiler : IDdlOutputCompiler
{
    public NodeType OutputType => NodeType.CreateTableOutput;

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
                Connection? input = context.Graph.GetSingleInputConnection(outputNode.Id, "table");
                if (input is null)
                    continue;

                NodeInstance tableNode = context.Graph.NodeMap[input.FromNodeId];
                if (tableNode.Type != NodeType.TableDefinition)
                {
                    context.AddError(
                        "E-DDL-OUTPUT-TABLE-TYPE",
                        "CreateTableOutput.table deve vir de TableDefinition.",
                        outputNode.Id
                    );
                    continue;
                }

                statements.Add(context.CompileTableDefinition(tableNode, idempotentMode));
            }
            catch (Exception ex)
            {
                context.AddError("E-DDL-COMPILE-CREATETABLE", ex.Message, outputNode.Id);
            }
        }
    }
}
