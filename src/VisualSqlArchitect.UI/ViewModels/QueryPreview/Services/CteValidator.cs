using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.QueryEngine;
using VisualSqlArchitect.Registry;
using VisualSqlArchitect.UI.Serialization;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VisualSqlArchitect.UI.ViewModels.QueryPreview.Services;

public sealed class CteValidator : IGraphValidator
{
    private readonly CanvasViewModel _canvas;
    private readonly IReadOnlyDictionary<string, string> _cteDefinitionNamesById;
    private readonly IReadOnlyList<CteBinding> _compiledCtes;

    public CteValidator(
        CanvasViewModel canvas,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById,
        IReadOnlyList<CteBinding> compiledCtes)
    {
        _canvas = canvas;
        _cteDefinitionNamesById = cteDefinitionNamesById;
        _compiledCtes = compiledCtes;
    }

    public void Validate(List<string> errors)
    {
        ValidateCteDefinitions(errors);
        ValidateCteSources(errors);
    }

    private void ValidateCteDefinitions(List<string> errors)
    {
        var names = new List<string>();

        IEnumerable<NodeViewModel> cteDefinitions = _canvas.Nodes.Where(n => n.Type == NodeType.CteDefinition);

        foreach (NodeViewModel definition in cteDefinitions)
        {
            string? name = ResolveDefinitionName(definition);
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add($"CTE Definition node '{definition.Title}' is missing a CTE name.");
                continue;
            }

            string trimmed = name.Trim();
            names.Add(trimmed);

            if (!IsValidCteIdentifier(trimmed))
            {
                errors.Add(
                    $"CTE name '{trimmed}' is invalid. Use letters, numbers, and underscore, starting with a letter or underscore."
                );
            }
        }

        foreach (string duplicate in names
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key))
        {
            errors.Add($"Duplicate CTE definition name: '{duplicate}'.");
        }
    }

    private void ValidateCteSources(List<string> errors)
    {
        IReadOnlyList<NodeViewModel> cteSourceNodes = _canvas.Nodes.Where(n => n.Type == NodeType.CteSource).ToList();

        // Only CTEs that were successfully compiled into the graph are "reachable".
        // A CTE definition that exists on the canvas but isn't connected to the main
        // query path will not appear in _compiledCtes, matching the original behaviour.
        var defined = new HashSet<string>(
            _compiledCtes.Select(c => c.Name),
            StringComparer.OrdinalIgnoreCase
        );

        // Check for duplicate compiled CTE names
        var duplicates = _compiledCtes
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (string duplicate in duplicates)
            errors.Add($"Duplicate CTE definition name: '{duplicate}'.");

        foreach (NodeViewModel source in cteSourceNodes)
        {
            string? name = ResolveCteSourceName(source);

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add($"CTE Source node '{source.Title}' is missing a CTE name.");
                continue;
            }

            if (!defined.Contains(name))
                errors.Add($"CTE Source references undefined CTE '{name}'.");
        }
    }

    private static string? ReadCteName(IReadOnlyDictionary<string, string> parameters)
    {
        if (
            parameters.TryGetValue("name", out string? name)
            && !string.IsNullOrWhiteSpace(name)
        )
        {
            return name.Trim();
        }

        if (
            parameters.TryGetValue("cte_name", out string? legacyName)
            && !string.IsNullOrWhiteSpace(legacyName)
        )
        {
            return legacyName.Trim();
        }

        return null;
    }

    private static bool IsValidCteIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);
    }

    private string? ResolveCteSourceName(NodeViewModel cteSource)
    {
        string? byTextInput = QueryGraphHelpers.ResolveTextInput(_canvas, cteSource, "cte_name_text");
        if (!string.IsNullOrWhiteSpace(byTextInput))
            return byTextInput;

        string? byParam = ReadCteName(cteSource.Parameters);
        if (!string.IsNullOrWhiteSpace(byParam))
            return byParam;

        ConnectionViewModel? byConnection = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == cteSource
            && c.ToPin?.Name == "cte"
            && c.FromPin.Owner.Type == NodeType.CteDefinition
            && _cteDefinitionNamesById.ContainsKey(c.FromPin.Owner.Id)
        );

        if (byConnection is null)
            return null;

        return _cteDefinitionNamesById[byConnection.FromPin.Owner.Id];
    }

    private string? ResolveCteSourceAlias(NodeViewModel cteSource)
    {
        string? byTextInput = QueryGraphHelpers.ResolveTextInput(_canvas, cteSource, "alias_text");
        if (!string.IsNullOrWhiteSpace(byTextInput))
            return byTextInput.Trim();

        if (
            cteSource.Parameters.TryGetValue("alias", out string? byParam)
            && !string.IsNullOrWhiteSpace(byParam)
        )
        {
            return byParam.Trim();
        }

        return null;
    }

    private string? ResolveDefinitionName(NodeViewModel def)
    {
        string? byTextInput = QueryGraphHelpers.ResolveTextInput(_canvas, def, "name_text");
        if (!string.IsNullOrWhiteSpace(byTextInput))
            return byTextInput;

        return ReadCteName(def.Parameters);
    }
}
