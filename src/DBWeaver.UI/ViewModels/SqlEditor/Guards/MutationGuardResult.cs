namespace DBWeaver.UI.ViewModels;

public sealed class MutationGuardResult
{
    public bool IsSafe { get; init; }
    public bool RequiresConfirmation { get; init; }
    public IReadOnlyList<MutationGuardIssue> Issues { get; init; } = [];
    public string? CountQuery { get; init; }
    public bool SupportsDiff { get; init; }

    public static MutationGuardResult Safe() => new()
    {
        IsSafe = true,
        RequiresConfirmation = false,
        Issues = [],
        CountQuery = null,
        SupportsDiff = false,
    };
}
