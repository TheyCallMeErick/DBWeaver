# Lista de Tarefas de Correção e Melhorias - Visual SQL Architect

Este documento detalha as tarefas necessárias para corrigir bugs, melhorar a usabilidade e adicionar novas funcionalidades à aplicação Visual SQL Architect. Cada tarefa é projetada para ser executada de forma independente.

---

## 🚨 INSTRUÇÕES CRÍTICAS PARA CLAUDE

### ⚠️ PROBLEMAS CRÍTICOS COM MÁXIMA PRIORIDADE

#### **(1) ✅ CONCLUÍDA: Wire Sync ao Mover Nós + Wires não renderizados no início (Tarefa 1)**
- **Status**: ✅ CONCLUÍDA
- **Impacto**: BLOQUEADOR - Fios não acompanham visualmente ao arrastar nós / Fios não renderizados no carregamento inicial
- **Arquivos Críticos**:
  - `src/VisualSqlArchitect.UI/Controls/InfiniteCanvas.cs` (linhas ~210-282)
  - `src/VisualSqlArchitect.UI/Controls/BezierWireLayer.cs` (renderização)
  - `src/VisualSqlArchitect.UI/Controls/NodeControl.axaml.cs`

**O que fazer:**
1. Verificar logs adicionados em `UpdatePinPositions()`, `SyncWires()`, `BezierWireLayer.Render()`
2. Executar: `dotnet run` em `/src/VisualSqlArchitect.UI`
3. Criar fluxo com 2 nós conectados, mover coluna e observar logs de debug
4. **Hipóteses principais a investigar:**
   - TranslatePoint() retornando null → Fallback geométrico permanece ativo
   - BezierWireLayer.InvalidateVisual() não está sendo chamado
   - Transformação de coordenadas incorreta (problema de espaço de coordenadas canvas/node)
5. Ao encontrar root cause, implementar correção e validar com testes

**Atalhos úteis:**
- Debug Output: `View → Output → C# (Debug)` no VS Code
- Procurar por: `"🔄 UpdatePinPositions"`, `"🔗 SyncWires"`, `"🎨 BezierWireLayer.Render"`

---

#### **(2) 🔴 ERRO DE COMPILAÇÃO: MaterialIconKind.HistoryVariant não existe**
- **Status**: 🔴 BLOQUEADOR - Build falha imediatamente
- **Impacto**: CRÍTICO - Impossível compilar a aplicação
- **Problema Exato**:
  - Arquivo: `src/VisualSqlArchitect.UI/Services/CommandPaletteFactory.cs` (linha 42)
  - Erro: `MaterialIconKind` não contém uma definição para `HistoryVariant`
  - Causa: Ícone referenciado não existe na biblioteca Material Icon Variant

**O que fazer:**
1. Abrir arquivo: `src/VisualSqlArchitect.UI/Services/CommandPaletteFactory.cs`
2. Localizar linha ~42 com referência a `MaterialIconKind.HistoryVariant`
3. **Opções de correção:**
   - **Opção A (Recomendada):** Substituir por ícone equivalente válido:
     ```csharp
     // Trocar:   MaterialIconKind.HistoryVariant
     // Por uma das opções válidas (escolher a mais apropriada):
     MaterialIconKind.History          // Simple history icon
     MaterialIconKind.UndoVariant       // Undo variant
     MaterialIconKind.ClockOutline      // Clock outline
     MaterialIconKind.RestoreVariant    // Restore variant
     ```
   - **Opção B:** Consultar documentação de ícones disponíveis
4. Após correção, validar compile:
   ```bash
   dotnet build --no-restore 2>&1 | grep -i "error"  # Deve estar vazio
   ```

**Verificação rápida:**
```bash
# Mostrar exato erro de compilação
cd /home/erickazevedo/Downloads/files
dotnet build --no-restore 2>&1 | grep "HistoryVariant"
```

---

### **AVISO: Existem também Warnings (não bloqueadores, mas devem ser tratados)**

Warnings detectados durante build:

| Arquivo | Linha | Problema | Severidade |
|---------|-------|----------|-----------|
| `DeleteConnectionCommand.cs` | 18 | Desreferência de possível nulo (CS8602) | ⚠️ Médio |
| `AutoFixNamingCommand.cs` | 32, 38 | Conversão nula em tipo não anulável (CS8600) | ⚠️ Médio |
| `GraphValidator.cs` | 172 | Conversão nula em tipo não anulável (CS8600) | ⚠️ Médio |
| `SnippetViewModel.cs` | 10 | Captura de variável (CS9124) | ⚠️ Baixo |

**Próximas ações (após Fix do erro de compilação):**
1. Corrigir warnings com `null-coalescing` (`?.`, `??`, `!`)
2. Passar build sem nenhum erro
3. Depois prosseguir com bugs funcionais

---

### 📋 FLUXO RECOMENDADO PARA CLAUDE

#### **Fase 1: Critique and Fix (30 minutos)**
1. ✅ Verificar Bug #1 (Wire Sync) - usar logs existentes
2. ✅ Investigar Dependência Circular - compile check
3. ✅ Se encontrar issue, fazer correção direto

#### **Fase 2: Validate (15 minutos)**
1. ✅ Executar: `dotnet build` → deve resultar em 0 errors
2. ✅ Executar: `dotnet test` → testes devem passar
3. ✅ Executar: `dotnet run` → app deve iniciar sem exception

#### **Fase 3: Document (5 minutos)**
1. ✅ Adicionar descobertas de Bug #1/Dependência Circular em `EIXO_8_STATUS.md`
2. ✅ Se correção foi feita:
   - Descrever root cause
   - Solução implementada
   - Arquivos modificados
   - Status: ✅ RESOLVIDO

---

### 🎯 OUTRAS TAREFAS P0 (Após resolver críticos acima)

| # | Tarefa | Motivo P0 | Esforço |
|---|--------|-----------|---------|
| **11** | Sistema de Validação Visual do Grafo | Previne SQL inválido | Médio |
| **29** | Guardrails de Segurança para Execução | Evita deletes acidentais | Baixo |
| **31** | Modo Seguro com Sandbox Preview | Proteção em prod | Médio |
| **36** | Diagnóstico Automático com Sugestões | UX essencial | Médio |
| **59** | Nó de Saída de Resultado | Feature core visual | Alto |

**Próx passos após P0:** Implementar P1 por ordem de dependência (vide seção "Critério de Desempate" ao final)

---

---

### ~~Tarefa 1: Corrigir o Posicionamento das Conexões (Fios) nos Nós~~ ✅ CONCLUÍDA

**Descrição do Problema:**
As linhas (conexões) que ligam os nós no canvas não estão perfeitamente alinhadas com os pontos de conexão (pins) dos nós. Quando um nó é arrastado, a linha se desconecta visualmente ou fica flutuando, em vez de permanecer ancorada no centro do pino de conexão.

**Comportamento Esperado:**
As linhas de conexão devem sempre se originar e terminar exatamente no centro dos pinos de entrada/saída dos nós, independentemente da posição do nó no canvas. A conexão deve ser suave e permanecer visualmente "colada" ao pino durante o arraste do nó.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/Controls/BezierWireLayer.cs`
- `src/VisualSqlArchitect.UI/Controls/NodeControl.axaml.cs`
- `src/VisualSqlArchitect.UI/Controls/PinDragInteraction.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`

**Guia de Implementação:**
1.  **Revisar a Lógica de Renderização:** Investigue o `BezierWireLayer.cs` para entender como as curvas de Bézier (as linhas) são calculadas e desenhadas. A lógica provavelmente usa as coordenadas dos pinos para definir os pontos de início e fim da curva.
2.  **Garantir Coordenadas Corretas:** Verifique se as coordenadas dos pinos estão sendo atualizadas e propagadas corretamente para a `BezierWireLayer` quando um nó é movido. O problema pode estar na forma como as coordenadas do nó são transformadas para as coordenadas do canvas.
3.  **Observar Atualizações de Layout:** Use os eventos de ciclo de vida do Avalonia (como `LayoutUpdated`) para garantir que as linhas sejam redesenhadas sempre que a posição de um nó mudar.

---

### ~~Tarefa 2: Exibir a Marca d'Água "Visual SQL Architect" Apenas no Canvas Vazio~~ ✅ CONCLUÍDA

**Descrição do Problema:**
O texto "Visual SQL Architect" aparece como uma marca d'água no canvas e permanece visível mesmo quando há nós e conteúdo no canvas, sobrepondo-se a outros elementos da UI.

**Comportamento Esperado:**
O texto "Visual SQL Architect" deve funcionar como um placeholder, sendo visível apenas quando o canvas estiver completamente vazio (sem nós). Assim que o primeiro nó for adicionado, o texto deve desaparecer. Se todos os nós forem removidos, o texto deve reaparecer.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/MainWindow.axaml`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`

**Guia de Implementação:**
1.  **Adicionar Propriedade de Visibilidade:** No `CanvasViewModel.cs`, adicione uma propriedade booleana, por exemplo, `IsCanvasEmpty`.
2.  **Controlar a Visibilidade:** Monitore a coleção de nós no `CanvasViewModel`. A propriedade `IsCanvasEmpty` deve ser `true` se a contagem de nós for zero e `false` caso contrário. Use `ObservableAsPropertyHelper` do ReactiveUI para vincular essa propriedade à contagem da coleção de nós.
3.  **Binding no XAML:** No `MainWindow.axaml`, encontre o `TextBlock` ou controle que exibe o texto "Visual SQL Architect". Faça um binding da sua propriedade `IsVisible` para a nova propriedade `IsCanvasEmpty` no `CanvasViewModel`.

---

### ~~Tarefa 3: Tela Inicial para Gerenciamento de Conexões de Banco de Dados~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Atualmente, não há uma interface para criar, selecionar ou gerenciar conexões de banco de dados. O usuário não consegue alternar facilmente entre diferentes fontes de dados.

**Comportamento Esperado:**
- **Tela de Boas-Vindas:** Ao iniciar, a aplicação deve apresentar uma tela inicial que permita ao usuário:
    - Ver uma lista de conexões salvas.
    - Selecionar uma conexão existente para abrir no canvas.
    - Adicionar uma nova conexão (especificando tipo de banco, host, usuário, senha, etc.).
    - Editar ou remover conexões existentes.
- **Troca Fácil:** Deve haver um menu ou dropdown facilmente acessível na tela principal (acima do canvas, talvez na barra de ferramentas) que mostre a conexão ativa e permita trocar rapidamente para outra conexão salva.

**Arquivos Relevantes (Sugestão de Novos Arquivos):**
- `src/VisualSqlArchitect.UI/Views/ConnectionManagerView.axaml` (Nova View)
- `src/VisualSqlArchitect.UI/ViewModels/ConnectionManagerViewModel.cs` (Novo ViewModel)
- `src/VisualSqlArchitect.UI/MainWindow.axaml` (para integrar a troca de conexão)

**Guia de Implementação:**
1.  **Criar a View de Gerenciamento:** Desenvolva a `ConnectionManagerView.axaml` com campos de formulário para os detalhes da conexão (tipo de banco, servidor, porta, nome de usuário, senha, etc.) e uma lista para exibir as conexões salvas.
2.  **Implementar o ViewModel:** O `ConnectionManagerViewModel.cs` deve gerenciar a lógica de salvar, carregar, editar e remover conexões. As conexões podem ser salvas localmente em um arquivo de configuração (JSON ou similar).
3.  **Integrar na Janela Principal:** Modifique a `MainWindow` para exibir a `ConnectionManagerView` na inicialização se nenhuma conexão estiver ativa. Adicione um `ComboBox` ou similar na UI principal, vinculado a uma lista de conexões disponíveis no `MainViewModel`, para permitir a troca rápida.

---

### ~~Tarefa 4: Corrigir Ícones Quebrados e Melhorar Contraste do Rodapé~~ ✅ CONCLUÍDA

**Descrição do Problema:**
- Vários ícones na aplicação não estão sendo exibidos corretamente, mostrando um placeholder de imagem quebrada.
- O texto no rodapé (possivelmente uma barra de status) está com uma cor muito escura, tornando-o ilegível contra o fundo.

**Comportamento Esperado:**
- Todos os ícones devem ser exibidos corretamente.
- O texto do rodapé deve ter um contraste adequado para ser facilmente legível.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/Assets/` (verificar se os ícones estão presentes)
- `src/VisualSqlArchitect.UI/Assets/Themes/AppStyles.axaml`
- `src/VisualSqlArchitect.UI/Assets/Themes/DesignTokens.axaml`
- Arquivos `.axaml` que utilizam os ícones (ex: `LiveSqlBar.axaml`).

