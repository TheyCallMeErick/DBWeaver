
namespace DBWeaver.UI.Services.QueryPreview;

internal interface IQueryCompilationSqlGenerator
{
    GeneratedQuery Generate(
        string fromTable,
        NodeGraph graph,
        IReadOnlyList<JoinDefinition> joins,
        SetOperationDefinition? setOperation);
}
