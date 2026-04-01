using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Services.Localization;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

/// <summary>
/// Interaction responsibilities extracted from InfiniteCanvas:
/// pointer/keyboard gestures, context menu, rubber-band visuals and alignment guides.
/// </summary>
public sealed partial class InfiniteCanvas
{
    // Note: In Avalonia 11+, Panel.Render() is sealed and cannot be overridden.
    // Rubber band selection rectangle is drawn as a visual element instead.
    private Border? _rubberBandRect;

    private const double GuideThreshold = 8.0; // canvas units — how close to snap a guide
    private const double DefaultNodeH = 130;

    private bool TryDeleteWire(ConnectionViewModel? wire)
    {
        if (ViewModel is null || wire is null)
            return false;

        ClearHoverHighlights();
        _wires.AddRemovalFlash(wire);
        ViewModel.DeleteConnection(wire);
        RequestWireSync();
        _wires.InvalidateVisual();
        return true;
    }

    private void OnPinPressed(object? s, (PinViewModel Pin, Point Canvas) a)
    {
        _pinDrag?.BeginDrag(a.Pin, a.Pin.AbsolutePosition);
        _wires.PendingConnection = _pinDrag?.LiveWire;
        Log($">>> PIN DRAG STARTED: Pin='{a.Pin.Owner.Title}.{a.Pin.Name}'");
    }

    private void OnWheel(object? s, PointerWheelEventArgs e)
    {
        if (ViewModel is null)
            return;
        double oldZoom = _zoom;
        Point oldPan = _panOffset;

        Point screen = e.GetPosition(this);
        double factor = e.Delta.Y > 0 ? 1.10 : 0.91;

        // Nodify-style approach: update viewport from a single source of truth (ViewModel),
        // then sync once to the visual transform.
        _isApplyingViewportFromCanvas = true;
        try
        {
            ViewModel.ZoomToward(screen, factor);
        }
        finally
        {
            _isApplyingViewportFromCanvas = false;
        }

        SyncTransform();
        SyncWires();
        e.Handled = true;
        Log($"    ZOOM: {oldZoom:F2} -> {_zoom:F2}, Pan: {oldPan} -> {_panOffset}, Delta={e.Delta.Y}");
    }

