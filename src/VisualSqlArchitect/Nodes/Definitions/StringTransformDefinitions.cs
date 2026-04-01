namespace VisualSqlArchitect.Nodes.Definitions;

using VisualSqlArchitect.Nodes;
using static NodeDefinitionHelpers;

/// <summary>
/// String transformation node definitions.
/// Defines nodes for text manipulation functions.
/// </summary>
public static class StringTransformDefinitions
{
    public static readonly NodeDefinition Upper = new(
        NodeType.Upper,
        NodeCategory.StringTransform,
        "UPPER",
        "Converts text to uppercase",
        [In("text", PinDataType.Text), Out("result", PinDataType.Text)],
        []
    );

    public static readonly NodeDefinition Lower = new(
        NodeType.Lower,
        NodeCategory.StringTransform,
        "LOWER",
        "Converts text to lowercase",
        [In("text", PinDataType.Text), Out("result", PinDataType.Text)],
        []
    );

    public static readonly NodeDefinition Trim = new(
        NodeType.Trim,
        NodeCategory.StringTransform,
        "TRIM",
        "Removes leading and trailing whitespace",
        [In("text", PinDataType.Text), Out("result", PinDataType.Text)],
        []
    );

    public static readonly NodeDefinition StringLength = new(
        NodeType.StringLength,
        NodeCategory.StringTransform,
        "LENGTH",
        "Returns the character count of a string",
        [In("text", PinDataType.Text), Out("length", PinDataType.Number)],
        []
    );

    public static readonly NodeDefinition Substring = new(
        NodeType.Substring,
        NodeCategory.StringTransform,
        "SUBSTRING",
        "Extracts a portion of a string",
        [
            In("text", PinDataType.Text),
            In("start", PinDataType.Number, required: false, desc: "1-based start position"),
            In("length", PinDataType.Number, required: false, desc: "Character count"),
            Out("result", PinDataType.Text),
        ],
        [
            new(
                "start",
                VisualSqlArchitect.Nodes.ParameterKind.Number,
                "1",
                "1-based start position"
            ),
            new(
                "length",
                VisualSqlArchitect.Nodes.ParameterKind.Number,
                null,
                "Character count (omit for rest of string)"
            ),
        ]
    );

    public static readonly NodeDefinition RegexMatch = new(
        NodeType.RegexMatch,
        NodeCategory.StringTransform,
        "REGEX Match",
        "Tests if a column matches a regular expression",
        [In("text", PinDataType.Text), Out("matches", PinDataType.Boolean)],
        [
            new(
                "pattern",
                VisualSqlArchitect.Nodes.ParameterKind.Text,
                null,
                "Regular expression pattern"
            ),
        ]
    );

    public static readonly NodeDefinition RegexReplace = new(
        NodeType.RegexReplace,
        NodeCategory.StringTransform,
        "REGEX Replace",
        "Replaces matches of a regular expression with a replacement string",
        [In("text", PinDataType.Text), Out("result", PinDataType.Text)],
        [
            new(
                "pattern",
                VisualSqlArchitect.Nodes.ParameterKind.Text,
                null,
                "Regular expression pattern"
            ),
            new(
                "replacement",
                VisualSqlArchitect.Nodes.ParameterKind.Text,
                "",
                "Replacement string (\\1, \\2 for backreferences)"
            ),
        ]
    );

    public static readonly NodeDefinition RegexExtract = new(
        NodeType.RegexExtract,
        NodeCategory.StringTransform,
        "REGEX Extract",
        "Extracts the first match (or first capture group) of a regular expression",
        [In("text", PinDataType.Text), Out("result", PinDataType.Text)],
        [
            new(
                "pattern",
                VisualSqlArchitect.Nodes.ParameterKind.Text,
                null,
                "Regular expression pattern (use a capture group for group extraction)"
            ),
        ]
    );

    public static readonly NodeDefinition Replace = new(
        NodeType.Replace,
        NodeCategory.StringTransform,
        "REPLACE",
        "Replaces all occurrences of a literal substring within a value",
        [In("value", PinDataType.Text), Out("result", PinDataType.Text)],
        [
            new(
                "search",
                VisualSqlArchitect.Nodes.ParameterKind.Text,
                null,
                "Literal text to search for"
            ),
            new(
                "replacement",
                VisualSqlArchitect.Nodes.ParameterKind.Text,
                "",
                "Replacement text (empty to delete matches)"
            ),
        ]
    );

    public static readonly NodeDefinition Concat = new(
        NodeType.Concat,
        NodeCategory.StringTransform,
        "CONCAT",
        "Concatenates two or more strings",
        [
            In("a", PinDataType.Text),
            In("b", PinDataType.Text),
            In("separator", PinDataType.Text, required: false),
            Out("result", PinDataType.Text),
        ],
        []
    );
}
