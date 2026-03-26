using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Data;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public void RaisePropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; RaisePropertyChanged(name); return true;
    }
}

public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? _) => canExecute?.Invoke() ?? true;
    public void Execute(object? _) => execute();
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

// ── OUTPUT COLUMN ENTRY (for ResultOutput node ordering) ─────────────────────

public sealed class OutputColumnEntry : ViewModelBase
{
    public string Key         { get; }
    public string DisplayName { get; }
    public ICommand MoveUpCommand   { get; }
    public ICommand MoveDownCommand { get; }

    public OutputColumnEntry(string key, string displayName, Action moveUp, Action moveDown)
    {
        Key             = key;
        DisplayName     = displayName;
        MoveUpCommand   = new RelayCommand(moveUp);
        MoveDownCommand = new RelayCommand(moveDown);
    }
}

// ── PIN VM ──────────────────────────────────────────────────────────────────

public sealed class PinViewModel : ViewModelBase
{
    private Point _absolutePosition;
    private bool  _isHovered, _isConnected, _isDropTarget;

    public string Name { get; }
    public PinDirection Direction { get; }
    public PinDataType DataType { get; }
    public bool IsRequired { get; }
    public bool AllowMultiple { get; }
    public NodeViewModel Owner { get; }

    public Color PinColor => DataType switch
    {
        PinDataType.Text       => Color.Parse("#60A5FA"),
        PinDataType.Number     => Color.Parse("#4ADE80"),
        PinDataType.Boolean    => Color.Parse("#FACC15"),
        PinDataType.DateTime   => Color.Parse("#22D3EE"),
        PinDataType.Json       => Color.Parse("#A78BFA"),
        PinDataType.Expression => Color.Parse("#F97316"),
        _                      => Color.Parse("#94A3B8")
    };

    public SolidColorBrush PinBrush     => new(PinColor);
    public SolidColorBrush PinGlowBrush => new(Color.FromArgb(60, PinColor.R, PinColor.G, PinColor.B));
    public string DataTypeLabel         => DataType.ToString().ToUpperInvariant();

    public Point AbsolutePosition
    { get => _absolutePosition; set => Set(ref _absolutePosition, value); }

    public bool IsHovered
    { get => _isHovered; set { Set(ref _isHovered, value); RaisePropertyChanged(nameof(VisualScale)); } }

    public bool IsConnected
    { get => _isConnected; set => Set(ref _isConnected, value); }

    public bool IsDropTarget
    {
        get => _isDropTarget;
        set { Set(ref _isDropTarget, value); RaisePropertyChanged(nameof(DropTargetBrush)); RaisePropertyChanged(nameof(VisualScale)); }
    }

    public double VisualScale      => IsHovered || IsDropTarget ? 1.4 : 1.0;
    public SolidColorBrush DropTargetBrush => IsDropTarget ? new SolidColorBrush(Color.Parse("#FACC15")) : PinBrush;

    public bool CanAccept(PinViewModel other)
    {
        if (other.Owner == Owner)        return false;
        if (other.Direction == Direction) return false;
        if (DataType != PinDataType.Any && other.DataType != PinDataType.Any && DataType != other.DataType)
            return false;
        return true;
    }

    public PinViewModel(PinDescriptor d, NodeViewModel owner)
    {
        Name = d.Name; Direction = d.Direction; DataType = d.DataType;
        IsRequired = d.IsRequired; AllowMultiple = d.AllowMultiple; Owner = owner;
    }
}

// ── NODE VM ─────────────────────────────────────────────────────────────────

public sealed class NodeViewModel : ViewModelBase
{
    private Point _position;
    private bool _isSelected, _isHovered;
    private string? _alias;
    private double _width = 220;
    private List<ValidationIssue> _validationIssues = [];

    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public NodeType Type { get; }
    public NodeCategory Category { get; }
    public string Title { get; }
    public string Subtitle { get; }

    public Dictionary<string, string> Parameters  { get; } = new();
    public Dictionary<string, string> PinLiterals { get; } = new();

    public ObservableCollection<PinViewModel>     InputPins         { get; } = [];
    public ObservableCollection<PinViewModel>     OutputPins        { get; } = [];
    public ObservableCollection<OutputColumnEntry> OutputColumnOrder { get; } = [];
    public IEnumerable<PinViewModel> AllPins => InputPins.Concat(OutputPins);

    public bool IsResultOutput => Type == NodeType.ResultOutput;

    public Point Position
    { get => _position; set => Set(ref _position, value); }

    public bool IsSelected
    {
        get => _isSelected;
        set { Set(ref _isSelected, value); RaisePropertyChanged(nameof(NodeBorderBrush)); RaisePropertyChanged(nameof(NodeShadow)); }
    }

