# Blossom Color Picker — Especificação Normativa de Congelamento

**Data:** 2026-04-11
**Status:** Congelado para implementação
**Revisão:** 4 (auditoria final)

---

## 1. Contexto e motivação

O DBWeaver gerencia múltiplas conexões de banco de dados em paralelo. Todas as conexões exibem o mesmo indicador visual fixo (`AccentPrimary`), impossibilitando identificação rápida. Esta feature adiciona ao `ConnectionProfile` um campo `Color` (hex string), expõe um picker circular interativo inspirado no BlossomColorPicker no formulário de edição de conexão, e propaga a cor para quatro superfícies visuais no `ConnectionManagerControl`.

A cor é um rótulo de identificação visual atribuído exclusivamente pelo usuário. Não carrega e não deve carregar nenhuma semântica de estado funcional da conexão.

---

## 2. Princípios normativos de UX

Os seguintes princípios governam todas as decisões de comportamento desta feature. Em caso de conflito entre qualquer regra específica e estes princípios, os princípios têm precedência.

**P1 — Identificação, nunca semântica.**
A cor de uma conexão não representa nem comunica erro, sucesso, aviso, saúde ou criticidade. Indicadores funcionais continuam usando exclusivamente os tokens `StatusOk`, `StatusError`, `StatusWarning`, `StatusInfo` do `DesignTokens.axaml`. O app não modifica, suprime, substitui nem interpreta funcionalmente a cor atribuída pelo usuário.

**P2 — Preview em tempo real, commit explícito.**
Toda interação dentro do picker atualiza `SelectedColor` imediatamente. O valor é persistido em `ConnectionProfile` apenas quando o usuário aciona "Salvar" no formulário de conexão. Fechar o picker sem usar Escape não desfaz a seleção da sessão de edição em curso.

**P3 — Escape é o único cancelamento interno do picker.**
O picker não possui botão de confirmação nem botão de cancelamento. Escape reverte para o valor no momento da abertura. Qualquer outro fechamento (swatch, núcleo, light dismiss) mantém o último valor interagido.

**P4 — O controle nunca emite valor inválido.**
Toda saída de `SelectedColor` é uma hex string no formato canônico `#RRGGBB` com dígitos maiúsculos. Entradas inválidas recebidas pelo controle são coercidas para o fallback antes de qualquer emissão ou exibição.

**P5 — Fechamento é imediato.**
O picker não possui animação de saída. O fechamento ocorre no mesmo frame da ação que o dispara, independentemente do estado da animação de abertura.

---

## 3. Arquitetura do projeto

### 3.1 Novo projeto: `DBWeaver.UI.Controls`

| Atributo | Valor |
|---|---|
| Caminho | `src/DBWeaver.UI.Controls/DBWeaver.UI.Controls.csproj` |
| Tipo | Class library Avalonia — sem `<OutputType>` (padrão Library) |
| Namespace raiz | `DBWeaver.UI.Controls` |
| `AvaloniaUseCompiledBindingsByDefault` | `true` |

**Dependências obrigatórias:**
```xml
<PackageReference Include="Avalonia" />
<PackageReference Include="Avalonia.Themes.Fluent" />
```

**Restrição absoluta de dependência:** Este projeto não referencia `DBWeaver.csproj`, `DBWeaver.Canvas.csproj` nem qualquer pacote de acesso a dados ou infraestrutura. É uma biblioteca de controles UI sem acoplamento ao domínio da aplicação.

**Referência a adicionar em `DBWeaver.UI`:**
```xml
<ProjectReference Include="..\DBWeaver.UI.Controls\DBWeaver.UI.Controls.csproj" />
```

O projeto deve ser adicionado ao `files.sln`.

### 3.2 Visibilidade entre tipos

| Tipo | Visibilidade |
|---|---|
| `BlossomColorPickerControl` | `public` |
| `HslColor` | `public` |
| `BlossomArcSlider` | `internal` |
| `BlossomPetalItem` | `internal` |

Nenhum `ViewModel` público é exposto pelo assembly `DBWeaver.UI.Controls`. Nenhum `ResourceDictionary` global é registrado no `App.axaml` do consumidor — todos os estilos do controle são declarados dentro de `BlossomColorPickerControl.axaml` via `UserControl.Styles`.

### 3.3 Estrutura de arquivos

```
src/DBWeaver.UI.Controls/
  DBWeaver.UI.Controls.csproj
  ColorPicker/
    BlossomColorPickerControl.axaml
    BlossomColorPickerControl.axaml.cs
    Internals/
      BlossomArcSlider.axaml
      BlossomArcSlider.axaml.cs
      BlossomPetalItem.axaml
      BlossomPetalItem.axaml.cs
    Models/
      HslColor.cs
```

---

## 4. Estados visuais e semânticos

### 4.1 Definição dos estados

| Estado | Tipo | Fonte de verdade | Descrição |
|---|---|---|---|
| `IsEditing` | Operacional | `ConnectionManagerViewModel.IsEditing` | Um `ConnectionProfile` está carregado no painel de formulário à direita. |
| `IsItemSelected` | Visual | `ListBox.SelectedItem != null` | Um item está selecionado na lista. Coincide com `IsEditing` enquanto o formulário está ativo. |
| `IsConnectionActive` | Operacional | `ConnectionProfile.IsActive` (ver §8.1) | Este profile é a conexão de banco de dados correntemente estabelecida. |
| `IsPickerOpen` | Visual interno | `BlossomColorPickerControl._isPickerOpen` | O `Popup` do controle picker está aberto. |

**Distinção crítica entre `IsItemSelected` e `IsConnectionActive`:** Um item pode estar selecionado na lista sem estar conectado. Uma conexão pode estar ativa sem estar selecionada — por exemplo, quando o usuário navega para outro item da lista. Os dois estados são independentes.

### 4.2 Impacto de cada estado nas superfícies visuais

| Superfície | Visível quando | Cor exibida | Observações |
|---|---|---|---|
| Bullet `●` na lista | Sempre (para cada profile) | `profile.Color` | Todos os profiles, independente de estado. |
| Swatch no formulário | `IsEditing = true` | `EditColor` (preview em tempo real) | Atualiza durante interação com picker. |
| Faixa colorida no header do form | `IsEditing = true` | `EditColor` (preview em tempo real) | Mesma fonte que o swatch. Atualiza simultaneamente. |
| Borda lateral 3px no item da lista | `profile.IsActive = true` | `profile.Color` | Não aparece em itens apenas selecionados. Usa cor persistida, não `EditColor`. |

**Regra de consistência:** O swatch e a faixa do header são alimentados por `EditColor` (estado de edição em andamento, pode diferir do valor persistido). O bullet e a borda lateral são alimentados por `ConnectionProfile.Color` (valor persistido). Durante uma sessão de edição com alteração de cor, os dois podem diferir — isso é comportamento correto e esperado.

### 4.3 Precedência e proteção visual

- A cor de identificação do usuário nunca é substituída por cor de estado funcional em nenhuma das quatro superfícies.
- `StatusOk`, `StatusError`, `StatusWarning` e `StatusInfo` são reservados para indicadores funcionais — jamais usados nas quatro superfícies de cor desta feature.
- O indicador de conexão ativa (ex.: texto "● conectado" em verde) usa `StatusOk` e é independente da borda lateral colorida. Ambos podem coexistir no mesmo item sem conflito.

---

## 5. Componentes

### 5.1 `BlossomColorPickerControl`

**Tipo:** `UserControl` público.

**Responsabilidade:** Exibir o swatch clicável (gatilho), gerenciar o `Popup` contendo o picker completo (petals + arc slider + núcleo interno), e emitir `SelectedColor` atualizado.

#### 5.1.1 Propriedade pública

| Propriedade | Tipo | Mecanismo | Padrão | Modo | Descrição |
|---|---|---|---|---|---|
| `SelectedColor` | `string` | `StyledProperty<string>` com `CoerceValue` | `"#5B7CFA"` | Two-way | Hex string canônica da cor selecionada. Único contrato externo do controle. |

