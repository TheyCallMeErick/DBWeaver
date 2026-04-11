using System.IO;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class SqlEditorLeftSidebarControlTemplateRegressionTests
{
    [Fact]
    public void SqlEditorLeftSidebarTemplate_BindsConnectionAndSchema()
    {
        string xaml = ReadXaml();

        Assert.Contains("x:DataType=\"vm:SqlEditorViewModel\"", xaml);
        Assert.Contains("Text=\"{Binding [sidebar.tab.connection], Source={x:Static loc:LocalizationService.Instance}}\"", xaml);
        Assert.Contains("shared:DatabaseConnectionCard", xaml);
        Assert.Contains("SwitchConnectionCommand=\"{Binding SharedConnectionManager.SwitchConnectionCommand}\"", xaml);
        Assert.Contains("SwitchSchemaCommand=\"{Binding SharedConnectionManager.SwitchSchemaCommand}\"", xaml);
        Assert.DoesNotContain("ShowDialectSelector", xaml);
        Assert.DoesNotContain("FallbackDialect", xaml);
        Assert.Contains("Text=\"{Binding [sidebar.tab.schema], Source={x:Static loc:LocalizationService.Instance}}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding SchemaTables}\"", xaml);
        Assert.Contains("x:DataType=\"vm:SqlEditorSchemaTableItem\"", xaml);
        Assert.Contains("x:DataType=\"vm:SqlEditorSchemaColumnItem\"", xaml);
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
                "SqlEditorLeftSidebarControl.axaml");

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate SqlEditorLeftSidebarControl.axaml from test base directory.");
    }
}
