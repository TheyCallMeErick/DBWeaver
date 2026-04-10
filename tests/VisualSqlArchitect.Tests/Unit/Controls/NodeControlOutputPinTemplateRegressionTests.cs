using System.IO;
using Xunit;

namespace DBWeaver.Tests.Unit.Controls;

public class NodeControlOutputPinTemplateRegressionTests
{
    [Fact]
    public void OutputPinTemplates_KeepTypedLayerAndFallbackRing()
    {
        string repoRoot = FindRepoRoot();
        string xamlPath = Path.Combine(repoRoot, "src", "DBWeaver.UI", "Controls", "Node", "NodeControl.axaml");

        Assert.True(File.Exists(xamlPath));

        string xaml = File.ReadAllText(xamlPath);

        Assert.Contains("Name=\"OutputPinDot\"", xaml);
        Assert.Contains("Name=\"OutputPinDotValueNode\"", xaml);
        Assert.Contains("Name=\"OutputPinDotColumnList\"", xaml);

        Assert.Contains("Safety ring: unobtrusive fallback", xaml);
        Assert.Contains("Typed layer: primary source of pin shape/color semantics", xaml);
        Assert.Contains("<ctrl:PinShapeControl", xaml);
        Assert.Contains("Scale=\"1\"", xaml);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "files.sln")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }
}
