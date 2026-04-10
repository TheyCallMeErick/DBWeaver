using DBWeaver.Core;
using DBWeaver.Registry;

namespace DBWeaver.Ddl;

/// <summary>
/// Emits provider SQL from compiled DDL expressions.
/// </summary>
public sealed class DdlGeneratorService(DatabaseProvider provider)
{
    private readonly DdlEmitContext _context = new(provider);

    public DdlGeneratorService(DatabaseProvider provider, IProviderRegistry registry)
        : this(provider)
    {
        _context = new DdlEmitContext(provider, registry);
    }

    public string Generate(IReadOnlyList<IDdlExpression> statements)
    {
        if (statements.Count == 0)
            return string.Empty;

        var emittedStatements = new List<string>(statements.Count);
        foreach (IDdlExpression statement in statements)
        {
            IReadOnlySet<DatabaseProvider>? supportedProviders = statement.SupportedProviders;
            if (supportedProviders is not null && !supportedProviders.Contains(_context.Provider))
            {
                throw new NotSupportedException(
                    $"{statement.GetType().Name} is not supported for provider '{_context.Provider}'."
                );
            }

            string sql = statement.Emit(_context);
            if (!string.IsNullOrWhiteSpace(sql))
                emittedStatements.Add(sql);
        }

        return string.Join("\n\n", emittedStatements);
    }
}
