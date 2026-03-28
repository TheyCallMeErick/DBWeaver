using Avalonia;
using Avalonia.Media;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Represents a connection (wire) entre two pins in the canvas.
/// Manages the visual and data flow representation of the connection.
/// </summary>
public sealed class ConnectionViewModel : ViewModelBase
{
    private Point _fromPoint;
    private Point _toPoint;
    private bool _isHighlighted;

    /// <summary>Unique identifier for this connection.</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>The pin data flows FROM (must be Output).</summary>
    public PinViewModel FromPin { get; }

    public ConnectionViewModel(PinViewModel fromPin, Point fromPoint, Point toPoint)
    {
        FromPin = fromPin;
        _fromPoint = fromPoint;
        _toPoint = toPoint;
    }

    /// <summary>The pin data flows TO (must be Input). Can be null while dragging.</summary>
    public PinViewModel? ToPin { get; set; }

    /// <summary>Screen position of the source pin.</summary>
    public Point FromPoint
    {
        get => _fromPoint;
        set
        {
            Set(ref _fromPoint, value);
            RaisePropertyChanged(nameof(BezierPath));
        }
    }

    /// <summary>Screen position of the target pin.</summary>
    public Point ToPoint
    {
        get => _toPoint;
        set
        {
            Set(ref _toPoint, value);
            RaisePropertyChanged(nameof(BezierPath));
        }
    }

    /// <summary>True if this connection is currently highlighted (hovered).</summary>
    public bool IsHighlighted
    {
        get => _isHighlighted;
        set => Set(ref _isHighlighted, value);
    }

    /// <summary>Wire color matches the source pin's data type color.</summary>
    public Color WireColor => FromPin.PinColor;

    /// <summary>Opacity increases when highlighted.</summary>
    public double WireOpacity => IsHighlighted ? 1.0 : 0.75;

    /// <summary>Thickness increases when highlighted.</summary>
    public double WireThickness => IsHighlighted ? 2.5 : 1.8;

    /// <summary>
    /// Generates a smooth cubic Bezier curve from FromPoint to ToPoint.
    /// Control points create smooth, readable connections.
    /// </summary>
    public string BezierPath
    {
        get
        {
            double dx = Math.Abs(ToPoint.X - FromPoint.X);
            double off = Math.Max(60, dx * 0.5);
            return $"M {FromPoint.X:F1},{FromPoint.Y:F1} "
                + $"C {FromPoint.X + off:F1},{FromPoint.Y:F1} {ToPoint.X - off:F1},{ToPoint.Y:F1} "
                + $"{ToPoint.X:F1},{ToPoint.Y:F1}";
        }
    }
}
