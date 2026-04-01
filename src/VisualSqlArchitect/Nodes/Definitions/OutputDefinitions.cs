namespace VisualSqlArchitect.Nodes.Definitions;

using VisualSqlArchitect.Nodes;
using static NodeDefinitionHelpers;

/// <summary>
/// Output and export node definitions.
/// Defines nodes for generating final query results and exporting data.
/// </summary>
public static class OutputDefinitions
{
    public static readonly NodeDefinition ColumnList = new(
        NodeType.ColumnList,
        NodeCategory.Output,
        "Column List",
        "Aggregates multiple columns and defines their order",
        [
            Out(
                "result",
                PinDataType.ColumnSet,
                desc: "Connect to ResultOutput to define columns for SELECT"
            ),
        ],
        []
    );

    public static readonly NodeDefinition ColumnSetBuilder = new(
        NodeType.ColumnSetBuilder,
        NodeCategory.Output,
        "ColumnSet Builder",
        "Builds a structural ColumnSet from individual column references",
        [
            In(
                "columns",
                PinDataType.ColumnRef,
                required: false,
                multi: true,
                desc: "Connect columns or expressions to include in the set"
            ),
            Out(
                "result",
                PinDataType.ColumnSet,
                desc: "Connect to ResultOutput.columns to define SELECT columns"
            ),
        ],
        []
    );

    public static readonly NodeDefinition ColumnSetMerge = new(
        NodeType.ColumnSetMerge,
        NodeCategory.Output,
        "ColumnSet Merge",
        "Merges multiple ColumnSet inputs into a single output set",
        [
            In(
                "sets",
                PinDataType.ColumnSet,
                required: false,
                multi: true,
                desc: "Connect one or more ColumnSet outputs"
            ),
            Out("result", PinDataType.ColumnSet, desc: "Merged ColumnSet output"),
        ],
        []
    );

    public static readonly NodeDefinition ResultOutput = new(
        NodeType.ResultOutput,
        NodeCategory.Output,
        "Result Output",
        "Defines the final SELECT output",
        [
            In(
                "top",
                PinDataType.ColumnSet,
                required: false,
                desc: "Connect a TOP / LIMIT node to restrict the number of rows"
            ),
            In(
                "where",
                PinDataType.Boolean,
                required: false,
                desc: "Connect a compiled WHERE condition"
            ),
            In(
                "columns",
                PinDataType.ColumnSet,
                required: false,
                desc: "Connect ColumnList/ColumnSetBuilder output to include columns in SELECT"
            ),
            In(
                "column",
                PinDataType.ColumnRef,
                required: false,
                multi: true,
                desc: "Connect individual columns directly (without ColumnList)"
            ),
            Out(
                "result",
                PinDataType.ColumnSet,
                desc: "Connect to an Export node to generate an output file"
            ),
        ],
        [
            new(
                "file_name",
                VisualSqlArchitect.Nodes.ParameterKind.Text,
                "export.html",
                "Destination file name or path (e.g. report.html)"
            ),
        ]
    );

    public static readonly NodeDefinition JsonExport = new(
        NodeType.JsonExport,
        NodeCategory.Output,
        "JSON Export",
        "Exports the result schema as a JSON template file",
        [In("query", PinDataType.ColumnSet, required: true, desc: "Connect from a Result Output node")],
        [
            new(
                "file_name",
                VisualSqlArchitect.Nodes.ParameterKind.Text,
                "export.json",
                "Destination file name or path (e.g. data.json)"
            ),
        ]
    );

    public static readonly NodeDefinition CsvExport = new(
        NodeType.CsvExport,
        NodeCategory.Output,
        "CSV Export",
        "Exports the result schema as a CSV file with a header row",
        [In("query", PinDataType.ColumnSet, required: true, desc: "Connect from a Result Output node")],
        [
            new(
                "file_name",
                VisualSqlArchitect.Nodes.ParameterKind.Text,
                "export.csv",
                "Destination file name or path (e.g. data.csv)"
            ),
            new(
                "delimiter",
                VisualSqlArchitect.Nodes.ParameterKind.Enum,
                ",",
                "Column delimiter",
                [",", ";", "\\t", "|"]
            ),
        ]
    );

    public static readonly NodeDefinition ExcelExport = new(
        NodeType.ExcelExport,
        NodeCategory.Output,
        "Excel Export (XLSX)",
        "Exports the result schema as an Excel workbook with a header row",
        [In("query", PinDataType.ColumnSet, required: true, desc: "Connect from a Result Output node")],
        [
            new(
                "file_name",
                VisualSqlArchitect.Nodes.ParameterKind.Text,
                "export.xlsx",
                "Destination file name or path (e.g. report.xlsx)"
            ),
            new(
                "sheet_name",
                VisualSqlArchitect.Nodes.ParameterKind.Text,
                "Sheet1",
                "Name of the first worksheet (e.g. Results)"
            ),
        ]
    );
}
