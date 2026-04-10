using DBWeaver.Nodes;

namespace DBWeaver.Ddl.Compilers;

internal sealed class CreateSequenceOutputCompiler : IDdlOutputCompiler
{
    public NodeType OutputType => NodeType.CreateSequenceOutput;

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
                Connection? input = context.Graph.GetSingleInputConnection(outputNode.Id, "seq");
                if (input is null)
                    continue;

                NodeInstance sequenceNode = context.Graph.NodeMap[input.FromNodeId];
                if (sequenceNode.Type != NodeType.SequenceDefinition)
                {
                    context.AddError(
                        "E-DDL-OUTPUT-SEQUENCE-TYPE",
                        "CreateSequenceOutput.seq deve vir de SequenceDefinition.",
                        outputNode.Id
                    );
                    continue;
                }

                statements.Add(context.CompileSequenceDefinition(sequenceNode, idempotentMode));
            }
            catch (Exception ex)
            {
                context.AddError("E-DDL-COMPILE-CREATESEQUENCE", ex.Message, outputNode.Id);
            }
        }
    }
}
