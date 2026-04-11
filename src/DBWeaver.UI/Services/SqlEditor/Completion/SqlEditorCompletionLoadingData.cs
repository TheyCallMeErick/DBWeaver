using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorCompletionLoadingData : ICompletionData
{
    private readonly SqlEditorCompletionLoadingContent _content;

    public SqlEditorCompletionLoadingData(string message)
    {
        Text = message;
        Description = message;
        _content = new SqlEditorCompletionLoadingContent(message);
    }

    public IImage? Image => null;

    public string Text { get; }

    public object Content => _content;

    public object Description { get; }

    public double Priority => double.MinValue;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        // Loading entry is informational only and should never mutate editor text.
    }
}