**Registro da propriedade com coerção:**
```csharp
public static readonly StyledProperty<string> SelectedColorProperty =
    AvaloniaProperty.Register<BlossomColorPickerControl, string>(
        nameof(SelectedColor),
        defaultValue: "#5B7CFA",
        coerce: CoerceSelectedColor);

private static string CoerceSelectedColor(AvaloniaObject o, string? value)
{
    if (!HslColor.TryParseHex(value, out _))
        return "#5B7CFA";
    return HslColor.FromHex(value!).ToHex(); // normaliza: uppercase, prefixo #
}

public string SelectedColor
{
    get => GetValue(SelectedColorProperty);
    set => SetValue(SelectedColorProperty, value);
}
```

A coerção garante que `SelectedColor` sempre contenha um hex canônico, independentemente da origem da atribuição (binding externo, código interno ou setter). `null` é aceito pelo parâmetro por ser `string?` — resulta no fallback `"#5B7CFA"`.

#### 5.1.2 Estado interno (campos privados)

| Campo | Tipo | Padrão inicial | Descrição |
|---|---|---|---|
| `_hue` | `double` | `230.0` | Matiz atual em graus `[0.0, 360.0)`. Inicializado do `SelectedColor` na abertura do picker. Atualizado ao clicar em petal. |
| `_currentSaturation` | `double` | `0.93` | Saturação atual `[0.0, 1.0]`. Inicializada do `SelectedColor` na abertura. Atualizada ao clicar em petal com a saturação específica da petal. |
| `_lightness` | `double` | `0.60` | Luminosidade atual `[0.30, 0.80]`. Inicializada do `SelectedColor` (clamped) na abertura. Controlada pelo arc slider. |
| `_colorBeforePickerOpened` | `string` | `"#5B7CFA"` | Snapshot de `SelectedColor` no momento em que o picker foi aberto. Usado exclusivamente pelo revert de Escape. |
| `_isPickerOpen` | `bool` | `false` | Estado do Popup. |
| `_selectedPetalIndex` | `int` | `-1` | Índice `[0..7]` da petal ativa. `-1` indica nenhuma petal selecionada. |
| `_bloomTimer` | `DispatcherTimer?` | `null` | Timer responsável pelo escalonamento da animação bloom. Deve ser parado e descartado no fechamento e no unload. |

**Nota sobre `_currentSaturation`:** A saturação não é um atributo global fixo do controle. Quando nenhuma petal está selecionada (`_selectedPetalIndex = -1`), `_currentSaturation` retém o valor derivado do `SelectedColor` no momento da abertura. Quando uma petal é clicada, `_currentSaturation` é atualizada com a saturação canônica da petal (tabela §5.1.6).

**Valores padrão iniciais** (`230.0`, `0.93`, `0.60`): correspondem à cor fallback `"#5B7CFA"` (H≈230°, S≈0.93, L≈0.60). Esses valores são sobrescritos na primeira abertura do picker.

#### 5.1.3 Estrutura de layout

O controle tem dois elementos na raiz: o **swatch** (visível sempre) e o **Popup** (visível quando `_isPickerOpen = true`).

**Swatch (gatilho externo):**

| Atributo | Valor |
|---|---|
| Tipo | `Button` — para receber foco de teclado e semântica de acessibilidade |
| Dimensão | 28×28px fixos |
| `CornerRadius` | 6px — segue padrão visual dos campos do formulário |
| `Background` | `SelectedColor` convertido para `SolidColorBrush` via code-behind |
| `BorderBrush` | `HslColor.FromHex(SelectedColor).WithLightness(L + 0.15).ToAvaloniaColor()` convertido para `SolidColorBrush` |
| `BorderThickness` | 2px |
| `Cursor` | `Hand` |
| `ToolTip.Tip` | Valor hex atual, ex.: `"#E16174"` |
| Comportamento de clique | Alterna `_isPickerOpen` (toggle — abre se fechado, fecha se aberto) |

O `Background` e o `BorderBrush` do swatch devem ser atualizados por código toda vez que `SelectedColor` muda, via `PropertyChanged` callback registrado em `SelectedColorProperty`.

**Popup:**

| Atributo | Valor |
|---|---|
| `PlacementMode` | `Bottom` |
| `PlacementTarget` | O swatch |
| `PlacementConstraintAdjustment` | `FlipY \| SlideX` |
| `HorizontalOffset` | `−96.0` — centraliza o canvas de 220px sobre o swatch de 28px: `−(220/2 − 28/2)` |
| `VerticalOffset` | `4.0` — margem visual entre swatch e popup |
| `IsLightDismissEnabled` | `true` |
| `IsOpen` | Controlado por código via `_isPickerOpen` — não via binding AXAML para evitar reentrância |
| Dimensão do conteúdo | `Canvas` de 220×220px fixos |

**Canvas interno:**

O Canvas tem dimensão fixa 220×220px. Centro lógico: `(110.0, 110.0)`.

**Ordem obrigatória dos filhos do Canvas** (determina z-order — último filho renderizado no topo):
1. **`BlossomArcSlider`** — primeiro filho, 220×220px, renderizado atrás de tudo.
2. **8 instâncias de `BlossomPetalItem`** — filhos intermediários, posicionados via `Canvas.Left`/`Canvas.Top`.
3. **Núcleo interno** — último filho, renderizado na frente de tudo.

Esta ordem deve ser mantida em code-behind ao adicionar os elementos ao Canvas em `InitializeComponent` ou `OnLoaded`.

**Núcleo interno:**

| Atributo | Valor |
|---|---|
| Tipo | `Button` circular |
| Dimensão | 36×36px |
| Posição no Canvas | `Canvas.Left = 110 − 18 = 92`, `Canvas.Top = 110 − 18 = 92` |
| `CornerRadius` | 18px (círculo perfeito) |
| `Background` | Atualizado por código em resposta a `SelectedColor` (mesma lógica do swatch) |
| `BorderBrush` | `HslColor.FromHex(SelectedColor).WithLightness(L + 0.15)` (mesma lógica do swatch) |
| `BorderThickness` | 2px |
| Comportamento de clique | Fecha o picker sem revert (equivalente a light dismiss — executa §6.2) |

#### 5.1.4 Posicionamento das petals

As 8 petals são posicionadas por code-behind em `OnLoaded` (ou ao adicionar os elementos ao Canvas):

```
centerX    = 110.0
centerY    = 110.0
petalRadius = 68.0
petalDiameter = 26.0

para i em [0..7]:
  angleRad = (i * 45.0 − 90.0) * Math.PI / 180.0
  left = centerX + petalRadius * Math.Cos(angleRad) − petalDiameter / 2.0
  top  = centerY + petalRadius * Math.Sin(angleRad) − petalDiameter / 2.0
  Canvas.SetLeft(petalItems[i], left)
  Canvas.SetTop(petalItems[i], top)
```

O offset de −90° posiciona a petal 0 no topo (12h). Distribuição horária: 0=topo, 1=superior-direito, 2=direito, 3=inferior-direito, 4=baixo, 5=inferior-esquerdo, 6=esquerdo, 7=superior-esquerdo.

**Verificação de não-sobreposição com o arc slider:**
`petalRadius + petalDiameter/2 = 68 + 13 = 81px`. O arc slider tem `Radius = 90.0` e `TrackThickness = 9.0`, portanto a borda interna do track está em `90 − 4.5 = 85.5px`. Como `81 < 85.5`, as petals não se sobrepõem ao arc track.

#### 5.1.5 Animação bloom

A animação de abertura é aplicada individualmente a cada `BlossomPetalItem` via a propriedade `IsBloomVisible` (`StyledProperty<bool>`) — **não** via a propriedade `IsVisible` do Avalonia, que suprimiria os elementos antes da transição ser executada.

**Sequência de abertura:**
1. Todas as petals recebem `IsBloomVisible = false` (reset antes de iniciar — garante estado limpo em reabertura).
2. O Popup é aberto.
3. `_bloomTimer` é criado com `Interval = TimeSpan.FromMilliseconds(30)`.
4. Uma variável `_bloomIndex` (campo privado `int`, zerado a cada abertura) é incrementada a cada tick.
5. A cada tick, a petal de índice `_bloomIndex - 1` recebe `IsBloomVisible = true`.
6. Quando `_bloomIndex` alcança 8, `_bloomTimer.Stop()` é chamado e `_bloomTimer` é descartado (`_bloomTimer = null`).
7. A petal 0 recebe `IsBloomVisible = true` imediatamente antes do timer iniciar (delay 0ms), sem esperar pelo primeiro tick.

