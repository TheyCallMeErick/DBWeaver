namespace DBWeaver.Metadata;

public interface IJoinSuggestionEngine
{
    IReadOnlyList<JoinSuggestion> Suggest(
        DbMetadata metadata,
        string newTable,
        IEnumerable<string> canvasTables
    );
}

public sealed class AutoJoinSuggestionEngine : IJoinSuggestionEngine
{
    public IReadOnlyList<JoinSuggestion> Suggest(
        DbMetadata metadata,
        string newTable,
        IEnumerable<string> canvasTables
    )
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(newTable);
        ArgumentNullException.ThrowIfNull(canvasTables);

        return new AutoJoinDetector(metadata).Suggest(newTable, canvasTables);
    }
}
