using Material.Icons;

namespace DBWeaver.UI.ViewModels;

public sealed class PaletteCommandItem
{
    public string ActionId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Shortcut { get; init; } = "";
    public MaterialIconKind Icon { get; init; } = MaterialIconKind.Play;
    public string Tags { get; init; } = "";
    public Action Execute { get; init; } = () => { };
}
