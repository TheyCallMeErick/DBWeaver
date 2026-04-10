using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class CanvasAliasConventionIntegrationTests
{
    [Fact]
    public void AutoFixNaming_UsesConventionConfiguredInPropertyPanel()
    {
        var vm = new CanvasViewModel();
        vm.Nodes.Clear();
        vm.Connections.Clear();
        vm.UndoRedo.Clear();

        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Alias), new Point(0, 0))
        {
            Alias = "order_total",
        };
        vm.Nodes.Add(node);

        vm.PropertyPanel.SelectedNamingConvention = "camelCase";
        vm.PropertyPanel.EnforceAliasNaming = true;
        vm.AutoFixNaming();

        Assert.Equal("orderTotal", node.Alias);
    }

    [Fact]
    public void GraphValidator_WithCamelCasePolicy_DoesNotReportSnakeCaseViolation()
    {
        var vm = new CanvasViewModel();
        vm.Nodes.Clear();
        vm.Connections.Clear();

        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Alias), new Point(0, 0))
        {
            Alias = "orderTotal",
        };
        vm.Nodes.Add(node);

        vm.PropertyPanel.SelectedNamingConvention = "camelCase";
        vm.PropertyPanel.EnforceAliasNaming = true;

        var issues = GraphValidator.Validate(
            vm,
            vm.PropertyPanel.BuildNamingConventionPolicy(),
            vm.AliasConventions);

        Assert.DoesNotContain(issues, i => i.Code.StartsWith("NAMING_", StringComparison.Ordinal));
    }

    [Fact]
    public void GraphValidator_AfterSidebarConventionSwap_ReflectsNewConvention()
    {
        var vm = new CanvasViewModel();
        vm.Nodes.Clear();
        vm.Connections.Clear();

        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Alias), new Point(0, 0))
        {
            Alias = "order_total",
        };
        vm.Nodes.Add(node);

        vm.PropertyPanel.SelectedNamingConvention = "snake_case";
        var snakeIssues = GraphValidator.Validate(
            vm,
            vm.PropertyPanel.BuildNamingConventionPolicy(),
            vm.AliasConventions);
        Assert.DoesNotContain(snakeIssues, i => i.Code.StartsWith("NAMING_", StringComparison.Ordinal));

        vm.PropertyPanel.SelectedNamingConvention = "camelCase";
        var camelIssues = GraphValidator.Validate(
            vm,
            vm.PropertyPanel.BuildNamingConventionPolicy(),
            vm.AliasConventions);
        Assert.Contains(camelIssues, i => i.Code == "NAMING_CAMEL_CASE");
    }

}