    public bool IsHovered
    { get => _isHovered; set => Set(ref _isHovered, value); }

    public string? Alias
    { get => _alias; set => Set(ref _alias, value); }

    public double Width
    { get => _width; set => Set(ref _width, value); }

    public Color HeaderColor => Category switch
    {
        NodeCategory.DataSource      => Color.Parse("#0F766E"),
        NodeCategory.StringTransform => Color.Parse("#4338CA"),
        NodeCategory.MathTransform   => Color.Parse("#B45309"),
        NodeCategory.TypeCast        => Color.Parse("#7E22CE"),
        NodeCategory.Comparison      => Color.Parse("#BE123C"),
        NodeCategory.LogicGate       => Color.Parse("#C2410C"),
        NodeCategory.Json            => Color.Parse("#6D28D9"),
        NodeCategory.Aggregate       => Color.Parse("#15803D"),
        NodeCategory.Conditional     => Color.Parse("#0E7490"),
        _                            => Color.Parse("#374151")
    };

    public Color HeaderColorLight => Category switch
    {
        NodeCategory.DataSource      => Color.Parse("#14B8A6"),
        NodeCategory.StringTransform => Color.Parse("#818CF8"),
        NodeCategory.MathTransform   => Color.Parse("#FBBF24"),
        NodeCategory.TypeCast        => Color.Parse("#C084FC"),
        NodeCategory.Comparison      => Color.Parse("#FB7185"),
        NodeCategory.LogicGate       => Color.Parse("#FB923C"),
        NodeCategory.Json            => Color.Parse("#A78BFA"),
        NodeCategory.Aggregate       => Color.Parse("#4ADE80"),
        NodeCategory.Conditional     => Color.Parse("#22D3EE"),
        _                            => Color.Parse("#9CA3AF")
    };

