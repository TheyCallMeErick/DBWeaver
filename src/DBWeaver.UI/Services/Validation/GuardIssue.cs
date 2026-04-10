namespace DBWeaver.UI.Services.Validation;

public sealed record GuardIssue(
    GuardSeverity Severity,
    string Code,
    string Message,
    string Suggestion
);
