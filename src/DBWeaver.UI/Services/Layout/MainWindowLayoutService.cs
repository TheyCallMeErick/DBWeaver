using System.IO;
using System.Text.Json;
using Avalonia.Controls;
using System.ComponentModel;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services;

/// <summary>
/// Manages grid layout persistence and data preview pane height.
/// Handles saving/loading column widths and preview pane dimensions.
/// </summary>
public class MainWindowLayoutService(Window window, CanvasViewModel vm) : IDisposable
{
    private readonly Window _window = window;
    private readonly CanvasViewModel _vm = vm;
    private double _previewHeight = 220;
    private EventHandler<WindowClosingEventArgs>? _windowClosingHandler;
    private PropertyChangedEventHandler? _previewPropertyChangedHandler;
    private bool _isWired;

    private static string AppDataDir =>
        global::DBWeaver.UI.AppConstants.AppDataDirectory;

    private static string LayoutFile => Path.Combine(AppDataDir, "layout.json");

    public void Wire()
    {
        if (_isWired)
            return;

        LoadLayout();

        _windowClosingHandler = (_, _) => SaveLayout();
        _previewPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(DataPreviewViewModel.IsVisible))
                UpdatePreviewRow();
        };

        _window.Closing += _windowClosingHandler;
        _vm.DataPreview.PropertyChanged += _previewPropertyChangedHandler;
        _isWired = true;

        UpdatePreviewRow();
    }

    public void Dispose()
    {
        if (!_isWired)
            return;

        if (_windowClosingHandler is not null)
            _window.Closing -= _windowClosingHandler;

        if (_previewPropertyChangedHandler is not null)
            _vm.DataPreview.PropertyChanged -= _previewPropertyChangedHandler;

        _windowClosingHandler = null;
        _previewPropertyChangedHandler = null;
        _isWired = false;
    }

    private void UpdatePreviewRow()
    {
        Grid? centerGrid = _window.FindControl<Grid>("CenterGrid");
        if (centerGrid is null || centerGrid.RowDefinitions.Count < 3)
            return;

        if (_vm.DataPreview.IsVisible)
        {
            centerGrid.RowDefinitions[1].Height = new GridLength(8);
            centerGrid.RowDefinitions[2].Height = new GridLength(_previewHeight);
        }
        else
        {
            double current = centerGrid.RowDefinitions[2].Height.Value;
            if (current > 0)
                _previewHeight = current;
            centerGrid.RowDefinitions[1].Height = new GridLength(0);
            centerGrid.RowDefinitions[2].Height = new GridLength(0);
        }
    }

    public void SaveLayout()
    {
        Grid? bodyGrid = _window.FindControl<Grid>("BodyGrid");
        Grid? centerGrid = _window.FindControl<Grid>("CenterGrid");
        if (bodyGrid is null || centerGrid is null || bodyGrid.ColumnDefinitions.Count < 5)
            return;

        if (centerGrid.RowDefinitions.Count >= 3)
        {
            double current = centerGrid.RowDefinitions[2].Height.Value;
            if (current > 0)
                _previewHeight = current;
        }

        var layout = new LayoutState(
            bodyGrid.ColumnDefinitions[0].Width.Value,
            bodyGrid.ColumnDefinitions[4].Width.Value,
            _previewHeight
        );

        try
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllText(LayoutFile, JsonSerializer.Serialize(layout));
        }
        catch
        { /* best effort */
        }
    }

    private void LoadLayout()
    {
        try
        {
            if (!File.Exists(LayoutFile))
                return;

            LayoutState? layout = JsonSerializer.Deserialize<LayoutState>(
                File.ReadAllText(LayoutFile)
            );
            if (layout is null)
                return;

            Grid? bodyGrid = _window.FindControl<Grid>("BodyGrid");
            if (bodyGrid is not null && bodyGrid.ColumnDefinitions.Count >= 5)
            {
                bodyGrid.ColumnDefinitions[0].Width = new GridLength(
                    Math.Clamp(layout.LeftWidth, 200, 420)
                );
                bodyGrid.ColumnDefinitions[4].Width = new GridLength(
                    Math.Clamp(layout.RightWidth, 220, 500)
                );
            }

            if (layout.PreviewHeight > 0)
                _previewHeight = Math.Clamp(layout.PreviewHeight, 150, 600);
        }
        catch
        { /* best effort */
        }
    }

    public record LayoutState(double LeftWidth, double RightWidth, double PreviewHeight);
}
