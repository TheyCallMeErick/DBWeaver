namespace DBWeaver.CanvasKit;

public enum CanvasWireDashKind
{
    Solid,
    ShortDash,
    MediumDash,
    LongDash,
    WideDash,
    Dotted,
}

public static class CanvasWireStylePolicy
{
    public static double ResolveThickness(
        double baseThickness,
        bool isHighlighted,
        double highlightBoost = 0.7
    ) => isHighlighted ? baseThickness + highlightBoost : baseThickness;
}
