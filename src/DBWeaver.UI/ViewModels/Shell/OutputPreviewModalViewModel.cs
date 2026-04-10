using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.ViewModels;

public sealed class OutputPreviewModalViewModel : ViewModelBase
{
    public enum EOutputPreviewMode
    {
        Query,
        Ddl,
        Unavailable,
    }

    public enum EOutputPreviewTab
    {
        Primary,
        Diagnostics,
    }

    private bool _isVisible;
    private EOutputPreviewMode _mode = EOutputPreviewMode.Query;
    private EOutputPreviewTab _activeTab = EOutputPreviewTab.Primary;
    private DataPreviewViewModel? _queryDataPreview;
    private LiveSqlBarViewModel? _queryLiveSql;
    private AppDiagnosticsViewModel? _diagnostics;
    private string _ddlSqlText = string.Empty;
    private string _ddlProviderLabel = string.Empty;
    private string _title = "Preview";
    private string _primaryTabLabel = "Preview";
    private string _unavailableMessage = "Preview is unavailable for this document.";

    public OutputPreviewModalViewModel()
    {
        CloseCommand = new RelayCommand(() => IsVisible = false);
        ShowPrimaryTabCommand = new RelayCommand(() => ActiveTab = EOutputPreviewTab.Primary);
        ShowDiagnosticsTabCommand = new RelayCommand(() => ActiveTab = EOutputPreviewTab.Diagnostics);
    }

    public RelayCommand CloseCommand { get; }
    public RelayCommand ShowPrimaryTabCommand { get; }
    public RelayCommand ShowDiagnosticsTabCommand { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    public EOutputPreviewMode Mode
    {
        get => _mode;
        private set
        {
            if (!Set(ref _mode, value))
                return;

            RaisePropertyChanged(nameof(IsQueryMode));
            RaisePropertyChanged(nameof(IsDdlMode));
            RaisePropertyChanged(nameof(IsUnavailableMode));
            RaisePropertyChanged(nameof(Title));
            RaisePropertyChanged(nameof(PrimaryTabLabel));
            RaisePropertyChanged(nameof(HasDiagnostics));
            RaisePropertyChanged(nameof(ShowPrimaryContent));
            RaisePropertyChanged(nameof(ShowDiagnosticsContent));
            RaisePropertyChanged(nameof(ShowQueryPrimaryContent));
            RaisePropertyChanged(nameof(ShowDdlPrimaryContent));
            RaisePropertyChanged(nameof(ShowUnavailablePrimaryContent));
        }
    }

    public bool IsQueryMode => Mode == EOutputPreviewMode.Query;
    public bool IsDdlMode => Mode == EOutputPreviewMode.Ddl;
    public bool IsUnavailableMode => Mode == EOutputPreviewMode.Unavailable;

    public EOutputPreviewTab ActiveTab
    {
        get => _activeTab;
        private set
        {
            if (!Set(ref _activeTab, value))
                return;

            RaisePropertyChanged(nameof(ShowPrimaryContent));
            RaisePropertyChanged(nameof(ShowDiagnosticsContent));
            RaisePropertyChanged(nameof(ShowQueryPrimaryContent));
            RaisePropertyChanged(nameof(ShowDdlPrimaryContent));
            RaisePropertyChanged(nameof(ShowUnavailablePrimaryContent));
        }
    }

    public string Title
    {
        get => _title;
        private set => Set(ref _title, value);
    }

    public string PrimaryTabLabel
    {
        get => _primaryTabLabel;
        private set => Set(ref _primaryTabLabel, value);
    }

    public bool ShowPrimaryContent => ActiveTab == EOutputPreviewTab.Primary;
    public bool ShowDiagnosticsContent => HasDiagnostics && ActiveTab == EOutputPreviewTab.Diagnostics;
    public bool ShowQueryPrimaryContent => IsQueryMode && ShowPrimaryContent;
    public bool ShowDdlPrimaryContent => IsDdlMode && ShowPrimaryContent;
    public bool ShowUnavailablePrimaryContent => IsUnavailableMode && ShowPrimaryContent;

    public DataPreviewViewModel? QueryDataPreview
    {
        get => _queryDataPreview;
        private set => Set(ref _queryDataPreview, value);
    }

    public LiveSqlBarViewModel? QueryLiveSql
    {
        get => _queryLiveSql;
        private set => Set(ref _queryLiveSql, value);
    }

    public AppDiagnosticsViewModel? Diagnostics
    {
        get => _diagnostics;
        private set
        {
            if (!Set(ref _diagnostics, value))
                return;

            RaisePropertyChanged(nameof(HasDiagnostics));
            RaisePropertyChanged(nameof(ShowDiagnosticsContent));
        }
    }

    public bool HasDiagnostics => Diagnostics is not null;

    public string DdlSqlText
    {
        get => _ddlSqlText;
        private set
        {
            if (!Set(ref _ddlSqlText, value))
                return;

            RaisePropertyChanged(nameof(HasDdlSql));
        }
    }

    public string DdlProviderLabel
    {
        get => _ddlProviderLabel;
        private set => Set(ref _ddlProviderLabel, value);
    }

    public string UnavailableMessage
    {
        get => _unavailableMessage;
        private set => Set(ref _unavailableMessage, value);
    }

    public bool HasDdlSql => !string.IsNullOrWhiteSpace(DdlSqlText);

    public void OpenForQuery(CanvasViewModel canvas)
    {
        Mode = EOutputPreviewMode.Query;
        Title = "Preview";
        PrimaryTabLabel = "Preview";
        QueryDataPreview = canvas.DataPreview;
        QueryLiveSql = canvas.LiveSql;
        Diagnostics = canvas.Diagnostics;
        ActiveTab = EOutputPreviewTab.Primary;
        IsVisible = true;
        Diagnostics?.RunChecksCommand.Execute(null);
    }

    public void OpenForDdl(CanvasViewModel canvas, LiveDdlBarViewModel liveDdl, string providerLabel)
    {
        Mode = EOutputPreviewMode.Ddl;
        Title = "SQL DDL Preview";
        PrimaryTabLabel = "SQL DDL";
        QueryDataPreview = null;
        QueryLiveSql = null;
        Diagnostics = canvas.Diagnostics;
        DdlSqlText = BuildDdlPreviewText(liveDdl);
        DdlProviderLabel = providerLabel;
        ActiveTab = EOutputPreviewTab.Primary;
        IsVisible = true;
        Diagnostics?.RunChecksCommand.Execute(null);
    }

    public void OpenUnavailable(string title, string primaryTabLabel, string unavailableMessage)
    {
        Mode = EOutputPreviewMode.Unavailable;
        Title = string.IsNullOrWhiteSpace(title) ? "Preview" : title;
        PrimaryTabLabel = string.IsNullOrWhiteSpace(primaryTabLabel) ? "Preview" : primaryTabLabel;
        UnavailableMessage = string.IsNullOrWhiteSpace(unavailableMessage)
            ? "Preview is unavailable for this document."
            : unavailableMessage;
        QueryDataPreview = null;
        QueryLiveSql = null;
        Diagnostics = null;
        DdlSqlText = string.Empty;
        DdlProviderLabel = string.Empty;
        ActiveTab = EOutputPreviewTab.Primary;
        IsVisible = true;
    }

    private static string BuildDdlPreviewText(LiveDdlBarViewModel liveDdl)
    {
        if (!string.IsNullOrWhiteSpace(liveDdl.RawSql))
            return liveDdl.RawSql;

        return string.Empty;
    }
}
