<div align="center">

# Visual SQL Architect

**Build SQL queries visually — no typing required.**

A node-based, infinite-canvas SQL designer that compiles to real, parameterised SQL for SQL Server, PostgreSQL and MySQL.

[![CI](https://github.com/TheyCallMeErick/VisualSqlArchtect/actions/workflows/ci.yml/badge.svg)](https://github.com/TheyCallMeErick/VisualSqlArchtect/actions/workflows/ci.yml)
[![Release](https://github.com/TheyCallMeErick/VisualSqlArchtect/actions/workflows/release.yml/badge.svg)](https://github.com/TheyCallMeErick/VisualSqlArchtect/releases)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com)
[![Avalonia](https://img.shields.io/badge/Avalonia-11.1-8B5CF6)](https://avaloniaui.net)

</div>

---

## What is it?

Visual SQL Architect lets you drag nodes onto a canvas and wire them together to form SQL queries. Every connection becomes a JOIN, every filter node becomes a WHERE clause, and the result is instantly shown as live SQL beneath the canvas — no typing, no syntax errors, no dialect headaches.

```
[ orders ] ──── [ customers ]          [ WHERE: status = 'active' ]
      │                │                           │
      └────────────────┴───────────────────────────┘
                       │
              [ SELECT: id, name, total ]
                       │
              ┌────────▼────────┐
              │  Live SQL Bar   │
              │                 │
              │  SELECT "o"."id", "c"."name", "o"."total"
              │  FROM "orders" AS o
              │  LEFT JOIN "customers" AS c ON o.customer_id = c.id
              │  WHERE "o"."status" = 'active'
              └─────────────────┘
```

---

## Features

### Canvas
- **Infinite pan & zoom** — middle-mouse pan, scroll-wheel zoom, keyboard shortcuts
- **Drag-and-drop nodes** — search palette with fuzzy find, keyboard-first workflow
- **Wired connections** — bezier curves with live validation, type-checked pins
- **Multi-select & align** — select, move, delete, and align groups of nodes
- **Auto-layout** — one click to arrange a messy graph into a clean tree
- **Undo / redo** — granular command stack, Ctrl+Z / Ctrl+Y
- **Save / load sessions** — JSON canvas persistence, open multiple tabs

### SQL Generation
- **Real-time SQL preview** — every edit updates the SQL bar instantly
- **Multi-dialect** — SQL Server, PostgreSQL, MySQL — switch provider, SQL updates
- **Safe previews** — `ExecutePreviewAsync` always rolls back; never mutates data
- **EXPLAIN plan** — one click to visualise the query plan returned by the server
- **SQL Importer** — paste existing SQL, import it back as a node graph

### Node Library

| Category | Nodes |
|---|---|
| **Data Source** | Table, Raw SQL |
| **Comparison** | =, ≠, >, ≥, <, ≤, BETWEEN, LIKE, IS NULL |
| **Logic Gates** | AND, OR, NOT |
| **Aggregates** | SUM, COUNT, AVG, MIN, MAX, COUNT DISTINCT |
| **Math** | +, −, ×, ÷, ROUND, ABS, MOD, POWER |
| **String** | UPPER, LOWER, TRIM, LENGTH, CONCAT, REPLACE, SUBSTRING, REGEX |
| **Conditionals** | CASE WHEN, IIF / COALESCE |
| **JSON** | JSON Extract, JSON Value, JSON Array Length |
| **Type** | CAST / CONVERT |
| **Result Modifiers** | ORDER BY, LIMIT / TOP, DISTINCT, GROUP BY, HAVING |

### Database Integration
- **Connection Manager** — save and name multiple connections, test with one click
- **Schema Explorer** — browse schemas, tables, and columns in a sidebar tree
- **Auto-join detection** — detects FK relationships and naming conventions (`orders.customer_id → customers.id`), suggests the right join automatically
- **Query Template Library** — save and load reusable node graph snippets

### Developer Tooling
- **App Diagnostics panel** — live memory, FPS, connection state
- **Benchmark overlay** — measure render time and compilation time
- **201 unit tests** — node compilers, dialect emission, metadata heuristics, all green

---

## Download

Grab the latest self-contained binary from [Releases](https://github.com/TheyCallMeErick/VisualSqlArchtect/releases) — no .NET install required.

| Platform | Binary |
|---|---|
| Windows x64 | `VisualSqlArchitect-win-x64.exe` |
| Linux x64 | `VisualSqlArchitect-linux-x64` |
| macOS x64 | `VisualSqlArchitect-osx-x64` |

---

## Build from source

**Prerequisites:** [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

```bash
git clone https://github.com/TheyCallMeErick/VisualSqlArchtect.git
cd VisualSqlArchtect

# Run the app
dotnet run --project src/VisualSqlArchitect.UI

# Run the test suite
dotnet test files.sln
```

---

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                   Avalonia Canvas (UI)                        │
│                                                               │
│  NodeControl  ──►  PinDragInteraction  ──►  BezierWireLayer  │
│  SidebarControl  ──►  SchemaControl                           │
│  LiveSqlBar    ──►  DataPreviewPanel                          │
└───────────────────────────┬──────────────────────────────────┘
                            │  NodeGraph (nodes + connections)
                            ▼
┌──────────────────────────────────────────────────────────────┐
│               NodeGraphCompiler                               │
│                                                               │
│  NodeCompilerFactory                                          │
│    ├── DataSourceCompiler   ──►  TableExpr / RawSqlExpr      │
│    ├── ComparisonCompiler   ──►  ComparisonExpr / BetweenExpr│
│    ├── LogicGateCompiler    ──►  LogicGateExpr                │
│    ├── AggregateCompiler    ──►  AggregateExpr                │
│    ├── MathTransformCompiler──►  FunctionCallExpr             │
│    ├── StringTransformCompiler ─►  FunctionCallExpr           │
│    ├── JsonCompiler         ──►  RawSqlExpr (->> / JSON_VALUE)│
│    ├── ConditionalCompiler  ──►  CaseExpr                     │
│    └── LiteralCompiler      ──►  LiteralExpr                  │
│                                                               │
│  CompiledNodeGraph { SelectExprs, WhereExprs, … }            │
└───────────────────────────┬──────────────────────────────────┘
                            │  ISqlExpression.Emit(ctx)
                            ▼
┌──────────────────────────────────────────────────────────────┐
│               QueryBuilderService  (SqlKata)                  │
│                                                               │
│  ISqlDialect          ISqlFunctionRegistry                    │
│    ├── PostgresDialect   ├── PostgresFunctionFragments        │
│    ├── MySqlDialect      ├── MySqlFunctionFragments           │
│    └── SqlServerDialect  └── SqlServerFunctionFragments       │
│                                                               │
│  CompiledQuery { Sql, Bindings }  ── always parameterised    │
└───────────────────────────┬──────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────────┐
│                IDbOrchestrator                                │
│  ├── SqlServerOrchestrator  (Microsoft.Data.SqlClient)        │
│  ├── MySqlOrchestrator      (MySqlConnector)                  │
│  └── PostgresOrchestrator   (Npgsql)                          │
│                                                               │
│  TestConnectionAsync()   GetSchemaAsync()                     │
│  ExecutePreviewAsync()   GetExplainPlanAsync()                │
└──────────────────────────────────────────────────────────────┘
```

---

## Project structure

```
VisualSqlArchtect/
├── src/
│   ├── VisualSqlArchitect/          # Core engine (no UI dependency)
│   │   ├── Nodes/                   # Node definitions, compilers, graph
│   │   ├── Expressions/             # SQL expression tree (ISqlExpression)
│   │   ├── Metadata/                # Schema inspection, auto-join detection
│   │   ├── Providers/               # DB orchestrators + SQL dialects
│   │   ├── QueryEngine/             # QueryBuilderService, function registry
│   │   └── Registry/                # IProviderRegistry (DI integration)
│   │
│   └── VisualSqlArchitect.UI/       # Avalonia desktop app
│       ├── Controls/                # InfiniteCanvas, NodeControl, overlays
│       ├── ViewModels/              # CanvasViewModel + decomposed managers
│       ├── Services/                # UI services extracted from MainWindow
│       └── Serialization/           # JSON canvas persistence
│
└── tests/
    └── VisualSqlArchitect.Tests/    # 201 unit tests (xUnit)
```

---

## Contributing

1. Fork the repo
2. Create a feature branch off `main`
3. Run `dotnet test files.sln` — all 201 tests must pass
4. Open a pull request

The CI pipeline runs on every PR; the release pipeline publishes binaries automatically when a `v*` tag is pushed.

---

<div align="center">
Built with Avalonia UI · .NET 9 · SqlKata
</div>
