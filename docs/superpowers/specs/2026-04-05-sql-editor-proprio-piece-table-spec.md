# SQL Editor Próprio (Piece Table) — Especificação e Requisitos

**Data:** 2026-04-05
**Escopo:** substituir integralmente o uso atual de `AvaloniaEdit` por um editor próprio no projeto, com paridade funcional e evolução para recursos avançados (`piece table`, multi-cursor, replace, métricas de linha/coluna, undo/redo transacional e rendering virtualizado).

---

## 1. Análise do estado atual

### 1.1 Biblioteca e pontos de acoplamento identificados

Uso atual de `AvaloniaEdit`:

- Dependência NuGet: `Avalonia.AvaloniaEdit` em `src/er.Uor principal SQL: `src/ls/SqlEditor/SqlEditorControl.axaml` e `SqlEditorControl.axaml.cs`.
- Editor read-only de preview DDL: `src/ls/Shell/OutputPreviewModalControl.axaml` e `.axaml.cs`.
- Highlighting XSHD: `src/es/Editors/SqlEditorHighlightingService.cs` + `Assets/Syntax/Sql.xshd`.
- Completion acoplada ao `AvaloniaEdit.CodeCompletion`: `src/es/SqlEditor/Completion/SqlEditorCompletionData.cs`.

### 1.2 Comportamentos atuais que precisam de paridade

No editor SQL principal, hoje existem:

- Edição textual básica com caret único.
- Números de linha (`ShowLineNumbers=True`).
- Seleção simples (`SelectionStart`, `SelectionLength`) usada na execução.
- Atalhos críticos: `F5`, `F8`, `Ctrl+Enter`, `Ctrl+Space`, `Ctrl+S`, `Ctrl+O`, `Ctrl+T`, `Ctrl+W`, `Ctrl+1..9`, `Esc`.
- Auto-complete manual/automático com `.` e contexto após espaço.
- Sincronização de texto com `SqlEditorViewModel.ActiveTab.SqlText`.

No preview DDL:

- Modo read-only.
- Highlight SQL.
- Números de linha.
- Posicionamento do caret no início após atualização.

### 1.3 Lacunas atuais (oportunidade da substituição)

- Sem multi-cursor.
- Sem busca/substituição robusta no próprio controle.
- Modelo de edição dependente da biblioteca externa.
- Sem arquitetura de engine separada para evoluções futuras (ex.: operações em larga escala, perf tuning fino, extensibilidade de seleção).

---

## 2. Objetivos da substituição

1. Remover dependência de editor externo (`AvaloniaEdit`) do fluxo de edição SQL.
2. Implementar engine textual própria baseada em `Piece Table`.
3. Garantir substituição adequada ao comportamento atual (sem regressões funcionais).
4. Entregar recursos avançados nativos: multi-cursor, replace completo, mapeamento linha/coluna eficiente, undo/redo transacional, rendering virtualizado.
5. Manter integração com `SqlEditorViewModel`, `SqlCompletionProvider`, execução de SQL e tema visual atual.

---

## 3. Requisitos funcionais

### RF-01. Edição textual completa

- Inserção, deleção, substituição e colagem para arquivos grandes.
- Operações por teclado e mouse.
- Preservar comportamento atual de edição por aba.

### RF-02. Estrutura de dados `Piece Table`

- Documento deve usar `Piece Table` com:
- `originalBuffer` (read-only, texto inicial).
- `addBuffer` (append-only, inserções).
- Lista/árvore de peças (`Piece`) com referência a buffer, offset e length.
- Edições devem manipular peças sem cópias integrais do texto.

### RF-03. Índice de linhas e colunas

- Cálculo de `offset -> (linha,coluna)` e `(linha,coluna) -> offset` em tempo sublinear.
- Expor API para status bar, execução de seleção e navegação.
- Manter número de linhas visível no gutter.

### RF-04. Multi-cursor e multi-seleção

- Suportar múltiplos carets simultâneos.
- Inserção/deleção sincronizada em todos os carets.
- Conversão de seleção para múltiplos carets (ex.: selecionar próxima ocorrência).
- Definir caret primário para operações de contexto.

### RF-05. Busca e substituição (replace)

- Busca incremental com opções:
- case sensitive
- whole word
- regex
- escopo seleção/documento
- Substituir atual e substituir todos.
- Operação `Replace All` deve ser transacional (um passo de undo).

### RF-06. Undo/redo transacional

- Pilhas próprias de undo/redo por aba.
- Agrupamento por transação para operações compostas (auto-complete, replace all, colagem multi-cursor).
- Limite configurável de histórico com política de descarte.

### RF-07. Auto-complete integrado

- Reutilizar `SqlCompletionProvider` existente.
- Disparo manual (`Ctrl+Space`) e automático (`.` e contexto após palavra-chave SQL).
- Inserção da sugestão deve respeitar prefixo e multi-cursor (quando aplicável).

### RF-08. Highlight SQL próprio

- Implementar tokenização SQL própria para coloração (sem `AvaloniaEdit.Highlighting`).
- Suportar no mínimo: keywords, string literal, número, comentário, identificador, operador.
- Aplicar tema já existente via recursos/tokens do app.

### RF-09. Scroll e rendering virtualizado

- Renderizar somente linhas visíveis + margem de pré-render.
- Suportar documentos grandes com suavidade de scroll.
- Manter seleção/caret consistente durante virtualização.

### RF-10. Compatibilidade de atalhos existentes

- Preservar atalhos já usados no `SqlEditorControl` atual.
- Não quebrar fluxo de execução (`ExecuteAllAsync`, `ExecuteSelectionOrCurrentAsync`).

### RF-11. Modo read-only para preview DDL

- Mesmo componente base em modo somente leitura.
- Sem edição, com seleção/cópia, numeração de linhas e highlight.

### RF-12. API de integração UI/ViewModel

- O novo controle deve expor contrato claro:
- `Text`
- `PrimaryCaretOffset`
- `PrimarySelectionRange`
- `Selections`
- eventos de mudança de texto e de seleção
- Manter sincronização com `SqlEditorViewModel.ActiveTab.SqlText`.

---

## 4. Requisitos não funcionais

### RNF-01. Performance

- Inserção/deleção local em tamanho constante amortizado (dependente do ajuste de peças).
- Navegação linha/coluna com índice incremental.
- `Replace All` em 100k+ caracteres sem travamento perceptível da UI.

### RNF-02. Escalabilidade

- Suportar scripts SQL extensos (meta inicial: 5 MB por aba com usabilidade aceitável).

### RNF-03. Confiabilidade

- Sem perda de conteúdo em sequência de undo/redo.
- Integridade do documento validada por testes de invariantes do `Piece Table`.

### RNF-04. Testabilidade

- Engine desacoplada da UI para testes unitários puros.
- Cobertura dedicada para parser de linhas, multi-cursor e replace.

### RNF-05. Manutenibilidade

- Separação explícita entre:
- `EditorCore` (engine)
- `EditorRendering` (layout/render)
- `EditorInput` (teclado/mouse/comandos)

### RNF-06. Compatibilidade visual

- Respeitar tokens do design system já existente.
- Preservar aparência e comportamento esperados do SQL editor no shell.

---

## 5. Arquitetura proposta

### 5.1 Módulos

**A) `EditorCore`** (novo namespace sugerido `Core`)

- `PieceTableDocument`
- `Piece`
- `TextSnapshot`
- `LineIndex`
- `SelectionModel`
- `Cursor`
- `EditTransaction`
- `UndoRedoManager`
- `SearchReplaceEngine`
- `SqlTokenizer`

**B) `EditorUI`** (novo controle Avalonia próprio)

- `SqlCodeEditorControl.axaml` + `.axaml.cs`
- `SqlCodeEditorPresenter` (layout/rendering)
- `SqlEditorInputController`
- `SqlEditorCompletionPopup` (popup próprio)
- `SqlEditorGutterControl` (linhas)

**C) `Integration`**

- Adaptador para `SqlEditorViewModel`.
- Adaptador para `SqlCompletionProvider`.
- Adaptador para execução (`SelectionStart/Length` equivalente ao range primário).

### 5.2 Modelo de dados principal (Piece Table)

Estruturas mínimas:

- `enum BufferKind { Original, Add }`
- `record Piece(BufferKind Buffer, int Start, int Length)`
- `sealed class PieceTableDocument`

Operações obrigatórias:

- `Insert(offset, text)`
- `Delete(offset, length)`
- `Replace(offset, length, text)`
- `GetText(range)`
- `GetSnapshot()`

Invariantes:

- Nenhuma peça com `Length <= 0`.
- Peças adjacentes do mesmo buffer e continuidade devem ser normalizadas quando possível.
- Offsets sempre válidos em relação ao tamanho lógico do documento.

### 5.3 Linha/coluna

- Implementar índice incremental de quebras de linha.
- Atualizar índice por delta de edição (não recalcular tudo).
- API mínima:
- `GetLineCount()`
- `GetLineStartOffset(line)`
- `GetLineEndOffset(line)`
- `GetLineColumnFromOffset(offset)`
- `GetOffsetFromLineColumn(line, column)`

### 5.4 Seleções e cursores

- `SelectionRange { StartOffset, EndOffset, IsReversed }`
- `SelectionSet` com caret primário + secundários.
- Operações aplicadas em ordem segura (offset decrescente para evitar invalidar ranges subsequentes).

### 5.5 Busca e replace

- Engine dedicada com varredura sobre snapshot.
- `FindNext/FindPrevious/FindAll`.
- `ReplaceCurrent` e `ReplaceAll`.
- `ReplaceAll` deve gerar relatório (quantidade de substituições, duração, possíveis conflitos).

### 5.6 Rendering

- Pipeline:
- calcular viewport em linhas
- obter spans visíveis
- tokenizar apenas região necessária
- desenhar texto + seleção + carets + gutter

### 5.7 Completion

- Popup independente com lista navegável por teclado.
- Fonte de dados: `SqlCompletionProvider.GetSuggestions(...)`.
- Inserção via comando de edição transacional.

---

## 6. Plano de migração e substituição

### Fase 1 — Foundation

- Criar `EditorCore` com `PieceTableDocument`, `LineIndex`, `UndoRedoManager`.
- Testes de invariantes e operações básicas.

### Fase 2 — Controle visual mínimo

- Novo `SqlCodeEditorControl` com edição básica, caret único, seleção única, line numbers.
- Integrar ao `SqlEditorControl` em modo experimental (feature flag interna).

### Fase 3 — Paridade funcional do editor atual

- Atalhos atuais.
- Completion integrada.
- Execução por seleção/caret.
- File open/save já existente permanece no code-behind.

### Fase 4 — Recursos avançados

- Multi-cursor.
- Search/replace completo.
- Melhorias de virtualização.

### Fase 5 — Substituição total

- Trocar uso de `AvaloniaEdit` também no `OutputPreviewModalControl` por versão read-only do novo controle.
- Remover `Avalonia.AvaloniaEdit` do `csproj`.
- Atualizar testes de regressão textual que hoje validam presença de `ae:TextEditor`.

---

## 7. Critérios de aceite

1. Sem dependência de `Avalonia.AvaloniaEdit` no projeto UI.
2. Editor SQL principal funcional com paridade dos atalhos atuais.
3. Multi-cursor funcional para inserção e deleção.
4. Replace atual e replace all funcionais com undo transacional.
5. Exibição correta de linha/coluna e line numbers.
6. Preview DDL em read-only com highlight e line numbers no novo componente.
7. Testes unitários e de regressão atualizados e verdes.

---

## 8. Estratégia de testes

### 8.1 Unitários (EditorCore)

- Invariantes do `Piece Table`.
- Casos de insert/delete/replace com offsets limítrofes.
- Undo/redo simples e transacional.
- Mapeamento linha/coluna após múltiplas edições.
- Multi-cursor com ranges sobrepostos.
- Find/replace com regex e texto literal.

### 8.2 Unitários (UI/integração)

- Atalhos do editor e roteamento de comandos.
- Sincronização `Text <-> ActiveTab.SqlText`.
- Completion popup e inserção.
- Renderização de line numbers visíveis.

### 8.3 Regressão

- Substituir testes que afirmam `ae:TextEditor` por asserts do novo controle.
- Preservar cenários existentes de execução (`F5`, `F8`, `Ctrl+Enter`).

---

## 9. Riscos e mitigação

- **Risco:** regressão de UX em edição longa.
  **Mitigação:** virtualização + benchmark smoke tests do editor.

- **Risco:** bugs em offset após multi-cursor.
  **Mitigação:** algoritmo determinístico por ordenação de ranges + testes randômicos.

- **Risco:** quebra de integração com execução SQL.
  **Mitigação:** contrato explícito de seleção primária e testes de integração com `SqlSelectionExtractor`.

- **Risco:** degradação de highlight em arquivos grandes.
  **Mitigação:** tokenização incremental por viewport.

---

## 10. Mudanças de código previstas (alto nível)

- Criar pasta nova: `src/ls/SqlCodeEditor/` (controle e subcomponentes).
- Criar pasta nova: `src/Core/` (engine).
- Refatorar:
- `Controls/SqlEditor/SqlEditorControl.axaml` e `.axaml.cs`
- `Controls/Shell/OutputPreviewModalControl.axaml` e `.axaml.cs`
- `Services/Editors/SqlEditorHighlightingService.cs` (substituir por tokenizer próprio)
- `Services/SqlEditor/Completion/SqlEditorCompletionData.cs` (desacoplar de `AvaloniaEdit`)
- Atualizar testes:
- `tests/.../SqlEditorControlTemplateRegressionTests.cs`
- `tests/.../OutputPreviewModalControlTemplateRegressionTests.cs`
- Adicionar nova suíte para `EditorCore`.
- Remover pacote `Avalonia.AvaloniaEdit` do `` ao final da Fase 5.

---

## 11. Definição de pronto (DoD)

- Paridade com fluxo atual do SQL editor validada.
- Recursos pedidos (piece table, linha/coluna, replace, multi-cursor) entregues e cobertos por testes.
- Sem regressões críticas no shell/editor preview.
- Dependência externa de editor removida.
- Documentação técnica de arquitetura do novo editor publicada junto ao código.