    private void OnPressed(object? s, PointerPressedEventArgs e)
    {
        Focus();
        PointerPointProperties props = e.GetCurrentPoint(this).Properties;
        Point screen = e.GetPosition(this);

        if (
            props.IsMiddleButtonPressed
            || (props.IsLeftButtonPressed && _isSpacePanArmed)
            || (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        )
        {
            ClearHoverHighlights();
            Log($">>> PAN STARTED: Screen={screen}, PanOffset={_panOffset}, Zoom={_zoom}");
            _isPanning = true;
            _panStart = screen; // Store screen position, not difference
            _startPanOffset = _panOffset; // NodifyM pattern: capture pan offset at start
            e.Pointer.Capture(this);
            Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Handled = true;
            return;
        }

        // Nodify-style usability: right-drag pans, right-click opens context menu.
        if (props.IsRightButtonPressed)
        {
            _contextMenuPending = true;
            _contextMenuPressScreen = screen;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (props.IsLeftButtonPressed)
        {
            Point canvas = ScreenToCanvas(screen);
            _rubberStart = canvas;
            _rubberCurrent = canvas;

            // Fast delete gesture for wires: Ctrl+Click on a wire removes it.
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                ConnectionViewModel? wireUnderPointer = _wires.HitTestWire(canvas, tolerance: 8);
                if (TryDeleteWire(wireUnderPointer))
                {
                    e.Handled = true;
                    return;
                }
            }

            // If a NodeControl already fired PinPressed (which called BeginDrag via OnPinPressed),
            // the event bubbled here without e.Handled — just capture the pointer so that
            // move and release events are owned by the canvas for the full drag gesture.
            if (_pinDrag?.IsDragging == true)
            {
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
            PinViewModel? pin = _pinDrag?.HitTestPin(canvas);
            if (pin is not null)
            {
                _pinDrag!.BeginDrag(pin, pin.AbsolutePosition);
                _wires.PendingConnection = _pinDrag.LiveWire;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }

            // Rubber band intentionally disabled (temporary workaround for critical wire issue).
            _isRubberBanding = false;
            UpdateRubberBandVisual();
            ViewModel?.DeselectAll();
            e.Handled = true;
            return;
        }
    }

    private void OnMoved(object? s, PointerEventArgs e)
    {
        Point screen = e.GetPosition(this);
        Point canvas = ScreenToCanvas(screen);

        if (_contextMenuPending && !_isPanning)
        {
            Vector d = screen - _contextMenuPressScreen;
            if (d.X * d.X + d.Y * d.Y >= ContextPanStartThreshold * ContextPanStartThreshold)
            {
                _contextMenuPending = false;
                _isPanning = true;
                _panStart = screen;
                _startPanOffset = _panOffset;
                Cursor = new Cursor(StandardCursorType.SizeAll);
                Log($">>> PAN STARTED (right-drag): Screen={screen}, PanOffset={_panOffset}, Zoom={_zoom}");
            }
            else
            {
                return;
            }
        }

        if (_isPanning)
        {
            ClearHoverHighlights();
            Point delta = screen - _panStart;
            _panOffset = _panOffset + (Vector)delta;

            Log($"    PAN MOVING: Screen={screen}, Delta={delta}, NewPanOffset={_panOffset}, Zoom={_zoom}");

            // Force immediate visual update during pan (don't wait for layout pass)
            _scene.RenderTransform = new TransformGroup
            {
                Children =
                [
                    new ScaleTransform(_zoom, _zoom),
                    new TranslateTransform(_panOffset.X, _panOffset.Y),
                ],
            };
            _grid.PanOffset = _panOffset;
            _grid.InvalidateVisual();

            _panStart = screen; // Update for next frame
            return;
        }
        if (_pinDrag?.IsDragging == true)
        {
            ClearHoverHighlights();
            _pinDrag.UpdateDrag(canvas);
            _wires.PendingConnection = _pinDrag.LiveWire;
            _wires.InvalidateVisual();
            return;
        }
        if (RubberBandEnabled && _isRubberBanding)
        {
            _rubberCurrent = canvas;
            UpdateRubberBand();
            UpdateRubberBandVisual();

            // Ensure wires stay visible during rubber band drag
            _wires.InvalidateVisual();

            // Keep wires on top as nodes are selected
            EnsureWiresOnTop();
        }

        // Always ensure wires are on top after any movement
        if (!_isPanning && !(_pinDrag?.IsDragging == true))
        {
            EnsureWiresOnTop();
            UpdateHoverHighlights(canvas);
        }
    }

    private void OnReleased(object? s, PointerReleasedEventArgs e)
    {
        Point screen = e.GetPosition(this);
        Point canvas = ScreenToCanvas(e.GetPosition(this));

        if (_contextMenuPending)
        {
            _contextMenuPending = false;
            e.Pointer.Capture(null);
            ShowContextMenu(screen);
            return;
        }

        if (_isPanning)
        {
            Log($"<<< PAN COMPLETED: FinalPanOffset={_panOffset}, Zoom={_zoom}");
            // Final sync to ViewModel to ensure persistence
            if (ViewModel is not null)
            {
                ViewModel.PanOffset = _panOffset;
                Log($"    Synced to ViewModel: PanOffset={_panOffset}");
            }
            _isPanning = false;
            e.Pointer.Capture(null);
            Cursor = _isSpacePanArmed ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            return;
        }
        if (_pinDrag?.IsDragging == true)
        {
            _pinDrag.EndDrag(canvas);
            _wires.PendingConnection = null;
            _wires.InvalidateVisual();
            e.Pointer.Capture(null);
            return;
        }
        if (RubberBandEnabled && _isRubberBanding)
        {
            Log($"<<< RUBBER BAND COMPLETED: Canvas={canvas}");
            _isRubberBanding = false;
            UpdateRubberBand();
            UpdateRubberBandVisual();

            // Ensure wires are fully synced and visible after rubber band selection
            SyncWires();
            EnsureWiresOnTop();
        }
    }

    private void OnKey(object? s, KeyEventArgs e)
    {
        if (ViewModel is null)
            return;

        if (e.Key == Key.Space)
        {
            _isSpacePanArmed = true;
            if (!_isPanning)
                Cursor = new Cursor(StandardCursorType.Hand);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            List<NodeViewModel> selected = ViewModel.Nodes.Where(n => n.IsSelected).ToList();
            if (selected.Count > 0)
            {
                double step = ViewModel.SnapToGrid ? CanvasViewModel.GridSize : 1.0;
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    step *= 10.0;

                double dx = 0, dy = 0;
                switch (e.Key)
                {
                    case Key.Left:
                        dx = -step;
                        break;
                    case Key.Right:
                        dx = step;
                        break;
                    case Key.Up:
                        dy = -step;
                        break;
                    case Key.Down:
                        dy = step;
                        break;
                }

                foreach (NodeViewModel n in selected)
                {
                    double nx = n.Position.X + dx;
                    double ny = n.Position.Y + dy;
                    if (ViewModel.SnapToGrid)
                    {
                        nx = CanvasViewModel.Snap(nx);
                        ny = CanvasViewModel.Snap(ny);
                    }
                    n.Position = new Point(nx, ny);
                }

                SyncWires();
                InvalidateArrange();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.F)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                if (FitSelectionInView())
                {
                    e.Handled = true;
                    return;
                }
            }

            if (CenterSelectionInView())
            {
                e.Handled = true;
                return;
            }
        }
        if (e.Key == Key.Escape)
        {
            _pinDrag?.CancelDrag();
            _wires.PendingConnection = null;
            _wires.InvalidateVisual();
            ClearHoverHighlights();
            ViewModel.DeselectAll();
            ViewModel.SearchMenu.Close();
            _isRubberBanding = false;
            UpdateRubberBandVisual();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Delete or Key.Back)
        {
            if (TryDeleteWire(_hoveredWire))
            {
                e.Handled = true;
                return;
            }
        }
    }

    private void OnKeyUp(object? s, KeyEventArgs e)
    {
        if (e.Key != Key.Space)
            return;

        _isSpacePanArmed = false;
        if (!_isPanning)
            Cursor = Cursor.Default;
        e.Handled = true;
    }

    private void UpdateRubberBand()
    {
        if (ViewModel is null)
            return;
        var r = new Rect(
            Math.Min(_rubberStart.X, _rubberCurrent.X),
            Math.Min(_rubberStart.Y, _rubberCurrent.Y),
            Math.Abs(_rubberCurrent.X - _rubberStart.X),
            Math.Abs(_rubberCurrent.Y - _rubberStart.Y)
        );
        foreach (NodeViewModel n in ViewModel.Nodes)
            n.IsSelected = r.Contains(n.Position);
    }

    private void UpdateRubberBandVisual()
    {
        if (!_isRubberBanding)
        {
            if (_rubberBandRect != null)
            {
                _scene.Children.Remove(_rubberBandRect);
                _rubberBandRect = null;

                // Ensure wires stay on top after rubber band is removed
                EnsureWiresOnTop();
            }
            return;
        }

        double x = _panOffset.X + Math.Min(_rubberStart.X, _rubberCurrent.X) * _zoom;
        double y = _panOffset.Y + Math.Min(_rubberStart.Y, _rubberCurrent.Y) * _zoom;
        double w = Math.Abs(_rubberCurrent.X - _rubberStart.X) * _zoom;
        double h = Math.Abs(_rubberCurrent.Y - _rubberStart.Y) * _zoom;

        if (_rubberBandRect == null)
        {
            _rubberBandRect = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#103B82F6")),
                BorderBrush = new SolidColorBrush(Color.Parse("#3B82F6")),
                BorderThickness = new Thickness(1),
                IsHitTestVisible = false,
            };
            // Add rubber band before wires so it renders behind
            int wiresIndex = _scene.Children.IndexOf(_wires);
            if (wiresIndex >= 0)
            {
                _scene.Children.Insert(wiresIndex, _rubberBandRect);
            }
            else
            {
                _scene.Children.Add(_rubberBandRect);
            }
        }

        Canvas.SetLeft(_rubberBandRect, x);
        Canvas.SetTop(_rubberBandRect, y);
        _rubberBandRect.Width = w;
        _rubberBandRect.Height = h;

        // Ensure wires remain visible during rubber band selection
        EnsureWiresOnTop();
        _wires.InvalidateVisual();
    }

