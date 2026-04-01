namespace VisualSqlArchitect.Nodes.Definitions;

using VisualSqlArchitect.Nodes;
using static NodeDefinitionHelpers;

/// <summary>
/// JSON manipulation node definitions.
/// Defines nodes for extracting and manipulating JSON data.
/// </summary>
public static class JsonDefinitions
{
    public static readonly NodeDefinition JsonExtract = new(
        NodeType.JsonExtract,
        NodeCategory.Json,
        "JSON Extract",
        "Extracts a value from a JSON column by path",
        [In("json", PinDataType.Json), Out("value", PinDataType.Expression)],
        [
            new(
                "path",
                VisualSqlArchitect.Nodes.ParameterKind.JsonPath,
                null,
                "JSON path (e.g. $.address.city)"
            ),
            new(
                "outputType",
                VisualSqlArchitect.Nodes.ParameterKind.Enum,
                "Text",
                "Cast extracted value to type",
                ["Text", "Number", "Boolean", "Json"]
            ),
        ]
    );

    public static readonly NodeDefinition JsonArrayLength = new(
        NodeType.JsonArrayLength,
        NodeCategory.Json,
        "JSON Array Length",
        "Returns the number of elements in a JSON array",
        [In("json", PinDataType.Json), Out("length", PinDataType.Number)],
        [new("path", VisualSqlArchitect.Nodes.ParameterKind.JsonPath, "$", "Path to the array")]
    );
}
