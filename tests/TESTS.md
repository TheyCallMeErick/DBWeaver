# Testes - Estrutura e Organização

## Visão Geral

O projeto de testes está organizado em **categorias lógicas** com fixtures compartilhadas para evitar duplicação de código.

```
tests/── Fixtures/                 # Fixtures compartilhadas
│   └── TestFixtures.cs       # Builders e helpers para testes
│
├── Unit/                     # Testes unitários
│   ├── Metadata/
│   │   └── MetadataServiceTests.cs
│   ├── Nodes/
│   │   └── NodeEmissionTests.cs
│   └── Queries/
│       └── SqlFunctionRegistryTests.cs
│
├── Integration/              # Testes de integração (futuro)
│
└── roj
```

## Namespaces

- **tures** - Fixtures compartilhadas
- **t.Metadata** - Testes de metadados
- **t.Nodes** - Testes de emissão de nós
- **t.Queries** - Testes de queries

## Arquivos de Teste

### 1. `Unit/Nodes/NodeEmissionTests.cs`
Testes para compilação e emissão de nós SQL.

**Fixtures disponíveis em `TestFixtures.Node`:**
- `PostgresContext`, `MySqlContext`, `SqlServerContext` - EmitContext para cada provedor
- `Column()` - Cria expressões de coluna
- `OrderTotal`, `UserEmail`, `EventPayload` - Colunas pré-definidas

### 2. `Unit/Metadata/MetadataServiceTests.cs`
Testes para inspeção e detecção automática de junções.

**Fixtures disponíveis em `TestFixtures.Metadata`:**
- `Column()` - Cria metadados de coluna
- `ForeignKey()` - Cria relações FK
- `Table()` - Cria tabelas completas
- `CreateEcommerceSchema()` - Schema completo de e-commerce para testes

### 3. `Unit/Queries/SqlFunctionRegistryTests.cs`
Testes para o registro de funções SQL por provedor.

**Fixtures disponíveis em `TestFixtures`:**
- Contextos de emissão
- Registros de funções SQL

## Como Usar as Fixtures

### Exemplo 1: Testar emissão de nó em PostgreSQL
```csharp
public class MyNodeTests
{
    [Fact]
    public void MyTest()
    {
        var ctx = TestFixtures.Node.PostgresContext;
        var column = TestFixtures.Node.OrderTotal;

        // seu teste aqui
    }
}
```

### Exemplo 2: Testar metadados com schema e-commerce
```csharp
public class MyMetadataTests
{
    [Fact]
    public void MyTest()
    {
        var schema = TestFixtures.Metadata.CreateEcommerceSchema();
        var ordersTable = schema.FindTable("public", "orders");

        // seu teste aqui
    }
}
```

## Executar Testes

```bash
# Todos os testes
dotnet test

# Apenas categoria específica
dotnet test --filter "Namespace=t.Nodes"

# Com saída verbosa
dotnet test --verbosity=detailed
```

## Futuro: Testes de Integração

A pasta `Integration/` está reservada para testes que:
- Conectam a bancos de dados reais (Docker)
- Testam fluxos completos end-to-end
- Validam comportamento de múltiplos componentes juntos

## Matriz de Cobertura - Hardening QueryPreview

### Cobertura por categoria

