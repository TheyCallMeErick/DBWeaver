# Roadmap — Left Sidebar com 3 Tabs

> **Objetivo:** substituir o painel esquerdo estático (Schema Explorer hardcoded) por um painel
> com **3 tabs navegáveis**: **Nodes**, **Connection** e **Schema**. O painel mantém a largura
> atual (200 px, min 180, max 400).

---

## Visão Geral da Interface

```
┌──────────────────────────────────┐
│  [Nodes]  [Connection]  [Schema] │  ← Tab bar fixa no topo (40px)
├──────────────────────────────────┤
│                                  │
│   Conteúdo da tab ativa          │  ← área scrollável, ocupa todo espaço restante
│                                  │
├──────────────────────────────────┤
│  [+ Add Node ⇧A]  [Preview F3]  │  ← Footer fixo, igual em todas as tabs
└──────────────────────────────────┘
```

### Cores das Tabs (underline ativo)

| Tab        | Cor       | Hex       | Motivo                          |
|------------|-----------|-----------|----------------------------------|
| Nodes      | Azul      | `#60A5FA` | Mesma cor dos pins de texto      |
| Connection | Verde     | `#4ADE80` | Mesma cor do dot de saúde ativa  |
| Schema     | Roxo      | `#A78BFA` | Mesma cor dos pins JSON          |

---

## TAB 1 — Nodes

### Conceito

O usuário precisa de **consciência situacional** do canvas: quantos nodes existem, em quais
categorias, quais estão órfãos, qual está selecionado — tudo sem precisar dar `Ctrl+A` ou
abrir o overlay de busca.

**Princípios de UX:**
- Zero cliques para encontrar qualquer node (busca instantânea, local, sem I/O)
- Um clique para navegar até ele (pan suave + seleção automática no canvas)
- Feedback visual imediato sobre a saúde do grafo (órfãos, node selecionado)
- Nenhuma ação destrutiva sem confirmação inline — sem modais externos

---

### Layout

```
┌──────────────────────────────────┐
│ 🔍  Search nodes...              │  ← sempre visível, foco automático ao abrir a tab
├──────────────────────────────────┤
│ 5 nodes · 1 orphan               │  ← resumo; some quando busca ativa
├──────────────────────────────────┤
│                                  │
│ ▼  Data Source              2    │  ← header: barra de cor + nome + contagem
│   ┌──────────────────────────┐  │
│   │ ▌ orders_t               │  │  ← borda esquerda colorida = categoria
│   │   TableSource            │  │  ← subtitle: tipo do node
│   └──────────────────────────┘  │
│   ┌──────────────────────────┐  │
│   │ ▌ customers_t       ⚠   │  │  ← ⚠ amarelo = node órfão
│   │   TableSource            │  │
│   └──────────────────────────┘  │
│                                  │
│ ▼  Math Transform           1    │
│   ┌──────────────────────────┐  │
│   │ ▌ round_total    ●       │  │  ← ● azul = node selecionado no canvas
│   │   Round · #alias set     │  │  ← mostra alias quando definido
│   └──────────────────────────┘  │
│                                  │
│ ▶  Aggregate                0    │  ← sem nodes → começa colapsado
│                                  │
│ ▼  Output                   1    │
│   ┌──────────────────────────┐  │
│   │ ▌ ResultOutput           │  │
│   │   Final output           │  │
│   └──────────────────────────┘  │
│                                  │
└──────────────────────────────────┘
```

**Hover num item** — botão delete aparece inline, sem deslocar layout:
```
│   ┌──────────────────────────┐  │
│   │ ▌ orders_t          [✕] │  │
│   │   TableSource            │  │
│   └──────────────────────────┘  │
```

**Confirmação de delete** — expande o card inline, sem modal:
```
│   ┌──────────────────────────┐  │
│   │ ▌ orders_t               │  │
│   │   Delete? [Yes]  [Cancel]│  │
│   └──────────────────────────┘  │
```

---

### Busca

- **Instantânea** — sem debounce, lista local, zero latência
- **Campos pesquisados:** `Title`, `Subtitle`, `Alias`, `NodeType.ToString()`
- **Case-insensitive, contains**
- Texto encontrado **destacado** (bold ou cor diferente no próprio item)
- Grupos com 0 resultados **somem completamente** (não ficam vazios)
- Limpar busca restaura grupos no estado de expand/collapse anterior
- Botão `✕` inline aparece assim que há texto; `Esc` limpa

