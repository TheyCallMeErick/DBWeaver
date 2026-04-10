using DBWeaver.Nodes;

namespace DBWeaver.Ddl.Compilers;

internal interface IDdlOutputCompiler
{
    NodeType OutputType { get; }

    void Compile(
        IReadOnlyList<NodeInstance> outputNodes,
        DdlOutputCompilationContext context,
        ICollection<IDdlExpression> statements
    );
}
