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
public class KeyboardInputHandler(Window window, CanvasViewModel vm)
{
    private readonly Window _window = window;
    private readonly CanvasViewModel _vm = vm;

    public void Wire() => _window.KeyDown += OnKeyDown;

    public void OnKeyDown(object? s, KeyEventArgs e)
    {
        // Handle overlay escape keys
        if (e.Key == Key.Escape)
        {
            if (_vm.CommandPalette.IsVisible)
            {
                _vm.CommandPalette.Close();
                e.Handled = true;
                return;
            }
            if (_vm.SearchMenu.IsVisible)
            {
                _vm.SearchMenu.Close();
                e.Handled = true;
                return;
            }
            if (_vm.AutoJoin.IsVisible)
            {
                _vm.AutoJoin.Dismiss();
                e.Handled = true;
                return;
            }
            if (_vm.DataPreview.IsVisible)
            {
                _vm.DataPreview.IsVisible = false;
                e.Handled = true;
                return;
            }
            if (_vm.ConnectionManager.IsVisible)
            {
                _vm.ConnectionManager.IsVisible = false;
                e.Handled = true;
                return;
            }
            if (_vm.Benchmark.IsVisible)
            {
                _vm.Benchmark.IsVisible = false;
                e.Handled = true;
                return;
            }
            if (_vm.ExplainPlan.IsVisible)
            {
                _vm.ExplainPlan.Close();
                e.Handled = true;
                return;
            }
            if (_vm.SqlImporter.IsVisible)
            {
                _vm.SqlImporter.Close();
                e.Handled = true;
                return;
            }
        }

        // Shortcuts requiring overlay checks
        if (
            e.Key == Key.A
            && e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            && !_vm.SearchMenu.IsVisible
        )
        {
            OpenSearch();
            e.Handled = true;
            return;
        }
        if (
            e.Key == Key.F
            && e.KeyModifiers.HasFlag(KeyModifiers.Control)
            && !_vm.SearchMenu.IsVisible
        )
        {
            OpenSearch();
            e.Handled = true;
            return;
        }

        // File operations
        if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;
            return;
        } // Handled by command palette
        if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;
            return;
        } // Handled by command palette
        if (e.Key == Key.N && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _window.DataContext = new CanvasViewModel();
            e.Handled = true;
            return;
        }

        // Undo/Redo
        if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _vm.UndoRedo.Undo();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Y && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _vm.UndoRedo.Redo();
            e.Handled = true;
            return;
        }

        // Command palette
        if (e.Key == Key.K && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _vm.CommandPalette.Open();
            e.Handled = true;
            return;
        }

        // Connection manager
        if (
            e.Key == Key.C
            && e.KeyModifiers.HasFlag(KeyModifiers.Control)
            && e.KeyModifiers.HasFlag(KeyModifiers.Shift)
        )
        {
            _vm.ConnectionManager.Open();
            e.Handled = true;
            return;
        }

        // Canvas operations
        if (e.Key == Key.L && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _vm.RunAutoLayout();
            _window.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.G && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _vm.ToggleSnapCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Explain Plan
        if (e.Key == Key.F4)
        {
            _vm.ExplainPlan.Open();
            e.Handled = true;
            return;
        }

        // Preview
        if (e.Key == Key.F3)
        {
            _vm.DataPreview.Toggle();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.F5)
        {
            e.Handled = true;
            return;
        } // Handled by command palette

        // Zoom
        if (
            (e.Key == Key.OemPlus || e.Key == Key.Add)
            && e.KeyModifiers.HasFlag(KeyModifiers.Control)
        )
        {
            _vm.ZoomInCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (
            (e.Key == Key.OemMinus || e.Key == Key.Subtract)
            && e.KeyModifiers.HasFlag(KeyModifiers.Control)
        )
        {
            _vm.ZoomOutCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (
            (e.Key == Key.D0 || e.Key == Key.NumPad0)
            && e.KeyModifiers.HasFlag(KeyModifiers.Control)
        )
        {
            _vm.FitToScreenCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OpenSearch()
    {
        InfiniteCanvas? canvas = _window.FindControl<InfiniteCanvas>("TheCanvas");
        Point ctr = canvas is not null
            ? new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2)
            : new Point(400, 300);
        _vm.SearchMenu.Open(ctr);
    }
}
