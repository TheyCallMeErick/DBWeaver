using DBWeaver.Nodes;

namespace DBWeaver.Ddl.Compilers;

internal sealed class AlterTableOutputCompiler : IDdlOutputCompiler
{
    public NodeType OutputType => NodeType.AlterTableOutput;

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
                Connection? tableInput = context.Graph.GetSingleInputConnection(outputNode.Id, "table");
                if (tableInput is null)
                {
                    context.AddError(
                        "E-DDL-ALTER-OUTPUT-TABLE",
                        "AlterTableOutput requires a connected 'table' input.",
                        outputNode.Id
                    );
                    continue;
                }

                NodeInstance tableNode = context.Graph.NodeMap[tableInput.FromNodeId];
                if (tableNode.Type != NodeType.TableDefinition)
                {
                    context.AddError(
                        "E-DDL-ALTER-OUTPUT-TABLE-TYPE",
                        "AlterTableOutput.table must come from TableDefinition.",
                        outputNode.Id
                    );
                    continue;
                }

                statements.Add(context.CompileAlterTableOutput(outputNode, tableNode));
            }
            catch (Exception ex)
            {
                context.AddError("E-DDL-COMPILE-ALTER", ex.Message, outputNode.Id);
            }
        }
    }
}
