namespace VisualSqlArchitect.Nodes;

// ═════════════════════════════════════════════════════════════════════════════
// PIN DIRECTION
// ═════════════════════════════════════════════════════════════════════════════

public enum PinDirection { Input, Output }

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
    bool AllowMultiple = false     // input pins that accept variadic connections (AND, OR)
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
    Output
}

public enum NodeType
{
    // ── Data Source ───────────────────────────────────────────────────────────
    TableSource,

    // ── String Transforms ─────────────────────────────────────────────────────
    Upper, Lower, Trim,
    Substring,
    RegexMatch, RegexReplace,
    Concat, StringLength,

    // ── Math Transforms ───────────────────────────────────────────────────────
    Round, Abs, Ceil, Floor,
    Add, Subtract, Multiply, Divide, Modulo,

    // ── Aggregates ────────────────────────────────────────────────────────────
    CountStar, CountDistinct, Sum, Avg, Min, Max,

    // ── Type Cast ─────────────────────────────────────────────────────────────
    Cast,

    // ── Comparison ────────────────────────────────────────────────────────────
    Equals, NotEquals,
    GreaterThan, GreaterOrEqual,
    LessThan, LessOrEqual,
    Between, NotBetween,
    IsNull, IsNotNull,
    Like, NotLike,

    // ── Logic Gates ───────────────────────────────────────────────────────────
    And, Or, Not,

    // ── JSON ─────────────────────────────────────────────────────────────────
    JsonExtract,
    JsonValue,       // scalar text extraction (alias for JsonExtract with text output)
    JsonArrayLength,

    // ── Conditional ───────────────────────────────────────────────────────────
    Case,

    // ── Output ────────────────────────────────────────────────────────────────
    SelectOutput,
    WhereOutput
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
    public IEnumerable<PinDescriptor> InputPins  => Pins.Where(p => p.Direction == PinDirection.Input);
    public IEnumerable<PinDescriptor> OutputPins => Pins.Where(p => p.Direction == PinDirection.Output);
}

// ═════════════════════════════════════════════════════════════════════════════
// NODE PARAMETER  (canvas property panel value)
// ═════════════════════════════════════════════════════════════════════════════

public enum ParameterKind
{
    Text, Number, Enum, Boolean, CastType, JsonPath
}

