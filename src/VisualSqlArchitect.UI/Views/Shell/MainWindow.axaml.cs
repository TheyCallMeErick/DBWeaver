using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System.ComponentModel;
using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.Services;
using VisualSqlArchitect.UI.Services.Localization;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI;

public partial class MainWindow : Window
{
    private sealed class QueryTabState
    {
        public required string FallbackTitle { get; init; }
        public string? SnapshotJson { get; set; }
        public string? CurrentFilePath { get; set; }
        public bool IsDirty { get; set; }
    }

    private CanvasViewModel CurrentVm => DataContext as CanvasViewModel
        ?? throw new InvalidOperationException("MainWindow DataContext must be a CanvasViewModel.");

    private readonly List<QueryTabState> _queryTabs = [];
    private int _activeQueryTabIndex;
    private bool _isRestoringTab;

    // Services
    private MainWindowLayoutService? _layoutService;
    private SessionManagementService? _sessionService;
    private KeyboardInputHandler? _keyboardHandler;
    private FileOperationsService? _fileOps;
    private ExportService? _export;
    private PreviewService? _preview;
    private CommandPaletteFactory? _commandFactory;
    private PropertyChangedEventHandler? _windowTitleChangedHandler;
    private PropertyChangedEventHandler? _databaseMetadataChangedHandler;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new CanvasViewModel();

        InitializeServices(CurrentVm);
        AttachCanvasHandlers(CurrentVm);
        WireWindowChrome();
        WireMenuButtons();
        WireSearchMenu();
        InitializeQueryTabs();

