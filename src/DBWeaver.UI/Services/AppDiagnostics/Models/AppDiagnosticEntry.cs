using Material.Icons;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.Services.AppDiagnostics.Models;

public sealed class AppDiagnosticEntry : ViewModelBase
{
    private DiagnosticStatus _status;
    private string _details = "";
    private DateTime _lastCheckAt = DateTime.MinValue;

    public string Name { get; init; } = "";
    public string Recommendation { get; init; } = "";
    public string? Code { get; init; }
    public string? Location { get; init; }
    public string? NodeId { get; init; }
    public string? PinName { get; init; }
    public RelayCommand? FocusCommand { get; init; }

    public DiagnosticStatus Status
    {
        get => _status;
        set
        {
            Set(ref _status, value);
            RaisePropertyChanged(nameof(StatusIcon));
            RaisePropertyChanged(nameof(StatusColor));
        }
    }

    public string Details
    {
        get => _details;
        set => Set(ref _details, value);
    }

    public DateTime LastCheckAt
    {
        get => _lastCheckAt;
        set
        {
            Set(ref _lastCheckAt, value);
            RaisePropertyChanged(nameof(LastCheckLabel));
        }
    }

    public string StatusIcon =>
        Status switch
        {
            DiagnosticStatus.Ok => "✓",
            DiagnosticStatus.Warning => "⚠",
            DiagnosticStatus.Error => "✕",
            _ => "?",
        };

    public MaterialIconKind StatusIconKind =>
        Status switch
        {
            DiagnosticStatus.Ok => MaterialIconKind.CheckCircle,
            DiagnosticStatus.Warning => MaterialIconKind.Alert,
            DiagnosticStatus.Error => MaterialIconKind.CloseCircle,
            _ => MaterialIconKind.HelpCircle,
        };

    public string StatusColor =>
        Status switch
        {
            DiagnosticStatus.Ok => UiColorConstants.C_4ADE80,
            DiagnosticStatus.Warning => UiColorConstants.C_FBBF24,
            DiagnosticStatus.Error => UiColorConstants.C_EF4444,
            _ => UiColorConstants.C_4A5568,
        };

    public string LastCheckLabel =>
        _lastCheckAt == DateTime.MinValue ? "—" : _lastCheckAt.ToString("HH:mm:ss");

    public bool HasCode => !string.IsNullOrWhiteSpace(Code);
    public bool HasLocation => !string.IsNullOrWhiteSpace(Location);
    public bool HasRecommendation => !string.IsNullOrWhiteSpace(Recommendation);
    public bool HasNodeReference => !string.IsNullOrWhiteSpace(NodeId);
    public bool HasFocusAction => FocusCommand is not null && HasNodeReference;
}
