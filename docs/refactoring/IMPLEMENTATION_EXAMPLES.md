# Guia de Implementação - Exemplos Práticos

**Complemento ao:** REFACTORING_ROADMAP.md
**Data:** 26 de março de 2026

---

## 1. Implementando ISqlDialect (Eixo 1.1)

### Passo 1: Definir a Interface

```csharp
// src/VisualSqlArchitect/Providers/Dialects/ISqlDialect.cs

namespace VisualSqlArchitect.Providers.Dialects;

/// <summary>
/// Abstração de dialeto SQL específico de cada provider.
/// Reutiliza queries de metadata e wrapping de preview.
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
```

### Passo 2: Implementar para PostgreSQL

```csharp
// src/VisualSqlArchitect/Providers/Dialects/PostgresDialect.cs

namespace VisualSqlArchitect.Providers.Dialects;

public sealed class PostgresDialect : ISqlDialect
{
    public string GetTablesQuery() => @"
        SELECT
            table_schema,
            table_name
        FROM
            information_schema.tables
        WHERE
            table_type = 'BASE TABLE'
            AND table_schema NOT IN ('pg_catalog', 'information_schema')
        ORDER BY
            table_schema, table_name
    ";

    public string GetColumnsQuery() => @"
        SELECT
            column_name,
            udt_name AS data_type,
            is_nullable::boolean,
            (column_name IN (
                SELECT a.attname
                FROM pg_index i
                JOIN pg_attribute a ON a.attrelid = i.indrelid
                    AND a.attnum = ANY(i.indkey)
                WHERE i.indisprimary
                    AND i.indrelid = (@schema || '.' || @table)::regclass
            ))::boolean AS is_primary_key
        FROM
            information_schema.columns
        WHERE
            table_schema = @schema
            AND table_name = @table
        ORDER BY
            ordinal_position
    ";

    public string GetPrimaryKeysQuery() => @"
        SELECT
            a.attname AS column_name
        FROM
            pg_index i
            JOIN pg_attribute a ON a.attrelid = i.indrelid
                AND a.attnum = ANY(i.indkey)
        WHERE
            i.indisprimary
            AND i.indrelid = (@schema || '.' || @table)::regclass
    ";

    public string GetForeignKeysQuery() => @"
        SELECT
            kcu.column_name,
            ccu.table_name AS referenced_table,
            ccu.column_name AS referenced_column
        FROM
            information_schema.table_constraints AS tc
            JOIN information_schema.key_column_usage AS kcu
                ON tc.constraint_name = kcu.constraint_name
                AND tc.table_schema = kcu.table_schema
            JOIN information_schema.constraint_column_usage AS ccu
                ON ccu.constraint_name = tc.constraint_name
                AND ccu.table_schema = tc.table_schema
        WHERE
            tc.constraint_type = 'FOREIGN KEY'
            AND tc.table_schema = @schema
            AND tc.table_name = @table
    ";

    public string WrapWithPreviewLimit(string baseQuery, int maxRows) =>
        $"{baseQuery} LIMIT {maxRows}";

    public string FormatPagination(int? limit, int? offset)
    {
        var parts = new List<string>();
        if (limit.HasValue)
            parts.Add($"LIMIT {limit.Value}");
        if (offset.HasValue)
            parts.Add($"OFFSET {offset.Value}");
        return string.Join(" ", parts);
    }

    public string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"")}\"";
}
```

### Passo 3: Implementar para SQL Server

