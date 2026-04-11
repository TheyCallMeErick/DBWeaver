using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class SqlImporterWiringTests
{
    [Fact]
    public async Task ImportAsync_WithJoinAndSimpleWhere_CreatesJoinNodeAndWiresWhereToResult()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT orders.id FROM orders INNER JOIN customers ON orders.customer_id = customers.id WHERE orders.id = 1";

        await canvas.SqlImporter.ImportAsync();

        NodeViewModel joinNode = canvas.Nodes.First(n => n.Type == NodeType.Join);
        Assert.Equal("INNER", joinNode.Parameters["join_type"]);
        Assert.Equal("public.customers", joinNode.Parameters["right_source"]);

        NodeViewModel resultNode = canvas.Nodes.First(n => n.Type == NodeType.ResultOutput);

        Assert.Contains(canvas.Connections, c =>
            c.FromPin.Name == "result"
            && c.ToPin?.Owner == resultNode
            && c.ToPin.Name == "where");
    }

    [Fact]
    public async Task ImportAsync_WithQuotedSchemaAndTableNames_CreatesJoinAndSecondSourceTable()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT \"employees\".* FROM \"main\".\"employees\" JOIN \"main\".\"departments\" ON \"main\".\"employees\".\"department_id\" = \"main\".\"departments\".\"id\"";

        await canvas.SqlImporter.ImportAsync();

        Assert.True(canvas.Nodes.Count(n => n.IsTableSource) >= 2);

        NodeViewModel joinNode = canvas.Nodes.First(n => n.Type == NodeType.Join);
        Assert.Equal("main.departments", joinNode.Parameters["right_source"]);
        Assert.Equal("main.employees.department_id", joinNode.Parameters["left_expr"]);
        Assert.Equal("main.departments.id", joinNode.Parameters["right_expr"]);

        NodeViewModel employees = canvas.Nodes.First(n =>
            n.IsTableSource && string.Equals(n.Subtitle, "main.employees", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("main.employees", employees.Parameters["table_full_name"]);
        Assert.Equal("employees", employees.Parameters["table"]);
        Assert.Equal("main.employees", employees.Parameters["source_table"]);
        Assert.Equal("main.employees", employees.Parameters["from_table"]);

        NodeViewModel columnSet = canvas.Nodes.First(n => n.Type == NodeType.ColumnSetBuilder);
        int employeesToColumnSetConnections = canvas.Connections.Count(c =>
            c.FromPin.Owner == employees
            && c.ToPin?.Owner == columnSet
            && c.ToPin.Name.Equals("columns", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, employeesToColumnSetConnections);
        Assert.Contains(canvas.Connections, c =>
            c.FromPin.Owner == employees
            && c.FromPin.Name.Equals("*", StringComparison.OrdinalIgnoreCase)
            && c.ToPin?.Owner == columnSet
            && c.ToPin.Name.Equals("columns", StringComparison.OrdinalIgnoreCase));
        NodeViewModel departments = canvas.Nodes.First(n =>
            n.IsTableSource && string.Equals(n.Subtitle, "main.departments", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(canvas.Connections, c =>
            c.FromPin.Owner == departments
            && c.ToPin?.Owner == columnSet
            && c.ToPin.Name.Equals("columns", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_WithMetadata_UsesRealColumnsAndEffectiveTableIdentity()
    {
        var canvas = new CanvasViewModel
        {
            DatabaseMetadata = BuildMainSchemaMetadata(),
        };

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT \"employees\".* FROM \"main\".\"employees\" JOIN \"main\".\"departments\" ON \"main\".\"employees\".\"department_id\" = \"main\".\"departments\".\"id\"";

        await canvas.SqlImporter.ImportAsync();

        NodeViewModel employees = canvas.Nodes.First(n =>
            n.IsTableSource && string.Equals(n.Subtitle, "main.employees", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(employees.OutputPins, pin => string.Equals(pin.Name, "id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(employees.OutputPins, pin => string.Equals(pin.Name, "department_id", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("main.employees", employees.Parameters["table_full_name"]);

        NodeViewModel joinNode = canvas.Nodes.First(n => n.Type == NodeType.Join);
        Assert.Equal("main.departments", joinNode.Parameters["right_source"]);
        Assert.Contains(canvas.Connections, c =>
            c.ToPin?.Owner == joinNode
            && c.ToPin.Name.Equals("left", StringComparison.OrdinalIgnoreCase)
            && c.FromPin.Name.Equals("department_id", StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.FromPin.Owner.Subtitle, "main.employees", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(canvas.Connections, c =>
            c.ToPin?.Owner == joinNode
            && c.ToPin.Name.Equals("right", StringComparison.OrdinalIgnoreCase)
            && c.FromPin.Name.Equals("id", StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.FromPin.Owner.Subtitle, "main.departments", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_WithJoinAliases_PrefersAliasOnJoinExpressionsAndRightSource()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT e.id FROM main.employees AS e JOIN main.departments d ON main.employees.department_id = main.departments.id";

        await canvas.SqlImporter.ImportAsync();

        NodeViewModel joinNode = canvas.Nodes.First(n => n.Type == NodeType.Join);
        Assert.Equal("d", joinNode.Parameters["right_source"]);
        Assert.Equal("e.department_id", joinNode.Parameters["left_expr"]);
        Assert.Equal("d.id", joinNode.Parameters["right_expr"]);

        NodeViewModel employees = canvas.Nodes.First(n =>
            n.IsTableSource && string.Equals(n.Subtitle, "main.employees", StringComparison.OrdinalIgnoreCase));
        NodeViewModel departments = canvas.Nodes.First(n =>
            n.IsTableSource && string.Equals(n.Subtitle, "main.departments", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("e", employees.Alias);
        Assert.Equal("d", departments.Alias);
    }

    [Fact]
    public async Task ImportAsync_WithUnqualifiedStarAndJoin_ProjectsWildcardFromAllSources()
    {
        var canvas = new CanvasViewModel
        {
            DatabaseMetadata = BuildMainSchemaMetadata(),
        };

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT * FROM main.employees e JOIN main.departments d ON e.department_id = d.id";

        await canvas.SqlImporter.ImportAsync();

        NodeViewModel columnSet = canvas.Nodes.First(n => n.Type == NodeType.ColumnSetBuilder);
        NodeViewModel employees = canvas.Nodes.First(n =>
            n.IsTableSource && string.Equals(n.Subtitle, "main.employees", StringComparison.OrdinalIgnoreCase));
        NodeViewModel departments = canvas.Nodes.First(n =>
            n.IsTableSource && string.Equals(n.Subtitle, "main.departments", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(canvas.Connections, c =>
            c.FromPin.Owner == employees
            && c.FromPin.Name.Equals("*", StringComparison.OrdinalIgnoreCase)
            && c.ToPin?.Owner == columnSet
            && c.ToPin.Name.Equals("columns", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(canvas.Connections, c =>
            c.FromPin.Owner == departments
            && c.FromPin.Name.Equals("*", StringComparison.OrdinalIgnoreCase)
            && c.ToPin?.Owner == columnSet
            && c.ToPin.Name.Equals("columns", StringComparison.OrdinalIgnoreCase));
    }

    private static DbMetadata BuildMainSchemaMetadata()
    {
        var employees = new TableMetadata(
            Schema: "main",
            Name: "employees",
            Kind: TableKind.Table,
            EstimatedRowCount: null,
            Columns:
            [
                new ColumnMetadata("id", "int", "int", false, true, false, true, true, 1),
                new ColumnMetadata("department_id", "int", "int", true, false, true, false, true, 2),
                new ColumnMetadata("name", "text", "text", true, false, false, false, false, 3),
            ],
            Indexes: [],
            OutboundForeignKeys: [],
            InboundForeignKeys: []);

        var departments = new TableMetadata(
            Schema: "main",
            Name: "departments",
            Kind: TableKind.Table,
            EstimatedRowCount: null,
            Columns:
            [
                new ColumnMetadata("id", "int", "int", false, true, false, true, true, 1),
                new ColumnMetadata("name", "text", "text", true, false, false, false, false, 2),
            ],
            Indexes: [],
            OutboundForeignKeys: [],
            InboundForeignKeys: []);

        return new DbMetadata(
            DatabaseName: "sample",
            Provider: DatabaseProvider.SQLite,
            ServerVersion: "test",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata("main", [employees, departments])],
            AllForeignKeys: []);
    }
}
