namespace VisualSqlArchitect.UI.ViewModels;

public enum ToastSeverity
{
    Success,
    Warning,
    Error,
}

public sealed class ToastCenterViewModel : ViewModelBase
{
    private bool _isVisible;
    private bool _isDetailsOpen;
    private string _message = string.Empty;
    private string? _details;
    private ToastSeverity _severity;
    private int _version;
    private CancellationTokenSource? _autoHideCts;

    public bool IsVisible
    {
        get => _isVisible;
        private set => Set(ref _isVisible, value);
    }

    public bool IsDetailsOpen
    {
        get => _isDetailsOpen;
        private set => Set(ref _isDetailsOpen, value);
    }

    public string Message
    {
        get => _message;
        private set => Set(ref _message, value);
    }

    public string? Details
    {
        get => _details;
        private set
        {
            Set(ref _details, value);
            RaisePropertyChanged(nameof(HasDetails));
        }
    }

    public bool HasDetails => !string.IsNullOrWhiteSpace(_details);

    public ToastSeverity Severity
    {
        get => _severity;
        private set
        {
            Set(ref _severity, value);
            RaisePropertyChanged(nameof(SeverityLabel));
            RaisePropertyChanged(nameof(SeverityIcon));
            RaisePropertyChanged(nameof(AccentColor));
            RaisePropertyChanged(nameof(DetailsTitle));
        }
    }

    public string SeverityLabel => _severity switch
    {
        ToastSeverity.Success => "Success",
        ToastSeverity.Warning => "Warning",
        _ => "Error",
    };

    public string SeverityIcon => _severity switch
    {
        ToastSeverity.Success => "✔",
        ToastSeverity.Warning => "⚠",
        _ => "✕",
    };

    public string AccentColor => _severity switch
    {
        ToastSeverity.Success => "#22C55E",
        ToastSeverity.Warning => "#F59E0B",
        _ => "#EF4444",
    };

    public string DetailsTitle => _severity switch
    {
        ToastSeverity.Success => "Success Details",
        ToastSeverity.Warning => "Warning Details",
        _ => "Error Details",
    };

    public RelayCommand DismissCommand { get; }
    public RelayCommand ShowDetailsCommand { get; }
    public RelayCommand CloseDetailsCommand { get; }

    public ToastCenterViewModel()
    {
        DismissCommand = new RelayCommand(Dismiss);
        ShowDetailsCommand = new RelayCommand(OpenDetails, () => HasDetails);
        CloseDetailsCommand = new RelayCommand(() => IsDetailsOpen = false);
    }

    public void ShowSuccess(string message, string? details = null) =>
        Show(ToastSeverity.Success, message, details);

    public void ShowError(string message, string? details = null) =>
        Show(ToastSeverity.Error, message, details);

    public void ShowWarning(string message, string? details = null) =>
        Show(ToastSeverity.Warning, message, details);

    private void Show(ToastSeverity severity, string message, string? details)
    {
        _version++;
        CancelAutoHide();

        Severity = severity;
        Message = message;
        Details = details;
        IsDetailsOpen = false;
        IsVisible = true;

        ShowDetailsCommand.NotifyCanExecuteChanged();
        ScheduleAutoHide(_version);
    }

    private void Dismiss()
    {
        CancelAutoHide();
        IsVisible = false;
    }

    private void OpenDetails()
    {
        if (!HasDetails)
            return;

        IsDetailsOpen = true;
    }

    private void ScheduleAutoHide(int version)
    {
        _autoHideCts = new CancellationTokenSource();
        CancellationToken token = _autoHideCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(6), token);
                if (token.IsCancellationRequested || version != _version)
                    return;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (version == _version && !IsDetailsOpen)
                        IsVisible = false;
                });
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private void CancelAutoHide()
    {
        _autoHideCts?.Cancel();
        _autoHideCts?.Dispose();
        _autoHideCts = null;
    }
}
