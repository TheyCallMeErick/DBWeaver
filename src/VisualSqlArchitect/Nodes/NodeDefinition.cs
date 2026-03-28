namespace VisualSqlArchitect.Nodes;

// ═════════════════════════════════════════════════════════════════════════════
// PIN DIRECTION
// ═════════════════════════════════════════════════════════════════════════════

public enum PinDirection
{
    Input,
    Output,
}

// ═════════════════════════════════════════════════════════════════════════════
// PIN DESCRIPTOR  (static metadata, part of node definition)
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Static descriptor for a single connection slot on a node.
/// The canvas renders one connector per PinDescriptor.
///
/// For a table DataSource node, one PinDescriptor is generated per column,
/// all of direction=Output.
/// </summary>
public sealed record PinDescriptor(
    string Name,
    PinDirection Direction,
    PinDataType DataType,
    bool IsRequired = true,
    string? Description = null,
    bool AllowMultiple = false // input pins that accept variadic connections (AND, OR)
);

// ═════════════════════════════════════════════════════════════════════════════
// NODE CATEGORY + TYPE
// ═════════════════════════════════════════════════════════════════════════════

public enum NodeCategory
{
    DataSource,
    StringTransform,
    MathTransform,
    TypeCast,
    Comparison,
    LogicGate,
    Json,
    Aggregate,
    Conditional,
    ResultModifier,
    Output,
    Literal,
}

public enum NodeType
{
    // ── Data Source ───────────────────────────────────────────────────────────
    TableSource,
    Alias,

    // ── String Transforms ─────────────────────────────────────────────────────
    Upper,
    Lower,
    Trim,
    Substring,
    RegexMatch,
    RegexReplace,
    RegexExtract,
    Concat,
    StringLength,
    Replace,

    // ── Math Transforms ───────────────────────────────────────────────────────
    Round,
    Abs,
    Ceil,
    Floor,
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,

    // ── Aggregates ────────────────────────────────────────────────────────────
    CountStar,
    CountDistinct,
    Sum,
    Avg,
    Min,
    Max,

    // ── Type Cast ─────────────────────────────────────────────────────────────
    Cast,

    // ── Comparison ────────────────────────────────────────────────────────────
    Equals,
    NotEquals,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
    Between,
    NotBetween,
    IsNull,
    IsNotNull,
    Like,
    NotLike,

    // ── Logic Gates ───────────────────────────────────────────────────────────
    And,
    Or,
    Not,

    // ── JSON ─────────────────────────────────────────────────────────────────
    JsonExtract,
    JsonValue, // scalar text extraction (alias for JsonExtract with text output)
    JsonArrayLength,

    // ── Conditional ───────────────────────────────────────────────────────────
    Case,
    NullFill, // COALESCE(value, fallback) — replaces NULL with a default
    EmptyFill, // COALESCE(NULLIF(TRIM(value),''), fallback) — replaces NULL or empty
    ValueMap, // CASE WHEN value = src THEN dst ELSE value END

    // ── Literal / Value nodes ───────────────────────────────────────────────
    ValueNumber,
    ValueString,
    ValueDateTime,
    ValueBoolean,

    // ── Result Modifiers ──────────────────────────────────────────────────────
    Top, // LIMIT/TOP - restricts the number of rows returned
    CompileWhere, // Compiles multiple boolean conditions into a WHERE clause

    // ── Output ────────────────────────────────────────────────────────────────
    ColumnList, // Aggregates multiple columns for SELECT
    SelectOutput,
    WhereOutput,
    ResultOutput,

    // ── Export ────────────────────────────────────────────────────────────────
    HtmlExport,
    JsonExport,
    CsvExport,
    ExcelExport,
}

// ═════════════════════════════════════════════════════════════════════════════
// NODE DEFINITION  (static registry entry — one per NodeType)
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Immutable descriptor of a node class — its category, display name, pins,
/// and which parameters it exposes in the canvas property panel.
/// The canvas uses this to render the node shell and validate connections.
/// </summary>
public sealed record NodeDefinition(
    NodeType Type,
    NodeCategory Category,
    string DisplayName,
    string Description,
    IReadOnlyList<PinDescriptor> Pins,
    IReadOnlyList<NodeParameter> Parameters
)
{
    public IEnumerable<PinDescriptor> InputPins =>
        Pins.Where(p => p.Direction == PinDirection.Input);
    public IEnumerable<PinDescriptor> OutputPins =>
        Pins.Where(p => p.Direction == PinDirection.Output);
}

