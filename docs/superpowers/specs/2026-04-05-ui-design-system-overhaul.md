# UI Design System Overhaul — DBWeaver

**Date:** 2026-04-05
**Scope:** Revisão sistêmica do design system (shell, sidebar, editor, connection card). Nodes, pins e wires do canvas ficam intocados. O tema JSON anterior é deletado — sem backward compatibility com tokens antigos.

---

## 1. Problema

A interface atual sofre de três problemas estruturais:

1. **Dois sistemas de cor paralelos sem critério de uso.** `Surface0–3` e `MacroBg0–3` coexistem com valores próximos e componentes que alternam entre eles arbitrariamente, gerando inconsistência visual entre áreas.
2. **Falta de identidade.** O acento azul genérico (`AccentBlue #3B82F6`) não diferencia o app de qualquer template escuro.
3. **Hierarquia tipográfica colapsada.** `FontSizeBody` (12px) e `FontSizeMeta` (11px) têm diferença de 1px — insuficiente para distinção visual em telas menores.

---

## 2. Decisões de design

### 2.1 Paleta de cores — sistema unificado Bg0–4

Substituir `Surface*` e `MacroBg*` por uma escala única de 5 camadas com semântica clara:

| Token | Valor | Uso |
|---|---|---|
| `Bg0` | `#0A0D12` | Window background, canvas |
| `Bg1` | `#0F1520` | Shell: header, sidebars, tab bars |
| `Bg2` | `#151E2E` | Panels, cards, dropdowns |
| `Bg3` | `#1C2840` | Card hover, input backgrounds |
| `Bg4` | `#243050` | Active hover, selected states |

Bordas com degraus perceptíveis:

| Token | Valor | Uso |
|---|---|---|
| `BorderSubtle` | `#1A2640` | Separadores internos, dividers |
| `Border` | `#253552` | Bordas de card, input, dropdown |
| `BorderActive` | `#14B8A6` | Foco, seleção, estado ativo |

**Tokens removidos (sem aliases):** `Surface0–3`, `SurfaceGlass`, `MacroBg0–3`, `MacroBorderSubtle`, `MacroScrim`, `PanelElevated`, `PanelMuted`, `InputBg`, `InputSelection` — deletados do arquivo. Todos os componentes são atualizados na mesma PR.

### 2.2 Acento — troca de azul para teal

| Token antigo | Token novo | Valor |
|---|---|---|
| `AccentBlue #3B82F6` | `AccentTeal` | `#0D9488` |
| `AccentBlueMid #1D4ED8` | `AccentTealMid` | `#0F766E` |
| `AccentGlow` | `AccentTealGlow` | `#6014B8A6` |
| `TextAccent #60A5FA` | `TextAccent` | `#5EEAD4` |
| `BorderActive #3B82F6` | `BorderActive` | `#14B8A6` |
| `BtnPrimaryBg #2563EB` | `BtnPrimaryBg` | `#0D9488` |

**Intocados:** todos os tokens de categoria de nodes (`CatDataSource`, `CatString`, `CatMath`, etc.) e de pins (`PinText`, `PinNumber`, `PinBoolean`, `PinDateTime`, `PinJson`, `PinAny`, `PinExpression`). O azul permanece como cor de pins de texto e status info.

### 2.3 Hierarquia tipográfica — 5 roles semânticos

| Role | Token | Tamanho | Peso | Cor | Uso |
|---|---|---|---|---|---|
| Display | `FontSizeDisplay` | 20px | Bold | `TextPrimary` | Títulos de página/modal |
| Heading | `FontSizeHeading` | 15px | SemiBold | `TextPrimary` | Títulos de seção |
| Label | `FontSizeLabel` | 13px | Medium | `TextPrimary` | Nomes de item, botões |
| Body | `FontSizeBody` | 12px | Regular | `TextPrimary` | Texto descritivo |
| Caption | `FontSizeCaption` | 10px | Regular | `TextSecondary` | Meta, timestamps, labels de campo |

**Section headers** (CONEXÃO, SCHEMA, MESSAGES, HISTORY, NODES): `Caption` uppercase, `letter-spacing: 1.5px`, cor `AccentTeal` (`#14B8A6`). Este padrão ancora cada bloco da sidebar visualmente sem aumentar tamanho de fonte.

**Tokens removidos:** `FontSizeXS`, `FontSizeSM`, `FontSizeMD`, `FontSizeLG`, `FontSizeXL` e todos os `StartFont*` — deletados. Todos os componentes migram para os 5 roles semânticos na mesma PR.

### 2.4 Arredondamentos — guideline Cupertino