| Petal (i) | Tempo desde abertura |
|---|---|
| 0 | 0 ms (imediato) |
| 1 | 30 ms |
| 2 | 60 ms |
| 3 | 90 ms |
| 4 | 120 ms |
| 5 | 150 ms |
| 6 | 180 ms |
| 7 | 210 ms |

**Parâmetros da transição por petal** (declarados no `Style` de `BlossomPetalItem`):
- `ScaleTransform.ScaleX` e `ScaleTransform.ScaleY`: 0.0 → 1.0, duração 280ms, easing `CubicEaseOut`.
- `Opacity`: 0.0 → 1.0, duração 200ms, easing linear.
- `RenderTransformOrigin`: `RelativePoint.Center` (0.5, 0.5) — a petal escala a partir de seu próprio centro.

**Fechamento:**
1. Se `_bloomTimer != null`: `_bloomTimer.Stop()`, `_bloomTimer = null`.
2. Todas as petals recebem `IsBloomVisible = false` — sem transição de saída (fechamento imediato, P5).

**Reabertura:** O passo de reset (passo 1 da sequência de abertura acima) garante que a animação reinicie do zero, mesmo que o picker seja fechado antes de completar o bloom.

**Cleanup no unload:** O controle deve sobrescrever `OnDetachedFromVisualTree` e executar o fechamento se `_isPickerOpen = true`, incluindo a parada do `_bloomTimer`. Isso evita que o timer continue ativo após o controle ser removido da árvore visual.

#### 5.1.6 Cores e atributos das petals

As 8 petals têm cores, hue e saturação fixos por design. A luminosidade exibida visualmente em cada petal é `L = 0.55` (valor representativo). Ao clicar na petal, o hue e a saturação canônicos da petal (tabela abaixo) substituem `_hue` e `_currentSaturation` do controle. O `SelectedColor` é recalculado usando `_lightness` atual.

| Índice | Nome | Hex base (L=0.55) | Hue canônico (graus) | Saturação canônica |
|---|---|---|---|---|
| 0 | Red | `#E16174` | 350.0 | 0.68 |
| 1 | Amber | `#D9A441` | 38.0 | 0.68 |
| 2 | Teal | `#2FBF84` | 155.0 | 0.60 |
| 3 | Blue | `#4D9BFF` | 210.0 | 1.00 |
| 4 | Purple | `#8A63F6` | 260.0 | 0.87 |
| 5 | Pink | `#F472B6` | 320.0 | 0.85 |
| 6 | Orange | `#FB923C` | 28.0 | 0.95 |
| 7 | Cyan | `#22D3EE` | 190.0 | 0.82 |

O hex base é usado exclusivamente para o `PetalColor` do `BlossomPetalItem` (exibição visual). Os valores de Hue e Saturação canônicos são usados no cálculo de `SelectedColor` ao clicar — não são derivados do hex base.

**`TrackColor` do arc slider associada a cada petal:** `Avalonia.Media.Color.Parse(petalHexBase[i])`. Atualizado ao clicar em petal.

**Seleção de petal ao abrir o picker:**
1. Para cada petal `i`, calcula a diferença circular: `diff[i] = min(|_hue − petalHue[i]|, 360 − |_hue − petalHue[i]|)`.
2. Se `min(diff[0..7]) ≤ 15.0°`, a petal com menor `diff` recebe `IsSelected = true`; `_selectedPetalIndex` é seu índice.
3. Se nenhuma petal tiver `diff ≤ 15.0°`, `_selectedPetalIndex = -1` e nenhuma petal fica selecionada.
4. Apenas uma petal pode estar selecionada por vez.

---

### 5.2 `BlossomArcSlider` (internal)

**Tipo:** `UserControl` interno.

**Responsabilidade:** Renderizar um arco circular de 300° com handle arrastável e emitir `Value` (0.0–1.0) em tempo real durante o drag.

#### 5.2.1 Propriedades (internal ao assembly)

| Propriedade | Tipo | Mecanismo | Padrão | Modo | Descrição |
|---|---|---|---|---|---|
| `Value` | `double` | `StyledProperty<double>` | `0.60` | Two-way | Posição no arco. Sempre coercida para `[0.0, 1.0]`. |
| `TrackColor` | `Avalonia.Media.Color` | `StyledProperty<Color>` | `Colors.White` | One-way in | Cor do arco preenchido e do stroke do handle. |
| `TrackThickness` | `double` | `StyledProperty<double>` | `9.0` | One-way in | Espessura do arco em pixels. |
| `Radius` | `double` | `StyledProperty<double>` | `90.0` | One-way in | Raio do arco a partir do centro do controle. |

`Value` deve ser registrado com `CoerceValue` que aplica `Math.Clamp(value, 0.0, 1.0)`.

#### 5.2.2 Geometria do arco

| Parâmetro | Valor |
|---|---|
| Extensão total | 300° |
| Ângulo de início | 120° (sentido horário a partir do topo — equivalente a ~4h10 no relógio) |
| Ângulo de fim | 420° contínuo (equivalente a 120° + 300°, ou ~1h50 passando pelo topo) |
| Gap | 60°, posicionado na base inferior (de ~7h50 a ~4h10) |
| Sentido de progressão | Horário — `Value = 0.0` posiciona handle no início (120°); `Value = 1.0` posiciona handle no fim (420°) |
| Referência de ângulo 0° | Topo do controle (12h) |

**Fórmula de coordenadas** para um ponto no arco dado ângulo `α` em graus (referência topo, horário):
```
rad = (α − 90.0) * Math.PI / 180.0
x   = centerX + Radius * Math.Cos(rad)
y   = centerY + Radius * Math.Sin(rad)
```
Onde `centerX = Width / 2.0`, `centerY = Height / 2.0`.

**Track de fundo:** arco completo de 120° a 420°, cor `#1C2338`, espessura `TrackThickness`, `StrokeLineCap.Round` em ambas as pontas.

**Track preenchido:** arco de 120° a `120° + Value × 300°`, cor `TrackColor`, espessura `TrackThickness`, `StrokeLineCap.Round`.

Ambos os arcos são recalculados via `StreamGeometry` sempre que `Value`, `Radius` ou `TrackThickness` mudam.

#### 5.2.3 Handle

| Atributo | Valor |
|---|---|
| Visual | `Ellipse` 14×14px |
| `Fill` | `Colors.White` |
| `Stroke` | `TrackColor` |
| `StrokeThickness` | 2px |
| Área de hit test | `Border` transparente de 28×28px centralizado sobre o visual |
| Posicionamento | `Canvas.Left = x − 7`, `Canvas.Top = y − 7`, onde `(x, y)` é o ponto no arco correspondente a `α = 120° + Value × 300°` |

Handle e sua área de hit test são filhos de um `Canvas` local do `BlossomArcSlider`, e devem ter `ZIndex` superior ao das `StreamGeometry` tracks.

#### 5.2.4 Interação, captura de pointer e zona de resposta

**Zona de resposta ao `PointerPressed`:**
O `BlossomArcSlider` é um `UserControl` de 220×220px que se sobrepõe ao Canvas inteiro, incluindo a área central onde ficam as petals e o núcleo. Para não bloquear cliques nesses elementos (que ficam atrás na z-order), o `BlossomArcSlider` só inicia captura quando o pointer cai na zona anular do arc track.

Em `PointerPressed`, antes de capturar:
```
dx       = pointer.X − centerX
dy       = pointer.Y − centerY
distance = Math.Sqrt(dx * dx + dy * dy)
innerBound = Radius − TrackThickness * 2.0   // ~71.0px com defaults
outerBound = Radius + TrackThickness * 2.0   // ~108.0px com defaults

if (distance < innerBound || distance > outerBound)
    return; // não captura — permite propagação para petals/núcleo abaixo na z-order
```

