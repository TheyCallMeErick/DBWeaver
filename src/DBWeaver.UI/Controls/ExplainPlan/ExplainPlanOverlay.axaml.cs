using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Avalonia.Platform.Storage;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.Services.Workspace.Models;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.Controls;

public sealed partial class ExplainPlanOverlay : UserControl
{
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
    private ExplainPlanViewModel? _currentVm;

    public ExplainPlanOverlay()
    {
        InitializeComponent();

        Button? closeBtn = this.FindControl<Button>("CloseBtn");
        Button? runBtn   = this.FindControl<Button>("RunBtn");
        Button? snapshotBtn = this.FindControl<Button>("SnapshotBtn");
        Button? listModeBtn = this.FindControl<Button>("ListModeBtn");
        Button? treeModeBtn = this.FindControl<Button>("TreeModeBtn");
        Button? copyJsonBtn = this.FindControl<Button>("CopyJsonBtn");
        Button? copyTextBtn = this.FindControl<Button>("CopyTextBtn");
        Button? saveJsonBtn = this.FindControl<Button>("SaveJsonBtn");
        Button? openDaliboBtn = this.FindControl<Button>("OpenDaliboBtn");

        if (closeBtn is not null)
            closeBtn.Click += (_, _) => (DataContext as ExplainPlanViewModel)?.Close();

        if (runBtn is not null)
            runBtn.Click += async (_, _) =>
            {
                if (DataContext is ExplainPlanViewModel vm)
                    await vm.RunExplainAsync();
            };
        if (snapshotBtn is not null)
            snapshotBtn.Click += (_, _) => (DataContext as ExplainPlanViewModel)?.CaptureSnapshot();
        if (listModeBtn is not null)
            listModeBtn.Click += (_, _) => (DataContext as ExplainPlanViewModel)?.SetListMode();
        if (treeModeBtn is not null)
            treeModeBtn.Click += (_, _) => (DataContext as ExplainPlanViewModel)?.SetTreeMode();
        if (copyJsonBtn is not null)
            copyJsonBtn.Click += async (_, _) => await CopyJsonAsync();
        if (copyTextBtn is not null)
            copyTextBtn.Click += async (_, _) => await CopyTextAsync();
        if (saveJsonBtn is not null)
            saveJsonBtn.Click += async (_, _) => await SaveJsonAsync();
        if (openDaliboBtn is not null)
            openDaliboBtn.Click += (_, _) => OpenDalibo();

        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && DataContext is ExplainPlanViewModel vm)
        {
            vm.Close();
            e.Handled = true;
        }
    }

    private void OnStepRowClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Control control)
            return;
        if (control.DataContext is not ExplainStep step)
            return;
        if (DataContext is not ExplainPlanViewModel vm)
            return;

        vm.SelectStepCommand.Execute(step);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModelPropertyChangedHandler is not null && _currentVm is not null)
            _currentVm.PropertyChanged -= _viewModelPropertyChangedHandler;

        if (DataContext is not ExplainPlanViewModel vm)
        {
            _currentVm = null;
            RenderTree(null);
            return;
        }

        _currentVm = vm;
        _viewModelPropertyChangedHandler = (_, args) =>
        {
            if (args.PropertyName is nameof(ExplainPlanViewModel.TreeNodes)
                or nameof(ExplainPlanViewModel.TreeEdges)
                or nameof(ExplainPlanViewModel.ShowTreeView)
                or nameof(ExplainPlanViewModel.TreeCanvasWidth)
                or nameof(ExplainPlanViewModel.TreeCanvasHeight))
            {
                RenderTree(vm);
            }
        };
        vm.PropertyChanged += _viewModelPropertyChangedHandler;
        RenderTree(vm);
    }

    private void RenderTree(ExplainPlanViewModel? vm)
    {
        Canvas? treeCanvas = this.FindControl<Canvas>("TreeCanvas");
        if (treeCanvas is null)
            return;

        treeCanvas.Children.Clear();
        if (vm is null || !vm.ShowTreeView)
            return;

        foreach (ExplainTreeVisualEdge edge in vm.TreeEdges)
        {
            var line = new Line
            {
                StartPoint = new Avalonia.Point(edge.X1, edge.Y1),
                EndPoint = new Avalonia.Point(edge.X2, edge.Y2),
                Stroke = ResourceBrush("BorderBrush", UiColorConstants.C_334164),
                StrokeThickness = 1.4,
            };
            treeCanvas.Children.Add(line);
        }

        foreach (ExplainTreeVisualNode node in vm.TreeNodes)
        {
            var border = new Border
            {
                Width = node.Width,
                Height = node.Height,
                CornerRadius = ResourceCornerRadius("RadiusSM", 6),
                Background = ResourceBrush("Bg1Brush", UiColorConstants.C_0F1220),
                BorderBrush = node.HasAlert
                    ? ResourceBrush("StatusWarningBrush", UiColorConstants.C_D9A441)
                    : ResourceBrush("BorderBrush", UiColorConstants.C_334164),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = node.Operation,
                            FontSize = 11,
                            Foreground = ResourceBrush("TextPrimaryBrush", UiColorConstants.C_E7ECFF),
                        },
                        new TextBlock
                        {
                            Text = $"cost={node.CostText}",
                            FontSize = 10,
                            Foreground = ResourceBrush("TextSecondaryBrush", UiColorConstants.C_AEB9D9),
                        },
                        new TextBlock
                        {
                            Text = node.AlertLabel,
                            FontSize = 9,
                            Foreground = ResourceBrush("StatusWarningBrush", UiColorConstants.C_D9A441),
                            IsVisible = node.HasAlert,
                        },
                    },
                },
            };

            border.PointerPressed += (_, _) => vm.SelectStepCommand.Execute(node.Step);
            Canvas.SetLeft(border, node.X);
            Canvas.SetTop(border, node.Y);
            treeCanvas.Children.Add(border);
        }
    }

    private async void OnIndexSuggestionClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Control control)
            return;
        if (control.DataContext is not ExplainIndexSuggestion suggestion)
            return;
        if (DataContext is not ExplainPlanViewModel vm)
            return;

        vm.SelectIndexSuggestion(suggestion);

        Avalonia.Input.Platform.IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(suggestion.Sql);

        if (TopLevel.GetTopLevel(this)?.DataContext is ShellViewModel shell)
            shell.SetActiveDocumentType(WorkspaceDocumentType.DdlCanvas);
    }

    private async Task CopyJsonAsync()
    {
        if (DataContext is not ExplainPlanViewModel vm || string.IsNullOrWhiteSpace(vm.RawOutput))
            return;

        Avalonia.Input.Platform.IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(vm.RawOutput);
    }

    private async Task CopyTextAsync()
    {
        if (DataContext is not ExplainPlanViewModel vm)
            return;

        Avalonia.Input.Platform.IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(vm.BuildExportText());
    }

    private async Task SaveJsonAsync()
    {
        if (DataContext is not ExplainPlanViewModel vm || string.IsNullOrWhiteSpace(vm.RawOutput))
            return;

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return;

        var jsonType = new FilePickerFileType("JSON Files") { Patterns = ["*.json"] };
        IStorageFile? result = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Salvar plano EXPLAIN",
                DefaultExtension = "json",
                FileTypeChoices = [jsonType],
                SuggestedFileName = "explain-plan",
            }
        );

        string? path = result?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            await File.WriteAllTextAsync(path, vm.RawOutput);
    }

    private void OpenDalibo()
    {
        if (DataContext is not ExplainPlanViewModel vm)
            return;

        string? url = vm.BuildDaliboUrl();
        if (string.IsNullOrWhiteSpace(url))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }

    private static IBrush ResourceBrush(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out object? resource) == true && resource is IBrush brush)
            return brush;

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private static CornerRadius ResourceCornerRadius(string key, double fallbackValue)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out object? resource) == true && resource is CornerRadius radius)
            return radius;

        return new CornerRadius(fallbackValue);
    }
}
