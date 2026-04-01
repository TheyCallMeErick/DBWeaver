# Plano de Adaptacao de Design Macro a partir da Referencia Visual

Data: 2026-04-01
Escopo: tipografia, hierarquia visual, fundo de layout e semantica de botoes.
Restricao explicita: nao alterar visual interno de cards, nodes e wires.

---

## 1. Leitura da referencia (imagem)

A imagem mostra um produto com linguagem visual de alta densidade informacional, mas com hierarquia clara:

- Barra superior fina e funcional, com tabs e controles utilitarios.
- Sidebar com blocos bem segmentados (contexto, filtros/listas, historico).
- Area central ampla com grid/dot pattern discreto e fundo atmosferico.
- Elementos de acao com semantica de cor (primario, informativo, alerta, neutro).
- Tipografia compacta, com forte contraste entre rotulos secundarios e titulos/estado.

Resultado esperado para o projeto:

- Melhor leitura por camada (topo, navegacao, trabalho, rodape).
- Menos ruido visual em fundos globais.
- Semantica consistente de botoes por situacao.
- Mantendo o DNA atual dos nodes/cards/wires.

---

## 2. Gap atual do projeto (macro)

Com base nos temas e shells existentes:

- Tokens ja existem, mas faltam tokens de camada macro para fundos de app (janela, shell, faixas, paines estruturais).
- Tipografia esta funcional, mas ainda sem escala semantica formal (Display/Title/Body/Meta) para uso consistente.
- Muitas cores estao hardcoded em views, especialmente na shell principal.
- Botoes usam estilos locais e globais misturados; falta um contrato unico de variantes semanticas.

Arquivos-chave atuais:

- src/VisualSqlArchitect.UI/Assets/Themes/DesignTokens.axaml
- src/VisualSqlArchitect.UI/Assets/Themes/AppStyles.axaml
- src/VisualSqlArchitect.UI/Views/Shell/MainWindow.axaml
- src/VisualSqlArchitect.UI/Controls/SidebarLeft/SidebarControl.axaml
- src/VisualSqlArchitect.UI/App.axaml

---

## 3. Diretrizes de design para aplicar (sem mexer em nodes)

### 3.1 Tipografia

Objetivo: tornar a hierarquia textual previsivel e consistente.

Proposta:

- Definir escala semantica:
  - Display: titulos de secao principal
  - Title: titulos de painel
  - Body: texto padrao
  - Meta: labels auxiliares, tooltips, badges
- Consolidar pesos: Regular, Medium, Semibold.
- Manter fonte principal atual, mas aplicar por papel semantico e nao por uso pontual.

Aplicacao:

- Criar tokens em DesignTokens.axaml para tamanhos e pesos semanticos.
- Criar classes utilitarias em AppStyles.axaml (ex.: .txt-title, .txt-meta).
- Trocar valores hardcoded de FontSize nas views macro (MainWindow/Sidebar).

### 3.2 Hierarquia de superficies

Objetivo: separar melhor plano de fundo global, barras estruturais e paines laterais.

Proposta de camadas:

- Layer 0: fundo da janela/app (mais escuro, base atmosferica).
- Layer 1: barras estruturais (titlebar, footer, tab-strip).
- Layer 2: paines de navegacao (sidebar e overlays estruturais).
- Layer 3: areas interativas destacadas (somente quando necessario).

Aplicacao:

- Criar tokens de fundo macro (MacroBg0..MacroBg3) em DesignTokens.axaml.
- Atualizar MainWindow.axaml e SidebarControl.axaml para usar apenas brushes tokenizadas.
- Evitar novos hex inline na shell.

### 3.3 Fundos (somente backgrounds macro)

Objetivo: melhorar atmosfera e profundidade sem tocar em cards/nodes/wires.

Proposta:

- Fundo da janela com gradiente sutil radial/linear de baixa opacidade.
- Titlebar e rodape com contraste leve contra o corpo.
- Sidebar com fundo de painel distinto do canvas, mas sem ruido.
- Grid de canvas permanece (sem alterar estilo dos elementos internos).

