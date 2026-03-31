using Material.Icons;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Single source of truth for every icon character used in the application.
/// All node category icons, validation severity icons, and diagnostic category icons
/// are defined here so that new components find and reuse them without hardcoding.
///
/// Convention: add a named constant below, then reference it from ViewModels/AXAML.
/// Fallback for any unknown category/state is <see cref="Unknown"/>.
/// </summary>
public static class NodeIconCatalog
{
    // ── Node category icons ────────────────────────────────────────────────────

    public const string DataSource = "⊞";
    public const string StringTransform = "Aa";
    public const string MathTransform = "∑";
    public const string TypeCast = "⇌";
    public const string Comparison = "≈";
    public const string LogicGate = "&";
    public const string Json = "{}";
    public const string Aggregate = "Σ";
    public const string Conditional = "?";
    public const string Output = "▶";

    /// <summary>Fallback shown when no specific icon matches.</summary>
    public const string Unknown = "○";

    // ── Validation / severity icons ───────────────────────────────────────────

    /// <summary>Error badge — shown as a filled red circle on the node header.</summary>
    public const string ValidationError = "!";

    /// <summary>Warning badge — shown as a filled yellow circle on the node header.</summary>
    public const string ValidationWarning = "⚠";

    /// <summary>Checkmark / success indicator.</summary>
    public const string Success = "✓";

    // ── Diagnostic category icons (ErrorDiagnostics) ──────────────────────────

    public const string DiagSafePreview = "🔒";
    public const string DiagConnection = "🔌";
    public const string DiagAuthorization = "🔑";
    public const string DiagTimeout = "⏱";
    public const string DiagSchema = "🗂";
    public const string DiagSyntax = "✏";
    public const string DiagCompatibility = "⚙";
    public const string DiagUnknown = "⚠";

    // ── Lookup ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the canonical icon character for the given node category.
    /// Falls back to <see cref="Unknown"/> for unrecognised values.
    /// </summary>
    public static string GetForCategory(NodeCategory category) =>
        category switch
        {
            NodeCategory.DataSource => DataSource,
            NodeCategory.StringTransform => StringTransform,
            NodeCategory.MathTransform => MathTransform,
            NodeCategory.TypeCast => TypeCast,
            NodeCategory.Comparison => Comparison,
            NodeCategory.LogicGate => LogicGate,
            NodeCategory.Json => Json,
            NodeCategory.Aggregate => Aggregate,
            NodeCategory.Conditional => Conditional,
            NodeCategory.Output => Output,
            _ => Unknown,
        };

    /// <summary>
    /// Returns the <see cref="MaterialIconKind"/> for the given node category.
    /// Falls back to <see cref="MaterialIconKind.Help"/> for unrecognised values.
    /// </summary>
    public static MaterialIconKind GetKindForCategory(NodeCategory category) =>
        category switch
        {
            NodeCategory.DataSource => MaterialIconKind.Table,
            NodeCategory.StringTransform => MaterialIconKind.FormatText,
            NodeCategory.MathTransform => MaterialIconKind.Calculator,
            NodeCategory.TypeCast => MaterialIconKind.SwapHorizontal,
            NodeCategory.Comparison => MaterialIconKind.Equal,
            NodeCategory.LogicGate => MaterialIconKind.Filter,
            NodeCategory.Json => MaterialIconKind.CodeBraces,
            NodeCategory.Aggregate => MaterialIconKind.ChartBar,
            NodeCategory.Conditional => MaterialIconKind.HelpCircle,
            NodeCategory.Output => MaterialIconKind.PlayCircle,
            _ => MaterialIconKind.Help,
        };
}