Se o pointer estiver dentro da zona anular, o controle executa:
1. `e.Pointer.Capture(this)`
2. Calcula e atualiza `Value` a partir da posição.
3. `e.Handled = true`.

**`PointerMoved`:**
- Se `e.Pointer.Captured != this`: ignora sem executar nenhuma ação.
- Caso contrário: calcula e atualiza `Value`.

**`PointerReleased`:**
1. `e.Pointer.Capture(null)`.
2. Calcula e emite `Value` final.
3. `e.Handled = true`.

**Conversão posição → ângulo → Value:**
```
dx       = pointer.X − centerX
dy       = pointer.Y − centerY
angleRad = Math.Atan2(dy, dx)
angleDeg = (angleRad * 180.0 / Math.PI) + 90.0   // +90 para referenciar ao topo
if (angleDeg < 0.0)   angleDeg += 360.0
if (angleDeg >= 360.0) angleDeg -= 360.0

// Mapear para o espaço contínuo 120°..420°
arcAngle = (angleDeg < 120.0) ? angleDeg + 360.0 : angleDeg

// Tratar o gap (420°..480° contínuo = 60°..120° original)
// Snap para a extremidade mais próxima do arco
if (arcAngle > 420.0)
    arcAngle = (arcAngle < 450.0) ? 420.0 : 480.0  // 480° → início contínuo = 120° + 360°
    // após snap para 480°: normalizar de volta: value = 0.0

value = Math.Clamp((arcAngle − 120.0) / 300.0, 0.0, 1.0)
```

`Value` é coercido para `[0.0, 1.0]` antes de qualquer emissão — nunca sai fora deste intervalo.

---

### 5.3 `BlossomPetalItem` (internal)

**Tipo:** `UserControl` interno.

**Responsabilidade:** Renderizar uma petal circular com estados de hover, seleção e bloom.

#### 5.3.1 Propriedades (internal ao assembly)

| Propriedade | Tipo | Mecanismo | Padrão | Descrição |
|---|---|---|---|---|
| `PetalColor` | `Avalonia.Media.Color` | `StyledProperty<Color>` | `Colors.Gray` | Cor de fill da petal. |
| `IsSelected` | `bool` | `StyledProperty<bool>` | `false` | Estado de seleção — exibe stroke branco sólido. |
| `IsBloomVisible` | `bool` | `StyledProperty<bool>` | `false` | Controla a animação bloom. Distinto de `IsVisible`. |

#### 5.3.2 Dimensões e hit target

| Atributo | Valor |
|---|---|
| Dimensão visual | `Ellipse` de 26×26px |
| Hit target | 44×44px — implementado com padding transparente ou `HitTestMargin` ao redor do `Ellipse` |
| `RenderTransformOrigin` | `RelativePoint.Center` (0.5, 0.5) — escala a partir do próprio centro |

#### 5.3.3 Estados visuais

| Estado | Fill | Stroke | StrokeThickness | Opacity | ScaleX/ScaleY |
|---|---|---|---|---|---|
| `IsBloomVisible = false` | `PetalColor` | Nenhum | 0 | 0.0 | 0.0 / 0.0 |
| Normal (`IsBloomVisible = true`) | `PetalColor` | Nenhum | 0 | 1.0 | 1.0 / 1.0 |
| `:pointerover` | `PetalColor` | `#FFFFFF` @ 40% opacidade | 2 | 1.0 | 1.0 / 1.0 |
| `IsSelected = true` | `PetalColor` | `#FFFFFF` @ 100% opacidade | 2 | 1.0 | 1.0 / 1.0 |
| `IsSelected = true` + `:pointerover` | `PetalColor` | `#FFFFFF` @ 100% opacidade | 2 | 1.0 | 1.0 / 1.0 |

Não existe modificação de brilho ou filtro no fill. O estado hover usa exclusivamente o stroke branco parcialmente opaco.

**Transições declaradas no `Style` do `BlossomPetalItem`**, ativadas por mudança em `IsBloomVisible`:
- `ScaleTransform.ScaleX` e `ScaleTransform.ScaleY`: 0.0 → 1.0, 280ms, `CubicEaseOut`.
- `Opacity`: 0.0 → 1.0, 200ms, linear.

Estas transições devem ser declaradas em AXAML usando `Transitions` no `Style`. A `RenderTransform` deve ser um `ScaleTransform` definido no template.

---

### 5.4 `HslColor` (public struct)

**Tipo:** `readonly struct`, `public`, sem dependências de Avalonia UI. Localizado em `ColorPicker/Models/HslColor.cs`.

#### 5.4.1 Contrato público completo

```csharp
public readonly struct HslColor : IEquatable<HslColor>
{
    public double H { get; }  // [0.0, 360.0) — normalizado no construtor
    public double S { get; }  // [0.0, 1.0]   — clamped no construtor
    public double L { get; }  // [0.0, 1.0]   — clamped no construtor

    public HslColor(double h, double s, double l);

    // Parsing
    public static HslColor FromHex(string hex);
    public static bool TryParseHex(string? hex, out HslColor result);

    // Conversão de/para Avalonia.Media.Color
    public static HslColor FromAvaloniaColor(Avalonia.Media.Color color);
    public Avalonia.Media.Color ToAvaloniaColor();

    // Serialização
    public string ToHex();

    // Derivação imutável
    public HslColor WithHue(double h);
    public HslColor WithSaturation(double s);
    public HslColor WithLightness(double l);

    // Igualdade
    public bool Equals(HslColor other);
    public override bool Equals(object? obj);
    public override int GetHashCode();
    public static bool operator ==(HslColor left, HslColor right);
    public static bool operator !=(HslColor left, HslColor right);
}
```

#### 5.4.2 Normalização no construtor

| Componente | Regra | Exemplos |
|---|---|---|
| `H` | `H = ((h % 360.0) + 360.0) % 360.0` | 370.0 → 10.0; −10.0 → 350.0; 360.0 → 0.0 |
| `S` | `S = Math.Clamp(s, 0.0, 1.0)` | −0.1 → 0.0; 1.1 → 1.0 |
| `L` | `L = Math.Clamp(l, 0.0, 1.0)` | −0.1 → 0.0; 1.1 → 1.0 |

`WithHue`, `WithSaturation` e `WithLightness` aplicam as mesmas regras e retornam um novo `HslColor`. A struct é imutável.

#### 5.4.3 Parsing — entradas aceitas e rejeitadas

| Entrada | `FromHex` | `TryParseHex` |
|---|---|---|
| `"#RRGGBB"` (6 dígitos, com `#`, qualquer case) | Aceito | `true` |
| `"RRGGBB"` (6 dígitos, sem `#`, qualquer case) | Aceito | `true` |
| `null` | Lança `ArgumentException` | `false`, `result = default` |
| `""` ou whitespace | Lança `ArgumentException` | `false` |
| `"#RGB"` (3 dígitos) | Lança `ArgumentException` — não expande | `false` |
| `"#RRGGBBAA"` (8 dígitos) | Lança `ArgumentException` — não extrai | `false` |
| Dígitos hex inválidos | Lança `ArgumentException` | `false` |
| Qualquer outro formato | Lança `ArgumentException` | `false` |

#### 5.4.4 Saída de `ToHex`

`ToHex` sempre retorna `"#RRGGBB"`: prefixo `#` obrigatório, 6 dígitos, maiúsculas (`A`–`F`), sem canal alpha.

Exemplo de conformidade: `new HslColor(350.0, 0.68, 0.55).ToHex()` deve retornar `"#E16174"`.

#### 5.4.5 Algoritmos de conversão

**HSL → RGB:** algoritmo padrão W3C (CSS Color Level 3, §4.2.4).

**RGB → HSL:** inversão padrão do mesmo algoritmo. Para cores acromáticas (`R == G == B`), `H` deve ser `0.0` e `S` deve ser `0.0` — nunca `NaN`.

**Round-trip:** `ToHex → FromHex → ToHex` deve produzir resultado idêntico para todas as 8 cores de petal definidas em §5.1.6. Isso deve ser verificado pelos testes unitários U20.

---

## 6. Comportamento normativo do picker

### 6.1 Abertura do picker (primeiro clique no swatch)

**Pré-condição:** `_isPickerOpen = false`.

