# Flutter + gRPC Migration — DBWeaver

**Date:** 2026-04-05
**Scope:** Migração da interface Avalonia para Flutter 3.41 mantendo o backend em .NET como servidor gRPC local. Fase 1 cobre o sistema de canvas e pins. Plataformas alvo: Windows e Linux (desktop).

---

## 1. Contexto

O DBWeaver é um editor visual de SQL com:
- 60+ tipos de nó organizados em 13 categorias
- Sistema de pins com type-checking (Expression, ColumnRef, Boolean, ColumnSet, etc.)
- Canvas infinito com bezier wires, drag, zoom/pan, rubber-band selection
- 4 providers de banco: SQL Server, MySQL, PostgreSQL, SQLite
- Motor de compilação SQL em C# (NodeGraphCompiler → QueryGeneratorService)
- 1796 testes automatizados existentes

O projeto atual tem 3 camadas:
- `DBWeaver` — core engine (intocado)
- `DBWeaver.Canvas` — geometria bezier (intocado)
- `DBWeaver.UI` — Avalonia UI (aposentado)

---

## 2. Decisões de design

### 2.1 Modelo de distribuição

Flutter inicia o servidor .NET como **processo filho** na inicialização. O usuário instala um único executável. O servidor escreve `READY:<port>` no stdout quando pronto; Flutter aguarda esse sinal antes de conectar via gRPC.

```
Flutter start
  → Process.start("vsaq-server")
  → aguarda "READY:<port>" no stdout
  → gRPC connect em localhost:<port>
  → GetNodeDefinitions()
  → canvas pronto
```

### 2.2 Divisão de responsabilidades

**Abordagem: Cliente autoritativo — servidor é o motor SQL.**

| Responsabilidade | Onde vive |
|---|---|
| Estado do canvas (posições, nós, conexões, seleção) | Flutter (GetX Controllers) |
| Zoom, pan, viewport transform | Flutter |
| Validação visual de tipo de pin | Flutter (regras simples duplicadas em Dart) |
| Definições de tipos de nó (dados estáticos) | Carregados do servidor 1x na inicialização |
| Compilação NodeGraph → SQL | Servidor .NET |
| Execução de queries | Servidor .NET |
| Carregamento de schema | Servidor .NET |
| Gerenciamento de conexão de banco | Servidor .NET |
| Credenciais | Nunca saem do processo filho |

O servidor é chamado apenas para: compilar SQL, executar queries, carregar schema e gerenciar conexões. Todas as interações de canvas (drag, zoom, pan, conectar pins) são locais sem round-trip.

### 2.3 Stack tecnológico

**Flutter:**
- Flutter 3.41 (Windows + Linux desktop)
- GetX para gerenciamento de estado
- `grpc` + `protobuf` (pub.dev) para comunicação
- `flutter_test` + `mocktail` para testes
- `golden_toolkit` para golden tests do canvas

**.NET:**
- `Grpc.AspNetCore` no novo projeto `DBWeaver.GrpcServer`
- `Grpc.Tools` para geração de stubs C# a partir dos `.proto`
- `Microsoft.AspNetCore.TestHost` para testes de contrato gRPC

### 2.4 Compatibilidade de arquivos

Sem compatibilidade com `.vsaq` antigos. O novo formato de persistência (JSON) é definido no cliente Flutter e não precisa ser compatível com o formato anterior.

---

## 3. Estrutura de projetos

