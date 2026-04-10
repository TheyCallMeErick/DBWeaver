using DBWeaver.UI.Services.Benchmark;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class ShellViewModelDdlModeTests
{
    [Fact]
    public void Shell_StartsInQueryMode_WithoutDdlCanvas()
    {
        var vm = new ShellViewModel(connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());

        Assert.Equal(ShellViewModel.AppMode.Query, vm.ActiveMode);
        Assert.True(vm.IsQueryModeActive);
        Assert.False(vm.IsDdlModeActive);
        Assert.Null(vm.DdlCanvas);
    }

    [Fact]
    public void SetActiveMode_Ddl_InitializesStubCanvas()
    {
        var vm = new ShellViewModel(connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());

        vm.SetActiveMode(ShellViewModel.AppMode.Ddl);

        Assert.Equal(ShellViewModel.AppMode.Ddl, vm.ActiveMode);
        Assert.False(vm.IsQueryModeActive);
        Assert.True(vm.IsDdlModeActive);
        Assert.NotNull(vm.DdlCanvas);
        Assert.True(vm.DdlCanvas!.IsEmpty);
    }

    [Fact]
    public void SetActiveMode_InvalidValue_ThrowsExpectedError()
    {
        var vm = new ShellViewModel(connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());

        Assert.Throws<ArgumentOutOfRangeException>(() => vm.SetActiveMode((ShellViewModel.AppMode)999));
    }

    [Fact]
    public void EnsureDdlCanvas_ReturnsSameInstance_OnSubsequentCalls()
    {
        var vm = new ShellViewModel(connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());

        var first = vm.EnsureDdlCanvas();
        var second = vm.EnsureDdlCanvas();

        Assert.Same(first, second);
    }

    [Fact]
    public void ModeCommands_SwitchBetweenQueryAndDdl()
    {
        var vm = new ShellViewModel(connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());

        vm.DdlModeCommand.Execute(null);
        Assert.Equal(ShellViewModel.AppMode.Ddl, vm.ActiveMode);

        vm.QueryModeCommand.Execute(null);
        Assert.Equal(ShellViewModel.AppMode.Query, vm.ActiveMode);
    }

    [Fact]
    public void SettingsSections_AllExpectedTitlesAndSubtitles_AreMapped()
    {
        var vm = new ShellViewModel(connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());

        vm.SelectSettingsSection(ShellViewModel.ESettingsSection.DateTime);
        string dateTimeTitle = vm.SettingsSectionTitle;
        Assert.False(string.IsNullOrWhiteSpace(dateTimeTitle));

        vm.SelectSettingsSection(ShellViewModel.ESettingsSection.KeyboardShortcuts);
        string keyboardTitle = vm.SettingsSectionTitle;
        string keyboardSubtitle = vm.SettingsSectionSubtitle;
        Assert.False(string.IsNullOrWhiteSpace(keyboardTitle));
        Assert.False(string.IsNullOrWhiteSpace(keyboardSubtitle));
        Assert.NotEqual(dateTimeTitle, keyboardTitle);

        vm.SelectSettingsSection(ShellViewModel.ESettingsSection.Notification);
        string notificationTitle = vm.SettingsSectionTitle;
        string notificationSubtitle = vm.SettingsSectionSubtitle;
        Assert.False(string.IsNullOrWhiteSpace(notificationTitle));
        Assert.NotEqual(keyboardTitle, notificationTitle);
        Assert.NotEqual(keyboardSubtitle, notificationSubtitle);

        vm.SelectSettingsSection(ShellViewModel.ESettingsSection.Accessibility);
        Assert.False(string.IsNullOrWhiteSpace(vm.SettingsSectionTitle));
        Assert.NotEqual(notificationTitle, vm.SettingsSectionTitle);
        Assert.False(string.IsNullOrWhiteSpace(vm.SettingsSectionSubtitle));
    }

    [Fact]
    public void AppVersionLabel_IsResolvedToNonEmptyValue()
    {
        var vm = new ShellViewModel(connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());

        Assert.False(string.IsNullOrWhiteSpace(vm.AppVersionLabel));
    }

    [Fact]
    public void ActiveCanvasContext_DerivesFromModeAndSubcanvasState()
    {
        var vm = new ShellViewModel(connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());

        Assert.Equal(CanvasContext.Query, vm.ActiveCanvasContext);

        vm.SetActiveMode(ShellViewModel.AppMode.Ddl);
        Assert.Equal(CanvasContext.Ddl, vm.ActiveCanvasContext);

        vm.SetViewSubcanvasActive(true);
        Assert.Equal(CanvasContext.ViewSubcanvas, vm.ActiveCanvasContext);

        vm.SetViewSubcanvasActive(false);
        Assert.Equal(CanvasContext.Ddl, vm.ActiveCanvasContext);

        vm.SetActiveMode(ShellViewModel.AppMode.Query);
        Assert.Equal(CanvasContext.Query, vm.ActiveCanvasContext);
    }

    [Fact]
    public void ModeSwitch_UsesIndependentActiveCanvasInstances_WithoutLeakingNodesAcrossModes()
    {
        var vm = new ShellViewModel(connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());

        CanvasViewModel queryCanvas = vm.EnsureCanvas();
        queryCanvas.SpawnTableNode("public.orders", [("id", PinDataType.Number)], new Point(10, 10));
        Assert.Same(queryCanvas, vm.ActiveCanvas);
        Assert.Single(queryCanvas.Nodes);

        vm.SetActiveMode(ShellViewModel.AppMode.Ddl);
        CanvasViewModel ddlCanvas = Assert.IsType<CanvasViewModel>(vm.ActiveCanvas);
        Assert.Same(vm.DdlCanvas, ddlCanvas);
        Assert.Empty(ddlCanvas.Nodes);

        ddlCanvas.SpawnNode(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(40, 20));
        Assert.Single(ddlCanvas.Nodes);
        Assert.Single(queryCanvas.Nodes);

        vm.SetActiveMode(ShellViewModel.AppMode.Query);
        Assert.Same(queryCanvas, vm.ActiveCanvas);
        Assert.Single(queryCanvas.Nodes);
        Assert.Single(ddlCanvas.Nodes);
    }

    [Fact]
    public void EnsureCanvasAndDdlCanvas_UseSharedShellToastCenter()
    {
        var vm = new ShellViewModel(connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());

        CanvasViewModel queryCanvas = vm.EnsureCanvas();
        CanvasViewModel ddlCanvas = vm.EnsureDdlCanvas();

        Assert.Same(vm.Toasts, queryCanvas.Toasts);
        Assert.Same(vm.Toasts, ddlCanvas.Toasts);
    }
}
