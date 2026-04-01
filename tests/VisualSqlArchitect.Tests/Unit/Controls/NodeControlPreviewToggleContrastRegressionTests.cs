using System.IO;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Controls;

public class NodeControlPreviewToggleContrastRegressionTests
{
    [Fact]
    public void PreviewToggle_UsesHighContrastStyles_AndActiveBinding()
    {
        string xaml = ReadNodeControlXaml();

        Assert.Contains("<Style Selector=\"Button.preview-toggle\">", xaml);
        Assert.Contains("<Setter Property=\"Background\"      Value=\"#0B1220\"/>", xaml);
        Assert.Contains("<Setter Property=\"BorderBrush\"     Value=\"#2B3A55\"/>", xaml);
        Assert.Contains("<Setter Property=\"Foreground\"      Value=\"#7DD3FC\"/>", xaml);

        Assert.Contains("<Style Selector=\"Button.preview-toggle.active\">", xaml);
        Assert.Contains("Classes.active=\"{Binding ShowInlinePreview}\"", xaml);
        Assert.Contains("Foreground=\"{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}\"", xaml);
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
