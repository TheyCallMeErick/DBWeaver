using System.Reflection;
using VisualSqlArchitect.UI.Controls;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Controls;

public class DotGridBackgroundDensityTests
{
    [Fact]
    public void ComputeDotStride_ReturnsOne_ForNormalDensity()
    {
        var method = typeof(DotGridBackground).GetMethod(
            "ComputeDotStride",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        int stride = (int)method!.Invoke(null, [1920d, 1080d, 28d, 14000])!;
        Assert.Equal(1, stride);
    }

    [Fact]
    public void ComputeDotStride_Increases_ForVeryDenseGrid()
    {
        var method = typeof(DotGridBackground).GetMethod(
            "ComputeDotStride",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        int stride = (int)method!.Invoke(null, [1920d, 1080d, 6d, 14000])!;
        Assert.True(stride > 1);
    }
}
