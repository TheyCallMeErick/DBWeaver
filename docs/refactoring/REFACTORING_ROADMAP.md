# Roadmap de Refatoração - Visual SQL Architect

**Data de Análise:** 26 de março de 2026  
**Status:** Planejamento de Melhorias Estruturais

---

## 📋 Sumário Executivo

O projeto Visual SQL Architect é uma arquitetura bem estruturada com separação clara entre Core Engine (.NET) e UI (Avalonia). Contudo, existem oportunidades significativas para melhorar a manutenibilidade, extensibilidade e testabilidade através de refatorações estratégicas.

### Índice de Maturidade Atual
- **Arquitetura:** 7/10 (bem pensada, com pontos de melhoria)
- **Coesão:** 6/10 (alguns módulos acoplados)
- **Testabilidade:** 5/10 (poucos testes, difícil testar providers)
- **Manutenibilidade:** 6/10 (código claro, mas com duplicação)
- **Extensibilidade:** 6/10 (bom padrão Factory, mas sem abstração de metadata)

---

## 🎯 Eixos de Refatoração Prioritários

### Eixo 1: ABSTRAÇÃO DE PROVIDERS (P0 - CRÍTICO)

#### Problema Atual
```
┌─ SqlServerOrchestrator
├─ MySqlOrchestrator      ◄─ Cada um reimplementa:
├─ PostgresOrchestrator       • InspectorFactory (3x duplicação)
└─ MetadataService            • Query wrapping (TOP vs LIMIT vs OFFSET)
                               • Connection pooling patterns
```

**Impacto:**
- Duplicação de lógica em ~40% dos métodos de schema discovery
- Difícil adicionar novo provider sem copiar-colar
- Testes necessitam de 3 implementações para validar interfaces

#### Refatorações Propostas

##### 1.1 - Extract Provider-Specific Query Builders
**Nível:** Médio | **Esforço:** 5 dias | **Ganho:** 30% menos código

```csharp
// NOVO: Strategy Pattern para query dialect
public interface ISqlDialect
{
    string WrapPreview(string sql, int maxRows);
    string GetTablesQuery();
    string GetColumnsQuery(string schema, string table);
    string GetPrimaryKeysQuery(string schema, string table);
    // ...
}

public sealed class SqlServerDialect : ISqlDialect
{
    public string WrapPreview(string sql, int maxRows) 
        => $"SELECT TOP {maxRows} * FROM ({sql}) AS __preview";
}

public sealed class PostgresDialect : ISqlDialect
{
    public string WrapPreview(string sql, int maxRows) 
        => $"{sql} LIMIT {maxRows}";
}

// REFATOR: BaseDbOrchestrator utiliza ISqlDialect
public abstract class BaseDbOrchestrator
{
    protected abstract ISqlDialect GetDialect();
    
    protected string WrapPreview(string sql, int maxRows) 
        => GetDialect().WrapPreview(sql, maxRows);
}
```

**Benefício:** Reduz implementações concretas; SQL queries ficam testáveis sem DB real.

##### 1.2 - Unify Inspector Factory Pattern
**Nível:** Baixo | **Esforço:** 2 dias | **Ganho:** Eliminação de 3 factories

Consolidar `DbOrchestratorFactory` e `InspectorFactory` em um único pattern:

```csharp
// NOVO: Unified provider registry
public sealed class ProviderRegistry
{
    private readonly Dictionary<DatabaseProvider, Func<ConnectionConfig, IDbOrchestrator>> 
        _orchestrators = new();
    
    public ProviderRegistry()
    {
        Register(DatabaseProvider.SqlServer, cfg => new SqlServerOrchestrator(cfg));
        Register(DatabaseProvider.MySql, cfg => new MySqlOrchestrator(cfg));
        Register(DatabaseProvider.Postgres, cfg => new PostgresOrchestrator(cfg));
    }
    
    public IDbOrchestrator CreateOrchestrator(ConnectionConfig config) 
        => _orchestrators[config.Provider](config);
}
```

**Benefício:** Ponto único de extensão; melhor para DI; testes mockaveis.

---

### Eixo 2: ABSTRAÇÃO DE METADATA DISCOVERY (P0 - CRÍTICO)

#### Problema Atual
```
Cada orchestrator implementa:
├─ FetchTablesAsync()
├─ FetchColumnsAsync()      ◄─ SQL queries hardcoded em 3 linguagens
├─ FetchPrimaryKeysAsync()
└─ FetchForeignKeysAsync()
```

