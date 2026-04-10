using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.PropertyPanel;

public class PropertyPanelNamingConventionSettingsTests
{
    [Fact]
    public void BuildNamingConventionPolicy_WhenEnforced_UsesSelectedConvention()
    {
        var panel = new PropertyPanelViewModel(new UndoRedoStack(new CanvasViewModel()));
        panel.EnforceAliasNaming = true;
        panel.SelectedNamingConvention = "SCREAMING_SNAKE_CASE";
        panel.MaxAliasLength = "32";

        NamingConventionPolicy policy = panel.BuildNamingConventionPolicy();

        Assert.Equal("SCREAMING_SNAKE_CASE", policy.ConventionName);
        Assert.Equal(32, policy.MaxLength);
        Assert.False(policy.EnforceSnakeCase);
    }

    [Fact]
    public void BuildNamingConventionPolicy_WhenDisabled_ClearsConventionName()
    {
        var panel = new PropertyPanelViewModel(new UndoRedoStack(new CanvasViewModel()));
        panel.EnforceAliasNaming = false;
        panel.SelectedNamingConvention = "camelCase";

        NamingConventionPolicy policy = panel.BuildNamingConventionPolicy();

        Assert.Null(policy.ConventionName);
        Assert.False(policy.EnforceSnakeCase);
    }

    [Fact]
    public void NamingSettingsChanged_RaisedWhenSettingsMutate()
    {
        var panel = new PropertyPanelViewModel(new UndoRedoStack(new CanvasViewModel()));
        int raised = 0;
        panel.NamingSettingsChanged += () => raised++;

        panel.SelectedNamingConvention = "camelCase";
        panel.EnforceAliasNaming = !panel.EnforceAliasNaming;
        panel.MaxAliasLength = "42";

        Assert.True(raised >= 3);
    }
}

