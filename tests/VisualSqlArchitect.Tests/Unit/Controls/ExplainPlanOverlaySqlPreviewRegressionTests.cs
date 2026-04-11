using DBWeaver.UI.Services.Explain;
using System.IO;

namespace DBWeaver.Tests.Unit.Controls;

public class ExplainPlanOverlaySqlPreviewRegressionTests
{
    [Fact]
    public void SqlPreview_ProvidesFullSqlTooltipBinding()
    {
        string xaml = ReadOverlayXaml();

        Assert.Contains("SelectableTextBlock Grid.Column=\"1\"", xaml);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", xaml);
        Assert.Contains("MaxLines=\"2\"", xaml);
        Assert.Contains("<ToolTip.Tip>", xaml);
        Assert.Contains("<TextBlock Text=\"{Binding SqlTooltipText}\"", xaml);
    }

    [Fact]
    public void Header_ContainsAnalyzeAndBuffersToggles_ForSupportedProviders()
    {
        string xaml = ReadOverlayXaml();

        Assert.Contains("IsVisible=\"{Binding CanUseAnalyzeOptions}\"", xaml);
        Assert.Contains("IsChecked=\"{Binding IncludeAnalyze}\"", xaml);
        Assert.Contains("IsChecked=\"{Binding IncludeBuffers}\"", xaml);
        Assert.Contains("IsEnabled=\"{Binding IncludeAnalyze}\"", xaml);
    }

    [Fact]
    public void Overlay_ShowsAnalyzeDmlWarningBinding()
    {
        string xaml = ReadOverlayXaml();

        Assert.Contains("IsVisible=\"{Binding HasAnalyzeDmlWarning}\"", xaml);
        Assert.Contains("Text=\"{Binding AnalyzeDmlWarningText}\"", xaml);
    }

    [Fact]
    public void PlanRows_ContainRelativeCostBarBindings()
    {
        string xaml = ReadOverlayXaml();

        Assert.Contains("IsVisible=\"{Binding HasCostBar}\"", xaml);
        Assert.Contains("Width=\"{Binding CostBarWidth}\"", xaml);
        Assert.Contains("Color=\"{Binding CostBarFill}\"", xaml);
        Assert.Contains("Text=\"{Binding RowsErrorText}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding HasRowsError}\"", xaml);
        Assert.Contains("Text=\"{Binding StaleStatsLabel}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding HasStaleStatsBadge}\"", xaml);
    }

    [Fact]
    public void Overlay_ContainsDetailsPanelBindings()
    {
        string xaml = ReadOverlayXaml();

        Assert.Contains("Click=\"OnStepRowClick\"", xaml);
        Assert.Contains("IsVisible=\"{Binding HasSelectedStep}\"", xaml);
        Assert.Contains("Text=\"{Binding SelectedStepTitle}\"", xaml);
        Assert.Contains("Text=\"{Binding SelectedStepDetailText}\"", xaml);
        Assert.Contains("Text=\"{Binding SelectedStepSuggestionText}\"", xaml);
    }

    [Fact]
    public void Footer_ContainsExplainExportActions()
    {
        string xaml = ReadOverlayXaml();

        Assert.Contains("Name=\"CopyJsonBtn\"", xaml);
        Assert.Contains("Name=\"CopyTextBtn\"", xaml);
        Assert.Contains("Name=\"SaveJsonBtn\"", xaml);
        Assert.Contains("Name=\"OpenDaliboBtn\"", xaml);
        Assert.Contains("IsEnabled=\"{Binding HasRawOutput}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding CanOpenDalibo}\"", xaml);
    }

    [Fact]
    public void Overlay_ContainsCompareSnapshotBindings()
    {
        string xaml = ReadOverlayXaml();

        Assert.Contains("Name=\"SnapshotBtn\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding Snapshots}\"", xaml);
        Assert.Contains("SelectedItem=\"{Binding SelectedSnapshotA}\"", xaml);
        Assert.Contains("SelectedItem=\"{Binding SelectedSnapshotB}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding ComparisonRows}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding CanCompareSnapshots}\"", xaml);
    }

    [Fact]
    public void Overlay_ContainsIndexSuggestionCards()
    {
        string xaml = ReadOverlayXaml();

        Assert.Contains("ItemsSource=\"{Binding IndexSuggestions}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding HasIndexSuggestions}\"", xaml);
        Assert.Contains("Click=\"OnIndexSuggestionClick\"", xaml);
        Assert.Contains("x:DataType=\"explain:ExplainIndexSuggestion\"", xaml);
        Assert.Contains("Text=\"{Binding Sql}\"", xaml);
    }

    [Fact]
    public void Overlay_ContainsHistoryBindings()
    {
        string xaml = ReadOverlayXaml();

        Assert.Contains("ItemsSource=\"{Binding History}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding HasHistory}\"", xaml);
        Assert.Contains("x:DataType=\"explain:ExplainHistoryItem\"", xaml);
        Assert.Contains("Text=\"{Binding TimeText}\"", xaml);
        Assert.Contains("Text=\"{Binding AlertText}\"", xaml);
    }

    [Fact]
    public void Overlay_ContainsTreeModeToggleAndCanvas()
    {
        string xaml = ReadOverlayXaml();

        Assert.Contains("Name=\"ListModeBtn\"", xaml);
        Assert.Contains("Name=\"TreeModeBtn\"", xaml);
        Assert.Contains("IsVisible=\"{Binding ShowListView}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding ShowTreeView}\"", xaml);
        Assert.Contains("Name=\"TreeCanvas\"", xaml);
    }

    [Fact]
    public void Overlay_UsesExpandedTwoColumnTechnicalLayout()
    {
        string xaml = ReadOverlayXaml();

        Assert.Contains("Width=\"1220\"", xaml);
        Assert.Contains("Height=\"820\"", xaml);
        Assert.Contains("Grid IsVisible=\"{Binding ShowListView}\"", xaml);
        Assert.Contains("ColumnDefinitions=\"3*,2*\"", xaml);
        Assert.Contains("Text=\"{Binding [explain.section.snapshotComparison], Source={x:Static loc:LocalizationService.Instance}}\"", xaml);
    }

    private static string ReadOverlayXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "DBWeaver.UI",
                "Controls",
                "ExplainPlan",
                "ExplainPlanOverlay.axaml"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate ExplainPlanOverlay.axaml from test base directory.");
    }
}

