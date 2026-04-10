using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DBWeaver.UI.Services.Benchmark;

namespace DBWeaver.UI.ViewModels;

// ── ViewModel ─────────────────────────────────────────────────────────────────

/// <summary>
/// Drives the benchmark overlay — runs the canvas SQL N times (with a warm-up
/// pass) and computes avg / median / p95 latency statistics.
/// Uses real DB execution latency when a connection is active; otherwise
/// falls back to simulated latency samples for offline/demo scenarios.
/// </summary>
public sealed class BenchmarkViewModel : ViewModelBase
{
    private readonly CanvasViewModel _canvas;
    private readonly ILogger<BenchmarkViewModel> _logger;
    private readonly IBenchmarkExecutionService _executionService;
    private readonly IBenchmarkTextProvider _textProvider;
    private readonly IBenchmarkProgressPresenter _progressPresenter;
    private readonly IBenchmarkRunStateCoordinator _runStateCoordinator;
    private readonly IBenchmarkRunContextFactory _runContextFactory;
    private readonly IBenchmarkResultCoordinator _resultCoordinator;
    private readonly IBenchmarkInitializationService _initializationService;

    // ── Visibility ────────────────────────────────────────────────────────────

    private bool _isVisible;
    private int _openRequestToken;
    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }
    public int OpenRequestToken
    {
        get => _openRequestToken;
        private set => Set(ref _openRequestToken, value);
    }

    // ── Configuration ─────────────────────────────────────────────────────────

    private int _iterations;
    public int Iterations
    {
        get => _iterations;
        set => Set(ref _iterations, BenchmarkRunConfiguration.NormalizeIterations(value));
    }

    private int _warmupIterations;
    public int WarmupIterations
    {
        get => _warmupIterations;
        set => Set(ref _warmupIterations, BenchmarkRunConfiguration.NormalizeWarmupIterations(value));
    }

    private int _intervalMs;
    public int IntervalMs
    {
        get => _intervalMs;
        set => Set(ref _intervalMs, BenchmarkRunConfiguration.NormalizeIntervalMs(value));
    }

    private string _runLabel = string.Empty;
    public string RunLabel
    {
        get => _runLabel;
        set => Set(ref _runLabel, value);
    }

    // ── Run state ─────────────────────────────────────────────────────────────

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            Set(ref _isRunning, value);
            RunCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    private string _progress = "";
    public string Progress
    {
        get => _progress;
        set => Set(ref _progress, value);
    }

    private double _progressFraction;
    public double ProgressFraction
    {
        get => _progressFraction;
        set => Set(ref _progressFraction, value);
    }

    // ── Latest result ─────────────────────────────────────────────────────────

    private BenchmarkRunResult? _latestResult;
    public BenchmarkRunResult? LatestResult
    {
        get => _latestResult;
        private set
        {
            Set(ref _latestResult, value);
            RaisePropertyChanged(nameof(HasResult));
        }
    }

    public bool HasResult => _latestResult is not null;

    // ── History ───────────────────────────────────────────────────────────────

    public ObservableCollection<BenchmarkRunResult> History { get; } = [];

    // ── SQL being benchmarked ─────────────────────────────────────────────────

    private string _sql = "";
    public string Sql
    {
        get => _sql;
        private set => Set(ref _sql, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public RelayCommand RunCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ClearHistoryCommand { get; }
    public RelayCommand CloseCommand { get; }

    private CancellationTokenSource? _cts;
    private string _activeRunSqlSnapshot = string.Empty;

    // ── Constructor ───────────────────────────────────────────────────────────

    public BenchmarkViewModel(
        CanvasViewModel canvas,
        IBenchmarkIterationExecutor? iterationExecutor = null,
        IBenchmarkConfigurationProvider? configurationProvider = null,
        IBenchmarkRunner? benchmarkRunner = null,
        IBenchmarkExecutionService? executionService = null,
        IBenchmarkTextProvider? textProvider = null,
        IBenchmarkProgressPresenter? progressPresenter = null,
        IBenchmarkRunStateCoordinator? runStateCoordinator = null,
        IBenchmarkRunContextFactory? runContextFactory = null,
        IBenchmarkResultCoordinator? resultCoordinator = null,
        IBenchmarkInitializationService? initializationService = null,
        ILogger<BenchmarkViewModel>? logger = null)
    {
        _canvas = canvas;
        IBenchmarkIterationExecutor resolvedIterationExecutor = iterationExecutor
            ?? new AdaptiveBenchmarkIterationExecutor(
                connectionResolver: () => _canvas.ActiveConnectionConfig,
                sqlResolver: () => string.IsNullOrWhiteSpace(_activeRunSqlSnapshot)
                    ? _canvas.LiveSql.RawSql
                    : _activeRunSqlSnapshot);
        IBenchmarkConfigurationProvider resolvedConfigurationProvider = configurationProvider ?? new EnvironmentBenchmarkConfigurationProvider();
        IBenchmarkRunner resolvedBenchmarkRunner = benchmarkRunner ?? new BenchmarkRunner(resolvedIterationExecutor);
        _executionService = executionService ?? new BenchmarkExecutionService(resolvedBenchmarkRunner);
        _textProvider = textProvider ?? new LocalizedBenchmarkTextProvider();
        _progressPresenter = progressPresenter ?? new BenchmarkProgressPresenter(_textProvider);
        _runStateCoordinator = runStateCoordinator ?? new BenchmarkRunStateCoordinator(_textProvider);
        _runContextFactory = runContextFactory ?? new BenchmarkRunContextFactory(_textProvider);
        _resultCoordinator = resultCoordinator ?? new BenchmarkResultCoordinator(_textProvider);
        _initializationService = initializationService
            ?? new BenchmarkInitializationService(resolvedConfigurationProvider, _textProvider);
        _logger = logger ?? NullLogger<BenchmarkViewModel>.Instance;

        BenchmarkInitialState initial = _initializationService.BuildInitialState();
        _iterations = initial.Iterations;
        _warmupIterations = initial.WarmupIterations;
        _intervalMs = initial.IntervalMs;
        _runLabel = initial.RunLabel;

        BenchmarkCommandBindings commands = BenchmarkCommandFactory.Create(
            startRun: StartRunSafe,
            canRun: () => !IsRunning,
            cancel: Cancel,
            canCancel: () => IsRunning,
            clearHistory: () => { History.Clear(); LatestResult = null; },
            close: () => IsVisible = false
        );
        RunCommand = commands.RunCommand;
        CancelCommand = commands.CancelCommand;
        ClearHistoryCommand = commands.ClearHistoryCommand;
        CloseCommand = commands.CloseCommand;
    }

    private void StartRunSafe() => _ = RunAsyncSafe();

    private async Task RunAsyncSafe()
    {
        try
        {
            await RunAsync();
        }
        catch (Exception ex)
        {
            Progress = _runStateCoordinator.BuildFailureMessage(ex.Message);
            BenchmarkRunUiState finished = _runStateCoordinator.BuildFinishState(Progress);
            IsRunning = finished.IsRunning;
            Progress = finished.Progress;
            ProgressFraction = finished.ProgressFraction;
            _logger.LogError(ex, "Unhandled exception in fire-and-forget benchmark run");
        }
    }

    public void Open()
    {
        Sql = _canvas.LiveSql.RawSql;
        // Auto-increment label per run
        RunLabel = _textProvider.BuildRunLabel(History.Count + 1);
        IsVisible = true;
        OpenRequestToken++;
    }

    // ── Run logic ─────────────────────────────────────────────────────────────

    private async Task RunAsync()
    {
        BenchmarkRunContextCreationResult creation = _runContextFactory.TryCreate(
            _canvas.LiveSql.RawSql,
            Iterations,
            WarmupIterations,
            IntervalMs);
        if (!creation.CanStart)
        {
            Progress = creation.RejectionMessage ?? string.Empty;
            return;
        }

        BenchmarkRunContext context = creation.Context!.Value;
        _activeRunSqlSnapshot = context.Sql;
        Sql = context.Sql;
        using CancellationTokenSource cts = context.CancellationTokenSource;
        _cts = cts;
        CancellationToken ct = cts.Token;

        BenchmarkRunUiState started = _runStateCoordinator.BuildStartState();
        IsRunning = started.IsRunning;
        Progress = started.Progress;
        ProgressFraction = started.ProgressFraction;

        try
        {
            BenchmarkRunResult result = await _executionService.ExecuteAsync(
                RunLabel,
                context.Configuration,
                onProgress: OnRunProgress,
                cancellationToken: ct);

            BenchmarkResultApplicationState success = _resultCoordinator.ApplySuccess(History, result);
            LatestResult = success.LatestResult;
            Progress = success.Progress;
            RunLabel = success.NextRunLabel;
        }
        catch (OperationCanceledException)
        {
            Progress = _runStateCoordinator.BuildCancelledMessage();
        }
        finally
        {
            _cts = null;
            _activeRunSqlSnapshot = string.Empty;
            BenchmarkRunUiState finished = _runStateCoordinator.BuildFinishState(Progress);
            IsRunning = finished.IsRunning;
            Progress = finished.Progress;
            ProgressFraction = finished.ProgressFraction;
        }
    }

    private void Cancel()
    {
        _cts?.Cancel();
    }

    private void OnRunProgress(BenchmarkRunProgress progress)
    {
        BenchmarkProgressViewState viewState = _progressPresenter.Build(progress, WarmupIterations, Iterations);
        Progress = viewState.Message;
        ProgressFraction = viewState.Fraction;
    }

}