// ═════════════════════════════════════════════════════════════════════════════
// NODE PARAMETER  (canvas property panel value)
// ═════════════════════════════════════════════════════════════════════════════

public enum ParameterKind
{
    Text,
    Number,
    Enum,
    Boolean,
    CastType,
    JsonPath,
    DateTime,
    Date,
}

/// <summary>
/// A configurable scalar value on a node — not wired via pins but set in the
/// node's property panel (e.g. ROUND precision, CAST target type, JSON path).
/// </summary>
public sealed record NodeParameter(
    string Name,
    ParameterKind Kind,
    string? DefaultValue = null,
    string? Description = null,
    IReadOnlyList<string>? EnumValues = null // for Kind == Enum
);

// ═════════════════════════════════════════════════════════════════════════════
// NODE DEFINITION REGISTRY  (all canonical node types)
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Static catalog of all Atomic Node definitions.
/// The canvas sidebar queries this to populate the node picker.
/// </summary>
public static class NodeDefinitionRegistry
{
    private static readonly Dictionary<NodeType, NodeDefinition> _map = BuildAll();

    public static NodeDefinition Get(NodeType type) =>
        _map.TryGetValue(type, out NodeDefinition? def)
            ? def
            : throw new KeyNotFoundException($"No definition for NodeType.{type}");

    public static IReadOnlyCollection<NodeDefinition> All => _map.Values;

    public static IReadOnlyList<NodeDefinition> ByCategory(NodeCategory cat) =>
        _map.Values.Where(d => d.Category == cat).ToList();

    // ── Builder helpers ───────────────────────────────────────────────────────

    private static PinDescriptor In(
        string name,
        PinDataType type = PinDataType.Any,
        bool required = true,
        bool multi = false,
        string? desc = null
    ) => new(name, PinDirection.Input, type, required, desc, multi);

    private static PinDescriptor Out(
        string name,
        PinDataType type = PinDataType.Any,
        string? desc = null
    ) => new(name, PinDirection.Output, type, Description: desc);

    private static NodeParameter Param(
        string name,
        ParameterKind kind,
        string? def = null,
        string? desc = null,
        params string[] enums
    ) => new(name, kind, def, desc, enums.Length > 0 ? enums : null);

    // ─────────────────────────────────────────────────────────────────────────
    // DEFINITIONS
    // ─────────────────────────────────────────────────────────────────────────

