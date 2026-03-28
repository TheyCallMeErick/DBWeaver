using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Services;

/// <summary>
/// Manages data preview pane wiring and query execution.
/// Supports real async cancellation via CancellationTokenSource,
/// live elapsed-time feedback, and clean state transitions.
/// </summary>
public class PreviewService(Window window, CanvasViewModel vm)
{
    private readonly Window _window = window;
    private readonly CanvasViewModel _vm = vm;
    private readonly QueryExecutorService _queryExecutor = new();

    // Active run — cancelled when user clicks Cancel or a new run starts
    private CancellationTokenSource? _runCts;

    // ── Wiring ────────────────────────────────────────────────────────────────

    public void Wire()
    {
        Console.WriteLine("[PreviewService] Wire() called - scheduling control lookup with delay");
        Debug.WriteLine("[PreviewService] Wire() called");

        // Schedule the wiring to happen after layout is updated
        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            // Wait for layout to complete
            await Task.Delay(500);

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
            Debug.WriteLine($"[PreviewService] Found buttons: run={run is not null}, cancel={cancel is not null}, close={cls is not null}");

            if (run is not null)
            {
                Console.WriteLine("[PreviewService] Wiring RunButton click event");
                run.Click += async (_, _) =>
                {
                    Console.WriteLine("[PreviewService] >>> RunButton CLICKED! <<<");
                    Debug.WriteLine("[PreviewService] RunButton clicked!");
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

                _vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(CanvasViewModel.HasErrors))
                        UpdateRunEnabled(run);
                };
                _vm.LiveSql.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(LiveSqlBarViewModel.IsMutatingCommand))
                        UpdateRunEnabled(run);
                };
            }

            if (cancel is not null)
                cancel.Click += (_, _) => CancelRun();

            if (cls is not null)
                cls.Click += (_, _) => _vm.DataPreview.IsVisible = false;
        });
    }

    private void UpdateRunEnabled(Button run)
    {
        bool hasErrors = _vm.HasErrors;
        bool isMutating = _vm.LiveSql.IsMutatingCommand;
        // Only disable if it's a mutating command - canvas errors shouldn't block query execution
        bool shouldEnable = !isMutating;
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
        Debug.WriteLine("[PreviewService] RunPreviewAsync called");

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
            Debug.WriteLine("[PreviewService] Query is mutating, showing error");
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
            Debug.WriteLine("[PreviewService] No active connection config");
            _vm.DataPreview.ShowError(
                "No active database connection. Please connect to a database first."
            );
            return;
        }

        Console.WriteLine($"[RunPreviewAsync] Using connection: {_vm.ActiveConnectionConfig.Provider} @ {_vm.ActiveConnectionConfig.Host}:{_vm.ActiveConnectionConfig.Port}/{_vm.ActiveConnectionConfig.Database}");
        Debug.WriteLine($"[PreviewService] Using connection: {_vm.ActiveConnectionConfig.Provider} @ {_vm.ActiveConnectionConfig.Host}:{_vm.ActiveConnectionConfig.Port}/{_vm.ActiveConnectionConfig.Database}");

        // Cancel any in-flight run before starting a new one
        _runCts?.Cancel();
        _runCts?.Dispose();
        _runCts = new CancellationTokenSource();
        var ct = _runCts.Token;

        string sql = string.IsNullOrEmpty(_vm.LiveSql.RawSql)
            ? (string.IsNullOrEmpty(_vm.QueryText) ? "SELECT 1 AS test" : _vm.QueryText)
            : _vm.LiveSql.RawSql;

        Console.WriteLine($"[RunPreviewAsync] SQL Query: {sql}");
        Debug.WriteLine($"[PreviewService] SQL Query: {sql}");

        // Log guardrail warnings (non-blocking)
        foreach (GuardIssue g in _vm.LiveSql.GuardIssues)
            Debug.WriteLine($"[GUARDRAIL] {g.Code}: {g.Message}");

        Console.WriteLine("[RunPreviewAsync] Calling ShowLoading...");
        _vm.DataPreview.ShowLoading(sql);

        // Start elapsed-time ticker (updates status chip every 100ms)
        var sw = Stopwatch.StartNew();
        using var ticker = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        var tickTask = TickElapsedAsync(ticker, sw, ct);

        try
        {
            Console.WriteLine($"[RunPreviewAsync] Calling ExecuteQueryAsync with SQL length={sql.Length}");
            Debug.WriteLine("[PreviewService] Starting query execution");

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
            Debug.WriteLine($"[PreviewService] Query completed successfully. Rows: {dt.Rows.Count}");

            Console.WriteLine("[RunPreviewAsync] Calling ShowResults...");
            _vm.DataPreview.ShowResults(dt, sw.ElapsedMilliseconds);
            Console.WriteLine("[RunPreviewAsync] ShowResults completed");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[RunPreviewAsync] Query was cancelled");
            Debug.WriteLine("[PreviewService] Query was cancelled");
            sw.Stop();
            _vm.DataPreview.ShowCancelled();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RunPreviewAsync] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[RunPreviewAsync] Stack: {ex.StackTrace}");
            Debug.WriteLine($"[PreviewService] Error executing query: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"[PreviewService] Stack trace: {ex.StackTrace}");
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
}