**Impacto:**
- Manutenção de query SQL em triplicate (bug fix = 3 lugares)
- Difícil de testar sem database real
- Novo provider = 4-5 métodos implementar

#### Refatorações Propostas

##### 2.1 - Extract SQL Metadata Queries Strategy
**Nível:** Médio | **Esforço:** 4 dias | **Ganho:** Testes unitários sem DB

```csharp
// NOVO: Abstração de queries de metadata
public interface IMetadataQueryProvider
{
    string GetTablesQuery();
    string GetColumnsQuery(string schema, string table);
    string GetPrimaryKeysQuery(string schema, string table);
    string GetForeignKeysQuery(string schema, string table);
    
    // Parse resultados
    IReadOnlyList<(string Schema, string Table)> ParseTables(DataTable dt);
    IReadOnlyList<ColumnSchema> ParseColumns(DataTable dt);
}

public sealed class SqlServerMetadataQueries : IMetadataQueryProvider
{
    public string GetTablesQuery() => @"
        SELECT TABLE_SCHEMA, TABLE_NAME
        FROM   INFORMATION_SCHEMA.TABLES
        WHERE  TABLE_TYPE = 'BASE TABLE'
        ORDER  BY TABLE_SCHEMA, TABLE_NAME
    ";
    
    public IReadOnlyList<(string, string)> ParseTables(DataTable dt) 
        => dt.AsEnumerable()
            .Select(r => (r.Field<string>(0), r.Field<string>(1)))
            .ToList();
}

// REFATOR: Reusável em múltiplos Orchestrators
public abstract class BaseDbOrchestrator(ConnectionConfig config, 
                                         IMetadataQueryProvider queries)
{
    protected async Task<IReadOnlyList<(string, string)>> FetchTablesAsync(
        DbConnection conn, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = queries.GetTablesQuery();
        var dt = new DataTable();
        new DbDataAdapter(cmd).Fill(dt);
        return queries.ParseTables(dt);
    }
}
```

**Benefício:** Queries isoladas; testáveis em unit tests; reutilizáveis.

##### 2.2 - Centralize Metadata Caching Logic
**Nível:** Baixo | **Esforço:** 2 dias | **Ganho:** Cache consistente em todos os providers

```csharp
// NOVO: Cache abstrato
public abstract class CachedMetadataStore : IMetadataStore
{
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly IMetadataQueryProvider _queries;
    
    public async Task<DbMetadata> GetMetadataAsync(DbConnection conn, CancellationToken ct)
    {
        string cacheKey = GetCacheKey();
        if (_cache.TryGetValue(cacheKey, out var entry) && !entry.IsExpired)
            return entry.Metadata;
        
        var metadata = await FetchMetadataAsync(conn, ct);
        _cache[cacheKey] = new CacheEntry(metadata, DateTimeOffset.UtcNow.AddMinutes(5));
        return metadata;
    }
    
    protected abstract Task<DbMetadata> FetchMetadataAsync(
        DbConnection conn, CancellationToken ct);
}
```

**Benefício:** TTL caching centralizado; menos inconsistências.

---

### Eixo 3: CONSOLIDAÇÃO DE QUERY BUILDING (P1 - ALTO)

#### Problema Atual
```
QueryBuilderService
  ├─ ApplySelects()        ◄─ Métodos privados não reutilizáveis
  ├─ ApplyJoins()              (acoplamento forte a SqlKata)
  ├─ ApplyFilters()
  ├─ ApplyGroupBy()
  ├─ ApplyOrderBy()
  └─ ApplyLimits()
```

**Impacto:**
- Lógica de query builder não testável isoladamente
- SqlKata acoplada; difícil trocar por outro query builder
- Funções complexas (Regex, JSON) espalhadas em SqlFunctionRegistry

#### Refatorações Propostas

##### 3.1 - Extract SqlKata Abstraction Layer
**Nível:** Alto | **Esforço:** 6 dias | **Ganho:** Independência de SqlKata

