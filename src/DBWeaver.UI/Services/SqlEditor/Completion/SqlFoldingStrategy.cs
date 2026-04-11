using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlFoldingStrategy
{
    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, IReadOnlyList<SqlToken> tokens)
    {
        if (document.TextLength == 0 || tokens.Count == 0)
            return [];

        var foldings = new List<NewFolding>();
        var parenthesisStack = new Stack<SqlToken>();
        var caseStack = new Stack<SqlToken>();

        foreach (SqlToken token in tokens)
        {
            if (token.Kind == SqlTokenKind.Punctuation)
            {
                if (token.Value == "(")
                {
                    parenthesisStack.Push(token);
                    continue;
                }

                if (token.Value == ")" && parenthesisStack.Count > 0)
                {
                    SqlToken start = parenthesisStack.Pop();
                    TryAddFolding(document, foldings, start.StartOffset, token.EndOffset, "(...)");
                }

                continue;
            }

            if (token.Kind != SqlTokenKind.Keyword)
                continue;

            if (string.Equals(token.Value, "CASE", StringComparison.OrdinalIgnoreCase))
            {
                caseStack.Push(token);
                continue;
            }

            if (string.Equals(token.Value, "END", StringComparison.OrdinalIgnoreCase) && caseStack.Count > 0)
            {
                SqlToken start = caseStack.Pop();
                TryAddFolding(document, foldings, start.StartOffset, token.EndOffset, "CASE ... END");
            }
        }

        return foldings.OrderBy(f => f.StartOffset).ThenBy(f => f.EndOffset);
    }

    public void UpdateFoldings(FoldingManager manager, TextDocument document, IReadOnlyList<SqlToken> tokens)
    {
        IEnumerable<NewFolding> foldings = CreateNewFoldings(document, tokens);
        manager.UpdateFoldings(foldings, firstErrorOffset: -1);
    }

    private static void TryAddFolding(TextDocument document, List<NewFolding> foldings, int startOffset, int endOffset, string title)
    {
        if (startOffset < 0 || endOffset > document.TextLength || endOffset <= startOffset)
            return;

        DocumentLine startLine = document.GetLineByOffset(startOffset);
        DocumentLine endLine = document.GetLineByOffset(Math.Max(startOffset, endOffset - 1));
        if (startLine.LineNumber == endLine.LineNumber)
            return;

        foldings.Add(new NewFolding
        {
            StartOffset = startOffset,
            EndOffset = endOffset,
            Name = title,
        });
    }
}