```csharp
// src/VisualSqlArchitect/Providers/Dialects/SqlServerDialect.cs

public sealed class SqlServerDialect : ISqlDialect
{
    public string GetTablesQuery() => @"
        SELECT
            TABLE_SCHEMA,
            TABLE_NAME
        FROM
            INFORMATION_SCHEMA.TABLES
        WHERE
            TABLE_TYPE = 'BASE TABLE'
        ORDER BY
            TABLE_SCHEMA, TABLE_NAME
    ";

    public string GetColumnsQuery() => @"
        SELECT
            COLUMN_NAME,
            DATA_TYPE,
            CASE WHEN IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS IS_NULLABLE,
            CASE WHEN COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 1
                 THEN 1 ELSE 0 END AS IS_PRIMARY_KEY
        FROM
            INFORMATION_SCHEMA.COLUMNS
        WHERE
            TABLE_SCHEMA = @schema
            AND TABLE_NAME = @table
        ORDER BY
            ORDINAL_POSITION
    ";

    public string GetPrimaryKeysQuery() => @"
        SELECT
            COLUMN_NAME
        FROM
            INFORMATION_SCHEMA.KEY_COLUMN_USAGE
        WHERE
            TABLE_SCHEMA = @schema
            AND TABLE_NAME = @table
            AND CONSTRAINT_NAME LIKE 'PK%'
    ";

    public string GetForeignKeysQuery() => @"
        SELECT
            KCU1.COLUMN_NAME,
            KCU2.TABLE_NAME AS REFERENCED_TABLE,
            KCU2.COLUMN_NAME AS REFERENCED_COLUMN
        FROM
            INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS RC
            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU1
                ON RC.CONSTRAINT_NAME = KCU1.CONSTRAINT_NAME
            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU2
                ON RC.UNIQUE_CONSTRAINT_NAME = KCU2.CONSTRAINT_NAME
        WHERE
            KCU1.TABLE_SCHEMA = @schema
            AND KCU1.TABLE_NAME = @table
    ";

    public string WrapWithPreviewLimit(string baseQuery, int maxRows) =>
        $"SELECT TOP {maxRows} * FROM ({baseQuery}) AS __preview";

    public string FormatPagination(int? limit, int? offset)
    {
        if (!offset.HasValue)
            return limit.HasValue ? $"OFFSET 0 ROWS FETCH NEXT {limit} ROWS ONLY" : "";

        var parts = new List<string> { $"OFFSET {offset} ROWS" };
        if (limit.HasValue)
            parts.Add($"FETCH NEXT {limit} ROWS ONLY");

        return string.Join(" ", parts);
    }

    public string QuoteIdentifier(string identifier) =>
        $"[{identifier.Replace("]", "]]")}]";
}
```

### Passo 4: Refatorar BaseDbOrchestrator

```csharp
// src/VisualSqlArchitect/Core/BaseDbOrchestrator.cs (REFATORADO)

using System.Data;
using System.Data.Common;
using VisualSqlArchitect.Providers.Dialects;

namespace VisualSqlArchitect.Core;

public abstract class BaseDbOrchestrator : IDbOrchestrator
{
    protected ConnectionConfig Config { get; }
    protected abstract ISqlDialect Dialect { get; }

    protected BaseDbOrchestrator(ConnectionConfig config)
    {
        Config = config;
    }

    public abstract DatabaseProvider Provider { get; }

    public async Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(ct);

        var tables = await FetchTablesWithColumnsAsync(conn, ct);

        return new DatabaseSchema(
            DatabaseName: Config.Database,
            Provider: Provider,
            Tables: tables.AsReadOnly()
        );
    }

    public async Task<PreviewResult> ExecutePreviewAsync(
        CompiledQuery query,
        CancellationToken ct = default)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            await using var conn = await OpenConnectionAsync(ct);

            // Wrap query com LIMIT
            string wrappedSql = Dialect.WrapWithPreviewLimit(query.Sql, maxRows: 100);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = wrappedSql;
            cmd.CommandTimeout = Config.TimeoutSeconds;

            // Bind parâmetros
            foreach (var binding in query.Bindings)
            {
                var param = cmd.CreateParameter();
                param.ParameterName = binding.Key;
                param.Value = binding.Value ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }

            var dt = new DataTable();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            dt.Load(reader);

            sw.Stop();

            return new PreviewResult(
                Success: true,
                Data: dt,
                ExecutionTime: sw.Elapsed
            );
        }
        catch (Exception ex)
        {
            return new PreviewResult(
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }

    // ── Métodos abstratos ──

    protected abstract Task<DbConnection> OpenConnectionAsync(CancellationToken ct);

    // ── Métodos que usam Dialect ──

    private async Task<List<TableSchema>> FetchTablesWithColumnsAsync(
        DbConnection conn,
        CancellationToken ct)
    {
        // Usar Dialect.GetTablesQuery()
        string tablesQuery = Dialect.GetTablesQuery();

        var tables = new List<TableSchema>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = tablesQuery;

        var dt = new DataTable();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        dt.Load(reader);

        foreach (DataRow row in dt.Rows)
        {
            string schema = row["TABLE_SCHEMA"].ToString() ?? "";
            string tableName = row["TABLE_NAME"].ToString() ?? "";

            var columns = await FetchColumnsForTableAsync(conn, schema, tableName, ct);
            tables.Add(new TableSchema(schema, tableName, columns));
        }

        return tables;
    }

    private async Task<IReadOnlyList<ColumnSchema>> FetchColumnsForTableAsync(
        DbConnection conn,
        string schema,
        string table,
        CancellationToken ct)
    {
        // Usar Dialect.GetColumnsQuery()
        string columnsQuery = Dialect.GetColumnsQuery();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = columnsQuery;

        // Parameters para @schema, @table
        var schemaParam = cmd.CreateParameter();
        schemaParam.ParameterName = "@schema";
        schemaParam.Value = schema;
        cmd.Parameters.Add(schemaParam);

        var tableParam = cmd.CreateParameter();
        tableParam.ParameterName = "@table";
        tableParam.Value = table;
        cmd.Parameters.Add(tableParam);

        var columns = new List<ColumnSchema>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            columns.Add(new ColumnSchema(
                Name: reader["COLUMN_NAME"].ToString() ?? "",
                DataType: reader["DATA_TYPE"].ToString() ?? "",
                IsNullable: Convert.ToBoolean(reader["IS_NULLABLE"]),
                IsPrimaryKey: Convert.ToBoolean(reader["IS_PRIMARY_KEY"])
            ));
        }

        return columns;
    }
}
```

