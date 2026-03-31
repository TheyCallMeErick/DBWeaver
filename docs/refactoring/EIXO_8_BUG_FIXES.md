# Eixo 8: Bug Fixes 🐛

**Status**: 🔄 **EM PROGRESSO**

---

## 📋 Bugs Identificados

### 🐛 BUG #1: Middle Mouse Button Pan não funciona
**Severidade**: 🟡 Média
**Afeta**: UI Canvas Navigation
**Descrição**: Usuário não consegue se mover pelo canvas utilizando o botão do meio do mouse + arrastar

**Comportamento Esperado**:
```
1. Usuário pressiona botão do meio do mouse (MMB) no canvas
2. Usuário arrasta o mouse
3. Canvas move/faz pan suavemente para a direção do arrasto
4. Ao soltar MMB, pan termina
```

**Comportamento Atual**:
```
❌ Botão do meio do mouse não dispara evento de pan
❌ Canvas permanece estático
❌ Nenhuma movimentação visual
```

**Arquivos Envolvidos**:
- [src/VisualSqlArchitect.UI/Controls/InfiniteCanvas.cs](../../../src/VisualSqlArchitect.UI/Controls/InfiniteCanvas.cs)
- [src/VisualSqlArchitect.UI/Controls/PinDragInteraction.cs](../../../src/VisualSqlArchitect.UI/Controls/PinDragInteraction.cs)

**Investigação Necessária**:
- [ ] Verificar se MMB events estão sendo capturados
- [ ] Verificar handlers de PointerPressed/PointerMoved/PointerReleased
- [ ] Validar logic de pan no InfiniteCanvas
- [ ] Testar com diferentes configurações de mouse

---

### 🐛 BUG #2: Wire não acompanha coluna ao se mover
**Severidade**: 🔴 Alta
**Afeta**: Visual Feedback, Node Connections
**Descrição**: Na column list, o wire fica em posição fixa. Ao mover a coluna, o wire fica parado no meio do nada

**Comportamento Esperado**:
```
1. Usuário seleciona uma coluna e a move no canvas
2. Todos os wires conectados à coluna se movem junto
3. Wires permanecem conectados aos pontos corretos (pins)
4. Visual feedback claro da conexão durante e após movimento
```

**Comportamento Atual**:
```
❌ Ao mover coluna, wires não acompanham
❌ Wires permanecem em posição antiga (fixa)
❌ Coluna se move, wire fica "flutuando" desconectado
❌ Estado visual inconsistente
```

**Arquivos Envolvidos**:
- [src/VisualSqlArchitect.UI/Controls/BezierWireLayer.cs](../../../src/VisualSqlArchitect.UI/Controls/BezierWireLayer.cs)
- [src/VisualSqlArchitect.UI/Controls/NodeControl.axaml.cs](../../../src/VisualSqlArchitect.UI/Controls/NodeControl.axaml.cs)
- [src/VisualSqlArchitect.UI/Controls/PinDragInteraction.cs](../../../src/VisualSqlArchitect.UI/Controls/PinDragInteraction.cs)

**Root Cause Potencial**:
- Wires armazenam posições absolutas em vez de relativas aos pins
- Evento de movimento de coluna não notifica camada de wires
- Lógica de atualização de wire posição está desacoplada

**Investigação Necessária**:
- [ ] Verificar como pins registram suas posições (absolutas vs relativas)
- [ ] Verificar se NodeControl notifica subscribers quando se move
- [ ] Analisar lógica de desenho de wires no BezierWireLayer
- [ ] Implementar update loop ou event system para sincronização

---

## 🔍 Análise Técnica

### Arquitetura de Interaction Atual

```
InfiniteCanvas (Main Canvas)
├── NodeControl (Colunas/Nós)
│   ├── PinDragInteraction (Arrastar nós)
│   └── Pins (Pontos de conexão)
├── BezierWireLayer (Desenho de fios/conexões)
└── AlignGuidesLayer (Guias de alinhamento)
```

### Fluxo de Eventos (Esperado)

```
Mouse Event
    ↓
InfiniteCanvas.OnPointerEvent()
    ├─ MiddleMouseButton? → Pan Canvas
    └─ LeftMouseButton? → Drag Node
          ↓
      NodeControl.OnDragStart()
          ↓
      PinDragInteraction.UpdateNodePosition()
          ↓
      BezierWireLayer.InvalidateVisual() ← AQUI PODE ESTAR FALHANDO
          ↓
      Wires recalculam posições
```

