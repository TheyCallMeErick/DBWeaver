using System.IO;
using Xunit;

namespace DBWeaver.Tests.Unit.Controls;

public class SchemaControlTemplateRegressionTests
{
    [Fact]
    public void SchemaTemplate_DefinesInteractiveCardStates_AndFilterEmptyState()
    {
        string xaml = ReadSchemaXaml();

        Assert.Contains("Style Selector=\"Border.schema-header-card:pointerover\"", xaml);
        Assert.Contains("Style Selector=\"Border.schema-object-card:pointerover\"", xaml);
        Assert.Contains("Style Selector=\"Border.schema-object-card.expanded\"", xaml);
        Assert.Contains("IsVisible=\"{Binding ShowFilterEmptyState}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding ShowNoTablesState}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding ShowLoadingState}\"", xaml);
        Assert.Contains("Text=\"{Binding [schema.emptyFiltered], Source={x:Static loc:LocalizationService.Instance}}\"", xaml);
        Assert.Contains("Text=\"{Binding [schema.emptyNoTables], Source={x:Static loc:LocalizationService.Instance}}\"", xaml);
        Assert.Contains("Text=\"{Binding [schema.loading], Source={x:Static loc:LocalizationService.Instance}}\"", xaml);
        Assert.DoesNotContain("schema.noConnectionHint", xaml);
    }

    [Fact]
    public void SchemaTemplate_UsesSuccessClassForAddNodeAction()
    {
        string xaml = ReadSchemaXaml();

        Assert.Contains("Command=\"{Binding AddNodeCommand}\"", xaml);
        Assert.Contains("Classes=\"success\"", xaml);
    }

    private static string ReadSchemaXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "DBWeaver.UI",
                "Controls",
                "SidebarLeft",
                "SchemaControl.axaml"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate SchemaControl.axaml from test base directory.");
    }
}
