using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Services;

/// <summary>
/// Handles file save/open dialogs and canvas serialization.
/// </summary>
public class FileOperationsService(Window window, CanvasViewModel vm)
{
    private static readonly FilePickerFileType FileType = new("SQL Architect Canvas")
    {
        Patterns = ["*.vsaq"],
        MimeTypes = ["application/json"],
    };

    private readonly Window _window = window;
    private readonly CanvasViewModel _vm = vm;

    public async Task SaveAsync(bool saveAs = false)
    {
        string? path = (!saveAs && _vm.CurrentFilePath is not null) ? _vm.CurrentFilePath : null;

        if (path is null)
        {
            IStorageFile? r = await _window.StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Save Canvas",
                    DefaultExtension = "vsaq",
                    FileTypeChoices = [FileType],
                    SuggestedFileName = "Query1",
                }
            );
            path = r?.TryGetLocalPath();
        }

        if (path is null)
            return;

        try
        {
            await CanvasSerializer.SaveToFileAsync(path, _vm);
            _vm.CurrentFilePath = path;
            _vm.IsDirty = false;
        }
        catch (Exception ex)
        {
            _vm.DataPreview.ShowError($"Save failed: {ex.Message}", ex);
        }
    }

    public async Task OpenAsync()
    {
        IReadOnlyList<IStorageFile> results = await _window.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open Canvas",
                FileTypeFilter = [FileType],
                AllowMultiple = false,
            }
        );

        string? path = results.FirstOrDefault()?.TryGetLocalPath();
        if (path is null)
            return;

        try
        {
            CanvasLoadResult result = await CanvasSerializer.LoadFromFileAsync(path, _vm);
            if (!result.Success)
            {
                _vm.DataPreview.ShowError($"Open failed: {result.Error}", null);
                return;
            }

            _vm.CurrentFilePath = path;
            _vm.IsDirty = false;
            _window.FindControl<Avalonia.Controls.Grid>("TheCanvas")?.InvalidateVisual();

            if (result.Warnings is { Count: > 0 })
                foreach (string w in result.Warnings)
                    _vm.Diagnostics.AddInfo($"Canvas migration: {w}");
        }
        catch (Exception ex)
        {
            _vm.DataPreview.ShowError($"Open failed: {ex.Message}", ex);
        }
    }
}
