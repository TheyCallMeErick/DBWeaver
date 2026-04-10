using Avalonia.Media;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.ViewModels.Canvas;

public sealed class DiffItemViewModel
{
    public DiffKind Kind { get; }
    public string Description { get; }

    public string Icon =>
        Kind switch
        {
            DiffKind.Added => "+",
            DiffKind.Removed => "−",
            _ => "~",
        };

    public Color KindColor =>
        Kind switch
        {
            DiffKind.Added => Color.Parse(UiColorConstants.C_4ADE80),
            DiffKind.Removed => Color.Parse(UiColorConstants.C_F87171),
            _ => Color.Parse(UiColorConstants.C_FBBF24),
        };

    public SolidColorBrush KindBrush => new(KindColor);

    public DiffItemViewModel(DiffKind kind, string description)
    {
        Kind = kind;
        Description = description;
    }
}