    public LinearGradientBrush HeaderGradient => new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint   = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = [ new GradientStop(HeaderColor, 0.0), new GradientStop(HeaderColorLight, 1.0) ]
    };

    // ── Validation state ──────────────────────────────────────────────────────
    public IReadOnlyList<ValidationIssue> ValidationIssues => _validationIssues;
    public bool HasError   => _validationIssues.Any(i => i.Severity == IssueSeverity.Error);
    public bool HasWarning => !HasError && _validationIssues.Any(i => i.Severity == IssueSeverity.Warning);
    public string? ValidationTooltip => _validationIssues.Count > 0
        ? string.Join("\n", _validationIssues.Select(i =>
            $"{(i.Severity == IssueSeverity.Error ? "✕" : "⚠")} {i.Message}" +
            (i.Suggestion is not null ? $"\n   → {i.Suggestion}" : "")))
        : null;

    internal void SetValidation(IEnumerable<ValidationIssue> issues)
    {
        _validationIssues = issues.ToList();
        RaisePropertyChanged(nameof(ValidationIssues));
        RaisePropertyChanged(nameof(HasError));
        RaisePropertyChanged(nameof(HasWarning));
        RaisePropertyChanged(nameof(ValidationTooltip));
        RaisePropertyChanged(nameof(NodeBorderBrush));
        RaisePropertyChanged(nameof(NodeShadow));
    }

    // ── ResultOutput column ordering ──────────────────────────────────────────

    /// <summary>
    /// Rebuilds <see cref="OutputColumnOrder"/> from current canvas connections.
    /// Keeps existing entries in their user-defined order; appends new ones and
    /// removes entries whose connections were deleted.
    /// </summary>
    internal void SyncOutputColumns(IEnumerable<ConnectionViewModel> allConnections)
    {
        if (!IsResultOutput) return;

        var incoming = allConnections
            .Where(c => c.ToPin?.Owner == this)
            .ToList();

        var existingKeys = OutputColumnOrder.Select(e => e.Key).ToHashSet();
        var newKeys      = incoming.Select(c => MakeKey(c.FromPin)).ToHashSet();

        // Remove orphaned entries
        foreach (var entry in OutputColumnOrder.ToList())
            if (!newKeys.Contains(entry.Key))
                OutputColumnOrder.Remove(entry);

        // Append newly connected columns
        foreach (var conn in incoming)
        {
            var key = MakeKey(conn.FromPin);
            if (!existingKeys.Contains(key))
            {
                var display = conn.FromPin.Owner.Type == NodeType.TableSource
                    ? $"{conn.FromPin.Owner.Subtitle?.Split('.').Last() ?? conn.FromPin.Owner.Title}.{conn.FromPin.Name}"
                    : $"{conn.FromPin.Owner.Title} → {conn.FromPin.Name}";
                OutputColumnOrder.Add(new OutputColumnEntry(
                    key, display,
                    () => MoveColumnUp(key),
                    () => MoveColumnDown(key)));
            }
        }
    }

    private static string MakeKey(PinViewModel pin) => $"{pin.Owner.Id}::{pin.Name}";

    private void MoveColumnUp(string key)
    {
        var idx = IndexOf(key);
        if (idx > 0) OutputColumnOrder.Move(idx, idx - 1);
    }

    private void MoveColumnDown(string key)
    {
        var idx = IndexOf(key);
        if (idx >= 0 && idx < OutputColumnOrder.Count - 1) OutputColumnOrder.Move(idx, idx + 1);
    }

    private int IndexOf(string key)
    {
        for (int i = 0; i < OutputColumnOrder.Count; i++)
            if (OutputColumnOrder[i].Key == key) return i;
        return -1;
    }

    /// <summary>
    /// Returns the ordered (nodeId, pinName) pairs for SQL compilation.
    /// </summary>
    public IReadOnlyList<(string NodeId, string PinName)> GetOrderedColumns() =>
        OutputColumnOrder
            .Select(e => e.Key.Split("::", 2))
            .Where(p => p.Length == 2)
            .Select(p => (p[0], p[1]))
            .ToList();

    public SolidColorBrush NodeBorderBrush =>
        IsSelected   ? new SolidColorBrush(Color.Parse("#3B82F6"))
        : HasError   ? new SolidColorBrush(Color.Parse("#EF4444"))
        : HasWarning ? new SolidColorBrush(Color.Parse("#FBBF24"))
        : new SolidColorBrush(Color.Parse("#252C3F"));

    public BoxShadows NodeShadow =>
        IsSelected   ? BoxShadows.Parse("0 0 0 2 #3B82F6, 0 8 32 0 #603B82F6")
        : HasError   ? BoxShadows.Parse("0 0 0 2 #EF4444, 0 4 16 0 #40EF4444")
        : HasWarning ? BoxShadows.Parse("0 0 0 1 #FBBF24, 0 4 12 0 #30FBBF24")
        : BoxShadows.Parse("0 4 24 0 #40000000, 0 1 4 0 #50000000");

    public string CategoryIcon => NodeIconCatalog.GetForCategory(Category);

    public NodeViewModel(NodeDefinition def, Point pos)
    {
        Type = def.Type; Category = def.Category;
        Title = def.DisplayName;
        Subtitle = def.Description.Length > 40 ? def.Description[..37] + "…" : def.Description;
        Position = pos;
        foreach (var p in def.Parameters) if (p.DefaultValue is not null) Parameters[p.Name] = p.DefaultValue;
        foreach (var pin in def.Pins)
        {
            var vm = new PinViewModel(pin, this);
            if (pin.Direction == PinDirection.Input) InputPins.Add(vm);
            else OutputPins.Add(vm);
        }
    }

    public NodeViewModel(string tableName, IEnumerable<(string n, PinDataType t)> cols, Point pos)
    {
        Type = NodeType.TableSource; Category = NodeCategory.DataSource;
        Title = tableName.Split('.').Last(); Subtitle = tableName; Position = pos;
        foreach (var (n, t) in cols)
            OutputPins.Add(new PinViewModel(new PinDescriptor(n, PinDirection.Output, t), this));
    }

    public PinViewModel? FindPin(string name, PinDirection? dir = null)
        => AllPins.FirstOrDefault(p => p.Name == name && (dir is null || p.Direction == dir));

    public void RaiseParameterChanged(string p) => RaisePropertyChanged($"Param_{p}");
}

// ── CONNECTION VM ────────────────────────────────────────────────────────────

public sealed class ConnectionViewModel : ViewModelBase
{
    private Point _fromPoint, _toPoint;
    private bool _isHighlighted;

    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public PinViewModel FromPin { get; }
    public PinViewModel? ToPin { get; set; }

    public Point FromPoint
    { get => _fromPoint; set { Set(ref _fromPoint, value); RaisePropertyChanged(nameof(BezierPath)); } }

    public Point ToPoint
    { get => _toPoint; set { Set(ref _toPoint, value); RaisePropertyChanged(nameof(BezierPath)); } }

    public bool IsHighlighted
    { get => _isHighlighted; set => Set(ref _isHighlighted, value); }

    public Color WireColor     => FromPin.PinColor;
    public double WireOpacity  => IsHighlighted ? 1.0 : 0.75;
    public double WireThickness => IsHighlighted ? 2.5 : 1.8;

