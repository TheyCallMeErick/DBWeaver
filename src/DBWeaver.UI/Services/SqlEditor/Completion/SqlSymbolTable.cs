namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlSymbolTable
{
    private readonly Dictionary<string, SqlTableBindingSymbol> _bindingsByAlias;

    public SqlSymbolTable(
        IReadOnlyList<SqlTableBindingSymbol> bindingsInOrder,
        IReadOnlySet<string> cteNames)
    {
        BindingsInOrder = bindingsInOrder ?? [];
        CteNames = cteNames ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _bindingsByAlias = new Dictionary<string, SqlTableBindingSymbol>(StringComparer.OrdinalIgnoreCase);

        foreach (SqlTableBindingSymbol binding in BindingsInOrder)
        {
            if (!_bindingsByAlias.ContainsKey(binding.Alias))
                _bindingsByAlias[binding.Alias] = binding;
        }
    }

    public IReadOnlyList<SqlTableBindingSymbol> BindingsInOrder { get; }

    public IReadOnlySet<string> CteNames { get; }

    public bool TryResolveBinding(string qualifier, out SqlTableBindingSymbol? binding)
    {
        binding = null;
        if (string.IsNullOrWhiteSpace(qualifier))
            return false;

        if (_bindingsByAlias.TryGetValue(qualifier, out SqlTableBindingSymbol? resolved))
        {
            binding = resolved;
            return true;
        }

        foreach (SqlTableBindingSymbol item in BindingsInOrder)
        {
            string shortName = item.TableRef.Split('.').Last();
            if (string.Equals(shortName, qualifier, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.TableRef, qualifier, StringComparison.OrdinalIgnoreCase))
            {
                binding = item;
                return true;
            }
        }

        return false;
    }
}
