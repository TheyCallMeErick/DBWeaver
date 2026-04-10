using DBWeaver.UI.Services.Localization;
using Xunit;

namespace DBWeaver.Tests.Unit.Services;

public class LocalizationServiceTests
{
    [Fact]
    public void SetCulture_SwitchesBetweenPtBrAndEnUs()
    {
        var loc = LocalizationService.Instance;

        Assert.True(loc.SetCulture("pt-BR"));
        string ptLabel = loc["connection.connect"];

        Assert.True(loc.SetCulture("en-US"));
        string enLabel = loc["connection.connect"];

        Assert.NotEqual(ptLabel, enLabel);
        Assert.Equal("Connect", enLabel);

        // restore default
        loc.SetCulture("pt-BR");
    }

    [Fact]
    public void ToggleCulture_UpdatesLanguageLabel()
    {
        var loc = LocalizationService.Instance;

        loc.SetCulture("pt-BR");
        string before = loc.CurrentLanguageLabel;

        bool changed = loc.ToggleCulture();

        Assert.True(changed);
        Assert.NotEqual(before, loc.CurrentLanguageLabel);

        // restore default
        loc.SetCulture("pt-BR");
    }

    [Fact]
    public void SetCulture_SupportsExtendedLocales()
    {
        var loc = LocalizationService.Instance;

        Assert.True(loc.SetCulture("es-ES"));
        Assert.Equal("es-ES", loc.CurrentCulture);

        Assert.True(loc.SetCulture("ru-RU"));
        Assert.Equal("ru-RU", loc.CurrentCulture);

        Assert.True(loc.SetCulture("ja-JP"));
        Assert.Equal("ja-JP", loc.CurrentCulture);

        Assert.True(loc.SetCulture("zh-TW"));
        Assert.Equal("zh-TW", loc.CurrentCulture);

        // restore default
        loc.SetCulture("pt-BR");
    }
}
