namespace DBWeaver.Nodes;

/// <summary>
/// Logical grouping used for discovery, styling and organization of node types.
/// </summary>
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
    Ddl,
}

/// <summary>
/// Canonical node identifiers supported by the canvas and compiler.
/// </summary>
public enum NodeType
{
    // ── Data Source ───────────────────────────────────────────────────────────
    TableSource,
    Join,
    RowSetJoin,
    Subquery,
    SubqueryDefinition,
    SubqueryReference,
    Alias,
    CteDefinition,
    CteSource,
    RawSqlQuery,

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
    DateAdd,
    DateDiff,
    DatePart,
    DateFormat,

    // ── Aggregates ────────────────────────────────────────────────────────────
    CountStar,
    CountDistinct,
    Sum,
    Avg,
    Min,
    Max,
    StringAgg,
    WindowFunction,

    // ── Type Cast ─────────────────────────────────────────────────────────────
    Cast,
    ColumnRefCast,
    ScalarFromColumn,

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
    SubqueryExists,
    SubqueryIn,
    SubqueryScalar,

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
    SystemDate,
    SystemDateTime,
    CurrentDate,
    CurrentTime,

    // ── Result Modifiers ──────────────────────────────────────────────────────
    Top, // LIMIT/TOP - restricts the number of rows returned
    CompileWhere, // Compiles multiple boolean conditions into a WHERE clause
    RowSetFilter, // Applies boolean predicates over a row set
    RowSetAggregate, // Groups/aggregates a row set
    SetOperation,

    // ── Output ────────────────────────────────────────────────────────────────
    ColumnList, // Aggregates multiple columns for SELECT
    ColumnSetBuilder, // Explicit structural builder for ColumnSet from ColumnRef inputs
    ColumnSetMerge, // Merges multiple ColumnSet inputs into one
    SelectOutput,
    WhereOutput,
    ResultOutput,
    ReportOutput,

    // ── Export ────────────────────────────────────────────────────────────────
    HtmlExport,
    JsonExport,
    CsvExport,
    ExcelExport,

    // ── DDL ─────────────────────────────────────────────────────────────────
    TableReference,
    ViewReference,
    TableDefinition,
    ColumnDefinition,
    PrimaryKeyConstraint,
    ForeignKeyConstraint,
    UniqueConstraint,
    CheckConstraint,
    DefaultConstraint,
    IndexDefinition,
    ViewDefinition,
    CreateTableOutput,
    EnumTypeDefinition,
    ScalarTypeDefinition,
    CreateTypeOutput,
    SequenceDefinition,
    CreateSequenceOutput,
    CreateTableAsOutput,
    CreateViewOutput,
    AlterViewOutput,
    AlterTableOutput,
    CreateIndexOutput,
    AddColumnOp,
    DropColumnOp,
    RenameColumnOp,
    RenameTableOp,
    DropTableOp,
    AlterColumnTypeOp,
}
