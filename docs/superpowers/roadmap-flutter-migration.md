# Roadmap — Migração Flutter (DBWeaver)

**Última atualização:** 2026-04-05
**Plataformas:** Windows + Linux desktop
**Stack:** Flutter 3.41 · GetX · gRPC · .NET 9
**Fora de escopo permanente:** Rubber-band selection, compatibilidade com `.vsaq` antigos

---

## Como ler este documento

Cada fase lista **requisitos** (o que deve ser construído), **condições de conclusão** (como saber que está feito), e a **dependência** que precisa estar pronta antes. O checklist final agrega todas as tarefas em ordem.

Uma fase só pode iniciar quando todas as condições de conclusão da fase anterior forem verificadas.

---

## Fase 1 — Canvas + Pins + gRPC Foundation

**Dependência:** nenhuma
**Plano:** `docs/superpowers/plans/2026-04-05-flutter-grpc-migration-phase1.md`

### Requisitos

| # | Requisito |
|---|---|
| 1.1 | 5 arquivos `.proto` definidos (`common`, `node_definition`, `sql_compiler`, `database`, `schema`) |
| 1.2 | Projeto `DBWeaver.GrpcServer` compilando com 4 serviços mapeados |
| 1.3 | `READY:<port>` emitido no stdout após inicialização do Kestrel |
| 1.4 | `NodeDefinitionGrpcService` serializa todos os tipos de nó do `NodeDefinitionRegistry` |
| 1.5 | `SqlCompilerGrpcService` mapeia protobuf → `NodeGraph` → `QueryGeneratorService` |
| 1.6 | `DatabaseGrpcService` expõe Connect, Disconnect, ExecuteQuery, Ping |
| 1.7 | `SchemaGrpcService` expõe GetSchema |
| 1.8 | Flutter scaffold com `pubspec.yaml`, GetX, stubs gRPC gerados via `protoc` |
| 1.9 | Modelos Dart: `PinRef`, `PinDescriptorModel`, `NodeDefinitionModel`, `NodeModel`, `WireModel` |
| 1.10 | `ServerProcessService` inicia processo filho, lê `READY:<port>` do stdout com timeout de 10s |
| 1.11 | `CanvasController` com: `addNode`, `removeNode`, `moveNode`, `connectPins`, `validateConnection`, `selectNode` |
| 1.12 | `ViewportController` com Matrix4 transform, `screenToCanvas`, `canvasToScreen`, zoom clampado entre 0.1–5.0 |
| 1.13 | `PinDragController` com: `startDrag`, `updateDrag`, `commit` (cria wire), `cancel` |
| 1.14 | `BezierWirePainter` portando algoritmo de `CanvasWireGeometry.cs` (control offset 60–200) |
| 1.15 | `DotGridPainter` (grid de fundo que acompanha zoom/pan) |
| 1.16 | `DraftWirePainter` (wire cinza durante drag de pin) |
| 1.17 | `NodeWidget` — draggable, exibe nome, pinos com cor por tipo |
| 1.18 | `PinWidget` — reporta posição global via callback após layout |
| 1.19 | `CanvasScreen` — Stack de 4 camadas + GestureDetector de zoom/pan |

### Condições de conclusão

- [ ] `dotnet test tests/DBWeaver.Tests/` — 1796 testes existentes passando, zero regressões
- [ ] `dotnet test tests/DBWeaver.GrpcServer.Tests/` — contrato: `GetAll` retorna ≥ 60 definições, `api_version = 1`
- [ ] `dotnet test tests/DBWeaver.GrpcServer.Tests/` — contrato: `CompileToSql` com TableSource + SelectBinding retorna SQL com SELECT
- [ ] `flutter test test/` — todos os unit tests de controllers passando
- [ ] `flutter test test/widgets/bezier_wire_painter_test.dart` — golden test aprovado
- [ ] `flutter test test/widgets/canvas_screen_test.dart` — canvas renderiza, nó aparece após `addNode`, GestureDetector não lança exceção
- [ ] Integration test: servidor inicia em < 3s, Flutter conecta, `GetAll` responde
- [ ] Integration test: conecta SQLite → `CompileToSql` retorna SQL com `SELECT`

---

## Fase 2 — Conexão + Schema + LiveSQL Bar

**Dependência:** Fase 1 concluída
**O que desbloqueará:** Fase 3 (paleta precisa do schema para sugerir tabelas)

### Requisitos