---

## 2. Criar IQueryBuilder Abstraction (Eixo 3.1)

### Interface

```csharp
// src/VisualSqlArchitect/QueryEngine/Builders/IQueryBuilder.cs

namespace VisualSqlArchitect.QueryEngine.Builders;

/// <summary>
/// Abstração agnóstica de query builder, independente de SqlKata.
/// Permite trocar implementação futuramente.
/// </summary>
public interface IQueryBuilder
{
    /// <summary>
    /// Define as colunas a selecionar.
    /// </summary>
    IQueryBuilder Select(IEnumerable<SelectColumn> columns);

    /// <summary>
    /// Adiciona uma tabela/join à query.
    /// </summary>
    IQueryBuilder From(string table);

    /// <summary>
    /// Adiciona um join.
    /// </summary>
    IQueryBuilder Join(JoinDefinition join);

    /// <summary>
    /// Adiciona um filtro WHERE.
    /// </summary>
    IQueryBuilder Where(FilterDefinition filter);

    /// <summary>
    /// Define GROUP BY.
    /// </summary>
    IQueryBuilder GroupBy(IEnumerable<string> columns);

    /// <summary>
    /// Define ORDER BY.
    /// </summary>
    IQueryBuilder OrderBy(IEnumerable<OrderDefinition> orders);

    /// <summary>
    /// Aplica LIMIT e OFFSET (dialeto-específico).
    /// </summary>
    IQueryBuilder Paginate(int? limit, int? offset);

    /// <summary>
    /// Compila para SQL string + bindings.
    /// </summary>
    CompiledQuery Build();
}
```

### Implementação com SqlKata

