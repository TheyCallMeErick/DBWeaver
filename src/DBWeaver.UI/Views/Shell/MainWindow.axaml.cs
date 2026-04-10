using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Material.Icons;
using System.ComponentModel;
using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.UI.Serialization;
using DBWeaver.UI.Controls;
using DBWeaver.UI.Controls.Ddl;
using DBWeaver.UI.Controls.Shell;
using DBWeaver.UI.Services.Ddl;
using DBWeaver.UI.Services;
using DBWeaver.UI.Services.CommandPalette;
using DBWeaver.UI.Services.Connection;
using DBWeaver.UI.Services.Input.ShortcutRegistry;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.Services.Modal;
using DBWeaver.UI.Services.Settings;
using DBWeaver.UI.Services.Theming;
using DBWeaver.UI.Services.Workspace.Models;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Shortcuts;
using DBWeaver.UI.ViewModels.Validation.Conventions;
using DBWeaver.UI.ViewModels.Validation.Conventions.Implementations;

namespace DBWeaver.UI;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly IGlobalModalManager _globalModalManager;

    private ShellViewModel CurrentShell => DataContext as ShellViewModel
        ?? throw new InvalidOperationException(
            L("error.mainWindow.invalidDataContext", "MainWindow DataContext must be a ShellViewModel.")
        );

    private CanvasViewModel CurrentVm => CurrentShell.ActiveCanvas ?? CurrentShell.Canvas
        ?? throw new InvalidOperationException(
            L("error.mainWindow.canvasNotInitialized", "CanvasViewModel was not initialized.")
        );

    private bool _canvasInitialized;
    private ContextMenu? _titleMenu;
    private bool _sidebarActionsWired;
    private readonly HashSet<ConnectionManagerViewModel> _wiredConnectionManagers = [];
    private ConnectionWorkspaceModule? _connectionModule;
    private SettingsWorkspaceModule? _settingsModule;
    private static readonly GridLength CollapsedRailWidth = new(24);
    private bool _queryModeLeftSidebarCollapsed;
    private bool _queryModeRightSidebarCollapsed;
    private bool _ddlModeLeftSidebarCollapsed;
    private bool _ddlModeRightSidebarCollapsed;

    // Services
    private MainWindowLayoutService? _layoutService;
    private SessionManagementService? _sessionService;
    private KeyboardInputHandler? _keyboardHandler;
    private FileOperationsService? _fileOps;
    private ExportService? _export;
    private PreviewService? _preview;
    private CommandPaletteFactory? _commandFactory;
    private ICommandPaletteService? _commandPaletteService;
    private IShortcutRegistry? _shortcutRegistry;
    private KeyboardShortcutsViewModel? _keyboardShortcutsViewModel;
    private PropertyChangedEventHandler? _windowTitleChangedHandler;
    private PropertyChangedEventHandler? _shellPropertyChangedHandler;
    private PropertyChangedEventHandler? _toastCenterPropertyChangedHandler;
    private PropertyChangedEventHandler? _outputPreviewPropertyChangedHandler;
    private readonly ThemeJsonSettingsService _themeJsonSettings;
    private bool _isGlobalModalManagerWired;
    private const double PreviewDockBaseBottomMargin = 44;
    private const double PreviewDockToastBottomMargin = 134;

    public MainWindow()
        : this(
            new ServiceCollection()
                .AddDBWeaver()
                .AddSingleton<IAliasConvention, SnakeCaseConvention>()
                .AddSingleton<IAliasConvention, CamelCaseConvention>()
                .AddSingleton<IAliasConvention, PascalCaseConvention>()
                .AddSingleton<IAliasConvention, ScreamingSnakeCaseConvention>()
                .AddSingleton<IAliasConventionRegistry, AliasConventionRegistry>()
                .AddSingleton<IGlobalModalManager>(_ => GlobalModalManager.Instance)
                .BuildServiceProvider(),
            new ShellViewModel(connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault()),
            new ThemeJsonSettingsService(),
            GlobalModalManager.Instance)
    {
    }

    public MainWindow(
        IServiceProvider services,
        ShellViewModel shell,
        ThemeJsonSettingsService themeJsonSettings,
        IGlobalModalManager? globalModalManager = null)
    {
        _services = services;
        _themeJsonSettings = themeJsonSettings;
        _globalModalManager = globalModalManager ?? GlobalModalManager.Instance;

        InitializeComponent();
        DataContext = shell;
        WireGlobalModalManager();

        WireHeaderMenus();
        WireToolbarMenuButtons();
        WireStartMenu();
        WireModeToggle();
        WirePreviewDock();
        Title = AppConstants.AppDisplayName;
    }

    private void WireGlobalModalManager()
    {
        if (_isGlobalModalManagerWired)
            return;

        _globalModalManager.ModalRequested += OnGlobalModalRequested;
        _isGlobalModalManagerWired = true;
        Closed += OnWindowClosed;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (!_isGlobalModalManagerWired)
            return;

        _globalModalManager.ModalRequested -= OnGlobalModalRequested;
        _isGlobalModalManagerWired = false;
        UnwirePreviewDock();
        Closed -= OnWindowClosed;
    }

    private void OnGlobalModalRequested(GlobalModalRequest request)
    {
        switch (request.Kind)
        {
            case GlobalModalKind.ConnectionManager:
                OpenConnectionsPanel(request.BeginNewProfile, request.KeepStartVisible);
                break;

            case GlobalModalKind.Settings:
                PopulateSettingsThemeJsonEditor();
                OpenSettings(request.KeepStartVisible);
                break;
        }
    }

    private void EnsureCanvasInitialized()
    {
        if (_canvasInitialized)
            return;

        CanvasViewModel vm = CurrentShell.EnsureCanvas(
            isDdlModeActiveResolver: () => CurrentShell.IsDdlDocumentPageActive,
            importDdlTableAction: (table, position) => ImportSingleTableToDdl(table, position));
        vm.SetCanvasContext(CurrentShell.ActiveCanvasContext);
        vm.ConnectionManager.IsVisible = false;

        InitializeServices(vm);
        AttachCanvasHandlers(vm);
        WireConnectionActivation(vm.ConnectionManager);
        WireSidebarActions(vm.Sidebar);
        WireSearchMenu();

        CurrentShell.StartMenu.RefreshData(vm.ConnectionManager.Profiles);
        Title = vm.WindowTitle;
        SyncModeToggleState();
        _canvasInitialized = true;
    }

    private void SyncCanvasContext()
    {
        if (!_canvasInitialized)
            return;

        if (CurrentShell.Canvas is not null)
            CurrentShell.Canvas.SetCanvasContext(CanvasContext.Query);

        if (CurrentShell.DdlCanvas is not null)
        {
            CanvasContext ddlContext = CurrentShell.ActiveCanvasContext == CanvasContext.ViewSubcanvas
                ? CanvasContext.ViewSubcanvas
                : CanvasContext.Ddl;

            CurrentShell.DdlCanvas.SetCanvasContext(ddlContext);
        }
    }

    private void WireSidebarActions(SidebarViewModel sidebar)
    {
        if (_sidebarActionsWired)
            return;

        sidebar.AddNodeRequested += () =>
        {
            EnterCanvasMode();
            OpenSearch();
        };

        sidebar.AddConnectionRequested += () =>
            _globalModalManager.RequestConnectionManager(beginNewProfile: true, keepStartVisible: false);

        sidebar.TogglePreviewRequested += () =>
        {
            EnterCanvasMode();
            _ = OpenModeAwareOutputPreviewSafeAsync();
        };

        _sidebarActionsWired = true;
    }

    private void WirePreviewDock()
    {
        _toastCenterPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName is nameof(ToastCenterViewModel.IsVisible) or nameof(ToastCenterViewModel.IsDetailsOpen))
                UpdatePreviewDockLayout();
        };

        _outputPreviewPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(OutputPreviewModalViewModel.IsVisible))
                UpdatePreviewDockLayout();
        };

        CurrentShell.Toasts.PropertyChanged += _toastCenterPropertyChangedHandler;
        CurrentShell.OutputPreview.PropertyChanged += _outputPreviewPropertyChangedHandler;
        UpdatePreviewDockLayout();
    }

    private void UnwirePreviewDock()
    {
        if (_toastCenterPropertyChangedHandler is not null)
            CurrentShell.Toasts.PropertyChanged -= _toastCenterPropertyChangedHandler;

        if (_outputPreviewPropertyChangedHandler is not null)
            CurrentShell.OutputPreview.PropertyChanged -= _outputPreviewPropertyChangedHandler;

        _toastCenterPropertyChangedHandler = null;
        _outputPreviewPropertyChangedHandler = null;
    }

    private void UpdatePreviewDockLayout()
    {
        Border? dock = this.FindControl<Border>("PreviewDockHost");
        if (dock is null)
            return;

        bool isDiagramPage = CurrentShell.IsDiagramDocumentPageActive;
        bool isBlockedByModal = CurrentShell.OutputPreview.IsVisible || CurrentShell.Toasts.IsDetailsOpen;
        dock.IsVisible = isDiagramPage && !isBlockedByModal;

        double bottomMargin = CurrentShell.Toasts.IsVisible
            ? PreviewDockToastBottomMargin
            : PreviewDockBaseBottomMargin;

        dock.Margin = new Thickness(0, 0, 0, bottomMargin);
    }

    private void WireStartMenu()
    {
        CurrentShell.StartMenu.CreateNewDiagramRequested += OnStartCreateNewDiagramRequested;
        CurrentShell.StartMenu.OpenConnectionsRequested += OnStartOpenConnectionsRequested;
        CurrentShell.StartMenu.OpenFromDiskRequested += OnStartOpenFromDiskRequested;
        CurrentShell.StartMenu.OpenSavedConnectionRequested += OnStartOpenSavedConnectionRequested;
        CurrentShell.StartMenu.OpenRecentProjectRequested += OnStartOpenRecentProjectRequested;
        CurrentShell.StartMenu.OpenTemplateRequested += OnStartOpenTemplateRequested;
        CurrentShell.StartMenu.OpenSettingsRequested += OnStartOpenSettingsRequested;
    }

    private void EnterCanvasMode()
    {
        EnsureCanvasInitialized();

        if (!CurrentShell.IsStartVisible)
            return;

        CurrentShell.EnterCanvas();
        SyncModeToggleState();
        SyncCanvasContext();
    }

    private void OnStartCreateNewDiagramRequested()
    {
        EnterCanvasMode();
        ResetCurrentCanvas();
    }

    private void OnStartOpenConnectionsRequested()
    {
        _globalModalManager.RequestConnectionManager(beginNewProfile: true, keepStartVisible: true);
    }


    private void OnStartOpenSavedConnectionRequested(StartSavedConnectionItem item)
    {
        if (GetConnectionModule().ConnectFromStartItem(item.Id))
            return;

        _globalModalManager.RequestConnectionManager(beginNewProfile: false, keepStartVisible: true);
    }

    private void OpenConnectionsPanel(bool beginNewProfile, bool keepStartVisible)
    {
        GetConnectionModule().OpenManager(beginNewProfile, keepStartVisible);
    }

    private ConnectionWorkspaceModule GetConnectionModule()
    {
        _connectionModule ??= new ConnectionWorkspaceModule(
            getConnectionManager: () =>
            {
                if (CurrentShell.ActiveCanvas is not null)
                {
                    WireConnectionActivation(CurrentShell.ActiveCanvas.ConnectionManager);
                    return CurrentShell.ActiveCanvas.ConnectionManager;
                }

                if (CurrentShell.IsDdlDocumentPageActive)
                {
                    CanvasViewModel ddlCanvas = CurrentShell.EnsureDdlCanvas();
                    WireConnectionActivation(ddlCanvas.ConnectionManager);
                    return ddlCanvas.ConnectionManager;
                }

                CanvasViewModel queryCanvas = CurrentShell.EnsureCanvas(
                    isDdlModeActiveResolver: () => CurrentShell.IsDdlDocumentPageActive,
                    importDdlTableAction: (table, position) => ImportSingleTableToDdl(table, position));
                WireConnectionActivation(queryCanvas.ConnectionManager);
                return queryCanvas.ConnectionManager;
            },
            activateConnectionSidebar: () =>
            {
                if (CurrentShell.ActiveCanvas is not null)
                {
                    CurrentShell.ActiveCanvas.Sidebar.ActiveTab = SidebarTab.Connection;
                    return;
                }

                if (CurrentShell.DdlCanvas is not null)
                {
                    CurrentShell.DdlCanvas.Sidebar.ActiveTab = SidebarTab.Connection;
                    return;
                }

                if (CurrentShell.Canvas is not null)
                    CurrentShell.Canvas.Sidebar.ActiveTab = SidebarTab.Connection;
            },
            enterCanvas: EnterCanvasMode
        );

        return _connectionModule;
    }

    private void WireConnectionActivation(ConnectionManagerViewModel connectionManager)
    {
        if (!_wiredConnectionManagers.Add(connectionManager))
            return;

        connectionManager.ConnectionActivated += _ =>
        {
            if (CurrentShell.IsStartVisible)
                EnterCanvasMode();

            CurrentShell.Toasts.ShowSuccess(
                L("toast.connectionActivated", "Conexao ativa."),
                L("toast.connectionActivatedDetails", "Pronto para executar consultas e operacoes de importacao."));
        };

        connectionManager.ConnectionFailed += (message, details) =>
        {
            string toastDetails = string.IsNullOrWhiteSpace(details)
                ? message
                : $"{message}{Environment.NewLine}{details}";

            CurrentShell.Toasts.ShowError(
                L("toast.connectionFailed", "Falha ao conectar."),
                toastDetails);

            CurrentShell.SqlEditor.NotifyConnectionContextChanged();
        };

        connectionManager.ProfilesChanged += () =>
        {
            CurrentShell.StartMenu.RefreshData(
                connectionManager.Profiles,
                connectionManager.ActiveProfileId
            );

            CurrentShell.SqlEditor.NotifyConnectionContextChanged();
        };

    }

    private async void OnStartOpenFromDiskRequested()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = L("file.openDialog.title", "Open Canvas"),
                FileTypeFilter =
                [
                    new FilePickerFileType("SQL Architect Canvas")
                    {
                        Patterns = ["*.vsaq"],
                        MimeTypes = ["application/json"],
                    },
                ],
                AllowMultiple = false,
            }
        );

        string? selectedPath = files.FirstOrDefault()?.TryGetLocalPath();
        if (selectedPath is null)
            return;

        EnterCanvasMode();
        if (_fileOps is not null)
            await _fileOps.OpenPathAsync(selectedPath);
    }

    private void OnStartOpenRecentProjectRequested(StartRecentProjectItem recent)
    {
        EnterCanvasMode();
        if (!string.IsNullOrWhiteSpace(recent.FilePath))
        {
            _ = _fileOps?.OpenPathAsync(recent.FilePath);
            return;
        }

        _ = _fileOps?.OpenAsync();
    }

    private void OnStartOpenTemplateRequested(StartTemplateItem item)
    {
        QueryTemplate? template = QueryTemplateLibrary.All.FirstOrDefault(t =>
            string.Equals(t.Name, item.Name, StringComparison.OrdinalIgnoreCase)
        );
        if (template is null)
            return;

        EnterCanvasMode();
        CurrentVm.LoadTemplate(template);
        InvalidateActiveDiagramCanvasWires();
    }

    private void AttachCanvasHandlers(CanvasViewModel vm)
    {
        _windowTitleChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(CanvasViewModel.WindowTitle))
                Title = vm.WindowTitle;

            if (e.PropertyName == nameof(CanvasViewModel.IsInViewEditor))
                CurrentShell.SetViewSubcanvasActive(vm.IsInViewEditor);
        };

        vm.PropertyChanged += _windowTitleChangedHandler;
    }

    private void DetachCanvasHandlers(CanvasViewModel vm)
    {
        if (_windowTitleChangedHandler is not null)
            vm.PropertyChanged -= _windowTitleChangedHandler;

        _windowTitleChangedHandler = null;
    }

    private void InitializeServices(CanvasViewModel vm)
    {
        _layoutService = ActivatorUtilities.CreateInstance<MainWindowLayoutService>(_services, this, vm);
        _sessionService = new SessionManagementService(
            window: this,
            vm: vm,
            ddlVmResolver: () => CurrentShell.DdlCanvas,
            activeDocumentTypeResolver: () => CurrentShell.ActiveWorkspaceDocumentType ?? WorkspaceDocumentType.QueryCanvas,
            applyActiveDocumentType: documentType =>
            {
                CurrentShell.SetActiveDocumentType(documentType);
                SyncModeToggleState();
            },
            workspaceDocumentsResolver: () => CurrentShell.OpenWorkspaceDocuments,
            activeWorkspaceDocumentIdResolver: () => CurrentShell.ActiveWorkspaceDocumentId,
            applyWorkspaceDocumentsSnapshot: snapshot =>
            {
                CurrentShell.RestoreWorkspaceDocuments(snapshot);
                SyncModeToggleState();
            },
            invalidateActiveCanvasWires: InvalidateActiveDiagramCanvasWires);
        _fileOps = new FileOperationsService(
            window: this,
            vm: vm,
            ddlVmResolver: () => CurrentShell.DdlCanvas,
            afterLoadCanvasStateChanged: (hasQueryContent, hasDdlContent) =>
            {
                if (!hasQueryContent && hasDdlContent)
                {
                    CurrentShell.SetActiveDocumentType(WorkspaceDocumentType.DdlCanvas);
                    SyncModeToggleState();
                }
            },
            activeDocumentTypeResolver: () => CurrentShell.ActiveWorkspaceDocumentType ?? WorkspaceDocumentType.QueryCanvas,
            applyActiveDocumentType: documentType =>
            {
                CurrentShell.SetActiveDocumentType(documentType);
                SyncModeToggleState();
            },
            workspaceDocumentsResolver: () => CurrentShell.OpenWorkspaceDocuments,
            activeWorkspaceDocumentIdResolver: () => CurrentShell.ActiveWorkspaceDocumentId,
            applyWorkspaceDocumentsSnapshot: snapshot =>
            {
                CurrentShell.RestoreWorkspaceDocuments(snapshot);
                SyncModeToggleState();
            },
            invalidateActiveCanvasWires: InvalidateActiveDiagramCanvasWires,
            logger: _services.GetService<ILogger<FileOperationsService>>()
        );
        _export = ActivatorUtilities.CreateInstance<ExportService>(_services, this, vm);
        _preview = ActivatorUtilities.CreateInstance<PreviewService>(_services, this, vm);
        _shortcutRegistry ??= new global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry();
        _commandFactory = new CommandPaletteFactory(
            this,
            () => CurrentShell.ActiveCanvas ?? CurrentVm,
            () => CurrentShell,
            _fileOps,
            _export,
            _preview,
            CreateNewQueryTab,
            _shortcutRegistry
        );
        _commandPaletteService = new CommandPaletteService(_commandFactory);
        _commandPaletteService.Refresh();
        CurrentShell.SetCommandPalette(_commandPaletteService.ViewModel);
        var canvasProvider = new ActiveCanvasProvider(
            () => CurrentShell.ActiveCanvas ?? vm
        );
        _keyboardHandler = new KeyboardInputHandler(
            this,
            canvasProvider,
            _fileOps,
            CurrentShell.CommandPalette,
            CreateNewQueryTab,
            showShortcutsAction: OpenKeyboardShortcutsWindow,
            canHandleCanvasShortcuts: () => CurrentShell.IsDiagramDocumentPageActive,
            openConnectionManagerAction: () => _globalModalManager.RequestConnectionManager(beginNewProfile: false, keepStartVisible: false),
            shortcutRegistry: _shortcutRegistry
        );

        _layoutService.Wire();
        _sessionService.Wire();
        _sessionService.CheckForSession();
        _keyboardHandler.Wire();
        _preview.Wire();
    }


    private static IBrush ResourceBrush(string key, string fallbackHex)
    {
        if (Application.Current?.TryFindResource(key, out object? resource) == true && resource is IBrush brush)
            return brush;

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private static string LF(string key, string fallbackFormat, params object[] args)
    {
        return string.Format(L(key, fallbackFormat), args);
    }

    private KeyboardShortcutsViewModel GetKeyboardShortcutsViewModel()
    {
        _shortcutRegistry ??= new global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry();
        _keyboardShortcutsViewModel ??= new KeyboardShortcutsViewModel(_shortcutRegistry);
        return _keyboardShortcutsViewModel;
    }

    private void OpenKeyboardShortcutsWindow()
    {
        KeyboardShortcutsViewModel viewModel = GetKeyboardShortcutsViewModel();
        viewModel.Refresh();
        new KeyboardShortcutsWindow(viewModel).Show(this);
    }

    private void PreviewDockOpenBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        EnterCanvasMode();
        _ = OpenModeAwareOutputPreviewSafeAsync();
        e.Handled = true;
    }

    private void PreviewDockDiagnosticsBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        EnterCanvasMode();
        _ = OpenOutputDiagnosticsTabSafeAsync();
        e.Handled = true;
    }

    private async Task OpenOutputDiagnosticsTabSafeAsync()
    {
        try
        {
            await OpenModeAwareOutputPreviewAsync();
            CurrentShell.OutputPreview.ShowDiagnosticsTabCommand.Execute(null);
        }
        catch (Exception ex)
        {
            CurrentShell.Toasts.ShowError(L("toast.previewOpenFailed", "Falha ao abrir preview."), ex.Message);
        }
    }

    private void ToastDetailsBackdrop_PointerPressed(object? s, PointerPressedEventArgs e)
    {
        CurrentShell.Toasts.CloseDetailsCommand.Execute(null);
        e.Handled = true;
    }

    private void ToastDetailsDialog_PointerPressed(object? s, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void ClearCanvasPromptBackdrop_PointerPressed(object? s, PointerPressedEventArgs e)
    {
        CurrentVm.ConnectionManager.CloseClearCanvasPromptCommand.Execute(null);
        e.Handled = true;
    }

    private void ClearCanvasPromptDialog_PointerPressed(object? s, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void WireSearchMenu()
    {
        WireSearchOverlay(this.FindControl<DiagramDocumentPageControl>("QueryDocumentPage"));
        WireSearchOverlay(this.FindControl<DiagramDocumentPageControl>("DdlDocumentPage"));
    }

    private void WireSearchOverlay(DiagramDocumentPageControl? pageControl)
    {
        SearchMenuControl? overlay = pageControl?.SearchOverlayControl;
        if (overlay is null)
            return;

        overlay.SpawnRequested += (_, def) =>
        {
            CanvasViewModel activeCanvas = CurrentShell.ActiveCanvas ?? CurrentVm;
            activeCanvas.SpawnNode(def, activeCanvas.SearchMenu.SpawnPosition);
            InvalidateActiveDiagramCanvasWires();
        };

        overlay.SpawnTableRequested += (_, args) =>
        {
            CanvasViewModel activeCanvas = CurrentShell.ActiveCanvas ?? CurrentVm;
            activeCanvas.SpawnTableNode(
                args.FullName,
                args.Cols.Select(c => (c.Name, c.Type)),
                activeCanvas.SearchMenu.SpawnPosition
            );
            InvalidateActiveDiagramCanvasWires();
            // Trigger join analysis after the node is added
            activeCanvas.TriggerAutoJoinAnalysis(args.FullName);
        };

        overlay.SnippetRequested += (_, snippet) =>
        {
            CanvasViewModel activeCanvas = CurrentShell.ActiveCanvas ?? CurrentVm;
            activeCanvas.InsertSnippet(snippet, activeCanvas.SearchMenu.SpawnPosition);
            InvalidateActiveDiagramCanvasWires();
        };
    }

    private void OpenSearch()
    {
        CanvasViewModel activeCanvas = CurrentShell.ActiveCanvas ?? CurrentVm;
        InfiniteCanvas? canvas = GetActiveDocumentCanvasControl();
        Point ctr = canvas is not null
            ? activeCanvas.ScreenToCanvas(new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2))
            : new Point(400, 300);
        activeCanvas.SearchMenu.Open(ctr);
    }

    private InfiniteCanvas? GetActiveDocumentCanvasControl()
    {
        if (CurrentShell.IsDdlDocumentPageActive)
            return this.FindControl<DiagramDocumentPageControl>("DdlDocumentPage")?.CanvasControl;

        return this.FindControl<DiagramDocumentPageControl>("QueryDocumentPage")?.CanvasControl;
    }

    private bool TryCloseTopModalOnEscape()
    {
        if (CurrentVm.ConnectionManager.IsClearCanvasPromptVisible)
        {
            CurrentVm.ConnectionManager.CloseClearCanvasPromptCommand.Execute(null);
            return true;
        }

        if (CurrentShell.Toasts.IsDetailsOpen)
        {
            CurrentShell.Toasts.CloseDetailsCommand.Execute(null);
            return true;
        }

        return false;
    }

    private void LeftSidebarHideBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool isCollapsed = CurrentShell.IsDdlDocumentPageActive
            ? _ddlModeLeftSidebarCollapsed
            : _queryModeLeftSidebarCollapsed;
        SetLeftSidebarCollapsed(!isCollapsed);
        e.Handled = true;
    }

    private void RightSidebarHideBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool isCollapsed = CurrentShell.IsDdlDocumentPageActive
            ? _ddlModeRightSidebarCollapsed
            : _queryModeRightSidebarCollapsed;
        SetRightSidebarCollapsed(!isCollapsed);
        e.Handled = true;
    }

    private void SetLeftSidebarCollapsed(bool collapsed)
    {
        Grid? bodyGrid = this.FindControl<Grid>("BodyGrid");
        if (bodyGrid is null || bodyGrid.ColumnDefinitions.Count < 5)
            return;

        ColumnDefinition sidebarColumn = bodyGrid.ColumnDefinitions[0];
        ColumnDefinition splitterColumn = bodyGrid.ColumnDefinitions[1];
        Border? sidebarHost = this.FindControl<Border>("LeftSidebarHost");
        Button? hideBtn = this.FindControl<Button>("LeftSidebarHideBtn");
        Material.Icons.Avalonia.MaterialIcon? toggleIcon = this.FindControl<Material.Icons.Avalonia.MaterialIcon>("LeftSidebarToggleIcon");

        if (sidebarHost is null)
            return;

        if (CurrentShell.IsQueryDocumentPageActive)
            _queryModeLeftSidebarCollapsed = collapsed;
        else if (CurrentShell.IsDdlDocumentPageActive)
            _ddlModeLeftSidebarCollapsed = collapsed;

        if (collapsed)
        {
            sidebarColumn.MinWidth = CollapsedRailWidth.Value;
            sidebarColumn.MaxWidth = CollapsedRailWidth.Value;
            sidebarColumn.Width = CollapsedRailWidth;
            splitterColumn.Width = new GridLength(0);
            sidebarHost.IsVisible = false;
            if (hideBtn is not null)
                hideBtn.IsVisible = true;
            if (toggleIcon is not null)
                toggleIcon.Kind = MaterialIconKind.ChevronRight;
            return;
        }

        sidebarColumn.MinWidth = 344;
        sidebarColumn.MaxWidth = 344;
        sidebarColumn.Width = new GridLength(344);
        splitterColumn.Width = new GridLength(0);
        sidebarHost.IsVisible = true;
        if (hideBtn is not null)
            hideBtn.IsVisible = true;
        if (toggleIcon is not null)
            toggleIcon.Kind = MaterialIconKind.ChevronLeft;
    }

    private void SetRightSidebarCollapsed(bool collapsed)
    {
        Grid? bodyGrid = this.FindControl<Grid>("BodyGrid");
        if (bodyGrid is null || bodyGrid.ColumnDefinitions.Count < 5)
            return;

        ColumnDefinition splitterColumn = bodyGrid.ColumnDefinitions[3];
        ColumnDefinition sidebarColumn = bodyGrid.ColumnDefinitions[4];
        Border? sidebarHost = this.FindControl<Border>("RightSidebarHost");
        Button? hideBtn = this.FindControl<Button>("RightSidebarHideBtn");
        Material.Icons.Avalonia.MaterialIcon? toggleIcon = this.FindControl<Material.Icons.Avalonia.MaterialIcon>("RightSidebarToggleIcon");

        if (sidebarHost is null)
            return;

        if (CurrentShell.IsQueryDocumentPageActive)
            _queryModeRightSidebarCollapsed = collapsed;
        else if (CurrentShell.IsDdlDocumentPageActive)
            _ddlModeRightSidebarCollapsed = collapsed;

        if (collapsed)
        {
            sidebarColumn.MinWidth = CollapsedRailWidth.Value;
            sidebarColumn.MaxWidth = CollapsedRailWidth.Value;
            sidebarColumn.Width = CollapsedRailWidth;
            splitterColumn.Width = new GridLength(0);
            sidebarHost.IsVisible = false;
            if (hideBtn is not null)
                hideBtn.IsVisible = true;
            if (toggleIcon is not null)
                toggleIcon.Kind = MaterialIconKind.ChevronLeft;
            return;
        }

        sidebarColumn.MinWidth = 344;
        sidebarColumn.MaxWidth = 344;
        sidebarColumn.Width = new GridLength(344);
        splitterColumn.Width = new GridLength(0);
        sidebarHost.IsVisible = true;
        if (hideBtn is not null)
            hideBtn.IsVisible = true;
        if (toggleIcon is not null)
            toggleIcon.Kind = MaterialIconKind.ChevronRight;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && TryCloseTopModalOnEscape())
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
        _keyboardHandler?.OnKeyDown(this, e);
    }
}