| # | Requisito |
|---|---|
| 2.1 | **Tela de conexão** — formulário com: provider (dropdown: SQL Server, MySQL, PostgreSQL, SQLite), host, porta, database, usuário, senha, autenticação integrada (Windows), timeout |
| 2.2 | Tela de conexão tem botão "Testar conexão" que chama `DatabaseService.Ping` antes de confirmar |
| 2.3 | Após conexão bem-sucedida, shell exibe status (provider + database + latência) na barra superior |
| 2.4 | **Sidebar de schema** — lista hierárquica: categorias de tabelas → tabelas → colunas com tipo e ícone de PK/FK |
| 2.5 | Schema sidebar tem campo de busca que filtra tabelas e colunas em tempo real (sem round-trip ao servidor) |
| 2.6 | Arrastar uma tabela da sidebar para o canvas cria um `TableSourceNode` na posição de drop com `tableFullName` preenchido |
| 2.7 | **LiveSQL Bar** — barra colapsável abaixo do canvas que exibe o SQL atual compilado via `CompileToSql` |
| 2.8 | LiveSQL Bar recompila automaticamente 300ms após qualquer mudança no grafo (debounce) |
| 2.9 | LiveSQL Bar exibe warnings (lista amarela) e erro de compilação (texto vermelho) quando presentes |
| 2.10 | LiveSQL Bar tem botão "Copiar SQL" |
| 2.11 | Botão "Desconectar" na barra superior desconecta via `DatabaseService.Disconnect` e limpa o schema sidebar |
| 2.12 | `SchemaController` GetX com: `isLoading`, `tables`, `filteredTables(query)`, `reload()` |
| 2.13 | `ConnectionController` GetX com: `isConnected`, `currentProfile`, `connect(profile)`, `disconnect()`, `latencyMs` |
| 2.14 | `SqlPreviewController` GetX com: `currentSql`, `warnings`, `error`, `isCompiling`, método `recompile()` |
| 2.15 | Proto `SchemaService.GetSchema` e `DatabaseService.Connect` já existem da Fase 1 — apenas consumir no Flutter |

### Condições de conclusão

- [x] Unit test `ConnectionController`: `connect` com perfil SQLite válido → `isConnected = true`
- [x] Unit test `ConnectionController`: `connect` com provider inválido → `isConnected = false`, `error` preenchido
- [x] Unit test `SchemaController`: `filteredTables('ord')` retorna apenas tabelas que contêm "ord"
- [x] Unit test `SqlPreviewController`: debounce de 300ms — mudança no grafo aciona recompile apenas uma vez
- [x] Widget test: `SchemaWidget` renderiza árvore com seção de tabelas quando `tables` não está vazio
- [x] Widget test: `LiveSqlBar` exibe SQL gerado após pump do controller
- [x] Widget test: `LiveSqlBar` exibe texto de erro em vermelho quando `error != null`
- [ ] Integration test: conecta SQLite → `GetSchema` retorna tabelas → drag tabela → canvas tem `TableSourceNode` com `tableFullName` correto
- [ ] Integration test: `TableSourceNode` no canvas → `LiveSqlBar` exibe SQL com o nome da tabela após 300ms

---

## Fase 3 — Paleta de Nós

**Dependência:** Fase 1 concluída (precisa de `NodeDefinitionModel` carregado)

### Requisitos

| # | Requisito |
|---|---|
| 3.1 | **Sidebar de paleta** — lista de nós agrupada por categoria (13 categorias), cada item mostra ícone de categoria + nome do nó |
| 3.2 | Paleta tem campo de busca fuzzy que filtra por nome e categoria em tempo real |
| 3.3 | Hover em item da paleta exibe tooltip com: nome completo, descrição, lista de pins de entrada/saída com tipos |
| 3.4 | Clique duplo em item da paleta adiciona o nó no centro do viewport atual |
| 3.5 | Drag de item da paleta para o canvas cria nó na posição de drop (convertida para coordenadas de canvas via `ViewportController.screenToCanvas`) |
| 3.6 | `NodePaletteController` GetX com: `groupedDefinitions`, `searchQuery`, `filteredResults`, `addNodeAtCenter(type)` |
| 3.7 | Paleta e schema ficam em abas na sidebar esquerda — `SidebarTab.nodes` e `SidebarTab.schema` |
| 3.8 | Aba ativa persiste entre sessões (preferência salva em `SharedPreferences`) |

### Condições de conclusão

- [x] Unit test `NodePaletteController`: `searchQuery = 'join'` retorna apenas nós com "join" no nome ou categoria
- [x] Unit test `NodePaletteController`: `addNodeAtCenter('TableSource')` adiciona nó na lista `CanvasController.nodes`
- [x] Unit test: filtro por categoria retorna apenas nós da categoria selecionada
- [x] Widget test: paleta renderiza pelo menos 13 grupos quando `definitions` está carregado
- [x] Widget test: campo de busca com texto vazio exibe todos os grupos
- [x] Widget test: tooltip do nó exibe nome + pelo menos uma pin ao fazer hover
- [x] Widget test: clique duplo em item da paleta dispara `addNodeAtCenter`
- [x] Flutter test cobertura de `NodePaletteController` ≥ 80%

---

## Fase 4 — Property Panel (Parâmetros de Nó)

**Dependência:** Fase 1 (precisa de `NodeDefinitionModel.parameters` carregado) + Fase 3 (nós criáveis via paleta)

### Requisitos

| # | Requisito |
|---|---|
| 4.1 | **Property panel** — painel lateral direito que aparece ao selecionar um nó |
| 4.2 | Painel exibe o tipo e categoria do nó selecionado no cabeçalho |
| 4.3 | Para cada `NodeParameter` do nó, renderiza o editor correspondente ao `kind`: `Text` → campo de texto, `Number` → campo numérico, `Boolean` → toggle, `Enum` → dropdown com `enumValues`, `CastType` → dropdown de tipos SQL, `DateTime`/`Date` → campo de texto por ora |
| 4.4 | Editar um parâmetro atualiza `NodeModel.parameters[name]` no `CanvasController` |
| 4.5 | Painel exibe seção "Pins de entrada" com pinos não conectados — permite inserir literal (valor fixo) para cada pin sem conexão |
| 4.6 | Alias do nó (usado no SELECT output) é editável via campo no painel |
| 4.7 | Para `TableSourceNode`, painel exibe o `tableFullName` editável |
| 4.8 | Painel fecha (ou colapsa) quando a seleção é limpa |
| 4.9 | `PropertyPanelController` GetX com: `selectedNode`, `update(paramName, value)`, `updateAlias(value)`, `updatePinLiteral(pinName, value)` |