    public string BezierPath
    {
        get
        {
            var dx = Math.Abs(ToPoint.X - FromPoint.X);
            var off = Math.Max(60, dx * 0.5);
            return $"M {FromPoint.X:F1},{FromPoint.Y:F1} " +
                   $"C {FromPoint.X + off:F1},{FromPoint.Y:F1} {ToPoint.X - off:F1},{ToPoint.Y:F1} " +
                   $"{ToPoint.X:F1},{ToPoint.Y:F1}";
        }
    }

    public ConnectionViewModel(PinViewModel fromPin, Point fromPoint, Point toPoint)
    { FromPin = fromPin; _fromPoint = fromPoint; _toPoint = toPoint; }
}

// ── PIN DRAG STATE ───────────────────────────────────────────────────────────

public sealed class PinDragState
{
    public PinViewModel SourcePin { get; }
    public ConnectionViewModel LiveWire { get; }
    public List<PinViewModel> ValidTargets { get; }

    public PinDragState(PinViewModel source, ConnectionViewModel wire, IEnumerable<PinViewModel> allPins)
    {
        SourcePin = source; LiveWire = wire;
        ValidTargets = allPins.Where(p => p.CanAccept(source)).ToList();
        foreach (var p in ValidTargets) p.IsDropTarget = true;
    }

    public void UpdateWireEnd(Point pt) => LiveWire.ToPoint = pt;

    public PinViewModel? HitTest(Point pt, double tol = 12)
        => ValidTargets.OrderBy(p => Dist(p.AbsolutePosition, pt))
                       .FirstOrDefault(p => Dist(p.AbsolutePosition, pt) <= tol);

    public void Cancel() { foreach (var p in ValidTargets) p.IsDropTarget = false; }

    static double Dist(Point a, Point b) => Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2));
}

// ── SEARCH MENU VM ───────────────────────────────────────────────────────────

public sealed class SearchMenuViewModel : ViewModelBase
{
    private string _query = ""; private bool _isVisible; private Point _spawnPos;
    private NodeSearchResultViewModel? _selected;

    public string Query { get => _query; set { Set(ref _query, value); FilterResults(); } }
    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }
    public Point SpawnPosition { get => _spawnPos; set => Set(ref _spawnPos, value); }
    public NodeSearchResultViewModel? SelectedResult { get => _selected; set => Set(ref _selected, value); }
    public ObservableCollection<NodeSearchResultViewModel> Results { get; } = [];

    private static readonly IReadOnlyList<NodeDefinition> AllDefs =
        NodeDefinitionRegistry.All.OrderBy(d => d.Category).ThenBy(d => d.DisplayName).ToList();

    private readonly List<NodeSearchResultViewModel> _tables = [];

    public void LoadTables(IEnumerable<(string FullName, IReadOnlyList<(string Name, PinDataType Type)> Cols)> tables)
    {
        _tables.Clear();
        _tables.AddRange(tables.Select(t => NodeSearchResultViewModel.ForTable(t.FullName, t.Cols)));
        FilterResults();
    }

    public void Open(Point pos) { SpawnPosition = pos; Query = ""; FilterResults(); IsVisible = true; }
    public void Close() { IsVisible = false; Query = ""; }

    private void FilterResults()
    {
        Results.Clear();
        var q = Query.Trim();

        // Tables first (up to 5)
        var filteredTables = string.IsNullOrEmpty(q) ? _tables
            : _tables.Where(t => t.TableFullName.Contains(q, StringComparison.OrdinalIgnoreCase)
                               || t.Title.Contains(q, StringComparison.OrdinalIgnoreCase));
        foreach (var t in filteredTables.Take(5)) Results.Add(t);

        // Then node definitions (fill remaining slots up to 12 total)
        var filteredNodes = string.IsNullOrEmpty(q) ? AllDefs
            : AllDefs.Where(d => d.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
                              || d.Category.ToString().Contains(q, StringComparison.OrdinalIgnoreCase));
        foreach (var d in filteredNodes.Take(12 - Results.Count))
            Results.Add(new NodeSearchResultViewModel(d));

        SelectedResult = Results.FirstOrDefault();
    }

    public void SelectNext() { if (Results.Count == 0) return; SelectedResult = Results[(Results.IndexOf(SelectedResult!) + 1) % Results.Count]; }
    public void SelectPrev() { if (Results.Count == 0) return; SelectedResult = Results[(Results.IndexOf(SelectedResult!) - 1 + Results.Count) % Results.Count]; }
}

public sealed class NodeSearchResultViewModel : ViewModelBase
{
    private readonly NodeDefinition? _def;

    public NodeSearchResultViewModel(NodeDefinition def) => _def = def;
    private NodeSearchResultViewModel() { }