```
VisualSqlArchtect/                  (repo existente)
├── src/
│   ├── DBWeaver/         ← INTOCADO (core engine)
│   ├── DBWeaver.Canvas/  ← INTOCADO (geometria bezier)
│   ├── DBWeaver.UI/      ← APOSENTADO (mantido, não deletado)
│   └── DBWeaver.GrpcServer/   ← NOVO
│       ├── Services/               (4 gRPC service implementations)
│       │   ├── NodeDefinitionGrpcService.cs
│       │   ├── SqlCompilerGrpcService.cs
│       │   ├── DatabaseGrpcService.cs
│       │   └── SchemaGrpcService.cs
│       ├── Protos/                 (symlink → ../../protos/)
│       └── Program.cs
├── protos/                         ← NOVO (compartilhado Flutter + .NET)
│   ├── common.proto
│   ├── node_definition.proto
│   ├── sql_compiler.proto
│   ├── database.proto
│   └── schema.proto
├── tests/                          (testes .NET existentes + novos de contrato gRPC)
└── flutter_app/                    ← NOVO
    ├── lib/
    │   ├── controllers/
    │   │   ├── canvas_controller.dart
    │   │   ├── viewport_controller.dart
    │   │   ├── pin_drag_controller.dart
    │   │   ├── connection_controller.dart
    │   │   ├── schema_controller.dart
    │   │   └── sql_preview_controller.dart
    │   ├── models/
    │   │   ├── node_model.dart
    │   │   ├── wire_model.dart
    │   │   ├── pin_descriptor_model.dart
    │   │   └── node_definition_model.dart
    │   ├── widgets/
    │   │   ├── canvas/
    │   │   │   ├── canvas_screen.dart
    │   │   │   ├── node_widget.dart
    │   │   │   ├── pin_widget.dart
    │   │   │   ├── bezier_wire_painter.dart
    │   │   │   ├── dot_grid_painter.dart
    │   │   │   └── draft_wire_painter.dart
    │   │   ├── sidebar/
    │   │   └── shared/
    │   └── services/
    │       ├── grpc/               (stubs gerados por protoc)
    │       └── process/
    │           └── server_process_service.dart
    ├── test/
    │   ├── controllers/
    │   └── widgets/
    ├── integration_test/
    └── protos/                     (symlink → ../protos/)
```

---

## 4. API gRPC

Os `.proto` ficam em `protos/` na raiz do repositório, compartilhados por symlink entre o servidor .NET e o app Flutter.

### 4.1 NodeDefinitionService

Chamado **uma vez** na inicialização. Retorna todos os tipos de nó para o Flutter cachear em memória durante a sessão.

```protobuf
service NodeDefinitionService {
  rpc GetAll(Empty) returns (GetAllResponse);
}

message GetAllResponse {
  repeated NodeDefinition definitions = 1;
  int32 api_version = 2;
}

message NodeDefinition {
  string type = 1;
  string category = 2;
  string display_name = 3;
  string description = 4;
  repeated PinDescriptor pins = 5;
  repeated NodeParameter parameters = 6;
}

message PinDescriptor {
  string name = 1;
  string direction = 2;       // "input" | "output"
  string data_type = 3;       // "Expression" | "ColumnRef" | "Boolean" | etc.
  bool is_required = 4;
  bool allow_multiple = 5;
}
```

### 4.2 SqlCompilerService

Recebe o estado atual do canvas serializado como protobuf e retorna o SQL compilado. Chamado ao clicar "Executar" ou para o LiveSqlBar.

```protobuf
service SqlCompilerService {
  rpc CompileToSql(CompileRequest) returns (CompileResponse);
}

message CompileRequest {
  repeated NodeProto nodes = 1;
  repeated WireProto wires = 2;
  repeated BindingProto bindings = 3;
  string provider = 4;   // "sqlserver" | "mysql" | "postgres" | "sqlite"
}

message CompileResponse {
  string sql = 1;
  repeated string warnings = 2;
  string error = 3;       // vazio se sucesso
}

message NodeProto {
  string id = 1;
  string type = 2;
  map<string, string> pin_literals = 3;
  map<string, string> parameters = 4;
  string alias = 5;
  string table_full_name = 6;
}

message WireProto {
  string from_node_id = 1;
  string from_pin_name = 2;
  string to_node_id = 3;
  string to_pin_name = 4;
}

message BindingProto {
  string binding_type = 1;  // "select" | "where" | "order" | "group" | "having"
  string node_id = 2;
  string pin_name = 3;
  bool desc = 4;            // para OrderBinding
}
```

### 4.3 DatabaseService

Único serviço com estado — mantém o pool de conexões ativo entre RPCs.

```protobuf
service DatabaseService {
  rpc Connect(ConnectionProfile) returns (ConnectResult);
  rpc Disconnect(Empty) returns (Empty);
  rpc ExecuteQuery(ExecuteRequest) returns (QueryResult);
  rpc ExecuteRawSql(RawSqlRequest) returns (QueryResult);
  rpc Ping(Empty) returns (PingResult);
  rpc ListDatabases(Empty) returns (ListDatabasesResponse);
  rpc SwitchDatabase(SwitchDatabaseRequest) returns (ConnectResult);
}

message ConnectionProfile {
  string provider = 1;
  string host = 2;
  int32 port = 3;
  string database = 4;
  string username = 5;
  string password = 6;
  bool use_integrated_security = 7;
  int32 timeout_seconds = 8;
}

message QueryResult {
  repeated ColumnMeta columns = 1;
  repeated bytes rows = 2;    // cada row serializada como JSON
  int64 duration_ms = 3;
  string error = 4;
}

message PingResult {
  int32 latency_ms = 1;
  bool success = 2;
}
```