### Condições de conclusão

- [ ] Unit test `PropertyPanelController`: `update('precision', '2')` atualiza `parameters['precision']` no `CanvasController.nodes`
- [ ] Unit test `PropertyPanelController`: `updatePinLiteral('right', '10')` atualiza `pinLiterals['right']`
- [ ] Widget test: selecionar nó com parâmetro `Number` renderiza `TextField` com valor numérico
- [ ] Widget test: selecionar nó com parâmetro `Enum` renderiza `DropdownButton` com as opções corretas
- [ ] Widget test: editar alias dispara `update` no controller
- [ ] Integration test: criar `RoundNode` → editar parâmetro `precision = 3` → `CompileToSql` retorna SQL com `ROUND(..., 3)`

---

## Fase 5 — Execução de Query + Painel de Resultados

**Dependência:** Fase 2 (conexão ativa + LiveSQL bar) + Fase 4 (parâmetros configurados para testes realistas)

### Requisitos

| # | Requisito |
|---|---|
| 5.1 | Botão "Executar" na toolbar do canvas chama `CompileToSql` → `ExecuteQuery` em sequência |
| 5.2 | **Painel de resultados** — drawer ou painel inferior colapsável exibindo tabela paginada (100 linhas por página) |
| 5.3 | Cabeçalho da tabela exibe nome e tipo de cada coluna |
| 5.4 | Painel exibe duração da query em ms (campo `duration_ms` do `QueryResult`) |
| 5.5 | Painel exibe erro da query em vermelho quando `QueryResult.error` está preenchido |
| 5.6 | Estado de loading (spinner) enquanto query está executando — botão "Executar" desabilitado durante execução |
| 5.7 | Botão "Cancelar" aborta a execução em andamento (cancela o `Future` pendente) |
| 5.8 | Exportação dos resultados para CSV (arquivo salvo via `FilePicker.saveFile`) |
| 5.9 | `QueryExecutorController` GetX com: `isExecuting`, `result`, `error`, `execute()`, `cancel()`, `exportCsv()` |
| 5.10 | Rows chegam como `repeated bytes` (JSON serializado) do servidor — desserializar em `List<Map<String, dynamic>>` no Flutter |

### Condições de conclusão

- [ ] Unit test `QueryExecutorController`: `execute()` chama `CompileToSql` → `ExecuteQuery` na ordem correta
- [ ] Unit test `QueryExecutorController`: `cancel()` muda `isExecuting` para false e não lança exceção
- [ ] Unit test: rows com JSON `[{"id":1,"name":"Alice"}]` → parsed para `List<Map>` com chaves corretas
- [ ] Widget test: `ResultTableWidget` com 3 colunas e 5 linhas renderiza grid correto
- [ ] Widget test: estado de loading exibe `CircularProgressIndicator`
- [ ] Widget test: `error != null` exibe texto vermelho, sem grid
- [ ] Integration test: conecta SQLite, executa query simples → painel exibe pelo menos 1 linha
- [ ] Integration test: exportar CSV → arquivo existe no caminho selecionado com cabeçalhos corretos

---

## Fase 6 — Tela Inicial + Persistência

**Dependência:** Fase 5 concluída (estado completo do canvas estabilizado antes de serializar)

### Requisitos

| # | Requisito |
|---|---|
| 6.1 | **Tela inicial** — exibida ao abrir o app sem arquivo ativo: botão "Novo canvas", lista de arquivos recentes, atalho de conexão rápida |
| 6.2 | Card de arquivo recente exibe: nome, path, data/hora do último acesso, provider de banco associado |
| 6.3 | **Salvar canvas** — serializa estado atual (`nodes`, `wires`, `bindings`, perfil de conexão, viewport) como JSON em arquivo `.dbwx` |
| 6.4 | **Abrir canvas** — lê `.dbwx`, restaura nós, wires e bindings no `CanvasController`, restaura viewport |
| 6.5 | Auto-save a cada 60s e ao fechar a janela (se houver mudanças não salvas) |
| 6.6 | Indicador de "modificado" no título da janela (asterisco) quando há alterações não salvas |
| 6.7 | Confirmação de "descartar alterações" ao abrir novo arquivo com canvas modificado |
| 6.8 | Lista de arquivos recentes persiste via `SharedPreferences` (máximo 10 entradas) |
| 6.9 | `SessionController` GetX com: `currentFilePath`, `isDirty`, `save()`, `saveAs()`, `open(path)`, `newCanvas()` |
| 6.10 | Formato `.dbwx` — JSON com versão de schema (`"format_version": 1`) para migrações futuras |

### Condições de conclusão

