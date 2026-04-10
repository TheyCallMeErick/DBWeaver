namespace DBWeaver.Nodes;

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
    IReadOnlyList<NodeParameter> Parameters,
    IReadOnlyList<NodeTag>? Tags = null
)
{
    /// <summary>
    /// Gets all input pins declared by this node definition.
    /// </summary>
    public IEnumerable<PinDescriptor> InputPins =>
        Pins.Where(p => p.Direction == PinDirection.Input);

    /// <summary>
    /// Gets all output pins declared by this node definition.
    /// </summary>
    public IEnumerable<PinDescriptor> OutputPins =>
        Pins.Where(p => p.Direction == PinDirection.Output);
}

// ═════════════════════════════════════════════════════════════════════════════
// NODE PARAMETER  (canvas property panel value)
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Supported editor kinds for node parameters in the property panel.
/// </summary>
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
        PinDataType type = PinDataType.Expression,
        bool required = true,
        bool multi = false,
        string? desc = null
    ) => new(name, PinDirection.Input, type, required, desc, multi);

    private static PinDescriptor Out(
        string name,
        PinDataType type = PinDataType.Expression,
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

            [NodeType.TableSource] = new(
                NodeType.TableSource,
                NodeCategory.DataSource,
                "Table Source",
                "Represents a physical table as a query source",
                [
                    Out("*", PinDataType.ColumnSet, desc: "All columns from this source"),
                ],
                [
                    Param("table_full_name", ParameterKind.Text, "", "Fully qualified table name (schema.table)"),
                    Param("table", ParameterKind.Text, "", "Fallback table name parameter"),
                    Param("source_table", ParameterKind.Text, "", "Legacy source table parameter"),
                    Param("from_table", ParameterKind.Text, "", "FROM source table fallback"),
                    Param("alias", ParameterKind.Text, "", "Optional source alias"),
                ]
            ),

            [NodeType.Alias] = new(
                NodeType.Alias,
                NodeCategory.DataSource,
                "ALIAS (AS)",
                "Renames a column or expression with AS",
                [
                    In("expression", PinDataType.Expression),
                    In("alias_text", PinDataType.Text, required: false),
                    Out("result", PinDataType.ColumnRef),
                ],
                [Param("alias", ParameterKind.Text, null, "New alias name (e.g. total_price)")]
            ),

            [NodeType.Join] = new(
                NodeType.Join,
                NodeCategory.DataSource,
                "JOIN",
                "Defines an explicit join between two sources",
                [
                    In("left", PinDataType.ColumnRef),
                    In("right", PinDataType.ColumnRef),
                    In("condition", PinDataType.Boolean, required: false),
                    Out("result", PinDataType.Boolean),
                ],
                [
                    Param(
                        "join_type",
                        ParameterKind.Enum,
                        "INNER",
                        "Join type",
                        "INNER",
                        "LEFT",
                        "RIGHT",
                        "FULL",
                        "CROSS"
                    ),
                    Param(
                        "operator",
                        ParameterKind.Enum,
                        "=",
                        "Comparison operator for explicit expression mode",
                        "=",
                        "<>",
                        ">",
                        ">=",
                        "<",
                        "<="
                    ),
                    Param(
                        "right_source",
                        ParameterKind.Text,
                        "",
                        "Optional target source for JOIN (table/cte/subquery alias)"
                    ),
                    Param(
                        "left_expr",
                        ParameterKind.Text,
                        "",
                        "Optional left operand SQL expression (e.g. pe.id)"
                    ),
                    Param(
                        "right_expr",
                        ParameterKind.Text,
                        "",
                        "Optional right operand SQL expression (e.g. c.id)"
                    ),
                ]
            ),

            [NodeType.RowSetJoin] = new(
                NodeType.RowSetJoin,
                NodeCategory.DataSource,
                "RowSet Join",
                "JOIN visual com dois RowSet + condição booleana",
                [
                    In("left", PinDataType.RowSet),
                    In("right", PinDataType.RowSet),
                    In("condition", PinDataType.Boolean, required: false),
                    Out("result", PinDataType.RowSet),
                ],
                [
                    Param(
                        "join_type",
                        ParameterKind.Enum,
                        "INNER",
                        "Join type",
                        "INNER",
                        "LEFT",
                        "RIGHT",
                        "FULL",
                        "CROSS"
                    ),
                ]
            ),

            [NodeType.Subquery] = new(
                NodeType.Subquery,
                NodeCategory.DataSource,
                "Subquery",
                "Uses a nested SELECT as a FROM source",
                [
                    In("query_text", PinDataType.Text, required: false),
                    In("alias_text", PinDataType.Text, required: false),
                    Out("result", PinDataType.RowSet),
                ],
                [
                    Param("alias", ParameterKind.Text, "subq", "Alias for the subquery source"),
                ]
            ),

            [NodeType.SubqueryDefinition] = new(
                NodeType.SubqueryDefinition,
                NodeCategory.DataSource,
                "Subquery Definition",
                "Defines a reusable subquery contract",
                [
                    In("query", PinDataType.ColumnSet, required: false),
                    In("name_text", PinDataType.Text, required: false),
                    Out("subquery", PinDataType.RowSet),
                ],
                [
                    Param("name", ParameterKind.Text, "subquery_name", "Subquery definition name"),
                ]
            ),

            [NodeType.SubqueryReference] = new(
                NodeType.SubqueryReference,
                NodeCategory.DataSource,
                "Subquery Reference",
                "References a subquery as a query source",
                [
                    In("subquery", PinDataType.RowSet, required: false),
                    In("query_text", PinDataType.Text, required: false),
                    In("alias_text", PinDataType.Text, required: false),
                    Out("result", PinDataType.RowSet),
                ],
                [
                    Param("alias", ParameterKind.Text, "subq", "Alias for the subquery source"),
                ]
            ),

            [NodeType.CteSource] = new(
                NodeType.CteSource,
                NodeCategory.DataSource,
                "CTE Source",
                "References a CTE as a table source",
                [
                    In("cte", PinDataType.RowSet, required: false),
                    In("cte_name_text", PinDataType.Text, required: false),
                    In("alias_text", PinDataType.Text, required: false),
                    Out("result", PinDataType.RowSet),
                ],
                [
                    Param("cte_name", ParameterKind.Text, "cte_name", "CTE name to reference"),
                    Param("alias", ParameterKind.Text, "", "Optional alias for the CTE source"),
                ]
            ),

            [NodeType.RawSqlQuery] = new(
                NodeType.RawSqlQuery,
                NodeCategory.DataSource,
                "Raw SQL Query",
                "Provides a raw SQL statement as report input",
                [
                    In("sql_text", PinDataType.Text, required: false),
                    Out("query", PinDataType.ReportQuery, desc: "Connect to report nodes"),
                ],
                [
                    Param("sql", ParameterKind.Text, "SELECT 1", "Raw SQL statement to execute in report flow"),
                ]
            ),

            [NodeType.SubqueryExists] = new(
                NodeType.SubqueryExists,
                NodeCategory.Comparison,
                "EXISTS",
                "Checks if a subquery returns at least one row",
                [
                    In("subquery", PinDataType.RowSet, required: false),
                    In("query_text", PinDataType.Text, required: false),
                    Out("result", PinDataType.Boolean),
                ],
                [
                    Param("negate", ParameterKind.Boolean, "false", "Emit NOT EXISTS when true"),
                ]
            ),

            [NodeType.SubqueryIn] = new(
                NodeType.SubqueryIn,
                NodeCategory.Comparison,
                "IN (Subquery)",
                "Checks if a value is present in a subquery result set",
                [
                    In("value", PinDataType.ColumnRef),
                    In("subquery", PinDataType.RowSet, required: false),
                    In("query_text", PinDataType.Text, required: false),
                    Out("result", PinDataType.Boolean),
                ],
                [
                    Param("negate", ParameterKind.Boolean, "false", "Emit NOT IN when true"),
                ]
            ),

            [NodeType.SubqueryScalar] = new(
                NodeType.SubqueryScalar,
                NodeCategory.Comparison,
                "Scalar Subquery",
                "Compares a value with a scalar subquery result",
                [
                    In("left", PinDataType.ColumnRef),
                    In("subquery", PinDataType.RowSet, required: false),
                    In("query_text", PinDataType.Text, required: false),
                    Out("result", PinDataType.Boolean),
                ],
                [
                    Param(
                        "operator",
                        ParameterKind.Enum,
                        "=",
                        "Comparison operator",
                        "=",
                        "<>",
                        ">",
                        ">=",
                        "<",
                        "<="
                    ),
                ]
            ),

            [NodeType.CteDefinition] = new(
                NodeType.CteDefinition,
                NodeCategory.DataSource,
                "CTE Definition",
                "Defines a WITH entry (name AS subquery)",
                [
                    In("query", PinDataType.ColumnSet, required: false),
                    In("name_text", PinDataType.Text, required: false),
                    In("source_table_text", PinDataType.Text, required: false),
                    Out("table", PinDataType.RowSet),
                ],
                [
                    Param("name", ParameterKind.Text, "cte_name", "CTE name to define"),
                    Param("cte_name", ParameterKind.Text, "cte_name", "CTE name to define"),
                    Param("source_table", ParameterKind.Text, "", "Base FROM table for the CTE"),
                    Param("recursive", ParameterKind.Boolean, "false", "Marks this CTE as recursive"),
                ]
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
                    In("a", PinDataType.Text),
                    In("b", PinDataType.Text),
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

            [NodeType.DateAdd] = new(
                NodeType.DateAdd,
                NodeCategory.MathTransform,
                "Date Add",
                "Adds an interval to a date/time value",
                [
                    In("date", PinDataType.DateTime),
                    In("amount", PinDataType.Number, required: false),
                    Out("result", PinDataType.DateTime),
                ],
                [
                    Param("amount", ParameterKind.Number, "1", "Amount of units to add"),
                    Param("unit", ParameterKind.Enum, "day", "Interval unit", "day", "month", "year", "hour", "minute", "second"),
                ]
            ),

            [NodeType.DateDiff] = new(
                NodeType.DateDiff,
                NodeCategory.MathTransform,
                "Date Diff",
                "Calculates the difference between two date/time values",
                [
                    In("start", PinDataType.DateTime),
                    In("end", PinDataType.DateTime),
                    Out("result", PinDataType.Number),
                ],
                [
                    Param("unit", ParameterKind.Enum, "day", "Difference unit", "day", "month", "year", "hour", "minute", "second"),
                ]
            ),

            [NodeType.DatePart] = new(
                NodeType.DatePart,
                NodeCategory.MathTransform,
                "Date Part",
                "Extracts a specific part from a date/time value",
                [In("value", PinDataType.DateTime), Out("result", PinDataType.Number)],
                [
                    Param("part", ParameterKind.Enum, "year", "Date part to extract", "year", "month", "day", "hour", "minute", "second"),
                ]
            ),

            [NodeType.DateFormat] = new(
                NodeType.DateFormat,
                NodeCategory.MathTransform,
                "Date Format",
                "Formats a date/time value as text",
                [In("value", PinDataType.DateTime), Out("result", PinDataType.Text)],
                [
                    Param("format", ParameterKind.Text, "yyyy-MM-dd", "Output format pattern"),
                ]
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
                [In("value", PinDataType.ColumnRef), Out("minimum", PinDataType.ColumnRef)],
                []
            ),

            [NodeType.Max] = new(
                NodeType.Max,
                NodeCategory.Aggregate,
                "MAX",
                "Maximum value",
                [In("value", PinDataType.ColumnRef), Out("maximum", PinDataType.ColumnRef)],
                []
            ),

            [NodeType.StringAgg] = new(
                NodeType.StringAgg,
                NodeCategory.Aggregate,
                "String Agg",
                "Concatenates values within a group into a delimited string",
                [
                    In("value", PinDataType.Text),
                    In("order_by", PinDataType.ColumnRef, required: false),
                    Out("result", PinDataType.Text),
                ],
                [
                    Param("separator", ParameterKind.Text, ", ", "Delimiter between values"),
                    Param("distinct", ParameterKind.Boolean, "false", "Deduplicate values"),
                ]
            ),

            [NodeType.WindowFunction] = new(
                NodeType.WindowFunction,
                NodeCategory.Aggregate,
                "Window Function",
                "Analytical function computed over an OVER clause",
                [
                    In("value", PinDataType.ColumnRef, required: false),
                    In("default", PinDataType.Expression, required: false),
                    In("partition_1", PinDataType.ColumnRef, required: false, multi: true),
                    In("order_1", PinDataType.ColumnRef, required: false, multi: true),
                    Out("result", PinDataType.Expression),
                ],
                [
                    Param(
                        "function",
                        ParameterKind.Enum,
                        "RowNumber",
                        "Window function kind",
                        "RowNumber",
                        "Rank",
                        "DenseRank",
                        "Ntile",
                        "Lag",
                        "Lead",
                        "FirstValue",
                        "LastValue",
                        "SumOver",
                        "AvgOver",
                        "MinOver",
                        "MaxOver",
                        "CountOver"
                    ),
                    Param("offset", ParameterKind.Number, "1", "Offset for LAG/LEAD"),
                    Param("ntile_groups", ParameterKind.Number, "4", "Number of groups for NTILE"),
                    Param("default_value", ParameterKind.Text, "", "Fallback value for LAG/LEAD when row is missing"),
                    Param(
                        "frame",
                        ParameterKind.Enum,
                        "None",
                        "Window frame clause",
                        "None",
                        "UnboundedPreceding_CurrentRow",
                        "CurrentRow_UnboundedFollowing",
                        "Custom"
                    ),
                    Param(
                        "frame_start",
                        ParameterKind.Enum,
                        "UnboundedPreceding",
                        "Custom frame start bound",
                        "UnboundedPreceding",
                        "Preceding",
                        "CurrentRow",
                        "Following",
                        "UnboundedFollowing"
                    ),
                    Param(
                        "frame_start_offset",
                        ParameterKind.Number,
                        "1",
                        "Offset for Preceding/Following start bound"
                    ),
                    Param(
                        "frame_end",
                        ParameterKind.Enum,
                        "CurrentRow",
                        "Custom frame end bound",
                        "UnboundedPreceding",
                        "Preceding",
                        "CurrentRow",
                        "Following",
                        "UnboundedFollowing"
                    ),
                    Param(
                        "frame_end_offset",
                        ParameterKind.Number,
                        "1",
                        "Offset for Preceding/Following end bound"
                    ),
                    Param("order_1_desc", ParameterKind.Boolean, "false", "Descending ORDER BY for order_1"),
                    Param("order_2_desc", ParameterKind.Boolean, "false", "Descending ORDER BY for order_2"),
                ]
            ),

            // ── Cast ──────────────────────────────────────────────────────────────

            [NodeType.Cast] = new(
                NodeType.Cast,
                NodeCategory.TypeCast,
                "CAST",
                "Converts a value to another data type",
                [In("value", PinDataType.Expression), Out("result", PinDataType.Expression)],
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

            [NodeType.ColumnRefCast] = new(
                NodeType.ColumnRefCast,
                NodeCategory.TypeCast,
                "ColumnRef Cast",
                "CAST explícito de coluna",
                [In("value", PinDataType.ColumnRef), Out("result", PinDataType.Expression)],
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

            [NodeType.ScalarFromColumn] = new(
                NodeType.ScalarFromColumn,
                NodeCategory.TypeCast,
                "Scalar From Column",
                "Desempacota ColumnRef para expressão escalar",
                [In("value", PinDataType.ColumnRef), Out("result", PinDataType.Expression)],
                []
            ),

            // ── Comparisons ───────────────────────────────────────────────────────

            [NodeType.Equals] = new(
                NodeType.Equals,
                NodeCategory.Comparison,
                "Equals (=)",
                "Tests equality",
                [
                    In("left", PinDataType.ColumnRef),
                    In("right", PinDataType.ColumnRef),
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
                    In("left", PinDataType.ColumnRef),
                    In("right", PinDataType.ColumnRef),
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
                    In("left", PinDataType.ColumnRef),
                    In("right", PinDataType.ColumnRef),
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
                    In("left", PinDataType.ColumnRef),
                    In("right", PinDataType.ColumnRef),
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
                    In("left", PinDataType.ColumnRef),
                    In("right", PinDataType.ColumnRef),
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
                    In("left", PinDataType.ColumnRef),
                    In("right", PinDataType.ColumnRef),
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
                    In("value", PinDataType.ColumnRef),
                    In("low", PinDataType.ColumnRef),
                    In("high", PinDataType.ColumnRef),
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
                    In("value", PinDataType.ColumnRef),
                    In("low", PinDataType.ColumnRef),
                    In("high", PinDataType.ColumnRef),
                    Out("result", PinDataType.Boolean),
                ],
                []
            ),

            [NodeType.IsNull] = new(
                NodeType.IsNull,
                NodeCategory.Comparison,
                "IS NULL",
                "Tests if a value is null",
                [In("value", PinDataType.ColumnRef), Out("result", PinDataType.Boolean)],
                []
            ),

            [NodeType.IsNotNull] = new(
                NodeType.IsNotNull,
                NodeCategory.Comparison,
                "IS NOT NULL",
                "Tests if a value is not null",
                [In("value", PinDataType.ColumnRef), Out("result", PinDataType.Boolean)],
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
                [In("json", PinDataType.Json), Out("value", PinDataType.Expression)],
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
                [In("value", PinDataType.Expression), Out("result", PinDataType.Expression)],
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
                [In("value", PinDataType.Expression), Out("result", PinDataType.Expression)],
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

            [NodeType.SystemDate] = new(
                NodeType.SystemDate,
                NodeCategory.Literal,
                "System DateTime",
                "Current date and time from database server",
                [Out("result", PinDataType.DateTime)],
                []
            ),

            [NodeType.SystemDateTime] = new(
                NodeType.SystemDateTime,
                NodeCategory.Literal,
                "System DateTime",
                "Current date and time from database server",
                [Out("result", PinDataType.DateTime)],
                []
            ),

            [NodeType.CurrentDate] = new(
                NodeType.CurrentDate,
                NodeCategory.Literal,
                "Current Date",
                "Current date from database server",
                [Out("result", PinDataType.DateTime)],
                []
            ),

            [NodeType.CurrentTime] = new(
                NodeType.CurrentTime,
                NodeCategory.Literal,
                "Current Time",
                "Current time from database server",
                [Out("result", PinDataType.DateTime)],
                []
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
                    Out("result", PinDataType.ColumnSet),
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

            [NodeType.RowSetFilter] = new(
                NodeType.RowSetFilter,
                NodeCategory.ResultModifier,
                "RowSet Filter",
                "WHERE integrado ao RowSet",
                [
                    In("source", PinDataType.RowSet),
                    In("conditions", PinDataType.Boolean, required: false, multi: true),
                    Out("result", PinDataType.RowSet),
                ],
                []
            ),

            [NodeType.RowSetAggregate] = new(
                NodeType.RowSetAggregate,
                NodeCategory.ResultModifier,
                "RowSet Aggregate",
                "GROUP BY integrado ao RowSet",
                [
                    In("source", PinDataType.RowSet),
                    In("group_by", PinDataType.ColumnRef, required: false, multi: true),
                    In("metrics", PinDataType.ColumnRef, required: false, multi: true),
                    Out("result", PinDataType.RowSet),
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
                        PinDataType.ColumnSet,
                        desc: "Connect to ResultOutput to define columns for SELECT"
                    ),
                ],
                []
            ),

            [NodeType.ColumnSetBuilder] = new(
                NodeType.ColumnSetBuilder,
                NodeCategory.Output,
                "ColumnSet Builder",
                "Builds a structural ColumnSet from individual column references",
                [
                    In(
                        "columns",
                        PinDataType.ColumnRef,
                        required: false,
                        multi: true,
                        desc: "Connect columns or expressions to include in the set"
                    ),
                    Out(
                        "result",
                        PinDataType.ColumnSet,
                        desc: "Connect to ResultOutput.columns to define SELECT columns"
                    ),
                ],
                []
            ),

            [NodeType.ColumnSetMerge] = new(
                NodeType.ColumnSetMerge,
                NodeCategory.Output,
                "ColumnSet Merge",
                "Merges multiple ColumnSet inputs into a single output set",
                [
                    In(
                        "sets",
                        PinDataType.ColumnSet,
                        required: false,
                        multi: true,
                        desc: "Connect one or more ColumnSet outputs"
                    ),
                    Out(
                        "result",
                        PinDataType.ColumnSet,
                        desc: "Merged ColumnSet output"
                    ),
                ],
                []
            ),

            [NodeType.SelectOutput] = new(
                NodeType.SelectOutput,
                NodeCategory.Output,
                "Select Output",
                "Legacy output sink for final SELECT projection",
                [
                    In(
                        "top",
                        PinDataType.ColumnSet,
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
                        "having",
                        PinDataType.Boolean,
                        required: false,
                        desc: "Connect a compiled HAVING condition (post-aggregation filter)"
                    ),
                    In(
                        "qualify",
                        PinDataType.Boolean,
                        required: false,
                        desc: "Connect a QUALIFY condition (post-window filter)"
                    ),
                    In(
                        "order_by",
                        PinDataType.ColumnRef,
                        required: false,
                        multi: true,
                        desc: "Connect ORDER BY terms (ascending)"
                    ),
                    In(
                        "order_by_desc",
                        PinDataType.ColumnRef,
                        required: false,
                        multi: true,
                        desc: "Connect ORDER BY terms (descending)"
                    ),
                    In(
                        "group_by",
                        PinDataType.ColumnRef,
                        required: false,
                        multi: true,
                        desc: "Connect GROUP BY terms"
                    ),
                    In(
                        "columns",
                        PinDataType.ColumnSet,
                        required: false,
                        desc: "Connect ColumnList/ColumnSetBuilder output to include columns in SELECT"
                    ),
                    In(
                        "column",
                        PinDataType.ColumnRef,
                        required: false,
                        multi: true,
                        desc: "Connect individual columns directly (without ColumnList)"
                    ),
                    In(
                        "set_operation",
                        PinDataType.ColumnSet,
                        required: false,
                        desc: "Connect Set Operation node to combine this SELECT with another query"
                    ),
                    Out(
                        "result",
                        PinDataType.ColumnSet,
                        desc: "Connect to an Export node to generate an output file"
                    ),
                ],
                []
            ),

            [NodeType.WhereOutput] = new(
                NodeType.WhereOutput,
                NodeCategory.Output,
                "Where Output",
                "Legacy WHERE sink for imported conditions",
                [
                    In(
                        "condition",
                        PinDataType.Boolean,
                        required: false,
                        desc: "Connect a boolean condition"
                    ),
                    Out(
                        "result",
                        PinDataType.Boolean,
                        desc: "WHERE condition output"
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
                        PinDataType.ColumnSet,
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
                        "having",
                        PinDataType.Boolean,
                        required: false,
                        desc: "Connect a compiled HAVING condition (post-aggregation filter)"
                    ),
                    In(
                        "qualify",
                        PinDataType.Boolean,
                        required: false,
                        desc: "Connect a QUALIFY condition (post-window filter)"
                    ),
                    In(
                        "order_by",
                        PinDataType.ColumnRef,
                        required: false,
                        multi: true,
                        desc: "Connect ORDER BY terms (ascending)"
                    ),
                    In(
                        "order_by_desc",
                        PinDataType.ColumnRef,
                        required: false,
                        multi: true,
                        desc: "Connect ORDER BY terms (descending)"
                    ),
                    In(
                        "group_by",
                        PinDataType.ColumnRef,
                        required: false,
                        multi: true,
                        desc: "Connect GROUP BY terms"
                    ),
                    In(
                        "columns",
                        PinDataType.ColumnSet,
                        required: false,
                        desc: "Connect ColumnList/ColumnSetBuilder output to include columns in SELECT"
                    ),
                    In(
                        "column",
                        PinDataType.ColumnRef,
                        required: false,
                        multi: true,
                        desc: "Connect individual columns directly (without ColumnList)"
                    ),
                    In(
                        "set_operation",
                        PinDataType.ColumnSet,
                        required: false,
                        desc: "Connect Set Operation node to combine this SELECT with another query"
                    ),
                    Out(
                        "result",
                        PinDataType.ColumnSet,
                        desc: "Connect to an Export node to generate an output file"
                    ),
                ],
                [
                    Param("distinct", ParameterKind.Boolean, "false", "Emit SELECT DISTINCT"),
                    Param("query_hints", ParameterKind.Text, "", "Engine-specific query hints"),
                    Param("set_operator", ParameterKind.Enum, "", "Legacy set operation operator", "", "UNION", "UNION ALL", "INTERSECT", "EXCEPT"),
                    Param("set_query", ParameterKind.Text, "", "Legacy set operation right-side query"),
                    Param("pivot_mode", ParameterKind.Enum, "NONE", "Pivot mode", "NONE", "PIVOT", "UNPIVOT"),
                    Param("pivot_config", ParameterKind.Text, "", "Pivot/Unpivot configuration payload"),
                ]
            ),

            [NodeType.ReportOutput] = new(
                NodeType.ReportOutput,
                NodeCategory.Output,
                "Report Output",
                "Terminal report sink that executes a report query input",
                [
                    In(
                        "query",
                        PinDataType.ReportQuery,
                        required: true,
                        desc: "Connect a ReportQuery-producing node (e.g. Raw SQL Query)"
                    ),
                    Out(
                        "result",
                        PinDataType.ReportQuery,
                        desc: "Report query output for downstream report/export handlers"
                    ),
                ],
                [
                    Param("report_name", ParameterKind.Text, "report", "Logical report identifier"),
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
                        PinDataType.ColumnSet,
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

            [NodeType.SetOperation] = new(
                NodeType.SetOperation,
                NodeCategory.Output,
                "Set Operation",
                "Combines the current SELECT with another query using UNION/INTERSECT/EXCEPT",
                [
                    In("left", PinDataType.RowSet, required: false),
                    In("right", PinDataType.RowSet, required: false),
                    In("operator_text", PinDataType.Text, required: false),
                    In("query_text", PinDataType.Text, required: false),
                    Out("result", PinDataType.ColumnSet),
                ],
                [
                    Param(
                        "operator",
                        ParameterKind.Enum,
                        "UNION",
                        "Set operator",
                        "UNION",
                        "UNION ALL",
                        "INTERSECT",
                        "EXCEPT"
                    ),
                    Param(
                        "query",
                        ParameterKind.Text,
                        "",
                        "Right-side SELECT SQL for set operation"
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
                        PinDataType.ColumnSet,
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
                        PinDataType.ColumnSet,
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

            // ── DDL ───────────────────────────────────────────────────────

            [NodeType.TableReference] = new(
                NodeType.TableReference,
                NodeCategory.Ddl,
                "Table Reference",
                "References an existing table as structural DDL target",
                [
                    Out("table_ref", PinDataType.TableDef),
                    Out("*", PinDataType.ColumnSet),
                ],
                [
                    Param("SchemaName", ParameterKind.Text, "public", "Referenced schema"),
                    Param("TableName", ParameterKind.Text, "", "Referenced table name"),
                    Param("Alias", ParameterKind.Text, "", "Optional runtime alias for previews"),
                ]
            ),

            [NodeType.ViewReference] = new(
                NodeType.ViewReference,
                NodeCategory.Ddl,
                "View Reference",
                "References an existing view as structural DDL target",
                [
                    Out("view_ref", PinDataType.ViewDef),
                    Out("*", PinDataType.ColumnSet),
                ],
                [
                    Param("SchemaName", ParameterKind.Text, "public", "Referenced schema"),
                    Param("ViewName", ParameterKind.Text, "", "Referenced view name"),
                    Param("Alias", ParameterKind.Text, "", "Optional runtime alias for previews"),
                ]
            ),

            [NodeType.TableDefinition] = new(
                NodeType.TableDefinition,
                NodeCategory.Ddl,
                "Table Definition",
                "Defines a DDL table with columns and constraints",
                [
                    In("column", PinDataType.ColumnDef, required: false, multi: true),
                    In("constraint", PinDataType.Constraint, required: false, multi: true),
                    Out("table", PinDataType.TableDef),
                ],
                [
                    Param("SchemaName", ParameterKind.Text, "public", "Table schema"),
                    Param("TableName", ParameterKind.Text, "", "Table name"),
                    Param("IfNotExists", ParameterKind.Boolean, "true", "Emit IF NOT EXISTS when supported"),
                    Param("Comment", ParameterKind.Text, "", "Table comment"),
                ]
            ),

            [NodeType.ColumnDefinition] = new(
                NodeType.ColumnDefinition,
                NodeCategory.Ddl,
                "Column Definition",
                "Defines a table column and its type metadata",
                [
                    In("default_value", PinDataType.Expression, required: false),
                    In("type_def", PinDataType.TypeDef, required: false),
                    In("sequence", PinDataType.SequenceDef, required: false),
                    Out("column", PinDataType.ColumnDef),
                ],
                [
                    Param("ColumnName", ParameterKind.Text, "", "Column name"),
                    Param("DataType", ParameterKind.Text, "INT", "Canonical data type"),
                    Param("IsNullable", ParameterKind.Boolean, "false", "Allow NULL values"),
                    Param("UseNativeType", ParameterKind.Boolean, "false", "Use a provider-native type expression"),
                    Param("NativeTypeExpression", ParameterKind.Text, "", "Provider-native type expression (e.g. CIDR, GEOGRAPHY)"),
                    Param("Comment", ParameterKind.Text, "", "Column comment"),
                ]
            ),

            [NodeType.EnumTypeDefinition] = new(
                NodeType.EnumTypeDefinition,
                NodeCategory.Ddl,
                "Enum Type Definition",
                "Defines ENUM values for MySQL inline columns or PostgreSQL CREATE TYPE",
                [Out("type_def", PinDataType.TypeDef)],
                [
                    Param("SchemaName", ParameterKind.Text, "public", "Type schema (PostgreSQL)"),
                    Param("TypeName", ParameterKind.Text, "status_enum", "Type name"),
                    Param("EnumValues", ParameterKind.Text, "NEW,ACTIVE,DISABLED", "Comma/newline-separated enum values"),
                ]
            ),

            [NodeType.ScalarTypeDefinition] = new(
                NodeType.ScalarTypeDefinition,
                NodeCategory.Ddl,
                "Scalar Type Definition",
                "Defines reusable scalar SQL type metadata for ColumnDefinition",
                [Out("type_def", PinDataType.TypeDef)],
                [
                    Param(
                        "TypeKind",
                        ParameterKind.Enum,
                        "VARCHAR",
                        "Base scalar type",
                        "VARCHAR",
                        "TEXT",
                        "INT",
                        "BIGINT",
                        "DECIMAL",
                        "BOOLEAN",
                        "DATE",
                        "DATETIME",
                        "JSON",
                        "UUID"
                    ),
                    Param("Length", ParameterKind.Number, "255", "Length for VARCHAR (optional)"),
                    Param("Precision", ParameterKind.Number, "18", "Precision for DECIMAL"),
                    Param("Scale", ParameterKind.Number, "2", "Scale for DECIMAL"),
                ]
            ),

            [NodeType.PrimaryKeyConstraint] = new(
                NodeType.PrimaryKeyConstraint,
                NodeCategory.Ddl,
                "Primary Key Constraint",
                "Defines PRIMARY KEY over one or more columns",
                [
                    In("column", PinDataType.ColumnDef, multi: true),
                    Out("pk", PinDataType.Constraint),
                ],
                [Param("ConstraintName", ParameterKind.Text, "", "Optional PK name")]
            ),

            [NodeType.ForeignKeyConstraint] = new(
                NodeType.ForeignKeyConstraint,
                NodeCategory.Ddl,
                "Foreign Key Constraint",
                "Defines a foreign key between child and parent columns",
                [
                    In("child_column", PinDataType.ColumnDef, multi: true),
                    In("parent_column", PinDataType.ColumnDef, multi: true),
                    Out("fk", PinDataType.Constraint),
                ],
                [
                    Param("ConstraintName", ParameterKind.Text, "", "Optional FK name"),
                    Param("OnDelete", ParameterKind.Enum, "NO ACTION", "Delete action", "NO ACTION", "CASCADE", "SET NULL", "SET DEFAULT", "RESTRICT"),
                    Param("OnUpdate", ParameterKind.Enum, "NO ACTION", "Update action", "NO ACTION", "CASCADE", "SET NULL", "SET DEFAULT", "RESTRICT"),
                ]
            ),

            [NodeType.UniqueConstraint] = new(
                NodeType.UniqueConstraint,
                NodeCategory.Ddl,
                "Unique Constraint",
                "Defines UNIQUE over one or more columns",
                [
                    In("column", PinDataType.ColumnDef, multi: true),
                    Out("uq", PinDataType.Constraint),
                ],
                [Param("ConstraintName", ParameterKind.Text, "", "Optional UNIQUE name")]
            ),

            [NodeType.CheckConstraint] = new(
                NodeType.CheckConstraint,
                NodeCategory.Ddl,
                "Check Constraint",
                "Defines CHECK expression for a table",
                [Out("ck", PinDataType.Constraint)],
                [
                    Param("ConstraintName", ParameterKind.Text, "", "Optional CHECK name"),
                    Param("Expression", ParameterKind.Text, "", "Boolean validation expression"),
                ]
            ),

            [NodeType.DefaultConstraint] = new(
                NodeType.DefaultConstraint,
                NodeCategory.Ddl,
                "Default Constraint",
                "Defines DEFAULT value for a column",
                [
                    In("column", PinDataType.ColumnDef),
                    Out("dc", PinDataType.Constraint),
                ],
                [
                    Param("ConstraintName", ParameterKind.Text, "", "Optional default constraint name"),
                    Param("DefaultValue", ParameterKind.Text, "", "Default literal or expression"),
                ]
            ),

            [NodeType.IndexDefinition] = new(
                NodeType.IndexDefinition,
                NodeCategory.Ddl,
                "Index Definition",
                "Defines CREATE INDEX statement metadata",
                [
                    In("table", PinDataType.TableDef),
                    In("column", PinDataType.ColumnDef, multi: true),
                    In("expression_column", PinDataType.Expression, required: false, multi: true),
                    In("include_column", PinDataType.ColumnDef, required: false, multi: true),
                    Out("idx", PinDataType.IndexDef),
                ],
                [
                    Param("IndexName", ParameterKind.Text, "", "Index name"),
                    Param("IsUnique", ParameterKind.Boolean, "false", "Emit UNIQUE"),
                ]
            ),

            [NodeType.ViewDefinition] = new(
                NodeType.ViewDefinition,
                NodeCategory.Ddl,
                "View Definition",
                "Defines view metadata and SELECT body for CREATE/ALTER VIEW",
                [
                    In("schema_text", PinDataType.Text, required: false),
                    In("view_name_text", PinDataType.Text, required: false),
                    In("from_table_text", PinDataType.Text, required: false),
                    In("select_sql_text", PinDataType.Text, required: false),
                    Out("view", PinDataType.ViewDef),
                ],
                [
                    Param("Schema", ParameterKind.Text, "public", "View schema"),
                    Param("ViewName", ParameterKind.Text, "", "View name"),
                    Param("OrReplace", ParameterKind.Boolean, "false", "Emit OR REPLACE where supported"),
                    Param("IsMaterialized", ParameterKind.Boolean, "false", "PostgreSQL materialized view"),
                    Param("ViewFromTable", ParameterKind.Text, "", "Source FROM used when compiling subcanvas graph"),
                    Param("ViewSubgraphGraphJson", ParameterKind.Text, "", "Serialized NodeGraph for view subcanvas"),
                    Param("SelectSql", ParameterKind.Text, "", "Compiled SELECT statement for the view body"),
                ]
            ),

            [NodeType.CreateTableOutput] = new(
                NodeType.CreateTableOutput,
                NodeCategory.Ddl,
                "Create Table Output",
                "Terminal output for CREATE TABLE",
                [In("table", PinDataType.TableDef)],
                [Param("IdempotentMode", ParameterKind.Enum, "None", "Script idempotency mode", "None", "IfNotExists", "DropAndCreate")]
            ),

            [NodeType.CreateTypeOutput] = new(
                NodeType.CreateTypeOutput,
                NodeCategory.Ddl,
                "Create Type Output",
                "Terminal output for CREATE TYPE (PostgreSQL)",
                [In("type_def", PinDataType.TypeDef)],
                [Param("IdempotentMode", ParameterKind.Enum, "None", "Script idempotency mode", "None", "IfNotExists", "DropAndCreate")]
            ),

            [NodeType.SequenceDefinition] = new(
                NodeType.SequenceDefinition,
                NodeCategory.Ddl,
                "Sequence Definition",
                "Defines CREATE SEQUENCE metadata",
                [
                    In("start_value", PinDataType.Number, required: false),
                    In("increment", PinDataType.Number, required: false),
                    In("min_value", PinDataType.Number, required: false),
                    In("max_value", PinDataType.Number, required: false),
                    In("cycle", PinDataType.Boolean, required: false),
                    In("cache", PinDataType.Number, required: false),
                    Out("seq", PinDataType.SequenceDef),
                ],
                [
                    Param("Schema", ParameterKind.Text, "public", "Sequence schema"),
                    Param("SequenceName", ParameterKind.Text, "seq_default", "Sequence name"),
                    Param("StartValue", ParameterKind.Number, "1", "START WITH value"),
                    Param("Increment", ParameterKind.Number, "1", "INCREMENT BY value"),
                    Param("MinValue", ParameterKind.Text, "", "Optional MINVALUE"),
                    Param("MaxValue", ParameterKind.Text, "", "Optional MAXVALUE"),
                    Param("Cycle", ParameterKind.Boolean, "false", "Emit CYCLE"),
                    Param("Cache", ParameterKind.Number, "", "Optional CACHE value"),
                ]
            ),

            [NodeType.CreateSequenceOutput] = new(
                NodeType.CreateSequenceOutput,
                NodeCategory.Ddl,
                "Create Sequence Output",
                "Terminal output for CREATE SEQUENCE",
                [In("seq", PinDataType.SequenceDef)],
                [Param("IdempotentMode", ParameterKind.Enum, "None", "Script idempotency mode", "None", "IfNotExists", "DropAndCreate")]
            ),

            [NodeType.CreateTableAsOutput] = new(
                NodeType.CreateTableAsOutput,
                NodeCategory.Ddl,
                "Create Table As Output",
                "Terminal output for CREATE TABLE AS SELECT / LIKE",
                [
                    In("source_table", PinDataType.TableDef, required: false),
                ],
                [
                    Param("TableName", ParameterKind.Text, "new_table", "Target table name"),
                    Param("Schema", ParameterKind.Text, "public", "Target schema"),
                    Param("IncludeData", ParameterKind.Boolean, "true", "PostgreSQL WITH DATA/WITH NO DATA"),
                    Param("SelectSql", ParameterKind.Text, "", "Fallback SELECT SQL when source_query cannot be resolved"),
                    Param("IdempotentMode", ParameterKind.Enum, "None", "Script idempotency mode", "None", "IfNotExists", "DropAndCreate"),
                ]
            ),

            [NodeType.CreateViewOutput] = new(
                NodeType.CreateViewOutput,
                NodeCategory.Ddl,
                "Create View Output",
                "Terminal output for CREATE VIEW",
                [In("view", PinDataType.ViewDef)],
                [Param("IdempotentMode", ParameterKind.Enum, "None", "Script idempotency mode", "None", "IfNotExists", "DropAndCreate")]
            ),

            [NodeType.AlterViewOutput] = new(
                NodeType.AlterViewOutput,
                NodeCategory.Ddl,
                "Alter View Output",
                "Terminal output for ALTER VIEW",
                [In("view", PinDataType.ViewDef)],
                []
            ),

            [NodeType.AlterTableOutput] = new(
                NodeType.AlterTableOutput,
                NodeCategory.Ddl,
                "Alter Table Output",
                "Terminal output for ALTER TABLE operations",
                [
                    In("table", PinDataType.TableDef),
                    In("operation", PinDataType.AlterOp, multi: true),
                ],
                [Param("EmitSeparateStatements", ParameterKind.Boolean, "true", "Emit one statement per operation")]
            ),

            [NodeType.CreateIndexOutput] = new(
                NodeType.CreateIndexOutput,
                NodeCategory.Ddl,
                "Create Index Output",
                "Terminal output for CREATE INDEX",
                [In("index", PinDataType.IndexDef)],
                []
            ),

            [NodeType.AddColumnOp] = new(
                NodeType.AddColumnOp,
                NodeCategory.Ddl,
                "Add Column Op",
                "ALTER TABLE operation: ADD COLUMN",
                [
                    In("target_table", PinDataType.TableDef, required: false),
                    In("column", PinDataType.ColumnDef),
                    Out("op", PinDataType.AlterOp),
                ],
                []
            ),

            [NodeType.DropColumnOp] = new(
                NodeType.DropColumnOp,
                NodeCategory.Ddl,
                "Drop Column Op",
                "ALTER TABLE operation: DROP COLUMN",
                [
                    In("target_column", PinDataType.ColumnDef, required: false),
                    Out("op", PinDataType.AlterOp),
                ],
                [
                    Param("ColumnName", ParameterKind.Text, "", "Column to drop"),
                    Param("IfExists", ParameterKind.Boolean, "false", "Emit IF EXISTS when supported"),
                ]
            ),

            [NodeType.RenameColumnOp] = new(
                NodeType.RenameColumnOp,
                NodeCategory.Ddl,
                "Rename Column Op",
                "ALTER TABLE operation: RENAME COLUMN",
                [
                    In("target_column", PinDataType.ColumnDef, required: false),
                    In("new_name", PinDataType.Text, required: false),
                    Out("op", PinDataType.AlterOp),
                ],
                [
                    Param("OldName", ParameterKind.Text, "", "Current column name"),
                    Param("NewName", ParameterKind.Text, "", "New column name"),
                ]
            ),

            [NodeType.RenameTableOp] = new(
                NodeType.RenameTableOp,
                NodeCategory.Ddl,
                "Rename Table Op",
                "ALTER TABLE operation: RENAME TABLE",
                [
                    In("target_table", PinDataType.TableDef, required: false),
                    In("new_name", PinDataType.Text, required: false),
                    In("new_schema", PinDataType.Text, required: false),
                    Out("op", PinDataType.AlterOp),
                ],
                [
                    Param("NewName", ParameterKind.Text, "", "New table name"),
                    Param("NewSchema", ParameterKind.Text, "", "Target schema (optional)"),
                ]
            ),

            [NodeType.DropTableOp] = new(
                NodeType.DropTableOp,
                NodeCategory.Ddl,
                "Drop Table Op",
                "ALTER TABLE operation: DROP TABLE",
                [
                    In("target_table", PinDataType.TableDef, required: false),
                    Out("op", PinDataType.AlterOp),
                ],
                [
                    Param("IfExists", ParameterKind.Boolean, "false", "Emit IF EXISTS when supported"),
                ]
            ),

            [NodeType.AlterColumnTypeOp] = new(
                NodeType.AlterColumnTypeOp,
                NodeCategory.Ddl,
                "Alter Column Type Op",
                "ALTER TABLE operation: ALTER COLUMN TYPE",
                [
                    In("target_column", PinDataType.ColumnDef, required: false),
                    In("new_column", PinDataType.ColumnDef),
                    Out("op", PinDataType.AlterOp),
                ],
                []
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
