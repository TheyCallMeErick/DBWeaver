# Visual SQL Architect — Core Engine

Multi-database query engine for the Avalonia infinite-canvas designer.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Avalonia Canvas (UI)                      │
│  DataSourceNode  ──►  ConnectionConfig                       │
│  FilterNode      ──►  FilterDefinition                       │
│  JoinNode        ──►  JoinDefinition                         │
│  SelectNode      ──►  SelectColumn                           │
└──────────────────────────┬──────────────────────────────────┘
                           │  VisualQuerySpec
                           ▼
┌─────────────────────────────────────────────────────────────┐
│               QueryBuilderService                            │
│                                                              │
│  SqlKata Query  ◄──────────────────  VisualQuerySpec        │
│       │                                                      │
│       │  function fragments                                  │
│       ▼                                                      │
│  ISqlFunctionRegistry                                        │
│    ├── PostgresMap  → col ~ pattern                         │
│    ├── MySqlMap     → col REGEXP pattern                     │
│    └── SqlServerMap → PATINDEX(pattern, col) > 0            │
│                                                              │
│  SqlKata Compiler (provider-injected)                        │
│    ├── PostgresCompiler  → "quoted", LIMIT/OFFSET            │
│    ├── MySqlCompiler     → `quoted`, LIMIT                   │
│    └── SqlServerCompiler → [quoted], TOP / OFFSET FETCH      │
│                                                              │
│  CompiledQuery { Sql, Bindings }                             │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                 IDbOrchestrator                              │
│  ├── SqlServerOrchestrator (Microsoft.Data.SqlClient)        │
│  ├── MySqlOrchestrator     (MySqlConnector)                  │
│  └── PostgresOrchestrator  (Npgsql)                          │
│                                                              │
│  TestConnectionAsync() → ConnectionTestResult                │
│  GetSchemaAsync()      → DatabaseSchema                      │
│  ExecutePreviewAsync() → PreviewResult (always rolled back)  │
└─────────────────────────────────────────────────────────────┘
```

---

## Quick Start

### 1 — Wire up DI (App.axaml.cs)

```csharp
var services = new ServiceCollection();
services.AddVisualSqlArchitect();
var provider = services.BuildServiceProvider();
```

### 2 — Connect to a database

```csharp
var ctx = provider.GetRequiredService<ActiveConnectionContext>();

await ctx.SwitchAsync(new ConnectionConfig(
    Provider:  DatabaseProvider.Postgres,
    Host:      "localhost",
    Port:      5432,
    Database:  "northwind",
    Username:  "postgres",
    Password:  "secret"
));
```

### 3 — Introspect the schema

```csharp
var schema = await ctx.Orchestrator.GetSchemaAsync();

foreach (var table in schema.Tables)
{
    Console.WriteLine($"{table.FullName}");
    foreach (var col in table.Columns)
        Console.WriteLine($"  {col.Name} {col.DataType} PK={col.IsPrimaryKey}");
}
```

### 4 — Ask the registry for a function fragment

Canvas nodes never write raw SQL. They ask the registry:

```csharp
// From a FilterNode that wants REGEX matching
var fragment = ctx.FunctionRegistry.GetFunction(SqlFn.Regex, "email", "'@corp\\.io'");

// Postgres  → email ~ '@corp\.io'
// MySQL     → email REGEXP '@corp\.io'
// SQL Server→ PATINDEX('@corp.io', email) > 0
```

### 5 — Build a query from the canvas graph

```csharp
var compiled = ctx.QueryBuilder.Compile(new VisualQuerySpec(
    FromTable: "orders",
    Selects: new[]
    {
        new SelectColumn("o.id",           "OrderId"),
        new SelectColumn("c.name",         "Customer"),
        new SelectColumn("o.total_amount", "Amount"),
    },
    Joins: new[]
    {
        new JoinDefinition("customers c", "o.customer_id", "c.id", "LEFT")
    },
    Filters: new[]
    {
        new FilterDefinition("o.status", "=", "active"),
        new FilterDefinition("email", "REGEX", "'@corp\\.io'"), // resolved via registry
    },
    Orders: new[] { new OrderDefinition("o.created_at", Descending: true) },
    Limit:  50
));

Console.WriteLine(compiled.Sql);
// Bindings are already parameterised — safe to pass straight to ADO.NET
```

### 6 — Preview results

```csharp
var preview = await ctx.Orchestrator.ExecutePreviewAsync(compiled.Sql, maxRows: 100);

if (preview.Success)
{
    // preview.Data is a DataTable — bind to DataGrid in Avalonia
    // preview.ExecutionTime gives latency for the status bar
}
else
{
    // Show preview.ErrorMessage in the canvas error overlay
}
```

---

## Adding a New Function

1. Add a constant to `SqlFn`:
   ```csharp
   public const string Base64Encode = "BASE64_ENCODE";
   ```

2. Add a renderer to each provider map in `SqlFunctionRegistry`:
   ```csharp
   // Postgres
   [SqlFn.Base64Encode] = a => $"encode({a[0]}::bytea, 'base64')",

   // MySQL
   [SqlFn.Base64Encode] = a => $"TO_BASE64({a[0]})",

   // SQL Server
   [SqlFn.Base64Encode] = a => $"CAST(N'' AS XML).value('xs:base64Binary(sql:column({a[0]}))', 'VARCHAR(MAX)')",
   ```

3. Canvas nodes can now call `registry.GetFunction(SqlFn.Base64Encode, "data_col")` — no other changes needed.

---

## Adding a New Provider

1. Create `Providers/OracleOrchestrator.cs` extending `BaseDbOrchestrator`.
2. Add `Oracle` to the `DatabaseProvider` enum.
3. Add a case to `DbOrchestratorFactory.Create()`.
4. Add an `OracleMap()` to `SqlFunctionRegistry.BuildMap()`.
5. Add a case to `QueryBuilderService.CreateCompiler()` (SqlKata ships an `OracleCompiler`).
