namespace DBWeaver.CanvasKit;

public enum CanvasSubEditorMode
{
    None,
    Cte,
    View,
}

public sealed record CanvasSubEditorSessionState(CanvasSubEditorMode Mode, string DisplayName)
{
    public static readonly CanvasSubEditorSessionState Empty = new(CanvasSubEditorMode.None, string.Empty);

    public bool IsActive => Mode != CanvasSubEditorMode.None;
    public bool IsViewEditor => Mode == CanvasSubEditorMode.View;
}

public static class CanvasSubEditorStateMachine
{
    public static CanvasSubEditorSessionState EnterCte(string displayName) =>
        new(CanvasSubEditorMode.Cte, NormalizeDisplayName(displayName));

    public static CanvasSubEditorSessionState EnterView(string displayName) =>
        new(CanvasSubEditorMode.View, NormalizeDisplayName(displayName));

    public static CanvasSubEditorSessionState Exit() => CanvasSubEditorSessionState.Empty;

    private static string NormalizeDisplayName(string displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
}
