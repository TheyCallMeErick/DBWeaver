using Avalonia;
using Avalonia.Controls;
using Material.Icons;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Services;

/// <summary>
/// Factory for creating command palette commands.
/// Centralizes 30+ command definitions with their shortcuts and metadata.
/// </summary>
public class CommandPaletteFactory(
    Window window,
    CanvasViewModel vm,
    FileOperationsService fileOps,
    ExportService export,
    PreviewService preview,
    Action? onCreateNewCanvas = null
)
{
    private readonly Window _window = window;
    private readonly CanvasViewModel _vm = vm;
    private readonly FileOperationsService _fileOps = fileOps;
    private readonly ExportService _export = export;
    private readonly PreviewService _preview = preview;
    private readonly Action? _onCreateNewCanvas = onCreateNewCanvas;

    public void RegisterAllCommands()
    {
        _vm.CommandPalette.RegisterCommands(CreateBasicCommands());
        _vm.CommandPalette.RegisterCommands(CreateTemplateCommands());
    }

    private List<PaletteCommandItem> CreateBasicCommands() =>
        [
            // ── Flow Version History ──────────────────────────────────────────
            new()
            {
                Name = "Flow Version History",
                Description = "Create checkpoints, compare versions side-by-side and restore a previous canvas state",
                Shortcut = "Ctrl+Shift+H",
                Icon = MaterialIconKind.ClipboardTextHistory,
                Tags = "version history checkpoint diff restore snapshot compare undo flow",
                Execute = () => _vm.FlowVersions.Open(),
            },
            new()
            {
                Name = "File Save/Load History",
                Description = "Open local file version history created on each save and restore previous saved snapshots",
                Shortcut = "Ctrl+Alt+H",
                Icon = MaterialIconKind.History,
                Tags = "file history save load backup versions restore local",
                Execute = () => _vm.FileHistory.Open(),
            },
            new()
            {
                Name = "Keyboard Shortcuts",
                Description = "Open shortcut reference screen",
                Shortcut = "F1",
                Icon = MaterialIconKind.Keyboard,
                Tags = "help shortcuts hotkeys keyboard reference",
                Execute = () => new KeyboardShortcutsWindow().Show(_window),
            },
            // ── Auto-Join ─────────────────────────────────────────────────────
            new()
            {
                Name = "Analyze All Joins",
                Description = "Scan all table-source nodes on the canvas for possible join relationships based on FK conventions and naming patterns",
                Shortcut = "",
                Icon = MaterialIconKind.AutoFix,
                Tags = "join autojoin analyze suggest detect foreign key relationships heuristic",
                Execute = () => _vm.AnalyzeAllCanvasJoins(),
            },
            // ── SQL Importer ──────────────────────────────────────────────────
            new()
            {
                Name = "Import SQL to Graph",
                Description = "Paste a SELECT statement and generate nodes automatically — FROM, JOIN, WHERE, LIMIT are supported",
                Shortcut = "",
                Icon = MaterialIconKind.CodeBrackets,
                Tags = "import sql paste convert graph reverse engineer query",
                Execute = () => _vm.SqlImporter.Open(),
            },
            // ── Snippets ──────────────────────────────────────────────────────
            new()
            {
                Name = "Save Selection as Snippet",
                Description = "Save the selected nodes as a reusable snippet — insert it later via the node search menu (⇧A)",
                Shortcut = "",
                Icon = MaterialIconKind.BookmarkPlus,
                Tags = "snippet save selection reuse template favorite bookmark",
                Execute = () =>
                {
                    var selected = _vm.Nodes.Where(n => n.IsSelected).ToList();
                    if (selected.Count == 0)
                        return;
                    // Auto-name: first node title + count
                    string baseName = selected[0].Title;
                    string name = selected.Count == 1
                        ? baseName
                        : $"{baseName} +{selected.Count - 1}";
                    _vm.SaveSelectionAsSnippet(name);
                },
            },
            new()
            {
                Name = "Edit Selected CTE",
                Description = "Open isolated sub-canvas editor for the selected CTE Definition node",
                Shortcut = "Ctrl+Alt+Enter",
                Icon = MaterialIconKind.FileTree,
                Tags = "cte with recursive editor subgraph subcanvas isolate",
                Execute = () => _vm.EnterCteEditorCommand.Execute(null),
            },
            new()
            {
                Name = "Exit CTE Editor",
                Description = "Apply CTE sub-canvas edits and return to the parent canvas",
                Shortcut = "Esc",
                Icon = MaterialIconKind.ExitToApp,
                Tags = "cte subcanvas exit apply back",
                Execute = () => _vm.ExitCteEditorCommand.Execute(null),
            },
            // ── Explain Plan ──────────────────────────────────────────────────
            new()
            {
                Name = "Explain Plan",
                Description = "Inspect the query execution plan — see scan types, join strategies, and cost estimates",
                Shortcut = "F4",
                Icon = MaterialIconKind.TableSearch,
                Tags = "explain plan execution cost scan index join performance",
                Execute = () => _vm.ExplainPlan.Open(),
            },
            // ── Benchmark ─────────────────────────────────────────────────────
            new()
            {
                Name = "Run Query Benchmark",
                Description = "Measure avg / median / p95 latency of the current SQL over N iterations",
                Shortcut = "",
                Icon = MaterialIconKind.TimerOutline,
                Tags = "benchmark performance latency timing profile measure speed",
                Execute = () => _vm.Benchmark.Open(),
            },
            // ── Connection ────────────────────────────────────────────────────
            new()
            {
                Name = "Manage Connections",
                Description = "Open the connection manager to add, edit or switch database connections",
                Shortcut = "Ctrl+Shift+C",
                Icon = MaterialIconKind.DatabaseSettings,
                Tags = "connection database server host provider switch",
                Execute = () => _vm.ConnectionManager.Open(),
            },
            // ── File ──────────────────────────────────────────────────────────
            new()
            {
                Name = "New Canvas",
                Description = "Clear canvas and start fresh",
                Shortcut = "Ctrl+N",
                Icon = MaterialIconKind.FileOutline,
                Tags = "reset clear blank",
                Execute = () =>
                {
                    if (_onCreateNewCanvas is not null)
                    {
                        _onCreateNewCanvas();
                        return;
                    }

                    _window.DataContext = new CanvasViewModel();
                    if (_window.DataContext is CanvasViewModel currentVm)
                        _window.Title = currentVm.WindowTitle;
                },
            },
            new()
            {
                Name = "Open File",
                Description = "Load a .vsaq canvas file",
                Shortcut = "Ctrl+O",
                Icon = MaterialIconKind.FolderOpenOutline,
                Tags = "load import vsaq",
                Execute = async () => await _fileOps.OpenAsync(),
            },
            new()
            {
                Name = "Save",
                Description = "Save current canvas",
                Shortcut = "Ctrl+S",
                Icon = MaterialIconKind.ContentSave,
                Tags = "persist write disk",
                Execute = async () => await _fileOps.SaveAsync(saveAs: false),
            },
            new()
            {
                Name = "Save As",
                Description = "Save canvas to a new file",
                Shortcut = "Ctrl+Shift+S",
                Icon = MaterialIconKind.ContentSaveEdit,
                Tags = "export persist copy",
                Execute = async () => await _fileOps.SaveAsync(saveAs: true),
            },
            // ── Edit ──────────────────────────────────────────────────────────
            new()
            {
                Name = "Undo",
                Description = "Undo last action",
                Shortcut = "Ctrl+Z",
                Icon = MaterialIconKind.Undo,
                Tags = "revert back history",
                Execute = () => _vm.UndoRedo.Undo(),
            },
            new()
            {
                Name = "Redo",
                Description = "Redo last undone action",
                Shortcut = "Ctrl+Y",
                Icon = MaterialIconKind.Redo,
                Tags = "forward history",
                Execute = () => _vm.UndoRedo.Redo(),
            },
            new()
            {
                Name = "Select All",
                Description = "Select all nodes on canvas",
                Shortcut = "Ctrl+A",
                Icon = MaterialIconKind.SelectAll,
                Tags = "highlight mark all nodes",
                Execute = () => _vm.SelectAllCommand.Execute(null),
            },
            new()
            {
                Name = "Deselect All",
                Description = "Clear node selection",
                Shortcut = "Esc",
                Icon = MaterialIconKind.SelectOff,
                Tags = "clear selection",
                Execute = () => _vm.DeselectAllCommand.Execute(null),
            },
            new()
            {
                Name = "Delete Selected",
                Description = "Delete the selected nodes",
                Shortcut = "Del",
                Icon = MaterialIconKind.Delete,
                Tags = "remove erase nodes",
                Execute = () => _vm.DeleteSelectedCommand.Execute(null),
            },
            // ── Canvas ────────────────────────────────────────────────────────
            new()
            {
                Name = "Add Node",
                Description = "Open node search menu to add a node",
                Shortcut = "Shift+A",
                Icon = MaterialIconKind.Plus,
                Tags = "create insert search transform",
                Execute = () => OpenSearch(),
            },
            new()
            {
                Name = "Bring Forward",
                Description = "Move selected nodes one layer forward",
                Shortcut = "Ctrl+PgUp",
                Icon = MaterialIconKind.ArrangeBringForward,
                Tags = "layer z-order forward selected nodes",
                Execute = () => _vm.BringSelectionForwardCommand.Execute(null),
            },
            new()
            {
                Name = "Send Backward",
                Description = "Move selected nodes one layer backward",
                Shortcut = "Ctrl+PgDown",
                Icon = MaterialIconKind.ArrangeSendBackward,
                Tags = "layer z-order backward selected nodes",
                Execute = () => _vm.SendSelectionBackwardCommand.Execute(null),
            },
            new()
            {
                Name = "Bring to Front",
                Description = "Move selected nodes to top layer",
                Shortcut = "Ctrl+Shift+PgUp",
                Icon = MaterialIconKind.ArrangeBringToFront,
                Tags = "layer z-order front selected nodes",
                Execute = () => _vm.BringSelectionToFrontCommand.Execute(null),
            },
            new()
            {
                Name = "Send to Back",
                Description = "Move selected nodes to bottom layer",
                Shortcut = "Ctrl+Shift+PgDown",
                Icon = MaterialIconKind.ArrangeSendToBack,
                Tags = "layer z-order back selected nodes",
                Execute = () => _vm.SendSelectionToBackCommand.Execute(null),
            },
            new()
            {
                Name = "Normalize Layers",
                Description = "Compact node layer indices to a clean 0..N order",
                Shortcut = "",
                Icon = MaterialIconKind.LayersTriple,
                Tags = "layer z-order normalize compact",
                Execute = () => _vm.NormalizeLayersCommand.Execute(null),
            },
            new()
            {
                Name = "Zoom In",
                Description = "Zoom into the canvas",
                Shortcut = "Ctrl++",
                Icon = MaterialIconKind.MagnifyPlus,
                Tags = "magnify enlarge",
                Execute = () => _vm.ZoomInCommand.Execute(null),
            },
            new()
            {
                Name = "Zoom Out",
                Description = "Zoom out of the canvas",
                Shortcut = "Ctrl+-",
                Icon = MaterialIconKind.MagnifyMinus,
                Tags = "shrink reduce",
                Execute = () => _vm.ZoomOutCommand.Execute(null),
            },
            new()
            {
                Name = "Fit to Screen",
                Description = "Fit all nodes into the visible area",
                Shortcut = "",
                Icon = MaterialIconKind.FitToPage,
                Tags = "auto layout view reset zoom",
                Execute = () => _vm.FitToScreenCommand.Execute(null),
            },
            new()
            {
                Name = "Reset Viewport",
                Description = "Reset zoom and pan to default",
                Shortcut = "Ctrl+0",
                Icon = MaterialIconKind.MagnifyRemoveOutline,
                Tags = "100 percent restore zoom pan viewport",
                Execute = () => _vm.ResetZoomCommand.Execute(null),
            },
            // ── Query / Preview ───────────────────────────────────────────────
            new()
            {
                Name = "Toggle Preview",
                Description = "Show or hide the data preview pane",
                Shortcut = "F3",
                Icon = MaterialIconKind.TableEye,
                Tags = "data results table panel",
                Execute = () => _vm.TogglePreviewCommand.Execute(null),
            },
            new()
            {
                Name = "Run Preview",
                Description = "Execute the current query in preview",
                Shortcut = "F5",
                Icon = MaterialIconKind.Play,
                Tags = "execute run sql query results",
                Execute = async () =>
                {
                    if (!_vm.HasErrors && !_vm.LiveSql.IsMutatingCommand)
                        await _preview.RunPreviewAsync();
                },
            },
            // ── Cleanup / Quality ─────────────────────────────────────────────
            new()
            {
                Name = "Auto Layout",
                Description = "Arrange nodes into logical columns automatically",
                Shortcut = "Ctrl+L",
                Icon = MaterialIconKind.FormatHorizontalAlignCenter,
                Tags = "layout arrange columns auto organize readability",
                Execute = () =>
                {
                    _vm.AutoLayoutCommand.Execute(null);
                    _window.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
                },
            },
            new()
            {
                Name = "Cleanup Orphans",
                Description = "Remove all nodes not connected to output",
                Shortcut = "",
                Icon = MaterialIconKind.VectorUnion,
                Tags = "orphan unused disconnected clean delete nodes",
                Execute = () => _vm.CleanupOrphansCommand.Execute(null),
            },
            new()
            {
                Name = "Auto-Fix Naming",
                Description = "Convert all aliases to snake_case",
                Shortcut = "",
                Icon = MaterialIconKind.AutoFix,
                Tags = "rename alias fix naming convention snake case",
                Execute = () => _vm.AutoFixNamingCommand.Execute(null),
            },
            // ── Snap / Alignment ──────────────────────────────────────────────
            new()
            {
                Name = "Toggle Snap to Grid",
                Description = "Snap node positions to 16px grid (Ctrl+G)",
                Shortcut = "Ctrl+G",
                Icon = MaterialIconKind.GridLarge,
                Tags = "snap grid align precision position",
                Execute = () => _vm.ToggleSnapCommand.Execute(null),
            },
            new()
            {
                Name = "Align Left",
                Description = "Align selected nodes to the leftmost edge",
                Shortcut = "",
                Icon = MaterialIconKind.AlignHorizontalLeft,
                Tags = "align left edge selection nodes",
                Execute = () => _vm.AlignLeftCommand.Execute(null),
            },
            new()
            {
                Name = "Align Right",
                Description = "Align selected nodes to the rightmost edge",
                Shortcut = "",
                Icon = MaterialIconKind.AlignHorizontalRight,
                Tags = "align right edge selection nodes",
                Execute = () => _vm.AlignRightCommand.Execute(null),
            },
            new()
            {
                Name = "Align Top",
                Description = "Align selected nodes to the topmost edge",
                Shortcut = "",
                Icon = MaterialIconKind.AlignVerticalTop,
                Tags = "align top edge selection nodes",
                Execute = () => _vm.AlignTopCommand.Execute(null),
            },
            new()
            {
                Name = "Align Bottom",
                Description = "Align selected nodes to the bottom edge",
                Shortcut = "",
                Icon = MaterialIconKind.AlignVerticalBottom,
                Tags = "align bottom edge selection nodes",
                Execute = () => _vm.AlignBottomCommand.Execute(null),
            },
            new()
            {
                Name = "Center Horizontally",
                Description = "Centre selected nodes on a horizontal axis",
                Shortcut = "",
                Icon = MaterialIconKind.AlignHorizontalCenter,
                Tags = "align center middle horizontal nodes",
                Execute = () => _vm.AlignCenterHCommand.Execute(null),
            },
            new()
            {
                Name = "Center Vertically",
                Description = "Centre selected nodes on a vertical axis",
                Shortcut = "",
                Icon = MaterialIconKind.AlignVerticalCenter,
                Tags = "align center middle vertical nodes",
                Execute = () => _vm.AlignCenterVCommand.Execute(null),
            },
            new()
            {
                Name = "Distribute Horizontally",
                Description = "Spread selected nodes with equal horizontal spacing",
                Shortcut = "",
                Icon = MaterialIconKind.DistributeHorizontalCenter,
                Tags = "distribute space equal horizontal nodes",
                Execute = () => _vm.DistributeHCommand.Execute(null),
            },
            new()
            {
                Name = "Distribute Vertically",
                Description = "Spread selected nodes with equal vertical spacing",
                Shortcut = "",
                Icon = MaterialIconKind.DistributeVerticalCenter,
                Tags = "distribute space equal vertical nodes",
                Execute = () => _vm.DistributeVCommand.Execute(null),
            },
            // ── Diagnostics ───────────────────────────────────────────────────
            new()
            {
                Name = "App Diagnostics",
                Description = "Open the self-check diagnostics panel",
                Shortcut = "",
                Icon = MaterialIconKind.HeartPulse,
                Tags = "health check status errors warnings debug",
                Execute = () => _vm.OpenDiagnosticsCommand.Execute(null),
            },
            // ── Export ────────────────────────────────────────────────────────
            new()
            {
                Name = "Export Documentation",
                Description = "Save Markdown documentation of the current flow",
                Shortcut = "",
                Icon = MaterialIconKind.FileDocument,
                Tags = "export markdown doc documentation flow save md",
                Execute = async () => await _export.RunExportDocumentationAsync(),
            },
            new()
            {
                Name = "Export HTML",
                Description = "Generate HTML file from the first HTML Export node",
                Shortcut = "",
                Icon = MaterialIconKind.LanguageHtml5,
                Tags = "export html file output report save",
                Execute = async () =>
                    await _export.RunExportWithDialogAsync(
                        NodeType.HtmlExport,
                        "HTML Files",
                        "html"
                    ),
            },
            new()
            {
                Name = "Export JSON",
                Description = "Generate JSON file from the first JSON Export node",
                Shortcut = "",
                Icon = MaterialIconKind.CodeJson,
                Tags = "export json file output save",
                Execute = async () =>
                    await _export.RunExportWithDialogAsync(
                        NodeType.JsonExport,
                        "JSON Files",
                        "json"
                    ),
            },
            new()
            {
                Name = "Export CSV",
                Description = "Generate CSV file from the first CSV Export node",
                Shortcut = "",
                Icon = MaterialIconKind.FileDelimited,
                Tags = "export csv file tabular output save",
                Execute = async () =>
                    await _export.RunExportWithDialogAsync(NodeType.CsvExport, "CSV Files", "csv"),
            },
            new()
            {
                Name = "Export Excel",
                Description = "Generate XLSX workbook from the first Excel Export node",
                Shortcut = "",
                Icon = MaterialIconKind.MicrosoftExcel,
                Tags = "export excel xlsx file tabular output spreadsheet save",
                Execute = async () =>
                    await _export.RunExportWithDialogAsync(
                        NodeType.ExcelExport,
                        "Excel Files",
                        "xlsx"
                    ),
            },
        ];

    private List<PaletteCommandItem> CreateTemplateCommands() =>
        [
            .. QueryTemplateLibrary.All.Select(t => new PaletteCommandItem
            {
                Name = $"Template: {t.Name}",
                Description = t.Description,
                Icon = MaterialIconKind.ViewGrid,
                Tags = $"template starter query {t.Category.ToLowerInvariant()} {t.Tags}",
                Execute = () =>
                {
                    _vm.LoadTemplate(t);
                    _window.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
                },
            }),
        ];

    private void OpenSearch()
    {
        InfiniteCanvas? canvas = _window.FindControl<InfiniteCanvas>("TheCanvas");
        Point ctr = canvas is not null
            ? new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2)
            : new Point(400, 300);
        _vm.SearchMenu.Open(ctr);
    }
}
