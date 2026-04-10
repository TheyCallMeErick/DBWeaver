using Avalonia.Controls;
using DBWeaver.UI.Controls.Shell;
using DBWeaver.UI.Serialization;
using DBWeaver.UI.Services.Workspace.Models;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI;

public partial class MainWindow
{
    private static string CreateFreshCanvasSnapshot()
    {
        using var vm = new CanvasViewModel();
        return CanvasSerializer.Serialize(vm);
    }

    private void InvalidateActiveDiagramCanvasWires()
    {
        if (CurrentShell.IsDdlDocumentPageActive)
        {
            this.FindControl<DiagramDocumentPageControl>("DdlDocumentPage")?.InvalidateCanvasWires();
            return;
        }

        this.FindControl<DiagramDocumentPageControl>("QueryDocumentPage")?.InvalidateCanvasWires();
    }

    private void ResetCurrentCanvas()
    {
        CanvasViewModel? activeCanvas = CurrentShell.ActiveCanvas;
        if (activeCanvas is null)
            return;

        CanvasLoadResult result = CanvasSerializer.Deserialize(CreateFreshCanvasSnapshot(), activeCanvas);
        if (!result.Success)
        {
            activeCanvas.DataPreview.ShowError(LF("tab.resetFailed", "Falha ao limpar documento ativo: {0}", result.Error ?? string.Empty), null);
            return;
        }

        activeCanvas.CurrentFilePath = null;
        activeCanvas.IsDirty = false;
        Title = activeCanvas.WindowTitle;
        InvalidateActiveDiagramCanvasWires();
    }

    private void CreateNewQueryTab()
    {
        if (CurrentShell.ActiveWorkspaceDocumentType == WorkspaceDocumentType.SqlEditor)
        {
            CurrentShell.SetActiveDocumentType(WorkspaceDocumentType.QueryCanvas);
            SyncModeToggleState();
        }

        EnterCanvasMode();
        ResetCurrentCanvas();
    }
}
