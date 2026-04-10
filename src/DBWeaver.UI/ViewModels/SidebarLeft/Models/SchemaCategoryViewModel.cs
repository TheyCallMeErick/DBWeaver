using System.Collections.ObjectModel;

namespace DBWeaver.UI.ViewModels;

public sealed class SchemaCategoryViewModel : ViewModelBase
{
    private bool _isExpanded = true;

    public string Name { get; }
    public string Icon { get; }
    public string Color { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public ObservableCollection<SchemaObjectViewModel> Items { get; } = [];
    public int Count => Items.Count;

    public RelayCommand<SchemaCategoryViewModel> ToggleCategoryCommand { get; }

    public SchemaCategoryViewModel(string name, string icon, string color)
    {
        Name = name;
        Icon = icon;
        Color = color;
        ToggleCategoryCommand = new RelayCommand<SchemaCategoryViewModel>(category =>
        {
            if (category is not null)
                category.IsExpanded = !category.IsExpanded;
        });
    }
}