**Guia de Implementação:**
1.  **Verificar Caminhos dos Ícones:** Investigue os arquivos `.axaml` onde os ícones são usados. Verifique se os caminhos para os recursos (assets) estão corretos. Pode ser um problema com o `Build Action` dos arquivos de ícone no projeto `.csproj` (deve ser `AvaloniaResource`).
2.  **Ajustar Cores do Tema:** Abra os arquivos de tema (`AppStyles.axaml` e `DesignTokens.axaml`). Localize os estilos aplicados ao rodapé ou à barra de status. Altere a cor do texto (`Foreground`) para uma cor mais clara (por exemplo, `White` ou um cinza claro) que forneça bom contraste com a cor de fundo.

---

### ~~Tarefa 5: Fechar Diálogos com a Tecla 'Esc'~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Diálogos, pop-ups ou menus de pesquisa abertos não podem ser fechados pressionando a tecla 'Esc', forçando o usuário a clicar fora ou em um botão de fechar.

**Comportamento Esperado:**
Qualquer diálogo, menu flutuante (como o menu de pesquisa) ou pop-up deve ser fechado quando o usuário pressionar a tecla 'Esc'.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/App.axaml.cs` (para uma solução global)
- `src/VisualSqlArchitect.UI/Controls/SearchMenuControl.axaml.cs` (para uma solução específica)
- `src/VisualSqlArchitect.UI/MainWindow.axaml.cs`

**Guia de Implementação:**
1.  **Manipulador de Eventos de Teclado:** Adicione um manipulador de eventos para `KeyDown` no nível da janela principal (`MainWindow`) ou em um controle pai.
2.  **Verificar a Tecla:** Dentro do manipulador, verifique se a tecla pressionada é `Key.Escape`.
3.  **Fechar o Diálogo:** Se a tecla for 'Esc', execute a lógica para fechar o diálogo ativo. Isso pode envolver a alteração de uma propriedade `IsOpen` em um ViewModel (se o diálogo estiver vinculado a uma) ou chamar um método `Close()` diretamente no controle do diálogo. Para menus como o `SearchMenuControl`, você pode simplesmente definir sua visibilidade como `false`.

---

### ~~Tarefa 6: Implementar o Nó Atômico "Alias"~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Não existe uma maneira de renomear colunas ou tabelas (criar um alias) de forma visual como um nó no canvas.

**Comportamento Esperado:**
Criar um novo tipo de nó chamado "Alias". Este nó deve:
- Ter um pino de entrada que aceite uma fonte de dados (tabela ou saída de outro nó).
- Ter um campo de texto onde o usuário possa digitar o novo nome (alias).
- Ter um pino de saída que represente a fonte de dados com o alias aplicado.
- O compilador de SQL deve gerar a cláusula `AS 'alias'` correspondente.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/Nodes/` (criar um novo arquivo para o nó de alias)
- `src/VisualSqlArchitect/Nodes/NodeDefinition.cs` (para registrar o novo nó)
- `src/VisualSqlArchitect/Nodes/NodeGraphCompiler.cs` (para adicionar a lógica de compilação)
- `src/VisualSqlArchitect/Registry/SqlFunctionRegistry.cs` (se aplicável)

**Guia de Implementação:**
1.  **Criar a Classe do Nó:** Crie uma nova classe `AliasNode.cs` em `src/VisualSqlArchitect/Nodes/` que herde de uma classe base de nó existente ou implemente `ISqlExpression`.
2.  **Definir Entradas e Saídas:** Defina os pinos de entrada e saída. A entrada aceitará uma expressão SQL e a saída produzirá a mesma expressão com um alias.
3.  **Adicionar Propriedade de Alias:** Adicione uma propriedade `string AliasName` ao nó.
4.  **Atualizar o Compilador:** Modifique o `NodeGraphCompiler.cs`. Ao encontrar um `AliasNode`, ele deve pegar a expressão SQL do nó de entrada e anexar `AS [AliasName]` a ela.
5.  **Registrar o Nó:** Adicione o `AliasNode` ao registro de nós (`NodeDefinition` ou similar) para que ele apareça no menu de pesquisa para ser adicionado ao canvas.

---

### ~~Tarefa 7: Garantir o Funcionamento de Todos os Botões~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Alguns botões na interface do usuário não estão funcionando como esperado. Eles podem não ter a lógica de clique implementada ou podem estar com o `Command` binding quebrado.

**Comportamento Esperado:**
Todos os botões visíveis na UI devem executar uma ação quando clicados.

**Arquivos Relevantes:**
- Todos os arquivos `.axaml` e seus respectivos `.axaml.cs` em `src/VisualSqlArchitect.UI/Controls/` e `src/VisualSqlArchitect.UI/`.
- ViewModels correspondentes em `src/VisualSqlArchitect.UI/ViewModels/`.

**Guia de Implementação:**
1.  **Auditoria de Botões:** Percorra a aplicação e identifique todos os botões que não funcionam.
2.  **Verificar Bindings:** Para cada botão quebrado, verifique o arquivo `.axaml` para garantir que a propriedade `Command` esteja corretamente vinculada a um `ICommand` no ViewModel correspondente.
3.  **Implementar Comandos:** Se o `ICommand` não existir no ViewModel, crie-o usando `ReactiveCommand.Create(...)` do ReactiveUI.
4.  **Implementar Lógica:** Adicione a lógica de negócio que deve ser executada quando o comando é invocado.
5.  **Verificar `IsEnabled`:** Garanta que a lógica que habilita/desabilita o botão (`CanExecute`) esteja correta. Um botão pode parecer "quebrado" se estiver permanentemente desabilitado.

---

### ~~Tarefa 8: Implementar Redimensionamento de Sidebars e Bottom Bar com Divisórias Arrastáveis~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Os painéis laterais (sidebars) e a barra inferior (bottom bar) têm tamanho fixo. O usuário não consegue ajustá-los conforme suas necessidades (aumentar ou diminuir o espaço disponível para o canvas ou para os painéis laterais).

**Comportamento Esperado:**
- As divisórias entre os painéis devem ser visualmente indicadas como arrastáveis (cursor muda para indicar redimensionamento).
- Ao passar o mouse sobre a divisória (splitter), ela deve ficar **azul** (`#3B82F6` ou similar).
- O usuário deve poder **arrastar** a divisória para **aumentar ou diminuir** o tamanho dos painéis adjacentes.
- Deve haver um **limite máximo** para o tamanho de cada painel (ex: sidebar não pode ocupar mais de 50% da largura total).
- Limite mínimo: cada painel deve manter um tamanho mínimo para ser funcional (ex: sidebar mínima de 200px, bottom bar mínima de 150px).
- As dimensões devem ser **preservadas** entre sessões (salvas em arquivo de configuração ou preferências do usuário).

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/MainWindow.axaml`
- `src/VisualSqlArchitect.UI/MainWindow.axaml.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs` (para armazenar as dimensões)

**Guia de Implementação:**
1.  **Estrutura de Grid:** O layout principal deve usar um `Grid` com `ColumnDefinitions` e `RowDefinitions` para organizar os painéis. Use `GridSplitter` do Avalonia para as linhas de divisória entre painéis.
2.  **Configurar GridSplitter:**
    - Para divisória vertical (entre sidebar e canvas): use `GridSplitter` com `Width="8"` e `VerticalAlignment="Stretch"`.
    - Para divisória horizontal (entre canvas e bottom bar): use `GridSplitter` com `Height="8"` e `HorizontalAlignment="Stretch"`.
    - Defina `ResizeDirection` apropriadamente (`Horizontal` ou `Vertical`).
3.  **Estilo Visual da Divisória:**
    - **Cor padrão**: use uma cor discreta (ex: `#1E2335`).
    - **Cor ao passar o mouse**: mude para azul (`#3B82F6`) usando style com `:pointerover`.
    - **Cursor**: defina como `SizeWE` (East-West) para vertical e `SizeNS` (North-South) para horizontal.
4.  **Limites de Redimensionamento:**
    - Use as propriedades `MinWidth`, `MaxWidth`, `MinHeight`, `MaxHeight` nas colunas e linhas do Grid.
    - Exemplo: `<ColumnDefinition Width="*" MinWidth="200" MaxWidth="600"/>`
5.  **Persistência:**
    - Após redimensionar, salve as dimensões atuais em `AppSettings.json` ou em `Preferences.xml`.
    - Ao iniciar a aplicação, carregue as últimas dimensões salvas.

---

### ~~Tarefa 9: Adicionar Tabelas ao Menu "Adicionar Node"~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Atualmente, o menu de busca (SearchMenuControl) para adicionar nós apenas mostra transformações, funções e operadores SQL. Não há uma forma visual de adicionar uma tabela do banco de dados como ponto de partida do fluxo de dados no canvas.

**Comportamento Esperado:**
- Ao clicar em "Adicionar Node" (Shift+A), o menu de pesquisa deve exibir, além dos nós existentes, um item separado com todas as tabelas disponíveis na conexão ativa.
- As tabelas devem aparecer com um ícone diferente (ex: ícone de tabela) para distingui-las dos nós de transformação.
- Ao selecionar uma tabela, um nó "Tabela" deve ser criado no canvas com:
  - **Nome da tabela** exibido como título do nó.
  - **Pinos de saída** para cada coluna da tabela (ou um único pino que represente toda a tabela, dependendo do design).
  - **Metadados** da tabela renderizados (nome, número de colunas, tipos de dados).

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/Controls/SearchMenuControl.axaml`
- `src/VisualSqlArchitect.UI/Controls/SearchMenuControl.axaml.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/ViewModels/SearchMenuViewModel.cs` (possivelmente novo ou existente)
- `src/VisualSqlArchitect/Nodes/` (para criar um nó de tabela, se não existir)
- `src/VisualSqlArchitect/Metadata/MetadataService.cs` (para buscar tabelas disponíveis)

**Guia de Implementação:**
1.  **Buscar Tabelas Disponíveis:**
    - Use o `MetadataService` para obter a lista de tabelas da conexão ativa.
    - Adicione um método no `SearchMenuViewModel` que combine a lista de nós de transformação com a lista de tabelas.
2.  **Criar um Tipo de Resultado:** Crie uma classe separada ou use uma propriedade booleana `IsTable` em `NodeSearchResultViewModel` para distinguir entre nós de transformação e tabelas.
3.  **Renderizar Tabelas no Menu:**
    - Atualize o template de item no `SearchMenuControl.axaml` para exibir um ícone diferente para tabelas (ex: 🗂️ ou similar).
    - Se usar ícones SVG, certifique-se de que os caminhos dos arquivos estão corretos.
4.  **Manipular Seleção de Tabela:**
    - Quando uma tabela for selecionada, crie um nó especial (ex: `TableSourceNode`).
    - Este nó deve:
      - Conter uma referência à tabela (nome, schema, etc.).
      - Ter pinos de saída para cada coluna da tabela (opcional, pode ser um único pino se preferir modelar de forma mais simplificada).
      - Integrar-se com o compilador SQL para gerar a cláusula `SELECT [coluna1], [coluna2], ... FROM [tabela]` adequadamente.
5.  **Atualizar o Compilador:**
    - Modifique o `NodeGraphCompiler.cs` para reconhecer e gerar SQL apropriado para `TableSourceNode`.

---

### ~~Tarefa 10: Salvar e Restaurar Sessão do Canvas (Auto-save + Restore)~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Atualmente, há risco de perda de trabalho quando a aplicação é fechada inesperadamente, trava ou é encerrada sem salvamento manual. O usuário perde nós, conexões e estado visual do canvas.

**Comportamento Esperado:**
- A aplicação deve salvar automaticamente o estado do canvas em intervalos regulares e em eventos críticos.
- Ao iniciar, a aplicação deve detectar sessão anterior e oferecer:
  - **Restaurar última sessão**
  - **Iniciar novo canvas**
- O estado da sessão deve incluir:
  - Nós e posições
  - Conexões entre pinos
  - Zoom e pan
  - Conexão ativa
  - Estado de UI relevante (ex: tamanhos de painéis/splitters)

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/Serialization/CanvasSerializer.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/MainWindow.axaml.cs`
- `src/VisualSqlArchitect.UI/App.axaml.cs`

