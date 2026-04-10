namespace DBWeaver.UI.Services.Validation;

public sealed record ValidationIssue(
    IssueSeverity Severity,
    string NodeId,
    string Code,
    string Message,
    string? Suggestion = null
);
