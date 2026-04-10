using System.Collections.Generic;

namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public sealed record ConnectionDetailsDto(
    string Id,
    string Name,
    string Provider,
    ConnectionProviderModeDto Mode,
    IReadOnlyDictionary<string, string?> FieldValues,
    string? UrlValue,
    string? Tag,
    bool IsFavorite,
    IReadOnlyDictionary<string, string?> AdvancedOptions);
