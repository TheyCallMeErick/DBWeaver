using System.Data;
using System.IO;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace DBWeaver.UI.Controls.SqlEditor;

internal static class SqlEditorResultCellContentFormatter
{
    private const int ExpandLongTextThreshold = 120;

    public static string FormatCellValue(object?[]? row, int index)
    {
        if (row is null || index < 0 || index >= row.Length)
            return string.Empty;

        if (row[index] is null)
            return string.Empty;

        if (row[index] is DBNull)
            return "NULL";

        return row[index]?.ToString() ?? string.Empty;
    }

    public static string GetColumnTypeLabel(DataColumn column)
    {
        Type type = column.DataType;

        if (type == typeof(string))
            return "string";
        if (type == typeof(int))
            return "int";
        if (type == typeof(long))
            return "long";
        if (type == typeof(short))
            return "short";
        if (type == typeof(decimal))
            return "decimal";
        if (type == typeof(double))
            return "double";
        if (type == typeof(float))
            return "float";
        if (type == typeof(bool))
            return "bool";
        if (type == typeof(DateTime))
            return "datetime";
        if (type == typeof(DateTimeOffset))
            return "datetimeoffset";
        if (type == typeof(Guid))
            return "guid";
        if (type == typeof(byte[]))
            return "binary";

        return type.Name;
    }

    public static bool ShouldOfferExpandedView(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value.Length >= ExpandLongTextThreshold)
            return true;

        if (value.Contains('\n', StringComparison.Ordinal) || value.Contains('\r', StringComparison.Ordinal))
            return true;

        return LooksLikeJson(value) || LooksLikeXml(value);
    }

    public static string FormatExpandedCellValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        string normalized = value.Trim();

        if (LooksLikeJson(normalized) && TryFormatJson(normalized, out string? formattedJson))
            return formattedJson ?? value;

        if (LooksLikeXml(normalized) && TryFormatXml(normalized, out string? formattedXml))
            return formattedXml ?? value;

        return value;
    }

    private static bool LooksLikeJson(string value)
    {
        string normalized = value.TrimStart();
        return normalized.StartsWith('{') || normalized.StartsWith('[');
    }

    private static bool LooksLikeXml(string value)
    {
        string normalized = value.TrimStart();
        return normalized.StartsWith('<') && normalized.EndsWith('>');
    }

    private static bool TryFormatJson(string value, out string? formatted)
    {
        try
        {
            using JsonDocument jsonDocument = JsonDocument.Parse(value);
            formatted = JsonSerializer.Serialize(
                jsonDocument.RootElement,
                new JsonSerializerOptions { WriteIndented = true });
            return true;
        }
        catch (JsonException)
        {
            formatted = null;
            return false;
        }
    }

    private static bool TryFormatXml(string value, out string? formatted)
    {
        try
        {
            XDocument xml = XDocument.Parse(value);
            var settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = true,
            };

            using var writer = new StringWriter();
            using XmlWriter xmlWriter = XmlWriter.Create(writer, settings);
            xml.Save(xmlWriter);
            xmlWriter.Flush();
            formatted = writer.ToString();
            return true;
        }
        catch (XmlException)
        {
            formatted = null;
            return false;
        }
    }
}