1. `_colorBeforePickerOpened = SelectedColor` (snapshot — sempre válido pela coerção).
2. `hsl = HslColor.FromHex(SelectedColor)`.
3. `_hue = hsl.H`.
4. `_currentSaturation = hsl.S`.
5. `_lightness = Math.Clamp(hsl.L, 0.30, 0.80)`.
6. Determina `_selectedPetalIndex` conforme regra de §5.1.6 (tolerância 15°, usando `_hue`).
7. Define `IsSelected` de cada `BlossomPetalItem`: `true` na petal de índice `_selectedPetalIndex`, `false` nas demais. Se `_selectedPetalIndex = -1`, todas recebem `false`.
8. Posiciona o arc slider: `arcSlider.Value = (_lightness − 0.30) / 0.50`.
9. Define `arcSlider.TrackColor`:
   - Se `_selectedPetalIndex >= 0`: `Color.Parse(petalHexBase[_selectedPetalIndex])`.
   - Se `_selectedPetalIndex = -1`: `Color.Parse("#5B7CFA")`.
10. `_isPickerOpen = true` → abre o Popup.
11. Inicia sequência bloom (§5.1.5).

### 6.2 Fechamento do picker pelo swatch (segundo clique)

**Pré-condição:** `_isPickerOpen = true`.

1. Para o bloom: se `_bloomTimer != null`, `_bloomTimer.Stop()`, `_bloomTimer = null`.
2. Define `IsBloomVisible = false` em todas as petals (sem transição).
3. `_isPickerOpen = false` → fecha o Popup.
4. `SelectedColor` mantém o último valor da sessão. Não há revert.
5. `_colorBeforePickerOpened` não é limpo — é um campo auxiliar que será sobrescrito na próxima abertura.

### 6.3 Fechamento por light dismiss (clique fora do Popup)

**Pré-condição:** `_isPickerOpen = true`. O usuário clicou fora dos limites do Popup.

O Popup fecha automaticamente via `IsLightDismissEnabled = true`. O controle detecta o fechamento via o evento `Popup.Closed` e executa os passos 1–4 de §6.2.

Resultado idêntico a §6.2: `SelectedColor` mantém o último valor. Sem revert.

### 6.4 Fechamento por clique no núcleo interno

**Pré-condição:** `_isPickerOpen = true`.

O handler de `Click` do núcleo interno executa os passos 1–4 de §6.2. Resultado idêntico a §6.2. O evento de click é marcado como `e.Handled = true` para não propagar ao swatch.

### 6.5 Fechamento por Escape

**Pré-condição:** `_isPickerOpen = true`, foco dentro do Popup, tecla `Escape` pressionada.

1. Para o bloom (mesmos passos 1–2 de §6.2).
2. `_isPickerOpen = false` → fecha o Popup.
3. `SelectedColor = _colorBeforePickerOpened` (revert — a coerção garante que seja válido).
4. `hsl = HslColor.FromHex(_colorBeforePickerOpened)`.
5. `_hue = hsl.H`.
6. `_currentSaturation = hsl.S`.
7. `_lightness = Math.Clamp(hsl.L, 0.30, 0.80)`.
8. Determina `_selectedPetalIndex` conforme regra de §5.1.6.
9. Define `IsSelected` de cada `BlossomPetalItem` conforme `_selectedPetalIndex`.
10. `e.Handled = true` — o evento não propaga para o formulário.

**Regra de snap do arc slider no revert:** Se `_colorBeforePickerOpened` tiver `L` fora de `[0.30, 0.80]`, `_lightness` é clamped para o extremo mais próximo (§5.4.2 não afeta, pois o clamp é aplicado aqui explicitamente). O `arcSlider.Value` é atualizado para refletir `_lightness` revertido: `arcSlider.Value = (_lightness − 0.30) / 0.50`.

### 6.6 Escape sem o picker aberto

O controle não registra handler de `KeyDown` quando `_isPickerOpen = false`. O evento propaga normalmente aos ancestrais.

### 6.7 Clique em petal

**Pré-condição:** `_isPickerOpen = true`, petal clicada de índice `i`.

1. `_selectedPetalIndex = i`.
2. `IsSelected = true` na petal `i`; `IsSelected = false` em todas as demais.
3. `_hue = petalHue[i]` (valor canônico da tabela §5.1.6).
4. `_currentSaturation = petalSaturation[i]` (valor canônico da tabela §5.1.6).
5. `newColor = new HslColor(_hue, _currentSaturation, _lightness).ToHex()`.
6. `SelectedColor = newColor`.
7. `arcSlider.TrackColor = Color.Parse(petalHexBase[i])`.
8. O Popup permanece aberto.
9. O núcleo interno e o swatch externo atualizam o `Background` imediatamente via o `PropertyChanged` callback de `SelectedColor`.

### 6.8 Drag do arc slider

**Pré-condição:** `_isPickerOpen = true`, pointer capturado pelo arc slider na zona anular (ver §5.2.4).

1. O `BlossomArcSlider` emite `Value` a cada `PointerMoved` com pointer capturado.
2. O `BlossomColorPickerControl` responde à mudança de `Value` via callback registrado em `BlossomArcSlider.ValueProperty`:
   - `_lightness = 0.30 + arcSlider.Value × 0.50`
   - `newColor = new HslColor(_hue, _currentSaturation, _lightness).ToHex()`
   - `SelectedColor = newColor`
3. `SelectedColor` recebe atualizações a cada frame de drag. Todos os valores emitidos são hex strings canônicas e válidas.
4. O Popup permanece aberto durante todo o drag.

**Quando `_selectedPetalIndex = -1`:** `_currentSaturation` retém o valor inicializado na abertura do picker (derivado de `SelectedColor`). O cálculo usa este valor sem alteração.

### 6.9 Comportamento quando nenhuma petal corresponde ao hue atual

Quando `_selectedPetalIndex = -1`:
- Nenhuma petal exibe stroke de seleção.
- `arcSlider.TrackColor = Color.Parse("#5B7CFA")`.
- Drag do slider usa `_currentSaturation` (derivado do `SelectedColor` na abertura).
- Clicar em uma petal define `_selectedPetalIndex`, `_hue`, `_currentSaturation` e `arcSlider.TrackColor` normalmente (§6.7).

---

## 7. Fluxo de dados completo

### 7.1 Diagrama de fluxo

```
ConnectionProfile.Color (string hex, persistido em JSON)
  ↓ ConnectionProfileFormMapper.ToFormData()
ConnectionProfileFormData.Color
  ↓ ConnectionManagerViewModel.LoadProfile()
ConnectionManagerViewModel.EditColor  (INotifyPropertyChanged)
  ↕ two-way binding em AXAML
BlossomColorPickerControl.SelectedColor  (StyledProperty com CoerceValue)
  ↑ interação do usuário (petals, arc slider) — emissão em tempo real
  ↓ volta pelo binding two-way
ConnectionManagerViewModel.EditColor
  ↓ ao clicar "Salvar" → ConnectionProfileFormMapper.ToProfile()
ConnectionProfile.Color
  ↓ ConnectionProfileStore.Save()
JSON persistido
```

### 7.2 O que `SelectedColor` emite em cada momento

| Momento | Emissão de `SelectedColor` |
|---|---|
| Picker fechado, sem interação | Nenhuma — `SelectedColor` está estável no valor de `EditColor` |
| Picker abrindo | Nenhuma — a abertura não altera `SelectedColor` |
| Clique em petal | Emite novo hex calculado com hue/sat da petal e `_lightness` atual |
| Drag do arc slider | Emite novo hex a cada frame (todos canônicos e válidos) |
| Escape | Emite o hex revertido (`_colorBeforePickerOpened`) |
| Light dismiss / clique no núcleo / swatch | Nenhuma emissão adicional |
| Receber `null` via binding externo | `CoerceValue` emite `"#5B7CFA"` (fallback) |
| Receber hex minúsculas ex. `"#e16174"` | `CoerceValue` emite `"#E16174"` (normalizado) |

### 7.3 Commit e persistência

