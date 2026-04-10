using Avalonia.Controls;
using DBWeaver.UI.Controls;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;
using System.ComponentModel;

namespace DBWeaver.UI.Controls.Shell;

public partial class DiagramDocumentPageControl : UserControl
{
    private const int BaseOverlayZIndex = 999;
    private int _nextOverlayZIndex = BaseOverlayZIndex;
    private Panel? _benchmarkOverlayHost;
    private Panel? _explainOverlayHost;
    private CanvasViewModel? _viewModel;
    private BenchmarkViewModel? _benchmarkViewModel;
    private ExplainPlanViewModel? _explainPlanViewModel;

    public DiagramDocumentPageControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        _benchmarkOverlayHost = this.FindControl<Panel>("BenchmarkOverlayHost");
        _explainOverlayHost = this.FindControl<Panel>("ExplainOverlayHost");

        if (_benchmarkOverlayHost is not null)
            _benchmarkOverlayHost.PointerPressed += (_, _) => BringOverlayToFront(_benchmarkOverlayHost);

        if (_explainOverlayHost is not null)
            _explainOverlayHost.PointerPressed += (_, _) => BringOverlayToFront(_explainOverlayHost);
    }

    public InfiniteCanvas? CanvasControl => this.FindControl<InfiniteCanvas>("TheCanvas");

    public SearchMenuControl? SearchOverlayControl => this.FindControl<SearchMenuControl>("SearchOverlay");

    public void InvalidateCanvasWires()
    {
        CanvasControl?.InvalidateWires();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        if (_benchmarkViewModel is not null)
            _benchmarkViewModel.PropertyChanged -= OnBenchmarkPropertyChanged;
        if (_explainPlanViewModel is not null)
            _explainPlanViewModel.PropertyChanged -= OnExplainPlanPropertyChanged;

        _viewModel = DataContext as CanvasViewModel;
        if (_viewModel is null)
            return;

        _benchmarkViewModel = _viewModel.Benchmark;
        _explainPlanViewModel = _viewModel.ExplainPlan;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _benchmarkViewModel.PropertyChanged += OnBenchmarkPropertyChanged;
        _explainPlanViewModel.PropertyChanged += OnExplainPlanPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
            return;

        if (e.PropertyName == nameof(CanvasViewModel.Benchmark) && _benchmarkOverlayHost is not null && _viewModel.Benchmark.IsVisible)
            BringOverlayToFront(_benchmarkOverlayHost);

        if (e.PropertyName == nameof(CanvasViewModel.ExplainPlan) && _explainOverlayHost is not null && _viewModel.ExplainPlan.IsVisible)
            BringOverlayToFront(_explainOverlayHost);
    }

    private void OnBenchmarkPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_benchmarkOverlayHost is null || _benchmarkViewModel is null)
            return;

        if ((e.PropertyName == nameof(BenchmarkViewModel.IsVisible) || e.PropertyName == nameof(BenchmarkViewModel.OpenRequestToken))
            && _benchmarkViewModel.IsVisible)
        {
            BringOverlayToFront(_benchmarkOverlayHost);
        }
    }

    private void OnExplainPlanPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_explainOverlayHost is null || _explainPlanViewModel is null)
            return;

        if ((e.PropertyName == nameof(ExplainPlanViewModel.IsVisible) || e.PropertyName == nameof(ExplainPlanViewModel.OpenRequestToken))
            && _explainPlanViewModel.IsVisible)
        {
            BringOverlayToFront(_explainOverlayHost);
        }
    }

    private void BringOverlayToFront(Panel? overlayHost)
    {
        if (overlayHost is null)
            return;

        _nextOverlayZIndex++;
        overlayHost.ZIndex = _nextOverlayZIndex;
    }
}