```csharp
// NOVO: Abstração de query builder
public interface IQueryBuilder
{
    IQueryBuilder Select(IEnumerable<SelectColumn> columns);
    IQueryBuilder Join(JoinDefinition join);
    IQueryBuilder Filter(FilterDefinition filter);
    IQueryBuilder GroupBy(IEnumerable<string> columns);
    IQueryBuilder OrderBy(IEnumerable<OrderDefinition> orders);
    IQueryBuilder Limit(int? limit, int? offset);
    CompiledQuery Compile();
}

// IMPLEMENTAÇÃO: SqlKata adapter
public sealed class SqlKataQueryBuilder(DatabaseProvider provider, 
                                         ISqlFunctionRegistry fnRegistry) 
    : IQueryBuilder
{
    private Query _query = new();
    
    public IQueryBuilder Select(IEnumerable<SelectColumn> columns)
    {
        foreach (var col in columns)
            _query = _query.SelectRaw(col.Expression, col.Alias);
        return this;
    }
    
    public CompiledQuery Compile()
    {
        var compiler = GetCompiler(provider);
        var result = compiler.Compile(_query);
        return new CompiledQuery(result.ToString(), result.Bindings ?? new Dictionary<string, object?>());
    }
}

// Uso: Independente de implementação
public class QueryBuilderService(IQueryBuilder builder)
{
    public CompiledQuery Build(VisualQuerySpec spec) =>
        builder
            .Select(spec.Selects ?? [])
            .Join(spec.Joins ?? [])
            .Filter(spec.Filters ?? [])
            .GroupBy(spec.GroupBy ?? [])
            .OrderBy(spec.Orders ?? [])
            .Limit(spec.Limit, spec.Offset)
            .Compile();
}
```

**Benefício:** Troca de library sem quebrar código; testes sem SqlKata.

##### 3.2 - Unified Function Fragment Registry
**Nível:** Médio | **Esforço:** 4 dias | **Ganho:** Funcionalidades centralizadas

Problemáticas atuais no `SqlFunctionRegistry`:

```csharp
// ANTES: Código duplicado em cada provider
if (canonicalFn == SqlFn.Regex)
{
    return provider switch
    {
        DatabaseProvider.Postgres => $"{column} ~ {value}",
        DatabaseProvider.MySql => $"{column} REGEXP {value}",
        DatabaseProvider.SqlServer => $"PATINDEX({value}, {column}) > 0",
        _ => throw new NotSupportedException()
    };
}
```

```csharp
// DEPOIS: Strategy Pattern consolidado
public interface IFunctionFragmentProvider
{
    string Regex(string column, string pattern);
    string RegexExtract(string column, string pattern, string group);
    string JsonExtract(string column, string path);
    string DateDiff(string fromExpr, string toExpr, string unit);
    // ...
}

public sealed class PostgresFunctionFragments : IFunctionFragmentProvider
{
    public string Regex(string column, string pattern) 
        => $"{column} ~ {pattern}";
}

// Registro centralizado
public sealed class SqlFunctionRegistry : ISqlFunctionRegistry
{
    private readonly IFunctionFragmentProvider _fragments;
    
    public SqlFunctionRegistry(DatabaseProvider provider)
    {
        _fragments = provider switch
        {
            DatabaseProvider.SqlServer => new SqlServerFunctionFragments(),
            DatabaseProvider.MySql => new MySqlFunctionFragments(),
            DatabaseProvider.Postgres => new PostgresFunctionFragments(),
            _ => throw new NotSupportedException()
        };
    }
}
```

**Benefício:** 40% menos código; adição de funções = 1 método por provider.

---

### Eixo 4: ESTRUTURA DE TESTES (P1 - ALTO)

#### Problema Atual
```
tests/
└─ VisualSqlArchitect.Tests/
    └─ Unit/
        ├─ Fixtures/        ◄─ Fixtures criadas manualmente
        └─ ...              ◄─ Poucos testes, principalmente e2e
```

**Impacto:**
- Cobertura de testes ~15% (estimado)
- Refatorações quebram sem feedback imediato
- Providers não testáveis sem DB real
- Query builders testáveis apenas com execução real

#### Refatorações Propostas

##### 4.1 - Create Unit Test Infrastructure
**Nível:** Médio | **Esforço:** 5 dias | **Ganho:** 40%+ cobertura de testes

