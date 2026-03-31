using VisualSqlArchitect.UI.Services.Localization;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Services;

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
}
