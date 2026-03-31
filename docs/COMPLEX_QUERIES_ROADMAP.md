# Roadmap — Suporte a Consultas de Alta Complexidade

> **Contexto:** A query de referência usa 4 CTEs encadeadas, window functions com island-gap
> pattern, HAVING, SELECT DISTINCT, STRING_AGG, aritmética sobre resultados de window
> functions, 6 JOINs com aliases, e views como fonte. Este documento mapeia o que precisa
> ser feito para que o Visual SQL Architect gere consultas nesse nível.

---

## Análise da Query de Referência

```sql
WITH BoletosVencidos AS (...)    -- CTE 1: JOIN + GROUP BY + WHERE com GETDATE()
   , ComGrupo AS (...)           -- CTE 2: window function ROW_NUMBER() + aritmética
   , GruposConsecutivos AS (...) -- CTE 3: GROUP BY + HAVING COUNT(*) = 3
   , ProcessosElegiveis AS (...) -- CTE 4: SELECT DISTINCT + BETWEEN em CTE anterior
SELECT
    STRING_AGG(CAST(...), ', ')  -- agregador de strings com CAST interno
    UPPER(...)                   -- função de string em coluna de JOIN
FROM ProcessosElegiveis pe       -- CTE como fonte principal
INNER JOIN Boleto b ON ...       -- 6 JOINs com aliases de tabela
INNER JOIN vwPessoa vwp ON ...   -- JOIN em view (não tabela)
WHERE ... AND b.dtVencimento < GETDATE()
GROUP BY ...                     -- GROUP BY em 9 colunas
ORDER BY vwp.nome
```

---

## Gap Analysis — Atual vs Necessário

| Feature | Suporte Atual | Gap |
|---------|--------------|-----|
| `WITH ... AS` (CTEs) | ❌ Nenhum | CTE Definition + CTE Source como nodes |
| `ROW_NUMBER() OVER(...)` | ❌ Nenhum | Window function node completo |
| Aritmética com window function | ❌ Parcial | Subtract aceita ISqlExpression; falta WindowExpr |
| `HAVING` | ❌ Nenhum | HavingBinding no pipeline + UI |
| `SELECT DISTINCT` | ❌ Nenhum | Flag no ResultOutput + gerador |
| `STRING_AGG` | ❌ Nenhum | Novo agregador com separador + dialeto |
| `GETDATE()` / `NOW()` | ❌ Nenhum | System date function node |
| JOINs visuais com aliases | ⚠️ Invisível | JOIN node visual + alias por tabela |
| View como fonte | ✅ Parcial | Schema já carrega views; TableSource aceita |
| CTE como fonte de outro CTE | ❌ Nenhum | Depende de CTEs |
| `CAST` dentro de função | ✅ Funciona | CastExpr já é ISqlExpression |
| BETWEEN com datas literais | ✅ Funciona | BetweenExpr já existe |
| 6+ JOINs | ⚠️ Sem visual | Funcional mas sem controle visual explícito |

---

## Fases de Implementação

---

## FASE 1 — Fundação: Features Isoladas e de Baixo Risco

> Cada item desta fase é independente, não quebra nada existente e tem ROI imediato.

---

### 1.1 — HAVING Clause

**Impacto:** alto. Bloqueia qualquer query com filtro pós-agregação.

**O que é:**
`HAVING COUNT(*) = 3` — filtra grupos **após** o GROUP BY, sobre valores agregados.
Conceitualmente idêntico ao WHERE, mas executado depois da agregação.

**Mudanças necessárias:**

**`NodeGraph.cs`** — adicionar `HavingBinding[]`:
```csharp
public record HavingBinding(string NodeId, string PinName);

// Em NodeGraph:
public HavingBinding[] Havings { get; init; } = [];
```

**`NodeDefinition.cs`** — adicionar pin `having` no ResultOutput:
```csharp
In("having", PinDataType.Boolean, required: false),  // nova entrada
```

**`NodeGraphCompiler.cs`** — compilar as expressões de HAVING (idêntico ao WHERE):
```csharp
public IReadOnlyList<ISqlExpression> HavingExprs { get; init; }
```

