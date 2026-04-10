using DBWeaver.Core;

namespace DBWeaver.UI.ViewModels;

public sealed class SqlEditorConnectionProfileOption
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required DatabaseProvider Provider { get; init; }
}
