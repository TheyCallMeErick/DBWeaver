# 🔧 Debug Script para Bug #2 (Wire Sync)

## Problema
Ao mover uma coluna, o wire fica parado no meio do nada em vez de acompanhar o movimento.

## Investigação

### Passo 1: Verificar se UpdatePinPositions() está sendo chamado
Adicionar logging em InfiniteCanvas.UpdatePinPositions()

**Arquivo**: src/VisualSqlArchitect.UI/Controls/InfiniteCanvas.cs
**Função**: UpdatePinPositions()
**Linhas**: ~210-255

**Código a Adicionar**:
```csharp
private void UpdatePinPositions()
{
    if (ViewModel is null)
        return;

    System.Diagnostics.Debug.WriteLine("🔄 UpdatePinPositions called");  // ← LOG

    const double headerH = 46,
        separatorH = 1,
        pinH = 26,
        padTop = 4;
    double pinYBase = headerH + separatorH + padTop + pinH / 2.0;
    foreach (NodeViewModel node in ViewModel.Nodes)
    {
        System.Diagnostics.Debug.WriteLine($"  Processing node: {node.Name}, Position: {node.Position}");  // ← LOG

        NodeControl? nc = _scene
            .Children.OfType<NodeControl>()
            .FirstOrDefault(c => c.DataContext == node);

        if (nc is not null && nc.Bounds.Width > 0)
        {
            System.Diagnostics.Debug.WriteLine($"    Using TranslatePoint method, NC.Bounds.Width: {nc.Bounds.Width}");  // ← LOG
            // ... resto do código
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"    Using Geometric fallback, NC.Bounds.Width: {nc?.Bounds.Width ?? 0}");  // ← LOG
            // ... resto do código
        }
    }
}
```

### Passo 2: Verificar se SyncWires() está sendo chamado durante drag
Adicionar logging em SyncWires()

```csharp
private void SyncWires()
{
    if (ViewModel is null)
        return;

    System.Diagnostics.Debug.WriteLine("🔗 SyncWires called");  // ← LOG

    UpdatePinPositions();
    foreach (ConnectionViewModel c in ViewModel.Connections)
    {
        System.Diagnostics.Debug.WriteLine($"  Wire: {c.FromPin.Name} → {c.ToPin?.Name}, FromPoint: {c.FromPoint}");  // ← LOG
        c.FromPoint = c.FromPin.AbsolutePosition;
        if (c.ToPin is not null)
            c.ToPoint = c.ToPin.AbsolutePosition;
        System.Diagnostics.Debug.WriteLine($"    Updated to FromPoint: {c.FromPin.AbsolutePosition}");  // ← LOG
    }
    _wires.Connections = ViewModel.Connections.ToList();
    _wires.PendingConnection = _pinDrag?.LiveWire;
    _wires.InvalidateVisual();
}
```

### Passo 3: Verificar se BezierWireLayer.Render() está sendo chamado

**Arquivo**: src/VisualSqlArchitect.UI/Controls/BezierWireLayer.cs
**Função**: Render()

```csharp
public override void Render(DrawingContext dc)
{
    System.Diagnostics.Debug.WriteLine($"🎨 BezierWireLayer.Render called with {Connections.Count} connections");  // ← LOG

    foreach (ConnectionViewModel conn in Connections)
    {
        System.Diagnostics.Debug.WriteLine($"  Drawing wire from {conn.FromPoint} to {conn.ToPoint}");  // ← LOG
        DrawWire(dc, conn);
    }

    if (PendingConnection is not null)
        DrawWireDragging(dc, PendingConnection);
}
```

## Como Usar

1. Adicionar os `System.Diagnostics.Debug.WriteLine()` no código
2. Abrir Debug Output em VS Code/Visual Studio
3. Criar um projeto com 2 tabelas conectadas
4. Mover uma tabela
5. Observar output de debug
6. Verificar:
   - ✅ UpdatePinPositions() é chamado?
   - ✅ SyncWires() é chamado?
   - ✅ UpdatePinPositions() usa TranslatePoint ou Fallback?
   - ✅ FromPoint/ToPoint são atualizados?
   - ✅ BezierWireLayer.Render() é chamado com coordenadas corretas?

## Saída Esperada Durante Drag

```
🔄 UpdatePinPositions called
  Processing node: Table1, Position: (100, 50)
    Using TranslatePoint method, NC.Bounds.Width: 220
  Processing node: Table2, Position: (400, 50)
    Using TranslatePoint method, NC.Bounds.Width: 220
🔗 SyncWires called
  Wire: Table1.id → Table2.fk_id, FromPoint: (320, 70)
    Updated to FromPoint: (320, 70)
🎨 BezierWireLayer.Render called with 1 connections
  Drawing wire from (320, 70) to (400, 75)
```

Se alguma dessas chamadas NÃO aparecer durante drag, encontramos o culpado!