**`QueryGeneratorService.cs`** — emitir HAVING:
```csharp
private void ApplyHavings(Query query, IReadOnlyList<ISqlExpression> havings)
{
    foreach (var expr in havings)
        query.HavingRaw(expr.Emit(ctx));
}
```

**`QueryGraphBuilder.cs`** — extrair HavingBindings do canvas (igual ao WhereBindings).

**Visual (UX):**
- Pin `having` no ResultOutput com cor distinta (âmbar, `#FBBF24`) para diferenciar do `where`
- Tooltip: *"HAVING — filters on aggregated values, after GROUP BY"*
- Nó `CompileHaving` (espelho do `CompileWhere`) para combinar múltiplas condições

---

### 1.2 — SELECT DISTINCT

**Impacto:** médio. Necessário para desduplicar resultados.

**Mudanças necessárias:**

**`NodeDefinition.cs`** — parâmetro boolean no ResultOutput:
```csharp
Param("distinct", ParameterKind.Boolean, "false", "Deduplicate rows (SELECT DISTINCT)")
```

**`NodeGraph.cs`:**
```csharp
public bool Distinct { get; init; }
```

**`QueryGeneratorService.cs`:**
```csharp
if (graph.Distinct)
    query.Distinct();
```

**Visual (UX):**
- Checkbox `Distinct` no painel de propriedades do ResultOutput
- Badge `DISTINCT` no header do node quando ativo (cor ciano)

---

### 1.3 — System Date Functions (GETDATE, NOW, CURRENT_DATE)

**Impacto:** médio. Datas dinâmicas são comuns em qualquer query de negócio.

**Novos NodeTypes:**
```csharp
SystemDate,        // GETDATE() / NOW() / CURRENT_TIMESTAMP
SystemDateTime,    // alias — mesma função, mantém compatibilidade
CurrentDate,       // DATE(NOW()) — apenas data, sem hora
CurrentTime,       // apenas hora
```

**`ISqlExpression.cs`** — nova expressão:
```csharp
public sealed record SystemDateExpr(SystemDatePart Part) : ISqlExpression
{
    public string Emit(EmitContext ctx) => ctx.Provider switch
    {
        DatabaseProvider.SqlServer => Part == SystemDatePart.Date ? "CAST(GETDATE() AS DATE)" : "GETDATE()",
        DatabaseProvider.MySql     => Part == SystemDatePart.Date ? "CURDATE()" : "NOW()",
        DatabaseProvider.Postgres  => Part == SystemDatePart.Date ? "CURRENT_DATE" : "CURRENT_TIMESTAMP",
        _ => "CURRENT_TIMESTAMP"
    };
    public PinDataType OutputType => PinDataType.DateTime;
}

public enum SystemDatePart { FullDateTime, DateOnly, TimeOnly }
```

**Visual (UX):**
- Categoria: `Literal` (valor constante determinístico)
- Output pin único: DateTime
- Label do node: `GETDATE()` / `NOW()` (atualiza conforme provider)
- Sem inputs — é um valor terminal

---

### 1.4 — STRING_AGG / GROUP_CONCAT

**Impacto:** médio. Necessário para agregar strings em grupos.

**`NodeDefinition.cs`** — novo NodeType `StringAgg`:
```csharp
[NodeType.StringAgg] = new(
    NodeType.StringAgg,
    NodeCategory.Aggregate,
    "String Agg",
    "Concatenates values within a group into a delimited string",
    [
        In("value",     PinDataType.Text,   required: true),
        In("order_by",  PinDataType.Any,    required: false),  // WITHIN GROUP ORDER BY
        Out("result",   PinDataType.Text),
    ],
    [
        Param("separator", ParameterKind.Text,    ", ",   "Delimiter between values"),
        Param("distinct",  ParameterKind.Boolean, "false","Deduplicate values"),
    ]
)
```