Seguir o princípio de **raio proporcional aninhado** do Cupertino (Apple HIG): elementos internos têm raio menor que seu container, proporcional ao padding entre eles (`inner radius ≈ outer radius − padding`). Isso evita o efeito "cantos dentro de cantos" visualmente inconsistente.

| Token | Valor | Uso |
|---|---|---|
| `RadiusXS` | `4` | Chips internos, badges, tooltips pequenos |
| `RadiusSM` | `7` | Inputs, botões, itens de lista |
| `RadiusMD` | `10` | Cards internos, dropdowns, popovers |
| `RadiusLG` | `14` | Cards principais, painéis, modais |
| `RadiusXL` | `18` | Containers de nível de tela (editor, overlays grandes) |
| `RadiusPill` | `999` | Pills, tags, badges arredondados |

**Regra de aninhamento:** um card com `RadiusLG` (14) e padding de 12px contém elementos com `RadiusSM` (7) ou `RadiusXS` (4). Um painel com `RadiusXL` (18) e padding de 10px contém cards com `RadiusMD` (10).

**Tokens removidos:** `NodeRadius`, `NodeHeaderRadius`, `PanelRadius`, `ChipRadius`, `StartRadiusCard`, `StartRadiusButton`, `StartRadiusPill` — deletados. Nodes mantêm seus valores hardcoded (intocados).

### 2.5 DatabaseConnectionCard — componente genérico reutilizável

Este card é um **componente autônomo de apresentação** (`UserControl`) sem lógica de negócio embutida — expõe apenas propriedades e comandos via binding. Isso permite reutilizá-lo na sidebar de conexão, no editor DDL e no editor DQL sem duplicação.

**Interface pública do controle (dependency properties):**

| Propriedade | Tipo | Descrição |
|---|---|---|
| `ConnectionName` | `string` | Nome do perfil ativo |
| `DatabaseName` | `string` | Banco selecionado |
| `AvailableConnections` | `IEnumerable` | Lista de perfis para o ComboBox de conexão |
| `AvailableDatabases` | `IEnumerable<string>` | Lista de databases para o ComboBox |
| `ServerVersion` | `string` | Texto da versão exibido no rodapé |
| `LatencyMs` | `int?` | Latência em ms; `null` oculta o valor |
| `IsConnected` | `bool` | Controla aparência do card (borda teal vs. apagada) |
| `IsReloading` | `bool` | Ativa o estado de loading (campos desabilitados + progress bar) |
| `DisconnectCommand` | `ICommand` | Botão "Desconectar" |
| `SwitchConnectionCommand` | `ICommand` | Acionado ao trocar o ComboBox de conexão |
| `SwitchDatabaseCommand` | `ICommand` | Acionado ao trocar o ComboBox de banco |

**Estrutura visual:**

```
┌─────────────────────────────────────┐  ← borda BorderActive quando IsConnected
│  [ícone DB]  ● Conectado · 45ms   [Desconectar]  │
│ ─────────────────────────────────── │
│  Conexão     [postgres-prod      ▾] │  ← ComboBox → SwitchConnectionCommand
│  Banco       [app_db             ▾] │  ← ComboBox → SwitchDatabaseCommand
│  ┌────────────────────────────────┐ │
│  │ Versão: PostgreSQL 16.2        │ │  ← monospace TextAccent, fundo Bg0
│  └────────────────────────────────┘ │
└─────────────────────────────────────┘
```

**Estados:**
- **Conectado:** borda `BorderActive`, campos habilitados, latência visível.
- **Dropdown aberto:** campo ativo com borda `BorderActive`, lista em `Bg3`, item selecionado com `border-left: 2px BorderActive`.
- **Recarregando** (`IsReloading = true`): campos com `Opacity 0.5` e `IsEnabled = false`, indicador pulsante no lugar da latência, `ProgressBar` indeterminada no rodapé.
- **Desconectado** (`IsConnected = false`): borda `Border`, sem latência/versão, botão "Conectar" no lugar de "Desconectar".

**Arquivo a criar:** `Controls/Shared/DatabaseConnectionCard.axaml` + `.axaml.cs`

**ViewModel — propriedades e comandos novos em `ConnectionManagerViewModel`:**
- `AvailableDatabases` (`ObservableCollection<string>`) — populada após conectar.
- `SelectedDatabase` (`string`) — banco ativo; ao mudar, dispara `SwitchDatabaseCommand`.
- `SwitchDatabaseCommand` (`ICommand`) — troca database e recarrega schema.
- `SwitchConnectionCommand` (`ICommand`) — recebe `ConnectionProfile`, reconecta e recarrega `AvailableDatabases` + schema.
- `IsReloadingSchema` (`bool`) — alimenta `IsReloading` do card em todos os contextos onde for usado.

