
namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationGenerationStage(
    IQueryCompilationSqlGenerator sqlGenerator,
    Func<string, IReadOnlyDictionary<string, object?>, string> inlineBindingsForPreview,
    Func<Exception, IEnumerable<string>> mapGenerationErrors,
    Func<string, IReadOnlyList<JoinDefinition>, string> fallbackSql)
{
    private readonly IQueryCompilationSqlGenerator _sqlGenerator = sqlGenerator;
    private readonly Func<string, IReadOnlyDictionary<string, object?>, string> _inlineBindingsForPreview = inlineBindingsForPreview;
    private readonly Func<Exception, IEnumerable<string>> _mapGenerationErrors = mapGenerationErrors;
    private readonly Func<string, IReadOnlyList<JoinDefinition>, string> _fallbackSql = fallbackSql;

    public QueryCompilationGenerationStageResult Execute(QueryCompilationGenerationStageInput input)
    {
        try
        {
            GeneratedQuery result = _sqlGenerator.Generate(input.FromTable, input.Graph, input.Joins, input.SetOperation);
            string previewSql = _inlineBindingsForPreview(result.Sql, result.Bindings);
            return new QueryCompilationGenerationStageResult(previewSql, input.Errors);
        }
        catch (Exception ex)
        {
            input.Errors.AddRange(_mapGenerationErrors(ex));
            return new QueryCompilationGenerationStageResult(
                _fallbackSql(input.FromTable, input.Joins),
                input.Errors);
        }
    }
}
