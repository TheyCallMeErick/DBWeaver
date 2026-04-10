namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlCompletionRequest
{
    public int PrefixLength { get; init; }
    public IReadOnlyList<SqlCompletionSuggestion> Suggestions { get; init; } = [];
}
