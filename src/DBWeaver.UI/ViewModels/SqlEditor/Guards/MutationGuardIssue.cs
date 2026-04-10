namespace DBWeaver.UI.ViewModels;

public sealed record MutationGuardIssue(
    MutationGuardSeverity Severity,
    string Code,
    string Message,
    string? Suggestion
);