```csharp
// src/VisualSqlArchitect/QueryEngine/Builders/SqlKataQueryBuilder.cs

using SqlKata;
using SqlKata.Compilers;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect.QueryEngine.Builders;

public sealed class SqlKataQueryBuilder : IQueryBuilder
{
    private Query _query;
    private readonly Compiler _compiler;
    private readonly ISqlFunctionRegistry _functionRegistry;
    private readonly DatabaseProvider _provider;

    public SqlKataQueryBuilder(
        DatabaseProvider provider,
        ISqlFunctionRegistry functionRegistry)
    {
        _provider = provider;
        _functionRegistry = functionRegistry;
        _compiler = provider switch
        {
            DatabaseProvider.SqlServer => new SqlServerCompiler(),
            DatabaseProvider.MySql => new MySqlCompiler(),
            DatabaseProvider.Postgres => new PostgresCompiler(),
            _ => throw new NotSupportedException($"Provider {provider} not supported")
        };

        _query = new Query();
    }

    public IQueryBuilder From(string table)
    {
        _query = _query.From(table);
        return this;
    }

    public IQueryBuilder Select(IEnumerable<SelectColumn> columns)
    {
        foreach (var col in columns)
        {
            if (string.IsNullOrEmpty(col.Alias))
                _query = _query.SelectRaw(col.Expression);
            else
                _query = _query.SelectRaw(col.Expression, col.Alias);
        }
        return this;
    }

    public IQueryBuilder Join(JoinDefinition join)
    {
        // Simplificado; expand conforme necessário
        string joinType = (join.Type?.ToUpper() ?? "INNER").Trim();

        switch (joinType)
        {
            case "LEFT":
                _query = _query.LeftJoin(
                    join.TargetTable,
                    $"{join.TargetTable}.{join.RightColumn}",
                    "=",
                    $"_table0_.{join.LeftColumn}"
                );
                break;
            case "RIGHT":
                // SqlKata não suporta RIGHT JOIN nativamente; usar LEFT com tabelas trocadas
                _query = _query.LeftJoin(
                    _query.From,
                    $"_table0_.{join.LeftColumn}",
                    "=",
                    $"{join.TargetTable}.{join.RightColumn}"
                );
                break;
            default: // INNER
                _query = _query.Join(
                    join.TargetTable,
                    $"{join.TargetTable}.{join.RightColumn}",
                    "=",
                    $"_table0_.{join.LeftColumn}"
                );
                break;
        }

        return this;
    }

    public IQueryBuilder Where(FilterDefinition filter)
    {
        // Se é function fragment (Regex, JSON, etc), resolver via registry
        if (!string.IsNullOrEmpty(filter.CanonicalFn))
        {
            string fragment = _functionRegistry.GetFunction(
                filter.CanonicalFn,
                filter.Column,
                filter.Value?.ToString() ?? ""
            );
            _query = _query.WhereRaw(fragment);
        }
        else
        {
            // Operador SQL padrão
            _query = _query.Where(
                filter.Column,
                filter.Operator,
                filter.Value
            );
        }

        return this;
    }

    public IQueryBuilder GroupBy(IEnumerable<string> columns)
    {
        foreach (var col in columns)
            _query = _query.GroupBy(col);
        return this;
    }

    public IQueryBuilder OrderBy(IEnumerable<OrderDefinition> orders)
    {
        foreach (var order in orders)
        {
            if (order.Descending)
                _query = _query.OrderByDesc(order.Column);
            else
                _query = _query.OrderBy(order.Column);
        }
        return this;
    }

    public IQueryBuilder Paginate(int? limit, int? offset)
    {
        if (offset.HasValue)
            _query = _query.Offset(offset.Value);

        if (limit.HasValue)
            _query = _query.Limit(limit.Value);

        return this;
    }

    public CompiledQuery Build()
    {
        var result = _compiler.Compile(_query);

        return new CompiledQuery(
            Sql: result.ToString(),
            Bindings: result.Bindings?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value as object
            ) ?? new Dictionary<string, object?>()
        );
    }
}
```

---

## 3. Implementar Testes Parametrizados (Eixo 4.1)

### Test Fixture

