using System.IO;
using Xunit;

namespace DBWeaver.Tests.Unit.Controls;

public class NodeControlPinHoverTemplateRegressionTests
{
    [Fact]
    public void PinDotContainers_AreHitTestableAndShowHandCursor()
    {
        string xaml = ReadNodeControlXaml();

        Assert.Contains("Name=\"InputPinDot\"", xaml);
        Assert.Contains("Name=\"OutputPinDot\"", xaml);
        Assert.Contains("Name=\"OutputPinDotValueNode\"", xaml);
        Assert.Contains("Name=\"InputPinDotResultOutput\"", xaml);
        Assert.Contains("Name=\"InputPinDotColumnList\"", xaml);
        Assert.Contains("Name=\"OutputPinDotColumnList\"", xaml);

        Assert.Contains("Background=\"Transparent\"", xaml);
        Assert.Contains("Cursor=\"Hand\"", xaml);
        Assert.Contains("<ToolTip.Tip>", xaml);
    }

    private static string ReadNodeControlXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "DBWeaver.UI",
                "Controls",
                "Node",
                "NodeControl.axaml"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate NodeControl.axaml from test base directory.");
    }
}
