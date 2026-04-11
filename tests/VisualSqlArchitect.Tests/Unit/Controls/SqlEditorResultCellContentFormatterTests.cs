using System.Data;
using DBWeaver.UI.Controls.SqlEditor;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class SqlEditorResultCellContentFormatterTests
{
    [Fact]
    public void FormatCellValue_ReturnsNullLabelForDatabaseNull()
    {
        object?[] row = [DBNull.Value];

        string value = SqlEditorResultCellContentFormatter.FormatCellValue(row, 0);

        Assert.Equal("NULL", value);
    }

    [Fact]
    public void GetColumnTypeLabel_ReturnsKnownTypeAlias()
    {
        var table = new DataTable();
        DataColumn column = table.Columns.Add("created_at", typeof(DateTime));

        string type = SqlEditorResultCellContentFormatter.GetColumnTypeLabel(column);

        Assert.Equal("datetime", type);
    }

    [Fact]
    public void ShouldOfferExpandedView_ReturnsTrueForJsonAndLongText()
    {
        string json = "{\"id\":1,\"name\":\"users\"}";
        string longText = new string('x', 140);

        bool jsonResult = SqlEditorResultCellContentFormatter.ShouldOfferExpandedView(json);
        bool longTextResult = SqlEditorResultCellContentFormatter.ShouldOfferExpandedView(longText);

        Assert.True(jsonResult);
        Assert.True(longTextResult);
    }

    [Fact]
    public void FormatExpandedCellValue_PrettyPrintsJson()
    {
        string json = "{\"a\":1,\"b\":{\"c\":2}}";

        string formatted = SqlEditorResultCellContentFormatter.FormatExpandedCellValue(json);

        Assert.Contains("\n", formatted);
        Assert.Contains("\"b\":", formatted);
    }

    [Fact]
    public void FormatExpandedCellValue_PrettyPrintsXml()
    {
        string xml = "<root><item id=\"1\">x</item></root>";

        string formatted = SqlEditorResultCellContentFormatter.FormatExpandedCellValue(xml);

        Assert.Contains("<root>", formatted);
        Assert.Contains("<item id=\"1\">x</item>", formatted);
    }
}
