using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.Services;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI;

public partial class MainWindow : Window
{
    private CanvasViewModel Vm => (CanvasViewModel)DataContext!;

    // Services
    private MainWindowLayoutService? _layoutService;
    private SessionManagementService? _sessionService;
    private KeyboardInputHandler? _keyboardHandler;
    private FileOperationsService? _fileOps;
    private ExportService? _export;
    private PreviewService? _preview;
    private CommandPaletteFactory? _commandFactory;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new CanvasViewModel();

        InitializeServices();
        WireWindowChrome();
        WireMenuButtons();
        WireSearchMenu();

        PreviewPanel.LiveSqlViewModel = Vm.LiveSql;

        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CanvasViewModel.WindowTitle))
                Title = Vm.WindowTitle;
        };
        Title = Vm.WindowTitle;
    }

    private void InitializeServices()
    {
        _layoutService = new MainWindowLayoutService(this, Vm);
        _sessionService = new SessionManagementService(this, Vm);
        _keyboardHandler = new KeyboardInputHandler(this, Vm);
        _fileOps = new FileOperationsService(this, Vm);
        _export = new ExportService(this, Vm);
        _preview = new PreviewService(this, Vm);
        _commandFactory = new CommandPaletteFactory(this, Vm, _fileOps, _export, _preview);

        _layoutService.Wire();
        _sessionService.Wire();
        _sessionService.CheckForSession();
        _keyboardHandler.Wire();
        _preview.Wire();
        _commandFactory.RegisterAllCommands();

        // Wire schema tree to update when database metadata changes
        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CanvasViewModel.DatabaseMetadata))
                UpdateSchemaTree();
        };
    }

    private void UpdateSchemaTree()
    {
        var schemaTree = this.FindControl<TreeView>("SchemaTree");
        if (schemaTree is null || Vm.DatabaseMetadata is null)
            return;

        schemaTree.Items.Clear();

        foreach (var schema in Vm.DatabaseMetadata.Schemas)
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
            () =>
            {
                DataContext = new CanvasViewModel();
                Title = Vm.WindowTitle;
            }
        );
        B(
            "NewTabBtn",
            () =>
            {
                DataContext = new CanvasViewModel();
                Title = Vm.WindowTitle;
            }
        );
        B("OpenSearchBtn", OpenSearch);
        B("ConnectionBadgeBtn", () => Vm.ConnectionManager.Open());
        B("SaveBtn", () => _ = _fileOps?.SaveAsync());
        B("OpenBtn", () => _ = _fileOps?.OpenAsync());
        B("ZoomInBtn", () => Vm.ZoomInCommand.Execute(null));
        B("ZoomOutBtn", () => Vm.ZoomOutCommand.Execute(null));
        B("FitBtn", () => Vm.FitToScreenCommand.Execute(null));
        B("TogglePreviewBtn", () => Vm.TogglePreviewCommand.Execute(null));
    }

    private void WireSearchMenu()
    {
        SearchMenuControl? overlay = this.FindControl<SearchMenuControl>("SearchOverlay");
        if (overlay is null)
            return;
        overlay.SpawnRequested += (_, def) =>
        {
            Vm.SpawnNode(def, Vm.SearchMenu.SpawnPosition);
            this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
        };
        overlay.SpawnTableRequested += (_, args) =>
        {
            Vm.SpawnTableNode(
                args.FullName,
                args.Cols.Select(c => (c.Name, c.Type)),
                Vm.SearchMenu.SpawnPosition
            );
            this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
            // Trigger join analysis after the node is added
            Vm.TriggerAutoJoinAnalysis(args.FullName);
        };
        overlay.SnippetRequested += (_, snippet) =>
        {
            Vm.InsertSnippet(snippet, Vm.SearchMenu.SpawnPosition);
            this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
        };
    }

    private void OpenSearch()
    {
        InfiniteCanvas? canvas = this.FindControl<InfiniteCanvas>("TheCanvas");
        Point ctr = canvas is not null
            ? new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2)
            : new Point(400, 300);
        Vm.SearchMenu.Open(ctr);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _keyboardHandler?.OnKeyDown(this, e);
    }
}