```csharp
// NOVO: Test fixtures para providers
public sealed class ProviderTestFixture : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Setup Docker container com Postgres/MySQL/SQL Server
        // Seed sample database
    }
    
    public async Task DisposeAsync()
    {
        // Cleanup
    }
}

// Testes parametrizados
[Theory]
[InlineData(DatabaseProvider.SqlServer)]
[InlineData(DatabaseProvider.MySql)]
[InlineData(DatabaseProvider.Postgres)]
public async Task SchemaDiscovery_ReturnsCorrectTables(DatabaseProvider provider)
{
    using var fixture = new ProviderTestFixture(provider);
    var orchestrator = DbOrchestratorFactory.Create(fixture.Config);
    
    var schema = await orchestrator.GetSchemaAsync();
    
    Assert.Contains("orders", schema.Tables.Select(t => t.Name));
}
```

**Benefício:** Testes paralelos; feedback de regressão; CI/CD confiável.

##### 4.2 - Add Snapshot Testing for Query Compilation
**Nível:** Baixo | **Esforço:** 2 dias | **Ganho:** Query output rastreável

```csharp
[Fact]
public void QueryBuilding_RegexFilter_GeneratesCorrectSql()
{
    var spec = new VisualQuerySpec(
        FromTable: "users",
        Filters: new[]
        {
            new FilterDefinition(
                Column: "email",
                CanonicalFn: SqlFn.Regex,
                FnArgs: new[] { "'@example\\.com$'" }
            )
        }
    );
    
    var builder = new SqlKataQueryBuilder(DatabaseProvider.Postgres, registry);
    var result = builder.Build(spec);
    
    Verify(result.Sql);  // Snapshot testing
}
```

**Benefício:** Detecção automática de mudanças em queries geradas.

---

### Eixo 5: ORGANIZAÇÃO ESTRUTURAL (P2 - MÉDIO)

#### Problema Atual
```
Expressions/
  ├─ Advanced/
  ├─ Columns/
  ├─ Functions/
  ├─ Literals/
  └─ Operations/        ◄─ Hierarquia profunda não utilizada
```

**Impacto:**
- Namespaces profundos reduzem discoverability
- Pouco claro qual pasta pertence o novo código
- Duplicação entre Nodes/ e Expressions/

#### Refatorações Propostas

##### 5.1 - Flatten Expression Hierarchy
**Nível:** Baixo | **Esforço:** 1 dia | **Ganho:** Melhor navegação

```
ANTES:
  Expressions/Advanced/ComplexExpression.cs
  Expressions/Columns/ColumnExpression.cs
  Expressions/Functions/FunctionCall.cs

DEPOIS:
  ExpressionBuilder/
    ComplexExpression.cs
    ColumnExpression.cs
    FunctionCallExpression.cs
  
  Queries/
    SelectExpression.cs
    FilterExpression.cs
    JoinExpression.cs
```

**Benefício:** Namespaces mais planos; imports reduzidos.

##### 5.2 - Consolidate Node Definition System
**Nível:** Médio | **Esforço:** 3 dias | **Ganho:** Remover duplicação Nodes/Expressions

Ambos os namespaces contêm conceitos similares:
- `Nodes/NodeDefinition.cs` vs `Expressions/ISqlExpression.cs`
- `Nodes/NodeGraph.cs` vs construção de query em `QueryBuilderService`

```csharp
// CONSOLIDADO: Unificar em Graph-based expression system
namespace VisualSqlArchitect.QueryGraph;

public abstract record QueryNode(string Id)
{
    public abstract TResult Accept<TResult>(IQueryNodeVisitor<TResult> visitor);
}

public record TableNode(string Id, string TableName) : QueryNode(Id);
public record FilterNode(string Id, string Column, string Operator, object? Value) : QueryNode(Id);
public record JoinNode(string Id, string TargetTable, string LeftColumn, string RightColumn) : QueryNode(Id);

// Visitor pattern para compilação
public interface IQueryNodeVisitor<TResult>
{
    TResult Visit(TableNode node);
    TResult Visit(FilterNode node);
    TResult Visit(JoinNode node);
}
```

**Benefício:** Arquitetura mais clara; double-dispatch para operações complexas.

---

### Eixo 6: DEPENDENCY INJECTION E LIFECYCLE (P2 - MÉDIO)

#### Problema Atual
```
ServiceRegistration.cs
├─ DbOrchestratorFactory (factory manual)
├─ ActiveConnectionContext (gerenciamento manual)
└─ QueryBuilderService (criação manual por provider)
                    ▲
                    └─ Sem padrão DI consistente
```