### 4.4 SchemaService

Carrega metadados do banco conectado. Schema é cacheado no servidor após primeira carga.

```protobuf
service SchemaService {
  rpc GetSchema(Empty) returns (SchemaResponse);
  rpc GetTableColumns(TableRequest) returns (ColumnsResponse);
  rpc GetJoinSuggestions(JoinSuggestRequest) returns (SuggestionsResponse);
}

message SchemaResponse {
  repeated TableMeta tables = 1;
}

message TableMeta {
  string name = 1;
  string schema = 2;
  repeated ColumnMeta columns = 3;
}

message ColumnMeta {
  string name = 1;
  string data_type = 2;
  bool is_nullable = 3;
  bool is_primary_key = 4;
  bool is_foreign_key = 5;
  string foreign_key_table = 6;
}
```

### 4.5 Handshake e versionamento

O campo `api_version` em `GetAllResponse` permite que o Flutter detecte incompatibilidade e mostre uma mensagem clara. Versão inicial: `1`.

---

## 5. Canvas Flutter

### 5.1 Widget tree

```
CanvasScreen
└── GetBuilder<CanvasController>
    └── Stack
        ├── CustomPaint (DotGridPainter)          camada 1: grid
        ├── CustomPaint (BezierWirePainter)        camada 2: wires finalizados
        ├── ...nodes.map → Positioned(NodeWidget)  camada 3: nós
        └── CustomPaint (DraftWirePainter)         camada 4: wire em drag
    └── GestureDetector (zoom/pan via ScaleGesture)
```

### 5.2 GetX Controllers

**CanvasController**
```dart
class CanvasController extends GetxController {
  final RxList<NodeModel> nodes = <NodeModel>[].obs;
  final RxList<WireModel> wires = <WireModel>[].obs;
  final RxSet<String> selectedIds = <String>{}.obs;
  final Map<String, NodeDefinitionModel> _definitions = {};

  void loadDefinitions(List<NodeDefinitionModel> defs);
  void addNode(String type, Offset position);
  void removeNode(String id);
  void moveNode(String id, Offset delta);
  void connectPins(PinRef from, PinRef to);
  void disconnect(WireModel wire);
  bool validateConnection(PinRef from, PinRef to);
  void selectNode(String id, {bool addToSelection = false});
  void clearSelection();
}
```

**ViewportController**
```dart
class ViewportController extends GetxController {
  final Rx<Matrix4> transform = Matrix4.identity().obs;
  double get scale;
  Offset get panOffset;

  void onScaleUpdate(ScaleUpdateDetails details);
  Offset screenToCanvas(Offset screenPos);
  Offset canvasToScreen(Offset canvasPos);
}
```

**PinDragController**
```dart
class PinDragController extends GetxController {
  final Rxn<PinRef> dragSourcePin = Rxn();
  final Rx<Offset> dragCurrentPos = Offset.zero.obs;
  final Rxn<PinRef> hoveredTargetPin = Rxn();

  void startDrag(PinRef pin, Offset startPos);
  void updateDrag(Offset pos);
  void tryHover(PinRef? pin);
  void commit();    // cria wire se compatível
  void cancel();
}
```

### 5.3 BezierWirePainter

Porta direta do algoritmo em `CanvasWireGeometry.cs`:

```dart
class BezierWirePainter extends CustomPainter {
  final List<WireModel> wires;
  final Map<PinRef, Offset> pinPositions;  // preenchido por PinWidget após layout

  @override
  void paint(Canvas canvas, Size size) {
    for (final wire in wires) {
      final from = pinPositions[wire.fromPin];
      final to = pinPositions[wire.toPin];
      if (from == null || to == null) continue;
      final path = _buildBezier(from, to);
      canvas.drawPath(path, _wirePaint);
    }
  }

  Path _buildBezier(Offset from, Offset to) {
    final dx = (to.dx - from.dx).abs();
    final controlOffset = dx.clamp(60.0, 200.0);
    return Path()
      ..moveTo(from.dx, from.dy)
      ..cubicTo(
        from.dx + controlOffset, from.dy,
        to.dx - controlOffset, to.dy,
        to.dx, to.dy,
      );
  }

  @override
  bool shouldRepaint(BezierWirePainter old) =>
      !listEquals(wires, old.wires) || !mapEquals(pinPositions, old.pinPositions);
}
```

