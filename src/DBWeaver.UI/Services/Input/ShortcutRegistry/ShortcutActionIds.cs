namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Stable action-id constants used by the default shortcut catalog.
/// </summary>
public static class ShortcutActionIds
{
    public const string OpenShortcutsReference = "shell.shortcuts.openReference";
    public const string OpenCommandPalette = "shell.commandPalette.open";
    public const string OpenConnectionManager = "shell.connectionManager.open";
    public const string OpenFlowVersions = "shell.flowVersions.open";
    public const string OpenFileHistory = "shell.fileHistory.open";

    public const string NewCanvas = "canvas.file.new";
    public const string OpenFile = "canvas.file.open";
    public const string Save = "canvas.file.save";
    public const string SaveAs = "canvas.file.saveAs";

    public const string Undo = "canvas.edit.undo";
    public const string Redo = "canvas.edit.redo";
    public const string SelectAll = "canvas.edit.selectAll";
    public const string DeleteSelection = "canvas.edit.deleteSelection";

    public const string OpenNodeSearch = "canvas.search.open";
    public const string AutoLayout = "canvas.layout.auto";
    public const string ToggleSnapToGrid = "canvas.snap.toggle";
    public const string BringForward = "canvas.layer.bringForward";
    public const string SendBackward = "canvas.layer.sendBackward";
    public const string BringToFront = "canvas.layer.bringToFront";
    public const string SendToBack = "canvas.layer.sendToBack";

    public const string TogglePreview = "canvas.preview.toggle";
    public const string RunPreview = "canvas.preview.run";
    public const string ExplainPlan = "canvas.preview.explain";

    public const string ZoomIn = "canvas.zoom.in";
    public const string ZoomOut = "canvas.zoom.out";
    public const string ZoomReset = "canvas.zoom.reset";
    public const string ToggleCteEditor = "canvas.cte.toggleEditor";

    public const string SqlTabNew = "sql.tab.new";
    public const string SqlTabClose = "sql.tab.close";
    public const string SqlTabOpenFile = "sql.file.open";
    public const string SqlTabSaveFile = "sql.file.save";
    public const string SqlRunAll = "sql.run.all";
    public const string SqlRunSelection = "sql.run.selection";
    public const string SqlRunCurrent = "sql.run.current";
    public const string SqlCancelExecution = "sql.execution.cancel";
}
