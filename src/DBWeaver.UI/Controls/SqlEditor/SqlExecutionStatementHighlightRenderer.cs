using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace DBWeaver.UI.Controls.SqlEditor;

internal sealed class SqlExecutionStatementHighlightRenderer : IBackgroundRenderer
{
    private int _startLine;
    private int _endLine;

    public KnownLayer Layer => KnownLayer.Selection;

    public void Update(int startLine, int endLine)
    {
        if (startLine <= 0 || endLine <= 0 || endLine < startLine)
        {
            _startLine = 0;
            _endLine = 0;
            return;
        }

        _startLine = startLine;
        _endLine = endLine;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_startLine <= 0 || _endLine <= 0 || !textView.VisualLinesValid || textView.Document is null)
            return;

        int boundedStart = Math.Clamp(_startLine, 1, textView.Document.LineCount);
        int boundedEnd = Math.Clamp(_endLine, boundedStart, textView.Document.LineCount);
        IBrush brush = ResolveHighlightBrush();

        for (int lineNumber = boundedStart; lineNumber <= boundedEnd; lineNumber++)
        {
            DocumentLine line = textView.Document.GetLineByNumber(lineNumber);
            int length = Math.Max(1, line.Length);

            var builder = new BackgroundGeometryBuilder
            {
                AlignToWholePixels = true,
                CornerRadius = 2,
            };
            builder.AddSegment(textView, new OffsetSegment(line.Offset, length));

            Geometry? geometry = builder.CreateGeometry();
            if (geometry is null)
                continue;

            drawingContext.DrawGeometry(brush, null, geometry);
        }
    }

    private static IBrush ResolveHighlightBrush()
    {
        if (Application.Current?.TryFindResource("AccentSubtleBrush", out object? resource) == true
            && resource is ISolidColorBrush solid)
        {
            return new SolidColorBrush(solid.Color, 0.28);
        }

        return new SolidColorBrush(Color.FromArgb(60, 100, 180, 255));
    }

    private readonly struct OffsetSegment(int offset, int length) : ISegment
    {
        public int Offset { get; } = offset;

        public int Length { get; } = length;

        public int EndOffset => Offset + Length;
    }
}
