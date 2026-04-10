using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorCompletionData : ICompletionData
{
    private readonly string _insertText;
    private readonly int _prefixLength;
    private readonly string _descriptionText;
    private readonly object _content;

    public SqlEditorCompletionData(string label, string insertText, string? description, int prefixLength)
    {
        Text = label;
        _insertText = insertText;
        _descriptionText = description ?? string.Empty;
        Description = _descriptionText;
        _prefixLength = Math.Max(0, prefixLength);
        _content = BuildContent();
    }

    public IImage? Image => null;

    public string Text { get; }

    public object Content => _content;

    public object Description { get; }

    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        int replaceStart = Math.Max(0, textArea.Caret.Offset - _prefixLength);
        textArea.Document.Replace(replaceStart, _prefixLength, _insertText);
    }

    private object BuildContent()
    {
        var root = new StackPanel
        {
            Spacing = 1,
            Margin = new Thickness(0),
        };

        root.Children.Add(new TextBlock
        {
            Text = Text,
            FontFamily = FontFamily.Parse("Consolas, 'Cascadia Mono', monospace"),
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = ResourceBrush("TextPrimaryBrush", UiColorConstants.C_E7ECFF),
        });

        if (!string.IsNullOrWhiteSpace(_descriptionText))
        {
            root.Children.Add(new TextBlock
            {
                Text = _descriptionText,
                FontSize = 10,
                Foreground = ResourceBrush("TextSecondaryBrush", UiColorConstants.C_AEB9D9),
            });
        }

        return root;
    }

    private static IBrush ResourceBrush(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out object? resource) == true && resource is IBrush brush)
            return brush;

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }
}
