using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorViewModelFactory : ISqlEditorViewModelFactory
{
    private readonly ILocalizationService _localization;

    public SqlEditorViewModelFactory(ILocalizationService? localization = null)
    {
        _localization = localization ?? LocalizationService.Instance;
    }

    public SqlEditorViewModel Create(SqlEditorViewModelFactoryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.ConnectionConfigResolver);
        ArgumentNullException.ThrowIfNull(context.ConnectionConfigByProfileIdResolver);
        ArgumentNullException.ThrowIfNull(context.ConnectionProfilesResolver);
        ArgumentNullException.ThrowIfNull(context.MetadataResolver);
        ArgumentNullException.ThrowIfNull(context.SharedConnectionManagerResolver);

        SqlEditorExecutionService executionService = new(localization: _localization);
        MutationGuardService mutationGuardService = new(_localization);
        SqlMutationDiffService mutationDiffService = new(executionService, _localization);
        SqlEditorMutationExecutionOrchestrator mutationExecutionOrchestrator = new(
            executionService,
            mutationGuardService,
            mutationDiffService,
            _localization);

        return new SqlEditorViewModel(
            initialProvider: context.InitialProvider,
            initialConnectionProfileId: context.InitialConnectionProfileId,
            executionService: executionService,
            mutationExecutionOrchestrator: mutationExecutionOrchestrator,
            localization: _localization,
            connectionConfigResolver: context.ConnectionConfigResolver,
            connectionConfigByProfileIdResolver: context.ConnectionConfigByProfileIdResolver,
            connectionProfilesResolver: context.ConnectionProfilesResolver,
            metadataResolver: context.MetadataResolver,
            sharedConnectionManagerResolver: context.SharedConnectionManagerResolver);
    }
}