**Impacto:**
- Inversão de controle limitada
- Difícil testar sem container completo
- Lifecycle de resources não claro

#### Refatorações Propostas

##### 6.1 - Structured Service Registration
**Nível:** Médio | **Esforço:** 3 dias | **Ganho:** DI consistente + fácil de testar

```csharp
// NOVO: Extension method centralizado
public static class VisualSqlArchitectServiceCollectionExtensions
{
    public static IServiceCollection AddVisualSqlArchitect(
        this IServiceCollection services,
        Action<VisualSqlArchitectOptions>? configure = null)
    {
        var options = new VisualSqlArchitectOptions();
        configure?.Invoke(options);
        
        // Core services
        services.AddSingleton<IProviderRegistry, ProviderRegistry>();
        services.AddSingleton<IMetadataQueryProviderFactory, MetadataQueryProviderFactory>();
        services.AddSingleton<IFunctionFragmentProviderFactory, FunctionFragmentProviderFactory>();
        
        // Active connection (scoped per canvas session)
        services.AddScoped<ActiveConnectionContext>();
        
        // Query building
        services.AddScoped<IQueryBuilderFactory, SqlKataQueryBuilderFactory>();
        
        return services;
    }
}

public class VisualSqlArchitectOptions
{
    public int MetadataCacheDurationSeconds { get; set; } = 300;
    public TimeoutSpan ConnectionTimeoutSeconds { get; set; } = 30;
}

// Uso em App.axaml.cs
var services = new ServiceCollection();
services.AddVisualSqlArchitect(opts => 
{
    opts.MetadataCacheDurationSeconds = 600;
    opts.ConnectionTimeoutSeconds = 45;
});
```

**Benefício:** DI claro; opções configuráveis; testes com serviços mockados.

##### 6.2 - Add Factory Patterns for Dynamic Provider Swap
**Nível:** Médio | **Esforço:** 2 dias | **Ganho:** Lifecycle seguro em mudanças de provider

```csharp
// NOVO: Factories para gerenciar lifetime
public interface IQueryBuilderFactory
{
    IQueryBuilder CreateForSpec(VisualQuerySpec spec);
}

public sealed class DynamicQueryBuilderFactory(
    IProviderRegistry providerRegistry,
    ISqlFunctionRegistry functionRegistry) : IQueryBuilderFactory
{
    public IQueryBuilder CreateForSpec(VisualQuerySpec spec)
    {
        var provider = functionRegistry.GetProvider();
        return new SqlKataQueryBuilder(provider, functionRegistry);
    }
}

// Uso seguro com provider switching
var activeContext = serviceProvider.GetRequiredService<ActiveConnectionContext>();
await activeContext.SwitchAsync(newConfig);

// Query builder atualizado automaticamente
var builder = serviceProvider.GetRequiredService<IQueryBuilderFactory>();
var queryBuilder = builder.CreateForSpec(spec);  // ✓ Correto para novo provider
```

**Benefício:** Provider switching sem erros; lifecycle automático.

---

### Eixo 7: ERROR HANDLING E RESILIENCE (P2 - MÉDIO)

#### Problema Atual
```
No BaseDbOrchestrator/Providers:
├─ Catch (Exception) { throw; }           ◄─ Não contextualizado
├─ Null propagation sem checks
└─ Nenhuma retry logic para transientes
```

**Impacto:**
- User-facing errors não informativos
- Falhas transientes (timeout) não recuperáveis
- Debugging difícil

#### Refatorações Propostas

##### 7.1 - Implement Exception Hierarchy
**Nível:** Baixo | **Esforço:** 2 dias | **Ganho:** Erros semanticamente corretos

```csharp
// NOVO: Exception hierarchy
namespace VisualSqlArchitect.Exceptions;

public abstract class VisualSqlArchitectException : Exception
{
    public virtual string UserMessage => Message;
}

public sealed class ConnectionFailedException : VisualSqlArchitectException
{
    public string? ConnectionString { get; set; }
    public TimeSpan? Latency { get; set; }
    public override string UserMessage => 
        $"Falha de conexão. Latência: {Latency?.TotalSeconds}s. Verifique credenciais.";
}

public sealed class SchemaIntrospectionException : VisualSqlArchitectException
{
    public string? Table { get; set; }
    public DatabaseProvider? Provider { get; set; }
}

public sealed class QueryCompilationException : VisualSqlArchitectException
{
    public VisualQuerySpec? QuerySpec { get; set; }
    public string? PartialSql { get; set; }
}

// Uso
try
{
    await connection.OpenAsync();
}
catch (SqlException ex) when (ex.Number == -2)  // Timeout
{
    throw new ConnectionFailedException(
        $"Timeout após {config.TimeoutSeconds}s", 
        ex) 
    { 
        Latency = stopwatch.Elapsed 
    };
}
```

