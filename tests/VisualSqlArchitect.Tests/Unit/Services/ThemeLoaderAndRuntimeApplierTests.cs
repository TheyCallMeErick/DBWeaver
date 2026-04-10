using DBWeaver.UI.Services.Theming;
using Xunit;

namespace DBWeaver.Tests.Unit.Services;

public class ThemeLoaderAndRuntimeApplierTests
{
    [Fact]
    public void LoadFromPath_MissingFile_ReturnsNotFound()
    {
        ThemeLoadResult result = ThemeLoader.LoadFromPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));

        Assert.Equal(ThemeLoadStatus.NotFound, result.Status);
    }

    [Fact]
    public void LoadFromPath_ValidJson_LoadsConfig()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "{\"colors\":{\"bg0\":\"#0B0E14\"}}");

            ThemeLoadResult result = ThemeLoader.LoadFromPath(file);

            Assert.Equal(ThemeLoadStatus.Loaded, result.Status);
            Assert.NotNull(result.Config);
            Assert.Equal("#0B0E14", result.Config!.Colors!.Bg0);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void RuntimeApplier_AppliesOverridesToResourceDictionary()
    {
        var resources = new Dictionary<object, object?>
        {
            ["FontSizeBody"] = 12d
        };

        var overrides = new Dictionary<string, object>
        {
            ["FontSizeBody"] = 13d,
            ["TextPrimary"] = "#FFFFFF"
        };

        int applied = ThemeRuntimeApplier.Apply(resources, overrides);

        Assert.Equal(2, applied);
        Assert.Equal(13d, resources["FontSizeBody"]);
        Assert.Equal("#FFFFFF", resources["TextPrimary"]);
    }
}