| Ação | `EditColor` muda? | `ConnectionProfile.Color` persiste? |
|---|---|---|
| Clique em petal | Sim | Não |
| Drag do arc slider | Sim (a cada frame) | Não |
| Escape no picker | Sim (revert) | Não |
| Light dismiss / fechar picker | Não | Não |
| Clicar "Salvar" no formulário | — | Sim |
| Clicar "Cancelar" / fechar formulário sem salvar | — | Não |
| Trocar de profile na lista sem salvar | `EditColor` sobrescrito pelo novo profile | Não (alterações anteriores descartadas) |

---

## 8. Modelo de dados

### 8.1 `ConnectionProfile` (modificar arquivo existente)

Adicionar ao final da lista de propriedades:

```csharp
public string Color { get; set; } = "#5B7CFA";

[System.Text.Json.Serialization.JsonIgnore]
public bool IsActive { get; set; }
```

**`Color`:** Inicializado com `"#5B7CFA"` garante que `new ConnectionProfile()` tenha sempre um valor válido.

**`IsActive`:** Propriedade de conveniência UI, não serializada. Gerenciada pelo `ConnectionManagerViewModel` (§8.3). Usada diretamente pelo binding em `ConnectionManagerControl.axaml` (§9.6).

**Métodos `WithProtectedPassword()` e `WithUnprotectedPassword()`:** Devem ser atualizados para incluir `Color = Color` e `IsActive = IsActive` na inicialização do objeto retornado. Omiti-los causaria reset silencioso para o padrão.

### 8.2 `ConnectionProfileFormData` (modificar arquivo existente)

Adicionar `Color` como **último** parâmetro posicional do record struct — minimiza quebra de compatibilidade com código que inicializa por posição:

```csharp
public readonly record struct ConnectionProfileFormData(
    string Id,
    string Name,
    DatabaseProvider Provider,
    string Host,
    int Port,
    string Database,
    string Username,
    string Password,
    bool RememberPassword,
    bool UseSsl,
    bool TrustServerCertificate,
    bool UseIntegratedSecurity,
    int TimeoutSeconds,
    string ConnectionUrl,
    string Color);
```

### 8.3 `ConnectionManagerViewModel` (modificar arquivo existente)

**Propriedade `EditColor`:**
```csharp
private string _editColor = "#5B7CFA";
public string EditColor
{
    get => _editColor;
    set
    {
        if (_editColor == value) return;
        _editColor = value;
        OnPropertyChanged();
    }
}
```

**Regras de ciclo de vida de `EditColor`:**
- `LoadProfile(ConnectionProfile profile)`: `EditColor = profile.Color ?? "#5B7CFA"`.
- `NewProfileCommand`: `EditColor = "#5B7CFA"`.
- `SaveProfileCommand`: `EditColor` é lido pelo mapper — sem reset após salvar.

**Gerenciamento de `IsActive`:**
Sempre que `ActiveProfileId` muda (conexão estabelecida ou desconectada), o VM deve:
1. Definir `IsActive = false` em todos os profiles de `Profiles`.
2. Se o novo `ActiveProfileId` não for nulo/vazio, localizar o profile com `Id == ActiveProfileId` e definir `IsActive = true`.

Isso deve ocorrer em todo método ou setter que altere `ActiveProfileId`.

### 8.4 `ConnectionProfileFormMapper` (modificar arquivo existente)

**Profile → FormData:**
```csharp
Color = string.IsNullOrWhiteSpace(profile.Color) ? "#5B7CFA" : profile.Color
```

**FormData → Profile:**
```csharp
profile.Color = string.IsNullOrWhiteSpace(formData.Color) ? "#5B7CFA" : formData.Color
```

O mapper aplica fallback apenas para nulo/vazio/whitespace — não valida o formato hex. A validação de formato fica na coerção do `BlossomColorPickerControl`.

### 8.5 Persistência no `ConnectionProfileStore`

Nenhuma alteração é necessária. O campo `Color` é serializado automaticamente por ser propriedade pública. `IsActive` não é serializado (`[JsonIgnore]`). Profiles sem o campo `Color` no JSON são desserializados com `Color = null`; o mapper aplica o fallback ao carregar.

---

## 9. Integração em `ConnectionManagerControl.axaml`

### 9.1 Namespace a importar

```xml
xmlns:dbc="using:DBWeaver.UI.Controls"
```

### 9.2 Ajuste de `RowDefinitions` do painel direito

O `Grid` do painel direito (`Grid.Column="1"`) tem atualmente `RowDefinitions="*,Auto"`. Alterar para:

```xml
RowDefinitions="Auto,*,Auto"
```

| Elemento | Antes | Depois |
|---|---|---|
| Faixa colorida (nova) | — | `Grid.Row="0"` |
| `ScrollViewer` | `Grid.Row="0"` | `Grid.Row="1"` |
| Footer com botões | `Grid.Row="1"` | `Grid.Row="2"` |

Quando `IsEditing = false`, a `Border` da faixa (Row 0, `Height="3"`) fica oculta e a Row `Auto` colapsa para 0px — comportamento nativo do Avalonia.

### 9.3 Faixa colorida no header do formulário

Inserir como primeiro filho do Grid do painel direito:

```xml
<Border Grid.Row="0"
        Height="3"
        IsVisible="{Binding IsEditing}"
        Background="{Binding EditColor, Converter={StaticResource HexToSolidBrushConverter}}"
        HorizontalAlignment="Stretch"/>
```

### 9.4 Campo "Cor da conexão" no formulário

Inserir dentro do `StackPanel IsVisible="{Binding IsEditing}"`, **após** o bloco do campo `Name` e **antes** do bloco de URL:

```xml
<TextBlock Classes="label" Text="Cor da conexão"/>
<dbc:BlossomColorPickerControl
    SelectedColor="{Binding EditColor, Mode=TwoWay}"
    Margin="0,0,0,12"/>
```

O `x:DataType` do contexto aqui é `vm:ConnectionManagerViewModel`. O binding `EditColor` refere-se a `ConnectionManagerViewModel.EditColor`.

### 9.5 Bullet colorido na lista

No `DataTemplate` do `ListBox.ItemTemplate` (`x:DataType="connmodels:ConnectionProfile"`), substituir:

```xml
<!-- Remover -->
<TextBlock Grid.Column="0" Text="●"
           Foreground="{StaticResource AccentPrimaryBrush}"
           FontSize="{StaticResource FontSizeCaption}"
           VerticalAlignment="Center" Margin="0,0,8,0"/>

<!-- Substituir por -->
<TextBlock Grid.Column="0" Text="●"
           Foreground="{Binding Color, Converter={StaticResource HexToSolidBrushConverter}}"
           FontSize="{StaticResource FontSizeCaption}"
           VerticalAlignment="Center" Margin="0,0,8,0"/>
```

### 9.6 Borda lateral no item ativo

O DataTemplate atual tem `Grid ColumnDefinitions="Auto,*"`. Alterar para `ColumnDefinitions="Auto,Auto,*"` e inserir a borda como primeiro filho:

```xml
<Grid ColumnDefinitions="Auto,Auto,*">

  <Border Grid.Column="0"
          Width="3"
          CornerRadius="2,0,0,2"
          Margin="0,2,6,2"
          Background="{Binding Color, Converter={StaticResource HexToSolidBrushConverter}}"
          IsVisible="{Binding IsActive}"/>

  <TextBlock Grid.Column="1"
             Text="●"
             Foreground="{Binding Color, Converter={StaticResource HexToSolidBrushConverter}}"
             FontSize="{StaticResource FontSizeCaption}"
             VerticalAlignment="Center" Margin="0,0,8,0"/>

  <StackPanel Grid.Column="2" Spacing="2">
    <!-- conteúdo existente do StackPanel, sem alteração -->
  </StackPanel>

</Grid>
```

O binding `IsVisible="{Binding IsActive}"` refere-se a `ConnectionProfile.IsActive` (§8.1). Este campo é gerenciado pelo VM (§8.3) e não serializado.

### 9.7 `HexToSolidBrushConverter`

Implementado em `DBWeaver.UI` (não em `DBWeaver.UI.Controls`). Declarado como recurso estático em `AppStyles.axaml` ou em `ConnectionManagerControl.axaml`.

