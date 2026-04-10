using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

/// <summary>
/// Tests for <see cref="NodeViewModel.ScalarTypeInlineLabel"/> and related properties
/// on <see cref="NodeType.ScalarTypeDefinition"/> nodes.
/// </summary>
public class NodeViewModelScalarTypeLabelTests
{
    private static NodeViewModel BuildScalarType() =>
        new(NodeDefinitionRegistry.Get(NodeType.ScalarTypeDefinition), new Point(0, 0));

    // â”€â”€ IsScalarTypeDefinition â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void IsScalarTypeDefinition_TrueForScalarTypeNode()
    {
        var node = BuildScalarType();
        Assert.True(node.IsScalarTypeDefinition);
    }

    [Fact]
    public void IsScalarTypeDefinition_FalseForColumnDefinitionNode()
    {
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(0, 0));
        Assert.False(node.IsScalarTypeDefinition);
    }

    // â”€â”€ ScalarTypeInlineLabel: VARCHAR â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void ScalarTypeInlineLabel_Varchar_WithLength()
    {
        var node = BuildScalarType();
        node.Parameters["TypeKind"] = "VARCHAR";
        node.Parameters["Length"] = "120";

        Assert.Equal("VARCHAR(120)", node.ScalarTypeInlineLabel);
    }

    [Fact]
    public void ScalarTypeInlineLabel_Varchar_DefaultLength_WhenLengthMissing()
    {
        var node = BuildScalarType();
        node.Parameters["TypeKind"] = "VARCHAR";
        node.Parameters.Remove("Length");

        Assert.Equal("VARCHAR(255)", node.ScalarTypeInlineLabel);
    }

    [Fact]
    public void ScalarTypeInlineLabel_Varchar_DefaultLength_WhenLengthNotNumeric()
    {
        var node = BuildScalarType();
        node.Parameters["TypeKind"] = "VARCHAR";
        node.Parameters["Length"] = "notanumber";

        Assert.Equal("VARCHAR(255)", node.ScalarTypeInlineLabel);
    }

    [Fact]
    public void ScalarTypeInlineLabel_Varchar_ClampsLengthToAtLeastOne()
    {
        var node = BuildScalarType();
        node.Parameters["TypeKind"] = "VARCHAR";
        node.Parameters["Length"] = "0";

        Assert.Equal("VARCHAR(1)", node.ScalarTypeInlineLabel);
    }

    [Fact]
    public void ScalarTypeInlineLabel_Varchar_DefaultTypeKind_WhenTypeMissing()
    {
        var node = BuildScalarType();
        node.Parameters.Remove("TypeKind");
        node.Parameters["Length"] = "50";

        // Falls back to VARCHAR
        Assert.Equal("VARCHAR(50)", node.ScalarTypeInlineLabel);
    }

    // â”€â”€ ScalarTypeInlineLabel: DECIMAL â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void ScalarTypeInlineLabel_Decimal_WithPrecisionAndScale()
    {
        var node = BuildScalarType();
        node.Parameters["TypeKind"] = "DECIMAL";
        node.Parameters["Precision"] = "18";
        node.Parameters["Scale"] = "2";

        Assert.Equal("DECIMAL(18,2)", node.ScalarTypeInlineLabel);
    }

    [Fact]
    public void ScalarTypeInlineLabel_Decimal_DefaultsWhenParamsMissing()
    {
        var node = BuildScalarType();
        node.Parameters["TypeKind"] = "DECIMAL";
        node.Parameters.Remove("Precision");
        node.Parameters.Remove("Scale");

        Assert.Equal("DECIMAL(18,2)", node.ScalarTypeInlineLabel);
    }

    [Fact]
    public void ScalarTypeInlineLabel_Decimal_ClampsNegativeScale()
    {
        var node = BuildScalarType();
        node.Parameters["TypeKind"] = "DECIMAL";
        node.Parameters["Precision"] = "10";
        node.Parameters["Scale"] = "-1";

        Assert.Equal("DECIMAL(10,0)", node.ScalarTypeInlineLabel);
    }

    // â”€â”€ ScalarTypeInlineLabel: fixed types â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Theory]
    [InlineData("TEXT")]
    [InlineData("INT")]
    [InlineData("BIGINT")]
    [InlineData("BOOLEAN")]
    [InlineData("DATE")]
    [InlineData("DATETIME")]
    [InlineData("JSON")]
    [InlineData("UUID")]
    public void ScalarTypeInlineLabel_FixedTypes_ReturnTypeKindAsIs(string typeKind)
    {
        var node = BuildScalarType();
        node.Parameters["TypeKind"] = typeKind;

        Assert.Equal(typeKind, node.ScalarTypeInlineLabel);
    }

    [Fact]
    public void ScalarTypeInlineLabel_TypeKindIsCaseNormalized()
    {
        var node = BuildScalarType();
        node.Parameters["TypeKind"] = "text";

        Assert.Equal("TEXT", node.ScalarTypeInlineLabel);
    }

    // â”€â”€ RaiseParameterChanged notifications â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Theory]
    [InlineData("TypeKind")]
    [InlineData("Length")]
    [InlineData("Precision")]
    [InlineData("Scale")]
    public void RaiseParameterChanged_RaisesScalarTypeInlineLabelChange(string param)
    {
        var node = BuildScalarType();
        List<string> raised = [];
        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null) raised.Add(e.PropertyName);
        };

        node.RaiseParameterChanged(param);

        Assert.Contains(nameof(NodeViewModel.ScalarTypeInlineLabel), raised);
    }

    [Fact]
    public void RaiseParameterChanged_OtherParams_DoNotRaiseScalarTypeInlineLabelChange()
    {
        var node = BuildScalarType();
        List<string> raised = [];
        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null) raised.Add(e.PropertyName);
        };

        node.RaiseParameterChanged("Comment");

        Assert.DoesNotContain(nameof(NodeViewModel.ScalarTypeInlineLabel), raised);
    }

    [Fact]
    public void RaiseParameterChanged_NonScalarTypeNode_DoesNotRaiseScalarTypeInlineLabelChange()
    {
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(0, 0));
        List<string> raised = [];
        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null) raised.Add(e.PropertyName);
        };

        node.RaiseParameterChanged("TypeKind");

        Assert.DoesNotContain(nameof(NodeViewModel.ScalarTypeInlineLabel), raised);
    }
}