**Exemplo — busca por "round":**
```
┌──────────────────────────────────┐
│ 🔍  round                   ✕   │
├──────────────────────────────────┤
│ 1 result                         │
│                                  │
│ ▼  Math Transform           1    │
│   ┌──────────────────────────┐  │
│   │ ▌ [round]_total          │  │  ← "round" em destaque
│   │   Round                  │  │
│   └──────────────────────────┘  │
└──────────────────────────────────┘
```

---

### Interações

| Ação                        | Resultado                                              |
|-----------------------------|--------------------------------------------------------|
| Clicar num item             | Seleciona node + pan suave do canvas até centralizá-lo |
| Hover num item              | Revela botão `✕` no canto direito                     |
| Clicar `✕`                  | Expande confirmação inline                             |
| Confirmar delete            | Remove node com undo registrado; lista atualiza        |
| Clicar header do grupo      | Toggle expand/collapse                                 |
| `↑` / `↓` no search box    | Navega entre itens filtrados com foco visual           |
| `Enter` no search box       | Seleciona e foca o primeiro resultado                  |
| `Esc` no search box         | Limpa busca; se vazia, desfoca o campo                 |

---

### Grupos e Cores

Seguem a ordem do enum `NodeCategory`. A cor é a mesma do `HeaderColor` dos nodes no canvas:

| Categoria        | Cor         | Hex       |
|------------------|-------------|-----------|
| Data Source      | Teal        | `#0D9488` |
| String Transform | Índigo      | `#6366F1` |
| Math Transform   | Laranja     | `#F97316` |
| Type Cast        | Cinza-azul  | `#475569` |
| Comparison       | Rosa        | `#EC4899` |
| Logic Gate       | Âmbar       | `#D97706` |
| JSON             | Roxo        | `#7C3AED` |
| Aggregate        | Ciano       | `#0891B2` |
| Conditional      | Verde-oliva | `#65A30D` |
| Result Modifier  | Cinza       | `#64748B` |
| Literal          | Pedra       | `#78716C` |
| Output           | Azul-forte  | `#1D4ED8` |

**Regra de expand inicial:**
- Grupo com nodes → expandido
- Grupo sem nodes → colapsado (header visível com contagem `0`)
- Com busca ativa → grupos sem resultado somem completamente

---

### Estados dos Items

| Estado                     | Indicador visual                                      |
|----------------------------|-------------------------------------------------------|
| Selecionado no canvas      | `●` azul à direita + fundo `#101A2B` levemente mais claro |
| Órfão (sem conexão ao output) | `⚠` amarelo à direita                             |
| Com alias definido         | Subtitle: `Round · #meu_alias`                       |
| Hovered                    | Fundo `#101A2B` + botão `✕` visível                  |

Um item pode ter múltiplos estados simultâneos (ex: selecionado + órfão).

---

### Empty States

**Canvas vazio:**
```
┌──────────────────────────────────┐
│ 🔍  Search nodes...              │
│                                  │
│        (ícone: GridLarge)        │
│                                  │
│       No nodes on canvas         │
│  Press ⇧A to add your first node │
│                                  │
└──────────────────────────────────┘
```

**Busca sem resultados:**
```
│        (ícone: Magnify)          │
│    No nodes match "xyz"          │
│         [Clear search]           │
```

---

### ViewModels — Tab Nodes

#### `NodesListViewModel`
```
src/VisualSqlArchitect.UI/ViewModels/NodesListViewModel.cs
```
```csharp
public sealed class NodesListViewModel : ViewModelBase
{
    // Injetado via construtor
    private readonly ObservableCollection<NodeViewModel> _canvasNodes;
    private readonly Action<NodeViewModel> _selectAndFocus;
    private readonly Action<NodeViewModel> _deleteNode;

    public string SearchQuery { get; set; }  // → dispara Rebuild()
    public string SummaryText { get; }       // "5 nodes · 2 orphans"
    public bool   ShowSummary { get; }       // false quando SearchQuery não vazio
    public bool   HasNodes    { get; }       // false → empty state

    public ICommand ClearSearchCommand { get; }

    public ObservableCollection<NodeGroupViewModel> FilteredGroups { get; }

    // Chamado em: CollectionChanged + SearchQuery changed + cada NodeViewModel.PropertyChanged
    private void Rebuild();
}
```