**Guia de Implementação:**
1.  **Formato de Persistência:**
    - Defina um arquivo de sessão (ex: `last-session.json`) em diretório de dados do usuário.
    - Inclua `schemaVersion` no JSON para compatibilidade futura.
2.  **Auto-save com Debounce:**
    - Acione salvamento quando houver mudança estrutural no canvas (adicionar/remover nó, mover nó, criar/remover conexão, alterar zoom/pan).
    - Use debounce (ex: 1000-2000ms) para evitar escrita excessiva.
3.  **Salvamento em Eventos Críticos:**
    - Forçar save em fechamento da janela, troca de conexão ativa e comando de salvar (`Ctrl+S`).
4.  **Restore na Inicialização:**
    - Na inicialização, verificar se existe sessão válida.
    - Mostrar prompt para restaurar ou ignorar.
    - Em caso de corrupção/incompatibilidade, registrar erro, descartar snapshot inválido e abrir canvas vazio.
5.  **Resiliência e Performance:**
    - Persistência deve ser assíncrona para não travar UI thread.
    - Garantir atomicidade de escrita (ex: salvar em arquivo temporário e substituir arquivo final).

**Critérios de Aceite:**
- Ao fechar e abrir a aplicação, o usuário consegue restaurar o último estado completo do canvas.
- Em encerramento inesperado, a recuperação oferece estado recente sem corrupção.
- Auto-save não degrada perceptivelmente a responsividade da UI.
- Mudanças de versão do arquivo de sessão são tratadas com fallback seguro.

---

### ~~Tarefa 11: Sistema de Validação Visual do Grafo e Prevenção de SQL Inválido~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Atualmente, o usuário pode montar fluxos no canvas que resultam em SQL inválido (pinos obrigatórios sem conexão, tipos incompatíveis, joins incompletos, alias duplicado ou vazio). Esses problemas só aparecem tarde no fluxo, durante preview/compilação/execução.

**Comportamento Esperado:**
- A validação deve ocorrer em tempo real, a cada alteração relevante no grafo.
- Erros bloqueantes devem ser exibidos visualmente no nó (borda destacada, indicador de erro e tooltip).
- Warnings devem ser exibidos sem bloquear execução.
- O botão/ação de executar preview deve ser bloqueado apenas quando existirem erros bloqueantes.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/Controls/NodeControl.axaml`
- `src/VisualSqlArchitect.UI/Controls/NodeControl.axaml.cs`
- `src/VisualSqlArchitect/Nodes/NodeGraphCompiler.cs`
- `src/VisualSqlArchitect/QueryEngine/QueryBuilderService.cs`

**Guia de Implementação:**
1.  **Criar Modelo de Diagnóstico:**
    - Criar uma estrutura como `ValidationIssue` com: `Severity` (Error/Warning), `NodeId`, `Code`, `Message` e `Suggestion`.
2.  **Validação por Regra:**
    - Implementar regras mínimas:
      - pinos obrigatórios conectados;
      - compatibilidade de tipo entre pinos;
      - parâmetros obrigatórios preenchidos;
      - joins com condição válida;
      - alias com nome válido, não vazio e sem duplicidade no escopo.
3.  **Validação Incremental:**
    - Revalidar o grafo após operações de editar/mover/conectar/desconectar/remover nó.
    - Evitar custo excessivo com debounce curto (ex: 100-250ms) para interações rápidas.
4.  **Exposição no ViewModel:**
    - Expor propriedades como `HasErrors`, `ErrorCount`, `WarningCount` e coleção de issues por nó.
    - Garantir `RaisePropertyChanged` para refletir estado na UI imediatamente.
5.  **Feedback Visual no Nó:**
    - No `NodeControl`, aplicar classes/estilos por estado (`error`, `warning`).
    - Exibir tooltip com mensagem amigável e ação sugerida de correção.
6.  **Integração com Compilação/Execução:**
    - No pipeline de compilação, retornar erros estruturados (não apenas exception textual).
    - Bloquear execução quando `HasErrors == true`.

**Critérios de Aceite:**
- Erros e warnings aparecem em tempo real ao editar o canvas.
- O usuário consegue identificar exatamente qual nó está inválido e por quê.
- SQL inválido bloqueante não é executado.
- Fluxos válidos continuam executando sem regressão perceptível de performance.

---

### ~~Tarefa 13: Sistema de Snap e Alinhamento Inteligente no Canvas~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Ao mover nós no canvas, o posicionamento é totalmente livre e dificulta manter diagramas organizados. O usuário precisa alinhar manualmente, o que consome tempo e aumenta inconsistências visuais.

**Comportamento Esperado:**
- O usuário pode ativar/desativar snap ao grid.
- Durante o arraste de nós, guias visuais devem aparecer ao alinhar com outros nós.
- Deve existir ação para distribuir/alinha automaticamente seleção de nós.
- O auto-arranjo deve preservar legibilidade e evitar sobreposição.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/Controls/InfiniteCanvas.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/Controls/BezierWireLayer.cs`
- `src/VisualSqlArchitect.UI/MainWindow.axaml`

**Guia de Implementação:**
1.  **Snap ao Grid:**
    - Definir tamanho de grid (ex: 8, 12 ou 16 px).
    - Aplicar snap no fim do drag ou em tempo real (configurável).
2.  **Guias de Alinhamento:**
    - Detectar alinhamento de bordas e centros entre nó ativo e nós próximos.
    - Renderizar guias horizontais/verticais temporárias no canvas.
3.  **Comandos de Layout:**
    - Adicionar ações: alinhar à esquerda/direita/topo/base, centralizar e distribuir.
4.  **Auto-arranjo Básico:**
    - Implementar algoritmo simples de layout para seleção (ou grafo inteiro) sem colisões diretas.
5.  **Preferências do Usuário:**
    - Persistir opção de snap ligado/desligado e tamanho do grid.

**Critérios de Aceite:**
- Nós podem ser alinhados com previsibilidade e rapidez.
- Guias aparecem apenas quando úteis e não poluem a UI.
- Diagramas médios podem ser organizados sem ajuste manual excessivo.

---

### ~~Tarefa 14: Melhorar Acessibilidade e Navegação por Teclado~~ ✅ CONCLUÍDA

**Descrição do Problema:**
A navegação atual depende fortemente de mouse. Existem limitações de foco, contraste e acesso por teclado para fluxos comuns, prejudicando usabilidade e acessibilidade.

