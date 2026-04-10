using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.Core;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;
using System.Reflection;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainPlanSimulationBadgeTests
{
    [Fact]
    public void IsSimulated_IsTrue_WhenCanvasHasNoActiveConnection()
    {
        var canvas = new CanvasViewModel();
        canvas.ActiveConnectionConfig = null;

        var sut = new ExplainPlanViewModel(canvas);

        Assert.True(sut.IsSimulated);
    }

    [Fact]
    public void IsSimulated_UpdatesToFalse_WhenConnectionIsSet()
    {
        var canvas = new CanvasViewModel();
        var sut = new ExplainPlanViewModel(canvas);

        canvas.ActiveConnectionConfig = new ConnectionConfig(
            Provider: DatabaseProvider.SQLite,
            Host: string.Empty,
            Port: 0,
            Database: "local.db",
            Username: string.Empty,
            Password: string.Empty
        );

        Assert.False(sut.IsSimulated);
    }

    [Fact]
    public void IsSimulated_RaisesPropertyChanged_WhenConnectionChanges()
    {
        var canvas = new CanvasViewModel();
        var sut = new ExplainPlanViewModel(canvas);
        var changedProps = new List<string>();

        sut.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.PropertyName))
                changedProps.Add(e.PropertyName!);
        };

        canvas.ActiveConnectionConfig = new ConnectionConfig(
            Provider: DatabaseProvider.SQLite,
            Host: string.Empty,
            Port: 0,
            Database: "db.sqlite",
            Username: string.Empty,
            Password: string.Empty
        );

        Assert.Contains(nameof(ExplainPlanViewModel.IsSimulated), changedProps);
    }

    [Fact]
    public void IncludeBuffers_IsForcedFalse_WhenAnalyzeDisabled()
    {
        var canvas = new CanvasViewModel();
        var sut = new ExplainPlanViewModel(canvas);

        sut.IncludeAnalyze = true;
        sut.IncludeBuffers = true;
        Assert.True(sut.IncludeBuffers);

        sut.IncludeAnalyze = false;
        Assert.False(sut.IncludeBuffers);
    }

    [Fact]
    public void CanUseAnalyzeOptions_TracksProvider()
    {
        var canvas = new CanvasViewModel();
        var sut = new ExplainPlanViewModel(canvas);

        Assert.True(sut.CanUseAnalyzeOptions);

        canvas.ActiveConnectionConfig = new ConnectionConfig(
            Provider: DatabaseProvider.SQLite,
            Host: string.Empty,
            Port: 0,
            Database: "db.sqlite",
            Username: string.Empty,
            Password: string.Empty
        );
        sut.Open();

        Assert.False(sut.CanUseAnalyzeOptions);
    }

    [Fact]
    public void HasAnalyzeDmlWarning_True_WhenAnalyzeEnabledAndSqlIsMutating()
    {
        var canvas = new CanvasViewModel();
        var sut = new ExplainPlanViewModel(canvas);
        SetPrivateField(sut, "_sql", "DELETE FROM orders WHERE id = 1");

        sut.IncludeAnalyze = true;

        Assert.True(sut.HasAnalyzeDmlWarning);
        Assert.False(string.IsNullOrWhiteSpace(sut.AnalyzeDmlWarningText));
    }

    [Fact]
    public void HasAnalyzeDmlWarning_False_WhenAnalyzeDisabled()
    {
        var canvas = new CanvasViewModel();
        var sut = new ExplainPlanViewModel(canvas);
        SetPrivateField(sut, "_sql", "DELETE FROM orders WHERE id = 1");

        sut.IncludeAnalyze = false;

        Assert.False(sut.HasAnalyzeDmlWarning);
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        FieldInfo? field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }
}