Aplicacao:

- AppStyles.axaml: estilo de Window com brush composta.
- MainWindow.axaml: remover backgrounds hardcoded e usar brushes macro.
- Nao alterar NodeControl.axaml nem classes de wire.

### 3.4 Botoes por semantica de situacao

Objetivo: manter botoes com cores coerentes por intencao de acao.

Variantes recomendadas:

- Primary: acao principal da tela.
- Secondary: acao complementar.
- Info: acao de contexto (diagnostico, preview, utilitarios).
- Success: acoes de aplicar/confirmar seguras.
- Warning: acoes que exigem atencao.
- Danger: fechar, remover, reset irreversivel.
- Ghost: acao neutra de toolbar.

Aplicacao:

- AppStyles.axaml: criar estilos por classe (ex.: Button.primary, .success, .warning, .danger, .ghost).
- MainWindow.axaml e SidebarControl.axaml: substituir overrides locais por classes semanticas.
- Preservar comportamento atual de hover/focus, apenas padronizando tokens.

### 3.5 Guia de hierarquia tipografica (proposto)

Tabela base (a partir da sua orientacao):

| Nivel de importancia | Elemento | Estilo de fonte | Peso/contraste |
|---|---|---|---|
| Primario (Acao) | Botao "Add context +" | Sans-Serif | Bold + fundo de alto contraste (azul) |
| Primario (Estrutura) | Titulos dos nos (Planets, etc.) | Sans-Serif | Bold + cor de fundo no cabecalho |
| Secundario (Navegacao) | Tabs e menu lateral | Sans-Serif | Regular + tons de cinza/branco |
| Tecnico (Conteudo) | Campos internos dos nos | Monospaced | Regular (separa esquema do banco da interface) |
| Terciario (Log/Historia) | History of queries | Monospaced | Tamanho menor + syntax highlighting |

Mapeamento direto para o projeto:

- Primario (Acao): botoes de CTA em Sidebar e overlays -> classe `Button.primary`.
- Primario (Estrutura): headers de secao (titlebar, titulos de paines) -> classe de texto `txt-title`.
- Secundario (Navegacao): tabs de shell/sidebar -> classe de texto `txt-nav` + cor `TextSecondary`.
- Tecnico (Conteudo): SQL e campos tecnicos -> `MonoFont` + `FontSizeBody`.
- Terciario (Log/Historia): blocos de historico e log -> `MonoFont` + `FontSizeMeta`.

Tokens recomendados para implementar essa hierarquia:

- `FontSizeTitle = 13`
- `FontSizeBody = 12`
- `FontSizeMeta = 11`
- `FontWeightStrong = SemiBold`
- `FontWeightAction = Bold`

Observacao importante:

- Como voce pediu para nao mexer em cards/nodes/wires neste ciclo, o item "Titulos dos nos" fica como guia de padrao para a proxima etapa visual interna dos nodes.

Sugestoes de fontes:

- Sans principal (UI): `Sora`, `Manrope`, `Plus Jakarta Sans`
- Monospace tecnico: `JetBrains Mono`, `IBM Plex Mono`, `Cascadia Code`
- Fallback robusto (Windows/Avalonia):
  - UI: `Sora,Manrope,Plus Jakarta Sans,Segoe UI,Arial,sans-serif`
  - Mono: `JetBrains Mono,IBM Plex Mono,Cascadia Code,Consolas,monospace`

Recomendacao de combinacao para este projeto:

- Opcao A (mais produto): UI `Sora`, tecnico `JetBrains Mono`
- Opcao B (mais neutra): UI `Manrope`, tecnico `IBM Plex Mono`
- Opcao C (compatibilidade maxima): UI `Plus Jakarta Sans`, tecnico `Cascadia Code`

Syntax highlighting (History of queries):