**Comportamento Esperado:**
- Fluxos principais devem ser executáveis via teclado.
- Ordem de foco entre controles deve ser consistente.
- Estados de foco precisam ser visíveis.
- Contraste deve atender níveis aceitáveis para leitura em todas as áreas críticas.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/MainWindow.axaml`
- `src/VisualSqlArchitect.UI/Assets/Themes/AppStyles.axaml`
- `src/VisualSqlArchitect.UI/Assets/Themes/DesignTokens.axaml`
- `src/VisualSqlArchitect.UI/Controls/*.axaml`

**Guia de Implementação:**
1.  **Mapa de Foco:**
    - Definir ordem de tabulação para toolbar, canvas, sidebars, bottom bar e diálogos.
2.  **Atalhos de Teclado:**
    - Expandir atalhos para ações frequentes (abrir conexão, focar busca, alternar painéis).
3.  **Feedback Visual de Foco:**
    - Estilizar foco com alto contraste e consistência entre controles.
4.  **Semântica e Labels:**
    - Garantir nomes acessíveis e descrições em controles interativos.
5.  **Validação de Contraste:**
    - Ajustar tokens de cor para texto/ícones com baixa legibilidade.

**Critérios de Aceite:**
- Usuário navega e executa fluxo principal sem mouse.
- Foco visível em todos os controles interativos relevantes.
- Itens críticos de UI possuem contraste adequado.

---

### ~~Tarefa 15: Histórico Avançado de Undo/Redo com Agrupamento de Ações~~ ✅ CONCLUÍDA

**Descrição do Problema:**
O histórico de undo/redo pode ficar poluído por micro-ações (ex: cada pequeno movimento durante drag). Isso torna reversão lenta e imprecisa.

**Comportamento Esperado:**
- Ações contínuas devem ser agrupadas em uma única entrada lógica de histórico.
- Undo/Redo deve restaurar estado de forma previsível para nó, conexão e propriedades.
- A UI deve exibir descrição legível da ação que será desfeita/refeita.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/ViewModels/UndoRedoStack.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/Controls/InfiniteCanvas.cs`

**Guia de Implementação:**
1.  **Modelo de Transação:**
    - Criar mecanismo de begin/end transaction para agrupar operações relacionadas.
2.  **Agrupamento de Interações Contínuas:**
    - Drag, resize e edição textual contínua entram como uma ação única.
3.  **Descrições de Histórico:**
    - Gerar rótulos como "Mover 3 nós" ou "Conectar Clientes -> Pedidos".
4.  **Integridade de Estado:**
    - Garantir restauração correta de seleção, posições e conexões.
5.  **Testes de Regressão:**
    - Cobrir cenários de múltiplas ações intercaladas com undo/redo.

**Critérios de Aceite:**
- Undo/Redo exige menos passos para voltar estado esperado.
- Histórico permanece consistente após sessões longas de edição.
- Não há perda de dados ao desfazer/refazer ações compostas.

---

### ~~Tarefa 16: Health Check e Teste de Conexão com Diagnóstico~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Ao criar/editar conexões de banco, falhas de rede, credenciais ou SSL podem retornar mensagens vagas e dificultar diagnóstico.

**Comportamento Esperado:**
- A tela de conexão deve ter botão "Testar Conexão" com resultado detalhado.
- Erros devem ser traduzidos para mensagens amigáveis por provedor.
- A conexão ativa deve exibir status de saúde visível na UI.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/Providers/*.cs`
- `src/VisualSqlArchitect/Metadata/MetadataService.cs`
- `src/VisualSqlArchitect.UI/ViewModels/ConnectionManagerViewModel.cs`
- `src/VisualSqlArchitect.UI/MainWindow.axaml`

**Guia de Implementação:**
1.  **Teste de Conectividade:**
    - Validar host, porta, autenticação, database e timeout.
2.  **Erros Enriquecidos:**
    - Mapear exceções comuns para mensagens claras (credencial inválida, DNS, firewall, SSL).
3.  **Indicador de Estado:**
    - Exibir status online/offline/degradado da conexão ativa.
4.  **Retry e Timeout Configurável:**
    - Permitir ajuste de timeout e tentativa controlada de reconexão.
5.  **Observabilidade:**
    - Registrar diagnósticos úteis para troubleshooting sem expor senha.

**Critérios de Aceite:**
- Usuário identifica rapidamente a causa de falha de conexão.
- Troca de conexão não deixa aplicação em estado inconsistente.
- Status da conexão ativa fica claramente visível.

---

### ~~Tarefa 17: Catálogo de Ícones Unificado e Fallback Seguro~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Há ícones quebrados e uso inconsistente de assets visuais, causando UI incompleta e difícil manutenção.

**Comportamento Esperado:**
- Todos os ícones devem ser resolvidos a partir de catálogo centralizado.
- Quando um ícone não existir, um fallback visual padrão deve ser exibido.
- Inclusão de novos ícones deve seguir convenção única de nome e localização.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/Assets/`
- `src/VisualSqlArchitect.UI/Assets/Themes/AppStyles.axaml`
- `src/VisualSqlArchitect.UI/Controls/*.axaml`
- `src/VisualSqlArchitect.UI/VisualSqlArchitect.UI.csproj`

**Guia de Implementação:**
1.  **Inventário de Ícones:**
    - Listar todos os ícones usados e mapear origem/caminho.
2.  **Registro Central:**
    - Criar dicionário/serviço de resolução de ícones com chaves padronizadas.
3.  **Fallback Padrão:**
    - Definir ícone default para entradas ausentes.
4.  **Validação em Build/Startup:**
    - Detectar referências inválidas de ícone e registrar aviso/erro.
5.  **Padronização:**
    - Documentar convenção de nomes e tamanhos recomendados.

**Critérios de Aceite:**
- Nenhum ícone quebrado em telas principais.
- Novos componentes usam o catálogo central sem hardcode de paths.
- Falhas de asset não quebram renderização da UI.

---

### ~~Tarefa 18: Otimização de Performance do Canvas para Grafos Grandes~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Com muitos nós e conexões, o canvas pode apresentar queda de FPS, atrasos em drag e lentidão geral.

**Comportamento Esperado:**
- Interações críticas devem permanecer fluidas em grafos grandes.
- Redraw de elementos deve ser otimizado para viewport atual.
- Deve existir modo de diagnóstico de performance para análise contínua.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/Controls/InfiniteCanvas.cs`
- `src/VisualSqlArchitect.UI/Controls/BezierWireLayer.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/MainWindow.axaml`

**Guia de Implementação:**
1.  **Profiling Inicial:**
    - Medir tempos de render, hit-test e operações de drag/zoom.
2.  **Render Parcial:**
    - Processar prioritariamente nós/conexões dentro da viewport.
3.  **Throttling de Atualizações:**
    - Limitar frequência de redraw em operações contínuas intensas.
4.  **Cache de Geometria:**
    - Reaproveitar cálculos de curvas/fios quando não houver mudança relevante.
5.  **Métricas em Debug:**
    - Exibir FPS e tempos médios de frame em modo desenvolvimento.

**Critérios de Aceite:**
- Canvas permanece responsivo com grafos extensos.
- Arraste de nós, pan/zoom e conexões não apresentam travamentos perceptíveis.
- Métricas de performance mostram ganho mensurável após otimizações.

---

### ~~Tarefa 19: Biblioteca de Templates de Consulta (Query Starters)~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Criar fluxos comuns do zero é repetitivo e lento, especialmente para usuários iniciantes. Falta um ponto de partida com estruturas prontas para cenários recorrentes.

**Comportamento Esperado:**
- A aplicação deve oferecer templates prontos (ex: SELECT simples, JOIN básico, agregação com GROUP BY, paginação).
- Ao escolher um template, o canvas deve ser preenchido com nós pré-configurados e conectados.
- Campos variáveis do template (tabelas, filtros e ordenações) devem ficar destacados para edição rápida.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/Controls/SearchMenuControl.axaml`
- `src/VisualSqlArchitect.UI/Controls/SearchMenuControl.axaml.cs`
- `src/VisualSqlArchitect/Nodes/NodeDefinition.cs`

**Guia de Implementação:**
1.  **Catálogo de Templates:**
    - Definir lista de templates por categoria e, quando aplicável, por provedor.
2.  **Modelo de Template:**
    - Criar estrutura serializável para descrever nós, conexões e parâmetros iniciais.
3.  **Inserção no Canvas:**
    - Implementar comando para instanciar template preservando consistência de IDs e links.
4.  **Campos Editáveis Destacados:**
    - Marcar parâmetros obrigatórios para completar após inserção.
5.  **Compatibilidade SQL:**
    - Validar template contra provedor ativo e exibir warning em incompatibilidades.

**Critérios de Aceite:**
- Usuário monta consulta funcional em poucos cliques com template.
- Templates não geram grafo quebrado ao inserir.
- Fluxos comuns exigem menos passos que montagem manual.

---

### ~~Tarefa 20: Pré-visualização de Dados em Amostra no Próprio Nó~~ ✅ CONCLUÍDA

**Descrição do Problema:**
O usuário precisa alternar constantemente entre canvas e painel de preview para validar transformações. Isso reduz produtividade e dificulta depuração de etapas intermediárias.

**Comportamento Esperado:**
- Nós de origem e transformação selecionados devem permitir preview rápido de amostra (ex: top 10 linhas).
- O nó deve exibir estado de carregamento, sucesso e erro de preview.
- A atualização deve ser eficiente, com cache de curta duração para evitar consultas repetidas.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/Controls/NodeControl.axaml`
- `src/VisualSqlArchitect.UI/Controls/NodeControl.axaml.cs`
- `src/VisualSqlArchitect/QueryEngine/QueryBuilderService.cs`

**Guia de Implementação:**
1.  **Comando de Preview por Nó:**
    - Adicionar ação contextual para carregar amostra da saída do nó.
2.  **Limite e Timeout:**
    - Aplicar limite de linhas e timeout curto para preservar responsividade.
3.  **Cache Temporário:**
    - Cachear resultados por assinatura de nó/consulta com invalidação em mudança de parâmetros/conexões.
4.  **UI de Estado:**
    - Exibir loading, erro amigável e timestamp do último preview.
5.  **Fallback Seguro:**
    - Se preview falhar, manter canvas operável e registrar diagnóstico.

**Critérios de Aceite:**
- Preview de amostra aparece rapidamente nos nós suportados.
- Falhas de preview não interrompem edição do grafo.
- Uso de cache reduz chamadas redundantes perceptivelmente.

---

### ~~Tarefa 21: Diff e Histórico de Versões do Fluxo~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Após múltiplas alterações no canvas, é difícil entender o que mudou e voltar a uma versão estável com segurança.

**Comportamento Esperado:**
- O usuário deve poder criar checkpoints de versão do fluxo.
- Deve existir visualização de diff entre versões (nós, conexões, parâmetros e propriedades alteradas).
- Deve ser possível restaurar versão anterior com confirmação.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/Serialization/CanvasSerializer.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/MainWindow.axaml`
- `src/VisualSqlArchitect.UI/MainWindow.axaml.cs`

**Guia de Implementação:**
1.  **Versionamento Local:**
    - Persistir snapshots com metadata (data/hora, autor/operação, descrição).
2.  **Engine de Diff:**
    - Comparar versões por IDs estáveis de nó/conexão e parâmetros.
3.  **Visualização de Diferenças:**
    - Exibir itens adicionados, removidos e modificados com destaque claro.
4.  **Restauração Controlada:**
    - Implementar restore com confirmação e opção de backup da versão atual.
5.  **Integração com Auto-save:**
    - Reaproveitar infraestrutura da Tarefa 10 para snapshots/versionamento.

**Critérios de Aceite:**
- Usuário entende com clareza o que mudou entre versões.
- Restauração funciona sem corromper estado atual.
- Checkpoints permitem recuperar fluxo estável com poucos passos.

---

### ~~Tarefa 22: Normalização Cross-Provider de SQL (Postgres/MySQL/SQL Server)~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Consultas que funcionam em um provedor podem falhar em outro por diferenças de funções, quoting, operadores e sintaxe específica.

**Comportamento Esperado:**
- O sistema deve adaptar geração SQL ao provedor ativo automaticamente sempre que possível.
- Quando não for possível adaptar, deve exibir warning/erro com sugestão objetiva.
- Usuário deve conseguir trocar provedor com menor risco de quebra em fluxos comuns.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/QueryEngine/QueryGeneratorService.cs`
- `src/VisualSqlArchitect/Registry/SqlFunctionRegistry.cs`
- `src/VisualSqlArchitect/Core/IDbOrchestrator.cs`
- `src/VisualSqlArchitect.UI/ViewModels/LiveSqlBarViewModel.cs`

**Guia de Implementação:**
1.  **Matriz de Compatibilidade:**
    - Definir equivalências de funções/operadores por provedor.
2.  **Camada de Normalização:**
    - Centralizar regras de quoting, casts, paginação e funções não portáveis.
3.  **Diagnóstico de Portabilidade:**
    - Marcar nós ou trechos SQL incompatíveis com provedor ativo.
4.  **Sugestões Automáticas:**
    - Oferecer substituição sugerida quando existir equivalente.
5.  **Testes Multi-provider:**
    - Cobrir cenários essenciais em Postgres, MySQL e SQL Server.

**Critérios de Aceite:**
- Fluxos comuns continuam válidos ao trocar provedor.
- Incompatibilidades são informadas com mensagem acionável.
- SQL gerado respeita sintaxe do provedor selecionado.

---

### ~~Tarefa 23: Layout Automático por Contexto (Readability-first)~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Em grafos médios e grandes, a organização manual dos nós consome muito tempo e frequentemente resulta em sobreposição visual e fluxos difíceis de ler.

**Comportamento Esperado:**
- O usuário deve poder executar um comando de auto layout para organizar nós em camadas lógicas.
- O layout deve priorizar legibilidade (fontes à esquerda, transformações ao centro, saídas à direita).
- Não deve haver sobreposição de nós após aplicação do layout.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/Controls/InfiniteCanvas.cs`
- `src/VisualSqlArchitect.UI/MainWindow.axaml`

**Guia de Implementação:**
1.  **Classificação por Camadas:**
    - Separar nós por categoria/nível de dependência no grafo.
2.  **Posicionamento Inicial:**
    - Definir coordenadas por coluna e espaçamento uniforme por linha.
3.  **Resolução de Colisões:**
    - Aplicar ajuste iterativo para remover sobreposição de bounding boxes.
4.  **Comando na UI:**
    - Expor ação "Auto Layout" em menu e atalho configurável.
5.  **Preservação de Contexto:**
    - Opcionalmente limitar auto layout à seleção atual.

**Critérios de Aceite:**
- Auto layout produz resultado legível em um único comando.
- Nós não ficam sobrepostos após aplicação.
- Usuário consegue continuar edição sem ajustes extensos pós-layout.

---

### ~~Tarefa 24: Inspector de Query com Explain Plan Integrado~~ ✅ CONCLUÍDA

**Descrição do Problema:**
O usuário gera SQL, mas não consegue avaliar facilmente custo e estratégia de execução sem sair da aplicação para ferramentas externas.

**Comportamento Esperado:**
- A UI deve permitir visualizar `EXPLAIN`/plano de execução para a query atual (quando suportado pelo provedor).
- O painel deve destacar pontos críticos de custo (scan pesado, sort, hash join, etc.).
- Deve existir fallback amigável quando o provedor não suportar a operação.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/ViewModels/LiveSqlBarViewModel.cs`
- `src/VisualSqlArchitect.UI/Controls/LiveSqlBar.axaml`
- `src/VisualSqlArchitect/Providers/*.cs`
- `src/VisualSqlArchitect/QueryEngine/QueryBuilderService.cs`

**Guia de Implementação:**
1.  **Comando Explain:**
    - Adicionar ação para executar `EXPLAIN` (ou equivalente) da SQL gerada.
2.  **Parser de Resultado:**
    - Normalizar saída por provedor para modelo comum de exibição.
3.  **Painel de Diagnóstico:**
    - Exibir etapas do plano com custo estimado e alertas visuais.
4.  **Fallback por Provedor:**
    - Mensagem clara quando explain não estiver disponível ou autorizado.
5.  **Segurança:**
    - Evitar execução de operações mutáveis nesse fluxo (somente leitura).

**Critérios de Aceite:**
- Usuário visualiza plano de execução sem sair da aplicação.
- Pontos de custo alto ficam claramente identificados.
- Feature funciona para provedores suportados sem quebrar os demais.

---

### ~~Tarefa 25: Sistema de Favoritos e Snippets de Nós~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Fluxos recorrentes precisam ser reconstruídos manualmente em novos projetos, gerando retrabalho e inconsistência entre consultas parecidas.

**Comportamento Esperado:**
- O usuário deve salvar subgrafos como snippet reutilizável.
- Deve ser possível favoritar nós e snippets para acesso rápido.
- Inserção de snippet deve preservar conexões internas e destacar campos a configurar.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/Serialization/CanvasSerializer.cs`
- `src/VisualSqlArchitect.UI/Controls/SearchMenuControl.axaml`
- `src/VisualSqlArchitect.UI/Controls/SearchMenuControl.axaml.cs`

**Guia de Implementação:**
1.  **Modelo de Snippet:**
    - Definir formato serializável com nós, conexões e metadata.
2.  **Salvar Seleção como Snippet:**
    - Permitir salvar seleção atual com nome, tags e descrição.
3.  **Catálogo e Favoritos:**
    - Listar snippets e itens favoritos em seção dedicada do menu de busca.
4.  **Inserção com Mapeamento de IDs:**
    - Gerar novos IDs para evitar conflito com grafo existente.
5.  **Gestão de Snippets:**
    - Permitir editar, remover e renomear snippets salvos.

**Critérios de Aceite:**
- Usuário salva e reinsere subgrafo em poucos cliques.
- Snippets inseridos não corrompem o grafo atual.
- Favoritos aceleram acesso a componentes recorrentes.

---

### ~~Tarefa 26: Diagnóstico de Nó Órfão e Limpeza Automática~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Com o tempo, o canvas acumula nós sem impacto na query final (órfãos ou desconectados), aumentando complexidade visual e risco de erro.

**Comportamento Esperado:**
- A aplicação deve identificar nós sem contribuição para saída final.
- Deve sugerir limpeza automática com pré-visualização do que será removido.
- O usuário deve poder revisar e confirmar antes da remoção.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/Nodes/NodeGraphCompiler.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/MainWindow.axaml`

**Guia de Implementação:**
1.  **Análise de Alcance:**
    - Percorrer grafo a partir de nós de saída para marcar nós relevantes.
2.  **Detecção de Órfãos:**
    - Classificar nós não alcançados como candidatos à limpeza.
3.  **Pré-visualização na UI:**
    - Destacar candidatos visualmente e exibir resumo.
4.  **Ação de Limpeza Segura:**
    - Executar remoção com confirmação e suporte a undo.
5.  **Relatório Pós-limpeza:**
    - Informar quantidade e tipo de itens removidos.

**Critérios de Aceite:**
- Nós sem efeito na saída são detectados com precisão.
- Limpeza automática reduz ruído sem remover nós relevantes.
- Operação é reversível via undo.

---

### ~~Tarefa 28: Exportar Documentação do Fluxo (Markdown + Imagem)~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Não existe uma forma padronizada de documentar o fluxo visual para auditoria, handoff técnico e compartilhamento com o time.

**Comportamento Esperado:**
- Deve existir exportação de documentação contendo:
  - imagem do grafo;
  - SQL gerada;
  - parâmetros principais;
  - metadata básica (data, conexão/provedor, versão).
- Formato alvo: Markdown com assets anexos.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/MainWindow.axaml.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect/QueryEngine/QueryGeneratorService.cs`

**Guia de Implementação:**
1.  **Captura do Canvas:**
    - Gerar snapshot da área útil do grafo em imagem.
2.  **Coleta de Conteúdo:**
    - Obter SQL atual, parâmetros e informações da conexão ativa.
3.  **Template Markdown:**
    - Montar documento padrão com seções fixas.
4.  **Exportação de Arquivos:**
    - Salvar `.md` e imagem em pasta selecionada pelo usuário.
5.  **Reprodutibilidade:**
    - Incluir metadata de versão para rastreabilidade.

**Critérios de Aceite:**
- Documento exportado é legível e completo para revisão técnica.
- Imagem e SQL refletem estado atual do canvas.
- Exportação funciona sem bloquear UI.

---

### ~~Tarefa 29: Guardrails de Segurança para Execução de Query~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Consultas potencialmente perigosas podem ser executadas sem alerta (ex: ausência de `LIMIT` em preview, operações de alto custo sem filtros), aumentando risco operacional.

**Comportamento Esperado:**
- Antes de executar, o sistema deve validar regras de segurança configuráveis.
- Deve bloquear ou alertar conforme severidade e ambiente (dev/homolog/prod).
- Usuário deve receber mensagem clara com motivo e sugestão de correção.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/QueryEngine/QueryBuilderService.cs`
- `src/VisualSqlArchitect.UI/ViewModels/LiveSqlBarViewModel.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`

**Guia de Implementação:**
1.  **Políticas Configuráveis:**
    - Definir conjunto de regras (ex: exigir `LIMIT` em preview, bloquear mutações em modo leitura).
2.  **Validação Pré-execução:**
    - Executar checagem antes do dispatch da query ao provedor.
3.  **Classificação de Severidade:**
    - Suportar warning e bloqueio com justificativa objetiva.
4.  **Perfis por Ambiente:**
    - Regras mais rígidas em produção, mais flexíveis em desenvolvimento.
5.  **Log de Decisão:**
    - Registrar evento de bloqueio/alerta para auditoria.

**Critérios de Aceite:**
- Regras de segurança são aplicadas de forma previsível.
- Consultas inseguras geram alerta ou bloqueio conforme política.
- Mensagens orientam claramente como corrigir o problema.

---

### ~~Tarefa 30: Command Palette de Ações Globais~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Com o crescimento de funcionalidades, ações importantes ficam dispersas em menus e botões, dificultando descoberta e produtividade.

**Comportamento Esperado:**
- A aplicação deve ter uma command palette acessível por atalho global.
- A palette deve permitir busca fuzzy e execução rápida de ações.
- Deve incluir ações como: adicionar nó, trocar conexão, auto layout, exportar, abrir validação e executar preview.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/MainWindow.axaml`
- `src/VisualSqlArchitect.UI/MainWindow.axaml.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/Controls/SearchMenuControl.axaml`

**Guia de Implementação:**
1.  **Modelo de Comando Global:**
    - Definir item com nome, descrição, atalho e callback.
2.  **UI da Palette:**
    - Criar overlay modal com campo de busca e lista de resultados ranqueados.
3.  **Busca Fuzzy:**
    - Implementar matching tolerante a digitação parcial.
4.  **Integração com Ações Existentes:**
    - Reusar comandos já implementados no ViewModel.
5.  **Extensibilidade:**
    - Permitir cadastro incremental de novos comandos.

**Critérios de Aceite:**
- Usuário executa ações globais sem navegar menus profundos.
- Busca retorna resultados relevantes com poucas teclas.
- Abertura e execução da palette são rápidas e estáveis.

---

### ~~Tarefa 31: Modo de Execução Segura com Sandbox de Preview~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Consultas de preview podem causar risco operacional quando comandos mutáveis são executados por engano em ambientes reais.

**Comportamento Esperado:**
- O preview deve rodar sempre em modo seguro (somente leitura).
- Comandos mutáveis (`INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `TRUNCATE`) devem ser bloqueados no preview.
- A UI deve indicar claramente que o usuário está em "Safe Preview Mode".

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/QueryEngine/QueryBuilderService.cs`
- `src/VisualSqlArchitect.UI/ViewModels/LiveSqlBarViewModel.cs`
- `src/VisualSqlArchitect.UI/Controls/LiveSqlBar.axaml`
- `src/VisualSqlArchitect/Core/IDbOrchestrator.cs`

**Guia de Implementação:**
1.  **Classificação de SQL:**
    - Implementar verificação da natureza do comando (read-only vs mutável).
2.  **Bloqueio no Preview:**
    - Interromper execução de preview quando detectar comando mutável.
3.  **Sinalização Visual:**
    - Exibir badge/banner de modo seguro ativo na barra de SQL.
4.  **Mensagens Ação-Oriented:**
    - Mostrar erro com sugestão objetiva quando bloqueado.
5.  **Telemetria/Auditoria:**
    - Registrar tentativas bloqueadas para diagnóstico.

**Critérios de Aceite:**
- Preview nunca executa comando mutável.
- Usuário entende claramente por que a execução foi bloqueada.
- Fluxo de preview read-only continua rápido e estável.

---

### ~~Tarefa 32: Assistente de Join com Auto-sugestão e Score de Confiança~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Criar joins manualmente é propenso a erro, especialmente em esquemas grandes com múltiplas chaves relacionais possíveis.

**Comportamento Esperado:**
- O sistema deve sugerir joins com base em FK e padrões de nomenclatura.
- Cada sugestão deve exibir score de confiança.
- O usuário pode aceitar, editar ou rejeitar a sugestão rapidamente.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/Metadata/MetadataService.cs`
- `src/VisualSqlArchitect/AutoJoinDetector.cs`
- `src/VisualSqlArchitect.UI/ViewModels/AutoJoinOverlayViewModel.cs`
- `src/VisualSqlArchitect.UI/Controls/AutoJoinOverlay.axaml`

**Guia de Implementação:**
1.  **Motor de Sugestão:**
    - Combinar FK explícita com heurísticas de nome de coluna/tabela.
2.  **Cálculo de Confiança:**
    - Atribuir score com base em qualidade da correspondência.
3.  **UI de Revisão:**
    - Mostrar sugestão com origem e score antes de aplicar.
4.  **Ações Rápidas:**
    - Implementar aceitar/editar/rejeitar sem sair do fluxo principal.
5.  **Aprendizado de Preferência (opcional):**
    - Priorizar padrões frequentemente aceitos pelo usuário.

**Critérios de Aceite:**
- Joins sugeridos reduzem passos manuais de criação.
- Score ajuda a escolher entre múltiplas opções plausíveis.
- Sugestões incorretas podem ser descartadas sem fricção.

---

### ~~Tarefa 33: Validador de Convenções de Naming SQL~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Sem regras de convenção, consultas geradas podem ficar inconsistentes entre projetos e desenvolvedores (alias, casing, nomes de expressões), dificultando manutenção.

**Comportamento Esperado:**
- O sistema deve validar naming conforme política configurável do projeto.
- Deve sinalizar violações e sugerir correção.
- Quando possível, oferecer auto-fix para problemas simples.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/QueryEngine/QueryGeneratorService.cs`
- `src/VisualSqlArchitect/Registry/SqlFunctionRegistry.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/Assets/Themes/AppStyles.axaml`

**Guia de Implementação:**
1.  **Política Configurável:**
    - Definir regras como `snake_case`, prefixos/sufixos e padrão de aliases.
2.  **Validação em Tempo de Geração:**
    - Aplicar checagem ao gerar SQL e também no grafo (quando possível).
3.  **Diagnóstico por Regra:**
    - Exibir código da regra violada e exemplo de formato correto.
4.  **Auto-fix Opcional:**
    - Corrigir automaticamente alias e nomes derivados quando seguro.
5.  **Relatório de Conformidade:**
    - Expor resumo de conformidade da query atual.

**Critérios de Aceite:**
- SQL gerado segue convenção definida quando não houver exceções.
- Violações são apresentadas com mensagens claras e acionáveis.
- Auto-fix não altera semântica da query.

---

### ~~Tarefa 34: Comparação Lado a Lado entre Fluxo Visual e SQL Final~~ ✅ CONCLUÍDA

**Descrição do Problema:**
É difícil rastrear quais nós do canvas originam trechos específicos da SQL final, tornando depuração e revisão mais lentas.

**Comportamento Esperado:**
- A UI deve exibir visual e SQL lado a lado.
- Selecionar um nó deve destacar trechos correlatos na SQL.
- Selecionar trecho de SQL deve indicar quais nós contribuíram para ele.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/MainWindow.axaml`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/ViewModels/LiveSqlBarViewModel.cs`
- `src/VisualSqlArchitect/Nodes/NodeGraphCompiler.cs`

**Guia de Implementação:**
1.  **Mapa de Proveniência:**
    - Criar estrutura que relacione nó/expressão visual ao trecho gerado na SQL.
2.  **UI de Comparação:**
    - Implementar painel split com sincronização de seleção.
3.  **Highlight Bidirecional:**
    - Destacar visual->SQL e SQL->visual.
4.  **Ação "Explicar Origem":**
    - Exibir resumo de como trecho foi construído.
5.  **Fallback em Casos Ambíguos:**
    - Mostrar múltiplas origens quando o trecho for composto.

**Critérios de Aceite:**
- Usuário rastreia rapidamente a origem de cada trecho SQL.
- Debug de problemas de compilação fica mais rápido e previsível.
- Interface permanece responsiva durante highlights cruzados.

---

### ~~Tarefa 36: Diagnóstico Automático Pós-Erro com Sugestões de Correção~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Quando ocorre erro de execução/compilação, o usuário recebe mensagens pouco acionáveis e perde tempo para entender causa raiz.

**Comportamento Esperado:**
- Erros devem ser classificados automaticamente por categoria (conexão, sintaxe, timeout, compatibilidade, autorização, etc.).
- A UI deve exibir causa provável e próximos passos objetivos.
- O sistema deve anexar contexto técnico suficiente para troubleshooting sem expor dados sensíveis.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/QueryEngine/QueryBuilderService.cs`
- `src/VisualSqlArchitect.UI/ViewModels/LiveSqlBarViewModel.cs`
- `src/VisualSqlArchitect.UI/MainWindow.axaml.cs`
- `src/VisualSqlArchitect.UI/Controls/LiveSqlBar.axaml`

**Guia de Implementação:**
1.  **Modelo de Erro Estruturado:**
    - Definir campos como categoria, severidade, mensagem amigável, detalhe técnico e sugestão.
2.  **Classificador de Falhas:**
    - Mapear exceções conhecidas para categorias com alta precisão.
3.  **Sugestões Contextuais:**
    - Gerar recomendações baseadas em tipo de erro e provedor.
4.  **UI de Feedback:**
    - Exibir bloco de erro com ações rápidas (copiar detalhe, abrir docs internas, tentar novamente).
5.  **Privacidade e Segurança:**
    - Sanitizar connection strings e dados sensíveis nos logs/erros exibidos.

**Critérios de Aceite:**
- Erro exibido com causa provável e ação recomendada.
- Tempo médio para resolver falhas recorrentes reduz visivelmente.
- Logs mantêm utilidade técnica sem vazar credenciais.

---

### ~~Tarefa 37: Importador SQL para Grafo Visual~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Muitas equipes já possuem queries SQL legadas e hoje precisam reconstruí-las manualmente no canvas, o que é lento e propenso a erro.

**Comportamento Esperado:**
- O usuário deve colar uma query SQL e gerar um grafo inicial automaticamente.
- O sistema deve criar nós e conexões equivalentes para estruturas suportadas.
- Trechos não suportados devem ser sinalizados com fallback claro (nó raw SQL ou aviso).

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/QueryEngine/QueryGeneratorService.cs`
- `src/VisualSqlArchitect/Nodes/NodeGraphCompiler.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/MainWindow.axaml`

**Guia de Implementação:**
1.  **Parser SQL de Entrada:**
    - Usar parser robusto para decompor `SELECT`, `FROM`, `JOIN`, `WHERE`, `GROUP BY`, `ORDER BY`.
2.  **Mapeamento para Nós:**
    - Converter estruturas parseadas em nós equivalentes e conectar dependências.
3.  **Fallback de Compatibilidade:**
    - Encapsular expressões não suportadas em nós especiais com aviso.
4.  **Relatório de Conversão:**
    - Exibir resumo com itens importados, parciais e não suportados.
5.  **Round-trip Básico:**
    - Garantir que SQL importada e recompilada seja semanticamente equivalente nos casos suportados.

**Critérios de Aceite:**
- Usuário importa SQL comum e obtém grafo funcional.
- Conversões parciais são informadas com clareza.
- Fluxo importado pode ser editado e recompilado sem quebrar.

---

### ~~Tarefa 38: Exportação de Grafo em Formato Interoperável Versionado~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Sem formato padronizado de intercâmbio, compartilhar fluxos entre ambientes/equipes é frágil e sujeito a incompatibilidades de versão.

**Comportamento Esperado:**
- O grafo deve ser exportado/importado em JSON versionado.
- O arquivo deve incluir `schemaVersion` e metadata de compatibilidade.
- Importação deve validar esquema e aplicar migração quando necessário.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/Serialization/CanvasSerializer.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/MainWindow.axaml.cs`

**Guia de Implementação:**
1.  **Schema Formal:**
    - Definir contrato JSON explícito para nós, conexões, parâmetros e layout.
2.  **Versionamento:**
    - Incluir `schemaVersion`, `appVersion` e `createdAt`.
3.  **Validação de Entrada:**
    - Rejeitar arquivos inválidos com mensagem acionável.
4.  **Migração de Versões:**
    - Implementar pipeline de migrações para versões antigas.
5.  **Testes de Compatibilidade:**
    - Cobrir export/import cross-version e round-trip.

**Critérios de Aceite:**
- Arquivos exportados são reimportáveis sem perda estrutural.
- Incompatibilidades de versão são tratadas com fallback seguro.
- Compartilhamento entre máquinas/usuários funciona com previsibilidade.

---

### ~~Tarefa 39: Execução Assíncrona com Cancelamento Real e Progresso~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Execuções longas degradam experiência do usuário quando não há feedback de progresso e cancelamento efetivo.

**Comportamento Esperado:**
- Execução de preview/query deve ser assíncrona com indicador visual de andamento.
- O usuário deve conseguir cancelar operação em andamento com efeito real no backend.
- O estado da UI deve refletir corretamente execução, cancelamento e término.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/Providers/*.cs`
- `src/VisualSqlArchitect/QueryEngine/QueryBuilderService.cs`
- `src/VisualSqlArchitect.UI/ViewModels/LiveSqlBarViewModel.cs`
- `src/VisualSqlArchitect.UI/Controls/LiveSqlBar.axaml`

**Guia de Implementação:**
1.  **CancellationToken End-to-End:**
    - Propagar token desde UI até camada de provedor.
2.  **Progresso de Execução:**
    - Expor estados (`Running`, `Completed`, `Canceled`, `Failed`) e progresso quando disponível.
3.  **Botão Cancelar:**
    - Adicionar ação de cancelamento com desabilitação apropriada.
4.  **Timeout Configurável:**
    - Permitir timeout padrão e sobrescrita por execução.
5.  **Limpeza de Estado:**
    - Garantir reset correto da UI após cancelamento/erro.

**Critérios de Aceite:**
- Usuário vê progresso/estado em tempo real.
- Cancelamento interrompe efetivamente execução longa.
- UI não fica travada após operações demoradas.

---

### ~~Tarefa 40: Gerenciamento Seguro de Credenciais (Vault/Keychain)~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Armazenar credenciais em texto simples ou de forma inadequada aumenta risco de vazamento e não atende boas práticas de segurança.

**Comportamento Esperado:**
- Credenciais devem ser armazenadas de forma segura (keychain/secret store do sistema operacional).
- A aplicação deve suportar rotação e atualização segura de credenciais.
- Logs e telas nunca devem exibir senha/token em texto claro.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/Providers/*.cs`
- `src/VisualSqlArchitect.UI/ViewModels/ConnectionManagerViewModel.cs`
- `src/VisualSqlArchitect.UI/MainWindow.axaml.cs`

**Guia de Implementação:**
1.  **Abstração de Secret Store:**
    - Criar interface para leitura/escrita segura de segredos.
2.  **Integração por SO:**
    - Implementar adaptadores para Linux/Windows/macOS.
3.  **Migração de Dados Antigos:**
    - Migrar credenciais existentes para armazenamento seguro.
4.  **Sanitização de Saída:**
    - Proteger logs, mensagens e diagnósticos contra exposição sensível.
5.  **Fluxo de Rotação:**
    - Permitir atualizar credenciais sem recriar conexão do zero.

**Critérios de Aceite:**
- Senhas não ficam persistidas em claro.
- Credenciais podem ser rotacionadas com baixo atrito.
- Nenhuma informação sensível vaza em logs/erros.

**Implementação realizada (31/03/2026):**
- ✅ Vault local dedicado para segredos: `credentials.vault.json`
- ✅ `connections.json` persistido sem senha inline
- ✅ Proteção por SO:
    - Windows: DPAPI (`DataProtectionScope.CurrentUser`)
    - Linux/macOS: AES-256-GCM com segredo de instalação
- ✅ Migração automática de perfis legados (plaintext/`enc:`) para vault
- ✅ Rotação transparente ao salvar perfil (mesmo `profileId` atualiza segredo)
- ✅ Remoção de segredo ao excluir perfil

---

### ~~Tarefa 42: Pipeline de Release com Artefatos Multiplataforma~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Sem pipeline de release padronizado, distribuição do aplicativo é manual, inconsistente e sujeita a erro.

**Comportamento Esperado:**
- Gerar artefatos de release para Linux, Windows e macOS.
- Aplicar versionamento semântico e changelog automatizado.
- Publicar artefatos com validação de build e testes mínimos.

**Arquivos Relevantes:**
- `files.sln`
- `src/VisualSqlArchitect.UI/VisualSqlArchitect.UI.csproj`
- `.github/workflows/*` (ou pipeline equivalente)
- `README.md`

**Guia de Implementação:**
1.  **Matriz de Build:**
    - Configurar build por sistema operacional e arquitetura alvo.
2.  **Versionamento Semântico:**
    - Automatizar incremento e marcação de versão de release.
3.  **Changelog Automático:**
    - Gerar changelog a partir de commits/PRs.
4.  **Assinatura/Empacotamento:**
    - Preparar formato de distribuição e assinatura quando aplicável.
5.  **Gate de Qualidade:**
    - Bloquear release com testes críticos falhando.

**Critérios de Aceite:**
- Releases saem de forma repetível e auditável.
- Artefatos funcionam nas plataformas suportadas.
- Processo de entrega reduz passos manuais.

**Implementação realizada (31/03/2026):**
- ✅ Workflow de release dedicado em `.github/workflows/release.yml`
- ✅ Build em matriz para múltiplos targets:
    - `linux-x64`, `linux-arm64`, `win-x64`, `osx-x64`, `osx-arm64`
- ✅ Publicação de binários self-contained/single-file por runtime
- ✅ Upload de artefatos por target e agregação final
- ✅ Criação automática de GitHub Release com `generate_release_notes: true`
- ✅ Gate de qualidade no fluxo CI (`.github/workflows/ci.yml`) com restore/build/test

**Observação:**
- O fluxo atual suporta tag manual (`push tags` / `workflow_dispatch`).
- Incremento semântico totalmente automático pode ser evoluído depois sem bloquear o critério principal da tarefa.

---

### ~~Tarefa 44: Internacionalização (i18n) e Externalização de Strings~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Strings hardcoded na UI dificultam manutenção e impedem suporte oficial a múltiplos idiomas.

**Comportamento Esperado:**
- Textos da interface devem ser extraídos para recursos localizáveis.
- A aplicação deve suportar ao menos `pt-BR` e `en-US`.
- Mudança de idioma deve refletir na UI sem recompilar aplicação.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/**/*.axaml`
- `src/VisualSqlArchitect.UI/**/*.cs`
- `src/VisualSqlArchitect.UI/Assets/` (arquivos de recursos de idioma)

**Guia de Implementação:**
1.  **Inventário de Strings:**
    - Identificar e extrair textos fixos da UI.
2.  **Camada de Localização:**
    - Definir mecanismo de lookup por chave.
3.  **Bundles de Idioma:**
    - Criar recursos para `pt-BR` e `en-US`.
4.  **Troca de Idioma em Runtime:**
    - Permitir alteração de cultura via configuração/menu.
5.  **Fallback de Chaves:**
    - Definir idioma padrão quando tradução não existir.

**Critérios de Aceite:**
- UI pode alternar entre idiomas suportados.
- Não há textos críticos hardcoded restantes.
- Falta de tradução não quebra renderização.

**Implementação realizada (31/03/2026):**
- ✅ Serviço central de localização com troca em runtime: `LocalizationService`
- ✅ Idiomas suportados: `pt-BR` e `en-US`
- ✅ Recursos externalizados em JSON:
    - `src/VisualSqlArchitect.UI/Assets/Localization/pt-BR.json`
    - `src/VisualSqlArchitect.UI/Assets/Localization/en-US.json`
- ✅ Assets de localização copiados para output no build (sem recompilar para trocar idioma)
- ✅ Toggle de idioma na UI principal (`LanguageToggleBtn`)
- ✅ Textos críticos externalizados no shell principal e no Connection Manager
- ✅ Mensagens operacionais do `ConnectionManagerViewModel` adaptadas para i18n
- ✅ Testes unitários para troca de cultura e alternância de idioma

---

### ~~Tarefa 45: Modo de Benchmark de Query~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Sem medições consistentes, comparar alternativas de modelagem SQL no canvas fica subjetivo e sem base de desempenho.

**Comportamento Esperado:**
- Usuário deve executar benchmark com múltiplas iterações da query.
- Resultado deve incluir métricas como média, mediana e percentis (ex: p95).
- Deve ser possível comparar execuções entre versões do fluxo.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/QueryEngine/QueryBuilderService.cs`
- `src/VisualSqlArchitect.UI/ViewModels/LiveSqlBarViewModel.cs`
- `src/VisualSqlArchitect.UI/Controls/LiveSqlBar.axaml`

