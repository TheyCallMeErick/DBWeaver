using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Canvas.AutoJoin;

internal sealed class CanvasAutoJoinMessagePresenter(ILocalizationService localization) : ICanvasAutoJoinMessagePresenter
{
    private readonly ILocalizationService _localization = localization;

    public AutoJoinToastMessage NoSimilarityFound() =>
        new(
            L("autoJoin.noSimilarityTitle", "No automatic join found"),
            L("autoJoin.noSimilarityDetails", "Choose the columns manually to create a simple join."));

    public AutoJoinToastMessage MultipleCandidatesFound(int suggestionsCount) =>
        new(
            L("autoJoin.multipleCandidatesTitle", "Multiple join candidates found"),
            string.Format(
                L("autoJoin.multipleCandidatesDetails", "{0} possible combinations were found. Confirm which columns should be used."),
                suggestionsCount));

    public AutoJoinToastMessage SuggestionsFound(int suggestionsCount) =>
        new(
            L("autoJoin.suggestionsFoundTitle", "Auto-join suggestions available"),
            string.Format(
                L("autoJoin.suggestionsFoundDetails", "{0} suggestion(s) found. Select two tables and run Auto-Join Selected."),
                suggestionsCount));

    public AutoJoinToastMessage SuggestionsUnavailable() =>
        new(
            L("autoJoin.suggestionsUnavailableTitle", "Suggestion details unavailable"),
            L("autoJoin.suggestionsUnavailableDetails", "Could not resolve tables in the current canvas to prefill the join modal."));

    public AutoJoinToastMessage AutoJoinApplied(string onClause) =>
        new(
            L("autoJoin.appliedTitle", "Auto-join applied"),
            onClause);

    public AutoJoinToastMessage ManualJoinFailed() =>
        new(
            L("autoJoin.manualJoinFailedTitle", "Manual join could not be created"),
            L("autoJoin.manualJoinFailedDetails", "Check selected columns and existing joins, then try again."));

    public AutoJoinToastMessage ManualJoinCreated(string leftRef, string rightRef) =>
        new(
            L("autoJoin.manualJoinCreatedTitle", "Manual join created"),
            $"{leftRef} = {rightRef}");

    private string L(string key, string fallback)
    {
        string value = _localization[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}