```csharp
// tests/VisualSqlArchitect.Tests/Unit/Providers/ProviderTestFixture.cs

using Testcontainers.MsSql;
using Testcontainers.MySql;
using Testcontainers.PostgreSql;
using VisualSqlArchitect.Core;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Providers;

public sealed class ProviderTestFixture : IAsyncLifetime
{
    private readonly DatabaseProvider _provider;
    private Container? _container;

    public ConnectionConfig Config { get; private set; } = null!;
    public IDbOrchestrator Orchestrator { get; private set; } = null!;

    public ProviderTestFixture(DatabaseProvider provider)
    {
        _provider = provider;
    }

    public async Task InitializeAsync()
    {
        _container = _provider switch
        {
            DatabaseProvider.Postgres => await InitPostgresAsync(),
            DatabaseProvider.MySql => await InitMySqlAsync(),
            DatabaseProvider.SqlServer => await InitSqlServerAsync(),
            _ => throw new NotSupportedException()
        };

        Orchestrator = DbOrchestratorFactory.Create(Config);

        // Seed sample data
        await SeedSampleDataAsync();
    }

    private async Task<Container> InitPostgresAsync()
    {
        var container = new PostgreSqlBuilder()
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        await container.StartAsync();

        Config = new ConnectionConfig(
            Provider: DatabaseProvider.Postgres,
            Host: container.Hostname,
            Port: container.GetMappedPublicPort(5432),
            Database: "testdb",
            Username: "testuser",
            Password: "testpass"
        );

        return container;
    }

    private async Task<Container> InitMySqlAsync()
    {
        var container = new MySqlBuilder()
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        await container.StartAsync();

        Config = new ConnectionConfig(
            Provider: DatabaseProvider.MySql,
            Host: container.Hostname,
            Port: container.GetMappedPublicPort(3306),
            Database: "testdb",
            Username: "testuser",
            Password: "testpass"
        );

        return container;
    }

    private async Task<Container> InitSqlServerAsync()
    {
        var container = new MsSqlBuilder()
            .WithPassword("P@ssw0rd123!")
            .Build();

        await container.StartAsync();

        Config = new ConnectionConfig(
            Provider: DatabaseProvider.SqlServer,
            Host: container.Hostname,
            Port: container.GetMappedPublicPort(1433),
            Database: "master",
            Username: "sa",
            Password: "P@ssw0rd123!",
            UseIntegratedSecurity: false
        );

        return container;
    }

    private async Task SeedSampleDataAsync()
    {
        // Criar tabelas e dados para testes
        await using var conn = await Orchestrator.OpenConnectionAsync(CancellationToken.None);

        // Seed SQL (provider-specific)
        string seedSql = _provider switch
        {
            DatabaseProvider.Postgres => SeedPostgres(),
            DatabaseProvider.MySql => SeedMySql(),
            DatabaseProvider.SqlServer => SeedSqlServer(),
            _ => throw new NotSupportedException()
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = seedSql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static string SeedPostgres() => @"
        CREATE TABLE IF NOT EXISTS users (
            id SERIAL PRIMARY KEY,
            name VARCHAR(100),
            email VARCHAR(100)
        );

        INSERT INTO users (name, email) VALUES
            ('Alice', 'alice@example.com'),
            ('Bob', 'bob@corp.io'),
            ('Charlie', 'charlie@example.com');
    ";

    private static string SeedMySql() => @"
        CREATE TABLE IF NOT EXISTS users (
            id INT AUTO_INCREMENT PRIMARY KEY,
            name VARCHAR(100),
            email VARCHAR(100)
        );

        INSERT INTO users (name, email) VALUES
            ('Alice', 'alice@example.com'),
            ('Bob', 'bob@corp.io'),
            ('Charlie', 'charlie@example.com');
    ";

    private static string SeedSqlServer() => @"
        CREATE TABLE users (
            id INT IDENTITY(1,1) PRIMARY KEY,
            name VARCHAR(100),
            email VARCHAR(100)
        );

        INSERT INTO users (name, email) VALUES
            ('Alice', 'alice@example.com'),
            ('Bob', 'bob@corp.io'),
            ('Charlie', 'charlie@example.com');
    ";

    public async Task DisposeAsync()
    {
        if (_container != null)
            await _container.StopAsync();
    }
}

public sealed class ProviderTestFixtureCollection : ICollectionFixture<ProviderTestFixture>
{
    // Collection definition for xUnit
}
```

### Tests Parametrizados