**Guia de Implementação:**
1.  **Runner de Benchmark:**
    - Executar N iterações com warm-up configurável.
2.  **Coleta de Métricas:**
    - Registrar latências e calcular estatísticas principais.
3.  **Visualização de Resultado:**
    - Exibir tabela/resumo comparável entre execuções.
4.  **Persistência de Históricos:**
    - Salvar resultados para análise evolutiva.
5.  **Controle de Ruído:**
    - Permitir configurações para reduzir variabilidade (ex: intervalo entre execuções).

**Critérios de Aceite:**
- Benchmark produz métricas reprodutíveis e úteis.
- Usuário compara variações de query com base em dados.
- Modo benchmark não interfere no fluxo normal de preview.

---

### Tarefa 46: Sistema de Plugins para Nós Customizados

**Descrição do Problema:**
A evolução de novos tipos de nó depende de alterações diretas no core, limitando extensibilidade e aumentando acoplamento.

**Comportamento Esperado:**
- Deve ser possível carregar nós customizados via plugins externos.
- Plugins devem registrar metadados, pinos, parâmetros e comportamento de compilação.
- Falha de plugin não deve derrubar aplicação inteira.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/Nodes/NodeDefinition.cs`
- `src/VisualSqlArchitect/Registry/SqlFunctionRegistry.cs`
- `src/VisualSqlArchitect/ServiceRegistration.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`

**Guia de Implementação:**
1.  **Contrato de Plugin:**
    - Definir interface estável para descoberta e registro de nós.
2.  **Loader Dinâmico:**
    - Implementar carregamento de assemblies/plugins em pasta definida.
3.  **Sandbox e Isolamento:**
    - Tratar falhas com isolamento e mensagens de diagnóstico.
4.  **Registro na UI:**
    - Integrar nós plugin no menu de busca/adicionar nó.
5.  **Versionamento de API:**
    - Definir compatibilidade mínima de versão para plugins.

**Critérios de Aceite:**
- Plugins podem adicionar nós sem alterar core.
- Falhas de plugin não impedem startup da aplicação.
- Nós plugin participam normalmente de validação e compilação.

---

### ~~Tarefa 56: Centro de Diagnósticos do App (Self-check)~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Atualmente, quando algo falha (conexão, assets, plugins, configuração), o diagnóstico fica disperso em telas e logs. Isso aumenta tempo de suporte e dificulta identificar causa raiz rapidamente.

**Comportamento Esperado:**
- A aplicação deve ter uma tela única de diagnósticos com health-check geral.
- O centro deve validar: conexão ativa, acesso a metadados, recursos visuais críticos, plugins carregados e configurações essenciais.
- Cada item deve mostrar status (`OK`, `Warning`, `Error`) com ação recomendada.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/MainWindow.axaml`
- `src/VisualSqlArchitect.UI/MainWindow.axaml.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect/ServiceRegistration.cs`

