using Avalonia.Controls;
using Avalonia.Input;
using DBWeaver.UI.Controls;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Controls.Shell;

public partial class DiagramDocumentPageControl : UserControl
{
    private Panel? _overlayDismissBackdrop;
    private CanvasViewModel? _viewModel;

    public DiagramDocumentPageControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AddHandler(KeyDownEvent, OnHostKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        _overlayDismissBackdrop = this.FindControl<Panel>("OverlayDismissBackdrop");
        if (_overlayDismissBackdrop is not null)
            _overlayDismissBackdrop.PointerPressed += (_, _) => TryDismissTopOverlay();
    }

    private void OnHostKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        if (TryDismissTopOverlay())
            e.Handled = true;
    }

    public InfiniteCanvas? CanvasControl => this.FindControl<InfiniteCanvas>("TheCanvas");

    public SearchMenuControl? SearchOverlayControl => this.FindControl<SearchMenuControl>("SearchOverlay");

    public void InvalidateCanvasWires()
    {
        CanvasControl?.InvalidateWires();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as CanvasViewModel;
    }

    private bool TryDismissTopOverlay()
    {
        if (_viewModel is null)
            return false;

        if (_viewModel.FileHistory.IsVisible)
        {
            _viewModel.FileHistory.Close();
            return true;
        }

        if (_viewModel.FlowVersions.IsVisible)
        {
            _viewModel.FlowVersions.Close();
            return true;
        }

        if (_viewModel.SqlImporter.IsVisible)
        {
            if (_viewModel.SqlImporter.IsImporting)
                _viewModel.SqlImporter.CancelImport();
            else
                _viewModel.SqlImporter.Close();

            return true;
        }

        if (_viewModel.ExplainPlan.IsVisible)
        {
            _viewModel.ExplainPlan.Close();
            return true;
        }

        if (_viewModel.Benchmark.IsVisible)
        {
            _viewModel.Benchmark.CloseCommand.Execute(null);
            return true;
        }

        if (VisualRoot is Window window && window.DataContext is ShellViewModel shellViewModel && shellViewModel.CommandPalette.IsVisible)
        {
            shellViewModel.CommandPalette.Close();
            return true;
        }

        return false;
    }
}
