namespace DBWeaver.UI.Services.Validation;

public sealed record DiagnosticResult(
    ErrorCategory Category,
    string CategoryLabel,
    string CategoryIcon,
    string FriendlyMessage,
    string? TechnicalDetail,
    string Suggestion
);