/// <summary>
/// A configurable scalar value on a node — not wired via pins but set in the
/// node's property panel (e.g. ROUND precision, CAST target type, JSON path).
/// </summary>
public sealed record NodeParameter(
    string Name,
    ParameterKind Kind,
    string? DefaultValue = null,
    string? Description  = null,
    IReadOnlyList<string>? EnumValues = null   // for Kind == Enum
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
        _map.TryGetValue(type, out var def)
            ? def
            : throw new KeyNotFoundException($"No definition for NodeType.{type}");

    public static IReadOnlyCollection<NodeDefinition> All => _map.Values;

    public static IReadOnlyList<NodeDefinition> ByCategory(NodeCategory cat) =>
        _map.Values.Where(d => d.Category == cat).ToList();

    // ── Builder helpers ───────────────────────────────────────────────────────

    private static PinDescriptor In(string name, PinDataType type = PinDataType.Any,
        bool required = true, bool multi = false, string? desc = null) =>
        new(name, PinDirection.Input, type, required, desc, multi);

    private static PinDescriptor Out(string name, PinDataType type = PinDataType.Any,
        string? desc = null) =>
        new(name, PinDirection.Output, type, Description: desc);

    private static NodeParameter Param(string name, ParameterKind kind,
        string? def = null, string? desc = null, params string[] enums) =>
        new(name, kind, def, desc, enums.Length > 0 ? enums : null);

    // ─────────────────────────────────────────────────────────────────────────
    // DEFINITIONS
    // ─────────────────────────────────────────────────────────────────────────

    private static Dictionary<NodeType, NodeDefinition> BuildAll() => new()
    {
        // ── String transforms ─────────────────────────────────────────────────

        [NodeType.Upper] = new(NodeType.Upper, NodeCategory.StringTransform,
            "UPPER", "Converts text to uppercase",
            new[] { In("text", PinDataType.Text), Out("result", PinDataType.Text) },
            Array.Empty<NodeParameter>()),

        [NodeType.Lower] = new(NodeType.Lower, NodeCategory.StringTransform,
            "LOWER", "Converts text to lowercase",
            new[] { In("text", PinDataType.Text), Out("result", PinDataType.Text) },
            Array.Empty<NodeParameter>()),

        [NodeType.Trim] = new(NodeType.Trim, NodeCategory.StringTransform,
            "TRIM", "Removes leading and trailing whitespace",
            new[] { In("text", PinDataType.Text), Out("result", PinDataType.Text) },
            Array.Empty<NodeParameter>()),

        [NodeType.StringLength] = new(NodeType.StringLength, NodeCategory.StringTransform,
            "LENGTH", "Returns the character count of a string",
            new[] { In("text", PinDataType.Text), Out("length", PinDataType.Number) },
            Array.Empty<NodeParameter>()),

        [NodeType.Substring] = new(NodeType.Substring, NodeCategory.StringTransform,
            "SUBSTRING", "Extracts a portion of a string",
            new[]
            {
                In("text",   PinDataType.Text),
                In("start",  PinDataType.Number, required: false, desc: "1-based start position"),
                In("length", PinDataType.Number, required: false, desc: "Character count"),
                Out("result", PinDataType.Text)
            },
            new[]
            {
                Param("start",  ParameterKind.Number, "1",  "1-based start position"),
                Param("length", ParameterKind.Number, null, "Character count (omit for rest of string)")
            }),

        [NodeType.RegexMatch] = new(NodeType.RegexMatch, NodeCategory.StringTransform,
            "REGEX Match", "Tests if a column matches a regular expression",
            new[] { In("text", PinDataType.Text), Out("matches", PinDataType.Boolean) },
            new[] { Param("pattern", ParameterKind.Text, desc: "Regular expression pattern") }),

        [NodeType.Concat] = new(NodeType.Concat, NodeCategory.StringTransform,
            "CONCAT", "Concatenates two or more strings",
            new[]
            {
                In("a",         PinDataType.Any),
                In("b",         PinDataType.Any),
                In("separator", PinDataType.Text, required: false),
                Out("result", PinDataType.Text)
            },
            Array.Empty<NodeParameter>()),

        // ── Math transforms ───────────────────────────────────────────────────

        [NodeType.Round] = new(NodeType.Round, NodeCategory.MathTransform,
            "ROUND", "Rounds a numeric value to N decimal places",
            new[] { In("value", PinDataType.Number), In("precision", PinDataType.Number, required: false), Out("result", PinDataType.Number) },
            new[] { Param("precision", ParameterKind.Number, "0", "Decimal places") }),

        [NodeType.Abs] = new(NodeType.Abs, NodeCategory.MathTransform,
            "ABS", "Absolute value",
            new[] { In("value", PinDataType.Number), Out("result", PinDataType.Number) },
            Array.Empty<NodeParameter>()),

        [NodeType.Ceil] = new(NodeType.Ceil, NodeCategory.MathTransform,
            "CEIL", "Rounds up to the nearest integer",
            new[] { In("value", PinDataType.Number), Out("result", PinDataType.Number) },
            Array.Empty<NodeParameter>()),

        [NodeType.Floor] = new(NodeType.Floor, NodeCategory.MathTransform,
            "FLOOR", "Rounds down to the nearest integer",
            new[] { In("value", PinDataType.Number), Out("result", PinDataType.Number) },
            Array.Empty<NodeParameter>()),

        [NodeType.Add] = new(NodeType.Add, NodeCategory.MathTransform,
            "ADD (+)", "Adds two values",
            new[] { In("a", PinDataType.Number), In("b", PinDataType.Number), Out("result", PinDataType.Number) },
            Array.Empty<NodeParameter>()),

        [NodeType.Subtract] = new(NodeType.Subtract, NodeCategory.MathTransform,
            "SUBTRACT (−)", "Subtracts b from a",
            new[] { In("a", PinDataType.Number), In("b", PinDataType.Number), Out("result", PinDataType.Number) },
            Array.Empty<NodeParameter>()),

        [NodeType.Multiply] = new(NodeType.Multiply, NodeCategory.MathTransform,
            "MULTIPLY (×)", "Multiplies two values",
            new[] { In("a", PinDataType.Number), In("b", PinDataType.Number), Out("result", PinDataType.Number) },
            Array.Empty<NodeParameter>()),

        [NodeType.Divide] = new(NodeType.Divide, NodeCategory.MathTransform,
            "DIVIDE (÷)", "Divides a by b",
            new[] { In("a", PinDataType.Number), In("b", PinDataType.Number), Out("result", PinDataType.Number) },
            Array.Empty<NodeParameter>()),

        // ── Aggregates ────────────────────────────────────────────────────────

        [NodeType.CountStar] = new(NodeType.CountStar, NodeCategory.Aggregate,
            "COUNT(*)", "Counts all rows",
            new[] { Out("count", PinDataType.Number) },
            Array.Empty<NodeParameter>()),

        [NodeType.Sum] = new(NodeType.Sum, NodeCategory.Aggregate,
            "SUM", "Sums a numeric column",
            new[] { In("value", PinDataType.Number), Out("total", PinDataType.Number) },
            Array.Empty<NodeParameter>()),

        [NodeType.Avg] = new(NodeType.Avg, NodeCategory.Aggregate,
            "AVG", "Average of a numeric column",
            new[] { In("value", PinDataType.Number), Out("average", PinDataType.Number) },
            Array.Empty<NodeParameter>()),

        [NodeType.Min] = new(NodeType.Min, NodeCategory.Aggregate,
            "MIN", "Minimum value",
            new[] { In("value", PinDataType.Any), Out("minimum", PinDataType.Any) },
            Array.Empty<NodeParameter>()),

        [NodeType.Max] = new(NodeType.Max, NodeCategory.Aggregate,
            "MAX", "Maximum value",
            new[] { In("value", PinDataType.Any), Out("maximum", PinDataType.Any) },
            Array.Empty<NodeParameter>()),

        // ── Cast ──────────────────────────────────────────────────────────────

        [NodeType.Cast] = new(NodeType.Cast, NodeCategory.TypeCast,
            "CAST", "Converts a value to another data type",
            new[] { In("value", PinDataType.Any), Out("result", PinDataType.Any) },
            new[]
            {
                Param("targetType", ParameterKind.CastType, "Text",
                    "Target SQL type",
                    "Text", "Integer", "BigInt", "Decimal", "Float",
                    "Boolean", "Date", "DateTime", "Timestamp", "Uuid")
            }),

        // ── Comparisons ───────────────────────────────────────────────────────

        [NodeType.Equals] = new(NodeType.Equals, NodeCategory.Comparison,
            "Equals (=)", "Tests equality",
            new[] { In("left", PinDataType.Any), In("right", PinDataType.Any), Out("result", PinDataType.Boolean) },
            Array.Empty<NodeParameter>()),

        [NodeType.NotEquals] = new(NodeType.NotEquals, NodeCategory.Comparison,
            "Not Equals (<>)", "Tests inequality",
            new[] { In("left", PinDataType.Any), In("right", PinDataType.Any), Out("result", PinDataType.Boolean) },
            Array.Empty<NodeParameter>()),

        [NodeType.GreaterThan] = new(NodeType.GreaterThan, NodeCategory.Comparison,
            "Greater Than (>)", "left > right",
            new[] { In("left", PinDataType.Any), In("right", PinDataType.Any), Out("result", PinDataType.Boolean) },
            Array.Empty<NodeParameter>()),

        [NodeType.GreaterOrEqual] = new(NodeType.GreaterOrEqual, NodeCategory.Comparison,
            "Greater or Equal (≥)", "left >= right",
            new[] { In("left", PinDataType.Any), In("right", PinDataType.Any), Out("result", PinDataType.Boolean) },
            Array.Empty<NodeParameter>()),

        [NodeType.LessThan] = new(NodeType.LessThan, NodeCategory.Comparison,
            "Less Than (<)", "left < right",
            new[] { In("left", PinDataType.Any), In("right", PinDataType.Any), Out("result", PinDataType.Boolean) },
            Array.Empty<NodeParameter>()),

        [NodeType.LessOrEqual] = new(NodeType.LessOrEqual, NodeCategory.Comparison,
            "Less or Equal (≤)", "left <= right",
            new[] { In("left", PinDataType.Any), In("right", PinDataType.Any), Out("result", PinDataType.Boolean) },
            Array.Empty<NodeParameter>()),

        [NodeType.Between] = new(NodeType.Between, NodeCategory.Comparison,
            "BETWEEN", "Tests if a value is within an inclusive range",
            new[] { In("value", PinDataType.Any), In("low", PinDataType.Any), In("high", PinDataType.Any), Out("result", PinDataType.Boolean) },
            Array.Empty<NodeParameter>()),

        [NodeType.NotBetween] = new(NodeType.NotBetween, NodeCategory.Comparison,
            "NOT BETWEEN", "Tests if a value is outside a range",
            new[] { In("value", PinDataType.Any), In("low", PinDataType.Any), In("high", PinDataType.Any), Out("result", PinDataType.Boolean) },
            Array.Empty<NodeParameter>()),

        [NodeType.IsNull] = new(NodeType.IsNull, NodeCategory.Comparison,
            "IS NULL", "Tests if a value is null",
            new[] { In("value", PinDataType.Any), Out("result", PinDataType.Boolean) },
            Array.Empty<NodeParameter>()),

        [NodeType.IsNotNull] = new(NodeType.IsNotNull, NodeCategory.Comparison,
            "IS NOT NULL", "Tests if a value is not null",
            new[] { In("value", PinDataType.Any), Out("result", PinDataType.Boolean) },
            Array.Empty<NodeParameter>()),

        [NodeType.Like] = new(NodeType.Like, NodeCategory.Comparison,
            "LIKE", "Pattern matching with wildcards",
            new[] { In("text", PinDataType.Text), Out("result", PinDataType.Boolean) },
            new[] { Param("pattern", ParameterKind.Text, desc: "e.g. '%suffix' or 'prefix%'") }),

        // ── Logic gates ───────────────────────────────────────────────────────

        [NodeType.And] = new(NodeType.And, NodeCategory.LogicGate,
            "AND", "All conditions must be true",
            new[]
            {
                In("conditions", PinDataType.Boolean, required: true, multi: true,
                   desc: "Connect two or more boolean expressions"),
                Out("result", PinDataType.Boolean)
            },
            Array.Empty<NodeParameter>()),

        [NodeType.Or] = new(NodeType.Or, NodeCategory.LogicGate,
            "OR", "At least one condition must be true",
            new[]
            {
                In("conditions", PinDataType.Boolean, required: true, multi: true,
                   desc: "Connect two or more boolean expressions"),
                Out("result", PinDataType.Boolean)
            },
            Array.Empty<NodeParameter>()),

        [NodeType.Not] = new(NodeType.Not, NodeCategory.LogicGate,
            "NOT", "Negates a boolean expression",
            new[] { In("condition", PinDataType.Boolean), Out("result", PinDataType.Boolean) },
            Array.Empty<NodeParameter>()),

        // ── JSON ─────────────────────────────────────────────────────────────

        [NodeType.JsonExtract] = new(NodeType.JsonExtract, NodeCategory.Json,
            "JSON Extract", "Extracts a value from a JSON column by path",
            new[] { In("json", PinDataType.Json), Out("value", PinDataType.Any) },
            new[]
            {
                Param("path", ParameterKind.JsonPath, desc: "JSON path (e.g. $.address.city)"),
                Param("outputType", ParameterKind.Enum, "Text", "Cast extracted value to type",
                      "Text", "Number", "Boolean", "Json")
            }),

        [NodeType.JsonArrayLength] = new(NodeType.JsonArrayLength, NodeCategory.Json,
            "JSON Array Length", "Returns the number of elements in a JSON array",
            new[] { In("json", PinDataType.Json), Out("length", PinDataType.Number) },
            new[] { Param("path", ParameterKind.JsonPath, "$", "Path to the array") }),
    };

    // Overload accepting varargs for pins (convenience)
    private static NodeDefinition new_(NodeType t, NodeCategory c, string name, string desc,
        PinDescriptor[] pins, NodeParameter[] ps) =>
        new(t, c, name, desc, pins, ps);
}
