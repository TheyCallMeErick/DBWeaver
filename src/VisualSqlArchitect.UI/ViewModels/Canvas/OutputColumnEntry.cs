using System.Windows.Input;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Represents an ordered column entry in a ResultOutput node.
/// Provides commands to reorder columns in the result set.
/// </summary>
public sealed class OutputColumnEntry(
    string key,
    string displayName,
    Action moveUp,
    Action moveDown
) : ViewModelBase
{
    /// <summary>
    /// Unique key: "{nodeId}::{pinName}"
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// Display name: "TableName.ColumnName" or "NodeTitle → PinName"
    /// </summary>
    public string DisplayName { get; } = displayName;

    /// <summary>
    /// Command to move this column up in the result set order.
    /// </summary>
    public ICommand MoveUpCommand { get; } = new RelayCommand(moveUp);

    /// <summary>
    /// Command to move this column down in the result set order.
    /// </summary>
    public ICommand MoveDownCommand { get; } = new RelayCommand(moveDown);
}
