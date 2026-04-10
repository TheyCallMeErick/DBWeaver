using DBWeaver.Registry;

namespace DBWeaver.Expressions;

// ═════════════════════════════════════════════════════════════════════════════
// PIN DATA TYPES  (for canvas-side type-checking)
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Semantic data types used by pin compatibility checks and expression emission.
/// </summary>
public enum PinDataType
{
    Text,
    Integer,
    Decimal,
    Number,
    Boolean,
    DateTime,
    Json,
    ColumnRef,
    ColumnSet,
    RowSet,
    TableDef,
    ColumnDef,
    Constraint,
    IndexDef,
    AlterOp,
    ReportQuery,
    Expression, // untyped SQL fragment — accepted by any slot
}

// ═════════════════════════════════════════════════════════════════════════════
// EXPRESSION INTERFACE
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A composable SQL fragment. Every Atomic Node emits one.
/// Expressions form a tree that mirrors the canvas node graph:
///
///   UPPER(orders.email)
///   └─ FunctionCallExpr("UPPER")
///      └─ ColumnExpr("orders", "email")
///
///   orders.total BETWEEN 100 AND 500
///   └─ BetweenExpr(negate:false)
///      ├─ ColumnExpr("orders", "total")
///      ├─ LiteralExpr("100")
///      └─ LiteralExpr("500")
/// </summary>
public interface ISqlExpression
{
    /// <summary>Compiles this expression node into a SQL fragment string.</summary>
    string Emit(EmitContext ctx);

    /// <summary>
    /// Semantic data type of this expression's output.
    /// Used to validate pin connections at design time.
    /// </summary>
    PinDataType OutputType { get; }
}