```csharp
// tests/VisualSqlArchitect.Tests/Unit/Providers/ProviderSchemaDiscoveryTests.cs

using VisualSqlArchitect.Core;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Providers;

public class ProviderSchemaDiscoveryTests
{
    private readonly ProviderTestFixture _fixture;

    public ProviderSchemaDiscoveryTests(ProviderTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetSchema_ReturnsAllTables()
    {
        // Arrange
        var orchestrator = _fixture.Orchestrator;

        // Act
        var schema = await orchestrator.GetSchemaAsync();

        // Assert
        Assert.NotEmpty(schema.Tables);
        Assert.Contains(schema.Tables, t => t.Name == "users");
    }

    [Fact]
    public async Task GetSchema_UserTable_HasCorrectColumns()
    {
        // Arrange
        var orchestrator = _fixture.Orchestrator;

        // Act
        var schema = await orchestrator.GetSchemaAsync();
        var usersTable = schema.Tables.First(t => t.Name == "users");

        // Assert
        Assert.Contains(usersTable.Columns, c => c.Name == "id");
        Assert.Contains(usersTable.Columns, c => c.Name == "name");
        Assert.Contains(usersTable.Columns, c => c.Name == "email");
    }

    [Fact]
    public async Task GetSchema_IdColumn_IsPrimaryKey()
    {
        // Arrange
        var orchestrator = _fixture.Orchestrator;

        // Act
        var schema = await orchestrator.GetSchemaAsync();
        var idColumn = schema.Tables
            .First(t => t.Name == "users")
            .Columns
            .First(c => c.Name == "id");

        // Assert
        Assert.True(idColumn.IsPrimaryKey);
    }
}

// Usage com múltiplos providers
[Theory]
[InlineData(DatabaseProvider.Postgres)]
[InlineData(DatabaseProvider.MySql)]
[InlineData(DatabaseProvider.SqlServer)]
public async Task SchemaDiscovery_AllProviders_Successful(
    DatabaseProvider provider)
{
    using var fixture = new ProviderTestFixture(provider);
    await fixture.InitializeAsync();

    var schema = await fixture.Orchestrator.GetSchemaAsync();
    Assert.NotEmpty(schema.Tables);

    await fixture.DisposeAsync();
}
```

---

## 4. Exception Handling Estruturado (Eixo 7.1)

### Hierarchy

```csharp
// src/VisualSqlArchitect/Exceptions/VisualSqlArchitectException.cs

namespace VisualSqlArchitect.Exceptions;

/// <summary>
/// Base para todas as exceções do Visual SQL Architect.
/// </summary>
public abstract class VisualSqlArchitectException : Exception
{
    protected VisualSqlArchitectException(string message, Exception? innerException = null)
        : base(message, innerException) { }

    /// <summary>
    /// Mensagem amigável para apresentar ao usuário.
    /// </summary>
    public virtual string UserFacingMessage => Message;

    /// <summary>
    /// Identifica se o erro é transiente (pode ser retentado).
    /// </summary>
    public virtual bool IsTransient => false;
}

/// <summary>
/// Falha ao conectar ao banco de dados.
/// </summary>
public sealed class ConnectionFailedException : VisualSqlArchitectException
{
    public string? ConnectionString { get; set; }
    public TimeSpan? Latency { get; set; }
    public DatabaseProvider? Provider { get; set; }

    public ConnectionFailedException(string message, Exception? innerException = null)
        : base(message, innerException) { }

    public override string UserFacingMessage =>
        $"Não foi possível conectar ao banco de dados. " +
        $"Verifique host, porta, credenciais e firewall. " +
        $"Latência: {Latency?.TotalSeconds:F2}s";

    public override bool IsTransient =>
        InnerException is TimeoutException or
                         OperationCanceledException;
}

/// <summary>
/// Falha ao introspectar schema do banco.
/// </summary>
public sealed class SchemaIntrospectionException : VisualSqlArchitectException
{
    public string? TableName { get; set; }
    public DatabaseProvider? Provider { get; set; }

    public SchemaIntrospectionException(string message, Exception? innerException = null)
        : base(message, innerException) { }

    public override string UserFacingMessage =>
        $"Erro ao ler schema do banco de dados. " +
        $"Tabela: {TableName}. " +
        $"Verifique permissões de acesso.";
}

/// <summary>
/// Falha ao compilar query visual para SQL.
/// </summary>
public sealed class QueryCompilationException : VisualSqlArchitectException
{
    public VisualQuerySpec? QuerySpec { get; set; }
    public string? PartialSql { get; set; }

    public QueryCompilationException(string message, Exception? innerException = null)
        : base(message, innerException) { }

    public override string UserFacingMessage =>
        $"Erro ao gerar SQL da query visual. Verifique configurações de filtros, joins e colunas.";
}

/// <summary>
/// Falha ao executar preview da query.
/// </summary>
public sealed class QueryExecutionException : VisualSqlArchitectException
{
    public CompiledQuery? Query { get; set; }
    public int RetryCount { get; set; }

    public QueryExecutionException(string message, Exception? innerException = null)
        : base(message, innerException) { }

    public override string UserFacingMessage =>
        $"Erro ao executar preview da query. Sintaxe SQL inválida ou timeout. " +
        $"Tentativas: {RetryCount}";

    public override bool IsTransient =>
        InnerException is TimeoutException or
                         OperationCanceledException;
}
```