**`ISqlExpression.cs`** — nova expressão:
```csharp
public sealed record StringAggExpr(
    ISqlExpression Value,
    string Separator,
    bool Distinct,
    ISqlExpression? OrderBy
) : ISqlExpression
{
    public string Emit(EmitContext ctx) => ctx.Provider switch
    {
        DatabaseProvider.SqlServer =>
            OrderBy is null
                ? $"STRING_AGG({(Distinct ? "DISTINCT " : "")}{Value.Emit(ctx)}, '{Separator}')"
                : $"STRING_AGG({Value.Emit(ctx)}, '{Separator}') WITHIN GROUP (ORDER BY {OrderBy.Emit(ctx)})",
        DatabaseProvider.MySql =>
            $"GROUP_CONCAT({(Distinct ? "DISTINCT " : "")}{Value.Emit(ctx)} SEPARATOR '{Separator}')",
        DatabaseProvider.Postgres =>
            $"STRING_AGG({(Distinct ? "DISTINCT " : "")}{Value.Emit(ctx)}, '{Separator}')",
        _ => $"STRING_AGG({Value.Emit(ctx)}, '{Separator}')"
    };
    public PinDataType OutputType => PinDataType.Text;
}
```

---

### 1.5 — Date Arithmetic Nodes

**Impacto:** médio. Comuns em qualquer contexto de negócio com prazos.

**Novos NodeTypes:**
```csharp
DateAdd,     // DATEADD(day, 30, col)
DateDiff,    // DATEDIFF(day, start, end)
DatePart,    // YEAR(), MONTH(), DAY(), DATEPART()
DateFormat,  // FORMAT(date, 'yyyy-MM-dd')
```

Dialeto-aware via `SqlFunctionRegistry`.

---

## FASE 2 — Window Functions

> A feature mais complexa isoladamente. Requer novo tipo de expressão, novo node com múltiplos
> sub-parâmetros visuais e mudanças na compilação.

---

### 2.1 — Arquitetura da WindowFunctionExpr

**`ISqlExpression.cs`** — nova hierarquia:
```csharp
public enum WindowFunctionKind
{
    RowNumber, Rank, DenseRank, Ntile,
    Lag, Lead, FirstValue, LastValue,
    SumOver, AvgOver, MinOver, MaxOver, CountOver,
}

public sealed record WindowFunctionExpr(
    WindowFunctionKind  Kind,
    ISqlExpression?     Value,          // null para RowNumber/Rank
    int?                Offset,         // para LAG/LEAD
    ISqlExpression?     DefaultValue,   // para LAG/LEAD
    int?                NtileGroups,    // para NTILE(n)
    IReadOnlyList<ISqlExpression> PartitionBy,
    IReadOnlyList<(ISqlExpression Expr, bool Desc)> OrderBy,
    WindowFrame?        Frame
) : ISqlExpression
{
    public string Emit(EmitContext ctx)
    {
        string fn = Kind switch
        {
            WindowFunctionKind.RowNumber  => "ROW_NUMBER()",
            WindowFunctionKind.Rank       => "RANK()",
            WindowFunctionKind.DenseRank  => "DENSE_RANK()",
            WindowFunctionKind.Ntile      => $"NTILE({NtileGroups})",
            WindowFunctionKind.Lag        => BuildLagLead("LAG", ctx),
            WindowFunctionKind.Lead       => BuildLagLead("LEAD", ctx),
            WindowFunctionKind.SumOver    => $"SUM({Value!.Emit(ctx)})",
            WindowFunctionKind.AvgOver    => $"AVG({Value!.Emit(ctx)})",
            WindowFunctionKind.MinOver    => $"MIN({Value!.Emit(ctx)})",
            WindowFunctionKind.MaxOver    => $"MAX({Value!.Emit(ctx)})",
            WindowFunctionKind.CountOver  => $"COUNT({(Value is null ? "*" : Value.Emit(ctx))})",
            WindowFunctionKind.FirstValue => $"FIRST_VALUE({Value!.Emit(ctx)})",
            WindowFunctionKind.LastValue  => $"LAST_VALUE({Value!.Emit(ctx)})",
            _ => throw new NotSupportedException()
        };
        return $"{fn} OVER ({BuildOver(ctx)})";
    }

    private string BuildOver(EmitContext ctx)
    {
        var parts = new List<string>();
        if (PartitionBy.Count > 0)
            parts.Add($"PARTITION BY {string.Join(", ", PartitionBy.Select(p => p.Emit(ctx)))}");
        if (OrderBy.Count > 0)
            parts.Add($"ORDER BY {string.Join(", ", OrderBy.Select(o => $"{o.Expr.Emit(ctx)}{(o.Desc ? " DESC" : "")}"))}");
        if (Frame is not null)
            parts.Add(Frame.Emit());
        return string.Join(" ", parts);
    }
}

public sealed record WindowFrame(
    WindowFrameUnit Unit,   // Rows, Range
    WindowFrameBound Start,
    WindowFrameBound? End
)
{
    public string Emit() =>
        End is null
            ? $"{Unit.ToString().ToUpper()} {Start.Emit()}"
            : $"{Unit.ToString().ToUpper()} BETWEEN {Start.Emit()} AND {End.Emit()}";
}

public sealed record WindowFrameBound(WindowFrameBoundKind Kind, int? Offset)
{
    public string Emit() => Kind switch
    {
        WindowFrameBoundKind.UnboundedPreceding => "UNBOUNDED PRECEDING",
        WindowFrameBoundKind.Preceding          => $"{Offset} PRECEDING",
        WindowFrameBoundKind.CurrentRow         => "CURRENT ROW",
        WindowFrameBoundKind.Following          => $"{Offset} FOLLOWING",
        WindowFrameBoundKind.UnboundedFollowing => "UNBOUNDED FOLLOWING",
        _ => "CURRENT ROW"
    };
}
```