**Guia de Implementação:**
1.  **Modelo de Diagnóstico:**
    - Criar estrutura com `Name`, `Status`, `Details`, `Recommendation` e `LastCheckAt`.
2.  **Checklist de Verificação:**
    - Validar subsistemas críticos (DB, metadata, ícones, serialização, plugins).
3.  **Execução Manual e Automática:**
    - Permitir rodar check sob demanda e opcionalmente no startup.
4.  **Ações de Recuperação:**
    - Incluir atalhos para correção (retestar conexão, recarregar plugins, abrir config).
5.  **Exportar Resultado:**
    - Permitir copiar/exportar diagnóstico para suporte técnico.

**Critérios de Aceite:**
- Usuário identifica rapidamente componentes com falha.
- Cada erro apresenta sugestão clara de correção.
- Diagnóstico pode ser compartilhado sem expor dados sensíveis.

---

### ~~Tarefa 57: Novo Nó Atômico de Regex (Regex Match/Extract/Replace)~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Falta um nó nativo para operações de regex, o que força uso de SQL raw ou cadeias complexas de transformação para validação, extração e substituição textual.

**Comportamento Esperado:**
- Deve existir um nó `Regex` no menu de adicionar nó.
- O nó deve suportar ao menos modos:
  - `Match` (retorna booleano)
  - `Extract` (retorna grupo/captura)
  - `Replace` (retorna texto transformado)