- [ ] Unit test `SessionController`: `isDirty` fica `true` após `addNode`, `false` após `save()`
- [ ] Unit test: serializar canvas com 2 nós e 1 wire → JSON com campos `nodes`, `wires`, `bindings`
- [ ] Unit test: desserializar o mesmo JSON → `CanvasController.nodes.length == 2`, `wires.length == 1`
- [ ] Unit test: `SessionController.open(path)` com arquivo `.dbwx` válido restaura estado completo
- [ ] Widget test: título da janela contém asterisco quando `isDirty = true`
- [ ] Widget test: abrir novo canvas com `isDirty = true` exibe dialog de confirmação
- [ ] Integration test: salvar → fechar → abrir → canvas idêntico ao original (nodes + wires + posições)

---

## Fase 7 — Undo / Redo

**Dependência:** Fase 6 (persistência garante que o modelo de dado é estável antes de rastrear comandos)

### Requisitos

| # | Requisito |
|---|---|
| 7.1 | Stack de undo/redo implementado como padrão Command (`ICanvasCommand` com `execute()` / `undo()`) |
| 7.2 | Comandos rastreados: `AddNode`, `RemoveNode`, `MoveNode`, `ConnectPins`, `Disconnect`, `EditParameter`, `UpdatePinLiteral`, `UpdateAlias` |
| 7.3 | `Ctrl+Z` → desfaz o último comando |
| 7.4 | `Ctrl+Shift+Z` (ou `Ctrl+Y`) → refaz o próximo comando |
| 7.5 | Ações não reversíveis (conectar ao banco, executar query) não entram no stack |
| 7.6 | Stack é limpo ao abrir um novo arquivo |
| 7.7 | Limite de 100 comandos no stack (descarta o mais antigo) |
| 7.8 | Botões de undo/redo na toolbar com estado desabilitado quando o stack está vazio |
| 7.9 | `UndoRedoController` GetX com: `canUndo`, `canRedo`, `push(command)`, `undo()`, `redo()`, `clear()` |
| 7.10 | `CanvasController` passa por `UndoRedoController.push()` para todas as mutações rastreadas |

### Condições de conclusão

- [ ] Unit test: `push(AddNodeCommand)` → `canUndo = true`, `canRedo = false`
- [ ] Unit test: `undo()` → nó removido da lista, `canRedo = true`
- [ ] Unit test: `undo()` seguido de `redo()` → nó de volta, estado idêntico ao original
- [ ] Unit test: `push()` com 101 comandos → stack tem exatamente 100 entradas
- [ ] Unit test: `clear()` → `canUndo = false`, `canRedo = false`
- [ ] Widget test: `Ctrl+Z` aciona `UndoRedoController.undo()`
- [ ] Widget test: botão undo desabilitado quando `canUndo = false`
- [ ] Integration test: add nó → mover → `Ctrl+Z` → nó na posição original → `Ctrl+Z` → nó removido

---

## Fase 8 — Command Palette + Atalhos de Teclado

**Dependência:** Fase 7 (ações de undo/redo incluídas na paleta)

### Requisitos

| # | Requisito |
|---|---|
| 8.1 | **Command palette** — abre com `Ctrl+Shift+P`, campo de busca fuzzy sobre lista de comandos registrados |
| 8.2 | Comandos disponíveis na palette: Novo arquivo, Abrir, Salvar, Conectar, Desconectar, Executar query, Undo, Redo, Zoom in, Zoom out, Reset zoom, Deletar seleção, Limpar canvas |
| 8.3 | Seleção via teclado (↑↓ Enter) ou mouse |
| 8.4 | Cada comando exibe: nome + atalho de teclado quando disponível |
| 8.5 | Atalhos globais: `Delete` → deleta nó selecionado, `Escape` → limpa seleção, `Ctrl+S` → salva, `Ctrl+O` → abre, `Ctrl+N` → novo, `Ctrl+=` → zoom in, `Ctrl+-` → zoom out, `Ctrl+0` → reset zoom |
| 8.6 | `CommandPaletteController` GetX com: `isOpen`, `query`, `filteredCommands`, `open()`, `close()`, `execute(command)` |
| 8.7 | Atalhos implementados via `RawKeyboard.onKey` registrado no `CanvasScreen` |

### Condições de conclusão

- [ ] Unit test `CommandPaletteController`: `query = 'conn'` retorna comandos com "conn" no nome
- [ ] Unit test: `execute(SalvarCommand)` chama `SessionController.save()`
- [ ] Widget test: `Ctrl+Shift+P` abre overlay da palette
- [ ] Widget test: `Escape` fecha palette
- [ ] Widget test: `Delete` com nó selecionado remove o nó do canvas
- [ ] Widget test: `Ctrl+S` aciona `SessionController.save()`
- [ ] Widget test: `Ctrl+0` reseta `ViewportController.transform` para identidade

---

## Fase 9 — SQL Editor (modo raw)

**Dependência:** Fase 5 (execução de query), Fase 6 (persistência de abas)

### Requisitos

| # | Requisito |
|---|---|
| 9.1 | Botão na toolbar alterna entre modo "Canvas" e modo "SQL Editor" |
| 9.2 | **SQL Editor** — área de texto com syntax highlighting básico para SQL (palavras-chave em cor, strings, comentários) |
| 9.3 | Botão "Executar" no SQL Editor executa o texto digitado via `DatabaseService.ExecuteRawSql` |
| 9.4 | Resultado exibido no mesmo painel de resultados da Fase 5 |
| 9.5 | Histórico de execuções — lista lateral com últimas 50 queries executadas, clique restaura o texto |
| 9.6 | Abas múltiplas de SQL Editor — `Ctrl+T` abre nova aba, `Ctrl+W` fecha aba atual |
| 9.7 | Cada aba mantém seu próprio texto e histórico de resultado |
| 9.8 | "Mutation guard" — antes de executar `INSERT/UPDATE/DELETE/DROP/TRUNCATE/ALTER`, exibe dialog de confirmação com preview do SQL e aviso de risco |
| 9.9 | `SqlEditorController` GetX com: `tabs`, `activeTabIndex`, `addTab()`, `closeTab(index)`, `executeActiveTab()` |

