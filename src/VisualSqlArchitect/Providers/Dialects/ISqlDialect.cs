namespace VisualSqlArchitect.Providers.Dialects;

/// <summary>
/// Abstração de dialeto SQL específico de cada provider.
/// Centraliza queries de metadata e operações de dialeto específicas.
/// </summary>
public interface ISqlDialect
{
    #region Schema Discovery Queries

    /// <summary>
    /// Query para listar todas as tabelas/views do banco.
    /// Deve retornar: TABLE_SCHEMA, TABLE_NAME
    /// </summary>
    string GetTablesQuery();

    /// <summary>
    /// Query para listar colunas de uma tabela específica.
    /// Parâmetros: @schema, @table
    /// Deve retornar: COLUMN_NAME, DATA_TYPE, IS_NULLABLE, IS_PRIMARY_KEY
    /// </summary>
    string GetColumnsQuery();

    /// <summary>
    /// Query para descobrir chaves primárias.
    /// Parâmetros: @schema, @table
    /// Deve retornar: COLUMN_NAME
    /// </summary>
    string GetPrimaryKeysQuery();

    /// <summary>
    /// Query para descobrir chaves estrangeiras.
    /// Parâmetros: @schema, @table
    /// Deve retornar: COLUMN_NAME, REFERENCED_TABLE, REFERENCED_COLUMN
    /// </summary>
    string GetForeignKeysQuery();

    #endregion

    #region Query Wrapping

    /// <summary>
    /// Envolve uma query em SELECT TOP N (SQL Server style).
    /// Ex: "SELECT TOP 100 * FROM (SELECT * FROM users WHERE ...) AS __preview"
    /// </summary>
    string WrapWithPreviewLimit(string baseQuery, int maxRows);

    /// <summary>
    /// Obtém a sintaxe de LIMIT/OFFSET específica do dialeto.
    /// Ex SQL Server: "OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY"
    /// Ex MySQL: "LIMIT 20 OFFSET 10"
    /// </summary>
    string FormatPagination(int? limit, int? offset);

    #endregion

    #region Identifier Quoting

    /// <summary>
    /// Quoteia um identificador (table name, column name) segundo dialeto.
    /// Ex SQL Server: [tableName]
    /// Ex MySQL: `tableName`
    /// Ex Postgres: "tableName"
    /// </summary>
    string QuoteIdentifier(string identifier);

    #endregion
}