### Resilience com Polly

```csharp
// src/VisualSqlArchitect/Core/ResilientDbOrchestrator.cs

using Polly;
using Polly.CircuitBreaker;
using VisualSqlArchitect.Exceptions;

namespace VisualSqlArchitect.Core;

/// <summary>
/// Decorator para IDbOrchestrator que adiciona retry logic e circuit breaker.
/// </summary>
public sealed class ResilientDbOrchestrator : IDbOrchestrator, IAsyncDisposable
{
    private readonly IDbOrchestrator _inner;
    private readonly IAsyncPolicy<PreviewResult> _previewPolicy;
    private readonly IAsyncPolicy _schemaPolicy;

    public DatabaseProvider Provider => _inner.Provider;

    public ResilientDbOrchestrator(IDbOrchestrator inner)
    {
        _inner = inner;

        // Policy para preview: retry em transientes, circuit breaker em falhas contínuas
        _previewPolicy = Policy<PreviewResult>
            .Handle<QueryExecutionException>(ex => ex.IsTransient)
            .Or<TimeoutException>()
            .Or<OperationCanceledException>()
            .OrResult(r => !r.Success && r.ErrorMessage?.Contains("timeout") == true)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100),
                onRetry: (outcome, duration, retryCount, ctx) =>
                    LogRetry($"ExecutePreviewAsync", duration, retryCount, outcome.Exception)
            )
            .WrapAsync(
                Policy<PreviewResult>
                    .Handle<QueryExecutionException>()
                    .OrResult(r => !r.Success)
                    .CircuitBreakerAsync(
                        handledEventsAllowedBeforeBreaking: 5,
                        durationOfBreak: TimeSpan.FromSeconds(30),
                        onBreak: (outcome, duration) =>
                            LogCircuitBreak("ExecutePreviewAsync", outcome.Exception, duration)
                    )
            );

        // Policy para schema: retry com backoff, sem circuit breaker
        _schemaPolicy = Policy
            .Handle<SchemaIntrospectionException>(ex => ex.IsTransient)
            .Or<TimeoutException>()
            .Or<OperationCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 200),
                onRetry: (outcome, duration, retryCount, ctx) =>
                    LogRetry($"GetSchemaAsync", duration, retryCount, outcome)
            );
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _inner.TestConnectionAsync(ct);
            sw.Stop();

            return result with { Latency = sw.Elapsed };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult(
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }

    public async Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default)
    {
        return await _schemaPolicy.ExecuteAsync(async () =>
            await _inner.GetSchemaAsync(ct)
        );
    }

    public async Task<PreviewResult> ExecutePreviewAsync(
        CompiledQuery query,
        CancellationToken ct = default)
    {
        return await _previewPolicy.ExecuteAsync(async () =>
            await _inner.ExecutePreviewAsync(query, ct)
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (_inner is IAsyncDisposable disposable)
            await disposable.DisposeAsync();
    }

    private static void LogRetry(
        string operation,
        TimeSpan duration,
        int retryCount,
        Exception? exception)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[POLLY] {operation} - Retry #{retryCount} após {duration.TotalMilliseconds}ms. " +
            $"Error: {exception?.Message}"
        );
    }

    private static void LogCircuitBreak(
        string operation,
        Exception? exception,
        TimeSpan durationOfBreak)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[POLLY] {operation} - Circuit breaker aberto por {durationOfBreak.TotalSeconds}s. " +
            $"Error: {exception?.Message}"
        );
    }
}
```

---

## Próximos Passos

1. **Criar branches** para cada Eixo
2. **Implementar incrementalmente** seguindo ordem de priorização
3. **Executar testes** após cada mudança
4. **Code review** antes de merge
5. **Atualizar documentação** conforme progresso

Referências:
- [Refactoring Guru](https://refactoring.guru)
- [Clean Code - Robert Martin](https://www.oreilly.com/library/view/clean-code-a/9780136083238/)
- [Domain-Driven Design](https://www.domainlanguage.com/ddd/)
