namespace DBWeaver.UI.ViewModels;

public sealed class SqlMutationDiffPreview
{
    public bool Available { get; init; }
    public required string Message { get; init; }

    public static SqlMutationDiffPreview Unavailable(string reason) => new()
    {
        Available = false,
        Message = reason,
    };
}
