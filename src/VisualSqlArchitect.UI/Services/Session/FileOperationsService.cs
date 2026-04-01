using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Controls;
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
            _vm.NotifySuccess("Canvas saved successfully.", path);
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
            CanvasLoadResult result = await CanvasSerializer.LoadFromFileAsync(path, _vm, BuildColumnLookup());
            if (!result.Success)
            {
                _vm.DataPreview.ShowError($"Open failed: {result.Error}", null);
                return;
            }

            _vm.CurrentFilePath = path;
            _vm.IsDirty = false;
            _window.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();

            string? warningDetails = result.Warnings is { Count: > 0 }
                ? string.Join(Environment.NewLine, result.Warnings)
                : null;
            if (warningDetails is null)
                _vm.NotifySuccess("Canvas opened successfully.", path);
            else
                _vm.NotifyWarning(
                    "Canvas opened with warnings.",
                    $"{path}{Environment.NewLine}{Environment.NewLine}{warningDetails}"
                );

            if (result.Warnings is { Count: > 0 })
                foreach (string w in result.Warnings)
                    _vm.Diagnostics.AddWarning(
                        area: "Canvas Migration",
                        message: $"Open: {w}",
                        recommendation: "Review diagnostics and re-save the canvas to persist the latest schema.",
                        openPanel: true
                    );
        }
        catch (Exception ex)
        {
            _vm.DataPreview.ShowError($"Open failed: {ex.Message}", ex);
        }
    }

    private IReadOnlyDictionary<string, IReadOnlyList<(string Name, PinDataType Type)>> BuildColumnLookup()
    {
        if (_vm.DatabaseMetadata is DbMetadata metadata)
            return BuildLookupFromMetadata(metadata);

        return CanvasViewModel.DemoCatalog.ToDictionary(t => t.FullName, t => t.Cols);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<(string Name, PinDataType Type)>> BuildLookupFromMetadata(
        DbMetadata metadata
    )
    {
        return metadata.Schemas
            .SelectMany(schema => schema.Tables)
            .ToDictionary(
                table => $"{table.Schema}.{table.Name}",
                table => (IReadOnlyList<(string Name, PinDataType Type)>)table.Columns
                    .OrderBy(column => column.OrdinalPosition)
                    .Select(column => (column.Name, MapDataTypeToPinDataType(column)))
                    .ToList(),
                StringComparer.OrdinalIgnoreCase
            );
    }

    private static PinDataType MapDataTypeToPinDataType(ColumnMetadata column)
    {
        return column.SemanticType switch
        {
            ColumnSemanticType.Numeric => PinDataType.Number,
            ColumnSemanticType.Text => PinDataType.Text,
            ColumnSemanticType.DateTime => PinDataType.DateTime,
            ColumnSemanticType.Boolean => PinDataType.Boolean,
            ColumnSemanticType.Guid => PinDataType.Text,
            ColumnSemanticType.Document => PinDataType.Json,
            ColumnSemanticType.Binary => PinDataType.Text,
            ColumnSemanticType.Spatial => PinDataType.Json,
            ColumnSemanticType.Other => PinDataType.Text,
            _ => PinDataType.Text,
        };
    }
}
