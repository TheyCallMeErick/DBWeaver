using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.Canvas.AutoJoin;

internal sealed class CanvasAutoJoinNotifier(
    ToastCenterViewModel toasts,
    ICanvasAutoJoinMessagePresenter messagePresenter) : ICanvasAutoJoinNotifier
{
    private readonly ToastCenterViewModel _toasts = toasts;
    private readonly ICanvasAutoJoinMessagePresenter _messagePresenter = messagePresenter;

    public void ShowNoSimilarityFound()
    {
        AutoJoinToastMessage msg = _messagePresenter.NoSimilarityFound();
        _toasts.ShowWarning(msg.Message, msg.Details);
    }

    public void ShowMultipleCandidatesFound(int suggestionsCount)
    {
        AutoJoinToastMessage msg = _messagePresenter.MultipleCandidatesFound(suggestionsCount);
        _toasts.ShowWarning(msg.Message, msg.Details);
    }

    public void ShowSuggestionsFound(int suggestionsCount, Action onDetails)
    {
        AutoJoinToastMessage msg = _messagePresenter.SuggestionsFound(suggestionsCount);
        _toasts.ShowWarning(msg.Message, msg.Details, onDetails);
    }

    public void ShowSuggestionsUnavailable()
    {
        AutoJoinToastMessage msg = _messagePresenter.SuggestionsUnavailable();
        _toasts.ShowWarning(msg.Message, msg.Details);
    }

    public void ShowAutoJoinApplied(string onClause)
    {
        AutoJoinToastMessage msg = _messagePresenter.AutoJoinApplied(onClause);
        _toasts.ShowSuccess(msg.Message, msg.Details);
    }

    public void ShowManualJoinFailed()
    {
        AutoJoinToastMessage msg = _messagePresenter.ManualJoinFailed();
        _toasts.ShowWarning(msg.Message, msg.Details);
    }

    public void ShowManualJoinCreated(string leftRef, string rightRef)
    {
        AutoJoinToastMessage msg = _messagePresenter.ManualJoinCreated(leftRef, rightRef);
        _toasts.ShowSuccess(msg.Message, msg.Details);
    }
}




