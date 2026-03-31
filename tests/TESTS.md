# Testes - Estrutura e Organização

## Visão Geral

O projeto de testes está organizado em **categorias lógicas** com fixtures compartilhadas para evitar duplicação de código.

```
tests/VisualSqlArchitect.Tests/
├── Fixtures/                 # Fixtures compartilhadas
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
└── VisualSqlArchitect.Tests.csproj
```

## Namespaces

- **VisualSqlArchitect.Tests.Fixtures** - Fixtures compartilhadas
- **VisualSqlArchitect.Tests.Unit.Metadata** - Testes de metadados
- **VisualSqlArchitect.Tests.Unit.Nodes** - Testes de emissão de nós
- **VisualSqlArchitect.Tests.Unit.Queries** - Testes de queries

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
dotnet test --filter "Namespace=VisualSqlArchitect.Tests.Unit.Nodes"

# Com saída verbosa
dotnet test --verbosity=detailed
```

## Futuro: Testes de Integração

A pasta `Integration/` está reservada para testes que:
- Conectam a bancos de dados reais (Docker)
- Testam fluxos completos end-to-end
- Validam comportamento de múltiplos componentes juntos
