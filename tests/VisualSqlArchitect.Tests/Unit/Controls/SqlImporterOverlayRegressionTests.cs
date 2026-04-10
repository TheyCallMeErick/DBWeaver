namespace DBWeaver.Tests.Unit.Controls;

public class SqlImporterOverlayRegressionTests
{
    [Fact]
    public void Overlay_ResultPanel_ContainsExplicitCloseButton()
    {
        string source = File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "src", "DBWeaver.UI", "Controls", "SqlImporter", "SqlImporterOverlay.axaml"));

        Assert.Contains("ResultCloseBtn", source, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding [common.close], Source={x:Static loc:LocalizationService.Instance}}\"", source, StringComparison.Ordinal);
    }
}