### Condições de conclusão

- [ ] Unit test `SqlEditorController`: `addTab()` → `tabs.length` aumenta 1
- [ ] Unit test `SqlEditorController`: `closeTab(0)` com 2 abas → `activeTabIndex` ajustado corretamente
- [ ] Unit test mutation guard: `SELECT * FROM t` → não exibe dialog; `DELETE FROM t` → exibe dialog
- [ ] Widget test: campo de texto aceita SQL e dispara `executeActiveTab` ao `Ctrl+Enter`
- [ ] Widget test: `Ctrl+T` adiciona aba
- [ ] Integration test: executar `SELECT 1` via SQL Editor → painel de resultados exibe linha com valor `1`
- [ ] Integration test: `DELETE` com mutation guard → cancelar → zero linhas afetadas

---

## Fase 10 — Auto-Join + Validação de Grafo

**Dependência:** Fase 2 (schema carregado — auto-join usa FK metadata) + Fase 7 (undo integrado)

### Requisitos

| # | Requisito |
|---|---|
| 10.1 | **Auto-join** — ao arrastar duas tabelas para o canvas, se existir FK entre elas, overlay sugere o join automático com tipo (INNER/LEFT) |
| 10.2 | Overlay de sugestão exibe: tabela A, tabela B, coluna de junção, tipo de join sugerido, botão "Aceitar" e "Ignorar" |
| 10.3 | Aceitar cria o nó de Join no canvas conectado às duas tabelas — ação entra no undo stack |
| 10.4 | **Manual join dialog** — ao arrastar um wire de RowSet para outro RowSet, dialog pede: coluna A, coluna B, tipo de join |
| 10.5 | **Validação de grafo** — painel de diagnósticos (ícone na toolbar) lista: nós sem conexão de saída, pins obrigatórios não conectados, aliases duplicados |
| 10.6 | Cada item de diagnóstico tem botão "Ir para" que centraliza e seleciona o nó problemático no canvas |
| 10.7 | `AutoJoinController` GetX com: `suggestions`, `detectForNewNodes(nodeIds)`, `accept(suggestion)`, `dismiss(suggestion)` |
| 10.8 | `ValidationController` GetX com: `issues`, `validate()`, `navigateTo(nodeId)` |
| 10.9 | Validação é executada automaticamente 500ms após qualquer mudança no grafo (debounce) |

### Condições de conclusão

- [x] Unit test `AutoJoinController`: dado schema com FK `orders.customer_id → customers.id`, detectar sugestão ao adicionar ambas as tabelas
- [x] Unit test `AutoJoinController`: `accept(suggestion)` → `CanvasController.nodes` tem Join node conectado
- [x] Unit test `ValidationController`: grafo com pin `is_required = true` sem conexão → issue de severidade `error`
- [x] Unit test `ValidationController`: grafo sem issues → `issues` vazio
- [ ] Widget test: overlay de auto-join aparece quando sugestão é detectada
- [ ] Widget test: botão "Ir para" em item de diagnóstico aciona `ViewportController` e `CanvasController.selectNode`
- [ ] Integration test: conectar banco com FKs → adicionar 2 tabelas relacionadas → sugestão de join aparece

---

## Fase 11 — Exportação

**Dependência:** Fase 5 (resultados disponíveis), Fase 6 (persistência de session)

### Requisitos

| # | Requisito |
|---|---|
| 11.1 | Exportar resultados como **CSV** — separador configurável (vírgula, ponto-e-vírgula, tab) |
| 11.2 | Exportar resultados como **JSON** — array de objetos |
| 11.3 | Exportar **canvas como imagem** (PNG) — captura o viewport atual via `RenderRepaintBoundary` |
| 11.4 | Exportar **SQL gerado** como arquivo `.sql` |
| 11.5 | `ExportController` GetX com: `exportResultsCsv()`, `exportResultsJson()`, `exportCanvasImage()`, `exportSql()` |
| 11.6 | Todos os exports usam `FilePicker.saveFile` com extensão pré-definida |

### Condições de conclusão

- [ ] Unit test: `List<Map>` com 2 linhas → CSV string com cabeçalho + 2 linhas de dados
- [ ] Unit test: mesmos dados → JSON array com 2 objetos com chaves corretas
- [ ] Widget test: botão de export CSV chama `FilePicker.saveFile` (mockado)
- [ ] Integration test: exportar CSV de query SQLite → arquivo legível com dados corretos

---

## Fase 12 — Features Avançadas (Benchmark + Explain + SQL Importer)

**Dependência:** Fase 9 (SQL Editor), Fase 5 (execução de query)

### Requisitos