    // ── Table-specific fields ─────────────────────────────────────────────────
    public bool IsTable { get; init; }
    public string TableFullName { get; init; } = "";
    public IReadOnlyList<(string Name, PinDataType Type)> TableColumns { get; init; } = [];

    public static NodeSearchResultViewModel ForTable(
        string fullName,
        IReadOnlyList<(string Name, PinDataType Type)> cols) =>
        new() { IsTable = true, TableFullName = fullName, TableColumns = cols };

    // ── Shared view properties ────────────────────────────────────────────────
    public NodeDefinition Definition =>
        _def ?? NodeDefinitionRegistry.Get(NodeType.TableSource);

    public string Title =>
        IsTable ? TableFullName.Split('.').Last() : _def!.DisplayName;

    public string Category =>
        IsTable ? "Table" : _def!.Category.ToString();

    public string Icon => NodeIconCatalog.GetForCategory(_def?.Category ?? NodeCategory.DataSource);

    public Color AccentColor => IsTable ? Color.Parse("#14B8A6") : _def!.Category switch
    {
        NodeCategory.DataSource => Color.Parse("#14B8A6"), NodeCategory.StringTransform => Color.Parse("#818CF8"),
        NodeCategory.MathTransform => Color.Parse("#FBBF24"), NodeCategory.TypeCast => Color.Parse("#C084FC"),
        NodeCategory.Comparison => Color.Parse("#FB7185"), NodeCategory.LogicGate => Color.Parse("#FB923C"),
        NodeCategory.Json => Color.Parse("#A78BFA"), NodeCategory.Aggregate => Color.Parse("#4ADE80"), _ => Color.Parse("#9CA3AF")
    };

    public SolidColorBrush AccentBrush => new(AccentColor);
}

// ── DATA PREVIEW VM ──────────────────────────────────────────────────────────

public sealed class DataPreviewViewModel : ViewModelBase
{
    private bool _isVisible, _isLoading; private string? _errorMsg;
    private string _queryText = ""; private DataTable? _data;
    private double _panelHeight = 280; private int _rows; private long _ms;
    private DiagnosticResult? _diagnostic;

    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }
    public bool IsLoading { get => _isLoading; set { Set(ref _isLoading, value); RaisePropertyChanged(nameof(StatusText)); } }
    public string? ErrorMessage { get => _errorMsg; set { Set(ref _errorMsg, value); RaisePropertyChanged(nameof(StatusText)); } }
    public string QueryText { get => _queryText; set => Set(ref _queryText, value); }
    public DataTable? ResultData { get => _data; set { Set(ref _data, value); RaisePropertyChanged(nameof(HasData)); RaisePropertyChanged(nameof(StatusText)); } }
    public double PanelHeight { get => _panelHeight; set => Set(ref _panelHeight, value); }
    public int RowCount { get => _rows; set => Set(ref _rows, value); }
    public long ExecutionMs { get => _ms; set => Set(ref _ms, value); }
    public bool HasData => _data is { Rows.Count: > 0 };
    public string StatusText => IsLoading ? "Running…" : ErrorMessage is not null ? "Error" : HasData ? $"{_rows} rows · {_ms}ms" : "No results";

    // ── Structured diagnostic ─────────────────────────────────────────────────
    public DiagnosticResult? Diagnostic
    {
        get => _diagnostic;
        private set
        {
            Set(ref _diagnostic, value);
            RaisePropertyChanged(nameof(HasDiagnostic));
            RaisePropertyChanged(nameof(DiagnosticIcon));
            RaisePropertyChanged(nameof(DiagnosticLabel));
            RaisePropertyChanged(nameof(DiagnosticSuggestion));
            RaisePropertyChanged(nameof(DiagnosticTechnical));
            RaisePropertyChanged(nameof(HasTechnicalDetail));
        }
    }
    public bool    HasDiagnostic      => _diagnostic is not null;
    public string  DiagnosticIcon     => _diagnostic?.CategoryIcon     ?? "⚠";
    public string  DiagnosticLabel    => _diagnostic?.CategoryLabel    ?? "Error";
    public string  DiagnosticSuggestion => _diagnostic?.Suggestion     ?? string.Empty;
    public string? DiagnosticTechnical  => _diagnostic?.TechnicalDetail;
    public bool    HasTechnicalDetail   => !string.IsNullOrWhiteSpace(_diagnostic?.TechnicalDetail);

    public void Toggle() => IsVisible = !IsVisible;
    public void ShowLoading(string sql) { QueryText = sql; IsLoading = true; ErrorMessage = null; ResultData = null; Diagnostic = null; IsVisible = true; }
    public void ShowResults(DataTable dt, long ms) { ResultData = dt; RowCount = dt.Rows.Count; ExecutionMs = ms; IsLoading = false; ErrorMessage = null; Diagnostic = null; }
    public void ShowError(string msg, Exception? ex = null)
    {
        Diagnostic   = ErrorDiagnostics.Classify(msg, ex);
        ErrorMessage = Diagnostic.FriendlyMessage;
        IsLoading    = false;
    }
}