#### `NodeGroupViewModel`
```csharp
public sealed class NodeGroupViewModel : ViewModelBase
{
    public NodeCategory Category   { get; }
    public string       Name       { get; }
    public Color        Color      { get; }
    public int          Count      { get; }
    public bool         IsExpanded { get; set; }
    public ICommand     ToggleExpandCommand { get; }
    public ObservableCollection<NodeListItemViewModel> Items { get; }
}
```

#### `NodeListItemViewModel`
```csharp
public sealed class NodeListItemViewModel : ViewModelBase
{
    public NodeViewModel Node              { get; }
    public string        Title            { get; }   // Node.Title
    public string        Subtitle         { get; }   // tipo + alias se existir
    public Color         Color            { get; }   // Node.HeaderColor
    public bool          IsOrphan         { get; }   // Node.IsOrphan
    public bool          IsSelected       { get; }   // espelha Node.IsSelected
    public bool          IsHovered        { get; set; }
    public bool          ShowDeleteConfirm{ get; set; }

    // Highlight de busca: dividir Title em [before, match, after] para colorir só o match
    public string? MatchBefore  { get; }
    public string? MatchText    { get; }
    public string? MatchAfter   { get; }
    public bool    HasHighlight { get; }

    public ICommand SelectCommand        { get; }
    public ICommand RequestDeleteCommand { get; }  // mostra confirmação inline
    public ICommand ConfirmDeleteCommand { get; }
    public ICommand CancelDeleteCommand  { get; }
}
```

---

### Controle AXAML — Tab Nodes

```
src/VisualSqlArchitect.UI/Controls/NodesListControl.axaml
x:DataType="vm:NodesListViewModel"
```