---

### 2.2 — WindowFunction Node

**`NodeDefinition.cs`** — novo NodeType `WindowFunction`:
```csharp
[NodeType.WindowFunction] = new(
    NodeType.WindowFunction,
    NodeCategory.Aggregate,    // visualmente no mesmo grupo
    "Window Function",
    "Analytical function computed over a sliding window (OVER clause)",
    [
        In("value",        PinDataType.Any, required: false),  // expr para SUM/AVG/LAG etc.
        In("partition_1",  PinDataType.Any, required: false),  // PARTITION BY (variadic)
        In("partition_2",  PinDataType.Any, required: false),
        In("partition_3",  PinDataType.Any, required: false),
        In("order_1",      PinDataType.Any, required: false),  // ORDER BY dentro do OVER
        In("order_2",      PinDataType.Any, required: false),
        Out("result",      PinDataType.Number),
    ],
    [
        Param("function", ParameterKind.Enum, "RowNumber",
              values: ["RowNumber","Rank","DenseRank","Ntile","Lag","Lead",
                       "FirstValue","LastValue","SumOver","AvgOver","MinOver","MaxOver","CountOver"]),
        Param("offset",       ParameterKind.Number, "1",    "Offset for LAG/LEAD"),
        Param("ntile_groups", ParameterKind.Number, "4",    "Number of groups for NTILE"),
        Param("order_1_desc", ParameterKind.Boolean,"false","Descending for first ORDER BY"),
        Param("order_2_desc", ParameterKind.Boolean,"false","Descending for second ORDER BY"),
        Param("frame",        ParameterKind.Enum,   "None",
              values: ["None","UnboundedPreceding_CurrentRow","CurrentRow_UnboundedFollowing","Custom"]),
    ]
)
```

**Visual (UX) do Node:**
```
┌─────────────────────────────────────────┐
│  ⊡  Window Function     [ROW_NUMBER]    │  ← header roxo, enum selector
├─────────────────────────────────────────┤
│  value ──────○                          │  ← input (null para ROW_NUMBER/RANK)
│  partition_1 ──────○  PARTITION BY      │
│  partition_2 ──────○                   │
│  order_1 ──────○      ORDER BY  [ASC▼] │
│  order_2 ──────○               [DESC▼] │
├─────────────────────────────────────────┤
│  Frame:  [None ▼]                       │
├─────────────────────────────────────────┤
│                                  ○──── result
└─────────────────────────────────────────┘
```

**SQL emitido para o exemplo:**
```sql
ROW_NUMBER() OVER(PARTITION BY id_processo_refis ORDER BY parcela)
```

---

### 2.3 — Pins Variádicos para PARTITION BY / ORDER BY

O node de window function precisará de pins variádicos (como o `And`/`Or` atual).
Implementar padrão de "adicionar pin dinamicamente" — já existe no `AllowMultiple` dos pins.

---

## FASE 3 — CTEs (Common Table Expressions)