| # | Requisito |
|---|---|
| 12.1 | **Benchmark** — executa a mesma query N vezes (configurável: 10/50/100) e exibe média, mínimo, máximo, percentil 95 de latência |
| 12.2 | **EXPLAIN plan** — botão "Explain" executa `EXPLAIN <sql>` via `ExecuteRawSql` e exibe resultado em tabela simples |
| 12.3 | **SQL Importer** — campo de texto para colar SQL existente; parser tenta mapear para nós no canvas (TableSource, Join, Where, Select) |
| 12.4 | SQL Importer exibe relatório: quantos nós foram criados, quais cláusulas não foram reconhecidas |
| 12.5 | SQL Importer — resultado entra no undo stack (desfazível com `Ctrl+Z`) |

### Condições de conclusão

- [ ] Unit test benchmark: resultado de 10 execuções com latências [10,20,30,...] → média correta, p95 correto
- [ ] Unit test SQL Importer: `SELECT a, b FROM orders WHERE a > 1` → cria TableSourceNode + 2 ColumnRef + 1 WhereNode
- [ ] Widget test benchmark: spinner visível durante execução, resultado exibido ao fim
- [ ] Integration test: executar benchmark de 10 rounds com SQLite → resultado não está vazio

---

## Fase 13 — Temas Customizáveis

**Dependência:** Fase 1 (design tokens base estabelecidos)
**Pode ser implementada paralelamente às Fases 3–12**

### Requisitos

| # | Requisito |
|---|---|
| 13.1 | Suporte a arquivo `.vsaqtheme` (JSON) com tokens de cor: `bg0`–`bg4`, `accentPrimary`, `textPrimary`, `textSecondary`, cores de pin por tipo |
| 13.2 | Tela de configurações com preview ao vivo ao editar tokens |
| 13.3 | Dois temas built-in: `dark` (padrão) e `light` |
| 13.4 | Tema selecionado persiste via `SharedPreferences` |
| 13.5 | `ThemeController` GetX com: `currentTheme`, `availableThemes`, `applyTheme(name)`, `loadFromFile(path)` |

### Condições de conclusão

- [ ] Unit test: `loadFromFile` com JSON válido → tokens aplicados ao `ThemeController`
- [ ] Unit test: `loadFromFile` com JSON inválido (cor hexadecimal mal-formada) → erro com mensagem clara, tema atual preservado
- [ ] Widget test: trocar tema atualiza cor de fundo do canvas na mesma frame

---

## Resumo de Dependências

```
Fase 1 ────────────────────────────────────────────────┐
   │                                                    │
   ├──► Fase 2 (Conexão + Schema + LiveSQL) ────────────┤
   │       │                                            │
   │       ├──► Fase 3 (Paleta)                         │
   │       │       │                                    │
   │       │       └──► Fase 4 (Property Panel)         │
   │       │                   │                        │
   │       └──────────────────►├──► Fase 5 (Resultados) │
   │                           │         │              │
   │                           │         └──► Fase 9    │
   │                           │         (SQL Editor)   │
   │                           │                        │
   │                           └──► Fase 6 (Persistência)
   │                                       │
   │                                       └──► Fase 7 (Undo/Redo)
   │                                                 │
   │                                                 └──► Fase 8 (Command Palette)
   │
   ├──► Fase 2 + Fase 7 ──► Fase 10 (Auto-Join + Validação)
   ├──► Fase 5 + Fase 6 ──► Fase 11 (Exportação)
   ├──► Fase 9 + Fase 5 ──► Fase 12 (Benchmark + Explain + Importer)
   └──► Paralela ──────────► Fase 13 (Temas)
```

---

## Checklist Completo

### Fase 1 — Canvas + Pins + gRPC Foundation
- [ ] 1.1 — 5 arquivos `.proto` criados e compilando em .NET e Flutter
- [ ] 1.2 — Projeto `GrpcServer` compila com 4 serviços registrados
- [ ] 1.3 — `READY:<port>` emitido no stdout após startup
- [ ] 1.4 — `NodeDefinitionGrpcService` serializa ≥ 60 tipos com `api_version = 1`
- [ ] 1.5 — `SqlCompilerGrpcService` mapeia proto → NodeGraph → SQL
- [ ] 1.6 — `DatabaseGrpcService` implementa Connect, Disconnect, ExecuteQuery, Ping
- [ ] 1.7 — `SchemaGrpcService` implementa GetSchema
- [ ] 1.8 — Flutter scaffold: pubspec, GetX, stubs gRPC gerados
- [ ] 1.9 — Modelos Dart: PinRef, PinDescriptorModel, NodeDefinitionModel, NodeModel, WireModel
- [ ] 1.10 — `ServerProcessService` inicia processo filho e lê porta com timeout
- [ ] 1.11 — `CanvasController`: addNode, removeNode, moveNode, connectPins, validateConnection, selectNode
- [ ] 1.12 — `ViewportController`: Matrix4 transform, screenToCanvas, canvasToScreen, zoom 0.1–5.0
- [ ] 1.13 — `PinDragController`: startDrag, updateDrag, commit, cancel
- [ ] 1.14 — `BezierWirePainter` com algoritmo de control offset 60–200
- [ ] 1.15 — `DotGridPainter` responsivo ao zoom/pan
- [ ] 1.16 — `DraftWirePainter` durante drag de pin
- [ ] 1.17 — `NodeWidget` draggável com pinos coloridos por tipo
- [ ] 1.18 — `PinWidget` reporta posição global via callback
- [ ] 1.19 — `CanvasScreen` com Stack de 4 camadas + GestureDetector
- [ ] 1.20 — Todos os 1796 testes .NET existentes passando
- [ ] 1.21 — Contrato gRPC: `GetAll` ≥ 60 defs, `CompileToSql` retorna SELECT válido
- [ ] 1.22 — Flutter unit tests de controllers passando com cobertura ≥ 80%
- [ ] 1.23 — Golden test de `BezierWirePainter` aprovado
- [ ] 1.24 — Integration test: servidor inicia < 3s, SQLite → SQL compilado

