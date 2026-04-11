using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorCompletionData : ICompletionData
{
    private readonly string _insertText;
    private readonly int _prefixLength;
    private readonly SqlEditorCompletionItemContent _content;
    private readonly Action<string>? _acceptedCallback;

    public SqlEditorCompletionData(
        string label,
        string insertText,
        string? description,
        int prefixLength,
        Action<string>? acceptedCallback = null)
    {
        Text = label;
        _insertText = insertText;
        string normalizedDescription = description ?? string.Empty;
        Description = normalizedDescription;
        _prefixLength = Math.Max(0, prefixLength);
        _content = new SqlEditorCompletionItemContent(label, normalizedDescription);
        _acceptedCallback = acceptedCallback;
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
        _acceptedCallback?.Invoke(Text);
    }
}
