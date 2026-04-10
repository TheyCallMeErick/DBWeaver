using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.Start.Models;

public sealed class StartTemplateItem : ViewModelBase
{
    private bool _isFavorite;

    public StartTemplateItem(string name, string category, string description)
    {
        Name = name;
        Category = category;
        Description = description;
    }

    public string Name { get; }
    public string Category { get; }
    public string Description { get; }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => Set(ref _isFavorite, value);
    }
}