### Problemas Identificados

**Para Bug #1 (Middle Mouse Pan)**:
- Possível: Handler MMB não registrado
- Possível: Evento consumido por outro handler antes
- Possível: Lógica de pan comentada/desabilitada

**Para Bug #2 (Wire Sync)**:
- Possível: Invalidate não está sendo chamado
- Possível: BezierWireLayer não recebe notificação de movimento
- Possível: Coordenadas de pin são estáticas (calculadas uma vez, não atualizadas)

---

## ✅ Plano de Correção

### Fase 1: Diagnóstico
- [ ] Adicionar logging a eventos de mouse
- [ ] Verificar se handlers estão sendo invocados
- [ ] Inspecionar estado de pins durante drag

### Fase 2: Correção MMB Pan (Bug #1)
1. Verificar PinDragInteraction para captura de MMB
2. Implementar handler de pan se não existir
3. Testar movimento com MMB

### Fase 3: Correção Wire Sync (Bug #2)
1. Modificar NodeControl para notificar BezierWireLayer
2. Implementar sistema de atualização de coordenadas de pin
3. Adicionar InvalidateVisual() no loop de drag
4. Testar movimento de coluna com wires conectadas

### Fase 4: Validação
- [ ] Testar ambos bugs em cenários reais
- [ ] Verificar performance (muitos nós + wires)
- [ ] Validar em diferentes resoluções/zoom levels

---

## 📊 Checklist de Tarefas

### Bug #1: Middle Mouse Pan
- [ ] Investigar InfiniteCanvas.cs - Handlers de mouse
- [ ] Verificar PinDragInteraction.cs - Captura de eventos
- [ ] Implementar/Ativar handler MMB
- [ ] Testar pan básico
- [ ] Testar pan com diferentes velocidades/distâncias
- [ ] Testar pan em diferentes zoom levels

### Bug #2: Wire Sync
- [ ] Investigar BezierWireLayer.cs - Cálculo de posições
- [ ] Verificar NodeControl.cs - Notificação de movimento
- [ ] Implementar binding de coordenadas de pin
- [ ] Adicionar InvalidateVisual() em eventos de drag
- [ ] Testar movimento simples
- [ ] Testar movimento com múltiplos wires
- [ ] Testar performance (stress test)

### Validação Final
- [ ] Ambos bugs corrigidos
- [ ] Build sem erros
- [ ] Testes existentes passando
- [ ] Novos testes adicionados
- [ ] Documentação atualizada

---

## 🔗 Arquivos Críticos

| Arquivo | Propósito |
|---------|-----------|
| [InfiniteCanvas.cs](../../../src/VisualSqlArchitect.UI/Controls/InfiniteCanvas.cs) | Canvas infinito, navegação, zoom, pan |
| [BezierWireLayer.cs](../../../src/VisualSqlArchitect.UI/Controls/BezierWireLayer.cs) | Renderização de fios/conexões |
| [NodeControl.axaml.cs](../../../src/VisualSqlArchitect.UI/Controls/NodeControl.axaml.cs) | Nó visual (coluna) |
| [PinDragInteraction.cs](../../../src/VisualSqlArchitect.UI/Controls/PinDragInteraction.cs) | Interação de drag de nó |
| [AlignGuidesLayer.cs](../../../src/VisualSqlArchitect.UI/Controls/AlignGuidesLayer.cs) | Guias de alinhamento (possível interferência) |

---

## 📝 Notas de Desenvolvimento

### Considerações para MMB Pan
- Distinguir entre MMB pan e outros tipos de drag
- Evitar conflito com seleção de múltiplos nós
- Feedback visual claro durante pan (cursor muda?)
- Persistência de pan ao soltar MMB

### Considerações para Wire Sync
- Performance: Atualizar wires em tempo real é custoso
- Usar debouncing/throttling para muitos wires?
- Cache de cálculos de curva de Bezier?
- Considerar usar GPU/Shader para renderização de wires?

---

**Data de Criação**: 26 de março de 2026
**Status**: 🔄 Em Investigação
**Próximo Passo**: Análise detalhada de código
