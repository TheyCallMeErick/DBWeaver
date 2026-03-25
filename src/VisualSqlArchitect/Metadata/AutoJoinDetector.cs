using System.Text.RegularExpressions;
using VisualSqlArchitect.QueryEngine;

namespace VisualSqlArchitect.Metadata;

// ═════════════════════════════════════════════════════════════════════════════
// JOIN SUGGESTION MODEL
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>How confident the detector is in this suggestion.</summary>
public enum JoinConfidence
{
    /// <summary>A foreign-key constraint exists in the catalog (certain).</summary>
    CatalogDefinedFk    = 4,

    /// <summary>
    /// The FK is defined in the other direction (the canvas table has a FK
    /// pointing at the new table, or vice-versa). Still certain.
    /// </summary>
    CatalogDefinedReverse = 3,

    /// <summary>Column name strongly implies a join (e.g. orders.customer_id → customers.id).</summary>
    HeuristicStrong     = 2,

    /// <summary>Column names match but no FK or naming convention confirms it.</summary>
    HeuristicWeak       = 1
}

/// <summary>
/// A single auto-join proposal returned by <see cref="AutoJoinDetector"/>.
/// The canvas UI renders these as "ghost" JOIN nodes for the user to accept/reject.
/// </summary>
public record JoinSuggestion(
    /// <summary>Table already on the canvas.</summary>
    string ExistingTable,

    /// <summary>Table the user just dragged in.</summary>
    string NewTable,

    /// <summary>Recommended JOIN type.</summary>
    string JoinType,

    /// <summary>Left side of the ON clause (child.column).</summary>
    string LeftColumn,

    /// <summary>Right side of the ON clause (parent.column).</summary>
    string RightColumn,

    /// <summary>Ready-to-use ON expression: "orders.customer_id = customers.id"</summary>
    string OnClause,

    /// <summary>0.0 – 1.0 confidence score.</summary>
    double Score,

    JoinConfidence Confidence,

    /// <summary>Human-readable explanation shown in the canvas suggestion tooltip.</summary>
    string Rationale,

    /// <summary>
    /// When not null, the FK constraint that backs this suggestion.
    /// Null for heuristic suggestions.
    /// </summary>
    ForeignKeyRelation? SourceFk = null
)
{
    /// <summary>
    /// Converts this suggestion to a <see cref="JoinDefinition"/> ready for
    /// <see cref="QueryBuilderService.Compile"/>.
    /// </summary>
    public JoinDefinition ToJoinDefinition() =>
        new(NewTable, LeftColumn, RightColumn, JoinType);
}

// ═════════════════════════════════════════════════════════════════════════════
// DETECTOR
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Analyses <see cref="DbMetadata"/> to generate JOIN suggestions whenever a
/// new table is dragged onto the canvas.
///
/// Detection pipeline (in order of confidence):
/// <list type="number">
///   <item>Catalog FK — child table has FK column pointing to parent PK</item>
///   <item>Catalog FK reverse — parent table is referenced by canvas table</item>
///   <item>Naming heuristic — column matches {singular(targetTable)}_id pattern</item>
///   <item>Column name equality — same name + compatible type in both tables</item>
/// </list>
/// Results are de-duplicated and ranked by score descending.
/// </summary>
public sealed class AutoJoinDetector
{
    // Minimum heuristic score to emit a suggestion (avoids noise)
    private const double HeuristicThreshold = 0.40;

    private readonly DbMetadata _metadata;

