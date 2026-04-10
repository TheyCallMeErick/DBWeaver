namespace Integration;

public class DdlProviderRollbackIntegrationTests
{
    [Theory]
    [InlineData(DatabaseProvider.SqlServer)]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SQLite)]
    public void DdlGraphCompileAndGenerate_CanBeWrappedInRollbackScript_ForProvider(DatabaseProvider provider)
    {
        NodeGraph graph = BuildSimpleCreateTableGraph();

        DdlCompileResult compile = new DdlGraphCompiler(graph, provider).CompileWithDiagnostics();
        Assert.False(compile.HasErrors);

        var generator = new DdlGeneratorService(provider);
        string ddlSql = generator.Generate(compile.Statements);
        string wrapped = WrapWithRollback(provider, ddlSql);

        Assert.Contains("CREATE TABLE", wrapped, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROLLBACK", wrapped, StringComparison.OrdinalIgnoreCase);
    }

    private static string WrapWithRollback(DatabaseProvider provider, string sql)
    {
        return provider switch
        {
            DatabaseProvider.SqlServer => $"BEGIN TRANSACTION;{Environment.NewLine}{sql}{Environment.NewLine}ROLLBACK TRANSACTION;",
            DatabaseProvider.Postgres => $"BEGIN;{Environment.NewLine}{sql}{Environment.NewLine}ROLLBACK;",
            DatabaseProvider.MySql => $"START TRANSACTION;{Environment.NewLine}{sql}{Environment.NewLine}ROLLBACK;",
            DatabaseProvider.SQLite => $"BEGIN TRANSACTION;{Environment.NewLine}{sql}{Environment.NewLine}ROLLBACK;",
            _ => sql,
        };
    }

    private static NodeGraph BuildSimpleCreateTableGraph()
    {
        var nodes = new List<NodeInstance>
        {
            new("out", NodeType.CreateTableOutput, new Dictionary<string, string>(), new Dictionary<string, string>()),
            new("tbl", NodeType.TableDefinition, new Dictionary<string, string>(), new Dictionary<string, string>
            {
                ["SchemaName"] = "public",
                ["TableName"] = "orders_rollback",
                ["IfNotExists"] = "true",
            }),
            new("col", NodeType.ColumnDefinition, new Dictionary<string, string>(), new Dictionary<string, string>
            {
                ["ColumnName"] = "id",
                ["DataType"] = "INT",
                ["IsNullable"] = "false",
            }),
        };

        var connections = new List<Connection>
        {
            new("tbl", "table", "out", "table"),
            new("col", "column", "tbl", "column"),
        };

        return new NodeGraph
        {
            Nodes = nodes,
            Connections = connections,
        };
    }
}