Estrutura geral:
```xml
<Grid RowDefinitions="Auto,Auto,*">

  <!-- Barra de busca -->
  <Border Grid.Row="0" Background="#0B0E15" BorderBrush="#1E2335" BorderThickness="0,0,0,1" Padding="8,6">
    <Grid ColumnDefinitions="Auto,*,Auto">
      <mi:MaterialIcon Kind="Magnify" Width="14" Foreground="#4A5568" VerticalAlignment="Center"/>
      <TextBox Grid.Column="1" Text="{Binding SearchQuery, Mode=TwoWay}"
               Watermark="Search nodes…" Background="Transparent" BorderThickness="0"/>
      <Button Grid.Column="2" Content="✕"
              IsVisible="{Binding SearchQuery, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"
              Command="{Binding ClearSearchCommand}"/>
    </Grid>
  </Border>

  <!-- Linha de resumo -->
  <TextBlock Grid.Row="1" Text="{Binding SummaryText}"
             IsVisible="{Binding ShowSummary}"
             FontSize="10" Foreground="#4A5568" Margin="12,4"/>

  <!-- Lista de grupos -->
  <ScrollViewer Grid.Row="2" VerticalScrollBarVisibility="Auto">
    <ItemsControl ItemsSource="{Binding FilteredGroups}">
      <ItemsControl.ItemTemplate>
        <DataTemplate x:DataType="vm:NodeGroupViewModel">
          <StackPanel>

            <!-- Header do grupo -->
            <Button Command="{Binding ToggleExpandCommand}" Background="Transparent"
                    BorderThickness="0" Padding="0" HorizontalAlignment="Stretch">
              <Grid ColumnDefinitions="3,*,Auto,Auto" Height="28" Margin="0">
                <Border Grid.Column="0" Background="{Binding Color}"/>
                <TextBlock Grid.Column="1" Text="{Binding Name}"
                           FontSize="10" FontWeight="SemiBold" Foreground="#8B95A8"
                           VerticalAlignment="Center" Margin="8,0,0,0"/>
                <TextBlock Grid.Column="2" Text="{Binding Count}"
                           FontSize="10" Foreground="#4A5568" VerticalAlignment="Center" Margin="0,0,8,0"/>
                <!-- chevron ▶/▼ -->
              </Grid>
            </Button>

            <!-- Items do grupo -->
            <ItemsControl ItemsSource="{Binding Items}" IsVisible="{Binding IsExpanded}">
              <ItemsControl.ItemTemplate>
                <DataTemplate x:DataType="vm:NodeListItemViewModel">
                  <Panel Margin="0,1">

                    <!-- Card normal -->
                    <Border Background="Transparent" IsVisible="{Binding ShowDeleteConfirm, Converter={x:Static BoolConverters.Not}}">
                      <Grid ColumnDefinitions="3,*,Auto,Auto" Height="40">
                        <Border Grid.Column="0" Background="{Binding Color}"/>

                        <!-- Título + subtitle com highlight -->
                        <StackPanel Grid.Column="1" VerticalAlignment="Center" Margin="8,0,4,0" Spacing="2">
                          <!-- highlight: 3 TextBlocks em linha (antes/match/depois) ou simples -->
                          <TextBlock Text="{Binding Title}" FontSize="12" Foreground="#C8D0DC"/>
                          <TextBlock Text="{Binding Subtitle}" FontSize="10" Foreground="#4A5568"/>
                        </StackPanel>

                        <!-- Indicadores: órfão + selecionado -->
                        <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center" Spacing="4">
                          <TextBlock Text="⚠" Foreground="#FBBF24" FontSize="11"
                                     IsVisible="{Binding IsOrphan}" ToolTip.Tip="Not connected to output"/>
                          <Ellipse Width="6" Height="6" Fill="#60A5FA"
                                   IsVisible="{Binding IsSelected}"/>
                        </StackPanel>

                        <!-- Botão delete (hover) -->
                        <Button Grid.Column="3" Content="✕" FontSize="10"
                                IsVisible="{Binding IsHovered}"
                                Command="{Binding RequestDeleteCommand}"
                                Background="Transparent" BorderThickness="0" Foreground="#4A5568"/>
                      </Grid>
                    </Border>

                    <!-- Confirmação inline de delete -->
                    <Border Background="#130D0D" BorderBrush="#5C1A1A" BorderThickness="1"
                            CornerRadius="4" Padding="8,6"
                            IsVisible="{Binding ShowDeleteConfirm}">
                      <Grid ColumnDefinitions="*,Auto,Auto">
                        <TextBlock Grid.Column="0" Text="{Binding Title, StringFormat='Delete {0}?'}"
                                   FontSize="11" Foreground="#F87171" VerticalAlignment="Center"/>
                        <Button Grid.Column="1" Content="Yes" FontSize="10"
                                Command="{Binding ConfirmDeleteCommand}"
                                Background="#5C1A1A" Foreground="#FB7185" Margin="0,0,4,0"/>
                        <Button Grid.Column="2" Content="Cancel" FontSize="10"
                                Command="{Binding CancelDeleteCommand}"
                                Background="Transparent" Foreground="#4A5568"/>
                      </Grid>
                    </Border>

                  </Panel>
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>

          </StackPanel>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>
  </ScrollViewer>

</Grid>
```

---

## TAB 2 — Connection

### Conceito

Exibe a conexão ativa com status de saúde e lista os perfis salvos para troca rápida.
**Não reimplementa** o formulário — delega 100% ao `ConnectionManagerControl` (popup existente).
Remove o `ConnectionBadgeBtn` da title bar, que perde a razão de existir.

---

### Layout

```
┌──────────────────────────────────┐
│  ACTIVE CONNECTION               │  ← label de seção
│                                  │
│  ┌────────────────────────────┐  │
│  │ ● Postgres          [Edit] │  │  ← dot de saúde + provider + botão Edit
│  │   northwind                │  │  ← database em destaque
│  │   localhost:5432           │  │  ← host:port em cinza
│  └────────────────────────────┘  │  ← borda verde/amarela/vermelha por saúde
│                                  │
│  SAVED CONNECTIONS               │
│                                  │
│  ┌────────────────────────────┐  │
│  │ ○ MySql · dev_db [Connect] │  │  ← perfil inativo; hover revela [Connect]
│  └────────────────────────────┘  │
│  ┌────────────────────────────┐  │
│  │ ○ SqlServer · staging      │  │
│  └────────────────────────────┘  │
│                                  │
│  [+ New Connection]              │
└──────────────────────────────────┘
```

---

### Comportamento

| Ação                             | Resultado                                                   |
|----------------------------------|-------------------------------------------------------------|
| Clicar no card ativo ou `Edit`   | `ConnectionManager.IsVisible = true` (abre popup existente) |
| Hover em perfil inativo          | Revela botão `[Connect]` inline                             |
| Clicar `[Connect]`               | Conecta diretamente (sem abrir popup)                       |
| Clicar `+ New Connection`        | `NewProfileCommand` + `IsVisible = true`                    |