### Fase 2 — Conexão + Schema + LiveSQL Bar
- [x] 2.1 — Tela de conexão com todos os campos (provider, host, porta, database, auth)
- [x] 2.2 — Botão "Testar conexão" chama Ping antes de confirmar
- [x] 2.3 — Status bar exibe provider + database + latência após conexão
- [x] 2.4 — Schema sidebar com árvore hierárquica de tabelas/colunas
- [x] 2.5 — Busca em tempo real no schema sidebar
- [x] 2.6 — Drag de tabela do schema → TableSourceNode no canvas
- [ ] 2.7 — LiveSQL Bar colapsável com SQL compilado
- [x] 2.8 — Recompilação com debounce de 300ms após mudanças no grafo
- [ ] 2.9 — LiveSQL Bar exibe warnings e erros de compilação
- [ ] 2.10 — Botão "Copiar SQL" na LiveSQL Bar
- [x] 2.11 — Botão Desconectar limpa schema sidebar
- [x] 2.12 — `SchemaController` com isLoading, tables, filteredTables, reload
- [x] 2.13 — `ConnectionController` com isConnected, connect, disconnect, latencyMs
- [x] 2.14 — `SqlPreviewController` com debounce, recompile, error, warnings
- [ ] 2.15 — Unit + widget + integration tests conforme condições de conclusão

### Fase 3 — Paleta de Nós
- [x] 3.1 — Sidebar com 13 categorias e itens por categoria
- [x] 3.2 — Busca fuzzy por nome e categoria
- [x] 3.3 — Tooltip com pins ao hover
- [x] 3.4 — Clique duplo adiciona nó no centro do viewport
- [x] 3.5 — Drag da paleta para canvas cria nó na posição de drop
- [x] 3.6 — `NodePaletteController` com groupedDefinitions, filteredResults, addNodeAtCenter
- [x] 3.7 — Abas na sidebar: nodes e schema
- [x] 3.8 — Aba ativa persiste em SharedPreferences
- [ ] 3.9 — Unit + widget tests conforme condições de conclusão

### Fase 4 — Property Panel
- [x] 4.1 — Painel lateral direito ao selecionar nó
- [x] 4.2 — Cabeçalho com tipo e categoria
- [x] 4.3 — Editores por kind: Text, Number, Boolean, Enum, CastType, DateTime
- [x] 4.4 — Editar parâmetro atualiza NodeModel.parameters
- [x] 4.5 — Pins não conectados permitem inserir literal
- [x] 4.6 — Alias editável
- [x] 4.7 — tableFullName editável para TableSourceNode
- [x] 4.8 — Painel fecha ao limpar seleção
- [x] 4.9 — `PropertyPanelController` com update, updateAlias, updatePinLiteral
- [ ] 4.10 — Unit + widget + integration tests conforme condições de conclusão

### Fase 5 — Execução de Query + Painel de Resultados
- [x] 5.1 — Botão "Executar" chama CompileToSql → ExecuteQuery
- [x] 5.2 — Painel de resultados com tabela paginada (100 linhas/página)
- [x] 5.3 — Cabeçalho com nome e tipo de cada coluna
- [x] 5.4 — Duração da query exibida em ms
- [x] 5.5 — Erro de query em vermelho
- [x] 5.6 — Loading state com spinner, botão Executar desabilitado
- [x] 5.7 — Botão Cancelar aborta execução
- [ ] 5.8 — Exportação CSV via FilePicker
- [ ] 5.9 — `QueryExecutorController` com isExecuting, result, error, execute, cancel, exportCsv
- [x] 5.10 — Desserialização de rows JSON do servidor
- [ ] 5.11 — Unit + widget + integration tests conforme condições de conclusão

### Fase 6 — Tela Inicial + Persistência
- [x] 6.1 — Tela inicial com Novo canvas, arquivos recentes, conexão rápida
- [x] 6.2 — Card de arquivo recente com nome, path, data, provider
- [x] 6.3 — Salvar canvas como `.dbwx` (JSON)
- [x] 6.4 — Abrir `.dbwx` restaura nós, wires, bindings, viewport
- [x] 6.5 — Auto-save a cada 60s e ao fechar
- [x] 6.6 — Asterisco no título quando há alterações não salvas
- [x] 6.7 — Dialog de confirmação ao abrir arquivo com canvas modificado
- [x] 6.8 — Histórico de 10 arquivos recentes em SharedPreferences
- [x] 6.9 — `SessionController` com currentFilePath, isDirty, save, saveAs, open, newCanvas
- [x] 6.10 — Formato com `format_version: 1`
- [ ] 6.11 — Unit + widget + integration tests conforme condições de conclusão