**Contrato:**
- Tipo de entrada: `string?` (valor do binding).
- Se nulo, vazio, whitespace ou hex inválido: retorna `new SolidColorBrush(Color.Parse("#5B7CFA"))`.
- Se hex válido: retorna `new SolidColorBrush(Color.Parse(value!))`.
- Nunca lança exceção.
- `ConvertBack`: não implementado (`throw new NotSupportedException()`).

---

## 10. Posicionamento e visibilidade do Popup

| Atributo | Valor |
|---|---|
| `PlacementMode` | `Bottom` |
| `PlacementTarget` | O swatch (`Button` de 28×28px) |
| `PlacementConstraintAdjustment` | `FlipY \| SlideX` |
| `HorizontalOffset` | `−96.0` — centraliza canvas de 220px sobre swatch de 28px: `−(220/2 − 28/2) = −96` |
| `VerticalOffset` | `4.0` |

**`FlipY`:** se não houver espaço abaixo, o Popup abre acima do swatch.
**`SlideX`:** se o Popup extrapolar lateralmente, desliza horizontalmente até caber nos limites da janela.

O Avalonia aplica os constraints automaticamente com `PlacementConstraintAdjustment` — nenhum código adicional é necessário para o comportamento de borda.

**Resize da janela com Popup aberto:** O Avalonia recalcula a posição do Popup a cada layout pass. O Popup pode reposicionar. O picker permanece aberto.

**Perda de foco da janela com Popup aberto:** `IsLightDismissEnabled = true` fecha o Popup, equivalente a light dismiss. `SelectedColor` mantém o último valor.

---

## 11. Regras de fallback e resiliência

### 11.1 Valor fallback canônico

`"#5B7CFA"` — token `AccentPrimary` do tema Graph Energy v1. É o único fallback; não existe fallback secundário.

### 11.2 Tabela de situações de fallback

| Situação | Onde tratado | Ação |
|---|---|---|
| `ConnectionProfile.Color = null` (JSON sem o campo) | `ConnectionProfileFormMapper` | `EditColor = "#5B7CFA"` |
| `ConnectionProfile.Color = ""` | `ConnectionProfileFormMapper` | `EditColor = "#5B7CFA"` |
| `ConnectionProfile.Color = "   "` | `ConnectionProfileFormMapper` | `EditColor = "#5B7CFA"` |
| `ConnectionProfile.Color = "#RGB"` (3 dígitos) | `ConnectionProfileFormMapper` | `EditColor = "#5B7CFA"` — não expande |
| `ConnectionProfile.Color = "#RRGGBBAA"` (8 dígitos) | `ConnectionProfileFormMapper` | `EditColor = "#5B7CFA"` — não extrai |
| `ConnectionProfile.Color` com dígitos inválidos | `ConnectionProfileFormMapper` | `EditColor = "#5B7CFA"` |
| `SelectedColor` recebe `null` via binding | `CoerceValue` do controle | Emite `"#5B7CFA"` |
| `SelectedColor` recebe hex inválido via binding | `CoerceValue` do controle | Emite `"#5B7CFA"` |
| `SelectedColor` recebe minúsculas ex. `"#e16174"` | `CoerceValue` do controle | Emite `"#E16174"` (normalizado) |
| `_colorBeforePickerOpened` corrompido (edge case extremo) | Revert no Escape | Aplica `"#5B7CFA"` via `CoerceValue` ao atribuir |

**Responsabilidade dividida:**
- O `ConnectionProfileFormMapper` aplica fallback para dados legados e corrompidos no modelo.
- O `CoerceValue` do `BlossomColorPickerControl` aplica fallback e normalização para qualquer valor que chegue via binding — incluindo dado legado que passou pelo mapper sem ser corrigido.
- O `HexToSolidBrushConverter` aplica fallback para o binding visual nas superfícies de cor.

### 11.3 Garantia de emissão válida

A coerção é a única defesa necessária. Implementação padrão Avalonia:

```csharp
public static readonly StyledProperty<string> SelectedColorProperty =
    AvaloniaProperty.Register<BlossomColorPickerControl, string>(
        nameof(SelectedColor),
        defaultValue: "#5B7CFA",
        coerce: CoerceSelectedColor);

private static string CoerceSelectedColor(AvaloniaObject obj, string? value)
{
    if (!HslColor.TryParseHex(value, out _))
        return "#5B7CFA";
    return HslColor.FromHex(value!).ToHex();
}
```

O método `CoerceSelectedColor` é chamado automaticamente pelo Avalonia ao atribuir qualquer valor à propriedade, independentemente da origem (código, binding, setter AXAML).

---

## 12. Regras de contraste e legibilidade

### 12.1 Faixa segura de lightness no slider

O arc slider impõe `L ∈ [0.30, 0.80]`:
- `L < 0.30`: cores escuras demais — confundem-se com `Bg0 = #090B14`.
- `L > 0.80`: cores claras demais — baixo contraste contra outras cores da lista.

Esta faixa é imposta pela conversão `Value → _lightness` em §6.8: `_lightness = 0.30 + Value × 0.50`. O `HslColor` struct aceita qualquer `L ∈ [0.0, 1.0]` — a faixa segura é restrição do picker, não da struct.

### 12.2 Valores de `L` fora da faixa em dados existentes

Se `ConnectionProfile.Color` tiver `L` fora de `[0.30, 0.80]`, o valor é aceito como `SelectedColor` sem modificação — o picker não reescreve `L` ao receber via binding. Ao abrir o picker, `_lightness` é clamped (§6.1, passo 5) e o arc slider é posicionado no extremo mais próximo. O valor emitido ao interagir com o picker estará dentro da faixa segura.

### 12.3 Validação pré-definida das petals

As 8 cores de petal têm `L ∈ [0.45, 0.65]`. Contraste mínimo verificado: ≥ 3.0:1 contra `Bg0 = #090B14` (WCAG AA para componentes de UI não-texto). Nenhuma validação de contraste é necessária em runtime.

### 12.4 Consistência visual entre superfícies

As quatro superfícies exibem exatamente o mesmo hex sem ajuste de opacidade, saturação ou luminosidade. Durante edição, swatch e faixa exibem `EditColor`; bullet e borda lateral exibem `ConnectionProfile.Color`. Essa divergência é intencional e documentada em §4.2.

---

## 13. Edge cases obrigatórios

| # | Cenário | Comportamento definido |
|---|---|---|
| 1 | `SelectedColor = null` via binding externo | `CoerceValue` emite `"#5B7CFA"`; fallback propagado de volta ao binding |
| 2 | Segundo clique no swatch antes do bloom terminar | Bloom cancelado; picker fecha imediatamente (`_bloomTimer` parado); `SelectedColor` mantém valor atual |
| 3 | Escape antes do bloom terminar | Bloom cancelado; picker fecha; `SelectedColor` revertido para `_colorBeforePickerOpened` |
| 4 | Drag no arc slider saindo dos limites visuais | Drag continua (pointer capturado); `Value` clamped em `[0.0, 1.0]` |
| 5 | Drag no arc slider passando pelo gap inferior | Snap para extremidade mais próxima do arco (ver §5.2.4) |
| 6 | Clique no centro do Canvas do picker (região das petals) via arc slider | Arc slider não captura (distância < `innerBound`); evento propaga para petal ou núcleo |
| 7 | Profile legado sem campo `Color` no JSON | Mapper aplica `"#5B7CFA"`; sem crash; sem UI vazia |
| 8 | `ConnectionProfile.Color = "#RGB"` | Mapper aplica fallback; não expande para 6 dígitos |
| 9 | Janela do app perde foco com picker aberto | Light dismiss fecha o picker; `SelectedColor` mantém último valor |
| 10 | Troca de profile na lista com picker aberto | Light dismiss fecha o picker; `EditColor` sobrescrito pelo novo profile |
| 11 | Clique em "+ Nova conexão" com picker aberto | Picker fecha por light dismiss; `EditColor` = `"#5B7CFA"` pelo `NewProfileCommand` |
| 12 | `_selectedPetalIndex = -1`, usuário draga o slider | `_currentSaturation` retém valor da abertura; cálculo normal |
| 13 | Dois profiles com a mesma cor | Sem problema — cor é rótulo, não identificador único |
| 14 | Resize da janela com picker aberto | Popup reposiciona; picker permanece aberto |
| 15 | Reabrir o picker 10+ vezes em sequência | Sem acúmulo de estado; `_bloomTimer` sempre parado antes de recriar |
| 16 | `WithProtectedPassword()` chamado após adicionar `Color` e `IsActive` | `Color` e `IsActive` devem ser copiados explicitamente no objeto retornado |
| 17 | Controle removido da árvore visual com picker aberto | `OnDetachedFromVisualTree` para `_bloomTimer`, fecha o popup; sem referências pendentes |
| 18 | `EditColor` recebe valor via binding antes do controle ser carregado | `CoerceValue` garante normalização; sem crash; valor correto exibido ao renderizar |