    private static Dictionary<NodeType, NodeDefinition> BuildAll() =>
        new()
        {
            // ── Data Source ───────────────────────────────────────────────────────

            [NodeType.Alias] = new(
                NodeType.Alias,
                NodeCategory.DataSource,
                "ALIAS (AS)",
                "Renames a column or expression with AS",
                [In("expression", PinDataType.Any), Out("result", PinDataType.Any)],
                [Param("alias", ParameterKind.Text, null, "New alias name (e.g. total_price)")]
            ),

            // ── String transforms ─────────────────────────────────────────────────

            [NodeType.Upper] = new(
                NodeType.Upper,
                NodeCategory.StringTransform,
                "UPPER",
                "Converts text to uppercase",
                [In("text", PinDataType.Text), Out("result", PinDataType.Text)],
                []
            ),

            [NodeType.Lower] = new(
                NodeType.Lower,
                NodeCategory.StringTransform,
                "LOWER",
                "Converts text to lowercase",
                [In("text", PinDataType.Text), Out("result", PinDataType.Text)],
                []
            ),

            [NodeType.Trim] = new(
                NodeType.Trim,
                NodeCategory.StringTransform,
                "TRIM",
                "Removes leading and trailing whitespace",
                [In("text", PinDataType.Text), Out("result", PinDataType.Text)],
                []
            ),

            [NodeType.StringLength] = new(
                NodeType.StringLength,
                NodeCategory.StringTransform,
                "LENGTH",
                "Returns the character count of a string",
                [In("text", PinDataType.Text), Out("length", PinDataType.Number)],
                []
            ),

            [NodeType.Substring] = new(
                NodeType.Substring,
                NodeCategory.StringTransform,
                "SUBSTRING",
                "Extracts a portion of a string",
                [
                    In("text", PinDataType.Text),
                    In(
                        "start",
                        PinDataType.Number,
                        required: false,
                        desc: "1-based start position"
                    ),
                    In("length", PinDataType.Number, required: false, desc: "Character count"),
                    Out("result", PinDataType.Text),
                ],
                [
                    Param("start", ParameterKind.Number, "1", "1-based start position"),
                    Param(
                        "length",
                        ParameterKind.Number,
                        null,
                        "Character count (omit for rest of string)"
                    ),
                ]
            ),

            [NodeType.RegexMatch] = new(
                NodeType.RegexMatch,
                NodeCategory.StringTransform,
                "REGEX Match",
                "Tests if a column matches a regular expression",
                [In("text", PinDataType.Text), Out("matches", PinDataType.Boolean)],
                [Param("pattern", ParameterKind.Text, desc: "Regular expression pattern")]
            ),

            [NodeType.RegexReplace] = new(
                NodeType.RegexReplace,
                NodeCategory.StringTransform,
                "REGEX Replace",
                "Replaces matches of a regular expression with a replacement string",
                [In("text", PinDataType.Text), Out("result", PinDataType.Text)],
                [
                    Param("pattern", ParameterKind.Text, desc: "Regular expression pattern"),
                    Param(
                        "replacement",
                        ParameterKind.Text,
                        "",
                        "Replacement string (\\1, \\2 for backreferences)"
                    ),
                ]
            ),

            [NodeType.RegexExtract] = new(
                NodeType.RegexExtract,
                NodeCategory.StringTransform,
                "REGEX Extract",
                "Extracts the first match (or first capture group) of a regular expression",
                [In("text", PinDataType.Text), Out("result", PinDataType.Text)],
                [
                    Param(
                        "pattern",
                        ParameterKind.Text,
                        desc: "Regular expression pattern (use a capture group for group extraction)"
                    ),
                ]
            ),

            [NodeType.Replace] = new(
                NodeType.Replace,
                NodeCategory.StringTransform,
                "REPLACE",
                "Replaces all occurrences of a literal substring within a value",
                [In("value", PinDataType.Text), Out("result", PinDataType.Text)],
                [
                    Param("search", ParameterKind.Text, desc: "Literal text to search for"),
                    Param(
                        "replacement",
                        ParameterKind.Text,
                        "",
                        "Replacement text (empty to delete matches)"
                    ),
                ]
            ),

            [NodeType.Concat] = new(
                NodeType.Concat,
                NodeCategory.StringTransform,
                "CONCAT",
                "Concatenates two or more strings",
                [
                    In("a", PinDataType.Any),
                    In("b", PinDataType.Any),
                    In("separator", PinDataType.Text, required: false),
                    Out("result", PinDataType.Text),
                ],
                []
            ),

            // ── Math transforms ───────────────────────────────────────────────────

            [NodeType.Round] = new(
                NodeType.Round,
                NodeCategory.MathTransform,
                "ROUND",
                "Rounds a numeric value to N decimal places",
                [
                    In("value", PinDataType.Number),
                    In("precision", PinDataType.Number, required: false),
                    Out("result", PinDataType.Number),
                ],
                [Param("precision", ParameterKind.Number, "0", "Decimal places")]
            ),

            [NodeType.Abs] = new(
                NodeType.Abs,
                NodeCategory.MathTransform,
                "ABS",
                "Absolute value",
                [In("value", PinDataType.Number), Out("result", PinDataType.Number)],
                []
            ),

            [NodeType.Ceil] = new(
                NodeType.Ceil,
                NodeCategory.MathTransform,
                "CEIL",
                "Rounds up to the nearest integer",
                [In("value", PinDataType.Number), Out("result", PinDataType.Number)],
                []
            ),

            [NodeType.Floor] = new(
                NodeType.Floor,
                NodeCategory.MathTransform,
                "FLOOR",
                "Rounds down to the nearest integer",
                [In("value", PinDataType.Number), Out("result", PinDataType.Number)],
                []
            ),

            [NodeType.Add] = new(
                NodeType.Add,
                NodeCategory.MathTransform,
                "ADD (+)",
                "Adds two values",
                [
                    In("a", PinDataType.Number),
                    In("b", PinDataType.Number),
                    Out("result", PinDataType.Number),
                ],
                []
            ),

            [NodeType.Subtract] = new(
                NodeType.Subtract,
                NodeCategory.MathTransform,
                "SUBTRACT (−)",
                "Subtracts b from a",
                [
                    In("a", PinDataType.Number),
                    In("b", PinDataType.Number),
                    Out("result", PinDataType.Number),
                ],
                []
            ),

            [NodeType.Multiply] = new(
                NodeType.Multiply,
                NodeCategory.MathTransform,
                "MULTIPLY (×)",
                "Multiplies two values",
                [
                    In("a", PinDataType.Number),
                    In("b", PinDataType.Number),
                    Out("result", PinDataType.Number),
                ],
                []
            ),

            [NodeType.Divide] = new(
                NodeType.Divide,
                NodeCategory.MathTransform,
                "DIVIDE (÷)",
                "Divides a by b",
                [
                    In("a", PinDataType.Number),
                    In("b", PinDataType.Number),
                    Out("result", PinDataType.Number),
                ],
                []
            ),

            // ── Aggregates ────────────────────────────────────────────────────────

            [NodeType.CountStar] = new(
                NodeType.CountStar,
                NodeCategory.Aggregate,
                "COUNT(*)",
                "Counts all rows",
                [Out("count", PinDataType.Number)],
                []
            ),

            [NodeType.Sum] = new(
                NodeType.Sum,
                NodeCategory.Aggregate,
                "SUM",
                "Sums a numeric column",
                [In("value", PinDataType.Number), Out("total", PinDataType.Number)],
                []
            ),

            [NodeType.Avg] = new(
                NodeType.Avg,
                NodeCategory.Aggregate,
                "AVG",
                "Average of a numeric column",
                [In("value", PinDataType.Number), Out("average", PinDataType.Number)],
                []
            ),

            [NodeType.Min] = new(
                NodeType.Min,
                NodeCategory.Aggregate,
                "MIN",
                "Minimum value",
                [In("value", PinDataType.Any), Out("minimum", PinDataType.Any)],
                []
            ),

            [NodeType.Max] = new(
                NodeType.Max,
                NodeCategory.Aggregate,
                "MAX",
                "Maximum value",
                [In("value", PinDataType.Any), Out("maximum", PinDataType.Any)],
                []
            ),

            // ── Cast ──────────────────────────────────────────────────────────────

            [NodeType.Cast] = new(
                NodeType.Cast,
                NodeCategory.TypeCast,
                "CAST",
                "Converts a value to another data type",
                [In("value", PinDataType.Any), Out("result", PinDataType.Any)],
                [
                    Param(
                        "targetType",
                        ParameterKind.CastType,
                        "Text",
                        "Target SQL type",
                        "Text",
                        "Integer",
                        "BigInt",
                        "Decimal",
                        "Float",
                        "Boolean",
                        "Date",
                        "DateTime",
                        "Timestamp",
                        "Uuid"
                    ),
                ]
            ),

            // ── Comparisons ───────────────────────────────────────────────────────

            [NodeType.Equals] = new(
                NodeType.Equals,
                NodeCategory.Comparison,
                "Equals (=)",
                "Tests equality",
                [
                    In("left", PinDataType.Any),
                    In("right", PinDataType.Any),
                    Out("result", PinDataType.Boolean),
                ],
                []
            ),

            [NodeType.NotEquals] = new(
                NodeType.NotEquals,
                NodeCategory.Comparison,
                "Not Equals (<>)",
                "Tests inequality",
                [
                    In("left", PinDataType.Any),
                    In("right", PinDataType.Any),
                    Out("result", PinDataType.Boolean),
                ],
                []
            ),

            [NodeType.GreaterThan] = new(
                NodeType.GreaterThan,
                NodeCategory.Comparison,
                "Greater Than (>)",
                "left > right",
                [
                    In("left", PinDataType.Any),
                    In("right", PinDataType.Any),
                    Out("result", PinDataType.Boolean),
                ],
                []
            ),

            [NodeType.GreaterOrEqual] = new(
                NodeType.GreaterOrEqual,
                NodeCategory.Comparison,
                "Greater or Equal (≥)",
                "left >= right",
                [
                    In("left", PinDataType.Any),
                    In("right", PinDataType.Any),
                    Out("result", PinDataType.Boolean),
                ],
                []
            ),

            [NodeType.LessThan] = new(
                NodeType.LessThan,
                NodeCategory.Comparison,
                "Less Than (<)",
                "left < right",
                [
                    In("left", PinDataType.Any),
                    In("right", PinDataType.Any),
                    Out("result", PinDataType.Boolean),
                ],
                []
            ),

            [NodeType.LessOrEqual] = new(
                NodeType.LessOrEqual,
                NodeCategory.Comparison,
                "Less or Equal (≤)",
                "left <= right",
                [
                    In("left", PinDataType.Any),
                    In("right", PinDataType.Any),
                    Out("result", PinDataType.Boolean),
                ],
                []
            ),

            [NodeType.Between] = new(
                NodeType.Between,
                NodeCategory.Comparison,
                "BETWEEN",
                "Tests if a value is within an inclusive range",
                [
                    In("value", PinDataType.Any),
                    In("low", PinDataType.Any),
                    In("high", PinDataType.Any),
                    Out("result", PinDataType.Boolean),
                ],
                []
            ),

            [NodeType.NotBetween] = new(
                NodeType.NotBetween,
                NodeCategory.Comparison,
                "NOT BETWEEN",
                "Tests if a value is outside a range",
                [
                    In("value", PinDataType.Any),
                    In("low", PinDataType.Any),
                    In("high", PinDataType.Any),
                    Out("result", PinDataType.Boolean),
                ],
                []
            ),

            [NodeType.IsNull] = new(
                NodeType.IsNull,
                NodeCategory.Comparison,
                "IS NULL",
                "Tests if a value is null",
                [In("value", PinDataType.Any), Out("result", PinDataType.Boolean)],
                []
            ),

            [NodeType.IsNotNull] = new(
                NodeType.IsNotNull,
                NodeCategory.Comparison,
                "IS NOT NULL",
                "Tests if a value is not null",
                [In("value", PinDataType.Any), Out("result", PinDataType.Boolean)],
                []
            ),

            [NodeType.Like] = new(
                NodeType.Like,
                NodeCategory.Comparison,
                "LIKE",
                "Pattern matching with wildcards",
                [In("text", PinDataType.Text), Out("result", PinDataType.Boolean)],
                [Param("pattern", ParameterKind.Text, desc: "e.g. '%suffix' or 'prefix%'")]
            ),

            // ── Logic gates ───────────────────────────────────────────────────────

            [NodeType.And] = new(
                NodeType.And,
                NodeCategory.LogicGate,
                "AND",
                "All conditions must be true",
                [
                    In("conditions", PinDataType.Boolean, required: false, multi: true),
                    Out("result", PinDataType.Boolean),
                ],
                []
            ),

            [NodeType.Or] = new(
                NodeType.Or,
                NodeCategory.LogicGate,
                "OR",
                "At least one condition must be true",
                [
                    In("conditions", PinDataType.Boolean, required: false, multi: true),
                    Out("result", PinDataType.Boolean),
                ],
                []
            ),

            [NodeType.Not] = new(
                NodeType.Not,
                NodeCategory.LogicGate,
                "NOT",
                "Negates a boolean expression",
                [In("condition", PinDataType.Boolean), Out("result", PinDataType.Boolean)],
                []
            ),

            // ── JSON ─────────────────────────────────────────────────────────────

            [NodeType.JsonExtract] = new(
                NodeType.JsonExtract,
                NodeCategory.Json,
                "JSON Extract",
                "Extracts a value from a JSON column by path",
                [In("json", PinDataType.Json), Out("value", PinDataType.Any)],
                [
                    Param("path", ParameterKind.JsonPath, desc: "JSON path (e.g. $.address.city)"),
                    Param(
                        "outputType",
                        ParameterKind.Enum,
                        "Text",
                        "Cast extracted value to type",
                        "Text",
                        "Number",
                        "Boolean",
                        "Json"
                    ),
                ]
            ),

            [NodeType.JsonArrayLength] = new(
                NodeType.JsonArrayLength,
                NodeCategory.Json,
                "JSON Array Length",
                "Returns the number of elements in a JSON array",
                [In("json", PinDataType.Json), Out("length", PinDataType.Number)],
                [Param("path", ParameterKind.JsonPath, "$", "Path to the array")]
            ),

            // ── Value Transform (Conditional) ─────────────────────────────────────

            [NodeType.NullFill] = new(
                NodeType.NullFill,
                NodeCategory.Conditional,
                "NULL Fill",
                "Returns a fallback value when input is NULL — COALESCE(value, fallback)",
                [In("value", PinDataType.Any), Out("result", PinDataType.Any)],
                [Param("fallback", ParameterKind.Text, "", "Value returned when input is NULL")]
            ),

            [NodeType.EmptyFill] = new(
                NodeType.EmptyFill,
                NodeCategory.Conditional,
                "Empty Fill",
                "Returns a fallback when input is NULL or an empty/whitespace string",
                [In("value", PinDataType.Text), Out("result", PinDataType.Text)],
                [
                    Param(
                        "fallback",
                        ParameterKind.Text,
                        "",
                        "Value returned when input is NULL or empty"
                    ),
                ]
            ),

            [NodeType.ValueMap] = new(
                NodeType.ValueMap,
                NodeCategory.Conditional,
                "Value Map",
                "Maps a specific input value to a new output value — CASE WHEN value = src THEN dst ELSE passthrough",
                [In("value", PinDataType.Any), Out("result", PinDataType.Any)],
                [
                    Param("src", ParameterKind.Text, desc: "Input value to match"),
                    Param("dst", ParameterKind.Text, desc: "Output value when matched"),
                ]
            ),

            // ── Literal / Value nodes ───────────────────────────────────────────
            [NodeType.ValueNumber] = new(
                NodeType.ValueNumber,
                NodeCategory.Literal,
                "Number",
                "Numeric literal value",
                [Out("result", PinDataType.Number)],
                [Param("value", ParameterKind.Number, "0", "Numeric value")]
            ),

            [NodeType.ValueString] = new(
                NodeType.ValueString,
                NodeCategory.Literal,
                "String",
                "Text literal value",
                [Out("result", PinDataType.Text)],
                [Param("value", ParameterKind.Text, "", "String value")]
            ),

            [NodeType.ValueDateTime] = new(
                NodeType.ValueDateTime,
                NodeCategory.Literal,
                "Date/DateTime",
                "Date or DateTime literal value",
                [Out("result", PinDataType.DateTime)],
                [
                    Param(
                        "value",
                        ParameterKind.DateTime,
                        "",
                        "Date or DateTime literal (ISO 8601) or leave empty for NULL"
                    ),
                ]
            ),

            [NodeType.ValueBoolean] = new(
                NodeType.ValueBoolean,
                NodeCategory.Literal,
                "Boolean",
                "Boolean literal value (true/false)",
                [Out("result", PinDataType.Boolean)],
                [Param("value", ParameterKind.Enum, "true", "Boolean value", "true", "false")]
            ),

            // ── Result Modifiers ──────────────────────────────────────────────────

            [NodeType.Top] = new(
                NodeType.Top,
                NodeCategory.ResultModifier,
                "TOP / LIMIT",
                "Limits the number of rows returned from a query",
                [
                    In(
                        "count",
                        PinDataType.Number,
                        required: false,
                        desc: "Connect a Number node or set manually"
                    ),
                    Out("result", PinDataType.Any),
                ],
                [Param("count", ParameterKind.Number, "100", "Maximum number of rows to return")]
            ),

            [NodeType.CompileWhere] = new(
                NodeType.CompileWhere,
                NodeCategory.ResultModifier,
                "COMPILE WHERE",
                "Combines multiple boolean conditions into a WHERE clause",
                [
                    In(
                        "conditions",
                        PinDataType.Boolean,
                        required: false,
                        multi: true,
                        desc: "Connect boolean comparisons/expressions"
                    ),
                    Out(
                        "result",
                        PinDataType.Boolean,
                        desc: "Connect to ResultOutput to generate WHERE clause"
                    ),
                ],
                []
            ),

            // ── Output ────────────────────────────────────────────────────

            [NodeType.ColumnList] = new(
                NodeType.ColumnList,
                NodeCategory.Output,
                "Column List",
                "Aggregates multiple columns and defines their order",
                [
                    // Input pins (col_1, col_2, …) are added dynamically by NodeViewModel.
                    // Only the output pin is declared statically.
                    Out(
                        "result",
                        PinDataType.Any,
                        desc: "Connect to ResultOutput to define columns for SELECT"
                    ),
                ],
                []
            ),

            [NodeType.ResultOutput] = new(
                NodeType.ResultOutput,
                NodeCategory.Output,
                "Result Output",
                "Defines the final SELECT output",
                [
                    In(
                        "top",
                        PinDataType.Any,
                        required: false,
                        desc: "Connect a TOP / LIMIT node to restrict the number of rows"
                    ),
                    In(
                        "where",
                        PinDataType.Boolean,
                        required: false,
                        desc: "Connect a compiled WHERE condition"
                    ),
                    In(
                        "columns",
                        PinDataType.Any,
                        required: false,
                        desc: "Connect ColumnList output to include columns in SELECT"
                    ),
                    Out(
                        "result",
                        PinDataType.Any,
                        desc: "Connect to an Export node to generate an output file"
                    ),
                ],
                [
                    Param(
                        "file_name",
                        ParameterKind.Text,
                        "export.html",
                        "Destination file name or path (e.g. report.html)"
                    ),
                ]
            ),

            [NodeType.JsonExport] = new(
                NodeType.JsonExport,
                NodeCategory.Output,
                "JSON Export",
                "Exports the result schema as a JSON template file",
                [
                    In(
                        "query",
                        PinDataType.Any,
                        required: true,
                        desc: "Connect from a Result Output node"
                    ),
                ],
                [
                    Param(
                        "file_name",
                        ParameterKind.Text,
                        "export.json",
                        "Destination file name or path (e.g. data.json)"
                    ),
                ]
            ),

            [NodeType.CsvExport] = new(
                NodeType.CsvExport,
                NodeCategory.Output,
                "CSV Export",
                "Exports the result schema as a CSV file with a header row",
                [
                    In(
                        "query",
                        PinDataType.Any,
                        required: true,
                        desc: "Connect from a Result Output node"
                    ),
                ],
                [
                    Param(
                        "file_name",
                        ParameterKind.Text,
                        "export.csv",
                        "Destination file name or path (e.g. data.csv)"
                    ),
                    Param(
                        "delimiter",
                        ParameterKind.Enum,
                        ",",
                        "Column delimiter",
                        ",",
                        ";",
                        "\\t",
                        "|"
                    ),
                ]
            ),

            [NodeType.ExcelExport] = new(
                NodeType.ExcelExport,
                NodeCategory.Output,
                "Excel Export (XLSX)",
                "Exports the result schema as an Excel workbook with a header row",
                [
                    In(
                        "query",
                        PinDataType.Any,
                        required: true,
                        desc: "Connect from a Result Output node"
                    ),
                ],
                [
                    Param(
                        "file_name",
                        ParameterKind.Text,
                        "export.xlsx",
                        "Destination file name or path (e.g. report.xlsx)"
                    ),
                    Param(
                        "sheet_name",
                        ParameterKind.Text,
                        "Sheet1",
                        "Name of the first worksheet (e.g. Results)"
                    ),
                ]
            ),
        };

    // Overload accepting varargs for pins (convenience)
    private static NodeDefinition new_(
        NodeType t,
        NodeCategory c,
        string name,
        string desc,
        PinDescriptor[] pins,
        NodeParameter[] ps
    ) => new(t, c, name, desc, pins, ps);
}
