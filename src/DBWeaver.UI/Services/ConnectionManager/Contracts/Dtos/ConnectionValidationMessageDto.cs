namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public sealed record ConnectionValidationMessageDto(
    string FieldKey,
    string Code,
    string Message);
