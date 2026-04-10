using DBWeaver.Ddl;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Ddl;

public class DdlDiagnosticsPanelViewModelTests
{
    [Fact]
    public void ReplaceDiagnostics_PopulatesCounters_AndFocusesNode()
    {
        string? focusedNode = null;
        var vm = new DdlDiagnosticsPanelViewModel(nodeId => focusedNode = nodeId);

        vm.ReplaceDiagnostics(
        [
            new DdlCompileDiagnostic("E-1", DdlDiagnosticSeverity.Error, "Error msg", "node-1"),
            new DdlCompileDiagnostic("W-1", DdlDiagnosticSeverity.Warning, "Warn msg", "node-2"),
        ]);

        Assert.True(vm.HasItems);
        Assert.Equal(1, vm.ErrorCount);
        Assert.Equal(1, vm.WarningCount);

        DdlDiagnosticItemViewModel first = vm.Items[0];
        Assert.True(first.FocusCommand.CanExecute(null));
        first.FocusCommand.Execute(null);

        Assert.Equal("node-1", focusedNode);
    }
}