### Fase 7 — Undo / Redo
- [ ] 7.1 — ICanvasCommand com execute() e undo()
- [ ] 7.2 — Comandos: AddNode, RemoveNode, MoveNode, ConnectPins, Disconnect, EditParameter, UpdatePinLiteral, UpdateAlias
- [ ] 7.3 — Ctrl+Z desfaz
- [ ] 7.4 — Ctrl+Shift+Z refaz
- [ ] 7.5 — Ações de banco não entram no stack
- [ ] 7.6 — Stack limpo ao abrir novo arquivo
- [ ] 7.7 — Limite de 100 comandos
- [ ] 7.8 — Botões undo/redo na toolbar com estado desabilitado
- [ ] 7.9 — `UndoRedoController` com canUndo, canRedo, push, undo, redo, clear
- [ ] 7.10 — CanvasController usa UndoRedoController.push para todas as mutações
- [ ] 7.11 — Unit + widget + integration tests conforme condições de conclusão

### Fase 8 — Command Palette + Atalhos
- [x] 8.1 — Command palette abre com Ctrl+Shift+P
- [x] 8.2 — Comandos: Novo, Abrir, Salvar, Conectar, Desconectar, Executar, Undo, Redo, Zoom in/out/reset, Delete, Limpar canvas
- [x] 8.3 — Seleção via teclado (↑↓ Enter) e mouse
- [x] 8.4 — Atalho exibido ao lado do nome do comando
- [x] 8.5 — Atalhos globais: Delete, Escape, Ctrl+S, Ctrl+O, Ctrl+N, Ctrl+=, Ctrl+-, Ctrl+0
- [x] 8.6 — `CommandPaletteController` com isOpen, query, filteredCommands, open, close, execute
- [x] 8.7 — RawKeyboard handler no CanvasScreen
- [x] 8.8 — Unit + widget tests conforme condições de conclusão

### Fase 9 — SQL Editor
- [x] 9.1 — Toggle Canvas / SQL Editor na toolbar
- [ ] 9.2 — SQL Editor com syntax highlighting básico (keywords, strings, comentários)
- [ ] 9.3 — Botão Executar chama ExecuteRawSql
- [ ] 9.4 — Resultado no painel da Fase 5
- [ ] 9.5 — Histórico de 50 queries, clique restaura texto
- [ ] 9.6 — Abas múltiplas: Ctrl+T abre, Ctrl+W fecha
- [ ] 9.7 — Cada aba com texto e resultado independentes
- [ ] 9.8 — Mutation guard para INSERT/UPDATE/DELETE/DROP/TRUNCATE/ALTER
- [ ] 9.9 — `SqlEditorController` com tabs, activeTabIndex, addTab, closeTab, executeActiveTab
- [ ] 9.10 — Unit + widget + integration tests conforme condições de conclusão

### Fase 10 — Auto-Join + Validação
- [x] 10.1 — Auto-join sugere join ao detectar FK entre tabelas adicionadas
- [x] 10.2 — Overlay com: tabela A, tabela B, coluna, tipo de join, Aceitar/Ignorar
- [x] 10.3 — Aceitar cria JoinNode conectado e entra no undo stack
- [x] 10.4 — Manual join dialog ao arrastar RowSet → RowSet
- [x] 10.5 — Painel de diagnósticos com: pins obrigatórios sem conexão, aliases duplicados, nós isolados
- [x] 10.6 — Botão "Ir para" centraliza e seleciona nó problemático
- [x] 10.7 — `AutoJoinController` com suggestions, detectForNewNodes, accept, dismiss
- [x] 10.8 — `ValidationController` com issues, validate, navigateTo
- [x] 10.9 — Validação com debounce de 500ms
- [ ] 10.10 — Unit + widget + integration tests conforme condições de conclusão

### Fase 11 — Exportação
- [ ] 11.1 — Exportar resultados como CSV com separador configurável
- [ ] 11.2 — Exportar resultados como JSON
- [ ] 11.3 — Exportar canvas como PNG
- [ ] 11.4 — Exportar SQL como arquivo `.sql`
- [ ] 11.5 — `ExportController` com exportResultsCsv, exportResultsJson, exportCanvasImage, exportSql
- [ ] 11.6 — FilePicker com extensão pré-definida em todos os exports
- [ ] 11.7 — Unit + integration tests conforme condições de conclusão

### Fase 12 — Benchmark + Explain + SQL Importer
- [ ] 12.1 — Benchmark: N execuções configuráveis, exibe média/min/max/p95
- [ ] 12.2 — EXPLAIN plan via ExecuteRawSql em tabela simples
- [ ] 12.3 — SQL Importer: colar SQL → mapeamento para nós no canvas
- [ ] 12.4 — Relatório do importer: nós criados + cláusulas não reconhecidas
- [ ] 12.5 — Resultado do importer no undo stack
- [ ] 12.6 — Unit + widget + integration tests conforme condições de conclusão

### Fase 13 — Temas Customizáveis
- [ ] 13.1 — Suporte a `.vsaqtheme` JSON com tokens de cor
- [ ] 13.2 — Tela de configurações com preview ao vivo
- [ ] 13.3 — Temas built-in: dark e light
- [ ] 13.4 — Tema persiste em SharedPreferences
- [ ] 13.5 — `ThemeController` com currentTheme, availableThemes, applyTheme, loadFromFile
- [ ] 13.6 — Unit + widget tests conforme condições de conclusão
