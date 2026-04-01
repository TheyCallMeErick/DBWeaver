# Matriz Minima de Cenarios Criticos

Data: 2026-04-01
Escopo: TD-006

## Objetivo

Cobrir lacunas prioritarias com testes iniciais executaveis e reproduziveis.

## Matriz

| Area | Cenario critico | Tipo de teste | Arquivo |
|---|---|---|---|
| UI Controls | NodeControl encontra CanvasViewModel no host logico e nao quebra em hooks sem botoes | Unit | tests/VisualSqlArchitect.Tests/Unit/Controls/NodeControlBehaviorTests.cs |
| UI Controls | InfiniteCanvas cria estrutura base e sincroniza cache de controles ao vincular ViewModel | Unit | tests/VisualSqlArchitect.Tests/Unit/Controls/InfiniteCanvasBindingTests.cs |
| Drag | Reroute/cancel e estabilidade de slots dinamicos | Unit | tests/VisualSqlArchitect.Tests/Unit/Controls/PinDragInteractionTests.cs |
| Excecoes | Load de stores com JSON corrompido gera warning e fallback seguro | Unit | tests/VisualSqlArchitect.Tests/Unit/Serialization/StoreCorruptionFallbackTests.cs |
| Banco real (smoke) | SqliteOrchestrator conecta, introspecta schema e executa preview em arquivo SQLite real | Integration | tests/VisualSqlArchitect.Tests/Integration/SqliteOrchestratorSmokeIntegrationTests.cs |
| Performance baseline | BuildSql em grafo grande (nodos extras) sem regressao severa de tempo | Unit/Performance | tests/VisualSqlArchitect.Tests/Unit/Performance/QueryGraphBuilderBenchmarkSmokeTests.cs |

## Criterio minimo de sucesso

- Todos os testes da matriz passam localmente.
- Build da solucao passa sem regressao.
- Full suite permanece verde.