**Benefício:** Erros UI-friendly; logging estruturado; retry logic identificável.

##### 7.2 - Add Polly Resilience Policies
**Nível:** Médio | **Esforço:** 3 dias | **Ganho:** Auto-retry transientes; circuit breaker

```csharp
public sealed class ResilientDbOrchestrator : IDbOrchestrator
{
    private readonly IAsyncPolicy<PreviewResult> _previewPolicy;
    private readonly IAsyncPolicy _connectionPolicy;
    
    public ResilientDbOrchestrator(IDbOrchestrator inner)
    {
        _connectionPolicy = Policy
            .Handle<ConnectionFailedException>(ex => IsTransient(ex))
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => 
                    TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100),
                onRetry: (outcome, duration, retryCount, ctx) =>
                    LogRetry(outcome, duration, retryCount)
            );
    }
    
    public async Task<PreviewResult> ExecutePreviewAsync(
        CompiledQuery query, CancellationToken ct) =>
        await _previewPolicy.ExecuteAsync(async () =>
            await _inner.ExecutePreviewAsync(query, ct)
        );
}
```

**Benefício:** Network resilience; melhor UX em conexões instáveis.

---

### Eixo 8: DOCUMENTATION E DISCOVERY (P3 - BAIXO)

#### Problema Atual
```
Projeto bem-comentado em nível de classe/método,
mas faltam:
├─ Architecture Decision Records (ADR)
├─ Extension guide para novos providers
├─ Troubleshooting guide
└─ API reference automatizado
```

#### Refatorações Propostas

##### 8.1 - Create Architecture Decision Records (ADR)
**Nível:** Baixo | **Esforço:** 2 dias | **Ganho:** Conhecimento compartilhado

```markdown
# ADR-001: SqlKata como Query Builder

## Contexto
Múltiplos providers (SQL Server, MySQL, Postgres) com dialetos SQL diferentes.

## Decisão
Usar SqlKata + Compiler pattern para abstração de dialetos SQL.

## Consequências
✓ Compiler injetável por provider
✓ Bindings parametrizados (SQL injection safe)
✓ Extensível para novos providers
✗ Acoplamento a SqlKata (resolver em ADR-002)
✗ Performance overhead em queries simples (~5%)

## Alternativas Consideradas
- Entity Framework Core → Overkill para query designer visual
- Hand-built SQL strings → Unsafe, sem abstração
- Custom query builder → Duplicação de work (SqlKata exists)
```

**Benefício:** Histórico de decisões; onboarding de devs; revisão de tradeoffs.

##### 8.2 - Generate API Docs from XML Comments
**Nível:** Baixo | **Esforço:** 1 dia | **Ganho:** Referência sempre atualizada

```csharp
// Adicionar ao .csproj
<PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>

// Build task para gerar docs
dotnet tool install -g docfx
docfx docs/docfx.json
```

**Benefício:** Documentação automatizada; sem desvios com código.

---

## 📊 Matriz de Priorização

| Eixo | P | Esforço | Ganho | Sprint | Blocker |
|------|---|---------|-------|--------|---------|
| 1 - Abstração Providers | P0 | 10d | Alto | Q2.1 | Sim (DX) |
| 2 - Abstração Metadata | P0 | 8d | Alto | Q2.1 | Sim (Testes) |
| 3 - Query Building | P1 | 12d | Alto | Q2.2 | Não |
| 4 - Testes | P1 | 7d | Muito Alto | Q2.2 | Não |
| 5 - Organização Estrutural | P2 | 4d | Médio | Q2.3 | Não |
| 6 - DI e Lifecycle | P2 | 5d | Médio | Q2.3 | Não |
| 7 - Error Handling | P2 | 5d | Médio | Q2.3 | Não |
| 8 - Documentation | P3 | 3d | Baixo | Q2.4 | Não |

**Total Esforço:** ~54 dias | **Timeline:** 3 sprints de 2 semanas

