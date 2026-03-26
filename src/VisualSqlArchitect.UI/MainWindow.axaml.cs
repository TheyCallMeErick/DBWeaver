using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI;

public partial class MainWindow : Window
{
    private CanvasViewModel Vm => (CanvasViewModel)DataContext!;

    private static readonly FilePickerFileType FileType = new("SQL Architect Canvas")
    { Patterns=["*.vsaq"], MimeTypes=["application/json"] };

    private double _previewHeight = 220;
    private CancellationTokenSource? _autoSaveCts;

    // Column lookup for restoring TableSource nodes from the demo catalog
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<(string Name, PinDataType Type)>> ColumnLookup =
        CanvasViewModel.DemoCatalog.ToDictionary(
            t => t.FullName,
            t => t.Cols);

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new CanvasViewModel();
        WireWindowChrome();
        WireMenuButtons();
        WireSearchMenu();
        WireCommandPalette();
        WirePreview();
        WireLayout();
        WireAutoSave();
        CheckForSession();
        Vm.PropertyChanged += (_, e) => { if (e.PropertyName==nameof(CanvasViewModel.WindowTitle)) Title=Vm.WindowTitle; };
        Title = Vm.WindowTitle;
    }

    private void WireWindowChrome()
    {
        var close=this.FindControl<Button>("CloseWindowBtn");
        var min=this.FindControl<Button>("MinimizeBtn");
        var max=this.FindControl<Button>("MaximizeBtn");
        if (close is not null) close.Click+=(_,_)=>Close();
        if (min   is not null) min.Click+=(_,_)=>WindowState=WindowState.Minimized;
        if (max   is not null) max.Click+=(_,_)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
    }

    private void TitleBar_PointerPressed(object? s, PointerPressedEventArgs e)
    { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e); }

    private void WireMenuButtons()
    {
        void B(string name, Action a) { var btn=this.FindControl<Button>(name); if (btn is not null) btn.Click+=(_,_)=>a(); }
        B("NewBtn",           () => { DataContext = new CanvasViewModel(); Title = Vm.WindowTitle; });
        B("NewTabBtn",        () => { DataContext = new CanvasViewModel(); Title = Vm.WindowTitle; });
        B("OpenSearchBtn",    OpenSearch);
        B("SaveBtn",          () => _ = SaveAsync());
        B("OpenBtn",          () => _ = OpenAsync());
        B("ZoomInBtn",        () => Vm.ZoomInCommand.Execute(null));
        B("ZoomOutBtn",       () => Vm.ZoomOutCommand.Execute(null));
        B("FitBtn",           () => Vm.FitToScreenCommand.Execute(null));
        B("TogglePreviewBtn", () => Vm.TogglePreviewCommand.Execute(null));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key==Key.Escape)
        {
            if (Vm.CommandPalette.IsVisible) { Vm.CommandPalette.Close();         e.Handled=true; return; }
            if (Vm.SearchMenu.IsVisible)     { Vm.SearchMenu.Close();             e.Handled=true; return; }
            if (Vm.AutoJoin.IsVisible)       { Vm.AutoJoin.Dismiss();             e.Handled=true; return; }
            if (Vm.DataPreview.IsVisible)    { Vm.DataPreview.IsVisible=false;    e.Handled=true; return; }
        }
        if (e.Key==Key.S&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { _=SaveAsync(e.KeyModifiers.HasFlag(KeyModifiers.Shift)); e.Handled=true; return; }
        if (e.Key==Key.O&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { _=OpenAsync(); e.Handled=true; return; }
        if (e.Key==Key.N&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { DataContext=new CanvasViewModel(); e.Handled=true; return; }
        if (e.Key==Key.Z&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { Vm.UndoRedo.Undo(); e.Handled=true; return; }
        if (e.Key==Key.Y&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { Vm.UndoRedo.Redo(); e.Handled=true; return; }
        if (e.Key==Key.K&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { Vm.CommandPalette.Open(); e.Handled=true; return; }
        if (e.Key==Key.A&&e.KeyModifiers.HasFlag(KeyModifiers.Shift)&&!Vm.SearchMenu.IsVisible) { OpenSearch(); e.Handled=true; return; }
        if (e.Key==Key.F&&e.KeyModifiers.HasFlag(KeyModifiers.Control)&&!Vm.SearchMenu.IsVisible) { OpenSearch(); e.Handled=true; return; }
        if (e.Key==Key.F3) { Vm.DataPreview.Toggle(); e.Handled=true; return; }
        if (e.Key==Key.F5) { if (!Vm.HasErrors && !Vm.LiveSql.IsMutatingCommand) _=RunPreviewAsync(); e.Handled=true; return; }
        if ((e.Key==Key.OemPlus||e.Key==Key.Add)&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { Vm.ZoomInCommand.Execute(null); e.Handled=true; return; }
        if ((e.Key==Key.OemMinus||e.Key==Key.Subtract)&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { Vm.ZoomOutCommand.Execute(null); e.Handled=true; return; }
        if ((e.Key==Key.D0||e.Key==Key.NumPad0)&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { Vm.FitToScreenCommand.Execute(null); e.Handled=true; }
    }

    // ── Layout persistence & DataPreview row management ──────────────────────

    private static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VisualSqlArchitect");

    private static string LayoutFile  => Path.Combine(AppDataDir, "layout.json");
    private static string SessionFile => Path.Combine(AppDataDir, "last-session.vsaq");
    private static string SessionTmp  => Path.Combine(AppDataDir, "last-session.vsaq.tmp");

    private void WireLayout()
    {
        LoadLayout();
        Closing += (_, _) => SaveLayout();
        Vm.DataPreview.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DataPreviewViewModel.IsVisible))
                UpdatePreviewRow();
        };
        UpdatePreviewRow();
    }

    private void UpdatePreviewRow()
    {
        var centerGrid = this.FindControl<Grid>("CenterGrid");
        if (centerGrid is null) return;
        if (Vm.DataPreview.IsVisible)
        {
            centerGrid.RowDefinitions[2].Height = new GridLength(8);
            centerGrid.RowDefinitions[3].Height = new GridLength(_previewHeight);
        }
        else
        {
            var current = centerGrid.RowDefinitions[3].Height.Value;
            if (current > 0) _previewHeight = current;
            centerGrid.RowDefinitions[2].Height = new GridLength(0);
            centerGrid.RowDefinitions[3].Height = new GridLength(0);
        }
    }

    private void SaveLayout()
    {
        var bodyGrid = this.FindControl<Grid>("BodyGrid");
        var centerGrid = this.FindControl<Grid>("CenterGrid");
        if (bodyGrid is null || centerGrid is null) return;
        var current = centerGrid.RowDefinitions[3].Height.Value;
        if (current > 0) _previewHeight = current;
        var layout = new LayoutState(
            bodyGrid.ColumnDefinitions[0].Width.Value,
            bodyGrid.ColumnDefinitions[4].Width.Value,
            _previewHeight);
        try
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllText(LayoutFile, JsonSerializer.Serialize(layout));
        }
        catch { /* best effort */ }
    }

    private void LoadLayout()
    {
        try
        {
            if (!File.Exists(LayoutFile)) return;
            var layout = JsonSerializer.Deserialize<LayoutState>(File.ReadAllText(LayoutFile));
            if (layout is null) return;
            var bodyGrid = this.FindControl<Grid>("BodyGrid");
            if (bodyGrid is not null)
            {
                bodyGrid.ColumnDefinitions[0].Width = new GridLength(Math.Clamp(layout.LeftWidth,   180, 400));
                bodyGrid.ColumnDefinitions[4].Width = new GridLength(Math.Clamp(layout.RightWidth,  200, 500));
            }
            if (layout.PreviewHeight > 0)
                _previewHeight = Math.Clamp(layout.PreviewHeight, 150, 600);
        }
        catch { /* best effort */ }
    }

    private record LayoutState(double LeftWidth, double RightWidth, double PreviewHeight);

    // ── Auto-save session ─────────────────────────────────────────────────────

    private void WireAutoSave()
    {
        // Wire banner buttons
        var restoreBtn  = this.FindControl<Button>("RestoreSessionBtn");
        var dismissBtn  = this.FindControl<Button>("DismissRestoreBtn");
        if (restoreBtn is not null) restoreBtn.Click  += async (_, _) => await RestoreSessionAsync();
        if (dismissBtn is not null) dismissBtn.Click  += (_, _)       => DismissSession();

        // Subscribe to canvas changes via IsDirty
        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CanvasViewModel.IsDirty) or
                nameof(CanvasViewModel.Zoom) or nameof(CanvasViewModel.PanOffset))
                ScheduleAutoSave();
        };

        // Force save on close (in addition to layout save)
        Closing += async (_, _) => await SaveSessionNowAsync();
    }

    private void ScheduleAutoSave()
    {
        _autoSaveCts?.Cancel();
        _autoSaveCts = new CancellationTokenSource();
        var token = _autoSaveCts.Token;
        Task.Delay(1500, token).ContinueWith(_ =>
        {
            if (!token.IsCancellationRequested)
                Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = SaveSessionNowAsync());
        }, TaskScheduler.Default);
    }

    private async Task SaveSessionNowAsync()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            var json = CanvasSerializer.Serialize(Vm);
            await File.WriteAllTextAsync(SessionTmp, json);
            File.Move(SessionTmp, SessionFile, overwrite: true);
        }
        catch { /* best effort */ }
    }

    // ── Session restore ───────────────────────────────────────────────────────

    private void CheckForSession()
    {
        if (!CanvasSerializer.IsValidFile(SessionFile)) return;
        var banner = this.FindControl<Border>("RestoreBanner");
        if (banner is not null) banner.IsVisible = true;
    }

    private async Task RestoreSessionAsync()
    {
        var banner = this.FindControl<Border>("RestoreBanner");
        if (banner is not null) banner.IsVisible = false;
        try
        {
            await CanvasSerializer.LoadFromFileAsync(SessionFile, Vm, ColumnLookup);
            Vm.IsDirty = false;
            this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
        }
        catch (Exception ex)
        {
            Vm.DataPreview.ShowError($"Restore failed: {ex.Message}", ex);
            // Session corrupted — delete it
            try { File.Delete(SessionFile); } catch { /* ignore */ }
        }
    }

    private void DismissSession()
    {
        var banner = this.FindControl<Border>("RestoreBanner");
        if (banner is not null) banner.IsVisible = false;
        try { File.Delete(SessionFile); } catch { /* ignore */ }
    }

    private void WireAutoJoin()
    {
        var overlay = this.FindControl<AutoJoinOverlay>("AutoJoinOverlayCtrl");
        if (overlay is null) return;

        // The AutoJoin overlay's internal buttons call Accept/Dismiss on JoinSuggestionCardViewModel
        // which raises events that CanvasViewModel.AutoJoin handles.
        // No extra wiring needed here — the cards self-contain their logic.
    }

    private void WireCommandPalette()
    {
        Vm.CommandPalette.RegisterCommands(
        [
            new() { Name="New Canvas",     Description="Clear canvas and start fresh",       Shortcut="Ctrl+N",       Icon="□",  Execute=() => { DataContext=new CanvasViewModel(); Title=Vm.WindowTitle; } },
            new() { Name="Open File",      Description="Load a .vsaq canvas file",           Shortcut="Ctrl+O",       Icon="📂", Execute=() => _=OpenAsync() },
            new() { Name="Save",           Description="Save current canvas",                Shortcut="Ctrl+S",       Icon="💾", Execute=() => _=SaveAsync() },
            new() { Name="Save As",        Description="Save canvas to a new file",          Shortcut="Ctrl+Shift+S", Icon="💾", Execute=() => _=SaveAsync(saveAs:true) },
            new() { Name="Undo",           Description="Undo last action",                   Shortcut="Ctrl+Z",       Icon="↩",  Execute=() => Vm.UndoRedo.Undo() },
            new() { Name="Redo",           Description="Redo last undone action",            Shortcut="Ctrl+Y",       Icon="↪",  Execute=() => Vm.UndoRedo.Redo() },
            new() { Name="Select All",     Description="Select all nodes on canvas",         Shortcut="",             Icon="⊡",  Execute=() => Vm.SelectAllCommand.Execute(null) },
            new() { Name="Delete Selected",Description="Delete the selected nodes",          Shortcut="Del",          Icon="✕",  Execute=() => Vm.DeleteSelectedCommand.Execute(null) },
            new() { Name="Add Node",       Description="Open node search menu",             Shortcut="Shift+A",      Icon="＋", Execute=OpenSearch },
            new() { Name="Zoom In",        Description="Zoom into the canvas",              Shortcut="Ctrl++",       Icon="🔍", Execute=() => Vm.ZoomInCommand.Execute(null) },
            new() { Name="Zoom Out",       Description="Zoom out of the canvas",            Shortcut="Ctrl+-",       Icon="🔍", Execute=() => Vm.ZoomOutCommand.Execute(null) },
            new() { Name="Fit to Screen",  Description="Fit all nodes to the visible area", Shortcut="Ctrl+0",       Icon="⊞",  Execute=() => Vm.FitToScreenCommand.Execute(null) },
            new() { Name="Reset Zoom",     Description="Reset zoom to 100%",                Shortcut="",             Icon="⊙",  Execute=() => Vm.ResetZoomCommand.Execute(null) },
            new() { Name="Toggle Preview", Description="Show or hide the data preview pane",Shortcut="F3",           Icon="▶",  Execute=() => Vm.TogglePreviewCommand.Execute(null) },
            new() { Name="Run Preview",    Description="Execute current query preview",     Shortcut="F5",           Icon="▷",  Execute=() => { if (!Vm.HasErrors && !Vm.LiveSql.IsMutatingCommand) _=RunPreviewAsync(); } },
        ]);
    }

    private void WireSearchMenu()
    {
        var overlay=this.FindControl<SearchMenuControl>("SearchOverlay");
        if (overlay is null) return;
        overlay.SpawnRequested     += (_,def)  => { Vm.SpawnNode(def, Vm.SearchMenu.SpawnPosition); this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires(); };
        overlay.SpawnTableRequested += (_,args) => { Vm.SpawnTableNode(args.FullName, args.Cols.Select(c=>(c.Name,c.Type)), Vm.SearchMenu.SpawnPosition); this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires(); };
    }

    private void OpenSearch()
    {
        var canvas=this.FindControl<InfiniteCanvas>("TheCanvas");
        var ctr=canvas is not null?new Point(canvas.Bounds.Width/2,canvas.Bounds.Height/2):new Point(400,300);
        Vm.SearchMenu.Open(ctr);
    }

    private void WirePreview()
    {
        var run=this.FindControl<Button>("RunButton");
        var cls=this.FindControl<Button>("ClosePreviewBtn");
        if (run is not null)
        {
            run.Click+=async (_,_)=>await RunPreviewAsync();
            void UpdateRunEnabled() => run.IsEnabled = !Vm.HasErrors && !Vm.LiveSql.IsMutatingCommand;
            UpdateRunEnabled();
            Vm.PropertyChanged        +=(_,e)=>{ if (e.PropertyName==nameof(CanvasViewModel.HasErrors))                  UpdateRunEnabled(); };
            Vm.LiveSql.PropertyChanged+=(_,e)=>{ if (e.PropertyName==nameof(LiveSqlBarViewModel.IsMutatingCommand)) UpdateRunEnabled(); };
        }
        if (cls is not null) cls.Click+=(_,_)=>Vm.DataPreview.IsVisible=false;
    }

    private async Task RunPreviewAsync()
    {
        // Double-check safe preview guard (defensive, button should already be disabled)
        if (Vm.LiveSql.IsMutatingCommand)
        {
            Vm.DataPreview.ShowError("Safe Preview Mode: data-mutating commands (INSERT/UPDATE/DELETE/DROP/ALTER/TRUNCATE) cannot be executed in preview.");
            return;
        }
        var sql=string.IsNullOrEmpty(Vm.LiveSql.RawSql)
            ? (string.IsNullOrEmpty(Vm.QueryText)?"SELECT * FROM orders LIMIT 100":Vm.QueryText)
            : Vm.LiveSql.RawSql;

        // Log guardrail warnings to debug output (non-blocking)
        foreach (var g in Vm.LiveSql.GuardIssues)
            System.Diagnostics.Debug.WriteLine($"[GUARDRAIL] {g.Code}: {g.Message}");

        Vm.DataPreview.ShowLoading(sql);
        try
        {
            await Task.Delay(500);
            var dt=BuildDemo();
            Vm.DataPreview.ShowResults(dt,89);
        }
        catch (Exception ex) { Vm.DataPreview.ShowError(ex.Message, ex); }
    }

    private static System.Data.DataTable BuildDemo()
    {
        var dt=new System.Data.DataTable();
        dt.Columns.Add("id",typeof(int)); dt.Columns.Add("StatusUpper",typeof(string));
        dt.Columns.Add("total",typeof(decimal)); dt.Columns.Add("created_at",typeof(string)); dt.Columns.Add("City",typeof(string));
        var rng=new Random(42);
        var s=new[]{"ACTIVE","SHIPPED","PENDING","CANCELLED"};
        var c=new[]{"New York","São Paulo","London","Berlin","Tokyo","Paris"};
        for (int i=1;i<=100;i++) dt.Rows.Add(i,s[rng.Next(s.Length)],Math.Round(rng.NextDouble()*4000+100,2),DateTime.UtcNow.AddDays(-rng.Next(0,365)).ToString("yyyy-MM-dd"),c[rng.Next(c.Length)]);
        return dt;
    }

    private async Task SaveAsync(bool saveAs=false)
    {
        var path=(!saveAs&&Vm.CurrentFilePath is not null)?Vm.CurrentFilePath:null;
        if (path is null)
        {
            var r=await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title="Save Canvas", DefaultExtension="vsaq", FileTypeChoices=[FileType], SuggestedFileName="Query1" });
            path=r?.TryGetLocalPath();
        }
        if (path is null) return;
        try { await CanvasSerializer.SaveToFileAsync(path,Vm); Vm.CurrentFilePath=path; Vm.IsDirty=false; }
        catch (Exception ex) { Vm.DataPreview.ShowError($"Save failed: {ex.Message}", ex); }
    }

    private async Task OpenAsync()
    {
        var results=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title="Open Canvas", FileTypeFilter=[FileType], AllowMultiple=false });
        var path=results.FirstOrDefault()?.TryGetLocalPath();
        if (path is null) return;
        try { await CanvasSerializer.LoadFromFileAsync(path,Vm); Vm.CurrentFilePath=path; Vm.IsDirty=false; this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires(); }
        catch (Exception ex) { Vm.DataPreview.ShowError($"Open failed: {ex.Message}", ex); }
    }
}
