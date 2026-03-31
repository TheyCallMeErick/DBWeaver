using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Services;

/// <summary>
/// Centralizes keyboard input handling.
/// Routes 15+ keyboard shortcuts to appropriate commands and overlays.
/// </summary>
public class KeyboardInputHandler
{
    private readonly Window? _window;
    private readonly CanvasViewModel _vm;
    private readonly FileOperationsService? _fileOps;
    private readonly Action? _onCreateNewCanvas;
    private readonly Action? _showShortcutsAction = null;
    private readonly Action? _openSearchAction = null;

    public KeyboardInputHandler(
        Window window,
        CanvasViewModel vm,
        FileOperationsService fileOps,
        Action? onCreateNewCanvas = null
    )
    {
        _window = window;
        _vm = vm;
        _fileOps = fileOps;
        _onCreateNewCanvas = onCreateNewCanvas;
    }

    public KeyboardInputHandler(
        CanvasViewModel vm,
        Action? onCreateNewCanvas = null,
        Action? showShortcutsAction = null,
        Action? openSearchAction = null
    )
    {
        _vm = vm;
        _onCreateNewCanvas = onCreateNewCanvas;
        _showShortcutsAction = showShortcutsAction;
        _openSearchAction = openSearchAction;
        _window = null;
        _fileOps = null;
    }

    public void Wire()
    {
        if (_window is not null)
            _window.KeyDown += OnKeyDown;
    }

    public void OnKeyDown(object? s, KeyEventArgs e)
    {
        if (e.Handled)
            return;

        if (HandleShortcut(e.Key, e.KeyModifiers))
            e.Handled = true;
    }

