using DBWeaver.Nodes;

namespace DBWeaver.Ddl.Compilers;

internal sealed class CreateTableAsOutputCompiler : IDdlOutputCompiler
{
    public NodeType OutputType => NodeType.CreateTableAsOutput;

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
                statements.Add(context.CompileCreateTableAsOutput(outputNode));
            }
            catch (Exception ex)
            {
                context.AddError("E-DDL-COMPILE-CREATETABLEAS", ex.Message, outputNode.Id);
            }
        }
    }
}
