using System.Collections.ObjectModel;
using System.Windows.Input;
using DBWeaver.Nodes;

namespace DBWeaver.UI.ViewModels;

public sealed class NodeTypeGroupViewModel : ViewModelBase
{
    private bool _isExpanded = true;

    public NodeCategory Category { get; }
    public string Name { get; }
    public string Color { get; }
    public int Count { get; set; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public ICommand ToggleExpandCommand { get; }
    public ObservableCollection<NodeTypeItemViewModel> Items { get; } = [];

    public NodeTypeGroupViewModel(NodeCategory category, string color)
        : this(category, color, null)
    {
    }

    public NodeTypeGroupViewModel(NodeCategory category, string color, string? customName)
    {
        Category = category;
        Color = color;
        Name = string.IsNullOrWhiteSpace(customName) ? GetCategoryName(category) : customName.Trim();
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    private static string GetCategoryName(NodeCategory category) => category switch
    {
        NodeCategory.DataSource => "Data Source",
        NodeCategory.StringTransform => "String Functions",
        NodeCategory.MathTransform => "Math Functions",
        NodeCategory.TypeCast => "Type Conversion",
        NodeCategory.Comparison => "Comparisons",
        NodeCategory.LogicGate => "Logic Gates",
        NodeCategory.Json => "JSON Functions",
        NodeCategory.Aggregate => "Aggregates",
        NodeCategory.Conditional => "Conditionals",
        NodeCategory.ResultModifier => "Result Modifiers",
        NodeCategory.Output => "Output",
        NodeCategory.Literal => "Literals",
        _ => category.ToString(),
    };
}
