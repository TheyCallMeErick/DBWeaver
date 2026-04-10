using System.IO;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class MutationConfirmDialogTemplateRegressionTests
{
    [Fact]
    public void MutationConfirmDialogTemplate_BindsMutationConfirmationAndCommands()
    {
        string xaml = ReadXaml();

        Assert.Contains("x:DataType=\"vm:SqlEditorViewModel\"", xaml);
        Assert.Contains("Text=\"{Binding PendingMutationMessage}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding PendingMutationIssues}\"", xaml);
        Assert.Contains("x:DataType=\"vm:MutationGuardIssue\"", xaml);
        Assert.Contains("Text=\"{Binding PendingMutationCountQuery}\"", xaml);
        Assert.Contains("Text=\"{Binding PendingMutationEstimateText}\"", xaml);
        Assert.Contains("<ctrl:DiffPreviewControl", xaml);
        Assert.Contains("sqlEditor.mutation.confirmExecute", xaml);
        Assert.Contains("common.cancel", xaml);
        Assert.Contains("Command=\"{Binding ConfirmPendingMutationCommand}\"", xaml);
        Assert.Contains("Command=\"{Binding CancelPendingMutationCommand}\"", xaml);
    }

    private static string ReadXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "DBWeaver.UI",
                "Controls",
                "SqlEditor",
                "MutationConfirmDialog.axaml");

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate MutationConfirmDialog.axaml from test base directory.");
    }
}