**Borda do card ativo por saúde:**
- Online → `#4ADE80` (verde)
- Degraded → `#FBBF24` (amarelo)
- Offline → `#EF4444` (vermelho)

**Remoção do `ConnectionBadgeBtn`:**
Após esta tab estar estável, remover de `MainWindow.axaml` o `<Button Name="ConnectionBadgeBtn">` e o handler associado em `MainWindow.axaml.cs`.

---

### Controle AXAML

```
src/VisualSqlArchitect.UI/Controls/ConnectionTabControl.axaml
x:DataType="vm:ConnectionManagerViewModel"
```

Usa propriedades já existentes em `ConnectionManagerViewModel`:
- `ActiveConnectionLabel`, `ConnectionIndicatorColor`, `ActiveHealthStatus`
- `Profiles`, `ActiveProfileId`
- `ConnectCommand`, `NewProfileCommand`, `IsVisible`

---

## TAB 3 — Schema

### Conceito

Substitui o `TreeView` hardcoded atual por uma visão dinâmica, alimentada por
`CanvasViewModel.DatabaseMetadata`. Exibe tabelas, views e triggers com filtro em tempo real
e ação de adicionar ao canvas.

---

### Layout

```
┌──────────────────────────────────┐
│  NORTHWIND                  [↺]  │  ← nome do banco + botão refresh
├──────────────────────────────────┤
│ 🔍  Filter...                    │
├──────────────────────────────────┤
│ ▼ Tables (24)                    │
│   ▶ 🗄 dbo.orders               │
│   ▼ 🗄 dbo.customers            │
│     🔑 id           int          │
│     🔗 region_id    int          │
│     •  name         text         │
│   ▶ 🗄 dbo.products             │
│                                  │
│ ▶ Views (3)                      │
│ ▶ Triggers (1)                   │
└──────────────────────────────────┘
```

---

### Comportamento

| Ação                          | Resultado                                           |
|-------------------------------|-----------------------------------------------------|
| Double-click em tabela        | Cria `TableSource` node no canvas                   |
| Hover em tabela               | Revela botão `+` inline (mesma ação)                |
| Clicar `[↺]`                  | Recarrega metadata da conexão ativa                 |
| Digitar no filter             | Filtra por nome de tabela e coluna em tempo real    |

**Estados especiais:**
- Sem conexão ativa: ícone de nuvem + *"Connect to a database to view schema"*
- Carregando metadata: `ProgressBar` indeterminate de 3px no topo da área de conteúdo
- Sem resultados no filtro: *"No tables match «query»"*

---

### `SchemaViewModel`
```
src/VisualSqlArchitect.UI/ViewModels/SchemaViewModel.cs
```
```csharp
public sealed class SchemaViewModel : ViewModelBase
{
    public string  DatabaseName  { get; }
    public string  FilterQuery   { get; set; }
    public bool    IsLoading     { get; }
    public bool    HasConnection { get; }

    public ObservableCollection<SchemaGroupViewModel> FilteredGroups { get; }

    public ICommand RefreshCommand { get; }

    // Chamado quando CanvasViewModel.DatabaseMetadata muda
    private void Repopulate(DbMetadata? metadata);
}
```

### Controle AXAML

```
src/VisualSqlArchitect.UI/Controls/SchemaControl.axaml
x:DataType="vm:SchemaViewModel"
```

---

## Infraestrutura Compartilhada

### `SidebarViewModel`
```
src/VisualSqlArchitect.UI/ViewModels/SidebarViewModel.cs
```
```csharp
public enum SidebarTab { Nodes, Connection, Schema }

public sealed class SidebarViewModel : ViewModelBase
{
    public SidebarTab ActiveTab      { get; set; }
    public bool ShowNodes            => ActiveTab == SidebarTab.Nodes;
    public bool ShowConnection       => ActiveTab == SidebarTab.Connection;
    public bool ShowSchema           => ActiveTab == SidebarTab.Schema;

    public NodesListViewModel         NodesList         { get; }
    public ConnectionManagerViewModel ConnectionManager { get; }
    public SchemaViewModel            Schema            { get; }
}
```

