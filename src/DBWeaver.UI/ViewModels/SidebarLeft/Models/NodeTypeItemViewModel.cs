using System.Windows.Input;
using Material.Icons;
using DBWeaver.Nodes;
using DBWeaver.Nodes.PinTypes;
using DBWeaver.UI.Services.Node;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.ViewModels;

public sealed class NodeTypeItemViewModel : ViewModelBase
{
    private bool _isHovered;

    public NodeDefinition Definition { get; }
    public string Title => Definition.DisplayName;
    public string Subtitle => Definition.Description;
    public string Color { get; }
    public MaterialIconKind IconKind => NodeIconCatalog.GetKindForCategory(Definition.Category);
    public IReadOnlyList<NodePinTooltipItemViewModel> InputPins { get; }
    public IReadOnlyList<NodePinTooltipItemViewModel> OutputPins { get; }
    public IReadOnlyList<NodeTagTooltipItemViewModel> TooltipTags { get; }
    public IReadOnlyList<NodeTagTooltipItemViewModel> CardTags { get; }
    public IReadOnlyList<string> SearchTerms { get; }
    public bool HasInputPins => InputPins.Count > 0;
    public bool HasOutputPins => OutputPins.Count > 0;
    public bool HasTooltipTags => TooltipTags.Count > 0;
    public bool HasCardTags => CardTags.Count > 0;

    public bool IsHovered
    {
        get => _isHovered;
        set => Set(ref _isHovered, value);
    }

    public ICommand SpawnNodeCommand { get; }

    public NodeTypeItemViewModel(NodeDefinition definition, string color, Action<NodeDefinition> onSpawn)
    {
        Definition = definition;
        Color = color;
        InputPins = [.. definition.InputPins.Select(BuildPinTooltipItem)];
        OutputPins = [.. definition.OutputPins.Select(BuildPinTooltipItem)];
        IReadOnlyList<NodeTag> tags = NodeTagCatalog.Resolve(definition);
        List<NodeTagTooltipItemViewModel> tagItems = [.. tags.Select(BuildTagTooltipItem)];
        TooltipTags = [.. tagItems.Take(3)];
        CardTags = [.. tagItems.Take(2)];
        SearchTerms = [.. tags.Select(t => t.Name)];
        SpawnNodeCommand = new RelayCommand(() => onSpawn(definition));
    }

    private static NodePinTooltipItemViewModel BuildPinTooltipItem(PinDescriptor pin)
    {
        string color = PinTypeRegistry.GetType(pin.DataType).VisualColorHex;
        (string glyph, string name) = ResolvePinShape(pin.DataType);
        return new NodePinTooltipItemViewModel(
            Name: pin.Name,
            TypeName: pin.DataType.ToString(),
            Color: color,
            ShapeGlyph: glyph,
            ShapeName: name);
    }

    private static NodeTagTooltipItemViewModel BuildTagTooltipItem(NodeTag tag)
    {
        string baseColor = tag.ColorHex;
        return new NodeTagTooltipItemViewModel(
            Name: tag.Name,
            BackgroundColor: ApplyAlpha(baseColor, "22"),
            BorderColor: ApplyAlpha(baseColor, "88"),
            ForegroundColor: ApplyAlpha(baseColor, "EE"));
    }

    private static string ApplyAlpha(string hexColor, string alphaHex)
    {
        if (string.IsNullOrWhiteSpace(hexColor))
            return UiColorConstants.C_334155;

        string normalized = hexColor.Trim();
        if (!normalized.StartsWith('#'))
            return normalized;

        if (normalized.Length == 7)
            return $"#{alphaHex}{normalized[1..]}";

        return normalized;
    }

    private static (string Glyph, string Name) ResolvePinShape(PinDataType dataType) =>
        dataType switch
        {
            PinDataType.ColumnRef => ("◆", "Diamond"),
            PinDataType.ColumnSet => ("◇", "Diamond"),
            PinDataType.RowSet => ("◆", "Flat Diamond"),
            PinDataType.TableDef => ("■", "Rounded Square"),
            PinDataType.ViewDef => ("■", "Rounded Square"),
            PinDataType.ColumnDef => ("◎", "Double Circle"),
            PinDataType.Constraint => ("◆", "Diamond"),
            PinDataType.TypeDef => ("◇", "Double Diamond"),
            PinDataType.IndexDef => ("▲", "Triangle"),
            PinDataType.AlterOp => ("➤", "Rounded Arrow"),
            PinDataType.ReportQuery => ("▣", "Report Query"),
            _ => ("●", "Circle"),
        };
}
