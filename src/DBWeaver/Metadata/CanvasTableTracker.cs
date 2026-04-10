namespace DBWeaver.Metadata;

public interface ICanvasTableTracker
{
    void Add(string fullTableName);
    void Remove(string fullTableName);
    bool Contains(string fullTableName);
    IReadOnlyList<string> Snapshot();
    int Count { get; }
}

public sealed class CanvasTableTracker : ICanvasTableTracker
{
    private readonly HashSet<string> _tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    public void Add(string fullTableName)
    {
        lock (_gate)
            _tables.Add(fullTableName);
    }

    public void Remove(string fullTableName)
    {
        lock (_gate)
            _tables.Remove(fullTableName);
    }

    public bool Contains(string fullTableName)
    {
        lock (_gate)
            return _tables.Contains(fullTableName);
    }

    public IReadOnlyList<string> Snapshot()
    {
        lock (_gate)
            return [.. _tables];
    }

    public int Count
    {
        get
        {
            lock (_gate)
                return _tables.Count;
        }
    }
}