    public bool HandleShortcut(Key key, KeyModifiers modifiers)
    {
        // Handle overlay escape keys
        if (key == Key.Escape)
        {
            if (_vm.CommandPalette.IsVisible)
            {
                _vm.CommandPalette.Close();
                return true;
            }
            if (_vm.SearchMenu.IsVisible)
            {
                _vm.SearchMenu.Close();
                return true;
            }
            if (_vm.AutoJoin.IsVisible)
            {
                _vm.AutoJoin.Dismiss();
                return true;
            }
            if (_vm.DataPreview.IsVisible)
            {
                _vm.DataPreview.IsVisible = false;
                return true;
            }
            if (_vm.ConnectionManager.IsVisible)
            {
                _vm.ConnectionManager.IsVisible = false;
                return true;
            }
            if (_vm.Benchmark.IsVisible)
            {
                _vm.Benchmark.IsVisible = false;
                return true;
            }
            if (_vm.ExplainPlan.IsVisible)
            {
                _vm.ExplainPlan.Close();
                return true;
            }
            if (_vm.SqlImporter.IsVisible)
            {
                _vm.SqlImporter.Close();
                return true;
            }
            if (_vm.FlowVersions.IsVisible)
            {
                _vm.FlowVersions.Close();
                return true;
            }
            if (_vm.FileHistory.IsVisible)
            {
                _vm.FileHistory.Close();
                return true;
            }
        }

        // Shortcuts requiring overlay checks
        if (
            key == Key.A
            && modifiers.HasFlag(KeyModifiers.Shift)
            && !_vm.SearchMenu.IsVisible
        )
        {
            OpenSearch();
            return true;
        }
        if (
            key == Key.F
            && modifiers.HasFlag(KeyModifiers.Control)
            && !_vm.SearchMenu.IsVisible
        )
        {
            OpenSearch();
            return true;
        }

        // File operations
        if (key == Key.S && modifiers.HasFlag(KeyModifiers.Control) && _fileOps is not null)
        {
            _ = _fileOps.SaveAsync(saveAs: false);
            return true;
        }
        if (key == Key.O && modifiers.HasFlag(KeyModifiers.Control) && _fileOps is not null)
        {
            _ = _fileOps.OpenAsync();
            return true;
        }
        if (key == Key.N && modifiers.HasFlag(KeyModifiers.Control))
        {
            _onCreateNewCanvas?.Invoke();
            return true;
        }

        // Undo/Redo
        if (key == Key.Z && modifiers.HasFlag(KeyModifiers.Control))
        {
            _vm.UndoRedo.Undo();
            return true;
        }
        if (key == Key.Y && modifiers.HasFlag(KeyModifiers.Control))
        {
            _vm.UndoRedo.Redo();
            return true;
        }

        // Layer ordering
        if (key == Key.PageUp && modifiers.HasFlag(KeyModifiers.Control))
        {
            if (modifiers.HasFlag(KeyModifiers.Shift))
                _vm.BringSelectionToFrontCommand.Execute(null);
            else
                _vm.BringSelectionForwardCommand.Execute(null);
            return true;
        }
        if (key == Key.PageDown && modifiers.HasFlag(KeyModifiers.Control))
        {
            if (modifiers.HasFlag(KeyModifiers.Shift))
                _vm.SendSelectionToBackCommand.Execute(null);
            else
                _vm.SendSelectionBackwardCommand.Execute(null);
            return true;
        }

        // Command palette
        if (key == Key.K && modifiers.HasFlag(KeyModifiers.Control))
        {
            _vm.CommandPalette.Open();
            return true;
        }

        // Shortcuts help
        if (key == Key.F1)
        {
            if (_showShortcutsAction is not null)
                _showShortcutsAction();
            else if (_window is not null)
                new KeyboardShortcutsWindow().Show(_window);
            return true;
        }

        // Flow Version History (Ctrl+Shift+H)
        if (
            key == Key.H
            && modifiers.HasFlag(KeyModifiers.Control)
            && modifiers.HasFlag(KeyModifiers.Shift)
        )
        {
            _vm.FlowVersions.Open();
            return true;
        }

        // Save/Load local file history (Ctrl+Alt+H)
        if (
            key == Key.H
            && modifiers.HasFlag(KeyModifiers.Control)
            && modifiers.HasFlag(KeyModifiers.Alt)
        )
        {
            _vm.FileHistory.Open();
            return true;
        }

        // Connection manager
        if (
            key == Key.C
            && modifiers.HasFlag(KeyModifiers.Control)
            && modifiers.HasFlag(KeyModifiers.Shift)
        )
        {
            _vm.ConnectionManager.Open();
            return true;
        }

        // Canvas operations
        if (key == Key.L && modifiers.HasFlag(KeyModifiers.Control))
        {
            _vm.RunAutoLayout();
            _window?.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
            return true;
        }
        if (key == Key.G && modifiers.HasFlag(KeyModifiers.Control))
        {
            _vm.ToggleSnapCommand.Execute(null);
            return true;
        }

        // Explain Plan
        if (key == Key.F4)
        {
            _vm.ExplainPlan.Open();
            return true;
        }

        // Preview
        if (key == Key.F3)
        {
            _vm.DataPreview.Toggle();
            return true;
        }
        if (key == Key.F5)
        {
            return true;
        } // Handled by command palette

        // Zoom
        if (
            (key == Key.OemPlus || key == Key.Add)
            && modifiers.HasFlag(KeyModifiers.Control)
        )
        {
            _vm.ZoomInCommand.Execute(null);
            return true;
        }
        if (
            (key == Key.OemMinus || key == Key.Subtract)
            && modifiers.HasFlag(KeyModifiers.Control)
        )
        {
            _vm.ZoomOutCommand.Execute(null);
            return true;
        }
        if (
            (key == Key.D0 || key == Key.NumPad0)
            && modifiers.HasFlag(KeyModifiers.Control)
        )
        {
            _vm.ResetZoomCommand.Execute(null);
            return true;
        }

        return false;
    }

    private void OpenSearch()
    {
        if (_openSearchAction is not null)
        {
            _openSearchAction();
            return;
        }

        if (_window is null)
            return;

        InfiniteCanvas? canvas = _window.FindControl<InfiniteCanvas>("TheCanvas");
        Point ctr = canvas is not null
            ? new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2)
            : new Point(400, 300);
        _vm.SearchMenu.Open(ctr);
    }
}