> A feature mais impactante arquiteturalmente. Requer novo conceito de "subgrafo nomeado"
> no modelo de dados e mudanças em todas as camadas.

---

### 3.1 — Modelo Mental Visual

Cada CTE é um **subgrafo encapsulado** com:
- Um nome (ex: `BoletosVencidos`)
- Seu próprio conjunto de nodes (TableSource, JOINs, WHERE, GROUP BY, SELECT)
- Um output que representa a "tabela virtual" resultante
- Possibilidade de referenciar CTEs anteriores

```
┌─── CTE: BoletosVencidos ───────────────┐
│                                        │
│  [Boleto]──JOIN──[SituacaoBoleto]      │
│       │                                │
│  [WHERE sit.id=3 AND dtVenc<GETDATE()]  │
│       │                                │
│  [GROUP BY id_processo, parcela]       │
│       │                                │
│  [SELECT: id_processo, parcela, MAX]   │
│                                        │
└─────────────────────── ○ virtual_table ┘
        │
        ▼ (output "virtual table" pin)
┌─── CTE: ComGrupo ──────────────────────┐
│   FROM BoletosVencidos                 │
│   + ROW_NUMBER() OVER(...)             │
│   + parcela - ROW_NUMBER()             │
└─────────────────────── ○ virtual_table ┘
```

---

### 3.2 — Abordagem de Implementação: CTE como Node Container

**Opção A — CteContainer (recomendada):**
Um node especial `CteDefinition` que contém um grafo interno completo.

```
CteDefinition node:
├─ Name: "BoletosVencidos"
├─ Inner Graph: NodeGraph completo (TableSources, JOINs, etc.)
└─ Output pin: "virtual_table" (PinDataType.Any → referência à CTE)
```

**Opção B — CteCanvas (canvas separado por aba):**
Cada CTE é uma aba/canvas separada. A tab principal referencia por nome.
- Mais familiar (como subrotinas em IDEs)
- Mais complexo no modelo de dados

**Recomendação: Opção A (mais natural para o paradigma node-based atual)**

---

### 3.3 — Novos NodeTypes

```csharp
CteDefinition,   // container do subgrafo — define a CTE
CteSource,       // referencia uma CTE pelo nome — como TableSource mas para CTEs
```

**`CteDefinition` node:**
```csharp
[NodeType.CteDefinition] = new(
    NodeType.CteDefinition,
    NodeCategory.DataSource,
    "CTE Definition",
    "Defines a named Common Table Expression (WITH ... AS)",
    [
        In("query",  PinDataType.Any, required: true),    // output de um ResultOutput interno
        Out("table", PinDataType.Any),                     // "virtual table" para uso em outros nodes
    ],
    [
        Param("name",      ParameterKind.Text, "cte_name", "Name of this CTE"),
        Param("recursive", ParameterKind.Boolean, "false", "RECURSIVE CTE (self-referential)"),
    ]
)
```

**`CteSource` node:**
```csharp
[NodeType.CteSource] = new(
    NodeType.CteSource,
    NodeCategory.DataSource,
    "CTE Source",
    "References a previously defined CTE as a table source",
    [
        In("cte",    PinDataType.Any, required: true),   // conecta ao output do CteDefinition
        Out("*",     PinDataType.Any),                    // expõe colunas da CTE
    ],
    [
        Param("alias", ParameterKind.Text, "", "Table alias (e.g., pe for ProcessosElegiveis)"),
    ]
)
```

---

### 3.4 — Mudanças no Motor de Compilação

**`NodeGraph.cs`** — adicionar lista de CTEs:
```csharp
public record CteDefinition(
    string Name,
    NodeGraph SubGraph,   // grafo interno da CTE
    bool Recursive
);

// Em NodeGraph:
public IReadOnlyList<CteDefinition> Ctes { get; init; } = [];
```

**`NodeGraphCompiler.cs`** — compilar CTEs em ordem topológica:
```csharp
// 1. Identificar todos os CteDefinition nodes
// 2. Ordenar por dependência (CTE que referencia outra CTE vem depois)
// 3. Compilar cada subgrafo recursivamente → CompiledCte
// 4. Retornar lista ordenada de CTEs para o gerador

public sealed record CompiledCte(
    string Name,
    CompiledNodeGraph SubQuery,
    bool Recursive
);
```

