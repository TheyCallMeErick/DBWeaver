
namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationSqlGenerator(DatabaseProvider provider) : IQueryCompilationSqlGenerator
{
    private readonly DatabaseProvider _provider = provider;

    public GeneratedQuery Generate(
        string fromTable,
        NodeGraph graph,
        IReadOnlyList<JoinDefinition> joins,
        SetOperationDefinition? setOperation)
    {
        QueryGeneratorService svc = QueryGeneratorService.Create(_provider);
        QueryGenerationStructure structure = new(
            FromTable: fromTable,
            Joins: joins.Count == 0 ? null : joins);
        return svc.Generate(graph, structure, setOperation);
    }
}