- Keywords: azul claro
- Identificadores de tabela/campo: cinza claro
- Strings/datas: verde/ciano
- Numeros: amarelo suave
- Comentarios/notas: cinza dessaturado

### 3.6 Feature: tema customizavel via JSON (com mapeamento)

Objetivo:

- Permitir customizacao de tema por arquivo JSON sem editar AXAML.
- Manter um contrato estavel entre o JSON e os tokens internos do Avalonia.

Principio de arquitetura:

- JSON nao estiliza controles diretamente.
- JSON define somente valores de design token (cores, tipografia, espacamento).
- A UI continua consumindo tokens via `StaticResource`.

Fluxo recomendado:

1. Carregar JSON de tema (`ThemeLoader`).
2. Validar schema e ranges (`ThemeValidator`).
3. Mapear chaves JSON para tokens internos (`ThemeTokenMapper`).
4. Aplicar no `Application.Resources` (`ThemeRuntimeApplier`).
5. Se falhar validacao, aplicar fallback para tema padrao.

Exemplo de estrutura JSON (macro):

```json
{
  "meta": {
    "name": "Studio Dark",
    "version": "1.0"
  },
  "colors": {
    "macroBg0": "#0B0E14",
    "macroBg1": "#101523",
    "macroBg2": "#141B2D",
    "textPrimary": "#E8EAED",
    "textSecondary": "#9CA3AF",
    "btnPrimaryBg": "#2563EB",
    "btnPrimaryFg": "#F8FAFC",
    "btnWarningBg": "#7C2D12",
    "btnWarningFg": "#FED7AA"
  },
  "typography": {
    "uiFont": "Sora,Manrope,Segoe UI,Arial,sans-serif",
    "monoFont": "JetBrains Mono,IBM Plex Mono,Cascadia Code,Consolas,monospace",
    "titleSize": 13,
    "bodySize": 12,
    "metaSize": 11
  }
}
```

Mapeamento minimo obrigatorio:

- `colors.macroBg0` -> `MacroBg0`
- `colors.macroBg1` -> `MacroBg1`
- `colors.macroBg2` -> `MacroBg2`
- `colors.textPrimary` -> `TextPrimary`
- `colors.textSecondary` -> `TextSecondary`
- `colors.btnPrimaryBg` -> `BtnPrimaryBg`
- `colors.btnPrimaryFg` -> `BtnPrimaryFg`
- `typography.uiFont` -> `UIFont`
- `typography.monoFont` -> `MonoFont`
- `typography.titleSize` -> `FontSizeTitle`
- `typography.bodySize` -> `FontSizeBody`
- `typography.metaSize` -> `FontSizeMeta`

Regras de seguranca e compatibilidade:

- Chaves desconhecidas: ignorar com warning.
- Chaves obrigatorias ausentes: usar fallback padrao e reportar diagnostico.
- Valores invalidos (hex/tamanho): nao aplicar e manter token default.
- Nunca permitir que JSON sobrescreva estilos de nodes/wires neste ciclo.

---

## 4. Plano tecnico por arquivo

### 4.1 src/VisualSqlArchitect.UI/Assets/Themes/DesignTokens.axaml

Adicionar:

- Tokens tipograficos semanticos (Title, Body, Meta + pesos).
- Tokens macro de fundo (MacroBg0..MacroBg3, MacroBorderSubtle).
- Tokens de botoes semanticos (BtnPrimaryBg/Fg, BtnWarningBg/Fg etc.).

Nao alterar:

- Tokens de node category, pin e wire.

### 4.2 src/VisualSqlArchitect.UI/Assets/Themes/AppStyles.axaml

Adicionar:

- Estilos de texto utilitarios por classe.
- Estilo base de Window com fundo macro (gradiente discreto).
- Biblioteca de botoes semanticos por classe.

Refatorar:

- Reduzir estilos locais duplicados de botoes e mover para aqui.

### 4.3 src/VisualSqlArchitect.UI/Views/Shell/MainWindow.axaml

