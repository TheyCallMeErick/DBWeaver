using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Canvas;
using VisualSqlArchitect.UI.Serialization;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

/// <summary>
/// Tests for FlowVersionOverlayViewModel handling of duplicate keys in
/// canvas state diffs. Relates to Fragility Report: ALTO priority issue
/// where ToDictionary() throws ArgumentException on duplicate NodeIds
/// or connection keys from serialization bugs.
/// </summary>
public class FlowVersionOverlayViewModelDuplicateKeyTests
{
    [Fact]
    public void FlowVersionOverlayViewModel_CanBeInstantiated()
    {
        var canvas = new CanvasViewModel();
        var vm = new FlowVersionOverlayViewModel(canvas);
        Assert.NotNull(vm);
    }

    [Fact]
    public void FlowVersionOverlayViewModel_HasVersionsCollection()
    {
        var canvas = new CanvasViewModel();
        var vm = new FlowVersionOverlayViewModel(canvas);
        Assert.NotNull(vm.Versions);
        Assert.Empty(vm.Versions);
    }

    [Fact]
    public void FlowVersionOverlayViewModel_HasDiffItemsCollection()
    {
        var canvas = new CanvasViewModel();
        var vm = new FlowVersionOverlayViewModel(canvas);
        Assert.NotNull(vm.DiffItems);
        Assert.Empty(vm.DiffItems);
    }

