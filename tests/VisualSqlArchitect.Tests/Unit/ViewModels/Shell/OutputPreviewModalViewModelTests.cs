using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas.Strategies;

namespace DBWeaver.Tests.Unit.ViewModels.Shell;

public class OutputPreviewModalViewModelTests
{
    [Fact]
    public void OpenForDdl_WithEmptyCanvas_ShowsModalWithoutInlineErrors()
    {
        var canvas = new CanvasViewModel(
            nodeManager: null,
            pinManager: null,
            selectionManager: null,
            localizationService: null,
            domainStrategy: new DdlDomainStrategy());
        LiveDdlBarViewModel liveDdl = Assert.IsType<LiveDdlBarViewModel>(canvas.LiveDdl);
        liveDdl.Recompile();

        var vm = new OutputPreviewModalViewModel();
        vm.OpenForDdl(canvas, liveDdl, "PostgreSQL");

        Assert.True(vm.IsVisible);
        Assert.True(vm.IsDdlMode);
        Assert.False(vm.HasDdlSql);
        Assert.True(vm.HasDiagnostics);
    }

    [Fact]
    public void OpenForDdl_WithInvalidGraph_DoesNotExposeInlineCompilerErrors()
    {
        var canvas = new CanvasViewModel(
            nodeManager: null,
            pinManager: null,
            selectionManager: null,
            localizationService: null,
            domainStrategy: new DdlDomainStrategy());

        canvas.SpawnNode(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(40, 40));
        LiveDdlBarViewModel liveDdl = Assert.IsType<LiveDdlBarViewModel>(canvas.LiveDdl);
        liveDdl.Recompile();

        var vm = new OutputPreviewModalViewModel();
        vm.OpenForDdl(canvas, liveDdl, "PostgreSQL");

        Assert.True(vm.IsVisible);
        Assert.True(vm.IsDdlMode);
        Assert.False(vm.HasDdlSql);
        Assert.True(vm.HasDiagnostics);
    }

    [Fact]
    public void OpenUnavailable_UsesExplicitUnavailableSurfaceWithoutDiagnostics()
    {
        var vm = new OutputPreviewModalViewModel();

        vm.OpenUnavailable("Preview", "Preview", "Preview indisponível para este documento.");

        Assert.True(vm.IsVisible);
        Assert.True(vm.IsUnavailableMode);
        Assert.True(vm.ShowUnavailablePrimaryContent);
        Assert.False(vm.HasDiagnostics);
        Assert.Equal("Preview indisponível para este documento.", vm.UnavailableMessage);
    }
}
