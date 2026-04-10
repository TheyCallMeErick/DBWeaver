using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.Canvas.AutoJoin;

internal interface ICanvasAutoJoinNotifier
{
    void ShowNoSimilarityFound();
    void ShowMultipleCandidatesFound(int suggestionsCount);
    void ShowSuggestionsFound(int suggestionsCount, Action onDetails);
    void ShowSuggestionsUnavailable();
    void ShowAutoJoinApplied(string onClause);
    void ShowManualJoinFailed();
    void ShowManualJoinCreated(string leftRef, string rightRef);
}





