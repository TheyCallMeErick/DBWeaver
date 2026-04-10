using DBWeaver.UI.Services.Benchmark;
using System.Data;
using Avalonia;
using DBWeaver.Ddl;
using DBWeaver.Nodes;
using DBWeaver.UI.Services.QueryPreview.Models;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.UI.ViewModels.Canvas.Strategies;

namespace DBWeaver.Tests.Unit.ViewModels;

public class AppDiagnosticsViewModelOverhaulTests
{
    [Fact]
    public void Categories_AreStructuredForAccordionSections()
    {
        var canvas = new CanvasViewModel();
        var vm = canvas.Diagnostics;

        Assert.Equal(4, vm.Categories.Count);
        Assert.Contains(vm.Categories, c => c.Key == "canvas");
        Assert.Contains(vm.Categories, c => c.Key == "output");
        Assert.Contains(vm.Categories, c => c.Key == "session");
        Assert.Contains(vm.Categories, c => c.Key == "notice");
    }

    [Fact]
    public void QueryOutputError_IsMirroredIntoDiagnosticsOutputCategory()
    {
        var canvas = new CanvasViewModel();
        var vm = canvas.Diagnostics;

        canvas.DataPreview.ShowError("Preview execution failed due timeout.");
        vm.RunChecksCommand.Execute(null);

        AppDiagnosticCategoryViewModel output = Assert.Single(vm.Categories, c => c.Key == "output");
        IReadOnlyList<AppDiagnosticEntry> outputItems = output.SnapshotItems();
        Assert.Contains(outputItems, e => e.Status == EDiagnosticStatus.Error);
        Assert.True(vm.HasAttention);
    }

    [Fact]
    public void DdlOutputWithoutStatements_ProducesWarningWithGuidance()
    {
        var ddlCanvas = new CanvasViewModel(
            nodeManager: null,
            pinManager: null,
            selectionManager: null,
            localizationService: null,
            domainStrategy: new DdlDomainStrategy());
        var vm = ddlCanvas.Diagnostics;

        vm.RunChecksCommand.Execute(null);

        AppDiagnosticCategoryViewModel output = Assert.Single(vm.Categories, c => c.Key == "output");
        IReadOnlyList<AppDiagnosticEntry> outputItems = output.SnapshotItems();
        Assert.Equal(EDiagnosticStatus.Warning, outputItems[1].Status);
    }

    [Fact]
    public void AddWarning_AppendsRuntimeNoticeCategory()
    {
        var canvas = new CanvasViewModel();
        var vm = canvas.Diagnostics;

        vm.AddWarning("Migration", "Legacy nodes transformed", "Save project");

        AppDiagnosticCategoryViewModel notices = Assert.Single(vm.Categories, c => c.Key == "notice");
        Assert.Contains(notices.SnapshotItems(), e => e.Name == "Migration");
    }

    [Fact]
    public void AttentionCountLabel_TracksErrorsAndWarnings()
    {
        var canvas = new CanvasViewModel();
        var vm = canvas.Diagnostics;

        vm.AddWarning("Check", "Any warning", "Fix later");

        Assert.True(vm.AttentionCount > 0);
        Assert.Equal(vm.AttentionCount.ToString(), vm.AttentionCountLabel);
    }

