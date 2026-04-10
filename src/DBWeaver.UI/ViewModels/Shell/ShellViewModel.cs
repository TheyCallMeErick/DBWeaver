using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DBWeaver.Core;
using DBWeaver.Metadata;
using Avalonia;
using DBWeaver.UI.Services.ConnectionManager.Models;
using DBWeaver.UI.Services.ConnectionManager;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.Services.Workspace;
using DBWeaver.UI.Services.Workspace.Diagnostics;
using DBWeaver.UI.Services.Workspace.Models;
using DBWeaver.UI.Services.Workspace.Pages;
using DBWeaver.UI.Services.Workspace.Preview;
using DBWeaver.UI.Serialization;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas.Strategies;

namespace DBWeaver.UI.ViewModels;

/// <summary>
/// Coordinates the application shell flow between Start Menu and Canvas work area.
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
    public enum AppMode
    {
        Query,
        Ddl,
        SqlEditor,
    }

    public enum ESettingsSection
    {
        Appearance,
        LanguageRegion,
        DateTime,
        KeyboardShortcuts,
        Privacy,
        Notification,
        Accessibility,
    }

    private bool _isStartVisible = true;
    private bool _isSettingsVisible;
    private ESettingsSection _selectedSettingsSection = ESettingsSection.Appearance;
    private AppMode _activeMode = AppMode.Query;
    private bool _isViewSubcanvasActive;
    private CanvasViewModel? _canvas;
    private CanvasViewModel? _ddlCanvas;
    private CanvasContext _activeCanvasContext = CanvasContext.Query;
    private ConnectionManagerViewModel? _observedQueryConnectionManager;
    private ConnectionManagerViewModel? _observedDdlConnectionManager;
    private PropertyChangedEventHandler? _activeConnectionManagerPropertyChanged;
    private readonly ILocalizationService _localization;
    private readonly ISqlEditorViewModelFactory _sqlEditorViewModelFactory;
    private readonly IWorkspaceRouter _workspaceRouter;
    private readonly IWorkspaceDocumentPageContractRegistry _pageContractRegistry;
    private readonly IWorkspaceDocumentPreviewContractRegistry _previewContractRegistry;
    private readonly IWorkspaceDocumentDiagnosticsContractRegistry _diagnosticsContractRegistry;
    private readonly IConnectionManagerViewModelFactory _connectionManagerViewModelFactory;
    private PropertyChangedEventHandler? _localizationPropertyChanged;
    private PropertyChangedEventHandler? _outputPreviewPropertyChanged;
    private Guid? _queryDocumentId;
    private Guid? _ddlDocumentId;

    public ShellViewModel(
        CanvasViewModel? canvas = null,
        ILocalizationService? localization = null,
        ISqlEditorViewModelFactory? sqlEditorViewModelFactory = null,
        IWorkspaceRouter? workspaceRouter = null,
        IWorkspaceDocumentPageContractRegistry? pageContractRegistry = null,
        IWorkspaceDocumentPreviewContractRegistry? previewContractRegistry = null,
        IWorkspaceDocumentDiagnosticsContractRegistry? diagnosticsContractRegistry = null,
        IConnectionManagerViewModelFactory? connectionManagerViewModelFactory = null)
    {
        _localization = localization ?? LocalizationService.Instance;
        _sqlEditorViewModelFactory = sqlEditorViewModelFactory ?? new SqlEditorViewModelFactory(_localization);
        _workspaceRouter = workspaceRouter ?? new WorkspaceRouter();
        _pageContractRegistry = pageContractRegistry ?? new WorkspaceDocumentPageContractRegistry();
        _previewContractRegistry = previewContractRegistry ?? new WorkspaceDocumentPreviewContractRegistry();
        _diagnosticsContractRegistry = diagnosticsContractRegistry ?? new WorkspaceDocumentDiagnosticsContractRegistry();
        _connectionManagerViewModelFactory = connectionManagerViewModelFactory
            ?? throw new ArgumentNullException(nameof(connectionManagerViewModelFactory));
        Toasts = canvas?.Toasts ?? new ToastCenterViewModel();
        _canvas = canvas;
        StartMenu = new StartMenuViewModel();
        LeftSidebar = new LeftSidebarViewModel();
        RightSidebar = new RightSidebarViewModel();
        OutputPreview = new OutputPreviewModalViewModel();
        SqlEditor = _sqlEditorViewModelFactory.Create(new SqlEditorViewModelFactoryContext
        {
            ConnectionConfigResolver = ResolveSharedActiveConnectionConfig,
            ConnectionConfigByProfileIdResolver = ResolveSqlEditorConnectionByProfileId,
            ConnectionProfilesResolver = ResolveSqlEditorConnectionProfiles,
            MetadataResolver = ResolveSharedMetadata,
            SharedConnectionManagerResolver = ResolveSharedConnectionManager,
        });
        QueryModeCommand = new RelayCommand(() => SetActiveMode(AppMode.Query));
        DdlModeCommand = new RelayCommand(() => SetActiveMode(AppMode.Ddl));
        SqlEditorModeCommand = new RelayCommand(() => SetActiveMode(AppMode.SqlEditor));
        RefreshConnectionManagerObservers();
        _localizationPropertyChanged = (_, e) =>
        {
            if (e.PropertyName is "" or "Item[]" or nameof(ILocalizationService.CurrentCulture))
            {
                RaisePropertyChanged(nameof(SettingsSectionTitle));
                RaisePropertyChanged(nameof(SettingsSectionSubtitle));
            }
        };
        _localization.PropertyChanged += _localizationPropertyChanged;
        _outputPreviewPropertyChanged = (_, e) =>
        {
            if (e.PropertyName == nameof(OutputPreviewModalViewModel.IsVisible))
                RaisePropertyChanged(nameof(IsOutputPreviewModalVisible));
        };
        OutputPreview.PropertyChanged += _outputPreviewPropertyChanged;
        if (_canvas is not null)
            RegisterOrUpdateQueryDocument(_canvas);
        ActivateDocumentForMode(ActiveMode);
        SyncExtractedPanels();
    }

    public CanvasViewModel? Canvas
    {
        get => _canvas;
        private set
        {
            if (!Set(ref _canvas, value))
                return;

            AttachCanvasObservers(value);
            RaisePropertyChanged(nameof(ActiveCanvas));
            RaisePropertyChanged(nameof(ActiveConnectionManager));
            RaisePropertyChanged(nameof(IsConnectionManagerVisible));
            RaisePropertyChanged(nameof(IsConnectionManagerOverlayVisible));
            SqlEditor.NotifyConnectionContextChanged();
            if (value is not null)
                RegisterOrUpdateQueryDocument(value);
            SyncExtractedPanels();
        }
    }

    public StartMenuViewModel StartMenu { get; }

    public QueryTabManagerViewModel QueryTabs { get; } = new();

    public SqlEditorViewModel SqlEditor { get; }

    public IReadOnlyList<OpenWorkspaceDocument> OpenWorkspaceDocuments => _workspaceRouter.OpenDocuments;

    public Guid? ActiveWorkspaceDocumentId => _workspaceRouter.ActiveDocumentId;

    public OpenWorkspaceDocument? ActiveWorkspaceDocument => _workspaceRouter.ActiveDocument;

    public WorkspaceDocumentType? ActiveWorkspaceDocumentType => ActiveWorkspaceDocument?.Descriptor.DocumentType;

    public WorkspaceDocumentPageContract ActivePageContract =>
        _pageContractRegistry.Resolve(ActiveWorkspaceDocumentType ?? WorkspaceDocumentType.QueryCanvas);

    public WorkspaceDocumentPreviewContract ActivePreviewContract =>
        _previewContractRegistry.Resolve(ActiveWorkspaceDocumentType ?? WorkspaceDocumentType.QueryCanvas);

    public WorkspaceDocumentDiagnosticsContract ActiveDiagnosticsContract =>
        _diagnosticsContractRegistry.Resolve(ActiveWorkspaceDocumentType ?? WorkspaceDocumentType.QueryCanvas);

    public bool IsQueryDocumentPageActive => ActiveWorkspaceDocumentType == WorkspaceDocumentType.QueryCanvas;

    public bool IsDdlDocumentPageActive => ActiveWorkspaceDocumentType == WorkspaceDocumentType.DdlCanvas;

    public bool IsSqlEditorDocumentPageActive => ActiveWorkspaceDocumentType == WorkspaceDocumentType.SqlEditor;

    public bool IsDiagramDocumentPageActive => IsQueryDocumentPageActive || IsDdlDocumentPageActive;

    public LeftSidebarViewModel LeftSidebar { get; }

    public RightSidebarViewModel RightSidebar { get; }

    public ToastCenterViewModel Toasts { get; }
    public CommandPaletteViewModel CommandPalette { get; private set; } = new();

    public OutputPreviewModalViewModel OutputPreview { get; }

    public RelayCommand QueryModeCommand { get; }

    public RelayCommand DdlModeCommand { get; }

    public RelayCommand SqlEditorModeCommand { get; }

    public bool IsStartVisible
    {
        get => _isStartVisible;
        private set
        {
            if (!Set(ref _isStartVisible, value))
                return;

            RaisePropertyChanged(nameof(IsCanvasVisible));
            RaisePropertyChanged(nameof(IsDiagramOverlayLayerVisible));
            RaisePropertyChanged(nameof(IsConnectionManagerOverlayVisible));
            RaisePropertyChanged(nameof(IsOutputPreviewModalVisible));
        }
    }

    public bool IsCanvasVisible => !IsStartVisible;

    public AppMode ActiveMode
    {
        get => _activeMode;
        private set
        {
            if (!Set(ref _activeMode, value))
                return;

            RaisePropertyChanged(nameof(IsQueryModeActive));
            RaisePropertyChanged(nameof(IsDdlModeActive));
            RaisePropertyChanged(nameof(IsSqlEditorModeActive));
            RaisePropertyChanged(nameof(IsDiagramModeActive));
        }
    }

    public bool IsQueryModeActive => ActiveMode == AppMode.Query;

    public bool IsDdlModeActive => ActiveMode == AppMode.Ddl;

    public bool IsSqlEditorModeActive => ActiveMode == AppMode.SqlEditor;

    public bool IsDiagramModeActive => IsDiagramDocumentPageActive;

    public CanvasContext ActiveCanvasContext
    {
        get => _activeCanvasContext;
        private set => Set(ref _activeCanvasContext, value);
    }

    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        private set => Set(ref _isSettingsVisible, value);
    }

    public bool IsConnectionManagerVisible => ActiveConnectionManager?.IsVisible == true;

    public bool IsDiagramOverlayLayerVisible => IsCanvasVisible && IsDiagramDocumentPageActive;

    public bool IsConnectionManagerOverlayVisible =>
        ActiveConnectionManager?.IsVisible == true;

    public bool IsOutputPreviewModalVisible =>
        IsDiagramOverlayLayerVisible && OutputPreview.IsVisible;

    public CanvasViewModel? ActiveCanvas =>
        ActiveWorkspaceDocument?.DocumentViewModel as CanvasViewModel;

    public ConnectionManagerViewModel? ActiveConnectionManager =>
        ActiveWorkspaceDocumentType switch
        {
            WorkspaceDocumentType.DdlCanvas => DdlCanvas?.ConnectionManager ?? Canvas?.ConnectionManager,
            WorkspaceDocumentType.QueryCanvas => Canvas?.ConnectionManager ?? DdlCanvas?.ConnectionManager,
            WorkspaceDocumentType.SqlEditor => ResolveSharedConnectionManager(),
            _ => ActiveCanvas?.ConnectionManager ?? ResolveSharedConnectionManager(),
        };

    public SidebarViewModel? ActiveDiagramSidebar => ActiveCanvas?.Sidebar;

    public PropertyPanelViewModel? ActiveDiagramPropertyPanel => ActiveCanvas?.PropertyPanel;

    public CanvasViewModel? ActiveQueryCanvasDocument =>
        ActiveWorkspaceDocumentType == WorkspaceDocumentType.QueryCanvas
            ? ActiveWorkspaceDocument?.DocumentViewModel as CanvasViewModel
            : null;

    public CanvasViewModel? ActiveDdlCanvasDocument =>
        ActiveWorkspaceDocumentType == WorkspaceDocumentType.DdlCanvas
            ? ActiveWorkspaceDocument?.DocumentViewModel as CanvasViewModel
            : null;

    public SqlEditorViewModel? ActiveSqlEditorDocument =>
        ActiveWorkspaceDocumentType == WorkspaceDocumentType.SqlEditor
            ? ActiveWorkspaceDocument?.DocumentViewModel as SqlEditorViewModel
            : null;

    public CanvasViewModel? DdlCanvas
    {
        get => _ddlCanvas;
        private set
        {
            if (!Set(ref _ddlCanvas, value))
                return;

            RefreshConnectionManagerObservers();
            RaisePropertyChanged(nameof(ActiveCanvas));
            RaisePropertyChanged(nameof(ActiveConnectionManager));
            RaisePropertyChanged(nameof(IsConnectionManagerVisible));
            RaisePropertyChanged(nameof(IsConnectionManagerOverlayVisible));
            SyncExtractedPanels();
        }
    }

    public string AppVersionLabel => ResolveAppVersionLabel();

    public ESettingsSection SelectedSettingsSection
    {
        get => _selectedSettingsSection;
        private set
        {
            if (!Set(ref _selectedSettingsSection, value))
                return;

            RaisePropertyChanged(nameof(IsAppearanceSectionSelected));
            RaisePropertyChanged(nameof(IsLanguageRegionSectionSelected));
            RaisePropertyChanged(nameof(IsDateTimeSectionSelected));
            RaisePropertyChanged(nameof(IsKeyboardShortcutsSectionSelected));
            RaisePropertyChanged(nameof(IsPrivacySectionSelected));
            RaisePropertyChanged(nameof(IsNotificationSectionSelected));
            RaisePropertyChanged(nameof(IsAccessibilitySectionSelected));
            RaisePropertyChanged(nameof(SettingsSectionTitle));
            RaisePropertyChanged(nameof(SettingsSectionSubtitle));
        }
    }

    public string SettingsSectionTitle => SelectedSettingsSection switch
    {
        ESettingsSection.Appearance => Localize("settings.section.appearance.title", "Themes"),
        ESettingsSection.LanguageRegion => Localize("settings.section.languageRegion.title", "Language & Region"),
        ESettingsSection.DateTime => Localize("settings.section.dateTime.title", "Date & Time"),
        ESettingsSection.KeyboardShortcuts => Localize("settings.section.keyboard.title", "Keyboard Shortcuts"),
        ESettingsSection.Privacy => Localize("settings.section.privacy.title", "Privacy"),
        ESettingsSection.Notification => Localize("settings.section.notification.title", "Notification"),
        ESettingsSection.Accessibility => Localize("settings.section.accessibility.title", "Accessibility"),
        _ => Localize("settings.section.default.title", "Settings"),
    };

    public string SettingsSectionSubtitle => SelectedSettingsSection switch
    {
        ESettingsSection.Appearance => Localize("settings.section.appearance.subtitle", "Choose your style or customize your theme"),
        ESettingsSection.LanguageRegion => Localize("settings.section.languageRegion.subtitle", "Manage language and regional formatting"),
        ESettingsSection.DateTime => Localize("settings.section.wip.subtitle", "Work in progress."),
        ESettingsSection.KeyboardShortcuts => Localize("settings.section.keyboard.subtitle", "Customize keyboard shortcuts used by command palette and canvas execution."),
        ESettingsSection.Privacy => Localize("settings.section.wip.subtitle", "Work in progress."),
        ESettingsSection.Notification => Localize("settings.section.wip.subtitle", "Work in progress."),
        ESettingsSection.Accessibility => Localize("settings.section.wip.subtitle", "Work in progress."),
        _ => Localize("settings.section.default.subtitle", "Application settings"),
    };

    public bool IsAppearanceSectionSelected => SelectedSettingsSection == ESettingsSection.Appearance;
    public bool IsLanguageRegionSectionSelected => SelectedSettingsSection == ESettingsSection.LanguageRegion;
    public bool IsDateTimeSectionSelected => SelectedSettingsSection == ESettingsSection.DateTime;
    public bool IsKeyboardShortcutsSectionSelected => SelectedSettingsSection == ESettingsSection.KeyboardShortcuts;
    public bool IsPrivacySectionSelected => SelectedSettingsSection == ESettingsSection.Privacy;
    public bool IsNotificationSectionSelected => SelectedSettingsSection == ESettingsSection.Notification;
    public bool IsAccessibilitySectionSelected => SelectedSettingsSection == ESettingsSection.Accessibility;

    public CanvasViewModel EnsureCanvas(Func<bool>? isDdlModeActiveResolver = null, Action<TableMetadata, Point>? importDdlTableAction = null)
    {
        if (ActiveQueryCanvasDocument is not null)
            return ActiveQueryCanvasDocument;

        if (Canvas is null)
            Canvas = new CanvasViewModel(
                nodeManager: null,
                pinManager: null,
                selectionManager: null,
                localizationService: null,
                domainStrategy: new QueryDomainStrategy(isDdlModeActiveResolver, importDdlTableAction),
                toastCenter: Toasts,
                connectionManagerFactory: _connectionManagerViewModelFactory);

        return Canvas;
    }

    public CanvasViewModel EnsureDdlCanvas()
    {
        if (ActiveDdlCanvasDocument is not null)
            return ActiveDdlCanvasDocument;

        if (DdlCanvas is null)
            DdlCanvas = new CanvasViewModel(
                nodeManager: null,
                pinManager: null,
                selectionManager: null,
                localizationService: null,
                domainStrategy: new DdlDomainStrategy(),
                toastCenter: Toasts,
                connectionManagerFactory: _connectionManagerViewModelFactory);

        RegisterOrUpdateDdlDocument(DdlCanvas);

        return DdlCanvas;
    }

    public void SetActiveMode(AppMode mode)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported app mode.");

        WorkspaceDocumentType documentType = mode switch
        {
            AppMode.Ddl => WorkspaceDocumentType.DdlCanvas,
            AppMode.SqlEditor => WorkspaceDocumentType.SqlEditor,
            _ => WorkspaceDocumentType.QueryCanvas,
        };

        SetActiveDocumentType(documentType);
    }

    public void SetActiveDocumentType(WorkspaceDocumentType documentType)
    {
        if (ActiveWorkspaceDocumentType == documentType)
            return;

        OpenWorkspaceDocument? target = _workspaceRouter.OpenDocuments
            .LastOrDefault(document => document.Descriptor.DocumentType == documentType);
        bool changed = target is not null && _workspaceRouter.TryActivate(target.Descriptor.DocumentId);
        if (!changed)
        {
            _ = OpenNewDocument(documentType);
            return;
        }

        SynchronizeModeFromActiveDocument();
        RaiseActiveDocumentPropertiesChanged();
        SyncExtractedPanels();
    }

    public Guid OpenNewDocument(WorkspaceDocumentType documentType)
    {
        OpenWorkspaceDocument? existing = _workspaceRouter.OpenDocuments
            .LastOrDefault(document => document.Descriptor.DocumentType == documentType);
        if (existing is not null)
        {
            _workspaceRouter.TryActivate(existing.Descriptor.DocumentId);
            SynchronizeModeFromActiveDocument();
            RaiseActiveDocumentPropertiesChanged();
            SyncExtractedPanels();
            return existing.Descriptor.DocumentId;
        }

        return documentType switch
        {
            WorkspaceDocumentType.DdlCanvas => OpenNewDdlDocument(),
            WorkspaceDocumentType.SqlEditor => OpenNewSqlEditorDocument(),
            _ => OpenNewQueryDocument(),
        };
    }

    public bool TryActivateWorkspaceDocument(Guid documentId)
    {
        if (!_workspaceRouter.TryActivate(documentId))
            return false;

        SynchronizeModeFromActiveDocument();
        RaiseActiveDocumentPropertiesChanged();
        SyncExtractedPanels();
        return true;
    }

    public bool TryCloseWorkspaceDocument(Guid documentId)
    {
        bool closed = _workspaceRouter.TryClose(documentId);
        if (!closed)
            return false;

        if (_workspaceRouter.ActiveDocument is null)
            _ = OpenNewDocument(WorkspaceDocumentType.QueryCanvas);

        SynchronizeModeFromActiveDocument();
        RaiseActiveDocumentPropertiesChanged();
        SyncExtractedPanels();
        return true;
    }

    public void RestoreWorkspaceDocuments(SavedWorkspaceDocumentsCanvas workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (workspace.Documents.Count == 0)
            return;

        var restoredDocuments = new List<OpenWorkspaceDocument>(workspace.Documents.Count);
        var restoredTypes = new HashSet<WorkspaceDocumentType>();
        foreach (SavedWorkspaceDocument savedDocument in workspace.Documents)
        {
            if (!Enum.TryParse(savedDocument.DocumentType, true, out WorkspaceDocumentType documentType))
                continue;
            if (!restoredTypes.Add(documentType))
                continue;

            object documentViewModel = documentType switch
            {
                WorkspaceDocumentType.QueryCanvas => BuildCanvasDocument(savedDocument, isDdl: false),
                WorkspaceDocumentType.DdlCanvas => BuildCanvasDocument(savedDocument, isDdl: true),
                WorkspaceDocumentType.SqlEditor => BuildSqlEditorDocument(),
                _ => BuildCanvasDocument(savedDocument, isDdl: false),
            };

            WorkspaceDocumentDescriptor descriptor = new(
                DocumentId: savedDocument.DocumentId == Guid.Empty ? Guid.NewGuid() : savedDocument.DocumentId,
                DocumentType: documentType,
                Title: string.IsNullOrWhiteSpace(savedDocument.Title) ? documentType.ToString() : savedDocument.Title,
                IsDirty: savedDocument.IsDirty,
                PersistenceSchemaVersion: string.IsNullOrWhiteSpace(savedDocument.PersistenceSchemaVersion)
                    ? "1.0"
                    : savedDocument.PersistenceSchemaVersion,
                Payload: JsonSerializer.SerializeToElement(new { }));

            restoredDocuments.Add(new OpenWorkspaceDocument(
                Descriptor: descriptor,
                DocumentViewModel: documentViewModel,
                PageViewModel: documentViewModel,
                PageState: null));
        }

        if (restoredDocuments.Count == 0)
            return;

        _workspaceRouter.ReplaceDocuments(restoredDocuments, workspace.ActiveDocumentId);

        _queryDocumentId = _workspaceRouter.OpenDocuments
            .FirstOrDefault(document => document.Descriptor.DocumentType == WorkspaceDocumentType.QueryCanvas)
            ?.Descriptor.DocumentId;
        _ddlDocumentId = _workspaceRouter.OpenDocuments
            .FirstOrDefault(document => document.Descriptor.DocumentType == WorkspaceDocumentType.DdlCanvas)
            ?.Descriptor.DocumentId;

        Canvas = _workspaceRouter.OpenDocuments
            .FirstOrDefault(document => document.Descriptor.DocumentType == WorkspaceDocumentType.QueryCanvas)
            ?.DocumentViewModel as CanvasViewModel;
        DdlCanvas = _workspaceRouter.OpenDocuments
            .FirstOrDefault(document => document.Descriptor.DocumentType == WorkspaceDocumentType.DdlCanvas)
            ?.DocumentViewModel as CanvasViewModel;

        SynchronizeModeFromActiveDocument();
        RaiseActiveDocumentPropertiesChanged();
        SyncExtractedPanels();
    }

    public void SetViewSubcanvasActive(bool isActive)
    {
        bool coerced = IsDdlDocumentPageActive && isActive;

        if (!Set(ref _isViewSubcanvasActive, coerced))
            return;

        UpdateActiveCanvasContext();
        SyncExtractedPanels();
    }

    public void EnterCanvas()
    {
        EnsureCanvas();
        ActivateDocumentForMode(ActiveMode);
        IsStartVisible = false;
    }

    public void ReturnToStart() => IsStartVisible = true;

    public void OpenSettings() => IsSettingsVisible = true;

    public void CloseSettings() => IsSettingsVisible = false;

    public void SelectSettingsSection(ESettingsSection section) => SelectedSettingsSection = section;

    public void SetCommandPalette(CommandPaletteViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        CommandPalette = viewModel;
        RaisePropertyChanged(nameof(CommandPalette));
    }

    private static string ResolveAppVersionLabel()
    {
        Assembly asm = typeof(ShellViewModel).Assembly;

        string? informational = asm
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            string clean = informational.Split('+')[0].Trim();
            if (!string.IsNullOrWhiteSpace(clean))
                return clean;
        }

        string? fileVersion = asm
            .GetCustomAttribute<AssemblyFileVersionAttribute>()
            ?.Version;
        if (!string.IsNullOrWhiteSpace(fileVersion))
            return fileVersion;

        return asm.GetName().Version?.ToString() ?? "dev";
    }

    private void AttachCanvasObservers(CanvasViewModel? _)
    {
        RefreshConnectionManagerObservers();
    }

    private void RefreshConnectionManagerObservers()
    {
        _activeConnectionManagerPropertyChanged ??= (_, e) =>
        {
            if (e.PropertyName == nameof(ConnectionManagerViewModel.IsVisible))
            {
                RaisePropertyChanged(nameof(IsConnectionManagerVisible));
                RaisePropertyChanged(nameof(IsConnectionManagerOverlayVisible));
            }

            SqlEditor.NotifyConnectionContextChanged();
        };

        ConnectionManagerViewModel? queryManager = Canvas?.ConnectionManager;
        if (!ReferenceEquals(_observedQueryConnectionManager, queryManager))
        {
            if (_observedQueryConnectionManager is not null)
                _observedQueryConnectionManager.PropertyChanged -= _activeConnectionManagerPropertyChanged;

            _observedQueryConnectionManager = queryManager;
            if (_observedQueryConnectionManager is not null)
                _observedQueryConnectionManager.PropertyChanged += _activeConnectionManagerPropertyChanged;
        }

        ConnectionManagerViewModel? ddlManager = DdlCanvas?.ConnectionManager;
        if (!ReferenceEquals(_observedDdlConnectionManager, ddlManager))
        {
            if (_observedDdlConnectionManager is not null)
                _observedDdlConnectionManager.PropertyChanged -= _activeConnectionManagerPropertyChanged;

            _observedDdlConnectionManager = ddlManager;
            if (_observedDdlConnectionManager is not null)
                _observedDdlConnectionManager.PropertyChanged += _activeConnectionManagerPropertyChanged;
        }
    }

    private void SyncExtractedPanels()
    {
        if (Canvas is not null)
            Canvas.Sidebar.ConnectionManagerOverride = null;

        if (DdlCanvas is not null)
        {
            DdlCanvas.Sidebar.ConnectionManagerOverride = Canvas?.ConnectionManager;
        }

        if (ActivePageContract.ShowsDiagramSidebar)
        {
            LeftSidebar.BindQuerySidebar(ActiveDiagramSidebar);
            RightSidebar.BindPropertyPanel(ActiveDiagramPropertyPanel);
            LeftSidebar.SyncVisibility(ActiveDiagramSidebar is not null);
            RightSidebar.SyncVisibility(ActiveDiagramPropertyPanel is not null);
            return;
        }

        LeftSidebar.BindQuerySidebar(null);
        RightSidebar.BindPropertyPanel(null);
        LeftSidebar.SyncVisibility(false);
        RightSidebar.SyncVisibility(false);
    }

    private void UpdateActiveCanvasContext()
    {
        ActiveCanvasContext = ActiveWorkspaceDocumentType switch
        {
            WorkspaceDocumentType.DdlCanvas when _isViewSubcanvasActive => CanvasContext.ViewSubcanvas,
            WorkspaceDocumentType.DdlCanvas => CanvasContext.Ddl,
            _ => CanvasContext.Query,
        };
    }

    private void ActivateDocumentForMode(AppMode mode)
    {
        if (mode == AppMode.Ddl)
            EnsureDdlCanvas();

        bool changed = mode switch
        {
            AppMode.Query => _queryDocumentId.HasValue && _workspaceRouter.TryActivate(_queryDocumentId.Value),
            AppMode.Ddl => _ddlDocumentId.HasValue && _workspaceRouter.TryActivate(_ddlDocumentId.Value),
            AppMode.SqlEditor => TryActivateLastDocumentByType(WorkspaceDocumentType.SqlEditor),
            _ => false,
        };

        if (!changed)
            return;

        SynchronizeModeFromActiveDocument();
        RaiseActiveDocumentPropertiesChanged();
        SyncExtractedPanels();
    }

    private bool TryActivateLastDocumentByType(WorkspaceDocumentType documentType)
    {
        OpenWorkspaceDocument? target = _workspaceRouter.OpenDocuments
            .LastOrDefault(document => document.Descriptor.DocumentType == documentType);
        return target is not null && _workspaceRouter.TryActivate(target.Descriptor.DocumentId);
    }

    private void SynchronizeModeFromActiveDocument()
    {
        AppMode targetMode = ActiveWorkspaceDocumentType switch
        {
            WorkspaceDocumentType.DdlCanvas => AppMode.Ddl,
            WorkspaceDocumentType.SqlEditor => AppMode.SqlEditor,
            _ => AppMode.Query,
        };

        if (targetMode != AppMode.Ddl)
            _isViewSubcanvasActive = false;

        ActiveMode = targetMode;
        RaisePropertyChanged(nameof(ActiveCanvas));
        RaisePropertyChanged(nameof(ActiveDiagramSidebar));
        RaisePropertyChanged(nameof(ActiveDiagramPropertyPanel));
        if (targetMode == AppMode.SqlEditor)
            HideDiagramOnlyOverlays();
        UpdateActiveCanvasContext();
    }

    private void RaiseActiveDocumentPropertiesChanged()
    {
        RaisePropertyChanged(nameof(OpenWorkspaceDocuments));
        RaisePropertyChanged(nameof(ActiveWorkspaceDocumentId));
        RaisePropertyChanged(nameof(ActiveWorkspaceDocument));
        RaisePropertyChanged(nameof(ActiveWorkspaceDocumentType));
        RaisePropertyChanged(nameof(ActivePageContract));
        RaisePropertyChanged(nameof(ActivePreviewContract));
        RaisePropertyChanged(nameof(ActiveDiagnosticsContract));
        RaisePropertyChanged(nameof(ActiveQueryCanvasDocument));
        RaisePropertyChanged(nameof(ActiveDdlCanvasDocument));
        RaisePropertyChanged(nameof(ActiveSqlEditorDocument));
        RaisePropertyChanged(nameof(IsQueryDocumentPageActive));
        RaisePropertyChanged(nameof(IsDdlDocumentPageActive));
        RaisePropertyChanged(nameof(IsSqlEditorDocumentPageActive));
        RaisePropertyChanged(nameof(IsDiagramDocumentPageActive));
        RaisePropertyChanged(nameof(IsDiagramOverlayLayerVisible));
        RaisePropertyChanged(nameof(ActiveConnectionManager));
        RaisePropertyChanged(nameof(IsConnectionManagerVisible));
        RaisePropertyChanged(nameof(IsConnectionManagerOverlayVisible));
        RaisePropertyChanged(nameof(IsOutputPreviewModalVisible));
        RaisePropertyChanged(nameof(ActiveCanvas));
        RaisePropertyChanged(nameof(ActiveDiagramSidebar));
        RaisePropertyChanged(nameof(ActiveDiagramPropertyPanel));
    }

    private void HideDiagramOnlyOverlays()
    {
        OutputPreview.IsVisible = false;

        if (Canvas is not null)
        {
            Canvas.ManualJoinDialog.Close();
            Canvas.ConnectionManager.IsVisible = false;
            Canvas.ConnectionManager.CloseClearCanvasPromptCommand.Execute(null);
        }

        if (DdlCanvas is not null)
        {
            DdlCanvas.ManualJoinDialog.Close();
            DdlCanvas.ConnectionManager.IsVisible = false;
            DdlCanvas.ConnectionManager.CloseClearCanvasPromptCommand.Execute(null);
        }

        RaisePropertyChanged(nameof(IsConnectionManagerOverlayVisible));
        RaisePropertyChanged(nameof(IsOutputPreviewModalVisible));
    }

    private void RegisterOrUpdateQueryDocument(CanvasViewModel queryCanvas)
    {
        _queryDocumentId ??= Guid.NewGuid();
        bool shouldActivate = _workspaceRouter.ActiveDocument is null;
        RegisterOrUpdateDocument(
            _queryDocumentId.Value,
            WorkspaceDocumentType.QueryCanvas,
            title: "Query Canvas",
            documentViewModel: queryCanvas,
            activate: shouldActivate);

        if (shouldActivate)
        {
            SynchronizeModeFromActiveDocument();
            SyncExtractedPanels();
        }
    }

    private void RegisterOrUpdateDdlDocument(CanvasViewModel ddlCanvas)
    {
        _ddlDocumentId ??= Guid.NewGuid();
        RegisterOrUpdateDocument(
            _ddlDocumentId.Value,
            WorkspaceDocumentType.DdlCanvas,
            title: "DDL Canvas",
            documentViewModel: ddlCanvas);
    }

    private Guid OpenNewQueryDocument()
    {
        int nextOrdinal = _workspaceRouter.OpenDocuments.Count(document =>
            document.Descriptor.DocumentType == WorkspaceDocumentType.QueryCanvas) + 1;
        string title = nextOrdinal == 1 ? "Query Canvas" : $"Query Canvas {nextOrdinal}";
        var canvas = new CanvasViewModel(
            nodeManager: null,
            pinManager: null,
            selectionManager: null,
            localizationService: null,
            domainStrategy: new QueryDomainStrategy(),
            toastCenter: Toasts,
            connectionManagerFactory: _connectionManagerViewModelFactory);
        Guid documentId = Guid.NewGuid();
        RegisterOrUpdateDocument(documentId, WorkspaceDocumentType.QueryCanvas, title, canvas, activate: true);
        _canvas ??= canvas;
        SynchronizeModeFromActiveDocument();
        RaiseActiveDocumentPropertiesChanged();
        SyncExtractedPanels();
        return documentId;
    }

    private Guid OpenNewDdlDocument()
    {
        int nextOrdinal = _workspaceRouter.OpenDocuments.Count(document =>
            document.Descriptor.DocumentType == WorkspaceDocumentType.DdlCanvas) + 1;
        string title = nextOrdinal == 1 ? "DDL Canvas" : $"DDL Canvas {nextOrdinal}";
        var ddlCanvas = new CanvasViewModel(
            nodeManager: null,
            pinManager: null,
            selectionManager: null,
            localizationService: null,
            domainStrategy: new DdlDomainStrategy(),
            toastCenter: Toasts,
            connectionManagerFactory: _connectionManagerViewModelFactory);
        Guid documentId = Guid.NewGuid();
        RegisterOrUpdateDocument(documentId, WorkspaceDocumentType.DdlCanvas, title, ddlCanvas, activate: true);
        _ddlCanvas ??= ddlCanvas;
        SynchronizeModeFromActiveDocument();
        RaiseActiveDocumentPropertiesChanged();
        SyncExtractedPanels();
        return documentId;
    }

    private Guid OpenNewSqlEditorDocument()
    {
        int nextOrdinal = _workspaceRouter.OpenDocuments.Count(document =>
            document.Descriptor.DocumentType == WorkspaceDocumentType.SqlEditor) + 1;
        string title = nextOrdinal == 1 ? "SQL Editor" : $"SQL Editor {nextOrdinal}";
        SqlEditorViewModel sqlEditorDocument = _sqlEditorViewModelFactory.Create(new SqlEditorViewModelFactoryContext
        {
            ConnectionConfigResolver = ResolveSharedActiveConnectionConfig,
            ConnectionConfigByProfileIdResolver = ResolveSqlEditorConnectionByProfileId,
            ConnectionProfilesResolver = ResolveSqlEditorConnectionProfiles,
            MetadataResolver = ResolveSharedMetadata,
            SharedConnectionManagerResolver = ResolveSharedConnectionManager,
        });
        Guid documentId = Guid.NewGuid();
        RegisterOrUpdateDocument(documentId, WorkspaceDocumentType.SqlEditor, title, sqlEditorDocument, activate: true);
        SynchronizeModeFromActiveDocument();
        RaiseActiveDocumentPropertiesChanged();
        SyncExtractedPanels();
        return documentId;
    }

    private void RegisterOrUpdateDocument(
        Guid documentId,
        WorkspaceDocumentType documentType,
        string title,
        object documentViewModel,
        bool activate = false)
    {
        WorkspaceDocumentDescriptor descriptor = new(
            DocumentId: documentId,
            DocumentType: documentType,
            Title: title,
            IsDirty: false,
            PersistenceSchemaVersion: "1.0",
            Payload: JsonSerializer.SerializeToElement(new { }));

        _workspaceRouter.OpenDocument(new OpenWorkspaceDocument(
            Descriptor: descriptor,
            DocumentViewModel: documentViewModel,
            PageViewModel: documentViewModel,
            PageState: null), activate);

        RaiseActiveDocumentPropertiesChanged();
    }

    private CanvasViewModel BuildCanvasDocument(SavedWorkspaceDocument savedDocument, bool isDdl)
    {
        var canvas = new CanvasViewModel(
            nodeManager: null,
            pinManager: null,
            selectionManager: null,
            localizationService: null,
            domainStrategy: isDdl ? new DdlDomainStrategy() : new QueryDomainStrategy(),
            toastCenter: Toasts,
            connectionManagerFactory: _connectionManagerViewModelFactory);

        if (savedDocument.CanvasPayload is SavedCanvas payload)
        {
            string payloadJson = JsonSerializer.Serialize(payload);
            CanvasSerializer.Deserialize(payloadJson, canvas);
        }

        return canvas;
    }

    private SqlEditorViewModel BuildSqlEditorDocument()
    {
        return _sqlEditorViewModelFactory.Create(new SqlEditorViewModelFactoryContext
        {
            ConnectionConfigResolver = ResolveSharedActiveConnectionConfig,
            ConnectionConfigByProfileIdResolver = ResolveSqlEditorConnectionByProfileId,
            ConnectionProfilesResolver = ResolveSqlEditorConnectionProfiles,
            MetadataResolver = ResolveSharedMetadata,
            SharedConnectionManagerResolver = ResolveSharedConnectionManager,
        });
    }

    private string Localize(string key, string fallback)
    {
        string value = _localization[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private ConnectionConfig? ResolveSqlEditorConnectionByProfileId(string? profileId)
    {
        (CanvasViewModel? connectionCanvas, ConnectionManagerViewModel? connectionManager) = ResolveSharedConnectionContext();
        if (connectionCanvas is null || connectionManager is null)
            return null;

        if (string.IsNullOrWhiteSpace(profileId))
            return connectionCanvas.ActiveConnectionConfig;

        ConnectionProfile? selected = connectionManager.Profiles
            .FirstOrDefault(profile => string.Equals(profile.Id, profileId, StringComparison.Ordinal));

        return selected?.ToConnectionConfig() ?? connectionCanvas.ActiveConnectionConfig;
    }

    private IReadOnlyList<SqlEditorConnectionProfileOption> ResolveSqlEditorConnectionProfiles()
    {
        ConnectionManagerViewModel? manager = ResolveSharedConnectionManager();
        if (manager is null)
            return [];

        return manager.Profiles
            .Select(profile => new SqlEditorConnectionProfileOption
            {
                Id = profile.Id,
                DisplayName = profile.Name,
                Provider = profile.Provider,
            })
            .ToList();
    }

    private ConnectionManagerViewModel? ResolveSharedConnectionManager() =>
        Canvas?.ConnectionManager ?? DdlCanvas?.ConnectionManager;

    private ConnectionConfig? ResolveSharedActiveConnectionConfig() =>
        Canvas?.ActiveConnectionConfig ?? DdlCanvas?.ActiveConnectionConfig;

    private DbMetadata? ResolveSharedMetadata() =>
        Canvas?.DatabaseMetadata ?? DdlCanvas?.DatabaseMetadata;

    private (CanvasViewModel? Canvas, ConnectionManagerViewModel? Manager) ResolveSharedConnectionContext()
    {
        if (Canvas is not null)
            return (Canvas, Canvas.ConnectionManager);

        if (DdlCanvas is not null)
            return (DdlCanvas, DdlCanvas.ConnectionManager);

        return (null, null);
    }
}
