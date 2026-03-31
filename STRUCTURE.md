# Estrutura do Projeto Visual SQL Architect

## Organização da Solução

```
.
├── files.sln                      # Arquivo de solução principal
├── README.md                      # Documentação do projeto
├── STRUCTURE.md                   # Este arquivo
│
├── src/                           # Código-fonte principal
│   ├── VisualSqlArchitect/        # Projeto Core (VisualSqlArchitect.csproj)
│   │   ├── Core/                  # Orquestradores principais
│   │   │   ├── BaseDbOrchestrator.cs
│   │   │   └── IDbOrchestrator.cs
│   │   │
│   │   ├── Metadata/              # Serviços de metadados
│   │   │   ├── MetadataService.cs
│   │   │   ├── DbMetadata.cs
│   │   │   ├── IDatabaseInspector.cs
│   │   │   ├── AutoJoinDetector.cs
│   │   │   └── Inspectors/        # Adaptadores para cada banco
│   │   │       ├── MySqlInspector.cs
│   │   │       ├── PostgresInspector.cs
│   │   │       └── SqlServerInspector.cs
│   │   │
│   │   ├── Nodes/                 # Modelo de nós do grafo
│   │   │   ├── NodeDefinition.cs
│   │   │   ├── NodeGraph.cs
│   │   │   ├── NodeGraphCompiler.cs
│   │   │   └── ISqlExpression.cs
│   │   │
│   │   ├── Providers/             # Orquestradores específicos por BD
│   │   │   ├── MySqlOrchestrator.cs
│   │   │   ├── PostgresOrchestrator.cs
│   │   │   └── SqlServerOrchestrator.cs
│   │   │
│   │   ├── QueryEngine/           # Serviços de construção de queries
│   │   │   ├── QueryBuilderService.cs
│   │   │   └── QueryGeneratorService.cs
│   │   │
│   │   ├── Registry/              # Registro de funções SQL
│   │   │   └── SqlFunctionRegistry.cs
│   │   │
│   │   ├── ServiceRegistration.cs # Configuração de injeção dependência
│   │   └── VisualSqlArchitect.csproj
│   │
│   └── VisualSqlArchitect.UI/     # Projeto UI (VisualSqlArchitect.UI.csproj)
│       ├── App.axaml              # Arquivo XAML principal
│       ├── App.axaml.cs           # Code-behind da aplicação
│       ├── MainWindow.axaml       # Janela principal
│       ├── MainWindow.axaml.cs
│       │
│       ├── Assets/                # Recursos
│       │   └── Themes/            # Temas de cores
│       │       ├── AppStyles.axaml
│       │       └── DesignTokens.axaml
│       │
│       ├── Controls/              # Controles Avalonia customizados
│       │   ├── NodeControl.axaml
│       │   ├── NodeControl.axaml.cs
│       │   ├── InfiniteCanvas.cs
│       │   ├── PropertyPanelControl.axaml
│       │   ├── PropertyPanelControl.axaml.cs
│       │   ├── LiveSqlBar.axaml
│       │   ├── LiveSqlBar.axaml.cs
│       │   ├── AutoJoinOverlay.axaml
│       │   ├── AutoJoinOverlay.axaml.cs
│       │   ├── SearchMenuControl.axaml
│       │   ├── SearchMenuControl.axaml.cs
│       │   ├── BezierWireLayer.cs
│       │   └── PinDragInteraction.cs
│       │
│       ├── ViewModels/            # ViewModels MVVM
│       │   ├── CanvasViewModel.cs
│       │   ├── LiveSqlBarViewModel.cs
│       │   ├── PropertyPanelViewModel.cs
│       │   ├── AutoJoinOverlayViewModel.cs
│       │   └── UndoRedoStack.cs
│       │
│       ├── Serialization/         # Serviços de serialização
│       │   └── CanvasSerializer.cs
│       │
│       ├── DataPreviewPanel.axaml
│       └── VisualSqlArchitect.UI.csproj
│
└── tests/                         # Projetos de testes
    └── VisualSqlArchitect.Tests/  # Projeto de Testes (VisualSqlArchitect.Tests.csproj)
        ├── ArchitectureTests.cs
        ├── AtomicNodeTests.cs
        ├── MetadataTests.cs
        └── VisualSqlArchitect.Tests.csproj
```

## Namespaces

### Core (VisualSqlArchitect.csproj)
- `VisualSqlArchitect.Core` - Interfaces e classes base
- `VisualSqlArchitect.Metadata` - Serviços de metadados
- `VisualSqlArchitect.Metadata.Inspectors` - Inspetores específicos por BD
- `VisualSqlArchitect.Nodes` - Modelo de nós
- `VisualSqlArchitect.Providers` - Implementações de orquestradores
- `VisualSqlArchitect.QueryEngine` - Serviços de queries
- `VisualSqlArchitect.Registry` - Registro de funções

### UI (VisualSqlArchitect.UI.csproj)
- `VisualSqlArchitect.UI` - Código principal da aplicação
- `VisualSqlArchitect.UI.Controls` - Controles customizados
- `VisualSqlArchitect.UI.ViewModels` - ViewModels
- `VisualSqlArchitect.UI.Serialization` - Serviços de serialização

### Testes (VisualSqlArchitect.Tests.csproj)
- `VisualSqlArchitect.Tests` - Testes unitários

## Dependências do Projeto

### Visual SQL Architect (Core)
- ✅ Independente de UI
- Depende de: SqlKata, database drivers

### Visual SQL Architect.UI
- Depende de: Core + Avalonia

### Visual SQL Architect.Tests
- Depende de: Core + xUnit

## Compilação

```bash
# Build completo
dotnet build

# Build apenas do Core
dotnet build src/VisualSqlArchitect/VisualSqlArchitect.csproj

# Build apenas da UI
dotnet build src/VisualSqlArchitect.UI/VisualSqlArchitect.UI.csproj

# Build e executa testes
dotnet test
```
