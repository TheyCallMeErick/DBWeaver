# ✅ Checklist - Eixo 8 Fase 2: Teste e Validação

**Responsável**: Próximo desenvolvedor  
**Status**: 🚀 PRONTO PARA TESTE MANUAL  
**Data de Início**: 26 de março de 2026

---

## 📋 Pre-requisitos

- [ ] Código compilado: `dotnet build` ✅
- [ ] Sem erros de compilação: 0 errors ✅
- [ ] Ter VS Code ou Visual Studio com Debug Output aberto
- [ ] Ter um teste project (2 tabelas, 1 wire conectada)

---

## 🧪 FASE 2: Teste Manual do Bug #2 (Wire Sync)

### Parte 1: Setup Inicial

```powershell
# Terminal 1: Compilar
cd c:\Users\azeve\Documents\VisualSqlArchtect
dotnet build
# Deve terminar com: "Build succeeded"

# Terminal 2: Executar
dotnet run
# App deve abrir com UI Avalonia
```

### Parte 2: Preparar Teste

**No VisualSqlArchitect.UI**:
1. [ ] Clique em "New Project"
2. [ ] Selecione um banco de dados (PostgreSQL, MySQL, ou SQL Server)
3. [ ] Espere carregar tabelas
4. [ ] Selecione 2 tabelas (ex: users e orders)
5. [ ] Clique em cada uma para adicionar ao canvas
6. [ ] Conecte com um wire: clique no pin de uma tabela → arraste → solte no pin da outra

**Resultado Esperado**: Canvas mostra 2 tabelas com 1 wire conectando-as

### Parte 3: Teste MMB Pan (Bug #1) - ✅ ESPERADO FUNCIONAR

```
1. Pressione botão do meio do mouse no canvas (vazio)
2. Arraste o mouse enquanto segura MMB
3. ESPERADO: Canvas se move suavemente na direção oposta ao arrasto
4. Solte MMB
5. RESULTADO: ✅ Pan completo, canvas está em nova posição
```

**Se não funcionar**: Reportar issue

### Parte 4: Teste Wire Sync (Bug #2) - EM INVESTIGAÇÃO

```
1. Abra Debug Output (View → Output → Choose "Omnisharp" or terminal)
2. Você deve ver a janela de debug messages
3. Clique em uma das tabelas no canvas
4. Arraste-a para outro local no canvas
5. OBSERVE Debug Output enquanto arrasta
```

**ESPERADO durante drag** (veja [DEBUG_BUG2.md](./DEBUG_BUG2.md)):
```
🔄 UpdatePinPositions called
  Node: users, Position: (200, 150)
    TranslatePoint mode, NC.Bounds.Width: 220
🔗 SyncWires called
  Wire: users.id → orders.user_id, FromPoint: (420, 165)
  Wire: users.id → orders.user_id, FromPoint: (420, 165)
🎨 BezierWireLayer.Render called with 1 connections
  Drawing wire from (420, 165) to (450, 180)
```

### Parte 5: Interpretação de Resultados

#### Cenário A: Logs APARECEM, Wire se MOVE ✅
```
✅ BUG #2 RESOLVIDO
- Tudo funcionando normalmente
- Remover Debug.WriteLine()
- Commit final
```

#### Cenário B: Logs APARECEM, Wire NÃO SE MOVE ❌
```
⚠️ PROBLEMA DE RENDERIZAÇÃO
- UpdatePinPositions() SIM é chamado
- Coordenadas MUDAM (veja FromPoint)
- MAS wire não se move visualmente
→ Investigar: BezierWireLayer.Render() está sendo chamado?
→ Investigar: Transformação de coordenadas está errada?
→ Adicionar logging de coordenadas de screen vs canvas
```

#### Cenário C: Logs NÃO APARECEM ❌
```
⚠️ PROBLEMA DE EVENT FLOW
- Fluxo de eventos não está funcionando
- OnNodeDragDelta() não está sendo acionado
- OU SyncWires() não é chamado
→ Investigar: NodeControl.OnMoved() está acionando evento?
→ Investigar: InfiniteCanvas.OnNodeDragDelta() existe?
→ Adicionar logging em OnNodeDragDelta()
```

#### Cenário D: Logs APARECEM COM COORDENADAS IGUAIS ❌
```
⚠️ PROBLEMA EM UpdatePinPositions()
- UpdatePinPositions() SIM é chamado
- MAS coordenadas não mudam (mesmo FromPoint)
- Significa pins não estão sendo repositionados
→ Investigar: TranslatePoint() está retornando null?
→ Investigar: Fallback geométrico está ativo (NC.Bounds.Width == 0)?
→ Verificar: Para cada pin: pvm.AbsolutePosition está sendo atualizado?
```

