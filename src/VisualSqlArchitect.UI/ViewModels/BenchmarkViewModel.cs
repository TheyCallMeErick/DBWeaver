using System.Collections.ObjectModel;

namespace VisualSqlArchitect.UI.ViewModels;

// ── Single run result ─────────────────────────────────────────────────────────

public sealed record BenchmarkRunResult(
    string Label,
    int Iterations,
    double MinMs,
    double MaxMs,
    double AvgMs,
    double MedianMs,
    double P95Ms,
    DateTime RunAt
)
{
    public string Summary =>
        $"avg {AvgMs:0.0}ms  p95 {P95Ms:0.0}ms  min {MinMs:0.0}ms  max {MaxMs:0.0}ms";
}

// ── ViewModel ─────────────────────────────────────────────────────────────────

/// <summary>
/// Drives the benchmark overlay — runs the canvas SQL N times (with a warm-up
/// pass) and computes avg / median / p95 latency statistics.
/// Because the preview layer is a mock, the benchmark simulates realistic
/// random latencies derived from a configurable base delay so the metrics
/// are meaningful for UI/UX validation purposes.
/// When a real IDbOrchestrator is wired up, swap <see cref="SimulateIterationAsync"/>
/// for an actual orchestrator call.
/// </summary>
public sealed class BenchmarkViewModel : ViewModelBase
{
    private readonly CanvasViewModel _canvas;

    // ── Visibility ────────────────────────────────────────────────────────────

    private bool _isVisible;
    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    // ── Configuration ─────────────────────────────────────────────────────────

    private int _iterations = 10;
    public int Iterations
    {
        get => _iterations;
        set => Set(ref _iterations, Math.Clamp(value, 1, 100));
    }

    private int _warmupIterations = 2;
    public int WarmupIterations
    {
        get => _warmupIterations;
        set => Set(ref _warmupIterations, Math.Clamp(value, 0, 10));
    }

    private int _intervalMs = 0;
    public int IntervalMs
    {
        get => _intervalMs;
        set => Set(ref _intervalMs, Math.Clamp(value, 0, 5000));
    }

    private string _runLabel = "Run 1";
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

    // ── Constructor ───────────────────────────────────────────────────────────

    public BenchmarkViewModel(CanvasViewModel canvas)
    {
        _canvas = canvas;
        RunCommand          = new RelayCommand(() => _ = RunAsync(), () => !IsRunning);
        CancelCommand       = new RelayCommand(Cancel, () => IsRunning);
        ClearHistoryCommand = new RelayCommand(() => { History.Clear(); LatestResult = null; });
        CloseCommand        = new RelayCommand(() => IsVisible = false);
    }

    public void Open()
    {
        Sql = _canvas.LiveSql.RawSql;
        // Auto-increment label per run
        RunLabel = $"Run {History.Count + 1}";
        IsVisible = true;
    }

    // ── Run logic ─────────────────────────────────────────────────────────────

    private async Task RunAsync()
    {
        var sql = _canvas.LiveSql.RawSql;
        if (string.IsNullOrWhiteSpace(sql))
        {
            Progress = "No SQL to benchmark — build a query first.";
            return;
        }

        Sql = sql;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        IsRunning = true;
        Progress = "";
        ProgressFraction = 0;

        var latencies = new List<double>(Iterations);
        var total = WarmupIterations + Iterations;

        try
        {
            // ── Warm-up passes ────────────────────────────────────────────────
            for (int i = 0; i < WarmupIterations; i++)
            {
                ct.ThrowIfCancellationRequested();
                Progress = $"Warm-up {i + 1}/{WarmupIterations}…";
                ProgressFraction = (double)(i + 1) / total;
                await SimulateIterationAsync(ct);
                if (IntervalMs > 0) await Task.Delay(IntervalMs, ct);
            }

            // ── Measured passes ───────────────────────────────────────────────
            for (int i = 0; i < Iterations; i++)
            {
                ct.ThrowIfCancellationRequested();
                Progress = $"Iteration {i + 1}/{Iterations}…";
                ProgressFraction = (double)(WarmupIterations + i + 1) / total;

                var ms = await SimulateIterationAsync(ct);
                latencies.Add(ms);

                if (IntervalMs > 0) await Task.Delay(IntervalMs, ct);
            }

            // ── Compute statistics ────────────────────────────────────────────
            latencies.Sort();
            var result = new BenchmarkRunResult(
                Label     : RunLabel,
                Iterations: Iterations,
                MinMs     : latencies[0],
                MaxMs     : latencies[^1],
                AvgMs     : latencies.Average(),
                MedianMs  : Percentile(latencies, 0.50),
                P95Ms     : Percentile(latencies, 0.95),
                RunAt     : DateTime.Now
            );

            LatestResult = result;
            History.Insert(0, result);
            Progress = $"Done — {result.Summary}";
            RunLabel = $"Run {History.Count + 1}";
        }
        catch (OperationCanceledException)
        {
            Progress = "Benchmark cancelled.";
        }
        finally
        {
            IsRunning = false;
            ProgressFraction = 0;
        }
    }

    private void Cancel()
    {
        _cts?.Cancel();
    }

    // ── Simulation / execution ────────────────────────────────────────────────

    /// <summary>
    /// Simulates one query execution and returns elapsed milliseconds.
    /// Replace this with a real orchestrator call when a live connection is wired.
    /// The simulation produces realistic latencies with random jitter so that
    /// p95 differs meaningfully from the average.
    /// </summary>
    private static readonly Random _rng = new();

    private static async Task<double> SimulateIterationAsync(CancellationToken ct)
    {
        // Base latency 30–80ms with occasional spikes (10 % chance of 200–600ms)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int delay = _rng.NextDouble() < 0.10
            ? _rng.Next(200, 600)
            : _rng.Next(30, 80);
        await Task.Delay(delay, ct);
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    // ── Statistics ────────────────────────────────────────────────────────────

    private static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        if (sorted.Count == 1) return sorted[0];
        double idx = p * (sorted.Count - 1);
        int lo = (int)idx;
        int hi = Math.Min(lo + 1, sorted.Count - 1);
        double frac = idx - lo;
        return sorted[lo] + frac * (sorted[hi] - sorted[lo]);
    }
}
