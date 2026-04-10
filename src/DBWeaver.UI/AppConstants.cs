namespace DBWeaver.UI;

/// <summary>
/// Application-wide constants and default values.
/// Centralises configuration that was previously spread across multiple view models and services.
/// </summary>
public static class AppConstants
{
    // ── App ──────────────────────────────────────────────────────────────────

    /// <summary>Internal app name used in storage paths.</summary>
    public const string AppName = "DBWeaver";

    /// <summary>Display name shown in window titles and UI labels.</summary>
    public const string AppDisplayName = "DBWeaver";

    /// <summary>Base directory under AppData for local app persistence.</summary>
    public static string AppDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppName
    );

    /// <summary>Application version embedded in saved canvas files.</summary>
    public const string AppVersion = "1.0.0";

    // ── Connection defaults ───────────────────────────────────────────────────

    /// <summary>Default hostname shown in new and reset connection profiles.</summary>
    public const string DefaultHost = "localhost";

    /// <summary>Seconds between background connection health-check pings.</summary>
    public const int HealthCheckIntervalSeconds = 60;

    // ── SQL import ────────────────────────────────────────────────────────────

    /// <summary>Maximum characters accepted in the SQL import text box.</summary>
    public const int DefaultMaxSqlInputLength = 50_000;

    /// <summary>Default timeout for a single SQL import operation.</summary>
    public static readonly TimeSpan DefaultImportTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Milliseconds to yield to the UI thread before starting heavy SQL import work,
    /// allowing the "Importing…" indicator to render before the CPU-bound parse begins.
    /// </summary>
    public const int DefaultImportStartDelayMs = 80;

    // ── UI timing ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Milliseconds to wait after the last query-graph change before triggering a
    /// live SQL preview refresh (debounce window).
    /// </summary>
    public const int PreviewDebounceMs = 500;

    /// <summary>Milliseconds debounce before re-running canvas validation.</summary>
    public const int ValidationDebounceMs = 200;
}
