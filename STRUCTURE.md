# Estrutura do Projeto DBWeaver

## Organização da Solução

```
.
├── files.sln                      # Arquivo de solução principal
├── README.md                      # Documentação do projeto
├── STRUCTURE.md                   # Este arquivo
│
├── src/                           # Código-fonte principal
│   ├──  Projeto Core (DBWeaver.c─ Core/                  # Orquestradores principais
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
│   │   └──    │
│   └──  Projeto UI (DBWeaver.U ├── App.axaml              # Arquivo XAML principal
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
│       └──
│
└── tests/                         # Projetos de testes
    └──  Projeto de Testes (DBWeaver.T    ├── ArchitectureTests.cs
        ├── AtomicNodeTests.cs
        ├── MetadataTests.cs
        └── roj
```

## Namespaces

### Core (- `nterfaces e classes base
- ` - Serviços de metadados
- `Inspectors` - Inspetores específicos por BD
- `Modelo de nós
- `` - Implementações de orquestradores
- `ne` - Serviços de queries
- ` - Registro de funções

### UI ()
- `igo principal da aplicação
- `ls` - Controles customizados
- `dels` - ViewModels
- `ization` - Serviços de serialização

### Testes (roj)
- `Testes unitários

## Dependências do Projeto

### DBWeaver (Core)
- ✅ Independente de UI
- Depende de: SqlKata, database drivers

### DBWeaver.UI
- Depende de: Core + Avalonia

### DBWeaver.Tests
- Depende de: Core + xUnit

## Compilação

```bash
# Build completo
dotnet build

# Build apenas do Core
dotnet build src/cpenas da UI
dotnet build src/er.Ud e executa testes
dotnet test
```
