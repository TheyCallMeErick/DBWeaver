using DBWeaver.UI.Services.Localization;
using Avalonia;
using Avalonia.Media;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.ViewModels;

public sealed class ToastCenterViewModel : ViewModelBase
{
    private bool _isVisible;
    private bool _isDetailsOpen;
    private string _message = string.Empty;
    private string? _details;
    private ToastSeverity _severity;
    private int _version;
    private CancellationTokenSource? _autoHideCts;
    private Action? _detailsAction;

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
            RaisePropertyChanged(nameof(HasDetailsAction));
        }
    }

    public bool HasDetails => _severity != ToastSeverity.Success
        && (!string.IsNullOrWhiteSpace(_details) || _detailsAction is not null);
    public bool HasDetailsAction => _detailsAction is not null;

    public ToastSeverity Severity
    {
        get => _severity;
        private set
        {
            Set(ref _severity, value);
            RaisePropertyChanged(nameof(SeverityLabel));
            RaisePropertyChanged(nameof(SeverityIcon));
            RaisePropertyChanged(nameof(AccentBrush));
            RaisePropertyChanged(nameof(DetailsTitle));
        }
    }

    public string SeverityLabel => _severity switch
    {
        ToastSeverity.Success => L("toast.severity.success", "Success"),
        ToastSeverity.Warning => L("toast.severity.warning", "Warning"),
        _ => L("toast.severity.error", "Error"),
    };

    public string SeverityIcon => _severity switch
    {
        ToastSeverity.Success => "✔",
        ToastSeverity.Warning => "⚠",
        _ => "✕",
    };

    public IBrush AccentBrush => _severity switch
    {
        ToastSeverity.Success => ResourceBrush("StatusOkBrush", UiColorConstants.C_2FBF84),
        ToastSeverity.Warning => ResourceBrush("StatusWarningBrush", UiColorConstants.C_D9A441),
        _ => ResourceBrush("StatusErrorBrush", UiColorConstants.C_E16174),
    };

    public string DetailsTitle => _severity switch
    {
        ToastSeverity.Success => L("toast.details.success", "Success Details"),
        ToastSeverity.Warning => L("toast.details.warning", "Warning Details"),
        _ => L("toast.details.error", "Error Details"),
    };

    public RelayCommand DismissCommand { get; }
    public RelayCommand ShowDetailsCommand { get; }
    public RelayCommand CloseDetailsCommand { get; }

    public ToastCenterViewModel()
    {
        DismissCommand = new RelayCommand(Dismiss);
        ShowDetailsCommand = new RelayCommand(OpenDetails, () => HasDetails || HasDetailsAction);
        CloseDetailsCommand = new RelayCommand(() => IsDetailsOpen = false);
    }

    public void ShowSuccess(string message, string? details = null) =>
        Show(ToastSeverity.Success, message, details, null);

    public void ShowError(string message, string? details = null) =>
        Show(ToastSeverity.Error, message, details, null);

    public void ShowWarning(string message, string? details = null, Action? onDetails = null) =>
        Show(ToastSeverity.Warning, message, details, onDetails);

    private void Show(ToastSeverity severity, string message, string? details, Action? onDetails)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _version++;
        CancelAutoHide();

        Severity = severity;
        Message = message;
        Details = details;
        _detailsAction = onDetails;
        RaisePropertyChanged(nameof(HasDetails));
        RaisePropertyChanged(nameof(HasDetailsAction));
        IsDetailsOpen = false;
        IsVisible = true;

        ShowDetailsCommand.NotifyCanExecuteChanged();
        ScheduleAutoHide(_version);
    }

    private void Dismiss()
    {
        CancelAutoHide();
        IsVisible = false;
        Message = string.Empty;
        Details = null;
        _detailsAction = null;
        RaisePropertyChanged(nameof(HasDetails));
        RaisePropertyChanged(nameof(HasDetailsAction));
    }

    private void OpenDetails()
    {
        if (_detailsAction is not null)
        {
            _detailsAction.Invoke();
            return;
        }

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

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private static IBrush ResourceBrush(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out object? resource) == true && resource is IBrush brush)
            return brush;

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }
}