| Categoria | Regras cobertas | Arquivos de teste |
|---|---|---|
| Type mismatch | Compatibilidade de pinos e conexoes invalidas | `tests/t/ViewModels/QueryPreview/QueryGraphBuilderConnectionValidationTests.cs` |
| Predicate | AND/OR/COMPILE WHERE redundante ou vazio; NOT sem condition (ativo) | `tests/t/ViewModels/QueryPreview/QueryGraphBuilderLogicGateValidationTests.cs`; `tests/DBWeaver.Tls/QueryPreview/QueryGraphBuilderNotAndJsonValidationTests.cs` |
| Comparison | Inputs obrigatorios de comparacao; LIKE sem pattern | `tests/t/ViewModels/QueryPreview/QueryGraphBuilderComparisonValidationTests.cs`; `tests/DBWeaver.Tls/QueryPreview/QueryGraphBuilderQualifyTests.cs` |
| Window | value/order/frame/offset/ntile validation | `tests/t/ViewModels/QueryPreview/QueryGraphBuilderWindowFunctionTests.cs` |
| CTE | nome, escopo, from inference, recursive prefix, CTE source alias | `tests/t/ViewModels/QueryPreview/QueryGraphBuilderCteTests.cs` |
| Join | Join explicito por tipo; join incompleto; fallback legado | `tests/t/ViewModels/QueryPreview/QueryGraphBuilderJoinTests.cs` |
| Subquery | shape SQL, alias handling e validacoes de subquery nodes | `tests/t/ViewModels/QueryPreview/QueryGraphBuilderSubqueryTests.cs` |
| Set operation | operador suportado, query obrigatoria, shape SELECT | `tests/t/ViewModels/QueryPreview/QueryGraphBuilderSetOperationTests.cs` |
| Query hints | hints por provider e validacao de sintaxe | `tests/t/ViewModels/QueryPreview/QueryGraphBuilderQueryHintsTests.cs` |
| Pivot | pivot/unpivot provider-aware e configuracao invalida | `tests/t/ViewModels/QueryPreview/QueryGraphBuilderPivotTests.cs` |
| Diagnostics estruturados | severidade/categoria/code + compatibilidade com mensagens legadas | `tests/t/ViewModels/QueryPreview/PreviewDiagnosticMapperTests.cs`; `tests/DBWeaver.Tls/QueryPreview/QueryGraphBuilderDiagnosticsTests.cs` |
| Paginacao | TOP/LIMIT <= 0; OFFSET sem ORDER BY deterministico | `tests/t/ViewModels/QueryPreview/QueryGraphBuilderPaginationValidationTests.cs` |
| Alias ambiguo | duplicidade de alias no mesmo escopo; sem falso positivo cross-scope | `tests/t/ViewModels/QueryPreview/QueryGraphBuilderAliasAmbiguityTests.cs` |

### Regras criticas de warning (minimo 1 teste por regra)

| Regra critica | Coberto por |
|---|---|
| Conexao de tipos incompativeis | `QueryGraphBuilderConnectionValidationTests` |
| Predicate vazio/redundante em WHERE/HAVING/QUALIFY | `QueryGraphBuilderLogicGateValidationTests` |
| Comparacao sem input obrigatorio | `QueryGraphBuilderComparisonValidationTests` |
| Window sem inputs obrigatorios/frame invalido | `QueryGraphBuilderWindowFunctionTests` |
| CTE mal configurada (nome/escopo/fonte) | `QueryGraphBuilderCteTests` |
| Join incompleto/tipo invalido | `QueryGraphBuilderJoinTests` |
| Subquery invalida (shape/alias) | `QueryGraphBuilderSubqueryTests` |
| Set operation invalida (operator/query) | `QueryGraphBuilderSetOperationTests` |
| Query hints invalidos por provider | `QueryGraphBuilderQueryHintsTests` |
| Pivot configuracao invalida/provider incompatível | `QueryGraphBuilderPivotTests` |
| NOT/JSON sem input ou path invalido (quando ativo) | `QueryGraphBuilderNotAndJsonValidationTests` |
| TOP/LIMIT <= 0 e OFFSET sem ORDER BY | `QueryGraphBuilderPaginationValidationTests` |
| Alias duplicado no mesmo escopo logico | `QueryGraphBuilderAliasAmbiguityTests` |
| Diagnostico estruturado com code/category/severity | `PreviewDiagnosticMapperTests`; `QueryGraphBuilderDiagnosticsTests` |

## Snapshot critico de diagnosticos

- Escopo: apenas codigos de diagnostico criticos do preview, evitando snapshot fragil de SQL completo.
- Arquivo snapshot: `tests/tures/Snapshots/querypreview-diagnostic-codes.snap`.
- Teste verificador: `tests/t/ViewModels/QueryPreview/QueryPreviewDiagnosticSnapshotTests.cs`.

### Politica de atualizacao do snapshot

1. Atualize o snapshot somente quando houver mudanca intencional de codigos de diagnostico (ex.: nova regra de hardening ou renomeacao deliberada de codigo).
2. Nao atualize snapshot para mascarar regressao; primeiro confirme a causa e ajuste o codigo/teste de regra.
3. Em PR, descreva quais codigos mudaram e por que.
4. Mantenha foco em codigos/mensagens criticas; nao adicionar snapshot de SQL inteiro quando nao for estritamente necessario.