    private void ShowContextMenu(Point screen)
    {
        if (ViewModel is null)
            return;

        Point canvasPoint = ScreenToCanvas(screen);
        ConnectionViewModel? wireUnderPointer = _wires.HitTestWire(canvasPoint, tolerance: 8);
        var sel = ViewModel.Nodes.Where(n => n.IsSelected).ToList();
        var menu = new ContextMenu();
        if (sel.Count > 0)
        {
            menu.Items.Add(
                new MenuItem
                {
                    Header = $"Delete {(sel.Count == 1 ? sel[0].Title : $"{sel.Count} nodes")}",
                    Command = new RelayCommand(ViewModel.DeleteSelected),
                }
            );

            if (sel.Count == 1 && sel[0].Type == NodeType.CteDefinition)
            {
                NodeViewModel cteNode = sel[0];
                menu.Items.Add(
                    new MenuItem
                    {
                        Header = LocalizationService.Instance["context.editCte"],
                        Command = new RelayCommand(() => ViewModel.EnterCteEditor(cteNode)),
                    }
                );
            }

            menu.Items.Add(
                new MenuItem
                {
                    Header = "Bring Forward (Ctrl+PgUp)",
                    Command = ViewModel.BringSelectionForwardCommand,
                }
            );
            menu.Items.Add(
                new MenuItem
                {
                    Header = "Send Backward (Ctrl+PgDown)",
                    Command = ViewModel.SendSelectionBackwardCommand,
                }
            );
            menu.Items.Add(
                new MenuItem
                {
                    Header = "Bring to Front (Ctrl+Shift+PgUp)",
                    Command = ViewModel.BringSelectionToFrontCommand,
                }
            );
            menu.Items.Add(
                new MenuItem
                {
                    Header = "Send to Back (Ctrl+Shift+PgDown)",
                    Command = ViewModel.SendSelectionToBackCommand,
                }
            );
            menu.Items.Add(
                new MenuItem
                {
                    Header = "Normalize Layers",
                    Command = ViewModel.NormalizeLayersCommand,
                }
            );
        }
        else if (wireUnderPointer is not null)
        {
            menu.Items.Add(
                new MenuItem
                {
                    Header = "Delete wire",
                    Command = new RelayCommand(() => TryDeleteWire(wireUnderPointer)),
                }
            );
        }
        else
        {
            menu.Items.Add(
                new MenuItem
                {
                    Header = "Add Node (Shift+A)",
                    Command = new RelayCommand(() =>
                        ViewModel.SearchMenu.Open(ScreenToCanvas(screen))
                    ),
                }
            );
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(
            new MenuItem
            {
                Header = $"Undo {ViewModel.UndoRedo.UndoDescription}",
                Command = ViewModel.UndoCommand,
            }
        );
        menu.Items.Add(new MenuItem { Header = "Redo", Command = ViewModel.RedoCommand });
        menu.Open(this);
    }

    /// <summary>
    /// Detects horizontal and vertical alignment between the dragged node and all
    /// other (non-selected) nodes. Guides appear for: left edge, right edge,
    /// vertical centre, top edge, bottom edge, horizontal centre.
    /// </summary>
    private void UpdateAlignGuides(NodeViewModel dragged)
    {
        if (ViewModel is null)
        {
            _guides.ClearGuides();
            return;
        }

        var others = ViewModel.Nodes.Where(n => n != dragged && !n.IsSelected).ToList();

        double dX = dragged.Position.X;
        double dW = dragged.Width > 0 ? dragged.Width : 230;
        double dCX = dX + dW / 2.0;
        double dRX = dX + dW;

        double dY = dragged.Position.Y;
        double dH = DefaultNodeH;
        double dCY = dY + dH / 2.0;
        double dBY = dY + dH;

        var hGuides = new List<double>(); // Y coordinates of horizontal guides
        var vGuides = new List<double>(); // X coordinates of vertical guides

        foreach (NodeViewModel n in others)
        {
            double oX = n.Position.X;
            double oW = n.Width > 0 ? n.Width : 230;
            double oCX = oX + oW / 2.0;
            double oRX = oX + oW;

            double oY = n.Position.Y;
            double oH = DefaultNodeH;
            double oCY = oY + oH / 2.0;
            double oBY = oY + oH;

            // Vertical guides (X alignment)
            if (Math.Abs(dX - oX) < GuideThreshold)
                vGuides.Add(oX);
            if (Math.Abs(dX - oRX) < GuideThreshold)
                vGuides.Add(oRX);
            if (Math.Abs(dCX - oCX) < GuideThreshold)
                vGuides.Add(oCX);
            if (Math.Abs(dRX - oX) < GuideThreshold)
                vGuides.Add(oX);
            if (Math.Abs(dRX - oRX) < GuideThreshold)
                vGuides.Add(oRX);

            // Horizontal guides (Y alignment)
            if (Math.Abs(dY - oY) < GuideThreshold)
                hGuides.Add(oY);
            if (Math.Abs(dY - oBY) < GuideThreshold)
                hGuides.Add(oBY);
            if (Math.Abs(dCY - oCY) < GuideThreshold)
                hGuides.Add(oCY);
            if (Math.Abs(dBY - oY) < GuideThreshold)
                hGuides.Add(oY);
            if (Math.Abs(dBY - oBY) < GuideThreshold)
                hGuides.Add(oBY);
        }

        _guides.SetGuides([.. hGuides.Distinct()], [.. vGuides.Distinct()]);
        if (hGuides.Count > 0 || vGuides.Count > 0)
            Log($"    Guides updated: {hGuides.Distinct().Count()} h, {vGuides.Distinct().Count()} v");
    }
}
