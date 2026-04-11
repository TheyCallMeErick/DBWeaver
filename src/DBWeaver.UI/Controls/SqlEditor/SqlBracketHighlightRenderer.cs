using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace DBWeaver.UI.Controls.SqlEditor;

internal sealed class SqlBracketHighlightRenderer : IBackgroundRenderer
{
    private int _primaryOffset = -1;
    private int _secondaryOffset = -1;
    private bool _isError;

    public KnownLayer Layer => KnownLayer.Selection;

    public void Update(string text, int caretOffset)
    {
        _primaryOffset = -1;
        _secondaryOffset = -1;
        _isError = false;

        if (string.IsNullOrEmpty(text) || caretOffset < 0 || caretOffset > text.Length)
            return;

        int delimiterOffset = ResolveDelimiterOffset(text, caretOffset);
        if (delimiterOffset < 0)
            return;

        char delimiter = text[delimiterOffset];
        _primaryOffset = delimiterOffset;

        int matchOffset = delimiter switch
        {
            '(' => FindForward(text, delimiterOffset, '(', ')'),
            ')' => FindBackward(text, delimiterOffset, '(', ')'),
            '[' => FindForward(text, delimiterOffset, '[', ']'),
            ']' => FindBackward(text, delimiterOffset, '[', ']'),
            '\'' => FindQuotePair(text, delimiterOffset, '\''),
            '"' => FindQuotePair(text, delimiterOffset, '"'),
            _ => -1,
        };

        if (matchOffset >= 0)
        {
            _secondaryOffset = matchOffset;
            return;
        }

        _isError = true;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_primaryOffset < 0 || !textView.VisualLinesValid)
            return;

        IBrush highlightBrush = ResolveHighlightBrush(isError: _isError);
        DrawOffset(textView, drawingContext, _primaryOffset, highlightBrush);

        if (_secondaryOffset >= 0)
            DrawOffset(textView, drawingContext, _secondaryOffset, highlightBrush);
    }

    private static int ResolveDelimiterOffset(string text, int caretOffset)
    {
        if (caretOffset < text.Length && IsSupportedDelimiter(text[caretOffset]))
            return caretOffset;

        int previous = caretOffset - 1;
        if (previous >= 0 && previous < text.Length && IsSupportedDelimiter(text[previous]))
            return previous;

        return -1;
    }

    private static bool IsSupportedDelimiter(char value)
    {
        return value is '(' or ')' or '[' or ']' or '\'' or '"';
    }

    private static int FindForward(string text, int startOffset, char open, char close)
    {
        int balance = 0;
        for (int i = startOffset; i < text.Length; i++)
        {
            char current = text[i];
            if (current == open)
                balance++;
            else if (current == close)
                balance--;

            if (balance == 0)
                return i;
        }

        return -1;
    }

    private static int FindBackward(string text, int startOffset, char open, char close)
    {
        int balance = 0;
        for (int i = startOffset; i >= 0; i--)
        {
            char current = text[i];
            if (current == close)
                balance++;
            else if (current == open)
                balance--;

            if (balance == 0)
                return i;
        }

        return -1;
    }

    private static int FindQuotePair(string text, int quoteOffset, char quoteChar)
    {
        for (int i = quoteOffset + 1; i < text.Length; i++)
        {
            if (text[i] == quoteChar && !IsEscaped(text, i))
                return i;
        }

        for (int i = quoteOffset - 1; i >= 0; i--)
        {
            if (text[i] == quoteChar && !IsEscaped(text, i))
                return i;
        }

        return -1;
    }

    private static bool IsEscaped(string text, int offset)
    {
        int backslashes = 0;
        for (int i = offset - 1; i >= 0 && text[i] == '\\'; i--)
            backslashes++;

        return backslashes % 2 != 0;
    }

    private static void DrawOffset(TextView textView, DrawingContext drawingContext, int offset, IBrush brush)
    {
        if (offset < 0 || textView.Document is null || offset >= textView.Document.TextLength)
            return;

        var builder = new BackgroundGeometryBuilder
        {
            AlignToWholePixels = true,
            CornerRadius = 2,
        };

        builder.AddSegment(textView, new OffsetSegment(offset, 1));
        Geometry? geometry = builder.CreateGeometry();
        if (geometry is null)
            return;

        drawingContext.DrawGeometry(brush, null, geometry);
    }

    private static IBrush ResolveHighlightBrush(bool isError)
    {
        string key = isError ? "StatusErrorBrush" : "AccentSubtleBrush";
        if (Application.Current?.TryFindResource(key, out object? resource) == true && resource is IBrush brush)
        {
            if (!isError && brush is ISolidColorBrush solid)
                return new SolidColorBrush(solid.Color, Math.Min(solid.Opacity, 0.45));

            return brush;
        }

        return Brushes.Transparent;
    }

    private readonly struct OffsetSegment(int offset, int length) : ISegment
    {
        public int Offset { get; } = offset;

        public int Length { get; } = length;

        public int EndOffset => Offset + Length;
    }
}
