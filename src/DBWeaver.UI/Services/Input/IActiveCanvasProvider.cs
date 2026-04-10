using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services;

/// <summary>
/// Resolves the currently active canvas at the time of the call.
/// Allows keyboard shortcuts to route to whichever canvas (Query or DDL) is active.
/// </summary>
public interface IActiveCanvasProvider
{
    CanvasViewModel GetActive();
}
