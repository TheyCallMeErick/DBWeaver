using System.IO;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class DiffPreviewControlTemplateRegressionTests
{
    [Fact]
    public void DiffPreviewControlTemplate_BindsDiffMessage()
    {
        string xaml = ReadXaml();

        Assert.Contains("x:DataType=\"vm:SqlEditorViewModel\"", xaml);
        Assert.Contains("sqlEditor.diffPreview.title", xaml);
        Assert.Contains("Text=\"{Binding PendingMutationDiffText}\"", xaml);
    }

    private static string ReadXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "DBWeaver.UI",
                "Controls",
                "SqlEditor",
                "DiffPreviewControl.axaml");

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate DiffPreviewControl.axaml from test base directory.");
    }
}