---

## 14. Estratégia de testes

### 14.1 Testes unitários — `tests/VisualSqlArchitect.Tests/`

#### `HslColor`

| # | Método | Entrada | Resultado esperado |
|---|---|---|---|
| U01 | `FromHex` | `"#E16174"` | `H ≈ 350.0`, `S ≈ 0.68`, `L ≈ 0.55` (tolerância ±0.5°, ±0.005) |
| U02 | `FromHex` | `"e16174"` (sem `#`, minúsculas) | Resultado idêntico a U01 |
| U03 | Round-trip | `"#E16174"` | `FromHex → ToHex` retorna `"#E16174"` |
| U04 | `FromHex` | `null` | Lança `ArgumentException` |
| U05 | `FromHex` | `""` | Lança `ArgumentException` |
| U06 | `FromHex` | `"   "` | Lança `ArgumentException` |
| U07 | `FromHex` | `"#E61"` (3 dígitos) | Lança `ArgumentException` |
| U08 | `FromHex` | `"#E16174FF"` (8 dígitos) | Lança `ArgumentException` |
| U09 | `FromHex` | `"#GGHHII"` | Lança `ArgumentException` |
| U10 | `TryParseHex` | `null` | Retorna `false`; sem exception |
| U11 | `TryParseHex` | `"#RGB"` | Retorna `false`; sem exception |
| U12 | `ToHex` | `new HslColor(0, 0, 0)` | `"#000000"` |
| U13 | `ToHex` | `new HslColor(0, 0, 1)` | `"#FFFFFF"` |
| U14 | `ToHex` | Qualquer `HslColor` válido | Começa com `#`, 7 caracteres, dígitos A–F maiúsculos |
| U15 | Construtor | `H = 370.0` | `H = 10.0` |
| U16 | Construtor | `H = -10.0` | `H = 350.0` |
| U17 | Construtor | `H = 360.0` | `H = 0.0` |
| U18 | Construtor | `S = 1.5` | `S = 1.0` |
| U19 | Construtor | `L = -0.1` | `L = 0.0` |
| U20 | `WithLightness` | `l = 1.5` | `L = 1.0` |
| U21 | Round-trip | Todas as 8 cores de petal (§5.1.6) | `ToHex → FromHex → ToHex` produz hex idêntico para cada uma |
| U22 | `FromAvaloniaColor` | `Color.Parse("#E16174")` | Mesmo resultado de U01 |
| U23 | `ToAvaloniaColor` | `new HslColor(350.0, 0.68, 0.55)` | `Color.Parse("#E16174")` |
| U24 | Acromático RGB → HSL | `Color.FromRgb(128, 128, 128)` | `H = 0.0`, `S = 0.0` (sem `NaN`) |

#### `ConnectionProfileFormMapper`

| # | Cenário | Resultado esperado |
|---|---|---|
| U25 | Profile com `Color = null` → FormData | `formData.Color = "#5B7CFA"` |
| U26 | Profile com `Color = ""` → FormData | `formData.Color = "#5B7CFA"` |
| U27 | Profile com `Color = "#E16174"` → FormData | `formData.Color = "#E16174"` — sem modificação |
| U28 | FormData com `Color = null` → Profile | `profile.Color = "#5B7CFA"` |
| U29 | FormData com `Color = ""` → Profile | `profile.Color = "#5B7CFA"` |
| U30 | FormData com `Color = "#E16174"` → Profile | `profile.Color = "#E16174"` — sem modificação |

### 14.2 Testes manuais — checklist pré-merge

| # | Cenário | Resultado esperado |
|---|---|---|
| M01 | Abrir formulário de conexão legada (sem `Color` no JSON) | Swatch e faixa exibem `#5B7CFA`; sem crash |
| M02 | Clicar no swatch | Popup abre; bloom inicia; petals aparecem escalonadas (petal 0 imediata, petal 7 após 210ms) |
| M03 | Clicar no swatch novamente (picker aberto) | Picker fecha imediatamente; `EditColor` mantido |
| M04 | Clicar em cada uma das 8 petals | Swatch e faixa de header atualizam imediatamente para a cor da petal; petal selecionada recebe borda branca sólida; demais sem borda |
| M05 | Arrastar o handle do arc slider | Cor atualiza em tempo real; swatch e header stripe refletem a mudança continuamente |
| M06 | Arrastar o handle fora dos limites visuais | Drag continua; valor não ultrapassa extremos; sem travamento |
| M07 | Arrastar o handle pelo gap inferior do arco | Snap para extremidade mais próxima; sem salto visual abrupto |
| M08 | Clicar na área central do picker (dentro do núcleo ou petal) | Arc slider NÃO captura o evento; petal ou núcleo respondem corretamente |
| M09 | Clicar fora do popup | Picker fecha; `EditColor` mantém o valor da última interação |
| M10 | Pressionar Escape com picker aberto | Picker fecha; `EditColor` revertido para o valor antes da abertura |
| M11 | Pressionar Escape sem picker aberto | Nenhuma ação no picker; evento propaga normalmente |
| M12 | Clicar em petal, Escape | Cor revertida para antes da abertura — não para antes do clique na petal |
| M13 | Clicar em petal, clicar fora | Cor da petal é mantida |
| M14 | Clicar em petal, arrastar slider, Escape | Revert para antes da abertura; toda a sessão de edição descartada |
| M15 | Clicar em petal, arrastar slider, clicar no núcleo interno | Cor resultante mantida; picker fecha |
| M16 | Salvar o formulário | `ConnectionProfile.Color = EditColor`; persiste no JSON; reabertura exibe a cor salva |
| M17 | Lista com múltiplas conexões de cores diferentes | Bullets exibem cores distintas corretamente |
| M18 | Conexão ativa na lista | Borda lateral 3px na cor do profile; bullet na cor do profile; texto de status "conectado" em `StatusOk` (verde) — independente da cor do profile |
| M19 | Redimensionar janela com picker aberto | Popup reposiciona; picker permanece aberto |
| M20 | Abrir e fechar picker 10+ vezes em sequência | Bloom reinicia corretamente em cada abertura; sem acúmulo de estado |
| M21 | Clicar em "+ Nova conexão" com picker aberto | Picker fecha; `EditColor = "#5B7CFA"` |
| M22 | Trocar de profile na lista com picker aberto | Picker fecha; formulário carrega a cor do novo profile |
| M23 | Alterar cor, trocar de profile sem salvar, voltar ao primeiro | Cor do primeiro profile inalterada — alteração não persistida |
| M24 | Ativar uma conexão diferente da que está sendo editada | Borda lateral aparece no item ativo; swatch/faixa permanecem no profile em edição |

---

## 15. Fora do escopo

As seguintes funcionalidades estão explicitamente fora do escopo desta implementação. Não devem ser adicionadas, antecipadas via abstração ou parcialmente implementadas:

- Canal alpha/transparência (`#RRGGBBAA` não suportado)
- Input manual de hex pelo usuário no controle
- Mais de 8 petals fixas
- Paleta de cores configurável pelo usuário
- Suporte a tema claro (`Light` theme)
- Navegação por teclado dentro do arc slider
- Persistência da última cor usada além do `ConnectionProfile`
- Exportação ou importação de paletas
- Aplicação da cor de conexão em outras telas além do `ConnectionManagerControl`
- Animação de fechamento do picker
- Uso da cor de conexão como indicador de estado funcional em qualquer parte do app
