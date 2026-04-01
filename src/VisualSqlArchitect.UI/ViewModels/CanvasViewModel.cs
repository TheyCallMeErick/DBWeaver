using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Text.Json;
using Avalonia;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Services.Localization;
using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.ViewModels.Canvas;
using VisualSqlArchitect.UI.ViewModels.UndoRedo;
using VisualSqlArchitect.UI.ViewModels.UndoRedo.Commands;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Facade for all canvas operations.
/// Coordinates UI interactions by delegating to specialised managers:
///   - <see cref="NodeManager"/>       — node lifecycle (spawn, delete, demo)
///   - <see cref="PinManager"/>         — connections and type narrowing
///   - <see cref="SelectionManager"/>   — node selection and alignment
///   - <see cref="NodeLayoutManager"/>  — zoom, pan, snap, auto-layout
///   - <see cref="ValidationManager"/>  — graph validation and orphan detection
/// </summary>
public sealed class CanvasViewModel : ViewModelBase, IDisposable
{
    private sealed record CteEditorSession(
        string ParentCanvasJson,
        string ParentCteNodeId,
        bool ParentWasDirty,
        string CteDisplayName
    );

    // ── Core collections ─────────────────────────────────────────────────────

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    // ── Child view models ─────────────────────────────────────────────────────

    public SearchMenuViewModel SearchMenu { get; }
    public CommandPaletteViewModel CommandPalette { get; } = new();
    public DataPreviewViewModel DataPreview { get; } = new();
    public ToastCenterViewModel Toasts { get; } = new();
    public AppDiagnosticsViewModel Diagnostics { get; }
    public PropertyPanelViewModel PropertyPanel { get; }
    public LiveSqlBarViewModel LiveSql { get; set; }
    public AutoJoinOverlayViewModel AutoJoin { get; set; }
    public UndoRedoStack UndoRedo { get; }
    public ConnectionManagerViewModel ConnectionManager { get; } = new();
    public BenchmarkViewModel Benchmark { get; private set; } = null!;
    public ExplainPlanViewModel ExplainPlan { get; private set; } = null!;
    public SqlImporterViewModel SqlImporter { get; private set; } = null!;
    public FlowVersionOverlayViewModel FlowVersions { get; private set; } = null!;
    public FileVersionHistoryViewModel FileHistory { get; private set; } = null!;
    public SidebarViewModel Sidebar { get; private set; } = null!;

    // ── Managers ──────────────────────────────────────────────────────────────

    // Tracks per-node PropertyChanged handlers so they can be removed when a node is deleted.
    private readonly Dictionary<NodeViewModel, PropertyChangedEventHandler> _nodeValidationHandlers = new();

    private readonly NodeManager _nodeManager;
    private readonly PinManager _pinManager;
    private readonly SelectionManager _selectionManager;
    private readonly NodeLayoutManager _layoutManager;
    private readonly ValidationManager _validationManager;

    // ── Event handler storage for proper disposal ─────────────────────────────
    // These fields store references to event handlers so they can be unsubscribed in Dispose()
    private PropertyChangedEventHandler? _liveSqlPropertyChangedHandler;
    private PropertyChangedEventHandler? _selfPropertyChangedHandler;
    private PropertyChangedEventHandler? _layoutManagerPropertyChangedHandler;
    private PropertyChangedEventHandler? _localizationPropertyChangedHandler;
    private NotifyCollectionChangedEventHandler? _nodesCollectionChangedHandler;
    private NotifyCollectionChangedEventHandler? _connectionsCollectionChangedHandler;

    // ── Canvas state ──────────────────────────────────────────────────────────

    private string _queryText = "";
    private bool _isDirty;
    private string? _filePath;
    private DbMetadata? _databaseMetadata;
    private ConnectionConfig? _activeConnectionConfig;
    private CteEditorSession? _cteEditorSession;