---

## 🚀 Implementação Proposta

### Fase 1: Fundação (Sprint 1-2)
1. ✅ Eixo 1 - Abstração de Providers (Provider Registry + Dialects)
2. ✅ Eixo 2 - Abstração de Metadata (Queries e Parsers)
3. ✅ Eixo 4 - Infraestrutura de Testes (Fixtures, parametrização)

**Objetivo:** Código refatorado + testes cobrindo providers

### Fase 2: Query Engine (Sprint 3)
4. ✅ Eixo 3 - Consolidação Query Building (SqlKata abstraction)
5. ✅ Eixo 6 - DI e Lifecycle (Service registration)

**Objetivo:** Query builder testável; DI consistente

### Fase 3: Polimento (Sprint 4-5)
6. ✅ Eixo 5 - Organização Estrutural (Flatten hierarquias)
7. ✅ Eixo 7 - Error Handling (Exceptions + Polly)
8. ✅ Eixo 8 - Documentation (ADRs + API docs)

**Objetivo:** Codebase limpo; documentação completa

---

## 📋 Checklist de Implementação

### Para Cada Refatoração

- [ ] Criar feature branch: `refactor/eixo-N-descricao`
- [ ] Implementar com testes (TDD)
- [ ] Atualizar documentação
- [ ] Code review com ≥2 aprovadores
- [ ] Rodar CI/CD pipeline completo
- [ ] Merge e criar release notes

### Exemplo: Eixo 1 (Provider Abstraction)

```bash
# 1. Branch
git checkout -b refactor/eixo-1-provider-abstraction

# 2. Criar interfaces
src/VisualSqlArchitect/Providers/ISqlDialect.cs
src/VisualSqlArchitect/Providers/Dialects/SqlServerDialect.cs
src/VisualSqlArchitect/Providers/Dialects/MySqlDialect.cs
src/VisualSqlArchitect/Providers/Dialects/PostgresDialect.cs

# 3. Refator orcherstrators
src/VisualSqlArchitect/Providers/SqlServerOrchestrator.cs (usar dialect)
src/VisualSqlArchitect/Providers/MySqlOrchestrator.cs (usar dialect)
src/VisualSqlArchitect/Providers/PostgresOrchestrator.cs (usar dialect)

# 4. Testes
tests/VisualSqlArchitect.Tests/Unit/Providers/ProviderDialectTests.cs

# 5. Atualizar ServiceRegistration
src/VisualSqlArchitect/ServiceRegistration.cs (usar ProviderRegistry)

# 6. Validação
dotnet test
dotnet build -c Release
```

---

## 🎓 Benefícios Esperados

| Benefício | Impacto | Métrica |
|-----------|---------|---------|
| **Redução de duplicação** | -40% LOC em providers | ~2,000 linhas economizadas |
| **Cobertura de testes** | 15% → 50%+ | +35 pontos percentuais |
| **Tempo de onboarding** | -50% | Novo dev produtivo em 3 dias |
| **Facilidade de extensão** | Novo provider em 2 dias | -5 dias vs atual |
| **Ciclo de feedback (CI/CD)** | -60% build time | ~10 min → 4 min |
| **Maintainability** | +70% code health | SonarQube score: 7.5 → 8.5+ |

---

## ⚠️ Riscos e Mitigações

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|--------|-----------|
| Refatoração quebra providers | Alto | Crítico | Testes parametrizados antes de refator |
| Regressão em query building | Alto | Alto | Snapshot testing de queries geradas |
| Aumento de complexidade temporária | Médio | Médio | Fazer refactors pequenos, incrementais |
| Conhecimento disperso | Médio | Médio | ADRs e documentação contínua |

---

## 📚 Referências Externas

- [Refactoring Guru - Strategy Pattern](https://refactoring.guru/design-patterns/strategy)
- [Martin Fowler - Strangler Pattern](https://martinfowler.com/bliki/StranglerFigPattern.html)
- [Microsoft - Dependency Injection Patterns](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [xUnit.net - Data-driven tests](https://xunit.net/docs/getting-started/netfx/attribute-examples)
- [Polly - Resilience Policies](https://github.com/App-vNext/Polly)

---

## 📞 Contato & Feedback

Questões sobre este roadmap? Comentários bem-vindos em discussions ou PRs.

**Última atualização:** 26 de março de 2026
