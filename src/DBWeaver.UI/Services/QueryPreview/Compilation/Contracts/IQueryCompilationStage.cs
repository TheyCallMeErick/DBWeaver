namespace DBWeaver.UI.Services.QueryPreview;

internal interface IQueryCompilationStage<TState>
{
    TState Execute(QueryCompilationPipelineContext context);
}