    [Fact]
    public void FlowVersionOverlayViewModel_CanOpenAndClose()
    {
        var canvas = new CanvasViewModel();
        var vm = new FlowVersionOverlayViewModel(canvas);

        Assert.False(vm.IsVisible);
        vm.Open();
        Assert.True(vm.IsVisible);
        vm.Close();
        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void FlowVersionOverlayViewModel_CanCreateCheckpoint()
    {
        var canvas = new CanvasViewModel();
        var vm = new FlowVersionOverlayViewModel(canvas);

        // Clear any existing versions
        vm.DeleteAllVersions();

        // Create checkpoint
        vm.CreateCheckpoint("Test Checkpoint");

        // Verify checkpoint was created
        Assert.NotEmpty(vm.Versions);
    }

    [Fact]
    public void FlowVersionOverlayViewModel_CanSelectVersion()
    {
        var canvas = new CanvasViewModel();
        var vm = new FlowVersionOverlayViewModel(canvas);

        // Create a checkpoint
        vm.CreateCheckpoint("Test");

        // Select it
        if (vm.Versions.Count > 0)
        {
            vm.SelectedVersion = vm.Versions[0];
            Assert.Equal(vm.Versions[0], vm.SelectedVersion);
        }
    }

    [Fact]
    public void FlowVersionOverlayViewModel_HandlesCheckpointDeletion()
    {
        var canvas = new CanvasViewModel();
        var vm = new FlowVersionOverlayViewModel(canvas);

        // Clear existing
        vm.DeleteAllVersions();

        // Create and delete
        vm.CreateCheckpoint("Temp");
        Assert.NotEmpty(vm.Versions);

        var versionId = vm.Versions[0].Id;
        vm.DeleteVersion(versionId);

        // Should be deleted if store supports it (may be empty or same)
        Assert.True(vm.Versions.Count == 0 || vm.Versions[0].Id != versionId);
    }

    [Fact]
    public void FlowVersionOverlayViewModel_DiffModeToggle()
    {
        var canvas = new CanvasViewModel();
        var vm = new FlowVersionOverlayViewModel(canvas);

        Assert.False(vm.IsDiffMode);
        vm.IsDiffMode = true;
        Assert.True(vm.IsDiffMode);
        vm.IsDiffMode = false;
        Assert.False(vm.IsDiffMode);
    }

    [Fact]
    public void FlowVersionOverlayViewModel_DiffSummary_Empty()
    {
        var canvas = new CanvasViewModel();
        var vm = new FlowVersionOverlayViewModel(canvas);

        Assert.Equal("No differences", vm.DiffSummary);
    }

        [Fact]
        public void ComputeDiff_DuplicateNodeIds_AddsDiagnosticItem()
        {
                var canvas = new CanvasViewModel();
                var vm = new FlowVersionOverlayViewModel(canvas);

                string baseJson = """
                        {
                            "Version": 3,
                            "DatabaseProvider": "Postgres",
                            "ConnectionName": "x",
                            "Zoom": 1.0,
                            "PanX": 0,
                            "PanY": 0,
                            "Nodes": [
                                { "NodeId": "n1", "NodeType": "TableSource", "X": 0, "Y": 0, "ZOrder": 0, "Alias": null, "TableFullName": null, "Parameters": {}, "PinLiterals": {} },
                                { "NodeId": "n1", "NodeType": "TableSource", "X": 1, "Y": 1, "ZOrder": 0, "Alias": null, "TableFullName": null, "Parameters": {}, "PinLiterals": {} }
                            ],
                            "Connections": [],
                            "SelectBindings": [],
                            "WhereBindings": []
                        }
                        """;

                string headJson = baseJson;

                var from = new FlowVersion("id1", "base", DateTimeOffset.UtcNow.ToString("o"), 2, 0, baseJson);
                var to = new FlowVersion("id2", "head", DateTimeOffset.UtcNow.ToString("o"), 2, 0, headJson);

                vm.ComputeDiff(from, to);

                Assert.Contains(vm.DiffItems, d => d.Description.Contains("Duplicate NodeId entries detected"));
        }

        [Fact]
        public void ComputeDiff_DuplicateConnections_AddsDiagnosticItem()
        {
                var canvas = new CanvasViewModel();
                var vm = new FlowVersionOverlayViewModel(canvas);

                string baseJson = """
                        {
                            "Version": 3,
                            "DatabaseProvider": "Postgres",
                            "ConnectionName": "x",
                            "Zoom": 1.0,
                            "PanX": 0,
                            "PanY": 0,
                            "Nodes": [
                                { "NodeId": "a", "NodeType": "TableSource", "X": 0, "Y": 0, "ZOrder": 0, "Alias": null, "TableFullName": null, "Parameters": {}, "PinLiterals": {} },
                                { "NodeId": "b", "NodeType": "TableSource", "X": 1, "Y": 1, "ZOrder": 0, "Alias": null, "TableFullName": null, "Parameters": {}, "PinLiterals": {} }
                            ],
                            "Connections": [
                                { "FromNodeId": "a", "FromPinName": "id", "ToNodeId": "b", "ToPinName": "id" },
                                { "FromNodeId": "a", "FromPinName": "id", "ToNodeId": "b", "ToPinName": "id" }
                            ],
                            "SelectBindings": [],
                            "WhereBindings": []
                        }
                        """;

                var from = new FlowVersion("id1", "base", DateTimeOffset.UtcNow.ToString("o"), 2, 2, baseJson);
                var to = new FlowVersion("id2", "head", DateTimeOffset.UtcNow.ToString("o"), 2, 2, baseJson);

                vm.ComputeDiff(from, to);

                Assert.Contains(vm.DiffItems, d => d.Description.Contains("Duplicate connection keys detected"));
        }

    [Fact]
    public void FlowVersionOverlayViewModel_NewLabelProperty()
    {
        var canvas = new CanvasViewModel();
        var vm = new FlowVersionOverlayViewModel(canvas);

        vm.NewLabel = "My Label";
        Assert.Equal("My Label", vm.NewLabel);
    }

    [Fact]
    public void FlowVersionOverlayViewModel_HasSelectionProperty()
    {
        var canvas = new CanvasViewModel();
        var vm = new FlowVersionOverlayViewModel(canvas);

        Assert.False(vm.HasSelection);

        vm.CreateCheckpoint("Test");
        if (vm.Versions.Count > 0)
        {
            vm.SelectedVersion = vm.Versions[0];
            Assert.True(vm.HasSelection);
        }
    }

    [Fact]
    public void FlowVersionOverlayViewModel_HasVersionsProperty()
    {
        var canvas = new CanvasViewModel();
        var vm = new FlowVersionOverlayViewModel(canvas);

        Assert.False(vm.HasVersions);

        vm.CreateCheckpoint("Test");
        Assert.True(vm.HasVersions);
    }

    // Helper to ensure clean state
    private void DeleteAllVersions(FlowVersionOverlayViewModel vm)
    {
        while (vm.Versions.Count > 0)
        {
            vm.DeleteVersion(vm.Versions[0].Id);
        }
    }
}

// Extension method for testing
internal static class FlowVersionOverlayViewModelTestExtensions
{
    internal static void DeleteAllVersions(this FlowVersionOverlayViewModel vm)
    {
        var versionIds = vm.Versions.Select(v => v.Id).ToList();
        foreach (var id in versionIds)
        {
            vm.DeleteVersion(id);
        }
    }
}
