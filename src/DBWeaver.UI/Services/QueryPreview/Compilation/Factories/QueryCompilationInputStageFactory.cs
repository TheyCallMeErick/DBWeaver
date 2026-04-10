namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationInputStageFactory(
    Func<IReadOnlyList<NodeViewModel>, Dictionary<string, string>> buildCteDefinitionNameMap,
    Func<IReadOnlyList<NodeViewModel>, IReadOnlyList<NodeViewModel>, IReadOnlyList<NodeViewModel>, IReadOnlyDictionary<string, string>, (string FromTable, string? Warning)> resolveFromTable,
    Func<PinViewModel, bool> isWildcardProjectionPin,
    Func<string?, bool> isProjectionInputPinName)
{
    private readonly Func<IReadOnlyList<NodeViewModel>, Dictionary<string, string>> _buildCteDefinitionNameMap = buildCteDefinitionNameMap;
    private readonly Func<IReadOnlyList<NodeViewModel>, IReadOnlyList<NodeViewModel>, IReadOnlyList<NodeViewModel>, IReadOnlyDictionary<string, string>, (string FromTable, string? Warning)> _resolveFromTable = resolveFromTable;
    private readonly Func<PinViewModel, bool> _isWildcardProjectionPin = isWildcardProjectionPin;
    private readonly Func<string?, bool> _isProjectionInputPinName = isProjectionInputPinName;

    public QueryCompilationInputStage Create() =>
        new(
            _buildCteDefinitionNameMap,
            _resolveFromTable,
            _isWildcardProjectionPin,
            _isProjectionInputPinName);
}
