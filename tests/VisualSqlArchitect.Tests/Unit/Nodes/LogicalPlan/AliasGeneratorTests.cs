using DBWeaver.Nodes.LogicalPlan;

namespace DBWeaver.Tests.Unit.Nodes.LogicalPlan;

public class AliasGeneratorTests
{
    [Fact]
    public void GenerateFor_WhenSuggestionRepeats_ProducesUniqueAliases()
    {
        var sut = new AliasGenerator();

        string first = sut.GenerateFor("orders");
        string second = sut.GenerateFor("orders");
        string third = sut.GenerateFor("orders");

        Assert.Equal("orders", first);
        Assert.Equal("orders_1", second);
        Assert.Equal("orders_2", third);
    }

    [Fact]
    public void GenerateFor_WhenSuggestionIsBlank_UsesDatasetPrefix()
    {
        var sut = new AliasGenerator();

        string alias = sut.GenerateFor(" ");

        Assert.Equal("ds", alias);
    }
}