### 5.4 Modelo PinRef

```dart
@immutable
class PinRef {
  final String nodeId;
  final String pinName;
  const PinRef(this.nodeId, this.pinName);

  @override
  bool operator ==(Object other) =>
      other is PinRef && other.nodeId == nodeId && other.pinName == pinName;

  @override
  int get hashCode => Object.hash(nodeId, pinName);
}
```

### 5.5 Registro de posições de pins

Cada `PinWidget` registra sua posição global via callback após o primeiro layout:

```dart
class PinWidget extends StatefulWidget {
  final PinDescriptorModel descriptor;
  final void Function(PinRef, Offset) onPositionChanged;
  // ...
}

class _PinWidgetState extends State<PinWidget> {
  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final box = context.findRenderObject() as RenderBox;
      final pos = box.localToGlobal(box.size.center(Offset.zero));
      widget.onPositionChanged(PinRef(nodeId, pinName), pos);
    });
  }
}
```

### 5.6 Validação de tipo de pin (client-side)

Regras simples em Dart para feedback visual imediato. O servidor revalida durante `CompileToSql()`.

```dart
bool pinsAreCompatible(PinDescriptorModel from, PinDescriptorModel to) {
  if (from.direction != PinDirection.output) return false;
  if (to.direction != PinDirection.input) return false;
  if (to.dataType == PinDataType.expression) return true; // aceita qualquer coisa
  return from.dataType == to.dataType;
}
```

### 5.7 MVP da Fase 1 — interações implementadas

- Drag de nó com suporte a multi-move quando múltiplos nós estão selecionados
- Zoom por wheel no desktop e pan de canvas por botão do meio / direito / Space+drag
- Criar conexão via drag de pin para pin com validação visual de compatibilidade
- Wire draft com feedback visual de conexão inválida
- Deletar wire por clique direto no fio (hit-test por distância da curva)
- Seleção simples por clique e seleção em caixa (Shift + drag)
- Undo/redo do estado do canvas (nós, wires e seleção)
- Snap-to-grid opcional com toggle no rodapé

**Fora do MVP da Fase 1 (fases seguintes):**
- Seleção por laço (free-form)
- Alignment guides
- Auto-layout
- LiveSqlBar (preview SQL ao vivo)
- Painel de resultado de query

---

## 6. Servidor gRPC (.NET)

### 6.1 Novo projeto

`src/DBWeaver.GrpcServer/DBWeaver.GrpcServer.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\DBWeaver\DBWeaver.csproj"/>
    <PackageReference Include="Grpc.AspNetCore" Version="2.*"/>
    <PackageReference Include="Grpc.Tools" Version="2.*" PrivateAssets="All"/>
  </ItemGroup>
  <ItemGroup>
    <Protobuf Include="Protos\*.proto" GrpcServices="Server"/>
  </ItemGroup>
</Project>
```

### 6.2 Program.cs — inicialização

```csharp
using System.Net;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
builder.Services.AddGrpc();
builder.Services.AddDBWeaver(); // DI do core existente

var app = builder.Build();
app.MapGrpcService<NodeDefinitionGrpcService>();
app.MapGrpcService<SqlCompilerGrpcService>();
app.MapGrpcService<DatabaseGrpcService>();
app.MapGrpcService<SchemaGrpcService>();

await app.StartAsync();
Uri? address = app.Urls
  .Select(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null)
  .FirstOrDefault(uri => uri is not null);

int port = address?.Port ?? 0;
Console.WriteLine($"READY:{port}");
await app.WaitForShutdownAsync();
```

### 6.3 SqlCompilerGrpcService

