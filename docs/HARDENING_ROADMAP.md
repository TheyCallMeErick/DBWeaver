# Roadmap — Hardening Futuro (Backlog)

> **Objetivo:** consolidar robustez e diagnósticos do pipeline de preview/compilação sem expandir escopo funcional de SQL.
>
> **Contexto (31/03/2026):** já existem sprints de hardening entregues para Set Operations, Subquery, Join explícito, CTEs, Window Functions, compatibilidade de pins, lógica booleana e comparações.

---

## Estado Atual (baseline)

Hardening já coberto:
- validações e warnings de `UNION/UNION ALL/INTERSECT/EXCEPT`;
- validações de `Subquery`/`SubqueryExists`;
- warnings e fallback para `Join` explícito incompleto/tipo inválido;
- diagnósticos de CTE (nome, duplicidade, ciclo, recursividade);
- validações de Window (`value`, `order`, `frame`, parâmetros numéricos);
- validação de compatibilidade de conexões (pin type mismatch) com exceções válidas conhecidas;
- warnings para `AND/OR/COMPILE WHERE` redundantes/vazios;
- validações de inputs obrigatórios em nós de comparação.

---

## Princípios de Hardening

1. **Não quebrar fluxo de preview** quando possível: preferir warning + fallback.
2. **Mensagens acionáveis**: explicar causa + correção sugerida.
3. **Escopo mínimo por sprint**: mudanças pequenas, testáveis e sem regressão.
4. **Paridade UI ↔ engine**: regras de validação no preview devem refletir regras reais da compilação.
5. **Cobertura orientada a risco**: cada regra nova deve ter ao menos 1 teste de erro e 1 de caminho feliz adjacente.

---

## Backlog Prioritário de Melhorias

## P0 — Diagnóstico e observabilidade do preview

### P0.1 — Estruturar erros/warnings por severidade e categoria

**Problema atual:** mensagens são texto livre em `List<string>`, sem metadado para UI (ex.: filtrar por tipo).

**Proposta:** introduzir modelo interno:
- `PreviewDiagnostic { Severity, Category, Code, Message, NodeId?, PinName? }`
- Severidades: `Info`, `Warning`, `Error`
- Categorias: `TypeCompatibility`, `Window`, `Cte`, `Join`, `Subquery`, `Predicate`, `Comparison`, `General`

**Checklist de aceite**
- [ ] Pipeline de preview consegue produzir diagnósticos estruturados.
- [ ] UI continua exibindo mensagens (compatibilidade mantida).
- [ ] Mapeamento legado `List<string>` preservado para não quebrar chamadas antigas.

---

### P0.2 — Padronizar códigos de erro

**Proposta:** criar códigos estáveis (ex.: `W-CMP-001`, `W-WIN-003`) para facilitar QA/regressão.

**Checklist de aceite**
- [ ] Cada warning/erro novo em hardening possui `Code`.
- [ ] Testes críticos passam a validar também `Code` (não só texto).

---

## P1 — Regras funcionais de consistência ainda faltantes

### P1.1 — Hardening de `NOT` e nós JSON

**Escopo:**
- warning para `NOT` sem `condition`;
- `JsonExtract`: warning sem input `json`;
- `JsonExtract`: warning com `path` vazio/inválido;
- `JsonArrayLength`: warning sem input `json`.

**Checklist de aceite**
- [ ] Warnings emitidos apenas quando o nó participa de `where/having` ou `select` ativo.
- [ ] Testes cobrindo ausência de inputs e parâmetros inválidos.

---

### P1.2 — Regras de `Top`/paginação inconsistentes

**Escopo:**
- warning para `TOP/LIMIT <= 0`;
- warning quando `Offset` sem ordenação explícita (determinismo fraco);
- alinhamento cross-provider de mensagens (SQL Server vs Postgres/MySQL).

**Checklist de aceite**
- [ ] SQL continua gerado como hoje (sem regressão comportamental).
- [ ] Mensagens orientam correção (ex.: “use ORDER BY para paginação determinística”).

---

### P1.3 — Detecção de aliases potencialmente ambíguos

**Escopo:**
- warning para aliases repetidos no mesmo escopo (`TableSource`, `CteSource`, `Subquery`).

**Checklist de aceite**
- [ ] Warning só no mesmo escopo lógico (main query ou subgrafo CTE).
- [ ] Casos válidos em escopos diferentes não geram falso positivo.

---

## P2 — Qualidade de testes e prevenção de regressão

### P2.1 — Matriz de testes por categoria de hardening

**Proposta:** consolidar suíte com tabela de cobertura por categoria:
- Type mismatch
- Predicate
- Comparison
- Window
- CTE
- Join
- Subquery
- Set operation

**Checklist de aceite**
- [ ] Documento `TESTS.md` atualizado com matriz de cobertura.
- [ ] Pelo menos 1 teste por regra crítica de warning.

---

### P2.2 — Testes de snapshot de mensagens críticas

**Proposta:** snapshots apenas para mensagens-chave (ou códigos), evitando regressão silenciosa de UX de diagnóstico.

**Checklist de aceite**
- [ ] Snapshot com política de atualização explícita.
- [ ] Evitar snapshot frágil de SQL completo quando não necessário.

---

## P3 — UX de diagnóstico no canvas

### P3.1 — Vincular warning ao nó/pin

**Escopo:**
- cada diagnóstico com `NodeId`/`PinName` (quando aplicável);
- destaque visual discreto no canvas para nó com warning.

**Checklist de aceite**
- [ ] Clique no warning navega/foca no nó.
- [ ] Sem impacto de performance perceptível no canvas.

---

### P3.2 — Ações rápidas de correção

**Escopo (MVP):**
- ações contextuais simples (ex.: “Adicionar pin de ORDER BY”, “Definir alias padrão”, “Abrir propriedade pattern”).

**Checklist de aceite**
- [ ] Ação rápida opcional e segura (sem alterar SQL automaticamente sem confirmação).
- [ ] Cobertura de pelo menos 3 categorias de warning.

---

## Ordem Recomendada de Execução

1. P0.1 Diagnóstico estruturado
2. P0.2 Códigos de erro
3. P1.1 NOT/JSON
4. P1.2 Top/paginação
5. P1.3 Aliases ambíguos
6. P2.1 Matriz de cobertura
7. P2.2 Snapshots críticos
8. P3.1 Link warning → nó
9. P3.2 Ações rápidas

---

## Critérios de Conclusão (Hardening)

- [ ] Diagnósticos críticos com **código + categoria + mensagem acionável**.
- [ ] Principais erros de configuração detectados **antes** da exceção de compilação.
- [ ] Suíte de QueryPreview com regressão estável e cobertura ampliada por categoria.
- [ ] UX do preview capaz de localizar rapidamente o nó com problema.

---

## Notas de Escopo

- Este roadmap **não** adiciona novos recursos SQL; foca robustez e experiência de diagnóstico.
- Melhorias funcionais de Complex Queries permanecem regidas por `docs/COMPLEX_QUERIES_ROADMAP.md`.
