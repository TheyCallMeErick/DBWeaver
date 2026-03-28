using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Services;

/// <summary>
/// Handles export operations for multiple formats.
/// Supports HTML, JSON, CSV, Excel, and Markdown documentation exports.
/// </summary>
public class ExportService(Window window, CanvasViewModel vm)
{
    private readonly Window _window = window;
    private readonly CanvasViewModel _vm = vm;

    public async Task RunExportDocumentationAsync()
    {
        string suggestedName = _vm.CurrentFilePath is not null
            ? Path.GetFileNameWithoutExtension(_vm.CurrentFilePath)
            : "flow-documentation";

        var mdType = new FilePickerFileType("Markdown Files") { Patterns = ["*.md"] };
        IStorageFile? result = await _window.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export Flow Documentation",
                DefaultExtension = "md",
                FileTypeChoices = [mdType],
                SuggestedFileName = suggestedName,
            }
        );

        string? path = result?.TryGetLocalPath();
        if (path is null)
            return;

        string? written = await FlowDocumentExporter.WriteAsync(_vm, path);
        if (written is not null)
            _vm.DataPreview.ShowError($"Documentation exported → {written}");
        else
            _vm.DataPreview.ShowError(
                "Documentation export failed — check file path and permissions."
            );
    }

    public async Task RunExportWithDialogAsync(
        NodeType exportType,
        string fileTypeName,
        string extension
    )
    {
        NodeViewModel? exportNode = _vm.Nodes.FirstOrDefault(n => n.Type == exportType);
        if (exportNode is null)
        {
            string msg =
                $"No {fileTypeName.Replace(" Files", "")} Export node found on the canvas. Add one via the node search menu.";
            _vm.DataPreview.ShowError(msg);
            return;
        }

        string suggestedName = exportNode.Parameters.TryGetValue("file_name", out string? fn)
            ? fn
            : $"export.{extension}";
        var fileType = new FilePickerFileType(fileTypeName) { Patterns = [$"*.{extension}"] };
        IStorageFile? result = await _window.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = $"Export as {extension.ToUpper()}",
                DefaultExtension = extension,
                FileTypeChoices = [fileType],
                SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedName),
            }
        );

        string? path = result?.TryGetLocalPath();
        if (path is null)
            return;

        string? written = await _vm.TriggerExportAsync(exportType, path);
        if (written is not null)
            _vm.DataPreview.ShowError($"Export successful → {written}");
        else
            _vm.DataPreview.ShowError($"Export failed — check file path and permissions.");
    }
}
