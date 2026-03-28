using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Providers;
using VisualSqlArchitect.UI.Services;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.ViewModels;

// ── Persisted connection profile ───────────────────────────────────────────────

public sealed class ConnectionProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Connection";
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Postgres;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseIntegratedSecurity { get; set; } = false;
    public int TimeoutSeconds { get; set; } = 30;

    public static int DefaultPort(DatabaseProvider p) => p switch
    {
        DatabaseProvider.Postgres  => 5432,
        DatabaseProvider.MySql     => 3306,
        DatabaseProvider.SqlServer => 1433,
        _                          => 5432,
    };

    public ConnectionConfig ToConnectionConfig() =>
        new(Provider, Host, Port, Database, Username, Password, UseIntegratedSecurity, TimeoutSeconds);
}

// ── Health status ─────────────────────────────────────────────────────────────

public enum ConnectionHealthStatus { Unknown, Online, Degraded, Offline }

// ── ViewModel ─────────────────────────────────────────────────────────────────

public sealed class ConnectionManagerViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    // Latency above this threshold is considered "Degraded" rather than "Online"
    private const double DegradedLatencyThresholdMs = 500.0;
    // How often the background health monitor pings the active connection (seconds)
    private const int HealthCheckIntervalSeconds = 60;

    // ── Services ───────────────────────────────────────────────────────────────

    private readonly DatabaseConnectionService _dbConnectionService;
    private readonly ILogger<ConnectionManagerViewModel> _logger;

    /// <summary>
    /// Reference to the canvas search menu where database tables will be loaded.
    /// Set by the CanvasViewModel after initialization.
    /// </summary>
    public SearchMenuViewModel? SearchMenu { get; set; }

    /// <summary>
    /// Reference to the canvas view model to reset/update when connecting to a new database.
    /// Set by the CanvasViewModel after initialization.
    /// </summary>
    public CanvasViewModel? Canvas { get; set; }

    // ── Visibility ────────────────────────────────────────────────────────────

    private bool _isVisible;
    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    // ── Profile list ──────────────────────────────────────────────────────────

    public ObservableCollection<ConnectionProfile> Profiles { get; } = [];

    private ConnectionProfile? _selectedProfile;
    public ConnectionProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            Set(ref _selectedProfile, value);
            if (value is not null) LoadProfileIntoForm(value);
            DeleteProfileCommand.NotifyCanExecuteChanged();
            ConnectCommand.NotifyCanExecuteChanged();
        }
    }

    // ── Active connection & health ────────────────────────────────────────────

    private string? _activeProfileId;
    public string? ActiveProfileId
    {
        get => _activeProfileId;
        set
        {
            Set(ref _activeProfileId, value);
            RaisePropertyChanged(nameof(ActiveConnectionLabel));
            ActiveHealthStatus = value is null
                ? ConnectionHealthStatus.Unknown
                : ConnectionHealthStatus.Online;
            RestartHealthMonitor();
        }
    }

    private bool _isConnecting;
    public bool IsConnecting
    {
        get => _isConnecting;
        set => Set(ref _isConnecting, value);
    }

    /// <summary>
    /// True when NOT connecting (for UI bindings that need the inverse).
    /// </summary>
    public bool IsNotConnecting => !IsConnecting;

    public string ActiveConnectionLabel
    {
        get
        {
            var p = Profiles.FirstOrDefault(x => x.Id == _activeProfileId);
            return p is null ? "No connection" : $"{p.Provider} · {p.Database}";
        }
    }

    private ConnectionHealthStatus _activeHealthStatus = ConnectionHealthStatus.Unknown;
    public ConnectionHealthStatus ActiveHealthStatus
    {
        get => _activeHealthStatus;
        private set
        {
            Set(ref _activeHealthStatus, value);
            RaisePropertyChanged(nameof(ConnectionIndicatorColor));
            RaisePropertyChanged(nameof(ConnectionHealthLabel));
            RaisePropertyChanged(nameof(ConnectionHealthTooltip));
        }
    }

    public string ConnectionIndicatorColor => _activeHealthStatus switch
    {
        ConnectionHealthStatus.Online   => "#4ADE80",
        ConnectionHealthStatus.Degraded => "#FBBF24",
        ConnectionHealthStatus.Offline  => "#EF4444",
        _                               => "#4A5568",
    };

    public string ConnectionHealthLabel => _activeHealthStatus switch
    {
        ConnectionHealthStatus.Online   => "Online",
        ConnectionHealthStatus.Degraded => "Degraded",
        ConnectionHealthStatus.Offline  => "Offline",
        _                               => "No connection",
    };

    public string ConnectionHealthTooltip
    {
        get
        {
            var p = Profiles.FirstOrDefault(x => x.Id == _activeProfileId);
            if (p is null) return "No active connection — click to manage";
            var label = ConnectionHealthLabel;
            return $"{p.Name} ({p.Provider} · {p.Host}:{p.Port}/{p.Database}) — {label}";
        }
    }

    // ── Edit form ─────────────────────────────────────────────────────────────

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set => Set(ref _isEditing, value);
    }

    private string _editId = Guid.NewGuid().ToString();

    private string _editName = "New Connection";
    public string EditName { get => _editName; set => Set(ref _editName, value); }

    private DatabaseProvider _editProvider = DatabaseProvider.Postgres;
    public DatabaseProvider EditProvider
    {
        get => _editProvider;
        set
        {
            var oldDefault = ConnectionProfile.DefaultPort(_editProvider);
            Set(ref _editProvider, value);
            if (EditPort == oldDefault)
                EditPort = ConnectionProfile.DefaultPort(value);
        }
    }

    private string _editHost = "localhost";
    public string EditHost { get => _editHost; set => Set(ref _editHost, value); }

    private int _editPort = 5432;
    public int EditPort { get => _editPort; set => Set(ref _editPort, value); }

    private string _editDatabase = "";
    public string EditDatabase { get => _editDatabase; set => Set(ref _editDatabase, value); }

    private string _editUsername = "";
    public string EditUsername { get => _editUsername; set => Set(ref _editUsername, value); }

    private string _editPassword = "";
    public string EditPassword { get => _editPassword; set => Set(ref _editPassword, value); }

    private bool _editUseIntegratedSecurity;
    public bool EditUseIntegratedSecurity
    {
        get => _editUseIntegratedSecurity;
        set => Set(ref _editUseIntegratedSecurity, value);
    }

    private int _editTimeout = 30;
    public int EditTimeout { get => _editTimeout; set => Set(ref _editTimeout, value); }

    // ── Test connection state ─────────────────────────────────────────────────

    private bool _isTesting;
    public bool IsTesting
    {
        get => _isTesting;
        set
        {
            Set(ref _isTesting, value);
            TestConnectionCommand.NotifyCanExecuteChanged();
        }
    }

    private string _testStatus = "";
    public string TestStatus { get => _testStatus; set => Set(ref _testStatus, value); }

    private string _testStatusColor = "#4A5568";
    public string TestStatusColor { get => _testStatusColor; set => Set(ref _testStatusColor, value); }

    // ── Background health monitor ─────────────────────────────────────────────

    private CancellationTokenSource? _healthMonitorCts;

    // ── Provider list for ComboBox ────────────────────────────────────────────

    public static IReadOnlyList<DatabaseProvider> AvailableProviders { get; } =
        Enum.GetValues<DatabaseProvider>();

    // ── Commands ──────────────────────────────────────────────────────────────

    public RelayCommand NewProfileCommand { get; }
    public RelayCommand SaveProfileCommand { get; }
    public RelayCommand DeleteProfileCommand { get; }
    public RelayCommand TestConnectionCommand { get; }
    public RelayCommand ConnectCommand { get; }
    public RelayCommand DisconnectCommand { get; }
    public RelayCommand CloseCommand { get; }
    public RelayCommand RefreshHealthCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public ConnectionManagerViewModel()
    {
        _logger = NullLogger<ConnectionManagerViewModel>.Instance;
        _dbConnectionService = new DatabaseConnectionService();

        NewProfileCommand     = new RelayCommand(BeginNewProfile);
        SaveProfileCommand    = new RelayCommand(SaveProfile);
        DeleteProfileCommand  = new RelayCommand(DeleteProfile, () => SelectedProfile is not null);
        TestConnectionCommand = new RelayCommand(() => _ = TestConnectionAsync(), () => !IsTesting && IsEditing);
        ConnectCommand        = new RelayCommand(Connect, () => SelectedProfile is not null);
        DisconnectCommand     = new RelayCommand(Disconnect, () => _activeProfileId is not null);
        CloseCommand          = new RelayCommand(() => IsVisible = false);
        RefreshHealthCommand  = new RelayCommand(() => _ = RunHealthCheckAsync(), () => _activeProfileId is not null);

        LoadProfiles();
    }

    public void Open() => IsVisible = true;

    // ── Form operations ───────────────────────────────────────────────────────

    private void BeginNewProfile()
    {
        _selectedProfile = null;
        RaisePropertyChanged(nameof(SelectedProfile));

        _editId = Guid.NewGuid().ToString();
        EditName = "New Connection";
        EditProvider = DatabaseProvider.Postgres;
        EditHost = "localhost";
        EditPort = 5432;
        EditDatabase = "";
        EditUsername = "";
        EditPassword = "";
        EditUseIntegratedSecurity = false;
        EditTimeout = 30;
        IsEditing = true;
        TestStatus = "";
        TestConnectionCommand.NotifyCanExecuteChanged();
    }

    private void LoadProfileIntoForm(ConnectionProfile p)
    {
        _editId = p.Id;
        EditName = p.Name;
        EditProvider = p.Provider;
        EditHost = p.Host;
        EditPort = p.Port;
        EditDatabase = p.Database;
        EditUsername = p.Username;
        EditPassword = p.Password;
        EditUseIntegratedSecurity = p.UseIntegratedSecurity;
        EditTimeout = p.TimeoutSeconds;
        IsEditing = true;
        TestStatus = "";
        TestConnectionCommand.NotifyCanExecuteChanged();
    }

    private void SaveProfile()
    {
        var profile = new ConnectionProfile
        {
            Id                    = _editId,
            Name                  = EditName,
            Provider              = EditProvider,
            Host                  = EditHost,
            Port                  = EditPort,
            Database              = EditDatabase,
            Username              = EditUsername,
            Password              = EditPassword,
            UseIntegratedSecurity = EditUseIntegratedSecurity,
            TimeoutSeconds        = EditTimeout,
        };

        var existing = Profiles.FirstOrDefault(p => p.Id == profile.Id);
        if (existing is null)
        {
            Profiles.Add(profile);
            _selectedProfile = profile;
            RaisePropertyChanged(nameof(SelectedProfile));
        }
        else
        {
            var idx = Profiles.IndexOf(existing);
            Profiles[idx] = profile;
            _selectedProfile = profile;
            RaisePropertyChanged(nameof(SelectedProfile));
        }

        if (_activeProfileId == profile.Id)
        {
            RaisePropertyChanged(nameof(ActiveConnectionLabel));
            RaisePropertyChanged(nameof(ConnectionHealthTooltip));
        }

        PersistProfiles();
    }

    private void DeleteProfile()
    {
        if (SelectedProfile is null) return;
        if (SelectedProfile.Id == ActiveProfileId) ActiveProfileId = null;
        Profiles.Remove(SelectedProfile);
        SelectedProfile = null;
        IsEditing = false;
        TestStatus = "";
        PersistProfiles();
    }

    private void Connect()
    {
        if (SelectedProfile is null) return;
        IsConnecting = true;
        ActiveProfileId = SelectedProfile.Id;
        // Run an immediate health check for the newly activated connection
        _ = RunHealthCheckAsync();
        // Load database tables into the search menu in the background
        _ = LoadDatabaseTablesAsync(SelectedProfile);
        IsVisible = false;
    }

    private void Disconnect()
    {
        ActiveProfileId = null;
        DisconnectCommand.NotifyCanExecuteChanged();
        ConnectCommand.NotifyCanExecuteChanged();
        RefreshHealthCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Asynchronously loads database tables into the search menu.
    /// This executes the normal workflow after a successful database connection:
    /// 1. Fetches database schema and metadata
    /// 2. Converts to search menu format
    /// 3. Populates the search menu with available tables
    /// 4. Resets the canvas to show the new database
    /// </summary>
    private async Task LoadDatabaseTablesAsync(ConnectionProfile profile)
    {
        // If SearchMenu is not set, we can't load tables (initialization not complete)
        if (SearchMenu is null)
        {
            IsConnecting = false;
            return;
        }

        try
        {
            var config = profile.ToConnectionConfig();
            await _dbConnectionService.ConnectAndLoadAsync(config, SearchMenu);

            // Update canvas with the loaded metadata and connection config, and reset it
            if (Canvas is not null && _dbConnectionService.LoadedMetadata is not null)
            {
                Canvas.SetDatabaseAndResetCanvas(_dbConnectionService.LoadedMetadata, config);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash — connection health check already provided feedback
            _logger?.LogError(
                ex,
                "Failed to load database tables for connection {Profile}",
                profile.Name
            );
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private async Task TestConnectionAsync()
    {
        IsTesting = true;
        TestStatus = "Testing...";
        TestStatusColor = "#FBBF24";

        try
        {
            var config = new ConnectionProfile
            {
                Provider              = EditProvider,
                Host                  = EditHost,
                Port                  = EditPort,
                Database              = EditDatabase,
                Username              = EditUsername,
                Password              = EditPassword,
                UseIntegratedSecurity = EditUseIntegratedSecurity,
                TimeoutSeconds        = EditTimeout,
            }.ToConnectionConfig();

            var result = await RunTestAsync(config, EditProvider, EditTimeout);

            if (result.Success)
            {
                var ms = result.Latency?.TotalMilliseconds ?? 0;
                var lag = ms >= DegradedLatencyThresholdMs ? $" — high latency ({ms:0}ms)" : $" · {ms:0}ms";
                TestStatus = $"Connected{lag}";
                TestStatusColor = ms >= DegradedLatencyThresholdMs ? "#FBBF24" : "#4ADE80";
            }
            else
            {
                TestStatus = result.ErrorMessage ?? "Connection failed";
                TestStatusColor = "#EF4444";
            }
        }
        catch (Exception ex)
        {
            TestStatus = MapConnectionException(ex, EditProvider);
            TestStatusColor = "#EF4444";
        }
        finally
        {
            IsTesting = false;
        }
    }

    // ── Background health monitor ─────────────────────────────────────────────

    private void RestartHealthMonitor()
    {
        _healthMonitorCts?.Cancel();
        _healthMonitorCts?.Dispose();

        if (_activeProfileId is null) return;

        _healthMonitorCts = new CancellationTokenSource();
        _ = HealthMonitorLoopAsync(_healthMonitorCts.Token);
    }

    private async Task HealthMonitorLoopAsync(CancellationToken ct)
    {
        // Skip the first tick — Connect() already triggers RunHealthCheckAsync
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(HealthCheckIntervalSeconds), ct);
                if (!ct.IsCancellationRequested)
                    await RunHealthCheckAsync(ct);
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
    }

    private async Task RunHealthCheckAsync(CancellationToken ct = default)
    {
        var profile = Profiles.FirstOrDefault(x => x.Id == _activeProfileId);
        if (profile is null)
        {
            ActiveHealthStatus = ConnectionHealthStatus.Unknown;
            return;
        }

        try
        {
            var result = await RunTestAsync(profile.ToConnectionConfig(), profile.Provider, profile.TimeoutSeconds, ct);
            if (!result.Success)
            {
                ActiveHealthStatus = ConnectionHealthStatus.Offline;
                return;
            }
            var ms = result.Latency?.TotalMilliseconds ?? 0;
            ActiveHealthStatus = ms >= DegradedLatencyThresholdMs
                ? ConnectionHealthStatus.Degraded
                : ConnectionHealthStatus.Online;
        }
        catch (OperationCanceledException)
        {
            // either shutdown or timeout — mark offline only if the monitor wasn't cancelled
            if (!ct.IsCancellationRequested)
                ActiveHealthStatus = ConnectionHealthStatus.Offline;
        }
        catch
        {
            ActiveHealthStatus = ConnectionHealthStatus.Offline;
        }
    }

    // ── Shared test helper ────────────────────────────────────────────────────

    private static async Task<ConnectionTestResult> RunTestAsync(
        ConnectionConfig config,
        DatabaseProvider provider,
        int timeoutSeconds,
        CancellationToken ct = default)
    {
        IDbOrchestrator orchestrator = provider switch
        {
            DatabaseProvider.Postgres  => new PostgresOrchestrator(config),
            DatabaseProvider.MySql     => new MySqlOrchestrator(config),
            DatabaseProvider.SqlServer => new SqlServerOrchestrator(config),
            _                          => throw new NotSupportedException($"Unknown provider: {provider}"),
        };

        await using (orchestrator)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            return await orchestrator.TestConnectionAsync(cts.Token);
        }
    }

    // ── Friendly error mapping ────────────────────────────────────────────────

    private static string MapConnectionException(Exception ex, DatabaseProvider provider)
    {
        // Avoid leaking passwords — only use ex.Message, never connection strings
        var msg = ex.Message;

        if (ex is OperationCanceledException or TimeoutException)
            return "Connection timed out — check that the server is reachable and increase the timeout if needed.";

        var lower = msg.ToLowerInvariant();

        if (ContainsAny(lower, "password", "authentication failed", "invalid password",
                        "no pg_hba.conf entry", "login failed", "access denied"))
            return $"Authentication failed — verify username and password for {provider}.";

        if (ContainsAny(lower, "does not exist", "unknown database", "database", "catalog"))
            return $"Database not found — confirm the database name exists on {provider}.";

        if (ContainsAny(lower, "name or service not known", "no such host", "getaddrinfo",
                        "nodename nor servname", "server not found", "host", "dns"))
            return "Host not found — check the server address and DNS resolution.";

        if (ContainsAny(lower, "connection refused", "refused", "econnrefused"))
            return $"Port {(lower.Contains("refused") ? "" : "connection")} refused — check the port number and that the server is running / firewall rules allow access.";

        if (ContainsAny(lower, "ssl", "tls", "certificate", "x509"))
            return "SSL/TLS error — check the server's SSL configuration or disable SSL for local connections.";

        if (ContainsAny(lower, "timeout", "timed out", "deadlock"))
            return "Connection timed out — the server may be overloaded or unreachable. Try increasing the timeout.";

        if (ContainsAny(lower, "permission denied", "privilege"))
            return "Insufficient privileges — the user may lack permission to connect to this database.";

        // Truncate very long messages to keep UI clean
        return msg.Length > 160 ? msg[..160] + "…" : msg;
    }

    private static bool ContainsAny(string source, params string[] tokens)
        => tokens.Any(t => source.Contains(t, StringComparison.OrdinalIgnoreCase));

    // ── Persistence ───────────────────────────────────────────────────────────

    private static string ProfilesFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VisualSqlArchitect", "connections.json");

    private void LoadProfiles()
    {
        try
        {
            if (!File.Exists(ProfilesFilePath)) return;
            var json = File.ReadAllText(ProfilesFilePath);
            var profiles = JsonSerializer.Deserialize<List<ConnectionProfile>>(json, JsonOpts);
            if (profiles is null) return;
            foreach (var p in profiles) Profiles.Add(p);
        }
        catch { /* ignore corrupt/missing file */ }
    }

    private void PersistProfiles()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProfilesFilePath)!);
            File.WriteAllText(
                ProfilesFilePath,
                JsonSerializer.Serialize(Profiles.ToList(), JsonOpts));
        }
        catch { /* persistence is best-effort */ }
    }
}