// ── CANVAS VM ────────────────────────────────────────────────────────────────

public sealed class CanvasViewModel : ViewModelBase
{
    private double _zoom = 1.0; private Point _panOffset; private string _queryText = "";
    private bool _isDirty; private string? _filePath;
    private CancellationTokenSource? _validationCts;

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    public SearchMenuViewModel      SearchMenu      { get; }
    public CommandPaletteViewModel  CommandPalette  { get; } = new();
    public DataPreviewViewModel     DataPreview     { get; } = new();
    public UndoRedoStack            UndoRedo        { get; }
    public PropertyPanelViewModel   PropertyPanel   { get; }
    public LiveSqlBarViewModel      LiveSql         { get; set; }
    public AutoJoinOverlayViewModel AutoJoin        { get; set; }

    public double Zoom
    { get => _zoom; set { Set(ref _zoom, Math.Clamp(value,0.15,4.0)); RaisePropertyChanged(nameof(ZoomPercent)); } }

    public Point PanOffset { get => _panOffset; set => Set(ref _panOffset, value); }
    public string ZoomPercent => $"{Zoom*100:F0}%";
    public string QueryText { get => _queryText; set => Set(ref _queryText, value); }
    public bool IsDirty { get => _isDirty; set => Set(ref _isDirty, value); }
    public string? CurrentFilePath { get => _filePath; set { Set(ref _filePath, value); RaisePropertyChanged(nameof(WindowTitle)); } }
    public string WindowTitle => (CurrentFilePath is not null ? Path.GetFileNameWithoutExtension(CurrentFilePath) : "Untitled") + (IsDirty ? " •" : "") + " — Visual SQL Architect";
    public bool IsCanvasEmpty => Nodes.Count == 0;

    // ── Validation summary ────────────────────────────────────────────────────
    public bool HasErrors    => Nodes.Any(n => n.HasError);
    public int  ErrorCount   => Nodes.Sum(n => n.ValidationIssues.Count(i => i.Severity == IssueSeverity.Error));
    public int  WarningCount => Nodes.Sum(n => n.ValidationIssues.Count(i => i.Severity == IssueSeverity.Warning));

    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand DeleteSelectedCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand DeselectAllCommand { get; }
    public RelayCommand ZoomInCommand { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand ResetZoomCommand { get; }
    public RelayCommand FitToScreenCommand { get; }
    public RelayCommand TogglePreviewCommand { get; }

    public CanvasViewModel()
    {
        UndoRedo      = new UndoRedoStack(this);
        SearchMenu    = new SearchMenuViewModel();
        PropertyPanel = new PropertyPanelViewModel(UndoRedo);

        UndoCommand           = new RelayCommand(UndoRedo.Undo, () => UndoRedo.CanUndo);
        RedoCommand           = new RelayCommand(UndoRedo.Redo, () => UndoRedo.CanRedo);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected);
        SelectAllCommand      = new RelayCommand(SelectAll);
        DeselectAllCommand    = new RelayCommand(DeselectAll);
        ZoomInCommand         = new RelayCommand(() => Zoom *= 1.15);
        ZoomOutCommand        = new RelayCommand(() => Zoom /= 1.15);
        ResetZoomCommand      = new RelayCommand(() => { Zoom = 1.0; PanOffset = new(0,0); });
        FitToScreenCommand    = new RelayCommand(FitToScreen);
        TogglePreviewCommand  = new RelayCommand(DataPreview.Toggle);

        LiveSql  = new LiveSqlBarViewModel(this);
        AutoJoin = new AutoJoinOverlayViewModel();

        // Populate the search menu table catalog with demo/northwind tables
        SearchMenu.LoadTables(DemoCatalog);

        // When a join is accepted, wire the pins on the canvas
        AutoJoin.JoinAccepted += (_, suggestion) =>
        {
            // Materialise the JOIN as a connection between table output pins
            var fromTable = Nodes.FirstOrDefault(n => n.Subtitle == suggestion.ExistingTable || n.Title == suggestion.ExistingTable.Split('.').Last());
            var toTable   = Nodes.FirstOrDefault(n => n.Subtitle == suggestion.NewTable      || n.Title == suggestion.NewTable.Split('.').Last());
            if (fromTable is null || toTable is null) return;

            // Extract column names from the OnClause: "table.col = table.col"
            var parts = suggestion.OnClause.Split('=');
            if (parts.Length != 2) return;
            var leftCol  = parts[0].Trim().Split('.').Last();
            var rightCol = parts[1].Trim().Split('.').Last();

            var fromPin = fromTable.OutputPins.FirstOrDefault(p => p.Name.Equals(leftCol, System.StringComparison.OrdinalIgnoreCase))
                       ?? toTable.OutputPins.FirstOrDefault(p => p.Name.Equals(leftCol, System.StringComparison.OrdinalIgnoreCase));
            var toPin   = toTable.InputPins.FirstOrDefault(p => p.Name.Equals(rightCol, System.StringComparison.OrdinalIgnoreCase))
                       ?? fromTable.InputPins.FirstOrDefault(p => p.Name.Equals(rightCol, System.StringComparison.OrdinalIgnoreCase));

            if (fromPin is not null && toPin is not null)
                ConnectPins(fromPin, toPin);
        };

        Nodes.CollectionChanged += (_, e) =>
        {
            IsDirty = true;
            RaisePropertyChanged(nameof(IsCanvasEmpty));
            // Subscribe to per-node property changes so validation re-runs on alias/param edits
            if (e.NewItems is not null)
                foreach (NodeViewModel n in e.NewItems)
                    n.PropertyChanged += (_, _) => ScheduleValidation();
            ScheduleValidation();
        };
        Connections.CollectionChanged += (_, _) =>
        {
            IsDirty = true;
            ScheduleValidation();
            // Keep ResultOutput column order in sync with canvas connections
            foreach (var n in Nodes.Where(n => n.IsResultOutput))
                n.SyncOutputColumns(Connections);
        };

        SpawnDemoNodes();
    }