        PreviewPanel.LiveSqlViewModel = CurrentVm.LiveSql;
        Title = CurrentVm.WindowTitle;
    }

    private void AttachCanvasHandlers(CanvasViewModel vm)
    {
        _windowTitleChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(CanvasViewModel.WindowTitle))
                Title = vm.WindowTitle;

            if (
                e.PropertyName is nameof(CanvasViewModel.IsDirty)
                    or nameof(CanvasViewModel.CurrentFilePath)
            )
            {
                SyncActiveTabMetadataFromCanvas();
                RefreshQueryTabsUi();
            }
        };

        _databaseMetadataChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(CanvasViewModel.DatabaseMetadata))
                UpdateSchemaTree();
        };

        vm.PropertyChanged += _windowTitleChangedHandler;
        vm.PropertyChanged += _databaseMetadataChangedHandler;
    }

    private void DetachCanvasHandlers(CanvasViewModel vm)
    {
        if (_windowTitleChangedHandler is not null)
            vm.PropertyChanged -= _windowTitleChangedHandler;
        if (_databaseMetadataChangedHandler is not null)
            vm.PropertyChanged -= _databaseMetadataChangedHandler;

        _windowTitleChangedHandler = null;
        _databaseMetadataChangedHandler = null;
    }

    private void InitializeServices(CanvasViewModel vm)
    {
        _layoutService = new MainWindowLayoutService(this, vm);
        _sessionService = new SessionManagementService(this, vm);
        _fileOps = new FileOperationsService(this, vm);
        _keyboardHandler = new KeyboardInputHandler(this, vm, _fileOps, CreateNewQueryTab);
        _export = new ExportService(this, vm);
        _preview = new PreviewService(this, vm);
        _commandFactory = new CommandPaletteFactory(
            this,
            vm,
            _fileOps,
            _export,
            _preview,
            CreateNewQueryTab
        );

        _layoutService.Wire();
        _sessionService.Wire();
        _sessionService.CheckForSession();
        _keyboardHandler.Wire();
        _preview.Wire();
        _commandFactory.RegisterAllCommands();
    }

    private void UpdateSchemaTree()
    {
        var schemaTree = this.FindControl<TreeView>("SchemaTree");
        if (schemaTree is null || CurrentVm.DatabaseMetadata is null)
            return;

        schemaTree.Items.Clear();

        foreach (var schema in CurrentVm.DatabaseMetadata.Schemas)
        {
            // Create schema node
            var schemaItem = new TreeViewItem
            {
                Header = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 5,
                    Children =
                    {
                        new Material.Icons.Avalonia.MaterialIcon { Kind = Material.Icons.MaterialIconKind.Database, Width = 12, Height = 12, Foreground = new SolidColorBrush(Color.Parse("#4A5568")) },
                        new TextBlock { Text = schema.Name, FontWeight = FontWeight.Medium, Foreground = new SolidColorBrush(Color.Parse("#8B95A8")), FontSize = 11 }
                    }
                },
                IsExpanded = true
            };

            // Add tables to schema
            foreach (var table in schema.Tables.OrderBy(t => t.Name))
            {
                var tableItem = new TreeViewItem
                {
                    Header = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 4,
                        Children =
                        {
                            new Material.Icons.Avalonia.MaterialIcon { Kind = Material.Icons.MaterialIconKind.Table, Width = 12, Height = 12, Foreground = new SolidColorBrush(Color.Parse("#14B8A6")) },
                            new TextBlock { Text = table.Name, FontSize = 11 }
                        }
                    }
                };

                // Add columns to table
                foreach (var column in table.Columns.OrderBy(c => c.OrdinalPosition))
                {
                    var columnItem = new TreeViewItem
                    {
                        Header = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 3,
                            Children =
                            {
                                new Material.Icons.Avalonia.MaterialIcon
                                {
                                    Kind = column.IsPrimaryKey ? Material.Icons.MaterialIconKind.Key : Material.Icons.MaterialIconKind.CircleSmall,
                                    Width = 10,
                                    Height = 10,
                                    Foreground = new SolidColorBrush(Color.Parse(column.IsPrimaryKey ? "#FBBF24" : "#4A5568"))
                                },
                                new TextBlock { Text = column.Name, FontFamily = new FontFamily("Consolas,monospace"), FontSize = 10 },
                                new TextBlock { Text = column.NativeType, Foreground = new SolidColorBrush(Color.Parse("#4ADE80")), FontSize = 9 }
                            }
                        }
                    };

                    tableItem.Items.Add(columnItem);
                }

                schemaItem.Items.Add(tableItem);
            }

            schemaTree.Items.Add(schemaItem);
        }
    }

    private static string CreateFreshCanvasSnapshot()
    {
        using var vm = new CanvasViewModel();
        return CanvasSerializer.Serialize(vm);
    }

    private string GetTabTitle(QueryTabState tab)
    {
        if (!string.IsNullOrWhiteSpace(tab.CurrentFilePath))
            return System.IO.Path.GetFileNameWithoutExtension(tab.CurrentFilePath);

        return tab.FallbackTitle;
    }

    private void CaptureActiveTabState()
    {
        if (_isRestoringTab || _activeQueryTabIndex < 0 || _activeQueryTabIndex >= _queryTabs.Count)
            return;

        QueryTabState activeTab = _queryTabs[_activeQueryTabIndex];
        activeTab.SnapshotJson = CanvasSerializer.Serialize(CurrentVm);
        activeTab.CurrentFilePath = CurrentVm.CurrentFilePath;
        activeTab.IsDirty = CurrentVm.IsDirty;
    }

    private void SyncActiveTabMetadataFromCanvas()
    {
        if (_isRestoringTab || _activeQueryTabIndex < 0 || _activeQueryTabIndex >= _queryTabs.Count)
            return;

        QueryTabState activeTab = _queryTabs[_activeQueryTabIndex];
        activeTab.CurrentFilePath = CurrentVm.CurrentFilePath;
        activeTab.IsDirty = CurrentVm.IsDirty;
    }

    private void RestoreTabState(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _queryTabs.Count)
            return;

        QueryTabState tab = _queryTabs[tabIndex];
        string snapshot = tab.SnapshotJson ?? CreateFreshCanvasSnapshot();
        CanvasLoadResult result = CanvasSerializer.Deserialize(snapshot, CurrentVm);
        if (!result.Success)
        {
            CurrentVm.DataPreview.ShowError($"Failed to switch tab: {result.Error}", null);
            return;
        }

        CurrentVm.CurrentFilePath = tab.CurrentFilePath;
        CurrentVm.IsDirty = tab.IsDirty;

        this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
        Title = CurrentVm.WindowTitle;
    }

    private void ActivateQueryTab(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _queryTabs.Count || tabIndex == _activeQueryTabIndex)
            return;

        CaptureActiveTabState();
        _activeQueryTabIndex = tabIndex;

        _isRestoringTab = true;
        try
        {
            RestoreTabState(tabIndex);
        }
        finally
        {
            _isRestoringTab = false;
        }

        RefreshQueryTabsUi();
    }

    private void RefreshQueryTabsUi()
    {
        StackPanel? host = this.FindControl<StackPanel>("QueryTabsHost");
        if (host is null)
            return;

        host.Children.Clear();

        for (int i = 0; i < _queryTabs.Count; i++)
        {
            int tabIndex = i;
            QueryTabState tab = _queryTabs[i];
            bool isActive = i == _activeQueryTabIndex;

            var title = new TextBlock
            {
                Text = GetTabTitle(tab),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse(isActive ? "#E8EAED" : "#8B95A8")),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var dot = new Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = new SolidColorBrush(Color.Parse(isActive ? "#3B82F6" : "#4A5568")),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                VerticalAlignment = VerticalAlignment.Center,
            };

            row.Children.Add(dot);
            row.Children.Add(title);

            if (tab.IsDirty)
            {
                row.Children.Add(
                    new TextBlock
                    {
                        Text = "•",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.Parse("#FBBF24")),
                        VerticalAlignment = VerticalAlignment.Center,
                    }
                );
            }

            var button = new Button
            {
                Classes = { "tb" },
                Padding = new Thickness(12, 5),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Content = row,
            };
            button.Click += (_, _) => ActivateQueryTab(tabIndex);

            var container = new Border
            {
                Background = new SolidColorBrush(Color.Parse(isActive ? "#171B26" : "#11151F")),
                BorderBrush = new SolidColorBrush(Color.Parse(isActive ? "#252C3F" : "#1E2335")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = button,
            };

            host.Children.Add(container);
        }
    }

    private void InitializeQueryTabs()
    {
        _queryTabs.Clear();
        _activeQueryTabIndex = 0;
        _queryTabs.Add(
            new QueryTabState
            {
                FallbackTitle = "Consulta 1",
                SnapshotJson = CanvasSerializer.Serialize(CurrentVm),
                CurrentFilePath = CurrentVm.CurrentFilePath,
                IsDirty = CurrentVm.IsDirty,
            }
        );

        RefreshQueryTabsUi();
    }

    private void ResetCurrentCanvas()
    {
        if (_activeQueryTabIndex < 0 || _activeQueryTabIndex >= _queryTabs.Count)
            return;

        QueryTabState activeTab = _queryTabs[_activeQueryTabIndex];
        activeTab.SnapshotJson = CreateFreshCanvasSnapshot();
        activeTab.CurrentFilePath = null;
        activeTab.IsDirty = false;

        _isRestoringTab = true;
        try
        {
            RestoreTabState(_activeQueryTabIndex);
        }
        finally
        {
            _isRestoringTab = false;
        }

        RefreshQueryTabsUi();
    }

    private void CreateNewQueryTab()
    {
        CaptureActiveTabState();

        int tabNumber = _queryTabs.Count + 1;
        _queryTabs.Add(
            new QueryTabState
            {
                FallbackTitle = $"Consulta {tabNumber}",
                SnapshotJson = CreateFreshCanvasSnapshot(),
                CurrentFilePath = null,
                IsDirty = false,
            }
        );

        _activeQueryTabIndex = _queryTabs.Count - 1;

        _isRestoringTab = true;
        try
        {
            RestoreTabState(_activeQueryTabIndex);
        }
        finally
        {
            _isRestoringTab = false;
        }

        RefreshQueryTabsUi();
    }

    private void WireWindowChrome()
    {
        Button? close = this.FindControl<Button>("CloseWindowBtn");
        Button? min = this.FindControl<Button>("MinimizeBtn");
        Button? max = this.FindControl<Button>("MaximizeBtn");
        if (close is not null)
            close.Click += (_, _) => Close();
        if (min is not null)
            min.Click += (_, _) => WindowState = WindowState.Minimized;
        if (max is not null)
            max.Click += (_, _) =>
                WindowState =
                    WindowState == WindowState.Maximized
                        ? WindowState.Normal
                        : WindowState.Maximized;
    }

    private void TitleBar_PointerPressed(object? s, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void ToastDetailsBackdrop_PointerPressed(object? s, PointerPressedEventArgs e)
    {
        CurrentVm.Toasts.CloseDetailsCommand.Execute(null);
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

    private void WireMenuButtons()
    {
        void B(string name, Action a)
        {
            Button? btn = this.FindControl<Button>(name);
            if (btn is not null)
                btn.Click += (_, _) => a();
        }
        B(
            "NewBtn",
            () => ResetCurrentCanvas()
        );
        B(
            "NewTabBtn",
            () => CreateNewQueryTab()
        );
        B("OpenSearchBtn", OpenSearch);
        B("ConnectionBadgeBtn", () => CurrentVm.ConnectionManager.Open());
        B("SaveBtn", () => _ = _fileOps?.SaveAsync());
        B("FileHistoryBtn", () => CurrentVm.FileHistory.Open());
        B("OpenBtn", () => _ = _fileOps?.OpenAsync());
        B("ShortcutsBtn", () => new KeyboardShortcutsWindow().Show(this));
        B("LanguageToggleBtn", () => LocalizationService.Instance.ToggleCulture());
        B("ZoomInBtn", () => CurrentVm.ZoomInCommand.Execute(null));
        B("ZoomOutBtn", () => CurrentVm.ZoomOutCommand.Execute(null));
        B("FitBtn", () => CurrentVm.FitToScreenCommand.Execute(null));
        B("TogglePreviewBtn", () => CurrentVm.TogglePreviewCommand.Execute(null));
    }

    private void WireSearchMenu()
    {
        SearchMenuControl? overlay = this.FindControl<SearchMenuControl>("SearchOverlay");
        if (overlay is null)
            return;
        overlay.SpawnRequested += (_, def) =>
        {
            CurrentVm.SpawnNode(def, CurrentVm.SearchMenu.SpawnPosition);
            this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
        };
        overlay.SpawnTableRequested += (_, args) =>
        {
            CurrentVm.SpawnTableNode(
                args.FullName,
                args.Cols.Select(c => (c.Name, c.Type)),
                CurrentVm.SearchMenu.SpawnPosition
            );
            this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
            // Trigger join analysis after the node is added
            CurrentVm.TriggerAutoJoinAnalysis(args.FullName);
        };
        overlay.SnippetRequested += (_, snippet) =>
        {
            CurrentVm.InsertSnippet(snippet, CurrentVm.SearchMenu.SpawnPosition);
            this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
        };
    }

    private void OpenSearch()
    {
        InfiniteCanvas? canvas = this.FindControl<InfiniteCanvas>("TheCanvas");
        Point ctr = canvas is not null
            ? new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2)
            : new Point(400, 300);
        CurrentVm.SearchMenu.Open(ctr);
    }

    private bool TryCloseTopModalOnEscape()
    {
        if (CurrentVm.ConnectionManager.IsClearCanvasPromptVisible)
        {
            CurrentVm.ConnectionManager.CloseClearCanvasPromptCommand.Execute(null);
            return true;
        }

        if (CurrentVm.Toasts.IsDetailsOpen)
        {
            CurrentVm.Toasts.CloseDetailsCommand.Execute(null);
            return true;
        }

        return false;
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
