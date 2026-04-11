using System.IO;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class SqlEditorResultPanelTemplateRegressionTests
{
    [Fact]
    public void SqlEditorResultPanelTemplate_BindsResultsGridAndEmptyState()
    {
        string xaml = ReadXaml();

        Assert.Contains("x:DataType=\"vm:SqlEditorViewModel\"", xaml);
        Assert.Contains("xmlns:loc=\"using:DBWeaver.UI.Services.Localization\"", xaml);
        Assert.Contains("Text=\"{Binding [sqlEditor.results.title], Source={x:Static loc:LocalizationService.Instance}}\"", xaml);
        Assert.Contains("Text=\"{Binding ResultsEmptyText}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding IsResultRowsEmpty}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding HasResultRows}\"", xaml);
        Assert.Contains("AutoGenerateColumns=\"False\"", xaml);
        Assert.Contains("x:Name=\"ResultGrid\"", xaml);
    }

    [Fact]
    public void SqlEditorResultPanelCodeBehind_UsesTypeHeaderAndExpandCellFlow()
    {
        string source = ReadCodeBehind();

        Assert.Contains("SortMemberPath = columnName", source);
        Assert.Contains("GetColumnTypeLabel", source);
        Assert.Contains("expandCell", source);
        Assert.Contains("DoubleTapped", source);
        Assert.Contains("SqlEditorCellExpandDialogWindow", source);
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
                "SqlEditorResultPanel.axaml");

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate SqlEditorResultPanel.axaml from test base directory.");
    }

    private static string ReadCodeBehind()
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
                "SqlEditorResultPanel.axaml.cs");

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate SqlEditorResultPanel.axaml.cs from test base directory.");
    }
}
