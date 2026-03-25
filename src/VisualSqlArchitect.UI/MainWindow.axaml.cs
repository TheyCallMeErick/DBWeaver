using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI;

public partial class MainWindow : Window
{
    private CanvasViewModel Vm => (CanvasViewModel)DataContext!;

    private static readonly FilePickerFileType FileType = new("SQL Architect Canvas")
    { Patterns=["*.vsaq"], MimeTypes=["application/json"] };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new CanvasViewModel();
        WireWindowChrome();
        WireMenuButtons();
        WireSearchMenu();
        WirePreview();
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
        if (e.Key==Key.S&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { _=SaveAsync(e.KeyModifiers.HasFlag(KeyModifiers.Shift)); e.Handled=true; return; }
        if (e.Key==Key.O&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { _=OpenAsync(); e.Handled=true; return; }
        if (e.Key==Key.N&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { DataContext=new CanvasViewModel(); e.Handled=true; return; }
        if (e.Key==Key.Z&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { Vm.UndoRedo.Undo(); e.Handled=true; return; }
        if (e.Key==Key.Y&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { Vm.UndoRedo.Redo(); e.Handled=true; return; }
        if (e.Key==Key.A&&e.KeyModifiers.HasFlag(KeyModifiers.Shift)&&!Vm.SearchMenu.IsVisible) { OpenSearch(); e.Handled=true; return; }
        if (e.Key==Key.F3) { Vm.DataPreview.Toggle(); e.Handled=true; }
    }

    private void WireAutoJoin()
    {
        var overlay = this.FindControl<AutoJoinOverlay>("AutoJoinOverlayCtrl");
        if (overlay is null) return;

        // The AutoJoin overlay's internal buttons call Accept/Dismiss on JoinSuggestionCardViewModel
        // which raises events that CanvasViewModel.AutoJoin handles.
        // No extra wiring needed here — the cards self-contain their logic.
    }

    private void WireSearchMenu()
    {
        var overlay=this.FindControl<SearchMenuControl>("SearchOverlay");
        if (overlay is not null)
            overlay.SpawnRequested+=(_,def) => { Vm.SpawnNode(def,Vm.SearchMenu.SpawnPosition); this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires(); };
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
        if (run is not null) run.Click+=async (_,_)=>await RunPreviewAsync();
        if (cls is not null) cls.Click+=(_,_)=>Vm.DataPreview.IsVisible=false;
    }

    private async Task RunPreviewAsync()
    {
        var sql=string.IsNullOrEmpty(Vm.LiveSql.RawSql)
            ? (string.IsNullOrEmpty(Vm.QueryText)?"SELECT * FROM orders LIMIT 100":Vm.QueryText)
            : Vm.LiveSql.RawSql;
        Vm.DataPreview.ShowLoading(sql);
        try
        {
            await Task.Delay(500);
            var dt=BuildDemo();
            Vm.DataPreview.ShowResults(dt,89);
        }
        catch (Exception ex) { Vm.DataPreview.ShowError(ex.Message); }
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
        catch (Exception ex) { Vm.DataPreview.ShowError($"Save failed: {ex.Message}"); }
    }

    private async Task OpenAsync()
    {
        var results=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title="Open Canvas", FileTypeFilter=[FileType], AllowMultiple=false });
        var path=results.FirstOrDefault()?.TryGetLocalPath();
        if (path is null) return;
        try { await CanvasSerializer.LoadFromFileAsync(path,Vm); Vm.CurrentFilePath=path; Vm.IsDirty=false; this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires(); }
        catch (Exception ex) { Vm.DataPreview.ShowError($"Open failed: {ex.Message}"); }
    }
}
