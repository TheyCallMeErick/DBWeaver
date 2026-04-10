namespace DBWeaver.UI.ViewModels;

public enum CanvasWireCurveMode
{
    Bezier,
    Straight,
    Orthogonal,
}

public static class CanvasWireCurveModeExtensions
{
    public static CanvasWireRoutingMode ToRoutingMode(this CanvasWireCurveMode mode) =>
        mode switch
        {
            CanvasWireCurveMode.Straight => CanvasWireRoutingMode.Straight,
            CanvasWireCurveMode.Orthogonal => CanvasWireRoutingMode.Orthogonal,
            _ => CanvasWireRoutingMode.Bezier,
        };

    public static CanvasWireCurveMode ToCurveMode(this CanvasWireRoutingMode mode) =>
        mode switch
        {
            CanvasWireRoutingMode.Straight => CanvasWireCurveMode.Straight,
            CanvasWireRoutingMode.Orthogonal => CanvasWireCurveMode.Orthogonal,
            _ => CanvasWireCurveMode.Bezier,
        };
}