    public AutoJoinDetector(DbMetadata metadata)
        => _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Called when the user drops <paramref name="newTable"/> onto the canvas.
    /// Returns suggestions sorted from most to least confident.
    /// </summary>
    public IReadOnlyList<JoinSuggestion> Suggest(
        string newTable,
        IEnumerable<string> canvasTables)
    {
        var canvasSet = new HashSet<string>(
            canvasTables, StringComparer.OrdinalIgnoreCase);

        if (canvasSet.Count == 0)
            return Array.Empty<JoinSuggestion>();

        var newMeta = _metadata.FindTable(newTable);
        if (newMeta is null)
            return Array.Empty<JoinSuggestion>();

        var suggestions = new List<JoinSuggestion>();

        foreach (var existingTable in canvasSet)
        {
            var existingMeta = _metadata.FindTable(existingTable);
            if (existingMeta is null) continue;

            // ── Pass 1: Catalog FK — new table is the child ───────────────────
            foreach (var fk in newMeta.OutboundForeignKeys
                .Where(r => r.ParentFullTable.Equals(existingTable,
                    StringComparison.OrdinalIgnoreCase)))
            {
                suggestions.Add(BuildFkSuggestion(
                    existing: existingTable,
                    newTable: newTable,
                    leftCol:  $"{newTable}.{fk.ChildColumn}",
                    rightCol: $"{existingTable}.{fk.ParentColumn}",
                    joinType: DeriveJoinType(fk),
                    confidence: JoinConfidence.CatalogDefinedFk,
                    score: 1.0,
                    rationale: $"FK constraint '{fk.ConstraintName}' defined on {newTable}.{fk.ChildColumn}",
                    fk: fk
                ));
            }

            // ── Pass 2: Catalog FK — new table is the parent ──────────────────
            foreach (var fk in newMeta.InboundForeignKeys
                .Where(r => r.ChildFullTable.Equals(existingTable,
                    StringComparison.OrdinalIgnoreCase)))
            {
                suggestions.Add(BuildFkSuggestion(
                    existing: existingTable,
                    newTable: newTable,
                    leftCol:  $"{existingTable}.{fk.ChildColumn}",
                    rightCol: $"{newTable}.{fk.ParentColumn}",
                    joinType: DeriveJoinType(fk),
                    confidence: JoinConfidence.CatalogDefinedReverse,
                    score: 0.95,
                    rationale: $"FK constraint '{fk.ConstraintName}' on {existingTable}.{fk.ChildColumn} references {newTable}",
                    fk: fk
                ));
            }

            // Skip heuristics if catalog already gave us suggestions for this pair
            if (suggestions.Any(s =>
                    s.ExistingTable.Equals(existingTable, StringComparison.OrdinalIgnoreCase)))
                continue;

            // ── Pass 3: Naming heuristics ─────────────────────────────────────
            var heuristic = RunNamingHeuristics(newMeta, existingMeta);
            suggestions.AddRange(heuristic.Where(s => s.Score >= HeuristicThreshold));
        }

        // De-duplicate: keep highest score for the same column pair
        return suggestions
            .GroupBy(s => NormKey(s.LeftColumn, s.RightColumn))
            .Select(g => g.MaxBy(s => s.Score)!)
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => (int)s.Confidence)
            .ToList();
    }

    // ── Heuristic engine ──────────────────────────────────────────────────────

    private static List<JoinSuggestion> RunNamingHeuristics(
        TableMetadata newMeta, TableMetadata existingMeta)
    {
        var results = new List<JoinSuggestion>();

        // Heuristic A: newTable has a column named {singular(existing)}_id
        //              and existingTable has a PK column named 'id' or '{existing}_id'
        var existingSingular = Singularize(existingMeta.Name);
        var impliedFkName    = $"{existingSingular}_id";

        var candidateFkCol = newMeta.Columns.FirstOrDefault(c =>
            c.Name.Equals(impliedFkName, StringComparison.OrdinalIgnoreCase));

        if (candidateFkCol is not null)
        {
            var pkCol = existingMeta.PrimaryKeyColumns.FirstOrDefault();
            if (pkCol is not null && AreTypesCompatible(candidateFkCol, pkCol))
            {
                results.Add(BuildHeuristicSuggestion(
                    existing: existingMeta.FullName,
                    newTable: newMeta.FullName,
                    leftCol:  $"{newMeta.FullName}.{candidateFkCol.Name}",
                    rightCol: $"{existingMeta.FullName}.{pkCol.Name}",
                    joinType: "LEFT",
                    score: 0.80,
                    confidence: JoinConfidence.HeuristicStrong,
                    rationale: $"Column '{candidateFkCol.Name}' matches naming convention '{existingSingular}_id'"
                ));
            }
        }

        // Heuristic B: existingTable has a column named {singular(new)}_id
        var newSingular       = Singularize(newMeta.Name);
        var impliedFkNameRev  = $"{newSingular}_id";

        var candidateFkColRev = existingMeta.Columns.FirstOrDefault(c =>
            c.Name.Equals(impliedFkNameRev, StringComparison.OrdinalIgnoreCase));

        if (candidateFkColRev is not null)
        {
            var pkCol = newMeta.PrimaryKeyColumns.FirstOrDefault();
            if (pkCol is not null && AreTypesCompatible(candidateFkColRev, pkCol))
            {
                results.Add(BuildHeuristicSuggestion(
                    existing: existingMeta.FullName,
                    newTable: newMeta.FullName,
                    leftCol:  $"{existingMeta.FullName}.{candidateFkColRev.Name}",
                    rightCol: $"{newMeta.FullName}.{pkCol.Name}",
                    joinType: "LEFT",
                    score: 0.80,
                    confidence: JoinConfidence.HeuristicStrong,
                    rationale: $"Column '{candidateFkColRev.Name}' in '{existingMeta.Name}' matches naming convention '{newSingular}_id'"
                ));
            }
        }

        // Heuristic C: shared column names with compatible types (weak)
        var newColMap = newMeta.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var existCol in existingMeta.Columns)
        {
            if (!newColMap.TryGetValue(existCol.Name, out var newCol)) continue;
            if (existCol.Name.Equals("id", StringComparison.OrdinalIgnoreCase)) continue; // too generic
            if (!AreTypesCompatible(existCol, newCol)) continue;

            var score = existCol.IsPrimaryKey && newCol.IsForeignKey ? 0.70
                      : newCol.IsPrimaryKey   && existCol.IsForeignKey ? 0.70
                      : existCol.IsIndexed    || newCol.IsIndexed ? 0.55
                      : 0.40;

            results.Add(BuildHeuristicSuggestion(
                existing: existingMeta.FullName,
                newTable: newMeta.FullName,
                leftCol:  $"{newMeta.FullName}.{newCol.Name}",
                rightCol: $"{existingMeta.FullName}.{existCol.Name}",
                joinType: "INNER",
                score: score,
                confidence: JoinConfidence.HeuristicWeak,
                rationale: $"Shared column name '{existCol.Name}' with compatible type '{existCol.DataType}'"
            ));
        }

        return results;
    }

    // ── Suggestion builders ───────────────────────────────────────────────────

    private static JoinSuggestion BuildFkSuggestion(
        string existing, string newTable,
        string leftCol, string rightCol,
        string joinType, JoinConfidence confidence,
        double score, string rationale,
        ForeignKeyRelation fk) =>
        new(
            ExistingTable: existing,
            NewTable:      newTable,
            JoinType:      joinType,
            LeftColumn:    leftCol,
            RightColumn:   rightCol,
            OnClause:      $"{leftCol} = {rightCol}",
            Score:         score,
            Confidence:    confidence,
            Rationale:     rationale,
            SourceFk:      fk
        );

    private static JoinSuggestion BuildHeuristicSuggestion(
        string existing, string newTable,
        string leftCol, string rightCol,
        string joinType, double score,
        JoinConfidence confidence, string rationale) =>
        new(
            ExistingTable: existing,
            NewTable:      newTable,
            JoinType:      joinType,
            LeftColumn:    leftCol,
            RightColumn:   rightCol,
            OnClause:      $"{leftCol} = {rightCol}",
            Score:         score,
            Confidence:    confidence,
            Rationale:     rationale,
            SourceFk:      null
        );

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Recommends INNER for non-nullable FK columns, LEFT for nullable ones
    /// (nullable FK usually means optional relationship).
    /// Cascade-delete FKs → INNER (child cannot exist without parent).
    /// </summary>
    private static string DeriveJoinType(ForeignKeyRelation fk) =>
        fk.OnDelete == ReferentialAction.Cascade ? "INNER" : "LEFT";

    /// <summary>
    /// Checks whether two columns have semantically compatible types for joining.
    /// We compare the normalised <see cref="ColumnMetadata.DataType"/> broad category.
    /// </summary>
    private static bool AreTypesCompatible(ColumnMetadata a, ColumnMetadata b)
    {
        // Exact match
        if (a.DataType.Equals(b.DataType, StringComparison.OrdinalIgnoreCase))
            return true;

        // Same semantic category (e.g. int + bigint, varchar + text)
        return a.SemanticType == b.SemanticType
            && a.SemanticType != ColumnSemanticType.Other;
    }

    // Very basic English singulariser — covers the most common DB naming patterns
    private static readonly Regex _trailingIes = new(@"ies$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _trailingEs  = new(@"(ss|sh|ch|x|z)es$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _trailingS   = new(@"s$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static string Singularize(string tableName)
    {
        if (_trailingIes.IsMatch(tableName))
            return _trailingIes.Replace(tableName, "y");

        if (_trailingEs.IsMatch(tableName))
            return _trailingEs.Replace(tableName, "$1");

        if (_trailingS.IsMatch(tableName)
            && !tableName.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
            return _trailingS.Replace(tableName, string.Empty);

        return tableName;
    }

    private static string NormKey(string left, string right)
    {
        var parts = new[] { left.ToLowerInvariant(), right.ToLowerInvariant() };
        Array.Sort(parts);
        return string.Join("|", parts);
    }
}
