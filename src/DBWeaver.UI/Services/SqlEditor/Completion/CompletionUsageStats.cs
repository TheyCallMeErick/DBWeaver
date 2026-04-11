namespace DBWeaver.UI.Services.SqlEditor;

public sealed class CompletionUsageStats
{
    private const int RecentCapacity = 200;
    private const int FrequencyCapacity = 5000;
    private static readonly TimeSpan PersistDebounce = TimeSpan.FromSeconds(5);

    private readonly ISqlCompletionFrequencyStore _frequencyStore;
    private readonly LinkedList<string> _recentIdentifiers = new();
    private readonly Dictionary<string, int> _frequencyByIdentifier = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    private string? _loadedProfileId;
    private Timer? _persistTimer;
    private bool _persistPending;

    public CompletionUsageStats(ISqlCompletionFrequencyStore? frequencyStore = null)
    {
        _frequencyStore = frequencyStore ?? new AppSettingsSqlCompletionFrequencyStore();
    }

    public void RecordAccepted(string identifier, string? profileId)
    {
        string normalized = NormalizeIdentifier(identifier);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        lock (_sync)
        {
            _recentIdentifiers.AddLast(normalized);
            while (_recentIdentifiers.Count > RecentCapacity)
                _recentIdentifiers.RemoveFirst();

            if (string.IsNullOrWhiteSpace(profileId))
                return;

            EnsureProfileLoaded(profileId!);
            _frequencyByIdentifier[normalized] = _frequencyByIdentifier.GetValueOrDefault(normalized) + 1;
            TrimLeastFrequentIfNeeded();
            SchedulePersist();
        }
    }

    public int CountRecentOccurrences(string identifier, int window = 10)
    {
        string normalized = NormalizeIdentifier(identifier);
        if (string.IsNullOrWhiteSpace(normalized) || window <= 0)
            return 0;

        lock (_sync)
        {
            int count = 0;
            int visited = 0;
            LinkedListNode<string>? node = _recentIdentifiers.Last;
            while (node is not null && visited < window)
            {
                if (string.Equals(node.Value, normalized, StringComparison.OrdinalIgnoreCase))
                    count++;

                visited++;
                node = node.Previous;
            }

            return count;
        }
    }

    public int GetFrequency(string identifier, string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return 0;

        string normalized = NormalizeIdentifier(identifier);
        if (string.IsNullOrWhiteSpace(normalized))
            return 0;

        lock (_sync)
        {
            EnsureProfileLoaded(profileId!);
            return _frequencyByIdentifier.GetValueOrDefault(normalized);
        }
    }

    private void EnsureProfileLoaded(string profileId)
    {
        if (string.Equals(_loadedProfileId, profileId, StringComparison.Ordinal))
            return;

        _loadedProfileId = profileId;
        _frequencyByIdentifier.Clear();
        IReadOnlyDictionary<string, int> loaded = _frequencyStore.Load(profileId);
        foreach ((string key, int value) in loaded)
        {
            if (string.IsNullOrWhiteSpace(key) || value <= 0)
                continue;

            _frequencyByIdentifier[key] = value;
        }

        TrimLeastFrequentIfNeeded();
    }

    private void TrimLeastFrequentIfNeeded()
    {
        if (_frequencyByIdentifier.Count <= FrequencyCapacity)
            return;

        foreach (string key in _frequencyByIdentifier
                     .OrderBy(static pair => pair.Value)
                     .ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(_frequencyByIdentifier.Count - FrequencyCapacity)
                     .Select(static pair => pair.Key)
                     .ToList())
        {
            _frequencyByIdentifier.Remove(key);
        }
    }

    private void SchedulePersist()
    {
        if (string.IsNullOrWhiteSpace(_loadedProfileId))
            return;

        _persistPending = true;
        _persistTimer ??= new Timer(static state => ((CompletionUsageStats)state!).PersistOnTimer(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _persistTimer.Change(PersistDebounce, Timeout.InfiniteTimeSpan);
    }

    private void PersistOnTimer()
    {
        string? profileId;
        Dictionary<string, int> snapshot;

        lock (_sync)
        {
            if (!_persistPending || string.IsNullOrWhiteSpace(_loadedProfileId))
                return;

            _persistPending = false;
            profileId = _loadedProfileId;
            snapshot = _frequencyByIdentifier.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        _frequencyStore.Save(profileId!, snapshot);
    }

    private static string NormalizeIdentifier(string? identifier)
    {
        string value = identifier?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        int asIndex = value.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
        if (asIndex > 0)
            value = value[..asIndex];

        if (value.Contains(' '))
            return string.Empty;

        if (value.Contains('.'))
            value = value.Split('.').Last();

        value = value.Trim('"', '\'', '`', '[', ']');
        return value;
    }
}
