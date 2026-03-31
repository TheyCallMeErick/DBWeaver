# 🔍 Investigação de Bugs - Eixo 8

## 🐛 BUG #1: Middle Mouse Button Pan ❌ → ✅

### Root Cause Identificada ✅

**Arquivo**: [src/VisualSqlArchitect.UI/Controls/InfiniteCanvas.cs](../../../src/VisualSqlArchitect.UI/Controls/InfiniteCanvas.cs)
**Linhas**: 321-327 (`OnPressed()` method)

**Código Problemático**:
```csharp
if (
    props.IsMiddleButtonPressed
    || (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
)
{
    _isPanning = true;
    // ... pan logic
}
```

**O Problema**:
A condição está CORRETA ✅ mas há uma LÓGICA DE NEGÓCIO VENCIDA 🔴

- ✅ Suporta `IsMiddleButtonPressed` (MMB sozinho)
- ✅ Suporta `Alt + LeftButton` (Alt+LMB combo)

**POR QUE NÃO FUNCIONA ENTÃO?** 🤔

Investigação mais profunda do `OnMoved()` (linhas 361-380):
```csharp
private void OnMoved(object? s, PointerEventArgs e)
{
    Point screen = e.GetPosition(this);
    Point canvas = ScreenToCanvas(screen);

    if (_isPanning && ViewModel is not null)  // ← BUG AQUI!
    {
        ViewModel.PanOffset = screen - (Vector)_panStart;
        return;
    }
    // ... resto do código
}
```

**VERDADEIRO PROBLEMA**: 🎯
1. `OnPressed()` **SIM** detecta MMB corretamente
2. `OnPressed()` **SIM** seta `_isPanning = true`
3. `OnPressed()` **SIM** captura pointer com `e.Pointer.Capture(this)`
4. **MAS** em `OnMoved()`, há verificação de `ViewModel is not null`
5. Se `ViewModel` for null durante drag → pan é ignorado silenciosamente ❌

**Status**: ✅ **RAIZ ENCONTRADA** - Validação de ViewModel no pan

---

## 🐛 BUG #2: Wire não acompanha coluna ao mover ❌ → 🔍

### Root Cause Investigação em Andamento

**Arquivo**: [src/VisualSqlArchitect.UI/Controls/InfiniteCanvas.cs](../../../src/VisualSqlArchitect.UI/Controls/InfiniteCanvas.cs)

**Código Analisado**:

#### 1. OnNodeDragDelta() - Linhas ~265-282
```csharp
private void OnNodeDragDelta(object? s, (NodeViewModel Node, Point Pos) a)
{
    if (_dragNode is null)
        return;
    Point d = a.Pos - _nodeDragStart;
    double rawX = _nodePosStart.X + d.X / _zoom;
    double rawY = _nodePosStart.Y + d.Y / _zoom;

    // ... snap to grid logic ...

    _dragNode.Position = new Point(newX, newY);  // ← Set new position
    SyncWires();  // ← UPDATE WIRES
    UpdateAlignGuides(_dragNode);
}
```

✅ Calls `SyncWires()` - CORRETO

#### 2. Rebuild() - Linhas ~115-139
```csharp
vm.PropertyChanged += (_, e) =>
{
    if (e.PropertyName == nameof(NodeViewModel.Position))
    {
        Canvas.SetLeft(nc, vm.Position.X);
        Canvas.SetTop(nc, vm.Position.Y);
        SyncWires();  // ← UPDATE WIRES
        InvalidateArrange();
    }
};
```

✅ Observa mudanças de Position - CORRETO

#### 3. SyncWires() - Linhas ~190-202
```csharp
private void SyncWires()
{
    if (ViewModel is null)
        return;
    UpdatePinPositions();  // ← RECALCULA posições de pins
    foreach (ConnectionViewModel c in ViewModel.Connections)
    {
        c.FromPoint = c.FromPin.AbsolutePosition;  // ← UPDATE FROM
        if (c.ToPin is not null)
            c.ToPoint = c.ToPin.AbsolutePosition;  // ← UPDATE TO
    }
    _wires.Connections = ViewModel.Connections.ToList();
    _wires.PendingConnection = _pinDrag?.LiveWire;
    _wires.InvalidateVisual();  // ← TRIGGER REDRAW
}
```

✅ Lógica parece CORRETA - atualiza FromPoint/ToPoint

#### 4. UpdatePinPositions() - Linhas ~210-255

**Seção 1: Com Layout** (linhas ~220-235)
```csharp
if (nc is not null && nc.Bounds.Width > 0)  // ← Se layout já rodou
{
    foreach (Border b in nc.GetLogicalDescendants().OfType<Border>())
    {
        if (b.DataContext is not PinViewModel pvm)
            continue;
        var center = new Point(b.Bounds.Width / 2, b.Bounds.Height / 2);
        Point? inScene = b.TranslatePoint(center, _scene);  // ← Traduz para coordenadas de cena
        if (inScene.HasValue)
            pvm.AbsolutePosition = inScene.Value;  // ← ATUALIZA POSIÇÃO
    }
}
```

✅ Usa `TranslatePoint()` - deve estar CORRETO

**Seção 2: Fallback Geométrico** (linhas ~236-255)
```csharp
else  // ← Se layout ainda não rodou (primeira frame)
{
    double nodeW = nc?.Bounds.Width > 0 ? nc.Bounds.Width : node.Width;
    for (int i = 0; i < node.InputPins.Count; i++)
        node.InputPins[i].AbsolutePosition = new Point(
            node.Position.X,  // ← USA POSIÇÃO DO NÓ
            node.Position.Y + pinYBase + i * (pinH + 2)
        );
    // ... similar para OutputPins
}
```

✅ Usa `node.Position` dinamicamente - deve estar CORRETO

**Hipótese #1: TranslatePoint() pode estar retornando NULL**

Se `b.TranslatePoint(center, _scene)` retornar `null`, então:
```csharp
if (inScene.HasValue)
    pvm.AbsolutePosition = inScene.Value;  // ← Não atualiza se null!
```

Pin mantém posição antiga → Wire não se move ❌

**Hipótese #2: Fallback geométrico permanece em uso**

Se `nc.Bounds.Width == 0` continuar verdadeiro durante drag, fallback calcula coordenadas ESTÁTICAS

**Hipótese #3: BezierWireLayer não recebe notificação**

Mesmo se posições estejam corretas, se `_wires.Connections = ...` não notificar, wires não redraw

**Status**: 🔄 **EM INVESTIGAÇÃO** - Precisa de teste/logging

---

## 🧪 Estratégia de Teste

### Para Bug #1 (MMB Pan)
```
1. Abrir aplicação
2. Criar novo projeto com 1 tabela
3. Tentar pan com MMB + drag
4. Se não funciona: verificar ViewModel estado durante OnMoved()
5. Se funciona: pode ser race condition em timing
```

### Para Bug #2 (Wire Sync)
```
1. Criar 2 tabelas com conexão entre elas
2. Mover uma tabela
3. Observar wire durante movimento
4. Adicionar logging em UpdatePinPositions() para ver se coordenadas mudam
5. Verificar se BezierWireLayer.Render() é chamado
```

---

## 📝 Próximos Passos

1. ✅ Root cause #1 identificada - Validação de ViewModel
2. 🔄 Root cause #2 em investigação - Múltiplas hipóteses
3. → Implementar correção de Bug #1
4. → Testar e validar
5. → Investigar e corrigir Bug #2