Adicionar em `CanvasViewModel`:
```csharp
public SidebarViewModel Sidebar { get; }
```

---

### `SidebarControl.axaml`
```
src/VisualSqlArchitect.UI/Controls/SidebarControl.axaml
x:DataType="vm:SidebarViewModel"
```
```xml
<Grid RowDefinitions="40,*">

  <!-- Tab bar -->
  <Border Grid.Row="0" Background="#0D0F14" BorderBrush="#1E2335" BorderThickness="0,0,0,1">
    <StackPanel Orientation="Horizontal">
      <Panel>
        <Button Classes="tab" Name="TabNodesBtn" Classes.active="{Binding ShowNodes}">
          <StackPanel Orientation="Horizontal" Spacing="5">
            <mi:MaterialIcon Kind="GridLarge" Width="12"/>
            <TextBlock Text="Nodes"/>
          </StackPanel>
        </Button>
        <Border Height="2" Background="#60A5FA" VerticalAlignment="Bottom"
                IsVisible="{Binding ShowNodes}" CornerRadius="1,1,0,0"/>
      </Panel>
      <Panel>
        <Button Classes="tab" Name="TabConnBtn" Classes.active="{Binding ShowConnection}">
          <StackPanel Orientation="Horizontal" Spacing="5">
            <mi:MaterialIcon Kind="DatabaseOutline" Width="12"/>
            <TextBlock Text="Connection"/>
          </StackPanel>
        </Button>
        <Border Height="2" Background="#4ADE80" VerticalAlignment="Bottom"
                IsVisible="{Binding ShowConnection}" CornerRadius="1,1,0,0"/>
      </Panel>
      <Panel>
        <Button Classes="tab" Name="TabSchemaBtn" Classes.active="{Binding ShowSchema}">
          <StackPanel Orientation="Horizontal" Spacing="5">
            <mi:MaterialIcon Kind="TableLarge" Width="12"/>
            <TextBlock Text="Schema"/>
          </StackPanel>
        </Button>
        <Border Height="2" Background="#A78BFA" VerticalAlignment="Bottom"
                IsVisible="{Binding ShowSchema}" CornerRadius="1,1,0,0"/>
      </Panel>
    </StackPanel>
  </Border>

  <!-- Conteúdo das tabs -->
  <ctrl:NodesListControl     Grid.Row="1" IsVisible="{Binding ShowNodes}"
                             DataContext="{Binding NodesList}"/>
  <ctrl:ConnectionTabControl Grid.Row="1" IsVisible="{Binding ShowConnection}"
                             DataContext="{Binding ConnectionManager}"/>
  <ctrl:SchemaControl        Grid.Row="1" IsVisible="{Binding ShowSchema}"
                             DataContext="{Binding Schema}"/>

</Grid>
```

---

### Integração no `MainWindow.axaml`

Substituir o bloco `Grid.Column="0"` (linhas 298–422) por:

```xml
<Border Grid.Column="0" Background="#0F1118" BorderBrush="#1A1D26" BorderThickness="0,0,1,0">
  <Grid RowDefinitions="*,Auto">

    <ctrl:SidebarControl Grid.Row="0" DataContext="{Binding Sidebar}"/>

    <!-- Footer fixo — igual em todas as tabs -->
    <Border Grid.Row="1" Background="#0B0D12" BorderBrush="#1A1D26"
            BorderThickness="0,1,0,0" Padding="8,7">
      <StackPanel Spacing="4">
        <Button Classes="tb" HorizontalAlignment="Stretch" HorizontalContentAlignment="Left"
                Name="OpenSearchBtn" ToolTip.Tip="Add node to canvas (Shift+A)">
          <StackPanel Orientation="Horizontal" Spacing="7">
            <mi:MaterialIcon Kind="PlusCircleOutline" Width="14" Foreground="#3B82F6"/>
            <TextBlock Text="Add Node" FontSize="11"/>
            <Border Background="#0D0F14" CornerRadius="3" Padding="4,1">
              <TextBlock Text="⇧A" FontSize="9" Foreground="#4A5568"/>
            </Border>
          </StackPanel>
        </Button>
        <Button Classes="tb" HorizontalAlignment="Stretch" HorizontalContentAlignment="Left"
                Command="{Binding TogglePreviewCommand}" ToolTip.Tip="Toggle data preview (F3)">
          <StackPanel Orientation="Horizontal" Spacing="7">
            <mi:MaterialIcon Kind="TableEye" Width="14" Foreground="#4A5568"/>
            <TextBlock Text="Preview" FontSize="11"/>
            <Border Background="#0D0F14" CornerRadius="3" Padding="4,1">
              <TextBlock Text="F3" FontSize="9" Foreground="#4A5568"/>
            </Border>
          </StackPanel>
        </Button>
      </StackPanel>
    </Border>

  </Grid>
</Border>
```

