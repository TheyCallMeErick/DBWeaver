namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorMutationEstimateCacheEntry
{
    public required string CountQuery { get; init; }

    public long? EstimatedRows { get; init; }

    public required DateTimeOffset CachedAt { get; init; }
}