### 2.6 Sidebar — unificação da aba Schema na aba Conexão

A aba **Schema** da sidebar esquerda é removida. Seu conteúdo (`SchemaControl`) passa a ser exibido diretamente na aba **Conexão**, abaixo do `DatabaseConnectionCard`, seguindo o mesmo padrão já adotado no editor (onde conexão e schema aparecem juntos).

**Estrutura da aba Conexão após unificação:**

```
[ CONEXÃO ]  [ NODES ]          ← apenas 2 tabs na sidebar

Aba Conexão:
  CONEXÃO ──────────────────
  [DatabaseConnectionCard]

  SCHEMA ───────────────────
  [SchemaControl]            ← mesmo componente existente, realocado

  PERFIS SALVOS ────────────
  [lista de perfis]
  [+ Nova conexão]
```

**Arquivos afetados:**
- `SidebarControl.axaml` — remover tab Schema, remover `SchemaTabButton`, remover slot `ShowSchema`.
- `ConnectionTabControl.axaml` — incorporar `SchemaControl` abaixo do card de conexão, com section header uppercase teal.
- `SchemaControl.axaml` — section header uppercase teal (já previsto em 2.5).
- `SidebarViewModel` — remover `ShowSchema`, `SchemaTabButton` e lógica de toggle da tab Schema.

### 2.7 Shell — aplicação dos novos tokens

| Área | Mudança |
|---|---|
| `AppHeaderBar` | `Background` → `Bg1`, `BorderBrush` → `BorderSubtle` |
| `SidebarControl` tab bar | Background → `Bg1`, tab ativa: fundo `AccentTeal` sólido (não só borda inferior) |
| `SqlEditorControl` | Background → `Bg1`, borda → `BorderSubtle`, `CornerRadius` → `RadiusXL` |
| `SqlEditorTabBar` | Tab ativa: `border-bottom: 2px BorderActive` |
| Status bar inferior | "PRONTO" / "ERRO" em uppercase Caption teal/vermelho |
| `GridSplitter` | `Background` → `BorderSubtle`, hover → `AccentTeal` |
| Todos os `Button` | `CornerRadius` → `RadiusSM` (7) como padrão global |
| Todos os `TextBox` / `ComboBox` | `CornerRadius` → `RadiusSM` (7) |
| Cards / `Border.connection-card` | `CornerRadius` → `RadiusLG` (14) |

---

## 3. Arquivos a modificar / criar

| Arquivo | Tipo de mudança |
|---|---|
| `Assets/Themes/DesignTokens.axaml` | Bg0–4, AccentTeal, tipografia, RadiusXS–XL; remover tokens obsoletos |
| `Assets/Themes/AppStyles.axaml` | Atualizar todos os seletores para novos tokens |
| `Controls/Shell/AppHeaderBar.axaml` | Trocar referências MacroBg → Bg |
| `Controls/SidebarLeft/SidebarControl.axaml` | Remover tab Schema, tab ativa com fundo teal sólido |
| `Controls/SidebarLeft/ConnectionTabControl.axaml` | Integrar `DatabaseConnectionCard` + `SchemaControl` |
| `Controls/SidebarLeft/SchemaControl.axaml` | Section header uppercase teal |
| `Controls/SidebarLeft/SidebarViewModel` | Remover lógica da tab Schema |
| `Controls/SqlEditor/SqlEditorControl.axaml` | Tokens atualizados |
| `Controls/SqlEditor/SqlEditorTabBar.axaml` | Tab ativa com borda teal |
| `Controls/SqlEditor/SqlEditorRightSidebarControl.axaml` | Section headers uppercase teal |
| `Views/Shell/MainWindow.axaml` | Tokens do splitter e toolbar |
| **NOVO** `Controls/Shared/DatabaseConnectionCard.axaml` | Card genérico reutilizável |
| **NOVO** `Controls/Shared/DatabaseConnectionCard.axaml.cs` | Code-behind com dependency properties |

---

## 4. Fora de escopo

- Nodes, pins, wires e lógica do canvas — intocados.
- Backward compatibility com temas JSON antigos — não preservada. `ThemeColorsConfig`, `ThemeTypographyConfig` e `ThemeTokenMapper` são reescritos para os novos tokens apenas.
- Lógica de negócio de conexão (`ConnectionManagerViewModel`) — apenas adições de propriedades/comandos, sem refatoração.
- Start Menu — tokens atualizados automaticamente pela mudança no design system, sem redesign específico.
- Temas claro / outras paletas — não considerados nesta iteração.
- Editores DDL e DQL — receberão o `DatabaseConnectionCard` mas seu redesign interno é escopo de spec separada.
