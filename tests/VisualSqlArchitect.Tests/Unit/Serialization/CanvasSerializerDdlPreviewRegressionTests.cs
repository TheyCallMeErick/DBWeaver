using DBWeaver.UI.Serialization;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas.Strategies;
using Xunit;

namespace DBWeaver.Tests.Unit.Serialization;

public class CanvasSerializerDdlPreviewRegressionTests
{
    [Fact]
    public void DeserializeWorkspace_WithDdlGraph_LoadsAndCompilesNonEmptyDdlSql()
    {
        string json = """
{
  "Version": 4,
  "QueryCanvas": {
    "Version": 3,
    "DatabaseProvider": "Postgres",
    "ConnectionName": "untitled",
    "Zoom": 1,
    "PanX": 0,
    "PanY": 0,
    "Nodes": [],
    "Connections": [],
    "SelectBindings": [],
    "WhereBindings": [],
    "AppVersion": "1.0.0",
    "CreatedAt": "2026-04-04T14:18:46.9755146Z"
  },
  "DdlCanvas": {
    "Version": 3,
    "DatabaseProvider": "SQLite",
    "ConnectionName": "untitled",
    "Zoom": 1,
    "PanX": 0,
    "PanY": 0,
    "Nodes": [
      {
        "NodeId": "f570970f",
        "NodeType": "CreateTableOutput",
        "X": 1600,
        "Y": 560,
        "ZOrder": 0,
        "Parameters": { "IdempotentMode": "None" },
        "PinLiterals": {}
      },
      {
        "NodeId": "d2499b6c",
        "NodeType": "TableDefinition",
        "X": 1216,
        "Y": 464,
        "ZOrder": 1,
        "Parameters": {
          "SchemaName": "public",
          "TableName": "TABELA",
          "IfNotExists": "true",
          "Comment": ""
        },
        "PinLiterals": {}
      },
      {
        "NodeId": "85875ac6",
        "NodeType": "ColumnDefinition",
        "X": 672,
        "Y": 320,
        "ZOrder": 2,
        "Parameters": {
          "ColumnName": "ID",
          "DataType": "INT",
          "IsNullable": "false",
          "UseNativeType": "false",
          "NativeTypeExpression": "",
          "Comment": "",
          "ResolvedDataTypeDisplay": "INT"
        },
        "PinLiterals": {}
      },
      {
        "NodeId": "61f73530",
        "NodeType": "ScalarTypeDefinition",
        "X": 400,
        "Y": 240,
        "ZOrder": 3,
        "Parameters": {
          "TypeKind": "INT",
          "Length": "255",
          "Precision": "18",
          "Scale": "2"
        },
        "PinLiterals": {}
      },
      {
        "NodeId": "e750e6ae",
        "NodeType": "PrimaryKeyConstraint",
        "X": 912,
        "Y": 624,
        "ZOrder": 4,
        "Parameters": { "ConstraintName": "" },
        "PinLiterals": {}
      }
    ],
    "Connections": [
      {
        "FromNodeId": "d2499b6c",
        "FromPinName": "table",
        "ToNodeId": "f570970f",
        "ToPinName": "table"
      },
      {
        "FromNodeId": "85875ac6",
        "FromPinName": "column",
        "ToNodeId": "d2499b6c",
        "ToPinName": "column"
      },
      {
        "FromNodeId": "61f73530",
        "FromPinName": "type_def",
        "ToNodeId": "85875ac6",
        "ToPinName": "type_def"
      },
      {
        "FromNodeId": "e750e6ae",
        "FromPinName": "pk",
        "ToNodeId": "d2499b6c",
        "ToPinName": "constraint"
      },
      {
        "FromNodeId": "85875ac6",
        "FromPinName": "column",
        "ToNodeId": "e750e6ae",
        "ToPinName": "column"
      }
    ],
    "SelectBindings": [],
    "WhereBindings": [],
    "AppVersion": "1.0.0",
    "CreatedAt": "2026-04-04T14:18:46.9755794Z"
  },
  "AppVersion": "1.0.0",
  "CreatedAt": "2026-04-04T14:18:46.9755798Z"
}
""";

        var queryCanvas = new CanvasViewModel(null, null, null, null, new QueryDomainStrategy());
        var ddlCanvas = new CanvasViewModel(null, null, null, null, new DdlDomainStrategy());

        var result = CanvasSerializer.DeserializeWorkspace(json, queryCanvas, ddlCanvas);
        Assert.True(result.Success, result.Error);

        Assert.NotNull(ddlCanvas.LiveDdl);
        ddlCanvas.LiveDdl!.Recompile();

        Assert.True(ddlCanvas.LiveDdl.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(ddlCanvas.LiveDdl.RawSql));
        Assert.Contains("CREATE TABLE", ddlCanvas.LiveDdl.RawSql, StringComparison.OrdinalIgnoreCase);
    }
}