- O nó deve aceitar parâmetros configuráveis: `Pattern`, `Flags`, `Replacement` (quando aplicável).

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/Nodes/NodeDefinition.cs`
- `src/VisualSqlArchitect/Nodes/NodeGraphCompiler.cs`
- `src/VisualSqlArchitect/Registry/SqlFunctionRegistry.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`

**Guia de Implementação:**
1.  **Definir NodeDefinition:**
    - Criar entrada de nó com pino de entrada textual e saída tipada por modo.
2.  **Parâmetros do Nó:**
    - Adicionar parâmetros para pattern, flags e replacement.
3.  **Compilação por Provedor:**
    - Mapear funções equivalentes de regex por banco (Postgres/MySQL/SQL Server), com fallback quando não suportado.
4.  **Validação de Configuração:**
    - Bloquear execução se pattern estiver vazio ou inválido.
5.  **Integração de UI:**
    - Exibir opções de modo e ajuda rápida com exemplos de regex.

**Critérios de Aceite:**
- Usuário consegue usar regex sem recorrer a SQL manual.
- SQL gerada respeita capacidade do provedor ativo.
- Erros de configuração de regex são informados antes da execução.

---

### ~~Tarefa 58: Novo Nó Atômico de Transformação de Valores~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Não há um nó dedicado para transformações condicionais de valor (mapeamentos, normalização, preenchimento default), exigindo lógica repetitiva e menos visual no fluxo.

**Comportamento Esperado:**
- Deve existir um nó `Value Transform` no menu de nós.
- O nó deve permitir operações comuns:
  - `Map` (valor origem -> valor destino)
  - `DefaultIfNull/Empty`
  - `Normalize` (trim, upper/lower, replace simples)
- Deve suportar múltiplas regras configuráveis em ordem.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/Nodes/NodeDefinition.cs`
- `src/VisualSqlArchitect/Nodes/NodeGraphCompiler.cs`
- `src/VisualSqlArchitect/Registry/SqlFunctionRegistry.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`

**Guia de Implementação:**
1.  **Definir Estrutura do Nó:**
    - Pino de entrada (valor), pino de saída (valor transformado) e coleção de regras.
2.  **Editor de Regras na UI:**
    - Permitir adicionar/remover/reordenar regras de transformação.
3.  **Compilação SQL:**
    - Traduzir regras para `CASE WHEN`, `COALESCE`, funções de string e operadores equivalentes.
4.  **Compatibilidade por Provedor:**
    - Adaptar geração conforme disponibilidade de funções por banco.
5.  **Validação e Preview:**
    - Validar regras inválidas e mostrar preview textual da transformação resultante.

**Critérios de Aceite:**
- Transformações de valor comuns são configuráveis sem SQL manual.
- Nó gera SQL correta e portável nos provedores suportados.
- Regras podem ser alteradas visualmente sem quebrar o fluxo.

---

### ~~Tarefa 59: Nó de Saída de Resultado com Seleção Final, Alias Encadeável e Ordenação Drag-and-Drop~~ ✅ CONCLUÍDA

**Descrição do Problema:**
Atualmente falta um nó de saída dedicado para controlar explicitamente o resultado final do SELECT. Também falta uma forma clara de aplicar alias de campo imediatamente antes da saída e de ordenar visualmente a sequência final das colunas exibidas.

