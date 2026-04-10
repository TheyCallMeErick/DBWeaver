namespace DBWeaver.UI.Services.SqlImport.Model;

public sealed record SqlImportModel(
    ImportSelect Select,
    ImportFrom From,
    IReadOnlyList<ImportJoin> Joins,
    ImportPredicate? Where,
    IReadOnlyList<ImportExpression> GroupBy,
    ImportPredicate? Having,
    IReadOnlyList<ImportOrderBy> OrderBy,
    int? Limit,
    int? Top
);

public sealed record ImportSelect(bool Distinct, IReadOnlyList<ImportProjection> Projections);

public sealed record ImportProjection(ImportExpression Expression, string? Alias);

public sealed record ImportExpression(string Text);

public sealed record ImportFrom(string Source, string? Alias);

public sealed record ImportJoin(string JoinType, string Source, string? Alias, ImportPredicate On);

public sealed record ImportPredicate(string Text);

public sealed record ImportOrderBy(ImportExpression Expression, bool Descending);

public sealed record SqlImportSemanticIssue(string Code, string Message, string Context);

public sealed record SqlImportMappingResult(
    SqlImportModel? Model,
    IReadOnlyList<SqlImportSemanticIssue> Issues
)
{
    public bool Success => Model is not null;
}
