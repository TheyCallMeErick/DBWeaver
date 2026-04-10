using System.IO;
using Xunit;

namespace DBWeaver.Tests.Unit.Controls;

public class DataPreviewPanelClipRegressionTests
{
    [Fact]
    public void Row2Content_UsesRoundedCornerClip_ForPreviewAndLiveSql()
    {
        string xaml = ReadDataPreviewPanelXaml();

        Assert.Contains("<Border Grid.Row=\"2\"", xaml);
        Assert.Contains("IsVisible=\"{Binding ShowDataPreview}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding ShowLiveSql}\"", xaml);
        Assert.Contains("CornerRadius=\"{StaticResource RadiusLGBottom}\"", xaml);
        Assert.Contains("ClipToBounds=\"True\"", xaml);
    }

    private static string ReadDataPreviewPanelXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "DBWeaver.UI",
                "Views",
                "Panels",
                "DataPreviewPanel.axaml"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate DataPreviewPanel.axaml from test base directory.");
    }
}