---

## 🔧 Correção Baseado em Resultado

### Se Cenário B (Renderização):
```csharp
// Adicionar logging em BezierWireLayer
public override void Render(DrawingContext dc)
{
    System.Diagnostics.Debug.WriteLine(
        $"BezierWireLayer.Render: {Connections.Count} connections, " +
        $"FirstWire From={Connections.FirstOrDefault()?.FromPoint}");
    // ... resto do código
}
```

### Se Cenário C (Event Flow):
```csharp
// Adicionar logging em OnNodeDragDelta
private void OnNodeDragDelta(object? s, (NodeViewModel Node, Point Pos) a)
{
    System.Diagnostics.Debug.WriteLine($"OnNodeDragDelta: {a.Node.Title}, newPos: {a.Pos}");
    // ... resto do código
}
```

### Se Cenário D (UpdatePinPositions):
```csharp
// Adicionar logging em UpdatePinPositions
if (inScene.HasValue)
{
    System.Diagnostics.Debug.WriteLine($"    Pin: {pvm.Name} updated to {inScene.Value}");
    pvm.AbsolutePosition = inScene.Value;
}
else
{
    System.Diagnostics.Debug.WriteLine($"    Pin: {pvm.Name} - TranslatePoint returned null!");
}
```

---

## ✅ Após Identificar Root Cause

### 1. Remover Todos os Debug.WriteLine()

Procure por:
```csharp
System.Diagnostics.Debug.WriteLine(
```

Remova todas as linhas (não remova as linhas de código antes/depois)

**Arquivos para limpar**:
- `InfiniteCanvas.cs` - ~15 linhas
- `BezierWireLayer.cs` - ~5 linhas

### 2. Implementar Correção

Baseado na root cause identificada

### 3. Testar Novamente SEM Debug Output

Deve funcionar silenciosamente

### 4. Testes Unitários

```powershell
dotnet test
```

Deve manter: 192+ tests passing

### 5. Build Final

```powershell
dotnet build
```

Deve terminar com: "Build succeeded"

### 6. Git Commit

```powershell
git add .
git commit -m "fix(eixo-8): Fix wire sync on column movement

- Root cause: [descrever o que era o problema]
- Solution: [descrever a solução implementada]
- Tests: All passing (X tests)
- Performance: [benchmarks if relevant]"

git push
```

---

## 📊 Metricas de Sucesso

| Critério | Status | Checklist |
|----------|--------|-----------|
| MMB Pan funciona | ✅ Esperado | [ ] Testado |
| Wire se move com nó | ✅ Esperado | [ ] Testado |
| Debug output limpo | ✅ Após fase 2 | [ ] Removido |
| Build sem erros | ✅ Esperado | [ ] Validado |
| Testes passando | ✅ Esperado | [ ] 200+/200+ |
| Git commit | ✅ Esperado | [ ] Feito |

---

## 📞 Se Você Ficar Preso

### Problema: "Não vejo nenhum debug output"

**Solução**:
1. Em VS Code: View → Output → Selecione "Omnisharp"
2. Ou em Visual Studio: Debug → Output → "Debug"
3. Ou no terminal: Redirecionar stderr: `dotnet run 2>&1`

### Problema: "App não inicia"

**Solução**:
1. Executar: `dotnet clean && dotnet build`
2. Verificar erros de compilação
3. Verificar dependências: `dotnet restore`

### Problema: "Wire não aparece no canvas"

**Solução**:
1. Verificar que 2 tabelas foram adicionadas
2. Verificar que pins estão visíveis
3. Tentar conectar novamente: clique pin → arraste → solte

### Problema: "Coordenadas não mudam no debug"

**Solução**:
1. Verificar que está realmente arrastando (não só passando mouse)
2. Verificar que NodeDragDelta está sendo acionado
3. Confirmar que mouse foi capturado: cursor muda?

---

## 🎯 Checklist Final

```
ANTES DE FAZER COMMIT:

[ ] Ambos bugs testados manualmente
[ ] MMB Pan funciona sem lag
[ ] Wire acompanha movimento de nó
[ ] Debug output está LIMPO (sem Debug.WriteLine)
[ ] dotnet build executa com 0 erros
[ ] dotnet test executa com 200+/200+ passing
[ ] Nenhum novo warning introduzido
[ ] Git status limpo (sem arquivos não rastreados)
[ ] Commit message é claro e descritivo
[ ] git push executado com sucesso
```

---

**Criado**: 26 de março de 2026  
**Status**: 🚀 PRONTO PARA TESTE  
**Responsável**: Próximo Dev  
**Tempo Estimado**: 45-60 min

🚀 **BOA SORTE!**
