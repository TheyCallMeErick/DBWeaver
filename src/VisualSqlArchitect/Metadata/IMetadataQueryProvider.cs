using System.Data;
using VisualSqlArchitect.Core;

namespace VisualSqlArchitect.Metadata;

/// <summary>
/// Abstraction over provider-specific SQL queries for schema discovery.
/// Decouples metadata queries from orchestrator implementations,
/// enabling isolated unit testing without a real database.
/// </summary>
public interface IMetadataQueryProvider
{
    /// <summary>
    /// Returns SQL query to fetch all user tables from the database.
    /// Must return: TABLE_SCHEMA, TABLE_NAME (in that order)
    /// </summary>
    string GetTablesQuery();

    /// <summary>
    /// Returns SQL query to fetch all columns for a specific table.
    /// Must accept @schema and @table parameters and return:
    /// COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH, IS_PK, FK_TABLE
    /// </summary>
    string GetColumnsQuery();

    /// <summary>
    /// Returns SQL query to fetch primary key constraints.
    /// Must accept @schema and @table parameters and return:
    /// COLUMN_NAME, CONSTRAINT_NAME
    /// </summary>
    string GetPrimaryKeysQuery();

    /// <summary>
    /// Returns SQL query to fetch foreign key relationships.
    /// Must accept @schema and @table parameters and return:
    /// COLUMN_NAME, FK_TABLE_SCHEMA, FK_TABLE_NAME, FK_COLUMN_NAME
    /// </summary>
    string GetForeignKeysQuery();

    /// <summary>
    /// Parses a DataTable result from GetTablesQuery into schema.table tuples.
    /// </summary>
    IReadOnlyList<(string Schema, string Table)> ParseTables(DataTable dt);

    /// <summary>
    /// Parses a DataTable result from GetColumnsQuery into ColumnSchema objects.
    /// </summary>
    IReadOnlyList<ColumnSchema> ParseColumns(DataTable dt);
}
