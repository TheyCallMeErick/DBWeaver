using DBWeaver.UI.Services.Canvas.AutoJoin;
using System.Reflection;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainPlanExportViewModelTests
{
    [Fact]
    public void BuildExportText_UsesInjectedFormatter()
    {
        var canvas = new CanvasViewModel();
        var formatter = new RecordingFormatter();
        var sut = new ExplainPlanViewModel(
            canvas,
            exportFormatter: formatter,
            daliboUrlBuilder: new StubDaliboUrlBuilder("https://example")
        );

        string output = sut.BuildExportText();

        Assert.Equal("formatted-output", output);
        Assert.NotNull(formatter.LastData);
        Assert.Equal(sut.ProviderLabel, formatter.LastData!.ProviderLabel);
    }

    [Fact]
    public void CanOpenDalibo_IsTrue_WhenRawOutputExistsAndBuilderReturnsUrl()
    {
        var canvas = new CanvasViewModel();
        var sut = new ExplainPlanViewModel(
            canvas,
            daliboUrlBuilder: new StubDaliboUrlBuilder("https://dalibo.local/plan")
        );
        SetPrivateField(sut, "_rawOutput", "{\"Plan\":{}}");

        Assert.True(sut.CanOpenDalibo);
        Assert.Equal("https://dalibo.local/plan", sut.BuildDaliboUrl());
    }

    [Fact]
    public void CanOpenDalibo_IsFalse_WhenProviderIsNotPostgres()
    {
        var canvas = new CanvasViewModel();
        canvas.ActiveConnectionConfig = new DBWeaver.Core.ConnectionConfig(
            Provider: DBWeaver.Core.DatabaseProvider.SQLite,
            Host: string.Empty,
            Port: 0,
            Database: "db.sqlite",
            Username: string.Empty,
            Password: string.Empty
        );
        var sut = new ExplainPlanViewModel(
            canvas,
            daliboUrlBuilder: new StubDaliboUrlBuilder("https://dalibo.local/plan")
        );
        SetPrivateField(sut, "_provider", DBWeaver.Core.DatabaseProvider.SQLite);
        SetPrivateField(sut, "_rawOutput", "{\"Plan\":{}}");

        Assert.False(sut.CanOpenDalibo);
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        FieldInfo? field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private sealed class RecordingFormatter : IExplainPlanExportFormatter
    {
        public ExplainPlanExportData? LastData { get; private set; }

        public string Format(ExplainPlanExportData data)
        {
            LastData = data;
            return "formatted-output";
        }
    }

    private sealed class StubDaliboUrlBuilder(string? url) : IExplainDaliboUrlBuilder
    {
        private readonly string? _url = url;

        public string? Build(string? rawJson) =>
            string.IsNullOrWhiteSpace(rawJson) ? null : _url;
    }
}

