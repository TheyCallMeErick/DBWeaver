namespace DBWeaver.CanvasKit;

using System.Globalization;

public static class CanvasWireGeometry
{
    public static string BuildBezierPath(double fromX, double fromY, double toX, double toY)
    {
        double dx = Math.Abs(toX - fromX);
        double offset = Math.Max(60, dx * 0.5);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"M {fromX:F1},{fromY:F1} C {fromX + offset:F1},{fromY:F1} {toX - offset:F1},{toY:F1} {toX:F1},{toY:F1}"
        );
    }
}