**`ISqlExpression.cs`** — nova expressão para referência a CTE:
```csharp
public sealed record CteReferenceExpr(string CteName, string? Alias) : ISqlExpression
{
    public string Emit(EmitContext ctx) =>
        Alias is null ? CteName : $"{CteName} {Alias}";
    public PinDataType OutputType => PinDataType.Any;
}
```

**`QueryGeneratorService.cs`** — emitir bloco WITH:
```csharp
private string BuildCteBlock(IReadOnlyList<CompiledCte> ctes, EmitContext ctx)
{
    if (ctes.Count == 0) return "";

    var parts = ctes.Select(cte =>
    {
        string keyword = cte.Recursive ? "RECURSIVE " : "";
        string subSql = GenerateSubQuery(cte.SubQuery, ctx);
        return $"{cte.Name} AS (\n{Indent(subSql)}\n)";
    });

    return "WITH " + string.Join("\n   , ", parts) + "\n";
}
```

**SQL emitido:**
```sql
WITH BoletosVencidos AS (
    SELECT b.id_processo_refis, b.parcela, MAX(b.dtVencimento) AS dtVencimento
    FROM [Boleto] b
    INNER JOIN [SituacaoBoleto] sit ON b.id_situacao = sit.id
    WHERE sit.id = 3 AND b.dtVencimento < GETDATE()
    GROUP BY b.id_processo_refis, b.parcela
)
, ComGrupo AS (
    ...
)
SELECT ...
```

---

### 3.5 — Visual UX dos CTEs

**No canvas:**
- `CteDefinition` node aparece com visual diferenciado — borda tracejada + header azul-escuro
- O node é **expansível** — clicar abre o subgrafo interno em uma "sub-view" do canvas
- Quando recolhido, mostra as colunas do output como pinos resumidos
- Linha de conexão entre CTEs tem estilo diferente (tracejado)

**Fluxo de criação:**
1. Usuário adiciona `CteDefinition` node via ⇧A
2. Double-click abre o sub-canvas da CTE
3. Usuário monta o grafo interno normalmente (TableSource → JOINs → WHERE → GROUP BY → SELECT)
4. O output do ResultOutput interno alimenta o pin `query` da CteDefinition
5. O output `table` da CteDefinition pode ser conectado a um `CteSource` no canvas principal

**Breadcrumb de navegação:**
```
[Canvas Principal] › [CTE: BoletosVencidos]   ← header quando dentro de uma CTE
```

---

## FASE 4 — JOINs Visuais com Aliases

> Atualmente JOINs são inferidos por metadata. Queries complexas precisam de controle explícito.

---

### 4.1 — JOIN Node Visual

**Novo NodeType `Join`:**
```csharp
[NodeType.Join] = new(
    NodeType.Join,
    NodeCategory.DataSource,
    "Join",
    "Explicitly joins two table sources",
    [
        In("left",       PinDataType.Any, required: true),   // tabela/CTE da esquerda
        In("right",      PinDataType.Any, required: true),   // tabela/CTE da direita
        In("condition",  PinDataType.Boolean, required: true), // ON condition
        Out("result",    PinDataType.Any),                    // tabela combinada
    ],
    [
        Param("type", ParameterKind.Enum, "INNER",
              values: ["INNER","LEFT","RIGHT","FULL","CROSS"]),
    ]
)
```

**Visual:**
```
┌──────────────────────────────────┐
│  ⊞  Join            [INNER ▼]   │
├──────────────────────────────────┤
│  left      ──────○               │
│  right     ──────○               │
│  condition ──────○  (ON clause)  │
├──────────────────────────────────┤
│                          ○────── result
└──────────────────────────────────┘
```

**Para a query de referência (6 JOINs):**
```
[ProcessosElegiveis pe]──┐
                         ├── JOIN(INNER) ──┐
[Boleto b] ──────────────┘  ON b.id_processo_refis=pe.id_processo_refis
                                           │
[SituacaoBoleto sit]─────┐                 ├── JOIN(INNER) ...
                         ├── JOIN(INNER) ──┘
[sit.id = 3 condition] ──┘
```

---

### 4.2 — Aliases de Tabela

Cada `TableSource` e `CteSource` precisa de um parâmetro de alias:

**`NodeDefinition.cs`** — TableSource já tem `alias` via Alias node; simplificar para parâmetro direto:
```csharp
Param("alias", ParameterKind.Text, "", "Table alias (e.g., 'b' for Boleto b)")
```

**`ColumnExpr`** — atualizar para usar alias quando disponível:
```csharp
public sealed record ColumnExpr(string Table, string Column, string? TableAlias) : ISqlExpression
{
    public string Emit(EmitContext ctx)
    {
        string tableRef = TableAlias ?? ctx.QuoteIdentifier(Table);
        return $"{tableRef}.{ctx.QuoteIdentifier(Column)}";
    }
}
```

---

## FASE 5 — Subqueries e Queries Aninhadas

> Generaliza o conceito de CTE para qualquer posição: FROM, WHERE IN, EXISTS.

---

### 5.1 — Subquery como Fonte (FROM subquery)

```sql
SELECT * FROM (SELECT ...) AS sub
```

**Novo NodeType `Subquery`:**
- Similar ao `CteDefinition` mas sem nome — inline no FROM
- Parâmetro `alias` obrigatório
- Output `table` pin

---

### 5.2 — Subquery em Condição (WHERE col IN (SELECT ...))

```sql
WHERE id IN (SELECT id FROM ...)
```

**Novos NodeTypes:**
```csharp
SubqueryIn,      // col IN (SELECT ...)
SubqueryExists,  // EXISTS (SELECT ...)
SubqueryScalar,  // col = (SELECT scalar)
```

---

## FASE 6 — Melhorias Adicionais de Qualidade

---

### 6.1 — UNION / UNION ALL / INTERSECT / EXCEPT

**Novo NodeType `SetOperation`:**
```csharp
[NodeType.SetOperation] = new(...)
// Parâmetros: type = [UNION, UNION_ALL, INTERSECT, EXCEPT]
// Inputs: left_query, right_query
// Output: combined_table
```

---

### 6.2 — PIVOT / UNPIVOT

Para transformações de linhas em colunas (SQL Server específico).

---

### 6.3 — Query Hints (NOLOCK, INDEX, etc.)

```sql
FROM Boleto b WITH (NOLOCK)
```

Parâmetro `hints` no TableSource — array de strings, dialect-aware.

---

### 6.4 — QUALIFY (Trino/BigQuery) e FILTER OVER

Window functions com filtragem pós-OVER.

---

## Mudanças por Camada — Resumo Técnico

### Camada 1: `ISqlExpression.cs` — Novos tipos

| Novo tipo | Fase |
|-----------|------|
| `WindowFunctionExpr` | 2 |
| `WindowFrame`, `WindowFrameBound` | 2 |
| `StringAggExpr` | 1 |
| `SystemDateExpr` | 1 |
| `CteReferenceExpr` | 3 |
| `SubqueryExpr` | 5 |
| `SetOperationExpr` | 6 |

### Camada 2: `NodeDefinition.cs` — Novos NodeTypes e modificações

| Node | Ação | Fase |
|------|------|------|
| `WindowFunction` | Criar | 2 |
| `StringAgg` | Criar | 1 |
| `SystemDate`, `CurrentDate` | Criar | 1 |
| `CteDefinition`, `CteSource` | Criar | 3 |
| `Join` | Criar | 4 |
| `CompileHaving` | Criar | 1 |
| `Subquery`, `SubqueryIn`, etc. | Criar | 5 |
| `ResultOutput` | + pin `having` + param `distinct` | 1 |
| `TableSource` | + param `alias` simplificado | 4 |

### Camada 3: `NodeGraphCompiler.cs` e Compilers

| Compiler | Ação | Fase |
|----------|------|------|
| `WindowFunctionCompiler` | Criar | 2 |
| `StringAggCompiler` | Criar (ou expandir AggregateCompiler) | 1 |
| `SystemDateCompiler` | Criar (LiteralCompiler ou novo) | 1 |
| `CteCompiler` | Criar (recursivo) | 3 |
| `JoinCompiler` | Criar | 4 |
| `HavingCompiler` | Criar | 1 |
| `NodeGraphCompiler` | + suporte a CTEs aninhadas | 3 |

