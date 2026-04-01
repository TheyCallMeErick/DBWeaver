using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.ViewModels;
using System.ComponentModel;

namespace VisualSqlArchitect.UI.Services;

/// <summary>
/// Manages data preview pane wiring and query execution.
/// Supports real async cancellation via CancellationTokenSource,
/// live elapsed-time feedback, and clean state transitions.
/// </summary>
public class PreviewService(Window window, CanvasViewModel vm, ILogger<PreviewService>? logger = null) : IDisposable
{
    private readonly Window _window = window;
    private readonly CanvasViewModel _vm = vm;
    private readonly QueryExecutorService _queryExecutor = new();
    private readonly ILogger<PreviewService> _logger = logger ?? NullLogger<PreviewService>.Instance;

    // Active run — cancelled when user clicks Cancel or a new run starts
    private CancellationTokenSource? _runCts;

    // Event handlers stored for proper cleanup (prevent memory leaks)
    private PropertyChangedEventHandler? _canvasPropertyChangedHandler;
    private PropertyChangedEventHandler? _liveSqlPropertyChangedHandler;
    private bool _disposed = false;

    // ── Wiring ────────────────────────────────────────────────────────────────

    public void Wire()
    {
        ThrowIfDisposed();

        Console.WriteLine("[PreviewService] Wire() called - scheduling control lookup with delay");
        _logger.LogDebug("Wire() called");

        // Clean up previous subscriptions if Wire is called again
        UnsubscribeFromPropertyChangedEvents();

        // Schedule the wiring to happen after layout is updated
        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            // Wait for layout to complete
            await Task.Delay(AppConstants.PreviewDebounceMs);

            Console.WriteLine("[PreviewService] Looking for DataPreviewPanel first...");
            // First, find the PreviewPanel (UserControl)
            var previewPanel = _window.FindControl<Control>("PreviewPanel");
            Console.WriteLine($"[PreviewService] Found PreviewPanel: {previewPanel is not null}");

            Button? run = null;
            Button? cancel = null;
            Button? cls = null;

            if (previewPanel is not null)
            {
                Console.WriteLine("[PreviewService] Searching for buttons within PreviewPanel...");
                // Search for buttons within the panel
                run = previewPanel.FindControl<Button>("RunButton");
                cancel = previewPanel.FindControl<Button>("CancelButton");
                cls = previewPanel.FindControl<Button>("CloseButton");
            }
            else
            {
                Console.WriteLine("[PreviewService] PreviewPanel not found, trying direct window search...");
                // Fallback: search from window
                run = _window.FindControl<Button>("RunButton");
                cancel = _window.FindControl<Button>("CancelButton");
                cls = _window.FindControl<Button>("CloseButton");
            }

            Console.WriteLine($"[PreviewService] Found buttons: run={run is not null}, cancel={cancel is not null}, close={cls is not null}");
            _logger.LogDebug(
                "Found buttons: run={RunFound}, cancel={CancelFound}, close={CloseFound}",
                run is not null,
                cancel is not null,
                cls is not null
            );

            if (run is not null)
            {
                Console.WriteLine("[PreviewService] Wiring RunButton click event");
                run.Click += async (_, _) =>
                {
                    Console.WriteLine("[PreviewService] >>> RunButton CLICKED! <<<");
                    _logger.LogDebug("RunButton clicked");
                    try
                    {
                        Console.WriteLine("[PreviewService] Starting RunPreviewAsync...");
                        await RunPreviewAsync();
                        Console.WriteLine("[PreviewService] RunPreviewAsync completed");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PreviewService] RunPreviewAsync threw exception: {ex}");
                    }
                };
                UpdateRunEnabled(run);

                // Store handlers for proper disposal
                _canvasPropertyChangedHandler = (_, e) =>
                {
                    if (e.PropertyName == nameof(CanvasViewModel.HasErrors))
                        UpdateRunEnabled(run);
                };
                _liveSqlPropertyChangedHandler = (_, e) =>
                {
                    if (e.PropertyName == nameof(LiveSqlBarViewModel.IsMutatingCommand))
                        UpdateRunEnabled(run);
                };

                _vm.PropertyChanged += _canvasPropertyChangedHandler;
                _vm.LiveSql.PropertyChanged += _liveSqlPropertyChangedHandler;
            }

            if (cancel is not null)
                cancel.Click += (_, _) => CancelRun();

