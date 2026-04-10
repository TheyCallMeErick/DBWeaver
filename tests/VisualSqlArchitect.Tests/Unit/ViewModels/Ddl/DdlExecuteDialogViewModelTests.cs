using DBWeaver.Core;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Ddl;

public class DdlExecuteDialogViewModelTests
{
    [Fact]
    public void ApplyResult_SetsSummaryAndDetails()
    {
        var vm = new DdlExecuteDialogViewModel("CREATE TABLE t(id INT);");

        var result = new DdlExecutionResult(
            Success: false,
            Statements:
            [
                new DdlStatementExecutionResult(1, "CREATE TABLE t(id INT)", true, RowsAffected: 0),
                new DdlStatementExecutionResult(2, "BAD", false, "syntax error"),
            ],
            ExecutionTime: TimeSpan.FromMilliseconds(42)
        );

        vm.ApplyResult(result);

        Assert.True(vm.HasResult);
        Assert.False(vm.IsSuccess);
        Assert.Contains("Statements: 2", vm.ResultSummary, StringComparison.Ordinal);
        Assert.Contains("FAIL", vm.ResultDetails, StringComparison.Ordinal);
        Assert.Contains("syntax error", vm.ResultDetails, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyError_SetsFailureState()
    {
        var vm = new DdlExecuteDialogViewModel("CREATE TABLE t(id INT);");

        vm.ApplyError(new InvalidOperationException("boom"));

        Assert.True(vm.HasResult);
        Assert.False(vm.IsSuccess);
        Assert.True(
            vm.ResultSummary.Contains("Falha", StringComparison.OrdinalIgnoreCase)
            || vm.ResultSummary.Contains("Failed", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Equal("boom", vm.ResultDetails);
    }

    [Fact]
    public void ApplyCancelled_SetsCancelledState()
    {
        var vm = new DdlExecuteDialogViewModel("CREATE TABLE t(id INT);");

        vm.ApplyCancelled();

        Assert.True(vm.HasResult);
        Assert.False(vm.IsSuccess);
        Assert.True(
            vm.ResultSummary.Contains("cancelada", StringComparison.OrdinalIgnoreCase)
            || vm.ResultSummary.Contains("cancelled", StringComparison.OrdinalIgnoreCase)
        );
        Assert.True(
            vm.ResultDetails.Contains("interrompida", StringComparison.OrdinalIgnoreCase)
            || vm.ResultDetails.Contains("interrupted", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Constructor_WithDropTable_DetectsDestructiveStatements()
    {
        var vm = new DdlExecuteDialogViewModel("DROP TABLE IF EXISTS users; CREATE TABLE users(id INT);");

        Assert.True(vm.HasDestructiveStatements);
        Assert.Contains(vm.StatementPreviews, s => s.IsDestructive);
        Assert.False(vm.CanExecute);
    }

    [Fact]
    public void CanExecute_RequiresConfirmationWhenDestructive()
    {
        var vm = new DdlExecuteDialogViewModel("DROP TABLE users;");

        Assert.False(vm.CanExecute);

        vm.ConfirmDestructiveExecution = true;

        Assert.True(vm.CanExecute);

        vm.IsExecuting = true;
        Assert.False(vm.CanExecute);
    }
}