### Camada 4: `NodeGraph.cs`

| Campo | Ação | Fase |
|-------|------|------|
| `HavingBinding[]` | Adicionar | 1 |
| `bool Distinct` | Adicionar | 1 |
| `CteDefinition[]` | Adicionar | 3 |

### Camada 5: `QueryGeneratorService.cs`

| Método | Ação | Fase |
|--------|------|------|
| `ApplyHavings()` | Criar | 1 |
| `BuildCteBlock()` | Criar | 3 |
| `ApplyJoins()` | Atualizar para JOIN node explícito | 4 |

### Camada 6: `QueryGraphBuilder.cs` (UI → Engine)

| Mudança | Fase |
|---------|------|
| Extrair `HavingBindings` | 1 |
| Extrair `Distinct` flag | 1 |
| Compilar `CteDefinition` nodes recursivamente | 3 |
| Extrair `Join` nodes explícitos | 4 |

### Camada 7: UI / AXAML

| Mudança | Fase |
|---------|------|
| Pin `having` no ResultOutput (cor âmbar) | 1 |
| Checkbox `Distinct` no painel de propriedades | 1 |
| Window Function node com pins variádicos | 2 |
| `CteDefinition` node com sub-canvas | 3 |
| Breadcrumb de navegação no canvas | 3 |
| Join node visual | 4 |

---

## Ordem de Implementação Recomendada

| # | Feature | Complexidade | Impacto | Fase |
|---|---------|-------------|---------|------|
| 1 | HAVING clause | Baixa | Alto | 1 |
| 2 | SELECT DISTINCT | Muito Baixa | Médio | 1 |
| 3 | GETDATE() / NOW() | Baixa | Médio | 1 |
| 4 | STRING_AGG | Baixa | Médio | 1 |
| 5 | Date arithmetic nodes | Baixa | Médio | 1 |
| 6 | Window Functions | Alta | Alto | 2 |
| 7 | Pins variádicos para PARTITION/ORDER | Média | Médio | 2 |
| 8 | CteDefinition + CteSource nodes | Muito Alta | Muito Alto | 3 |
| 9 | Sub-canvas para CTEs | Muito Alta | Muito Alto | 3 |
| 10 | CTE compilation (recursiva) | Alta | Muito Alto | 3 |
| 11 | JOIN node visual | Alta | Alto | 4 |
| 12 | Alias por TableSource | Média | Alto | 4 |
| 13 | Subquery no FROM | Alta | Médio | 5 |
| 14 | WHERE IN (subquery) | Alta | Médio | 5 |
| 15 | UNION / UNION ALL | Média | Médio | 6 |

---

## Estimativa de Esforço

| Fase | Feature Principal | Esforço Estimado |
|------|------------------|-----------------|
| 1 | HAVING, DISTINCT, GETDATE, STRING_AGG | ~2–3 dias |
| 2 | Window Functions | ~5–7 dias |
| 3 | CTEs completas (sub-canvas) | ~10–15 dias |
| 4 | JOINs visuais + aliases | ~4–5 dias |
| 5 | Subqueries | ~5–7 dias |
| 6 | UNION, PIVOT, hints | ~3–5 dias |

**Para executar a query de referência exatamente:**
Fases 1 + 2 + 3 + 4 são necessárias.

---

## Pontos Arquiteturais Críticos

1. **Sub-canvas para CTEs** é a maior decisão de UX e arquitetura. Errar aqui é difícil de
   reverter. Validar o fluxo com usuário antes de implementar.

2. **Window functions com pins variádicos** — o sistema de pins atualmente tem quantidade fixa.
   Implementar variadic inputs (como AND/OR já faz com `AllowMultiple`) é pré-requisito.

3. **Aliases de tabela** mudam como `ColumnExpr` referencia colunas em todo o sistema.
   Testar exaustivamente queries existentes após essa mudança.

4. **Compilação recursiva de CTEs** — o `NodeGraphCompiler` precisa suportar subgrafos
   aninhados sem ciclos. O sort topológico já existe, mas não para subgrafos.

5. **SqlKata** (biblioteca atual de geração SQL) suporta CTEs via `.With()`. Verificar se
   suporta window functions; se não, emitir via `RawExpression` no bloco SELECT.
