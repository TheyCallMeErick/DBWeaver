using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainPlanDetailsPanelTests
{
    [Fact]
    public void SelectStepCommand_SelectsStep_AndExposesDetailFields()
    {
        var canvas = new CanvasViewModel();
        var sut = new ExplainPlanViewModel(canvas);
        var step = new ExplainStep
        {
            Operation = "Seq Scan",
            Detail = "relation=orders",
            EstimatedRows = 500,
            ActualRows = 48231,
            ActualTotalTimeMs = 15.125,
            ActualLoops = 3,
        };

        sut.SelectStepCommand.Execute(step);

        Assert.True(sut.HasSelectedStep);
        Assert.Equal("Seq Scan", sut.SelectedStepTitle);
        Assert.Equal("relation=orders", sut.SelectedStepDetailText);
        Assert.Equal("500", sut.SelectedStepEstimatedRowsText);
        Assert.Equal("48,231", sut.SelectedStepActualRowsText);
        Assert.Equal("95.5x", sut.SelectedStepRowsErrorText);
        Assert.Equal("15.125 ms", sut.SelectedStepActualTimeText);
        Assert.Equal("3", sut.SelectedStepLoopsText);
        Assert.Contains("Refresh table statistics", sut.SelectedStepSuggestionText);
        Assert.Equal("orders", sut.HighlightedTableName);
    }
}


