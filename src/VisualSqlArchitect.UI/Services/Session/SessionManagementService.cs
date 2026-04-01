using System.IO;
using Avalonia.Controls;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Services;

/// <summary>
/// Manages auto-save scheduling and session restoration.
/// Handles debounced saves, session file cleanup, and corruption detection.
/// </summary>
public class SessionManagementService(Window window, CanvasViewModel vm)
{
    private readonly Window _window = window;
    private readonly CanvasViewModel _vm = vm;
    private readonly object _autoSaveLock = new();  // Synchronization for _autoSaveCts
    private CancellationTokenSource? _autoSaveCts;

    // Column lookup for restoring TableSource nodes from the demo catalog
    private static readonly IReadOnlyDictionary<
        string,
        IReadOnlyList<(string Name, PinDataType Type)>
    > ColumnLookup = CanvasViewModel.DemoCatalog.ToDictionary(t => t.FullName, t => t.Cols);

    private static string AppDataDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VisualSqlArchitect"
        );

    private static string SessionFile => Path.Combine(AppDataDir, "last-session.vsaq");
    private static string SessionTmp => Path.Combine(AppDataDir, "last-session.vsaq.tmp");

    public void Wire()
    {
        // Wire banner buttons
        Button? restoreBtn = _window.FindControl<Button>("RestoreSessionBtn");
        Button? dismissBtn = _window.FindControl<Button>("DismissRestoreBtn");
        if (restoreBtn is not null)
            restoreBtn.Click += async (_, _) => await RestoreSessionAsync();
        if (dismissBtn is not null)
            dismissBtn.Click += (_, _) => DismissSession();

        // Subscribe to canvas changes via IsDirty
        _vm.PropertyChanged += (_, e) =>
        {
            if (
                e.PropertyName
                is nameof(CanvasViewModel.IsDirty)
                    or nameof(CanvasViewModel.Zoom)
                    or nameof(CanvasViewModel.PanOffset)
            )
                ScheduleAutoSave();
        };

        // Force save on close (in addition to layout save)
        // Avoid async void: use fire-and-forget with proper exception handling
        _window.Closing += (_, _) =>
        {
            // Fire and forget the save operation - exceptions are caught in SaveSessionNowAsync
            _ = SaveSessionNowAsync();
        };
    }

    private void ScheduleAutoSave()
    {
        lock (_autoSaveLock)
        {
            _autoSaveCts?.Cancel();
            _autoSaveCts?.Dispose();
            _autoSaveCts = new CancellationTokenSource();
            CancellationToken token = _autoSaveCts.Token;

            Task.Delay(1500, token)
                .ContinueWith(
                    _ =>
                    {
                        if (!token.IsCancellationRequested)
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                _ = SaveSessionNowAsync()
                            );
                    },
                    TaskScheduler.Default
                );
        }
    }

    private async Task SaveSessionNowAsync()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            string json = CanvasSerializer.Serialize(_vm);
            await File.WriteAllTextAsync(SessionTmp, json);
            File.Move(SessionTmp, SessionFile, overwrite: true);
        }
        catch
        { /* best effort */
        }
    }

    public void CheckForSession()
    {
        if (!CanvasSerializer.IsValidFile(SessionFile))
            return;
        Border? banner = _window.FindControl<Border>("RestoreBanner");
        if (banner is not null)
            banner.IsVisible = true;
    }

    private async Task RestoreSessionAsync()
    {
        Border? banner = _window.FindControl<Border>("RestoreBanner");
        if (banner is not null)
            banner.IsVisible = false;

        try
        {
            CanvasLoadResult result = await CanvasSerializer.LoadFromFileAsync(
                SessionFile,
                _vm,
                ColumnLookup
            );
            if (!result.Success)
            {
                _vm.DataPreview.ShowError($"Restore failed: {result.Error}", null);
                try
                {
                    File.Delete(SessionFile);
                }
                catch
                { /* ignore */
                }
                return;
            }

            _vm.IsDirty = false;
            _window.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
            if (result.Warnings is { Count: > 0 })
                _vm.NotifyWarning(
                    "Session restored with warnings.",
                    $"{SessionFile}{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, result.Warnings)}"
                );
            else
                _vm.NotifySuccess("Session restored successfully.", SessionFile);

            // Surface migration warnings as diagnostics
            if (result.Warnings is { Count: > 0 })
                foreach (string w in result.Warnings)
                    _vm.Diagnostics.AddWarning(
                        area: "Canvas Migration",
                        message: $"Session restore: {w}",
                        recommendation: "Review diagnostics and save the canvas to persist the migrated schema.",
                        openPanel: true
                    );
        }
        catch (Exception ex)
        {
            _vm.DataPreview.ShowError($"Restore failed: {ex.Message}", ex);
            // Session corrupted — delete it
            try
            {
                File.Delete(SessionFile);
            }
            catch
            { /* ignore */
            }
        }
    }

    private void DismissSession()
    {
        Border? banner = _window.FindControl<Border>("RestoreBanner");
        if (banner is not null)
            banner.IsVisible = false;
        try
        {
            File.Delete(SessionFile);
        }
        catch
        { /* ignore */
        }
    }
}
