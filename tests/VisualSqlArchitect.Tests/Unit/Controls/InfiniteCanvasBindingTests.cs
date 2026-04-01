using VisualSqlArchitect.UI.Controls;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Controls;

public class InfiniteCanvasBindingTests
{
    [Fact]
    public void Constructor_CreatesExpectedVisualLayers()
    {
        var canvas = new InfiniteCanvas();

        Assert.Equal(3, canvas.Children.Count);
    }

    [Fact]
    public void InfiniteCanvas_HasNodeControlCacheFieldForFastLookup()
    {
        var cacheField = typeof(InfiniteCanvas).GetField(
            "_nodeControlCache",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        );

        Assert.NotNull(cacheField);
        Assert.Contains("Dictionary", cacheField!.FieldType.Name, StringComparison.OrdinalIgnoreCase);
    }
}
