using System.Collections.ObjectModel;
using System.Windows.Input;

namespace DBWeaver.UI.ViewModels;

public sealed class SchemaObjectViewModel : ViewModelBase
{
    private bool _isExpanded;

    public string Name { get; }
    public string Icon { get; }
    public string SubText { get; }
    public string? DataType { get; }
    public string? BadgeColor { get; }
    public object? Data { get; }
    public ICommand? AddNodeCommand { get; }

    public bool IsExpandable => Children.Count > 0;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public ObservableCollection<SchemaObjectViewModel> Children { get; } = [];

    public SchemaObjectViewModel(
        string name,
        string icon,
        string subText = "",
        string? dataType = null,
        string? badgeColor = null,
        object? data = null,
        ICommand? addNodeCommand = null)
    {
        Name = name;
        Icon = icon;
        SubText = subText;
        DataType = dataType;
        BadgeColor = badgeColor;
        Data = data;
        AddNodeCommand = addNodeCommand;
    }
}