---

### Wiring do `NodesListViewModel` em `CanvasViewModel`

```csharp
NodesList = new NodesListViewModel(
    nodes: Nodes,
    selectAndFocus: node =>
    {
        SelectionManager.SelectOnly(node);
        NodeLayoutManager.CenterOn(node);
    },
    deleteNode: node => NodeManager.Delete(node)
);
```

---

### Persistência da Tab Ativa

Adicionar campo ao `layout.json` via `MainWindowLayoutService`:

```json
{
  "LeftWidth": 200,
  "RightWidth": 240,
  "PreviewHeight": 220,
  "SidebarTab": "Nodes"
}
```

- Salvar em `SaveLayout()`
- Restaurar em `LoadLayout()`
- Default se ausente: `SidebarTab.Nodes`

---

## Ordem de Implementação

| # | Tarefa | Arquivo(s) alvo | Dependências |
|---|--------|----------------|--------------|
| 1 | `SidebarViewModel` + enum `SidebarTab` | `SidebarViewModel.cs` | — |
| 2 | `NodesListViewModel` + `NodeGroupViewModel` + `NodeListItemViewModel` | `NodesListViewModel.cs` | `CanvasViewModel.Nodes` |
| 3 | `SchemaViewModel` dinâmico | `SchemaViewModel.cs` | `CanvasViewModel.DatabaseMetadata` |
| 4 | Adicionar `Sidebar` em `CanvasViewModel` | `CanvasViewModel.cs` | passos 1–3 |
| 5 | `SidebarControl.axaml` (tab bar + switching) | `SidebarControl.axaml` | `SidebarViewModel` |
| 6 | `NodesListControl.axaml` | `NodesListControl.axaml` | `NodesListViewModel` |
| 7 | `ConnectionTabControl.axaml` | `ConnectionTabControl.axaml` | `ConnectionManagerViewModel` |
| 8 | `SchemaControl.axaml` (migrar TreeView) | `SchemaControl.axaml` | `SchemaViewModel` |
| 9 | Integrar `SidebarControl` no `MainWindow.axaml` | `MainWindow.axaml` | passos 5–8 |
| 10 | Wiring de click handlers no `SidebarControl` code-behind | `SidebarControl.axaml.cs` | passo 9 |
| 11 | Remover `ConnectionBadgeBtn` da title bar | `MainWindow.axaml` + `.cs` | passo 7 estável |
| 12 | Persistência da tab ativa no `layout.json` | `MainWindowLayoutService.cs` | passo 9 |

---

## Pontos de Atenção

- **`NodesListViewModel`** deve ouvir `CollectionChanged` dos `Nodes` E `PropertyChanged` de
  cada `NodeViewModel` individualmente — isso mantém a lista sincronizada em tempo real quando
  nodes são adicionados, removidos, renomeados, selecionados ou viram órfãos.

- **Tab Connection** não reimplementa formulário de edição. Delega 100% ao
  `ConnectionManagerControl` existente. Isso preserva a lógica de validação, teste de conexão
  e health monitoring sem duplicação.

- **Footer** (`Add Node` + `Preview`) é fixo em todas as tabs — é um atalho universal de
  ação rápida que não pertence a nenhuma tab específica.

- **Tabs sem dados** mostram empty states descritivos. Nunca tela em branco.

- **`SchemaViewModel`** observa `CanvasViewModel.PropertyChanged` e repopula automaticamente
  quando `DatabaseMetadata` muda (ao conectar ou reconectar).

- **Highlight de texto** na busca de nodes: dividir `Title` em três partes
  (`before`, `match`, `after`) e renderizar cada parte em `TextBlock` separado dentro de um
  `WrapPanel` horizontal — a parte `match` recebe `Foreground="#60A5FA"` e `FontWeight="Bold"`.
