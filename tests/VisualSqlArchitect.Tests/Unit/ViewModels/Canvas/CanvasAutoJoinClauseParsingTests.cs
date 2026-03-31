using System.Reflection;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class CanvasAutoJoinClauseParsingTests
{
    [Theory]
    [InlineData("orders.customer_id = customers.id", true)]
    [InlineData("orders.total >= customers.min_total", false)]
    [InlineData("orders.total <= customers.max_total", false)]
    [InlineData("orders.id != customers.id", false)]
    [InlineData("orders.id == customers.id", false)]
    [InlineData("orders.id = customers.id AND 1=1", false)]
    public void TrySplitJoinClauseOnEquality_ValidatesOperatorShape(string clause, bool expected)
    {
        MethodInfo method = typeof(CanvasViewModel)
            .GetMethod("TrySplitJoinClauseOnEquality", BindingFlags.NonPublic | BindingFlags.Static)!;

        object?[] args = [clause, null!, null!];
        bool success = (bool)method.Invoke(null, args)!;

        Assert.Equal(expected, success);
        if (expected)
        {
            Assert.Equal("orders.customer_id", Assert.IsType<string>(args[1]));
            Assert.Equal("customers.id", Assert.IsType<string>(args[2]));
        }
    }
}
