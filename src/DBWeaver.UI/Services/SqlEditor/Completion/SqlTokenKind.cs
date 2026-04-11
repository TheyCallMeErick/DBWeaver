namespace DBWeaver.UI.Services.SqlEditor;

public enum SqlTokenKind
{
    Keyword,
    Identifier,
    QuotedIdentifier,
    StringLiteral,
    NumericLiteral,
    Operator,
    Punctuation,
    LineComment,
    BlockComment,
    Whitespace,
    Unknown,
}
