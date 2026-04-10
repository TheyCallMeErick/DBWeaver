using DBWeaver.Nodes;

namespace DBWeaver.Ddl.Compilers;

internal sealed class DdlOutputCompilationContext
{
    public DdlOutputCompilationContext(
        NodeGraph graph,
        Func<NodeInstance, DdlIdempotentMode> readIdempotentMode,
        Func<NodeInstance, DdlIdempotentMode, CreateEnumTypeExpr> compileEnumTypeDefinition,
        Func<NodeInstance, DdlIdempotentMode, CreateSequenceExpr> compileSequenceDefinition,
        Func<NodeInstance, DdlIdempotentMode, CreateTableExpr> compileTableDefinition,
        Func<NodeInstance, CreateTableAsExpr> compileCreateTableAsOutput,
        Func<NodeInstance, DdlIdempotentMode, CreateViewExpr> compileCreateViewDefinition,
        Func<NodeInstance, AlterViewExpr> compileAlterViewDefinition,
        Func<NodeInstance, CreateIndexExpr> compileIndexDefinition,
        Func<NodeInstance, NodeInstance, AlterTableExpr> compileAlterTableOutput,
        Action<string, string, string?> addError
    )
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(readIdempotentMode);
        ArgumentNullException.ThrowIfNull(compileEnumTypeDefinition);
        ArgumentNullException.ThrowIfNull(compileSequenceDefinition);
        ArgumentNullException.ThrowIfNull(compileTableDefinition);
        ArgumentNullException.ThrowIfNull(compileCreateTableAsOutput);
        ArgumentNullException.ThrowIfNull(compileCreateViewDefinition);
        ArgumentNullException.ThrowIfNull(compileAlterViewDefinition);
        ArgumentNullException.ThrowIfNull(compileIndexDefinition);
        ArgumentNullException.ThrowIfNull(compileAlterTableOutput);
        ArgumentNullException.ThrowIfNull(addError);
        Graph = graph;
        ReadIdempotentMode = readIdempotentMode;
        CompileEnumTypeDefinition = compileEnumTypeDefinition;
        CompileSequenceDefinition = compileSequenceDefinition;
        CompileTableDefinition = compileTableDefinition;
        CompileCreateTableAsOutput = compileCreateTableAsOutput;
        CompileCreateViewDefinition = compileCreateViewDefinition;
        CompileAlterViewDefinition = compileAlterViewDefinition;
        CompileIndexDefinition = compileIndexDefinition;
        CompileAlterTableOutput = compileAlterTableOutput;
        AddError = addError;
    }

    public NodeGraph Graph { get; }
    public Func<NodeInstance, DdlIdempotentMode> ReadIdempotentMode { get; }
    public Func<NodeInstance, DdlIdempotentMode, CreateEnumTypeExpr> CompileEnumTypeDefinition { get; }
    public Func<NodeInstance, DdlIdempotentMode, CreateSequenceExpr> CompileSequenceDefinition { get; }
    public Func<NodeInstance, DdlIdempotentMode, CreateTableExpr> CompileTableDefinition { get; }
    public Func<NodeInstance, CreateTableAsExpr> CompileCreateTableAsOutput { get; }
    public Func<NodeInstance, DdlIdempotentMode, CreateViewExpr> CompileCreateViewDefinition { get; }
    public Func<NodeInstance, AlterViewExpr> CompileAlterViewDefinition { get; }
    public Func<NodeInstance, CreateIndexExpr> CompileIndexDefinition { get; }
    public Func<NodeInstance, NodeInstance, AlterTableExpr> CompileAlterTableOutput { get; }
    public Action<string, string, string?> AddError { get; }
}
