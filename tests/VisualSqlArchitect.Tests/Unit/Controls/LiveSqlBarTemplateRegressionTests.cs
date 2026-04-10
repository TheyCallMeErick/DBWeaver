using System.IO;

namespace DBWeaver.Tests.Unit.Controls;

public class LiveSqlBarTemplateRegressionTests
{
    [Fact]
    public void ActionCtas_AreProminentAndPlacedInBottomSection()
    {
        string xaml = ReadLiveSqlBarXaml();

        Assert.Contains("Grid RowDefinitions=\"Auto,Auto,*,Auto,Auto,Auto\"", xaml);
        Assert.Contains("<Border Grid.Row=\"5\"", xaml);
        Assert.Contains("Name=\"ExplainBtn\"", xaml);
        Assert.Contains("Name=\"BenchmarkBtn\"", xaml);
        Assert.Contains("Classes=\"sql-cta explain\"", xaml);
        Assert.Contains("Classes=\"sql-cta benchmark\"", xaml);
    }

    [Fact]
    public void ActionCtas_KeepExplainAndBenchmarkBindingsAndHints()
    {
        string xaml = ReadLiveSqlBarXaml();

        Assert.Contains("Text=\"{Binding [liveSql.actionsHint]", xaml);
        Assert.Contains("Text=\"{Binding [liveSql.explain], Source={x:Static loc:LocalizationService.Instance}}\"", xaml);
        Assert.Contains("Text=\"{Binding [liveSql.benchmark], Source={x:Static loc:LocalizationService.Instance}}\"", xaml);
        Assert.Contains("Text=\"F4\"", xaml);
    }

    private static string ReadLiveSqlBarXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "DBWeaver.UI",
                "Controls",
                "LiveSqlBar",
                "LiveSqlBar.axaml"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate LiveSqlBar.axaml from test base directory.");
    }
}