    [Fact]
    public void QueryOutputWarning_FromLiveSqlCollections_IsMirroredToOutputCategory()
    {
        var canvas = new CanvasViewModel();
        var vm = canvas.Diagnostics;

        canvas.LiveSql.GuardIssues.Add(
            new GuardIssue(EGuardSeverity.Warning, "NO_LIMIT", "Missing limit", "Add LIMIT")
        );
        vm.RunChecksCommand.Execute(null);

        AppDiagnosticCategoryViewModel output = Assert.Single(vm.Categories, c => c.Key == "output");
        AppDiagnosticEntry compile = output.SnapshotItems()[0];

        Assert.Equal(EDiagnosticStatus.Warning, compile.Status);
        Assert.Contains("guardrail", compile.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QueryOutputError_FromLiveSqlErrorHints_IsMirroredToOutputCategory()
    {
        var canvas = new CanvasViewModel();
        var vm = canvas.Diagnostics;

        canvas.LiveSql.ErrorHints.Add("Invalid graph: output node missing source");
        vm.RunChecksCommand.Execute(null);

        AppDiagnosticCategoryViewModel output = Assert.Single(vm.Categories, c => c.Key == "output");
        AppDiagnosticEntry compile = output.SnapshotItems()[0];

        Assert.Equal(EDiagnosticStatus.Error, compile.Status);
        Assert.Contains("Invalid graph", compile.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryOutputCancelledPreview_IsMirroredAsWarning()
    {
        var canvas = new CanvasViewModel();
        var vm = canvas.Diagnostics;

        canvas.DataPreview.ShowCancelled();
        vm.RunChecksCommand.Execute(null);

        AppDiagnosticCategoryViewModel output = Assert.Single(vm.Categories, c => c.Key == "output");
        AppDiagnosticEntry execution = output.SnapshotItems()[2];

        Assert.Equal(EDiagnosticStatus.Warning, execution.Status);
        Assert.False(string.IsNullOrWhiteSpace(execution.Details));
    }

    [Fact]
    public void QueryOutputDonePreview_IsMirroredAsOk()
    {
        var canvas = new CanvasViewModel();
        var vm = canvas.Diagnostics;
        var table = new DataTable();
        table.Columns.Add("id");
        table.Rows.Add("1");

        canvas.DataPreview.ShowResults(table, 12);
        vm.RunChecksCommand.Execute(null);

        AppDiagnosticCategoryViewModel output = Assert.Single(vm.Categories, c => c.Key == "output");
        AppDiagnosticEntry execution = output.SnapshotItems()[2];

        Assert.Equal(EDiagnosticStatus.Ok, execution.Status);
        Assert.False(string.IsNullOrWhiteSpace(execution.Details));
    }

    [Fact]
    public void DdlOutputWithCompilerWarning_IsMirroredAsWarning()
    {
        var ddlCanvas = new CanvasViewModel(
            nodeManager: null,
            pinManager: null,
            selectionManager: null,
            localizationService: null,
            domainStrategy: new DdlDomainStrategy());
        var vm = ddlCanvas.Diagnostics;
        Assert.NotNull(ddlCanvas.LiveDdl);
        LiveDdlBarViewModel liveDdl = ddlCanvas.LiveDdl!;

        liveDdl.DiagnosticsPanel.ReplaceDiagnostics(
        [
            new DdlCompileDiagnostic(
                "W-DDL-TEST",
                DdlDiagnosticSeverity.Warning,
                "Synthetic warning for diagnostics coverage")
        ]);
        vm.RunChecksCommand.Execute(null);

        AppDiagnosticCategoryViewModel output = Assert.Single(vm.Categories, c => c.Key == "output");
        AppDiagnosticEntry compile = output.SnapshotItems()[0];

        Assert.Equal(EDiagnosticStatus.Warning, compile.Status);
        Assert.False(string.IsNullOrWhiteSpace(compile.Details));
    }

    [Fact]
    public void AddInfo_AddsCanvasMigrationNotice()
    {
        var canvas = new CanvasViewModel();
        var vm = canvas.Diagnostics;

        vm.AddInfo("Migrated legacy payload");

        AppDiagnosticCategoryViewModel notices = Assert.Single(vm.Categories, c => c.Key == "notice");
        Assert.Contains(notices.SnapshotItems(), n => n.Details.Contains("Migrated legacy payload", StringComparison.Ordinal));
    }

    [Fact]
    public void QueryOutputDetailedDiagnostics_IncludeCodeLocationAndFocusAction()
    {
        var canvas = new CanvasViewModel();
        var vm = canvas.Diagnostics;
        NodeViewModel node = canvas.SpawnNode(NodeDefinitionRegistry.Get(NodeType.Equals), new Point(40, 20));

        canvas.LiveSql.Diagnostics.Add(
            new PreviewDiagnostic(
                PreviewDiagnosticSeverity.Warning,
                PreviewDiagnosticCategory.General,
                "W-TEST-900",
                "Synthetic diagnostic",
                node.Id,
                "lhs"));

        vm.RunChecksCommand.Execute(null);

        AppDiagnosticCategoryViewModel output = Assert.Single(vm.Categories, c => c.Key == "output");
        AppDiagnosticEntry detail = Assert.Single(output.SnapshotItems(), i => i.Code == "W-TEST-900");

        Assert.True(detail.HasCode);
        Assert.True(detail.HasLocation);
        Assert.True(detail.HasFocusAction);
        Assert.Equal(EDiagnosticStatus.Warning, detail.Status);
    }

    [Fact]
    public void OpenAndCloseCommands_ToggleVisibility()
    {
        var canvas = new CanvasViewModel();
        var vm = canvas.Diagnostics;

        vm.Open();
        Assert.True(vm.IsVisible);

        vm.CloseCommand.Execute(null);
        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void CopyReportCommand_PublishesReportToDataPreviewError()
    {
        var canvas = new CanvasViewModel();
        var vm = canvas.Diagnostics;

        vm.CopyReportCommand.Execute(null);

        Assert.Equal(EPreviewExecutionState.Failed, canvas.DataPreview.CurrentState);
        Assert.False(string.IsNullOrWhiteSpace(canvas.DataPreview.ErrorMessage));
    }

    [Fact]
    public void CanvasCategory_WhenNodeExists_ReportsNonEmptyState()
    {
        var canvas = new CanvasViewModel();
        var vm = canvas.Diagnostics;

        canvas.SpawnNode(NodeDefinitionRegistry.Get(NodeType.Equals), new Point(40, 20));
        vm.RunChecksCommand.Execute(null);

        AppDiagnosticCategoryViewModel category = Assert.Single(vm.Categories, c => c.Key == "canvas");
        AppDiagnosticEntry state = category.SnapshotItems()[0];
        Assert.Equal(EDiagnosticStatus.Ok, state.Status);
    }
}