```csharp
public sealed class SqlCompilerGrpcService : SqlCompilerService.SqlCompilerServiceBase
{
    private readonly INodeGraphCompiler _compiler;
    private readonly IQueryGeneratorService _generator;
    private readonly IProviderRegistry _registry;

    public override Task<CompileResponse> CompileToSql(
        CompileRequest request, ServerCallContext context)
    {
        var provider = Enum.Parse<DatabaseProvider>(request.Provider, ignoreCase: true);
        var dialect = _registry.GetDialect(provider);
        var graph = MapToNodeGraph(request);

        var compiled = _compiler.Compile(graph);
        var result = _generator.Generate(compiled, dialect);

        return Task.FromResult(new CompileResponse { Sql = result.Sql });
    }
}
```

---

## 7. Processo filho — ServerProcessService (Flutter)

```dart
class ServerProcessService {
  Process? _process;
  ClientChannel? _channel;

  Future<ClientChannel> start(
    String executablePath, {
    List<String> args = const <String>[],
    String? workingDirectory,
    Duration readyTimeout = const Duration(seconds: 30),
  }) async {
    _process = await Process.start(
      executablePath,
      args,
      workingDirectory: workingDirectory,
    );

    final port = await _process!.stdout
        .transform(utf8.decoder)
        .transform(const LineSplitter())
        .map(parsePortFromLine)
        .firstWhere((value) => value != null)
        .timeout(readyTimeout)
        .then((value) => value!);

    _channel = ClientChannel(
      'localhost',
      port: port,
      options: const ChannelOptions(credentials: ChannelCredentials.insecure()),
    );

    return _channel!;
  }

  Future<void> stop() async {
    await _channel?.shutdown();
    _process?.kill();
    _process = null;
  }

  static int? parsePortFromLine(String? line) {
    if (line == null || !line.startsWith('READY:')) {
      return null;
    }

    return int.tryParse(line.substring('READY:'.length));
  }
}
```

---

## 8. Testes

### 8.1 .NET — testes de contrato gRPC (novos)

Usando `Microsoft.AspNetCore.TestHost` + `Grpc.Net.Client`:

```csharp
public class NodeDefinitionContractTests : IClassFixture<GrpcTestFixture>
{
    [Fact]
    public async Task GetAll_ReturnsAllNodeTypes()
    {
        var client = new NodeDefinitionService.NodeDefinitionServiceClient(_fixture.Channel);
        var response = await client.GetAllAsync(new Empty());
        Assert.True(response.Definitions.Count >= 60);
        Assert.All(response.Definitions, d => Assert.NotEmpty(d.Type));
    }
}
```

### 8.2 Flutter — unit tests (controllers)

```dart
void main() {
  group('CanvasController', () {
    late CanvasController controller;
    late MockSqlCompilerService mockCompiler;

    setUp(() {
      mockCompiler = MockSqlCompilerService();
      controller = CanvasController(compiler: mockCompiler);
    });

    test('addNode inserts node at given position', () {
      controller.addNode('TableSource', const Offset(100, 200));
      expect(controller.nodes, hasLength(1));
      expect(controller.nodes.first.x, 100);
    });

    test('connectPins validates types before creating wire', () {
      // setup: pin Expression → pin Boolean (incompatível)
      expect(controller.validateConnection(exprPin, boolPin), isFalse);
    });
  });
}
```

### 8.3 Flutter — widget tests (golden)

```dart
testWidgets('BezierWire renders correctly between two pins', (tester) async {
  await tester.pumpWidget(
    buildCanvasWithTwoConnectedNodes(),
  );
  await expectLater(
    find.byType(CanvasScreen),
    matchesGoldenFile('goldens/single_wire.png'),
  );
});
```

### 8.4 Flutter — integration tests

```dart
void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('full flow: connect SQLite → load schema → compile SQL', (tester) async {
    final service = ServerProcessService();
    final channel = await service.start(serverPath);

    final dbClient = DatabaseServiceClient(channel);
    await dbClient.connect(ConnectionProfile(
      provider: 'sqlite',
      database: 'test/fixtures/northwind.db',
    ));

    final schemaClient = SchemaServiceClient(channel);
    final schema = await schemaClient.getSchema(Empty());
    expect(schema.tables, isNotEmpty);

    final compilerClient = SqlCompilerServiceClient(channel);
    final result = await compilerClient.compileToSql(CompileRequest(
      nodes: [tableSourceNode('orders')],
      bindings: [selectBinding('orders', 'rowset_out')],
      provider: 'sqlite',
    ));
    expect(result.error, isEmpty);
    expect(result.sql, contains('SELECT'));

    await service.stop();
  });
}
```