    public NodeViewModel SpawnNode(NodeDefinition def, Point pos)
    {
        var vm = new NodeViewModel(def, pos);
        UndoRedo.Execute(new AddNodeCommand(vm));
        SearchMenu.Close();
        return vm;
    }

    // ── Demo table catalog (northwind schema) ────────────────────────────────
    internal static readonly IReadOnlyList<(string FullName, IReadOnlyList<(string Name, PinDataType Type)> Cols)>
        DemoCatalog = new (string, IReadOnlyList<(string, PinDataType)>)[]
    {
        ("public.orders",      new[] { ("id",PinDataType.Number),   ("customer_id",PinDataType.Number), ("status",PinDataType.Text),   ("total",PinDataType.Number), ("created_at",PinDataType.DateTime), ("metadata",PinDataType.Json) }),
        ("public.customers",   new[] { ("id",PinDataType.Number),   ("name",PinDataType.Text),          ("email",PinDataType.Text),    ("city",PinDataType.Text),    ("country",PinDataType.Text),        ("created_at",PinDataType.DateTime) }),
        ("public.products",    new[] { ("id",PinDataType.Number),   ("name",PinDataType.Text),          ("category",PinDataType.Text), ("price",PinDataType.Number), ("stock",PinDataType.Number) }),
        ("public.order_items", new[] { ("id",PinDataType.Number),   ("order_id",PinDataType.Number),    ("product_id",PinDataType.Number), ("qty",PinDataType.Number), ("unit_price",PinDataType.Number) }),
        ("public.employees",   new[] { ("id",PinDataType.Number),   ("name",PinDataType.Text),          ("department",PinDataType.Text), ("salary",PinDataType.Number), ("hire_date",PinDataType.DateTime) }),
    };

    public NodeViewModel SpawnTableNode(string table, IEnumerable<(string n, PinDataType t)> cols, Point pos)
    {
        var vm = new NodeViewModel(table, cols, pos);
        UndoRedo.Execute(new AddNodeCommand(vm));
        SearchMenu.Close();
        return vm;
    }

    public void DeleteSelected()
    {
        var nodes = Nodes.Where(n => n.IsSelected).ToList();
        if (nodes.Count == 0) return;
        var wires = Connections.Where(c => nodes.Contains(c.FromPin.Owner) || (c.ToPin is not null && nodes.Contains(c.ToPin.Owner))).ToList();
        UndoRedo.Execute(new DeleteSelectionCommand(nodes, wires));
        PropertyPanel.Clear();
    }

    public void ConnectPins(PinViewModel from, PinViewModel to)
    {
        var src  = from.Direction == PinDirection.Output ? from : to;
        var dest = from.Direction == PinDirection.Input  ? from : to;
        var displaced = dest.AllowMultiple ? null : Connections.FirstOrDefault(c => c.ToPin == dest);
        var conn = new ConnectionViewModel(src, src.AbsolutePosition, dest.AbsolutePosition) { ToPin = dest };
        UndoRedo.Execute(new AddConnectionCommand(conn, displaced));
        IsDirty = true;
    }