Alterar:

- Substituir cores hex inline por StaticResource de tokens macro.
- Aplicar classes semanticas de botoes na toolbar.
- Uniformizar font sizes para escala semantica.

Preservar:

- Estrutura funcional da toolbar, tabs e comandos.
- Interacoes e bindings existentes.

### 4.4 src/VisualSqlArchitect.UI/Controls/SidebarLeft/SidebarControl.axaml

Alterar:

- Migrar header/body/footer para tokens macro.
- Aplicar tipografia semantica em tabs e botoes de footer.
- Aplicar variantes semanticas nos botoes de acao.

Preservar:

- Logica de tabs e comportamento atual.

### 4.5 Novo bloco de feature para tema JSON

Adicionar arquivos:

- `src/VisualSqlArchitect.UI/Services/Theming/ThemeConfig.cs`
- `src/VisualSqlArchitect.UI/Services/Theming/ThemeLoader.cs`
- `src/VisualSqlArchitect.UI/Services/Theming/ThemeValidator.cs`
- `src/VisualSqlArchitect.UI/Services/Theming/ThemeTokenMapper.cs`
- `src/VisualSqlArchitect.UI/Services/Theming/ThemeRuntimeApplier.cs`

Integracao sugerida:

- Inicializar carregamento em `App.axaml.cs` no startup.
- Procurar arquivo padrao em `Assets/Themes/user-theme.json`.
- Se existir e for valido, aplicar override de tokens macro.
- Se nao existir/invalido, manter DesignTokens.axaml padrao.

Critérios de aceite da feature JSON:

- Mudanca no JSON reflete em runtime (apos reinicio inicial; hot reload opcional depois).
- Tema invalido nao quebra a aplicacao.
- Tokens de nodes/wires permanecem intactos.
- Logs claros informam status: loaded, partial, fallback.

---

## 5. Ordem recomendada de implementacao

### Fase 1 (base de design system)

- Criar tokens macro + tipografia + botoes semanticos.
- Sem tocar em telas ainda.

### Fase 2 (shell principal)

- Aplicar tokens em MainWindow.axaml.
- Eliminar hardcodes macro de cor na janela principal.

### Fase 3 (sidebar e paines estruturais)

- Aplicar tokens em SidebarControl.axaml e componentes de shell adjacentes.

### Fase 4 (hardening visual)

- Revisar contrastes, focus state e consistencia de botoes.
- Ajustes finos de espacamento e densidade tipografica.

### Fase 5 (theming JSON)

- Implementar pipeline `load -> validate -> map -> apply`.
- Documentar schema JSON em `docs/`.
- Adicionar testes unitarios de validacao e mapeamento.

---

## 6. Critérios de aceite

- Nenhuma mudanca visual interna em cards, nodes e wires.
- Fundos macro atualizados (janela/shell/sidebar), sem regressao funcional.
- Reducao de cores hardcoded nas views macro.
- Botoes com variantes semanticas aplicadas e consistentes.
- Hierarquia tipografica perceptivel em toda a shell.
- Build e testes existentes continuam verdes.

---

## 7. Riscos e mitigacoes

Risco: quebra visual por conflito entre estilos locais e globais.
Mitigacao: migracao gradual por arquivo, removendo override local so apos validar tela.

Risco: contraste insuficiente em alguns estados (hover/focus/disabled).
Mitigacao: validar com checklist de acessibilidade visual minima (contraste e foco visivel).

Risco: regressao de UX por excesso de alteracoes numa unica entrega.
Mitigacao: PRs pequenos por fase, com screenshots antes/depois.

---

## 8. Entrega sugerida em PRs

- PR 1: tokens e estilos semanticos (sem alterar tela)
- PR 2: MainWindow migrada para tokens
- PR 3: Sidebar migrada para tokens
- PR 4: ajuste fino + remocao de hardcodes residuais
- PR 5: suporte a tema JSON + schema + testes