---

## 9. Critérios de aceite — Fase 1 (Canvas + Pins)

| # | Critério | Como verificar |
|---|---|---|
| 1 | Servidor inicia em < 3s e Flutter conecta | Integration test com timeout de 3s |
| 2 | 60+ tipos de nó carregados na paleta | `GetAll` retorna ≥ 60 definições |
| 3 | Drag de nó fluido (≥ 60fps, 50 nós no canvas) | Widget test com `WidgetTester.pump` em loop |
| 4 | Zoom e pan responsivos | Widget test: ScaleGesture atualiza ViewportController |
| 5 | Criar conexão via drag de pin com validação de tipo | Unit test: validateConnection + widget test de drag |
| 6 | Wires bezier renderizados corretamente | Golden test: frame com wires = imagem de referência |
| 7 | CompileToSql correto para JOIN de 2 tabelas | gRPC contract test com grafo fixo |
| 8 | ExecuteQuery retorna resultado para SQLite | Integration test com northwind.db |
| 9 | 1796 testes .NET existentes continuam passando | CI: `dotnet test` sem falhas novas |
| 10 | Flutter unit + widget tests: cobertura ≥ 80% dos controllers | `flutter test --coverage` |

---

## 10. Fora de escopo — Fase 1

- Seleção por laço (free-form)
- Alignment guides
- LiveSqlBar (preview SQL ao vivo enquanto edita)
- Painel de resultado de query no canvas
- Editor SQL dedicado (SqlEditorTabBar)
- Exportação (CSV, Excel, HTML, Markdown)
- Importação de SQL → canvas
- DDL canvas (CREATE TABLE visual)
- EXPLAIN plan
- Benchmark de queries
- Temas customizados (JSON theme)
- Auto-join detection
- Múltiplos perfis de conexão simultâneos
- SQL editor com syntax highlighting

---

## 11. Estado Atual (2026-04-05) e continuação

### 11.1 Entregue até agora

- Pipeline Flutter -> processo filho .NET -> handshake `READY:<port>` -> attach de clients gRPC.
- Fallback de inicialização do servidor para:
  - executável pré-buildado,
  - `dotnet <dll>`,
  - `dotnet run --project ...` quando necessário.
- Catálogo de nodes carregado via gRPC com agrupamento e filtro por texto.
- Schema e SQL preview exibidos no canvas com refresh manual.
- Interações de canvas: zoom, pan, drag node, conexão de pins, desconexão por clique no wire, seleção em caixa, multi-move.
- Prioridade de gesto pin vs node: início de drag em pin não é capturado por drag de node.
- Undo/redo e snap-to-grid opcional no canvas.
- Status operacional de gRPC no AppBar (connected / connecting / reconnecting / error / disconnected).
- Retry automático de bootstrap gRPC no startup do app Flutter.
- Teste de widget para interação de gesto (node body drag e pin priority).

### 11.2 Próxima etapa de migração

1.1 Core do compilador gRPC:
  - [x] Implementar `SqlCompilerGrpcService.CompileToSql` com mapeamento `CompileRequest -> NodeGraph`.
  - [x] Integrar `NodeGraphCompiler` + `QueryGeneratorService` removendo placeholder.
  - [x] Cobrir contrato básico de sucesso/erro/warnings com testes.
1.2 Core de serviços de dados:
  - [x] Implementar `SchemaGrpcService` e `DatabaseGrpcService` para fluxo real (connect/schema/query).
  - [x] Padronizar erros gRPC com mensagens acionáveis no cliente Flutter.
1.3 Qualidade do core:
  - [x] Adicionar testes de contrato/smoke gRPC para fluxo mínimo de compile e node definitions.
  - [x] Validar regressão do pipeline compile SQL com testes automatizados.
1.4 Estabilidade operacional:
  - [x] Barra de status de conexão gRPC no app (connected / reconnecting / error).
  - [x] Retry automático no bootstrap caso conexão inicial falhe.
  - [x] Reconexão automática contínua quando processo filho cai após já conectado.
1.5 Produtividade de edição (depois do core):
  - [x] Atalhos `Ctrl/Cmd+Z`, `Ctrl/Cmd+Shift+Z`, `Delete`.
  - [x] Testes de interação de alto volume (drag/pan/box-select com 50+ nós).