    public void DeleteConnection(ConnectionViewModel conn) => UndoRedo.Execute(new DeleteConnectionCommand(conn));

    public void SelectAll()   { foreach (var n in Nodes) n.IsSelected = true; }
    public void DeselectAll() { foreach (var n in Nodes) n.IsSelected = false; PropertyPanel.Clear(); }

    public void SelectNode(NodeViewModel node, bool add = false)
    {
        if (!add) DeselectAll();
        node.IsSelected = true;
        var sel = Nodes.Where(n => n.IsSelected).ToList();
        if (sel.Count == 1) PropertyPanel.ShowNode(sel[0]);
        else if (sel.Count > 1) PropertyPanel.ShowMultiSelection(sel);
    }

    public void ZoomToward(Point screen, double factor)
    {
        var old = Zoom; Zoom = Math.Clamp(old * factor, 0.15, 4.0);
        PanOffset = new Point(screen.X - (screen.X - PanOffset.X) * (Zoom / old), screen.Y - (screen.Y - PanOffset.Y) * (Zoom / old));
    }

    public Point ScreenToCanvas(Point s) => new((s.X - PanOffset.X) / Zoom, (s.Y - PanOffset.Y) / Zoom);
    public Point CanvasToScreen(Point c) => new(c.X * Zoom + PanOffset.X, c.Y * Zoom + PanOffset.Y);

    private void FitToScreen() { if (Nodes.Count == 0) return; Zoom = 0.85; PanOffset = new(80, 80); }

    public void UpdateQueryText(string sql) { QueryText = sql; DataPreview.QueryText = sql; }

    // ── Graph validation ──────────────────────────────────────────────────────

    public void ScheduleValidation()
    {
        _validationCts?.Cancel();
        _validationCts = new CancellationTokenSource();
        var token = _validationCts.Token;
        Task.Delay(200, token).ContinueWith(_ =>
        {
            if (!token.IsCancellationRequested)
                Avalonia.Threading.Dispatcher.UIThread.Post(RunValidation);
        }, TaskScheduler.Default);
    }

    private void RunValidation()
    {
        var allIssues = GraphValidator.Validate(this);
        var byNode    = allIssues
            .Where(i => !string.IsNullOrEmpty(i.NodeId))
            .GroupBy(i => i.NodeId)
            .ToDictionary(g => g.Key, g => (IEnumerable<ValidationIssue>)g);

        foreach (var node in Nodes)
            node.SetValidation(byNode.TryGetValue(node.Id, out var issues) ? issues : []);

        RaisePropertyChanged(nameof(HasErrors));
        RaisePropertyChanged(nameof(ErrorCount));
        RaisePropertyChanged(nameof(WarningCount));
    }

    private void SpawnDemoNodes()
    {
        var orders = new NodeViewModel("public.orders",
            new[] { ("id",PinDataType.Number), ("customer_id",PinDataType.Number), ("status",PinDataType.Text),
                    ("total",PinDataType.Number), ("created_at",PinDataType.DateTime), ("metadata",PinDataType.Json) },
            new Point(60, 80));
        Nodes.Add(orders);

        var upper = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Upper), new Point(380, 120)) { Alias = "StatusUpper" };
        Nodes.Add(upper);
        var between = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Between), new Point(380, 280));
        between.PinLiterals["low"] = "100"; between.PinLiterals["high"] = "9999";
        Nodes.Add(between);
        var json = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.JsonExtract), new Point(380, 430)) { Alias = "City" };
        json.Parameters["path"] = "$.address.city";
        Nodes.Add(json);
        var and = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.And), new Point(660, 310));
        Nodes.Add(and);

        Connections.Add(new ConnectionViewModel(orders.OutputPins.First(p=>p.Name=="status"),  default, default) { ToPin = upper.InputPins.First(p=>p.Name=="text") });
        Connections.Add(new ConnectionViewModel(orders.OutputPins.First(p=>p.Name=="total"),   default, default) { ToPin = between.InputPins.First(p=>p.Name=="value") });
        Connections.Add(new ConnectionViewModel(orders.OutputPins.First(p=>p.Name=="metadata"),default, default) { ToPin = json.InputPins.First(p=>p.Name=="json") });
        Connections.Add(new ConnectionViewModel(between.OutputPins.First(p=>p.Name=="result"), default, default) { ToPin = and.InputPins.First(p=>p.Name=="conditions") });

        IsDirty = false; UndoRedo.Clear();
    }
}