            if (cls is not null)
                cls.Click += (_, _) => _vm.DataPreview.IsVisible = false;
        });
    }

    /// <summary>
    /// Unsubscribe from PropertyChanged events to prevent accumulation
    /// </summary>
    private void UnsubscribeFromPropertyChangedEvents()
    {
        if (_canvasPropertyChangedHandler != null)
        {
            _vm.PropertyChanged -= _canvasPropertyChangedHandler;
            _canvasPropertyChangedHandler = null;
        }

        if (_liveSqlPropertyChangedHandler != null)
        {
            _vm.LiveSql.PropertyChanged -= _liveSqlPropertyChangedHandler;
            _liveSqlPropertyChangedHandler = null;
        }
    }

    private void UpdateRunEnabled(Button run)
    {
        bool hasErrors = _vm.HasErrors;
        bool isMutating = _vm.LiveSql.IsMutatingCommand;
        // Only disable if it's a mutating command - canvas errors shouldn't block query execution
        bool shouldEnable = !isMutating;
        run.IsEnabled = shouldEnable;
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    private void CancelRun()
    {
        _runCts?.Cancel();
    }

    // ── Run ───────────────────────────────────────────────────────────────────

    public async Task RunPreviewAsync()
    {
        Console.WriteLine("\n>>> [RunPreviewAsync] STARTED <<<");
        _logger.LogDebug("RunPreviewAsync called");

        // Check if there are canvas errors and warn about them
        Console.WriteLine($"[RunPreviewAsync] Canvas HasErrors={_vm.HasErrors}");
        if (_vm.HasErrors)
        {
            Console.WriteLine("[RunPreviewAsync] ⚠️ AVISO: Canvas has validation errors - execution may have unexpected behavior");
        }

        // Safe-preview guard
        Console.WriteLine($"[RunPreviewAsync] IsMutatingCommand={_vm.LiveSql.IsMutatingCommand}");
        if (_vm.LiveSql.IsMutatingCommand)
        {
            Console.WriteLine("[RunPreviewAsync] Query is mutating, showing error");
            _logger.LogInformation("Blocked mutating query in preview mode");
            _vm.DataPreview.ShowError(
                "Safe Preview Mode: data-mutating commands (INSERT/UPDATE/DELETE/DROP/ALTER/TRUNCATE) cannot be executed in preview."
            );
            return;
        }

        // Check if connection is available
        Console.WriteLine($"[RunPreviewAsync] ActiveConnectionConfig={(_vm.ActiveConnectionConfig is not null ? "EXISTS" : "NULL")}");
        if (_vm.ActiveConnectionConfig == null)
        {
            Console.WriteLine("[RunPreviewAsync] No active connection config - showing error");
            _logger.LogWarning("No active connection config");
            _vm.DataPreview.ShowError(
                "No active database connection. Please connect to a database first."
            );
            return;
        }

        Console.WriteLine($"[RunPreviewAsync] Using connection: {_vm.ActiveConnectionConfig.Provider} @ {_vm.ActiveConnectionConfig.Host}:{_vm.ActiveConnectionConfig.Port}/{_vm.ActiveConnectionConfig.Database}");
        _logger.LogInformation(
            "Using connection {Provider} @ {Host}:{Port}/{Database}",
            _vm.ActiveConnectionConfig.Provider,
            _vm.ActiveConnectionConfig.Host,
            _vm.ActiveConnectionConfig.Port,
            _vm.ActiveConnectionConfig.Database
        );

        // Cancel any in-flight run before starting a new one
        _runCts?.Cancel();
        _runCts?.Dispose();
        _runCts = new CancellationTokenSource();
        var ct = _runCts.Token;

        string sql = string.IsNullOrWhiteSpace(_vm.LiveSql.RawSql)
            ? (string.IsNullOrWhiteSpace(_vm.QueryText) ? "SELECT 1 AS test" : _vm.QueryText)
            : _vm.LiveSql.RawSql;

        Console.WriteLine($"[RunPreviewAsync] SQL Query: {sql}");
        _logger.LogDebug("SQL query for preview: {Query}", sql);

        // Log guardrail warnings (non-blocking)
        foreach (GuardIssue g in _vm.LiveSql.GuardIssues)
            _logger.LogWarning("Guardrail {Code}: {Message}", g.Code, g.Message);

        Console.WriteLine("[RunPreviewAsync] Calling ShowLoading...");
        _vm.DataPreview.ShowLoading(sql);

        // Start elapsed-time ticker (updates status chip every 100ms)
        var sw = Stopwatch.StartNew();
        using var ticker = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        var tickTask = TickElapsedAsync(ticker, sw, ct);

        try
        {
            Console.WriteLine($"[RunPreviewAsync] Calling ExecuteQueryAsync with SQL length={sql.Length}");
            _logger.LogDebug("Starting query execution");

            // Execute query against the connected database
            var dt = await _queryExecutor.ExecuteQueryAsync(
                _vm.ActiveConnectionConfig,
                sql,
                maxRows: 1000,
                ct: ct
            );

            sw.Stop();
            ct.ThrowIfCancellationRequested();

            Console.WriteLine($"[RunPreviewAsync] Query SUCCESS! Got {dt.Rows.Count} rows in {sw.ElapsedMilliseconds}ms");
            _logger.LogInformation("Query completed successfully. Rows: {RowCount}", dt.Rows.Count);

            Console.WriteLine("[RunPreviewAsync] Calling ShowResults...");
            _vm.DataPreview.ShowResults(dt, sw.ElapsedMilliseconds);
            Console.WriteLine("[RunPreviewAsync] ShowResults completed");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[RunPreviewAsync] Query was cancelled");
            _logger.LogInformation("Query was cancelled");
            sw.Stop();
            _vm.DataPreview.ShowCancelled();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RunPreviewAsync] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[RunPreviewAsync] Stack: {ex.StackTrace}");
            _logger.LogError(ex, "Error executing query: {Message}", ex.Message);
            sw.Stop();
            _vm.DataPreview.ShowError(ex.Message, ex);
        }
        finally
        {
            Console.WriteLine("[RunPreviewAsync] Finally block - cleaning up ticker");
            await tickTask; // let ticker finish cleanly
            Console.WriteLine("[RunPreviewAsync] FINISHED\\n");
        }
    }

    // ── Elapsed ticker ────────────────────────────────────────────────────────

    private async Task TickElapsedAsync(PeriodicTimer timer, Stopwatch sw, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
            {
                var ms = sw.ElapsedMilliseconds;
                await Dispatcher.UIThread.InvokeAsync(() =>
                    _vm.DataPreview.UpdateElapsed(ms));
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Disposes the PreviewService and releases event subscriptions.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Clean up event subscriptions
        UnsubscribeFromPropertyChangedEvents();

        // Cancel any running query
        _runCts?.Cancel();
        _runCts?.Dispose();
        _runCts = null;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Throws ObjectDisposedException if the service has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PreviewService));
    }
}

