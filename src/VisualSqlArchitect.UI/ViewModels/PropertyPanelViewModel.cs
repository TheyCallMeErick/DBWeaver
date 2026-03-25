using System.Collections.ObjectModel;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels;

// ─── Parameter row ────────────────────────────────────────────────────────────

/// <summary>
/// A single editable parameter row in the property panel.
/// Bound to one <see cref="NodeParameter"/> on the selected node.
/// </summary>
public sealed class ParameterRowViewModel : ViewModelBase
{
    private string? _value;
    private bool    _isDirty;

    public string        Name        { get; }
    public ParameterKind Kind        { get; }
    public string?       Description { get; }
    public IReadOnlyList<string>? EnumValues { get; }

    // ── Visibility helpers (one True per kind) ────────────────────────────────
    public bool IsText      => Kind is ParameterKind.Text or ParameterKind.JsonPath;
    public bool IsNumber    => Kind == ParameterKind.Number;
    public bool IsBoolean   => Kind == ParameterKind.Boolean;
    public bool IsEnum      => Kind is ParameterKind.Enum or ParameterKind.CastType;

    public string? Value
    {
        get => _value;
        set
        {
            if (Set(ref _value, value))
                IsDirty = true;
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => Set(ref _isDirty, value);
    }

    public ParameterRowViewModel(NodeParameter param, string? currentValue)
    {
        Name        = param.Name;
        Kind        = param.Kind;
        Description = param.Description;
        EnumValues  = param.EnumValues;
        _value      = currentValue ?? param.DefaultValue;
    }

    public void MarkClean() => IsDirty = false;
}

// ─── Pin info row ─────────────────────────────────────────────────────────────

public sealed class PinInfoRowViewModel(PinViewModel pin)
{
    public string Name      => pin.Name;
    public string TypeLabel => pin.DataType.ToString();
    public string Direction => pin.Direction.ToString();
    public bool   Connected => pin.IsConnected;
    public Avalonia.Media.Color Color => pin.PinColor;
    public Avalonia.Media.SolidColorBrush ColorBrush => pin.PinBrush;
}

// ─── Property panel ──────────────────────────────────────────────────────────

/// <summary>
/// Bound to the right-side panel. Shows details and editable parameters for
/// the currently selected node, or a multi-selection summary.
/// </summary>
public sealed class PropertyPanelViewModel : ViewModelBase
{
    private NodeViewModel?  _selectedNode;
    private bool            _isVisible;
    private string          _panelTitle  = "Properties";

    private readonly UndoRedoStack _undo;

    // ── Sub-collections ───────────────────────────────────────────────────────
    public ObservableCollection<ParameterRowViewModel> Parameters { get; } = [];
    public ObservableCollection<PinInfoRowViewModel>   InputPins  { get; } = [];
    public ObservableCollection<PinInfoRowViewModel>   OutputPins { get; } = [];

    // ── State ─────────────────────────────────────────────────────────────────

    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        private set => Set(ref _selectedNode, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    public string PanelTitle
    {
        get => _panelTitle;
        private set => Set(ref _panelTitle, value);
    }

    // ── Computed from SelectedNode ────────────────────────────────────────────

    public bool HasNode    => SelectedNode is not null;
    public bool HasParams  => Parameters.Count > 0;
    public bool HasInputs  => InputPins.Count > 0;
    public bool HasOutputs => OutputPins.Count > 0;

    public string NodeTitle    => SelectedNode?.Title    ?? string.Empty;
    public string NodeCategory => SelectedNode?.Category.ToString() ?? string.Empty;
    public string NodeAlias
    {
        get => SelectedNode?.Alias ?? string.Empty;
        set
        {
            if (SelectedNode is not null)
                SelectedNode.Alias = string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public Avalonia.Media.LinearGradientBrush? HeaderGradient =>
        SelectedNode?.HeaderGradient;

    public string CategoryIcon => SelectedNode?.CategoryIcon ?? string.Empty;

    // ── Constructor ───────────────────────────────────────────────────────────

    public PropertyPanelViewModel(UndoRedoStack undo)
    {
        _undo = undo;
    }

    // ── Selection management ──────────────────────────────────────────────────

    public void ShowNode(NodeViewModel node)
    {
        // Commit any dirty parameters before switching
        CommitDirty();

        SelectedNode = node;
        PanelTitle   = node.Title;
        IsVisible    = true;

        RebuildRows(node);
        RaisePropertyChanged(nameof(HasNode));
        RaisePropertyChanged(nameof(NodeTitle));
        RaisePropertyChanged(nameof(NodeCategory));
        RaisePropertyChanged(nameof(NodeAlias));
        RaisePropertyChanged(nameof(HeaderGradient));
        RaisePropertyChanged(nameof(CategoryIcon));
    }

    public void ShowMultiSelection(IReadOnlyList<NodeViewModel> nodes)
    {
        CommitDirty();
        SelectedNode = null;
        PanelTitle   = $"{nodes.Count} nodes selected";
        Parameters.Clear();
        InputPins.Clear();
        OutputPins.Clear();
        IsVisible = true;
        RaisePropertyChanged(nameof(HasNode));
    }

    public void Clear()
    {
        CommitDirty();
        SelectedNode = null;
        PanelTitle   = "Properties";
        Parameters.Clear();
        InputPins.Clear();
        OutputPins.Clear();
        IsVisible = false;
        RaisePropertyChanged(nameof(HasNode));
    }

    // ── Parameter building ────────────────────────────────────────────────────

    private void RebuildRows(NodeViewModel node)
    {
        Parameters.Clear();
        InputPins.Clear();
        OutputPins.Clear();

        // Get the static definition for this node type
        NodeDefinition? def = null;
        try { def = NodeDefinitionRegistry.Get(node.Type); }
        catch { /* TableSource and custom nodes have no registry entry */ }

        if (def is not null)
        {
            foreach (var param in def.Parameters)
            {
                node.Parameters.TryGetValue(param.Name, out var currentVal);
                Parameters.Add(new ParameterRowViewModel(param, currentVal));
            }
        }

        foreach (var pin in node.InputPins)
            InputPins.Add(new PinInfoRowViewModel(pin));

        foreach (var pin in node.OutputPins)
            OutputPins.Add(new PinInfoRowViewModel(pin));
    }

    // ── Commit / apply ────────────────────────────────────────────────────────

    /// <summary>
    /// Writes all dirty parameter rows to the node via undo-able commands.
    /// Called automatically on selection change and on explicit Apply.
    /// </summary>
    public void CommitDirty()
    {
        if (SelectedNode is null) return;

        foreach (var row in Parameters.Where(r => r.IsDirty))
        {
            SelectedNode.Parameters.TryGetValue(row.Name, out var old);
            _undo.Execute(new EditParameterCommand(SelectedNode, row.Name, old, row.Value));
            row.MarkClean();
        }
    }
}
