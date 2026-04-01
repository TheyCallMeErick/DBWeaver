using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Serialization;

public class CanvasSerializerLegacyProjectionMigrationTests
{
    [Fact]
    public void Deserialize_RedirectsLegacyColPins_AndReportsMigrationWarning()
    {
        string json =
            """
            {
              "Version": 3,
              "DatabaseProvider": "Postgres",
              "ConnectionName": "test",
              "Zoom": 1.0,
              "PanX": 0.0,
              "PanY": 0.0,
              "Nodes": [
                {
                  "NodeId": "tbl1",
                  "NodeType": "TableSource",
                  "X": 0,
                  "Y": 0,
                  "Alias": null,
                  "TableFullName": "public.orders",
                  "Parameters": {},
                  "PinLiterals": {},
                  "Columns": [
                    { "Name": "id", "Type": "Integer" }
                  ]
                },
                {
                  "NodeId": "cols1",
                  "NodeType": "ColumnSetBuilder",
                  "X": 240,
                  "Y": 0,
                  "Alias": null,
                  "TableFullName": null,
                  "Parameters": {},
                  "PinLiterals": {}
                },
                {
                  "NodeId": "out1",
                  "NodeType": "ResultOutput",
                  "X": 480,
                  "Y": 0,
                  "Alias": null,
                  "TableFullName": null,
                  "Parameters": {},
                  "PinLiterals": {}
                }
              ],
              "Connections": [
                { "FromNodeId": "tbl1", "FromPinName": "id", "ToNodeId": "cols1", "ToPinName": "col_1" },
                { "FromNodeId": "cols1", "FromPinName": "result", "ToNodeId": "out1", "ToPinName": "columns" }
              ],
              "SelectBindings": [],
              "WhereBindings": []
            }
            """;

        var vm = new CanvasViewModel();
        vm.Nodes.Clear();
        vm.Connections.Clear();

        CanvasLoadResult result = CanvasSerializer.Deserialize(json, vm);

        Assert.True(result.Success);
        Assert.NotNull(result.Warnings);
        Assert.Contains(result.Warnings!, w =>
            w.Contains("legacy projection", StringComparison.OrdinalIgnoreCase)
            || w.Contains("col_*", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(vm.Connections, c =>
            c.ToPin is not null
            && c.ToPin.Owner.Type == NodeType.ColumnSetBuilder
            && c.ToPin.Name == "columns");
    }
}
