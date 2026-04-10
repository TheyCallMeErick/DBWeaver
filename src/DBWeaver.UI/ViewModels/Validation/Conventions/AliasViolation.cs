namespace DBWeaver.UI.ViewModels.Validation.Conventions;

/// <summary>
/// Immutable validation violation for alias naming conventions.
/// </summary>
public sealed record AliasViolation(string Code, string Message, string? Suggestion);