**Comportamento Esperado:**
- Deve existir um nó final chamado `Result Output` (ou equivalente) para materializar a projeção final da query.
- O `Result Output` deve aceitar conexão apenas de nós válidos de tabela/projeção e não deve aceitar conexão originada de busca de subconsultas quando não houver suporte semântico.
- Deve existir um nó de alias de campo encadeável antes do `Result Output`, permitindo renomear o nome final da coluna no SQL gerado.
- Dentro do `Result Output`, as colunas finais devem ser exibidas em lista e permitir reordenação por drag-and-drop (estilo kanban), refletindo a ordem do SELECT.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/Nodes/NodeDefinition.cs`
- `src/VisualSqlArchitect/Nodes/NodeGraphCompiler.cs`
- `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`
- `src/VisualSqlArchitect.UI/Controls/NodeControl.axaml`
- `src/VisualSqlArchitect.UI/Controls/NodeControl.axaml.cs`

**Guia de Implementação:**
1.  **Criar Nó de Saída Final:**
    - Definir novo tipo de nó `Result Output` com responsabilidade explícita de projeção final.
    - Estruturar pinos de entrada para receber campos/colunas provenientes de nós de tabela/projeção.
2.  **Restrições de Conexão:**
    - Bloquear conexões incompatíveis no nível de validação de pino/tipo.
    - Impedir conexão direta de origem subquery-search quando não houver suporte no compilador.
    - Exibir mensagem de erro amigável explicando o motivo do bloqueio.
3.  **Nó de Alias de Campo (Pré-saída):**
    - Permitir conectar nó de alias imediatamente antes do `Result Output`.
    - Garantir que alias aplicado reflita no nome final da coluna no SELECT gerado.
4.  **Editor de Colunas no Nó de Saída:**
    - Exibir lista de colunas conectadas no `Result Output`.
    - Implementar reorder por drag-and-drop entre itens da lista.
    - Persistir a ordem definida no estado do canvas/serialização.
5.  **Compilação SQL Final:**
    - Gerar cláusula SELECT respeitando:
      - ordem definida por drag-and-drop;
      - aliases aplicados no nó de alias;
      - exclusão de entradas inválidas/desconectadas.
6.  **Validação e UX:**
    - Sinalizar quando `Result Output` não tiver colunas válidas conectadas.
    - Mostrar preview textual da ordem final e nomes das colunas antes da execução.

**Critérios de Aceite:**
- Usuário consegue definir explicitamente o resultado final no nó de saída.
- Conexões inválidas (incluindo subconsulta sem suporte) são bloqueadas com feedback claro.
- Alias de campo antes da saída altera corretamente o nome final no SQL.
- Ordem das colunas pode ser alterada por drag-and-drop e é refletida no SELECT gerado.
- Ordem e aliases permanecem íntegros após salvar/reabrir o canvas.

---

### Tarefa 60: Nó Atômico de Exportação HTML (Base dbDumper) ✅ CONCLUÍDA

**Descrição:**
Implementar o nó `HtmlExport` que utiliza a lógica e os templates do projeto `dbDumper`. Este nó deve receber a saída do nó `Result Output`.

**Comportamento Esperado:**
- Possuir um pino de entrada que aceita a conexão vinda do `Result Output`.
- Propriedade `File Name` no painel de propriedades para definir o destino (ex: `relatorio.html`).
- Botão de ação no nó ou comando global para disparar a geração do arquivo.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/Nodes/NodeDefinition.cs`
- `src/VisualSqlArchitect.UI/ViewModels/LiveSqlBarViewModel.cs`
- Integração com a pasta `dbDumper`.

---

### Tarefa 61: Nó Atômico de Exportação JSON ✅ CONCLUÍDA

**Descrição:**
Implementar o nó `JsonExport` para converter o conjunto de dados resultante em um arquivo JSON estruturado.

**Comportamento Esperado:**
- Entrada conectada ao `Result Output`.
- Propriedade `File Name` (ex: `dados.json`).
- O arquivo deve conter um array de objetos JSON baseados nos aliases definidos no `Result Output`.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/Nodes/NodeDefinition.cs`
- `src/VisualSqlArchitect.UI/Serialization/CanvasSerializer.cs`

---

### Tarefa 62: Nó Atômico de Exportação CSV ✅ CONCLUÍDA

**Descrição:**
Implementar o nó `CsvExport` para exportação de dados tabulares simples.

**Comportamento Esperado:**
- Entrada conectada ao `Result Output`.
- Propriedade `File Name` (ex: `export.csv`).
- Parâmetro adicional para escolher o delimitador (`,` ou `;`).

**Arquivos Relevantes:**
- `src/VisualSqlArchitect/Nodes/NodeDefinition.cs`

---

### ~~Tarefa 63: Nó Atômico de Exportação Excel (XLSX)~~ ✅ CONCLUÍDA

**Descrição:**
Implementar o nó `ExcelExport` para gerar planilhas formatadas.

**Comportamento Esperado:**
- Entrada conectada ao `Result Output`.
- Propriedade `File Name` (ex: `analise.xlsx`).
- Requer a adição da biblioteca `ClosedXML` ou similar ao projeto.

**Arquivos Relevantes:**
- `src/VisualSqlArchitect.UI/VisualSqlArchitect.UI.csproj`
- `src/VisualSqlArchitect/Nodes/NodeDefinition.cs`

---

## Notas Gerais

- **Priorização por Impacto no Sistema (o que aplicar primeiro):**
    - **P0 — Crítico (estabilidade/uso imediato):** 1, 2, 4, 5, 7, 11, 29, 31, 36, 59
    - **P1 — Alto (produtividade e usabilidade):** 3, 6, 8, 9, 10, 13, 14, 16, 17, 19, 20, 22, 23, 24, 25, 26, 30, 32, 33, 34, 37, 38, 39, 40, 56, 57, 58, 60, 61, 62, 63
    - **P2 — Estratégico (escala/governança/evolução):** 15, 18, 21, 28, 42, 44, 45, 46

- **Dificuldade Estimada:**
    - **Baixa:** 2, 4, 5, 7, 14, 17, 29
    - **Média:** 1, 6, 8, 9, 10, 11, 13, 16, 19, 20, 22, 23, 24, 25, 26, 30, 31, 32, 33, 36, 57, 58, 59, 60, 63
    - **Alta:** 3, 15, 18, 21, 28, 34, 37, 38, 39, 40, 42, 44, 45, 46, 56

- **Plano Recomendo (Impacto Primeiro):**
    - **Onda 1 — Estabilização do Core (P0):** 1, 2, 4, 5, 7, 11, 29, 31, 36, 59
    - **Onda 2 — UX e Fluxo Principal (P1 de menor risco):** 8, 9, 10, 13, 14, 16, 17, 20, 30, 32, 57, 58
    - **Onda 3 — Escalabilidade Técnica (P1/P2 complexos):** 3, 15, 18, 21, 24, 25, 26, 34, 37, 38, 39, 40, 42, 44, 45, 46, 56

- **Critério de Desempate para execução:** quando duas tarefas tiverem mesma prioridade, executar primeiro a de menor dificuldade e maior dependência para outras tarefas.
- **Testes:** Após implementar cada tarefa, realize testes de integração para garantir que não haja regressões.
- **Documentação de Código:** Adicione comentários de código explicando a lógica, especialmente em problemas complexos como o das conexões de fios e compilação SQL.
- **Atalhos de Teclado:** Mantenha atalhos de teclado consistentes (ex: Shift+A para adicionar nó, Esc para fechar, Ctrl+S para salvar).

---

## 🤖 GUIA RÁPIDO DE EXECUÇÃO (ATUALIZADO)

### **CHECKLIST DE PENDÊNCIAS REAIS**

- [x] **TAREFA 40 - Gerenciamento Seguro de Credenciais (Vault/Keychain)**
    ```bash
    # Criar abstração de secret store e integração por SO
    # Migrar segredos existentes e sanitizar logs
    ```

- [x] **TAREFA 42 - Pipeline de Release com Artefatos Multiplataforma**
    ```bash
    # Configurar workflow com matriz Linux/Windows/macOS
    # Gate de qualidade: build + testes antes de release
    ```

- [x] **TAREFA 44 - Internacionalização (i18n) e Externalização de Strings**
    ```bash
    # Extrair strings hardcoded da UI
    # Criar bundles pt-BR e en-US com fallback
    ```

- [ ] **TAREFA 46 - Sistema de Plugins para Nós Customizados**
    ```bash
    # Definir contrato de plugin + loader dinâmico
    # Isolar falhas e integrar descoberta na UI
    ```

- [x] **Validação técnica da base atual**
    ```bash
    # dotnet build  -> OK
    # dotnet test   -> 467/467 passing
    ```

---

### **RESUMO DAS PRIORIDADES POR SEVERIDADE**

#### 🔴 **CRÍTICO (Pendências de arquitetura/plataforma)**
1. **Tarefa 46**: Plugins para nós customizados

#### 🟠 **ALTO (já concluído na base atual)**
- Tarefas 1, 11, 29, 31, 36, 57, 58, 59 e demais marcadas como ✅ CONCLUÍDA neste documento.

#### 🟡 **MÉDIO (UX/Produtividade - Próx semana)**
- Tarefa 8: Redimensionamento de Sidebars
- Tarefa 13: Snap e Alinhamento
- Tarefa 14: Acessibilidade
- Tarefa 20: Pré-visualização de Dados
- Tarefa 25: Sistema de Snippets

#### 🟢 **BAIXO (Estratégico - Roadmap futuro)**
- Tarefa 15: Undo/Redo avançado
- Tarefa 18: Otimização de Performance
- Tarefa 42: Release Pipeline
- Tarefa 44: Internacionalização
- Tarefa 46: Sistema de Plugins

---

### **MATRIZ DE DECISÃO - Qual Tarefa Fazer Agora?**

```
┌─────────────────────────────────────────────────────────┐
│ Segurança e compliance primeiro?                        │
├─────────────────────────────────────────────────────────┤
│ SIM → Fazer Tarefa 40 (Credenciais Seguras)             │
└─────────────────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────────────────┐
│ Precisa padronizar entrega/release?                     │
├─────────────────────────────────────────────────────────┤
│ SIM → Fazer Tarefa 42 (Pipeline Multiplataforma)        │
└─────────────────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────────────────┐
│ Produto vai escalar para múltiplos idiomas?             │
├─────────────────────────────────────────────────────────┤
│ SIM → Fazer Tarefa 44 (i18n)                             │
└─────────────────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────────────────┐
│ Precisa extensibilidade por terceiros?                  │
├─────────────────────────────────────────────────────────┤
│ SIM → Fazer Tarefa 46 (Plugins)                          │
└─────────────────────────────────────────────────────────┘
```

---

### **CONTEXTO RÁPIDO DO PROJETO**

- **Tipo**: Arquiteto Visual para SQL (drag-and-drop query builder)
- **Tech Stack**: C# + Avalonia (UI multiplataforma)
- **Bancos Suportados**: PostgreSQL, MySQL, SQL Server
- **Estrutura**:
  - Core lógica: `/src/VisualSqlArchitect/`
  - UI: `/src/VisualSqlArchitect.UI/` (Avalonia)
  - Testes: `/tests/VisualSqlArchitect.Tests/`

---

### **CONTATOS RÁPIDOS PARA DEBUG**

| Problema | Solução Rápida |
|----------|---|
| App não compila | `dotnet clean && dotnet build` |
| Wire não aparece | Verificar `BezierWireLayer.cs` + logs |
| Nós não se movem | Verificar `InfiniteCanvas.cs` OnNodeDragDelta() |
| Crash no startup | Verificar `App.axaml.cs` ServiceRegistration |
| SQL gerada errada | Verificar `NodeGraphCompiler.cs` + `QueryBuilderService.cs` |
| Teste falhando | `dotnet test --verbosity detailed --no-restore` |

---

### **PRÓXIMOS PASSOS (Resumo Atual)**

```
SPRINT ATUAL:
    1. Executar Tarefa 40 (Credenciais Seguras)
    2. Executar Tarefa 42 (Pipeline de Release)

SPRINT SEGUINTE:
    3. Executar Tarefa 44 (i18n)
    4. Executar Tarefa 46 (Plugins)

APÓS CADA ENTREGA:
    5. Rodar dotnet build + dotnet test
    6. Atualizar este documento e status correlatos
```

---

### **🔍 STATUS DE BUILD/TESTES (31 de março de 2026)**

**Build Status**: ✅ SUCESSO

#### Validação técnica atual:

| Verificação | Resultado |
|------------|-----------|
| `dotnet build` | ✅ Sucesso |
| `dotnet test` | ✅ 464/464 passando |
| Erro `HistoryVariant` | ✅ Resolvido |
| Warnings tratados no ciclo recente | ✅ Resolvido |

#### Observação
- O bloco de diagnóstico de 27/03 foi mantido aqui apenas como histórico de incidente já resolvido.

---

### **STATUS GERAL ATUAL**

```
Situação Atual:
├─ Build: ✅ SUCESSO
├─ Testes: ✅ 464/464 passando
├─ Runtime: ✅ Estável para ciclo atual
└─ Pendências Reais: 4 tarefas (40, 42, 44, 46)

Próximas Ações Imediatas:
1. Implementar Tarefa 40
2. Implementar Tarefa 42
3. Planejar Tarefa 44
4. Planejar Tarefa 46
5. Atualizar status ao fim de cada entrega
```
