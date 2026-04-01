using System.IO;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Controls;

public class NodeControlInlinePreviewClipRegressionTests
{
    [Fact]
    public void NodeShell_AndInlinePreview_UseRoundedClipToAvoidSquareCorners()
    {
        string xaml = ReadNodeControlXaml();

        Assert.Contains("CornerRadius=\"24\"", xaml);
        Assert.Contains("ClipToBounds=\"True\"", xaml);
        Assert.Contains("<Border Grid.Row=\"4\"", xaml);
        Assert.Contains("CornerRadius=\"0,0,23,23\"", xaml);
    }

    private static string ReadNodeControlXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "VisualSqlArchitect.UI",
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
