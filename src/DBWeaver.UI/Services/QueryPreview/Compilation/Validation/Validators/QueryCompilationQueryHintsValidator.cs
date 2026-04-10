
namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationQueryHintsValidator(DatabaseProvider provider)
{
    private readonly DatabaseProvider _provider = provider;

    public void Validate(NodeViewModel resultOutputNode, List<string> errors)
    {
        if (!resultOutputNode.Parameters.TryGetValue("query_hints", out string? rawHints))
            return;

        if (string.IsNullOrWhiteSpace(rawHints))
            return;

        if (!QueryHintSyntax.TryNormalize(_provider, rawHints, out _, out string? validationError))
        {
            errors.Add(validationError ?? "Invalid query hints configuration.");
        }
    }
}



