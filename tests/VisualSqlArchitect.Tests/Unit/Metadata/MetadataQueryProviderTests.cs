using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;

namespace VisualSqlArchitect.Tests.Unit.Metadata;

/// <summary>
/// Tests for IMetadataQueryProvider implementations.
/// Validates that each provider generates correct schema discovery queries.
/// </summary>
public class MetadataQueryProviderTests
{
    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void GetTablesQuery_ProducesNonEmptySql(DatabaseProvider provider)
    {
        // Arrange
        var metadataProvider = CreateMetadataProvider(provider);

        // Act
        string query = metadataProvider.GetTablesQuery();

        // Assert
        Assert.NotEmpty(query);
        Assert.Contains("SELECT", query.ToUpper());
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void GetColumnsQuery_ProducesNonEmptySql(DatabaseProvider provider)
    {
        // Arrange
        var metadataProvider = CreateMetadataProvider(provider);

        // Act
        string query = metadataProvider.GetColumnsQuery();

        // Assert
        Assert.NotEmpty(query);
        Assert.Contains("SELECT", query.ToUpper());
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void GetPrimaryKeysQuery_ProducesNonEmptySql(DatabaseProvider provider)
    {
        // Arrange
        var metadataProvider = CreateMetadataProvider(provider);

        // Act
        string query = metadataProvider.GetPrimaryKeysQuery();

        // Assert
        Assert.NotEmpty(query);
        Assert.Contains("SELECT", query.ToUpper());
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void GetForeignKeysQuery_ProducesNonEmptySql(DatabaseProvider provider)
    {
        // Arrange
        var metadataProvider = CreateMetadataProvider(provider);

        // Act
        string query = metadataProvider.GetForeignKeysQuery();

        // Assert
        Assert.NotEmpty(query);
        Assert.Contains("SELECT", query.ToUpper());
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void ParseTables_ReturnsEmptyCollectionForEmptyResult(DatabaseProvider provider)
    {
        // Arrange
        var metadataProvider = CreateMetadataProvider(provider);
        var emptyDataTable = new System.Data.DataTable();
        emptyDataTable.Columns.Add("TABLE_SCHEMA");
        emptyDataTable.Columns.Add("TABLE_NAME");

        // Act
        var tables = metadataProvider.ParseTables(emptyDataTable);

        // Assert
        Assert.Empty(tables);
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void ParseColumns_ReturnsEmptyCollectionForEmptyResult(DatabaseProvider provider)
    {
        // Arrange
        var metadataProvider = CreateMetadataProvider(provider);
        var emptyDataTable = new System.Data.DataTable();
        emptyDataTable.Columns.Add("TABLE_SCHEMA");
        emptyDataTable.Columns.Add("TABLE_NAME");
        emptyDataTable.Columns.Add("COLUMN_NAME");
        emptyDataTable.Columns.Add("DATA_TYPE");

        // Act
        var columns = metadataProvider.ParseColumns(emptyDataTable);

        // Assert
        Assert.Empty(columns);
    }

    [Fact]
    public void PostgresMetadataQueries_SpecificBehaviors()
    {
        // Arrange
        var metadataProvider = new PostgresMetadataQueries();

        // Act
        string tablesQuery = metadataProvider.GetTablesQuery();
        string columnsQuery = metadataProvider.GetColumnsQuery();

        // Assert - Postgres uses information_schema
        Assert.Contains("information_schema", tablesQuery.ToLower());
        Assert.Contains("information_schema", columnsQuery.ToLower());
    }

    [Fact]
    public void MySqlMetadataQueries_SpecificBehaviors()
    {
        // Arrange
        var metadataProvider = new MySqlMetadataQueries();

        // Act
        string tablesQuery = metadataProvider.GetTablesQuery();
        string columnsQuery = metadataProvider.GetColumnsQuery();

        // Assert - MySQL uses information_schema
        Assert.Contains("information_schema", tablesQuery.ToLower());
        Assert.Contains("information_schema", columnsQuery.ToLower());
    }

    [Fact]
    public void SqlServerMetadataQueries_SpecificBehaviors()
    {
        // Arrange
        var metadataProvider = new SqlServerMetadataQueries();

        // Act
        string tablesQuery = metadataProvider.GetTablesQuery();
        string columnsQuery = metadataProvider.GetColumnsQuery();

        // Assert - SQL Server uses information_schema or sys views
        Assert.NotEmpty(tablesQuery);
        Assert.NotEmpty(columnsQuery);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static IMetadataQueryProvider CreateMetadataProvider(DatabaseProvider provider) =>
        provider switch
        {
            DatabaseProvider.Postgres => new PostgresMetadataQueries(),
            DatabaseProvider.MySql => new MySqlMetadataQueries(),
            DatabaseProvider.SqlServer => new SqlServerMetadataQueries(),
            _ => throw new NotSupportedException($"Provider {provider} not supported in tests")
        };
}
