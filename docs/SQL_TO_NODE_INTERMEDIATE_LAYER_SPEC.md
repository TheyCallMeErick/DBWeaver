# SQL_TO_NODE_INTERMEDIATE_LAYER_SPEC

## 1. Objetivo

Definir uma especificação básica para uma camada intermediária entre **SQL texto** e **grafo de nós** no importador.

Meta principal:
- tornar o pipeline SQL → Node previsível, testável e extensível.

## 2. Contexto

Hoje o importador converte partes do SQL de forma relativamente direta para nós. Isso funciona para casos simples, mas aumenta complexidade para:
- expressões booleanas aninhadas (`AND`/`OR`/`NOT`)
- predicados compostos (`IN` literal, `BETWEEN`, `LIKE`, subqueries)
- evolução de dialetos e regras semânticas

A camada intermediária proposta reduz acoplamento e melhora manutenção.

## 3. Escopo (MVP)

Inclui:
- `SELECT ... FROM ... JOIN ... WHERE ...`
- projeções simples e alias
- predicates de WHERE com árvore lógica
- suporte a literais escalares (`string`, `number`, `boolean`, `date/datetime`)
- `IN` com subquery e com lista literal

Fora do MVP:
- `UNION`/`INTERSECT`/`EXCEPT`
- janela analítica avançada (`OVER` complexo)
- DML (`INSERT/UPDATE/DELETE`)
- otimização de layout visual

## 4. Arquitetura Proposta

Pipeline:
1. SQL texto → parser (AST)
2. AST → **IR canônica** (intermediate representation)
3. IR → grafo de nós (NodeViewModel + conexões)
4. (Opcional futuro) Node graph → IR → SQL

## 5. Modelo da IR (alto nível)

### 5.1 Query
- `SelectItems[]`
- `FromSource`
- `Joins[]`
- `WhereExpr?`

### 5.2 Source / Join
- `TableRef(schema, table, alias)`
- `SubqueryRef(sql|queryRef, alias)`
- `Join(type, rightSource, onExpr)`

### 5.3 Expressões
- `BinaryExpr(left, operator, right)`
- `LogicalExpr(kind=And|Or, operands[])`
- `NotExpr(inner)`
- `IsNullExpr(value, negate)`
- `BetweenExpr(value, low, high, negate)`
- `LikeExpr(value, pattern, negate)`
- `InExpr(value, right=Subquery|LiteralList, negate)`

### 5.4 Literais
- `LiteralExpr(type, raw, normalized)`
  - tipos mínimos: `String`, `Number`, `Boolean`, `DateTime`, `Null`

## 6. Regras de Conversão IR → Nodes

- Cada predicado vira um nó de comparação (ou subquery) com wiring explícito.
- Literais devem virar nós `Value*` quando possível (evitar depender só de `PinLiterals`).
- `AND`/`OR` produzem nós lógicos variádicos (`conditions`).
- `NOT` envolve a expressão interna com nó `Not`.
- Quando um campo não existir no `TableSource`, criar pin inferido de saída.

## 7. Tratamento de Parcial/Fallback

Quando não houver suporte completo:
- registrar item `Partial` com motivo claro
- preservar trecho SQL original no contexto do relatório
- continuar import do restante da árvore sempre que possível

## 8. Observabilidade e Diagnóstico

Adicionar métricas por etapa:
- parse_ms
- map_ast_to_ir_ms
- map_ir_to_nodes_ms
- import_total_ms
- contagem: imported/partial/skipped

## 9. Estratégia de Testes

### 9.1 Unitários
- AST → IR para cada tipo de expressão
- IR → Nodes para cada mapeamento de predicado

### 9.2 Integração
- consultas reais com combinação de `AND/OR/NOT`
- validação de nós literais conectados
- round-trip básico (import + recompile SQL)

## 10. Plano de Adoção

Fase 1:
- introduzir tipos da IR e mapper AST→IR sem alterar fluxo atual

Fase 2:
- habilitar flag para usar IR→Nodes no import de WHERE

Fase 3:
- migrar demais cláusulas (`SELECT`, `JOIN`) para IR

Fase 4:
- remover caminho legado após cobertura de testes e estabilização

## 11. Critérios de Pronto (MVP)

- consultas com `WHERE` lógico complexo não caem em fallback para casos suportados
- criação de nós literais visível para comparações com constantes
- cobertura de testes de integração para cenários reais
- nenhuma regressão nos testes existentes de SQL import

---

Versão: `0.1 (básica)`
Data: `2026-04-10`
