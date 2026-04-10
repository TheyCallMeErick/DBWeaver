using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using System.ComponentModel;
using DBWeaver.UI.ViewModels;
using ESidebarTab = DBWeaver.UI.ViewModels.SidebarTab;

namespace DBWeaver.UI.Controls;

public partial class SidebarControl : UserControl
{
    private bool _buttonsWired = false;
    private SidebarViewModel? _subscribedVm;
    private bool _isAnimatingTab;

    public SidebarControl()
    {
        InitializeComponent();

        // Wire up button click handlers when loaded
        this.Loaded += (_, _) => WireUpButtons();
    }

    private void WireUpButtons()
    {
        if (_buttonsWired || DataContext is not SidebarViewModel vm)
            return;

        _buttonsWired = true;

        var nodesButton = this.FindControl<Button>("NodesTabButton");
        var connectionButton = this.FindControl<Button>("ConnectionTabButton");
        var schemaButton = this.FindControl<Button>("SchemaTabButton");

        if (nodesButton is not null)
        {
            nodesButton.Click += (_, _) => vm.ActiveTab = ESidebarTab.Nodes;
        }
        if (connectionButton is not null)
        {
            connectionButton.Click += (_, _) => vm.ActiveTab = ESidebarTab.Connection;
        }
        if (schemaButton is not null)
        {
            schemaButton.Click += (_, _) => vm.ActiveTab = ESidebarTab.Schema;
        }
        // Set child control DataContexts
        var nodesControl = this.FindControl<NodesListControl>("NodesControl");
        var connectionControl = this.FindControl<ConnectionTabControl>("ConnectionControl");
        var schemaControl = this.FindControl<SchemaControl>("SchemaControl");

        if (nodesControl is not null)
            nodesControl.DataContext = vm.NodesList;
        if (connectionControl is not null)
            connectionControl.DataContext = vm.ConnectionManager;
        if (connectionControl is not null)
            connectionControl.DataContext = vm.EffectiveConnectionManager;
        if (schemaControl is not null)
            schemaControl.DataContext = vm.Schema;

        AttachVmSubscriptions(vm);
        _ = AnimateActiveTabAsync(vm.ActiveTab);
    }

    private void AttachVmSubscriptions(SidebarViewModel vm)
    {
        if (ReferenceEquals(_subscribedVm, vm))
            return;

        if (_subscribedVm is not null)
        {
            _subscribedVm.PropertyChanged -= OnSidebarPropertyChanged;
            _subscribedVm.AddNodeRequested -= OnAddNodeRequested;
        }

        _subscribedVm = vm;
        _subscribedVm.PropertyChanged += OnSidebarPropertyChanged;
        _subscribedVm.AddNodeRequested += OnAddNodeRequested;
    }

    private void OnSidebarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_subscribedVm is null)
            return;

        if (e.PropertyName == nameof(SidebarViewModel.EffectiveConnectionManager))
        {
            ConnectionTabControl? connectionControl = this.FindControl<ConnectionTabControl>("ConnectionControl");
            if (connectionControl is not null)
                connectionControl.DataContext = _subscribedVm.EffectiveConnectionManager;

            SchemaControl? schemaControl = this.FindControl<SchemaControl>("SchemaControl");
            if (schemaControl is not null)
                schemaControl.DataContext = _subscribedVm.Schema;
        }

        if (e.PropertyName != nameof(SidebarViewModel.ActiveTab))
            return;

        _ = AnimateActiveTabAsync(_subscribedVm.ActiveTab);
    }

    private void OnAddNodeRequested()
    {
        Dispatcher.UIThread.Post(() =>
        {
            TextBox? search = this.FindControl<TextBox>("NodesSearchBox");
            search?.Focus();
        }, DispatcherPriority.Background);
    }

    private async Task AnimateActiveTabAsync(SidebarTab tab)
    {
        if (_isAnimatingTab)
            return;

        Control? target = tab switch
        {
            SidebarTab.Nodes => this.FindControl<Control>("NodesControl"),
            SidebarTab.Connection => this.FindControl<Control>("ConnectionControl"),
            SidebarTab.Schema => this.FindControl<Control>("SchemaControl"),
            _ => null,
        };

        if (target is null)
            return;

        _isAnimatingTab = true;
        try
        {
            target.Opacity = 0;
            const int steps = 5;
            for (int i = 1; i <= steps; i++)
            {
                target.Opacity = i / (double)steps;
                await Task.Delay(18);
            }
        }
        finally
        {
            _isAnimatingTab = false;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        // Reset and re-wire if data context changes
        if (this.IsLoaded)
        {
            _buttonsWired = false;
            WireUpButtons();
        }
    }
}
