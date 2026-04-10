using DBWeaver.Core;
using DBWeaver.Ddl;
using DBWeaver.Nodes;
using DBWeaver.Compilation;

namespace DBWeaver.Compilation;

/// <summary>
/// Wraps <see cref="DBWeaver.Ddl.DdlGraphCompiler"/> to provide a uniform compilation interface.
/// Compiles a graph into DDL statements (CREATE TABLE, CREATE VIEW, etc.).
/// </summary>
public sealed class DdlGraphCompilerAdapter : IGraphCompiler<IReadOnlyList<IDdlExpression>>
{
    private readonly DatabaseProvider _provider;

    public DdlGraphCompilerAdapter(DatabaseProvider provider = DatabaseProvider.SqlServer)
    {
        _provider = provider;
    }

    /// <summary>
    /// Attempts to compile the given graph into DDL expressions.
    /// </summary>
    public bool TryCompile(
        NodeGraph graph,
        out IReadOnlyList<IDdlExpression>? output,
        out IReadOnlyList<string> errors)
    {
        output = null;
        errors = [];

        if (graph is null)
        {
            errors = ["Graph is null"];
            return false;
        }

        try
        {
            var compiler = new DdlGraphCompiler(graph, _provider);
            var result = compiler.CompileWithDiagnostics();

            if (result.Diagnostics.Any(d => d.Severity == DdlDiagnosticSeverity.Error))
            {
                errors = result.Diagnostics
                    .Where(d => d.Severity == DdlDiagnosticSeverity.Error)
                    .Select(d => d.Message)
                    .ToList();
                return false;
            }

            output = result.Statements;
            return true;
        }
        catch (Exception ex)
        {
            errors = [ex.Message];
            return false;
        }
    }
}