    public string QueryText
    {
        get => _queryText;
        set => Set(ref _queryText, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        set => Set(ref _isDirty, value);
    }

    public string? CurrentFilePath
    {
        get => _filePath;
        set
        {
            Set(ref _filePath, value);
            RaisePropertyChanged(nameof(WindowTitle));
        }
    }

    /// <summary>
    /// The currently connected database metadata (schemas, tables, columns).
    /// Updated when a new database connection is established.
    /// </summary>
    public DbMetadata? DatabaseMetadata
    {
        get => _databaseMetadata;
        set => Set(ref _databaseMetadata, value);
    }

    /// <summary>
    /// The active database connection configuration.
    /// Used to execute queries for preview results.
    /// </summary>
    public ConnectionConfig? ActiveConnectionConfig
    {
        get => _activeConnectionConfig;
        set
        {
            if (!Set(ref _activeConnectionConfig, value))
                return;

            if (LiveSql is not null)
                LiveSql.Provider = value?.Provider ?? DatabaseProvider.Postgres;
        }
    }

    public string WindowTitle =>
        (
            CurrentFilePath is not null
                ? Path.GetFileNameWithoutExtension(CurrentFilePath)
                : "Untitled"
        )
        + (IsDirty ? " •" : "")
        + " — Visual SQL Architect";

    public bool IsCanvasEmpty => Nodes.Count == 0;

    /// <summary>
    /// True when canvas should be disabled (e.g., during database connection).
    /// </summary>
    public bool IsCanvasDisabled => ConnectionManager.IsConnecting;
    public bool IsInCteEditor => _cteEditorSession is not null;
    public string CteEditorBreadcrumb =>
        _cteEditorSession is null
            ? string.Empty
            : $"{LocalizationService.Instance["main.cteEditor.editingPrefix"]}{_cteEditorSession.CteDisplayName}";

    // ── Layout properties (delegated to NodeLayoutManager) ────────────────────

    public double Zoom
    {
        get => _layoutManager.Zoom;
        set => _layoutManager.Zoom = value;
    }

    public Point PanOffset
    {
        get => _layoutManager.PanOffset;
        set => _layoutManager.PanOffset = value;
    }

    public bool SnapToGrid
    {
        get => _layoutManager.SnapToGrid;
        set => _layoutManager.SnapToGrid = value;
    }

    public const int GridSize = NodeLayoutManager.GridSize;
    public string ZoomPercent => _layoutManager.ZoomPercent;
    public string SnapToGridLabel => _layoutManager.SnapToGridLabel;

    // ── Validation properties (delegated to ValidationManager) ────────────────

    public bool HasErrors => _validationManager.HasErrors;
    public int ErrorCount => _validationManager.ErrorCount;
    public int WarningCount => _validationManager.WarningCount;
    public bool HasOrphanNodes => _validationManager.HasOrphanNodes;
    public int OrphanCount => _validationManager.OrphanCount;
    public bool HasNamingViolations => _validationManager.HasNamingViolations;
    public int NamingConformance => _validationManager.NamingConformance;

    // ── Commands ──────────────────────────────────────────────────────────────

    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand DeleteSelectedCommand { get; }
    public RelayCommand CleanupOrphansCommand { get; }
    public RelayCommand AutoFixNamingCommand { get; }
    public RelayCommand AutoLayoutCommand { get; }
    public RelayCommand OpenDiagnosticsCommand { get; }
    public RelayCommand TogglePreviewCommand { get; }
    public RelayCommand BringSelectionToFrontCommand { get; }
    public RelayCommand SendSelectionToBackCommand { get; }
    public RelayCommand BringSelectionForwardCommand { get; }
    public RelayCommand SendSelectionBackwardCommand { get; }
    public RelayCommand NormalizeLayersCommand { get; }
    public RelayCommand EnterCteEditorCommand { get; }
    public RelayCommand ExitCteEditorCommand { get; }

    // ─ Delegated from SelectionManager
    public RelayCommand SelectAllCommand => _selectionManager.SelectAllCommand;
    public RelayCommand DeselectAllCommand => _selectionManager.DeselectAllCommand;
    public RelayCommand AlignLeftCommand => _selectionManager.AlignLeftCommand;
    public RelayCommand AlignRightCommand => _selectionManager.AlignRightCommand;
    public RelayCommand AlignTopCommand => _selectionManager.AlignTopCommand;
    public RelayCommand AlignBottomCommand => _selectionManager.AlignBottomCommand;
    public RelayCommand AlignCenterHCommand => _selectionManager.AlignCenterHCommand;
    public RelayCommand AlignCenterVCommand => _selectionManager.AlignCenterVCommand;
    public RelayCommand DistributeHCommand => _selectionManager.DistributeHCommand;
    public RelayCommand DistributeVCommand => _selectionManager.DistributeVCommand;

    // ─ Delegated from NodeLayoutManager
    public RelayCommand ZoomInCommand => _layoutManager.ZoomInCommand;
    public RelayCommand ZoomOutCommand => _layoutManager.ZoomOutCommand;
    public RelayCommand ResetZoomCommand => _layoutManager.ResetZoomCommand;
    public RelayCommand FitToScreenCommand => _layoutManager.FitToScreenCommand;
    public RelayCommand ToggleSnapCommand => _layoutManager.ToggleSnapCommand;

    // ── Constructor ───────────────────────────────────────────────────────────

    public CanvasViewModel()
    {
        UndoRedo = new UndoRedoStack(this);
        SearchMenu = new SearchMenuViewModel();
        PropertyPanel = new PropertyPanelViewModel(UndoRedo);
        Diagnostics = new AppDiagnosticsViewModel(this);

        // Initialise managers
        _nodeManager = new NodeManager(Nodes, Connections, UndoRedo, PropertyPanel, SearchMenu);
        _selectionManager = new SelectionManager(Nodes, PropertyPanel, UndoRedo);
        _layoutManager = new NodeLayoutManager(this, UndoRedo);
        _pinManager = new PinManager(Nodes, Connections, UndoRedo);
        _validationManager = new ValidationManager(this);

        // Forward layout manager changes through this ViewModel so bindings observing
        // CanvasViewModel receive updates for delegated properties (Zoom/Pan/Snap labels).
        _layoutManagerPropertyChangedHandler = (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(NodeLayoutManager.Zoom):
                    RaisePropertyChanged(nameof(Zoom));
                    RaisePropertyChanged(nameof(ZoomPercent));
                    break;

                case nameof(NodeLayoutManager.PanOffset):
                    RaisePropertyChanged(nameof(PanOffset));
                    break;

                case nameof(NodeLayoutManager.SnapToGrid):
                    RaisePropertyChanged(nameof(SnapToGrid));
                    RaisePropertyChanged(nameof(SnapToGridLabel));
                    break;

                case nameof(NodeLayoutManager.ZoomPercent):
                    RaisePropertyChanged(nameof(ZoomPercent));
                    break;

                case nameof(NodeLayoutManager.SnapToGridLabel):
                    RaisePropertyChanged(nameof(SnapToGridLabel));
                    break;
            }
        };
        _layoutManager.PropertyChanged += _layoutManagerPropertyChangedHandler;

        _localizationPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName is "" or "Item[]" or nameof(LocalizationService.CurrentCulture))
                RaisePropertyChanged(nameof(CteEditorBreadcrumb));
        };
        LocalizationService.Instance.PropertyChanged += _localizationPropertyChangedHandler;

        // Build commands
        UndoCommand = new RelayCommand(UndoRedo.Undo, () => UndoRedo.CanUndo);
        RedoCommand = new RelayCommand(UndoRedo.Redo, () => UndoRedo.CanRedo);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected);
        CleanupOrphansCommand = new RelayCommand(CleanupOrphans, () => HasOrphanNodes);
        AutoFixNamingCommand = new RelayCommand(AutoFixNaming, () => HasNamingViolations);
        AutoLayoutCommand = new RelayCommand(
            () => _layoutManager.RunAutoLayout(),
            () => Nodes.Count > 0
        );
        OpenDiagnosticsCommand = new RelayCommand(() => Diagnostics.Open());
        TogglePreviewCommand = new RelayCommand(DataPreview.Toggle);
        BringSelectionToFrontCommand = new RelayCommand(
            () => BringSelectionToFront(),
            () => Nodes.Any(n => n.IsSelected)
        );
        SendSelectionToBackCommand = new RelayCommand(
            () => SendSelectionToBack(),
            () => Nodes.Any(n => n.IsSelected)
        );
        BringSelectionForwardCommand = new RelayCommand(
            () => BringSelectionForward(),
            () => Nodes.Any(n => n.IsSelected)
        );
        SendSelectionBackwardCommand = new RelayCommand(
            () => SendSelectionBackward(),
            () => Nodes.Any(n => n.IsSelected)
        );
        NormalizeLayersCommand = new RelayCommand(
            () => NormalizeLayers(),
            () => Nodes.Count > 0
        );
        EnterCteEditorCommand = new RelayCommand(
            () => EnterSelectedCteEditor(),
            CanEnterSelectedCteEditor
        );
        ExitCteEditorCommand = new RelayCommand(
            () => ExitCteEditor(),
            () => IsInCteEditor
        );

        // Link commands into validation manager for CanExecute refresh
        _validationManager.CleanupOrphansCommand = CleanupOrphansCommand;
        _validationManager.AutoFixNamingCommand = AutoFixNamingCommand;

        LiveSql = new LiveSqlBarViewModel(this);
        AutoJoin = new AutoJoinOverlayViewModel();
        Benchmark    = new BenchmarkViewModel(this);
        ExplainPlan  = new ExplainPlanViewModel(this);
        SqlImporter  = new SqlImporterViewModel(this);
        FlowVersions = new FlowVersionOverlayViewModel(this);
        FileHistory  = new FileVersionHistoryViewModel(this);

        // Initialize Sidebar with its three tabs
        var nodesList = new NodesListViewModel(
            spawnNode: (definition, position) =>
            {
                _nodeManager.SpawnNode(definition, position);
            }
        );
        var schemaVM = new SchemaViewModel(
            onAddTableNode: (tableName, columns, position) =>
            {
                _nodeManager.SpawnTableNode(tableName, columns, position);
            }
        );
        Sidebar = new SidebarViewModel(nodesList, ConnectionManager, schemaVM);

        // Subscribe to LiveSql property changes with stored handler
        _liveSqlPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(LiveSqlBarViewModel.RawSql))
                PropertyPanel.UpdateSqlTrace(LiveSql.RawSql);
        };
        LiveSql.PropertyChanged += _liveSqlPropertyChangedHandler;
        DataPreview.ErrorNotified += OnDataPreviewErrorNotified;

        SearchMenu.LoadTables(NodeManager.DemoCatalog);

        // Enable automatic table loading when database is connected
        ConnectionManager.SearchMenu = SearchMenu;
        ConnectionManager.Canvas = this;

        // Update schema tab when database metadata changes
        // Store handler for proper unsubscribe in Dispose()
        _selfPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(DatabaseMetadata))
                schemaVM.Metadata = DatabaseMetadata;
        };
        this.PropertyChanged += _selfPropertyChangedHandler;

        AutoJoin.JoinAccepted += OnJoinAccepted;

        // Store collection changed handlers for proper unsubscribe
        _nodesCollectionChangedHandler = (_, e) =>
        {
            IsDirty = true;
            RaisePropertyChanged(nameof(IsCanvasEmpty));

            if (e.NewItems is not null)
                foreach (NodeViewModel n in e.NewItems)
                {
                    PropertyChangedEventHandler h = (_, e) =>
                    {
                        _validationManager.ScheduleValidation();
                        if (e.PropertyName == nameof(NodeViewModel.IsSelected))
                        {
                            NotifyLayerCommandsCanExecuteChanged();
                            NotifyCteEditorCommandsCanExecuteChanged();
                        }
                    };
                    n.PropertyChanged += h;
                    _nodeValidationHandlers[n] = h;
                }

            if (e.OldItems is not null)
                foreach (NodeViewModel n in e.OldItems)
                    if (_nodeValidationHandlers.TryGetValue(n, out PropertyChangedEventHandler? h))
                    {
                        n.PropertyChanged -= h;
                        _nodeValidationHandlers.Remove(n);
                    }

            _validationManager.ScheduleValidation();
        };
        Nodes.CollectionChanged += _nodesCollectionChangedHandler;

        _connectionsCollectionChangedHandler = (_, e) =>
        {
            if (IsTransientConnectionDragChange(e))
                return;

            IsDirty = true;
            _validationManager.ScheduleValidation();
            NotifyLayerCommandsCanExecuteChanged();
            NotifyCteEditorCommandsCanExecuteChanged();
            // Single pass — check each node type once instead of three Where iterations
            foreach (NodeViewModel n in Nodes)
            {
                if (n.IsResultOutput) n.SyncOutputColumns(Connections);
                else if (n.IsColumnList) n.SyncColumnListPins(Connections);
                else if (n.IsLogicGate) n.SyncLogicGatePins(Connections);
                else if (n.IsWindowFunction) n.SyncWindowFunctionPins(Connections);
            }
        };
        Connections.CollectionChanged += _connectionsCollectionChangedHandler;

        _nodeManager.SpawnDemoNodes(UndoRedo);
        NotifyLayerCommandsCanExecuteChanged();
        IsDirty = false;
    }

    private static bool IsTransientConnectionDragChange(NotifyCollectionChangedEventArgs e)
    {
        static bool HasStructuralEndpoint(NotifyCollectionChangedEventArgs args)
        {
            bool NewHasStructured = args.NewItems is not null
                && args.NewItems.Cast<object>().OfType<ConnectionViewModel>().Any(c => c.ToPin is not null);
            bool OldHasStructured = args.OldItems is not null
                && args.OldItems.Cast<object>().OfType<ConnectionViewModel>().Any(c => c.ToPin is not null);
            return NewHasStructured || OldHasStructured;
        }

        return !HasStructuralEndpoint(e);
    }

    private void NotifyLayerCommandsCanExecuteChanged()
    {
        BringSelectionToFrontCommand.NotifyCanExecuteChanged();
        SendSelectionToBackCommand.NotifyCanExecuteChanged();
        BringSelectionForwardCommand.NotifyCanExecuteChanged();
        SendSelectionBackwardCommand.NotifyCanExecuteChanged();
        NormalizeLayersCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCteEditorCommandsCanExecuteChanged()
    {
        EnterCteEditorCommand.NotifyCanExecuteChanged();
        ExitCteEditorCommand.NotifyCanExecuteChanged();
    }

    private bool CanEnterSelectedCteEditor()
    {
        if (IsInCteEditor)
            return false;

        return Nodes.Count(n => n.IsSelected && n.Type == NodeType.CteDefinition) == 1;
    }

    public bool EnterSelectedCteEditor()
    {
        if (!CanEnterSelectedCteEditor())
            return false;

        NodeViewModel cteNode = Nodes.Single(n => n.IsSelected && n.Type == NodeType.CteDefinition);
        string cteName = ResolveCteDisplayName(cteNode);

        string parentJson = CanvasSerializer.Serialize(this);
        bool parentWasDirty = IsDirty;

        (List<SavedNode> nodes, List<SavedConnection> conns) = ExtractCteEditableSubgraph(cteNode);
        if (nodes.Count == 0)
        {
            var seedResult = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ResultOutput), new Point(260, 140));
            (nodes, conns) = CanvasSerializer.SerialiseSubgraph([seedResult], []);
        }

        string subCanvasJson = BuildTemporaryCanvasJson(nodes, conns, description: $"CTE editor: {cteName}");

        CanvasLoadResult load = CanvasSerializer.Deserialize(subCanvasJson, this);
        if (!load.Success)
            return false;

        _cteEditorSession = new CteEditorSession(parentJson, cteNode.Id, parentWasDirty, cteName);
        IsDirty = false;
        RaisePropertyChanged(nameof(IsInCteEditor));
        RaisePropertyChanged(nameof(CteEditorBreadcrumb));
        NotifyCteEditorCommandsCanExecuteChanged();
        return true;
    }

    public bool EnterCteEditor(NodeViewModel cteNode)
    {
        if (IsInCteEditor)
            return false;
        if (cteNode.Type != NodeType.CteDefinition)
            return false;
        if (!Nodes.Contains(cteNode))
            return false;

        _selectionManager.DeselectAll();
        cteNode.IsSelected = true;
        return EnterSelectedCteEditor();
    }

    public bool ExitCteEditor()
    {
        if (_cteEditorSession is null)
            return false;

        CteEditorSession session = _cteEditorSession;
        (List<SavedNode> editedNodes, List<SavedConnection> editedConnections) =
            CanvasSerializer.SerialiseSubgraph(Nodes, Connections);

        CanvasLoadResult restore = CanvasSerializer.Deserialize(session.ParentCanvasJson, this);
        if (!restore.Success)
            return false;

        NodeViewModel? cteNode = Nodes.FirstOrDefault(n =>
            n.Id == session.ParentCteNodeId && n.Type == NodeType.CteDefinition
        );
        if (cteNode is null)
        {
            _cteEditorSession = null;
            RaisePropertyChanged(nameof(IsInCteEditor));
            RaisePropertyChanged(nameof(CteEditorBreadcrumb));
            NotifyCteEditorCommandsCanExecuteChanged();
            return false;
        }

        RemoveExistingCteQuerySubgraph(cteNode);

        PersistCteSubgraph(cteNode, editedNodes, editedConnections);

        _cteEditorSession = null;
        IsDirty = true;
        RaisePropertyChanged(nameof(IsInCteEditor));
        RaisePropertyChanged(nameof(CteEditorBreadcrumb));
        NotifyCteEditorCommandsCanExecuteChanged();
        return true;
    }

    private static string ResolveCteDisplayName(NodeViewModel cteNode)
    {
        if (cteNode.Parameters.TryGetValue("name", out string? name) && !string.IsNullOrWhiteSpace(name))
            return name.Trim();

        if (cteNode.Parameters.TryGetValue("cte_name", out string? legacy) && !string.IsNullOrWhiteSpace(legacy))
            return legacy.Trim();

        return cteNode.Title;
    }

    private (List<SavedNode> Nodes, List<SavedConnection> Connections) ExtractCteEditableSubgraph(NodeViewModel cteNode)
    {
        if (TryReadPersistedCteSubgraph(cteNode, out SavedCteSubgraph? persisted) && persisted is not null)
            return (persisted.Nodes, persisted.Connections);

        ConnectionViewModel? queryWire = Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == cteNode
            && c.ToPin.Name == "query"
            && c.FromPin.Owner.Type == NodeType.ResultOutput
        );

        if (queryWire?.FromPin.Owner is not NodeViewModel resultOutput)
            return ([], []);

        HashSet<string> upstreamIds = CollectUpstreamNodeIds(resultOutput, includeCteDefinitions: false);
        List<NodeViewModel> subgraphNodes = Nodes
            .Where(n => upstreamIds.Contains(n.Id) && n.Type != NodeType.CteDefinition)
            .ToList();

        List<ConnectionViewModel> subgraphConnections = Connections
            .Where(c => c.ToPin is not null
                && upstreamIds.Contains(c.FromPin.Owner.Id)
                && upstreamIds.Contains(c.ToPin.Owner.Id))
            .ToList();

        return CanvasSerializer.SerialiseSubgraph(subgraphNodes, subgraphConnections);
    }

    private static bool TryReadPersistedCteSubgraph(NodeViewModel cteNode, out SavedCteSubgraph? subgraph)
    {
        subgraph = null;

        if (!cteNode.Parameters.TryGetValue(CanvasSerializer.CteSubgraphParameterKey, out string? payload)
            || string.IsNullOrWhiteSpace(payload))
            return false;

        try
        {
            subgraph = JsonSerializer.Deserialize<SavedCteSubgraph>(payload);
            return subgraph is not null;
        }
        catch
        {
            return false;
        }
    }

    private static void PersistCteSubgraph(
        NodeViewModel cteNode,
        List<SavedNode> editedNodes,
        List<SavedConnection> editedConnections)
    {
        string? resultOutputNodeId = editedNodes
            .FirstOrDefault(n => string.Equals(n.NodeType, nameof(NodeType.ResultOutput), StringComparison.OrdinalIgnoreCase))
            ?.NodeId;

        var subgraph = new SavedCteSubgraph(
            Nodes: editedNodes,
            Connections: editedConnections,
            ResultOutputNodeId: resultOutputNodeId
        );

        cteNode.Parameters[CanvasSerializer.CteSubgraphParameterKey] = JsonSerializer.Serialize(subgraph);
    }

    private HashSet<string> CollectUpstreamNodeIds(NodeViewModel sinkNode, bool includeCteDefinitions)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { sinkNode.Id };
        var queue = new Queue<string>();
        queue.Enqueue(sinkNode.Id);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (ConnectionViewModel conn in Connections.Where(c => c.ToPin?.Owner.Id == current))
            {
                NodeViewModel fromOwner = conn.FromPin.Owner;
                if (!includeCteDefinitions && fromOwner.Type == NodeType.CteDefinition)
                    continue;

                if (visited.Add(fromOwner.Id))
                    queue.Enqueue(fromOwner.Id);
            }
        }

        return visited;
    }

    private void RemoveExistingCteQuerySubgraph(NodeViewModel cteNode)
    {
        ConnectionViewModel? queryWire = Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == cteNode
            && c.ToPin.Name == "query"
            && c.FromPin.Owner.Type == NodeType.ResultOutput
        );

        if (queryWire?.FromPin.Owner is not NodeViewModel resultOutput)
            return;

        HashSet<string> removeNodeIds = CollectUpstreamNodeIds(resultOutput, includeCteDefinitions: false);

        foreach (ConnectionViewModel conn in Connections
                     .Where(c =>
                         c.ToPin is not null
                         && (removeNodeIds.Contains(c.FromPin.Owner.Id)
                             || removeNodeIds.Contains(c.ToPin.Owner.Id)
                             || (c.FromPin.Owner == resultOutput && c.ToPin.Owner == cteNode && c.ToPin.Name == "query")))
                     .ToList())
        {
            Connections.Remove(conn);
        }

        foreach (NodeViewModel node in Nodes.Where(n => removeNodeIds.Contains(n.Id)).ToList())
            Nodes.Remove(node);
    }

    private void ConnectResultOutputToCte(NodeViewModel resultOutput, NodeViewModel cteNode)
    {
        PinViewModel? from = resultOutput.OutputPins.FirstOrDefault(p => p.Name == "result");
        PinViewModel? to = cteNode.InputPins.FirstOrDefault(p => p.Name == "query");
        if (from is null || to is null)
            return;

        bool alreadyConnected = Connections.Any(c =>
            c.FromPin.Owner == resultOutput
            && c.FromPin.Name == "result"
            && c.ToPin?.Owner == cteNode
            && c.ToPin.Name == "query"
        );
        if (alreadyConnected)
            return;

        var conn = new ConnectionViewModel(from, from.AbsolutePosition, to.AbsolutePosition)
        {
            ToPin = to,
        };

        from.IsConnected = true;
        to.IsConnected = true;
        Connections.Add(conn);
    }

    private static string BuildTemporaryCanvasJson(
        List<SavedNode> nodes,
        List<SavedConnection> connections,
        string description
    )
    {
        var saved = new SavedCanvas(
            Version: CanvasSerializer.CurrentSchemaVersion,
            DatabaseProvider: "Postgres",
            ConnectionName: "cte-editor",
            Zoom: 1.0,
            PanX: 0,
            PanY: 0,
            Nodes: nodes,
            Connections: connections,
            SelectBindings: [],
            WhereBindings: [],
            AppVersion: CanvasSerializer.AppVersion,
            CreatedAt: DateTime.UtcNow.ToString("o"),
            Description: description
        );

        return JsonSerializer.Serialize(saved);
    }

    // ── Node operations (delegated to NodeManager) ────────────────────────────

    public NodeViewModel SpawnNode(NodeDefinition def, Point pos) =>
        _nodeManager.SpawnNode(def, pos);

    public NodeViewModel SpawnTableNode(
        string table,
        IEnumerable<(string n, PinDataType t)> cols,
        Point pos
    ) => _nodeManager.SpawnTableNode(table, cols, pos);

    public void DeleteSelected() => _nodeManager.DeleteSelected();

    public void CleanupOrphans() => _nodeManager.CleanupOrphans();

    // ── Snippets ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves the currently selected nodes (and their internal connections) as a
    /// named snippet in the persistent snippet store.
    /// </summary>
    public void SaveSelectionAsSnippet(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        List<NodeViewModel> selected = _selectionManager.SelectedNodes();
        if (selected.Count == 0)
            return;

        (List<SavedNode> nodes, List<SavedConnection> conns) =
            CanvasSerializer.SerialiseSubgraph(selected, Connections);

        var snippet = new SavedSnippet(
            Id: Guid.NewGuid().ToString(),
            Name: name.Trim(),
            Tags: null,
            Description: null,
            CreatedAt: DateTime.UtcNow.ToString("o"),
            Nodes: nodes,
            Connections: conns
        );

        SnippetStore.Add(snippet);
        SearchMenu.LoadSnippets();
    }

    /// <summary>
    /// Inserts a saved snippet into the canvas, centered at <paramref name="canvasPos"/>,
    /// with fresh node IDs to avoid conflicts with the existing graph.
    /// </summary>
    public void InsertSnippet(SavedSnippet snippet, Point canvasPos) =>
        CanvasSerializer.InsertSubgraph(snippet.Nodes, snippet.Connections, this, canvasPos);

    /// <summary>
    /// Loads a query template by clearing the canvas and invoking the template's Build action.
    /// Resets undo history and dirty flag so the user starts with a clean slate.
    /// </summary>
    public void LoadTemplate(QueryTemplate template)
    {
        var stateBeforeTemplate = new RestoreCanvasStateCommand(this, "Load Template");

        Connections.Clear();
        Nodes.Clear();
        CurrentFilePath = null;
        QueryText = "";
        Zoom = 1.0;
        PanOffset = new Point(0, 0);
        template.Build(this);
        IsDirty = false;

        stateBeforeTemplate.CaptureAfterState(this);
        UndoRedo.Execute(stateBeforeTemplate);
    }

    /// <summary>
    /// Resets the canvas to a clean state and sets the database metadata.
    /// Called when a user connects to a new database.
    /// </summary>
    public void SetDatabaseAndResetCanvas(DbMetadata? metadata)
    {
        SetDatabaseAndResetCanvas(metadata, null);
    }

    /// <summary>
    /// Updates the database metadata and connection configuration without clearing the canvas.
    /// </summary>
    public void SetDatabaseContext(DbMetadata? metadata, ConnectionConfig? config)
    {
        DatabaseMetadata = metadata;
        ActiveConnectionConfig = config;
    }

    /// <summary>
    /// Resets the canvas to a clean state and sets the database metadata and connection config.
    /// Called when a user connects to a new database.
    /// </summary>
    public void SetDatabaseAndResetCanvas(DbMetadata? metadata, ConnectionConfig? config)
    {
        // Clear the current canvas
        Connections.Clear();
        Nodes.Clear();
        CurrentFilePath = null;
        QueryText = "";
        Zoom = 1.0;
        PanOffset = new Point(0, 0);
        IsDirty = false;
        UndoRedo.Clear();

        // Set the new database metadata and connection config
        DatabaseMetadata = metadata;
        ActiveConnectionConfig = config;

        // Reload demo nodes if no metadata provided
        if (metadata is null)
            _nodeManager.SpawnDemoNodes(UndoRedo);
    }

    // ── Naming & layout ───────────────────────────────────────────────────────

    /// <summary>Converts all alias violations to snake_case (undoable).</summary>
    public void AutoFixNaming()
    {
        var cmd = new AutoFixNamingCommand(Nodes);
        if (!cmd.HasChanges)
            return;
        UndoRedo.Execute(cmd);
    }

    /// <summary>Arranges nodes into logical columns (undoable). Pass a scope to layout only selected nodes.</summary>
    public void RunAutoLayout(IReadOnlyList<NodeViewModel>? scope = null)
    {
        if (Nodes.Count == 0)
            return;
        UndoRedo.Execute(new AutoLayoutCommand(this, scope));
    }

    // ── Pin & connection operations (delegated to PinManager) ─────────────────

    public void ConnectPins(PinViewModel from, PinViewModel to)
    {
        _pinManager.ConnectPins(from, to);
        IsDirty = true;
    }

    public void DeleteConnection(ConnectionViewModel conn)
    {
        _pinManager.DeleteConnection(conn);
        IsDirty = true;
    }

    internal void ClearNarrowingIfNeeded(IEnumerable<NodeViewModel> nodes) =>
        _pinManager.ClearNarrowingIfNeeded(nodes);

    // ── Selection (delegated to SelectionManager) ─────────────────────────────

    public void SelectAll() => _selectionManager.SelectAll();

    public void DeselectAll() => _selectionManager.DeselectAll();

    public void SelectNode(NodeViewModel node, bool add = false) =>
        _selectionManager.SelectNode(node, add);

    public bool BringSelectionToFront() =>
        ApplyLayerOrder("Bring to front", NodeLayerOrdering.BringToFront, requireSelection: true);

    public bool SendSelectionToBack() =>
        ApplyLayerOrder("Send to back", NodeLayerOrdering.SendToBack, requireSelection: true);

    public bool BringSelectionForward() =>
        ApplyLayerOrder("Bring forward", NodeLayerOrdering.BringForward, requireSelection: true);

    public bool SendSelectionBackward() =>
        ApplyLayerOrder("Send backward", NodeLayerOrdering.SendBackward, requireSelection: true);

    public bool NormalizeLayers() =>
        ApplyLayerOrder("Normalize layers", NodeLayerOrdering.OrderByZ, requireSelection: false);

    private bool ApplyLayerOrder(
        string action,
        Func<List<NodeViewModel>, List<NodeViewModel>> reorder,
        bool requireSelection
    )
    {
        List<NodeViewModel> all = Nodes.ToList();
        if (all.Count == 0)
            return false;
        if (requireSelection && all.All(n => !n.IsSelected))
            return false;

        Dictionary<NodeViewModel, int> from = all.ToDictionary(n => n, n => n.ZOrder);
        List<NodeViewModel> ordered = reorder(all);
        Dictionary<NodeViewModel, int> to = NodeLayerOrdering.BuildNormalizedMap(ordered);

        if (from.All(kv => to.TryGetValue(kv.Key, out int z) && z == kv.Value))
            return false;

        UndoRedo.Execute(new ReorderNodesCommand(action, from, to));
        return true;
    }

    // ── Coordinate transforms ─────────────────────────────────────────────────

    public void ZoomToward(Point screen, double factor)
    {
        double old = Zoom;
        Zoom = Math.Clamp(old * factor, 0.15, 4.0);
        PanOffset = new Point(
            screen.X - (screen.X - PanOffset.X) * (Zoom / old),
            screen.Y - (screen.Y - PanOffset.Y) * (Zoom / old)
        );
    }

    /// <summary>
    /// Informs the layout manager of the current viewport size so that
    /// <c>FitToScreen</c> can compute an accurate zoom level and pan offset.
    /// Called by <c>InfiniteCanvas.ArrangeOverride</c> after each layout pass.
    /// </summary>
    public void SetViewportSize(double width, double height) =>
        _layoutManager.SetViewportSize(width, height);

    public Point ScreenToCanvas(Point s) =>
        new((s.X - PanOffset.X) / Zoom, (s.Y - PanOffset.Y) / Zoom);

    public Point CanvasToScreen(Point c) =>
        new(c.X * Zoom + PanOffset.X, c.Y * Zoom + PanOffset.Y);

    // ── Query & export ────────────────────────────────────────────────────────

    public void UpdateQueryText(string sql)
    {
        QueryText = sql;
        DataPreview.QueryText = sql;
    }

    /// <summary>
    /// Finds the first export node of <paramref name="exportType"/> and triggers export.
    /// Returns the generated file path, or null if none found or export failed.
    /// </summary>
    public async Task<string?> TriggerExportAsync(NodeType exportType, string? overridePath = null)
    {
        NodeViewModel? node = Nodes.FirstOrDefault(n => n.Type == exportType);
        if (node is null)
            return null;
        return await ExportNodeHandler.RunExportAsync(this, node, overridePath);
    }

    public void ScheduleValidation() => _validationManager.ScheduleValidation();

    public static double Snap(double v) => NodeLayoutManager.Snap(v);

    /// <summary>Forwards to <see cref="NodeManager.DemoCatalog"/> — kept for backwards compatibility.</summary>
    public static IReadOnlyList<(
        string FullName,
        IReadOnlyList<(string Name, PinDataType Type)> Cols
    )> DemoCatalog => NodeManager.DemoCatalog;

    // ── Auto-join: canvas-driven analysis ────────────────────────────────────

    /// <summary>
    /// Analyses join opportunities involving <paramref name="newTableFullName"/>
    /// against all other table-source nodes currently on the canvas.
    /// Uses naming heuristics (no DB connection required) to suggest JOINs.
    /// </summary>
    public void TriggerAutoJoinAnalysis(string newTableFullName)
    {
        if (string.IsNullOrWhiteSpace(newTableFullName))
            return;

        // Need at least one other table to compare against
        List<NodeViewModel> tables = Nodes
            .Where(n => n.IsTableSource && n.Subtitle != newTableFullName)
            .ToList();
        if (tables.Count == 0)
            return;

        DbMetadata meta = BuildMetadataFromCanvas();
        var detector = new AutoJoinDetector(meta);
        List<string> candidateTables = tables
            .Select(GetTableIdentifier)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidateTables.Count == 0)
            return;

        IReadOnlyList<JoinSuggestion> suggestions = detector.Suggest(
            newTableFullName,
            candidateTables
        );

        if (suggestions.Count > 0)
            AutoJoin.Show(newTableFullName, suggestions);
    }

    /// <summary>
    /// Analyses ALL table-source pairs currently on the canvas and shows
    /// the join suggestion overlay if any high-confidence suggestions are found.
    /// </summary>
    public void AnalyzeAllCanvasJoins()
    {
        List<NodeViewModel> tables = Nodes.Where(n => n.IsTableSource).ToList();
        if (tables.Count < 2)
            return;

        DbMetadata meta = BuildMetadataFromCanvas();
        var detector = new AutoJoinDetector(meta);
        var allSuggestions = new List<JoinSuggestion>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (NodeViewModel t in tables)
        {
            string current = GetTableIdentifier(t);
            if (string.IsNullOrWhiteSpace(current))
                continue;

            IEnumerable<string> others = tables
                .Where(x => x != t)
                .Select(GetTableIdentifier)
                .Where(name => !string.IsNullOrWhiteSpace(name));

            foreach (JoinSuggestion s in detector.Suggest(current, others))
            {
                // De-duplicate symmetric pairs
                string[] pair = new[] { s.ExistingTable, s.NewTable };
                Array.Sort(pair, StringComparer.OrdinalIgnoreCase);
                string key = $"{pair[0]}|{pair[1]}|{s.LeftColumn}|{s.RightColumn}";
                if (seen.Add(key))
                    allSuggestions.Add(s);
            }
        }

        if (allSuggestions.Count > 0)
            AutoJoin.Show("canvas", allSuggestions.OrderByDescending(s => s.Score).ToList());
    }

    /// <summary>
    /// Builds a synthetic <see cref="DbMetadata"/> from the table-source nodes
    /// currently on the canvas.  Column data-types are inferred from pin types.
    /// FK relations are not populated here — the detector falls back to naming
    /// heuristics which do not require FK metadata.
    /// </summary>
    private DbMetadata BuildMetadataFromCanvas()
    {
        List<TableMetadata> tables = Nodes
            .Where(n => n.IsTableSource)
            .Select(BuildTableMetadata)
            .ToList();

        string schema = tables.Count > 0
            ? (tables[0].Schema ?? "public")
            : "public";

        return new DbMetadata(
            DatabaseName: "canvas",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "0",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata(schema, tables)],
            AllForeignKeys: []
        );
    }

    private static TableMetadata BuildTableMetadata(NodeViewModel node)
    {
        // node.Subtitle = full table name (e.g. "public.orders")
        // Safely handle null Subtitle and Title
        string full = node.Subtitle ?? node.Title ?? "unknown.table";
        string[] parts = full.Split('.', 2);
        string schema = parts.Length == 2 ? parts[0] : "public";
        string name = parts.Length == 2 ? parts[1] : full;

        List<ColumnMetadata> cols = node
            .OutputPins.Select((p, i) =>
            {
                string nativeType = p.DataType switch
                {
                    PinDataType.Number => "int",
                    PinDataType.Text => "varchar",
                    PinDataType.Boolean => "bool",
                    PinDataType.DateTime => "timestamp",
                    PinDataType.Json => "jsonb",
                    _ => "text",
                };
                bool isPk =
                    p.Name.Equals("id", StringComparison.OrdinalIgnoreCase) && i == 0;
                bool isFk =
                    p.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase)
                    && !isPk;
                return new ColumnMetadata(
                    Name: p.Name,
                    DataType: nativeType,
                    NativeType: nativeType,
                    IsNullable: !isPk,
                    IsPrimaryKey: isPk,
                    IsForeignKey: isFk,
                    IsUnique: isPk,
                    IsIndexed: isPk || isFk,
                    OrdinalPosition: i + 1
                );
            })
            .ToList();

        return new TableMetadata(
            Schema: schema,
            Name: name,
            Kind: TableKind.Table,
            EstimatedRowCount: null,
            Columns: cols,
            Indexes: [],
            OutboundForeignKeys: [],
            InboundForeignKeys: []
        );
    }

    private static string GetTableIdentifier(NodeViewModel node)
    {
        if (!string.IsNullOrWhiteSpace(node.Subtitle))
            return node.Subtitle;

        return node.Title ?? string.Empty;
    }

    // ── Auto-join handler ─────────────────────────────────────────────────────

    private static bool TrySplitJoinClauseOnEquality(
        string onClause,
        out string leftExpr,
        out string rightExpr)
    {
        leftExpr = string.Empty;
        rightExpr = string.Empty;

        if (string.IsNullOrWhiteSpace(onClause))
            return false;

        string clause = onClause.Trim();

        // Reject composite predicates and unsupported operators.
        if (clause.Contains(" AND ", StringComparison.OrdinalIgnoreCase)
            || clause.Contains(" OR ", StringComparison.OrdinalIgnoreCase)
            || clause.Contains(">=", StringComparison.Ordinal)
            || clause.Contains("<=", StringComparison.Ordinal)
            || clause.Contains("!=", StringComparison.Ordinal)
            || clause.Contains("==", StringComparison.Ordinal))
            return false;

        int idx = clause.IndexOf('=');
        if (idx <= 0 || idx >= clause.Length - 1)
            return false;

        // Ensure only a single equality operator exists.
        if (clause.IndexOf('=', idx + 1) >= 0)
            return false;

        leftExpr = clause[..idx].Trim();
        rightExpr = clause[(idx + 1)..].Trim();

        return leftExpr.Length > 0 && rightExpr.Length > 0;
    }

    private void OnJoinAccepted(object? _, JoinSuggestion suggestion)
    {
        NodeViewModel? fromTable = Nodes.FirstOrDefault(n =>
            n.Subtitle == suggestion.ExistingTable
            || n.Title == suggestion.ExistingTable.Split('.').Last()
        );
        NodeViewModel? toTable = Nodes.FirstOrDefault(n =>
            n.Subtitle == suggestion.NewTable
            || n.Title == suggestion.NewTable.Split('.').Last()
        );
        if (fromTable is null || toTable is null)
            return;

        if (!TrySplitJoinClauseOnEquality(suggestion.OnClause, out string leftExpr, out string rightExpr))
            return;

        string leftCol = leftExpr.Split('.').Last();
        string rightCol = rightExpr.Split('.').Last();

        PinViewModel? fromPin =
            fromTable.OutputPins.FirstOrDefault(p =>
                p.Name.Equals(leftCol, StringComparison.OrdinalIgnoreCase)
            )
            ?? toTable.OutputPins.FirstOrDefault(p =>
                p.Name.Equals(leftCol, StringComparison.OrdinalIgnoreCase)
            );

        PinViewModel? toPin =
            toTable.InputPins.FirstOrDefault(p =>
                p.Name.Equals(rightCol, StringComparison.OrdinalIgnoreCase)
            )
            ?? fromTable.InputPins.FirstOrDefault(p =>
                p.Name.Equals(rightCol, StringComparison.OrdinalIgnoreCase)
            );

        if (fromPin is not null && toPin is not null)
            ConnectPins(fromPin, toPin);
    }

    public void NotifySuccess(string message, string? details = null) =>
        Toasts.ShowSuccess(message, details);

    public void NotifyWarning(string message, string? details = null) =>
        Toasts.ShowWarning(message, details);

    public void NotifyError(string message, string? details = null) =>
        Toasts.ShowError(message, details);

    private void OnDataPreviewErrorNotified(string message, string? details) =>
        NotifyError(message, details);

    /// <summary>
    /// Disposes all resources and unsubscribes from all event handlers.
    /// Called when the CanvasViewModel is being replaced (e.g., Ctrl+N to create new canvas).
    /// Prevents memory leaks by releasing references to event handlers and collection handlers.
    /// </summary>
    public void Dispose()
    {
        // Dispose AutoJoin overlay (which clears its cards and handlers)
        AutoJoin?.Dispose();

        // Unsubscribe from LiveSql PropertyChanged
        if (_liveSqlPropertyChangedHandler is not null)
            LiveSql.PropertyChanged -= _liveSqlPropertyChangedHandler;

        DataPreview.ErrorNotified -= OnDataPreviewErrorNotified;

        // Unsubscribe from self PropertyChanged
        if (_selfPropertyChangedHandler is not null)
            this.PropertyChanged -= _selfPropertyChangedHandler;

        // Unsubscribe from layout manager PropertyChanged forwarding
        if (_layoutManagerPropertyChangedHandler is not null)
            _layoutManager.PropertyChanged -= _layoutManagerPropertyChangedHandler;

        if (_localizationPropertyChangedHandler is not null)
            LocalizationService.Instance.PropertyChanged -= _localizationPropertyChangedHandler;

        // Unsubscribe from AutoJoin events
        if (AutoJoin is not null)
            AutoJoin.JoinAccepted -= OnJoinAccepted;

        // Unsubscribe from collection changed handlers
        if (_nodesCollectionChangedHandler is not null)
            Nodes.CollectionChanged -= _nodesCollectionChangedHandler;

        if (_connectionsCollectionChangedHandler is not null)
            Connections.CollectionChanged -= _connectionsCollectionChangedHandler;

        // Unsubscribe from all node validation handlers
        foreach (var handler in _nodeValidationHandlers.Values)
            foreach (var node in Nodes)
                node.PropertyChanged -= handler;
        _nodeValidationHandlers.Clear();
    }
}
