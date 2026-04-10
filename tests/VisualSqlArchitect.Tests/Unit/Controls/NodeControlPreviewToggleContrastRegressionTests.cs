using System.IO;
using Xunit;

namespace DBWeaver.Tests.Unit.Controls;

public class NodeControlPreviewToggleContrastRegressionTests
{
    [Fact]
    public void PreviewToggle_UsesHighContrastStyles_AndActiveBinding()
    {
        string xaml = ReadNodeControlXaml();

        Assert.Contains("<Style Selector=\"Button.preview-toggle\">", xaml);
        Assert.Contains("<Setter Property=\"Background\"      Value=\"{StaticResource Bg1Brush}\"/>", xaml);
        Assert.Contains("<Setter Property=\"BorderBrush\"     Value=\"{StaticResource BorderBrush}\"/>", xaml);
        Assert.Contains("<Setter Property=\"Foreground\"      Value=\"{StaticResource AccentPrimaryHoverBrush}\"/>", xaml);

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
