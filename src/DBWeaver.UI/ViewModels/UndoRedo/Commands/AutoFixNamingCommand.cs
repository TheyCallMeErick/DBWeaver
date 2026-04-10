using DBWeaver.UI.ViewModels.Validation.Conventions;

namespace DBWeaver.UI.ViewModels.UndoRedo.Commands;

public sealed class AutoFixNamingCommand : ICanvasCommand
{
    private readonly List<(NodeViewModel Node, string? OldAlias, string NewAlias)> _renames = [];

    public string Description =>
        _renames.Count == 1
            ? $"Fix alias '{_renames[0].OldAlias}' → '{_renames[0].NewAlias}'"
            : $"Fix {_renames.Count} alias name(s)";

    public AutoFixNamingCommand(
        IEnumerable<NodeViewModel> nodes,
        NamingConventionPolicy? policy = null,
        IAliasConventionRegistry? registry = null)
    {
        policy ??= NamingConventionPolicy.Default;
        registry ??= AliasConventionRegistry.CreateDefault();

        foreach (NodeViewModel node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Alias))
                continue;
            IReadOnlyList<AliasViolation> violations =
                NamingConventionValidator.CheckAlias(node.Alias!, policy, registry);
            if (violations.Count == 0)
                continue;
            string fixed_ = NamingConventionValidator.NormalizeAlias(node.Alias!, policy, registry);
            if (fixed_ != node.Alias)
                _renames.Add((node, node.Alias, fixed_));
        }
    }

    public bool HasChanges => _renames.Count > 0;

    public void Execute(CanvasViewModel canvas)
    {
        foreach ((NodeViewModel node, string? _, string newAlias) in _renames)
            node.Alias = newAlias;
    }

    public void Undo(CanvasViewModel canvas)
    {
        foreach ((NodeViewModel node, string? oldAlias, string _) in _renames)
            node.Alias = oldAlias;
    }
}
