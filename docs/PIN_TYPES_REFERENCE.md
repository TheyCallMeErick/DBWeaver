# Referência Visual de Tipos de Pino

> Este documento é a fonte de verdade sobre o que cada cor e forma de pino representa no canvas.
> Derivado do código em `PinViewModel.cs`, `PinShapeControl.cs` e `PinDataTypeExtensions.cs`.

---

## Gramática visual: forma + cor

> **Forma = família semântica. Cor = tipo específico.**
>
> A forma permite identificar o que um pino representa sem ler o label — e sem depender de cor.
> Isso torna o canvas acessível para usuários com daltonismo parcial.

---

## Tabela de referência rápida

| Tipo | Forma | Cor | Hex | Label | Família |
|---|---|---|---|---|---|
| `Text` | Círculo | Azul | `#60A5FA` | `TXT` | Escalar |
| `Integer` | Círculo | Esmeralda | `#34D399` | `INT` | Escalar |
| `Decimal` | Círculo | Verde-lima | `#86EFAC` | `DEC` | Escalar |
| `Number` | Círculo | Verde | `#4ADE80` | `NUM` | Escalar |
| `Boolean` | Círculo | Âmbar | `#FCD34D` | `BOOL` | Escalar |
| `DateTime` | Círculo | Azul-ciano | `#38BDF8` | `DT` | Escalar |
| `Json` | Círculo | Índigo | `#818CF8` | `JSON` | Escalar |
| `ColumnRef` | Losango sólido | Laranja | `#FB923C` | `INT↑` / `TXT↑` etc. | Estrutural |
| `ColumnSet` | Losango com miolo vazado | Ouro | `#FBBF24` | `SET` | Estrutural |
| `RowSet` | Losango achatado | Rosa-magenta | `#F472B6` | `ROWS` | Estrutural |
| `Expression` | Círculo tracejado | Cinza-ardósia | `#6B7280` | `SQL` | Escape hatch |

---

## Formas em detalhe

### Círculo — tipos escalares

Usado por todos os tipos que representam **um único valor por linha**: texto, números, booleano, data, JSON.

```
    ●
  (sólido quando conectado,
   apenas borda quando desconectado)
```

O círculo tracejado (`Expression`) segue a mesma geometria, mas a borda é pontilhada `2 2`
para indicar que não há garantia de tipo — é um fragmento SQL bruto.

---

### Losango sólido — `ColumnRef`

Representa uma **referência a uma coluna específica** — o equivalente a `tabela.coluna` no SQL.
Carrega metadados: nome da coluna, alias da tabela, tipo escalar interno e se é nullable.

```
    ◆
  (sólido quando conectado)
```

O label exibe o tipo escalar interno com uma seta `↑` para indicar que é uma referência:
`INT↑` significa "referência a uma coluna do tipo Integer".

Tooltip ao hover: `u.user_id : Integer NOT NULL`

---

### Losango com miolo vazado — `ColumnSet`

Representa uma **lista ordenada de colunas** — o conjunto de colunas de um SELECT.
É o tipo de primeira classe para conectar um `TableSource` a um `SelectOutput` sem
precisar cabear cada coluna individualmente.

```
    ◇
  (losango externo + losango interno recortado)
```

Tooltip ao hover: `ColumnSet[4] id:INT, name:TXT, email:TXT, created_at:DT`

---

### Losango achatado — `RowSet`

Representa uma **tabela inteira** — resultado de uma query, tabela física, subquery ou CTE.
É o tipo "mais pesado" do canvas: carrega o schema completo de linhas e colunas.

```
    ⬥
  (losango com altura reduzida a ~70% da largura — visualmente "largo")
```

Tooltip ao hover: `RowSet[5] id:INT, name:TXT, email:TXT, ...`

---

## Estados visuais do pino

| Estado | Aparência |
|---|---|
| **Desconectado** | Apenas a borda colorida; interior transparente (hollow) |
| **Conectado** | Interior preenchido com a cor do tipo (solid) |
| **Hover / drag sobre pino válido** | Escala ampliada + glow semitransparente na cor do tipo |
| **Drop target válido** | Borda âmbar `#FBBF24` (independente do tipo) |
| **Drop target inválido** | Pino fica apagado (opacity reduzida) — sem vermelho agressivo |
| **Erro de validação** | Borda vermelha `#F87171` + indicador no label do pino |

> O feedback de incompatibilidade é **subtração** (o pino some), não adição de cor de erro.
> Vermelho é reservado exclusivamente para erros de validação estática (pino obrigatório sem conexão).

---

## Famílias semânticas e lógica de cor

### Família fria — texto e tempo

Tipos que carregam **informação descritiva**: texto, datas, JSON estruturado.

| Tipo | Cor | Justificativa |
|---|---|---|
| `Text` | Azul `#60A5FA` | Azul informacional — texto como dado |
| `DateTime` | Azul-ciano `#38BDF8` | Mais frio que o azul — fluxo de tempo |
| `Json` | Índigo `#818CF8` | Estrutura complexa e opaca — roxo indica profundidade |

### Família verde — dados quantitativos

Tipos que representam **números**.

| Tipo | Cor | Justificativa |
|---|---|---|
| `Integer` | Esmeralda `#34D399` | Verde vibrante — quantidade discreta |
| `Decimal` | Verde-lima `#86EFAC` | Verde mais claro — quantidade contínua |
| `Number` | Verde `#4ADE80` | Numérico genérico (interoperável com Integer e Decimal) |

> `Number` existe como tipo de compatibilidade durante migração de grafos antigos e para nós
> que operam em qualquer número sem distinção. É interoperável com `Integer` e `Decimal`.

### Família quente — lógica e estrutura

Tipos que representam **decisões e agrupamentos** — os tipos que controlam o fluxo do dado.

| Tipo | Cor | Justificativa |
|---|---|---|
| `Boolean` | Âmbar `#FCD34D` | Decisão binária — semáforo/alerta |
| `ColumnRef` | Laranja `#FB923C` | Ponteiro — referência posicional a um dado |
| `ColumnSet` | Ouro `#FBBF24` | Coleção de ponteiros — mais "cheio" que uma referência só |
| `RowSet` | Rosa-magenta `#F472B6` | Nível de tabela — mais amplo que colunas |

### Neutro — sem tipo garantido

| Tipo | Cor | Justificativa |
|---|---|---|
| `Expression` | Cinza `#6B7280` | Fragmento SQL bruto sem garantia de tipo |

---

## Regras de compatibilidade de conexão

Dois pinos só podem ser conectados se forem compatíveis. As regras implementadas em `CanAccept`:

| De | Para | Permitido? | Observação |
|---|---|---|---|
| Qualquer escalar | Mesmo escalar | Sim | Tipos idênticos sempre conectam |
| `Integer` / `Decimal` / `Number` | Qualquer dos três | Sim | Família numérica é interoperável |
| `ColumnRef` | `ColumnRef` (mesmo ScalarType) | Sim | Tipos escalares internos devem ser compatíveis |
| `ColumnRef` | Qualquer escalar | Sim | Desempacota a referência para uso escalar |
| `Expression` | Qualquer escalar | Sim | Escape hatch aceito em slots escalares |
| `RowSet` | `RowSet` | Sim | Apenas com outro RowSet |
| `ColumnSet` | `ColumnSet` | Sim | Apenas com outro ColumnSet |
| `RowSet` | Escalar / ColumnRef / ColumnSet | Não | Tipos estruturalmente incompatíveis |
| Escalar | `RowSet` ou `ColumnSet` | Não | Não há upcast implícito |

> Para converter entre famílias incompatíveis, use nós explícitos:
> `ColumnRefCast` para mudar o tipo escalar de uma referência de coluna,
> `ScalarFromColumn` para extrair o valor escalar de um `ColumnRef`.

---

## `ColumnRef` e seus metadados

Um pino `ColumnRef` carrega mais do que o tipo — carrega a **identidade da coluna**:

```
alias_da_tabela . nome_da_coluna : ScalarType  (nullable?)

Exemplo: u.user_id : Integer NOT NULL
```

Campos em `ColumnRefMeta`:
- `ColumnName` — ex: `user_id`
- `TableAlias` — ex: `u` (de `users AS u`)
- `ScalarType` — o tipo do valor: `Integer`, `Text`, `Decimal`, etc.
- `IsNullable` — se a coluna aceita NULL

Quando o `ScalarType` é conhecido, o label no canvas exibe o tipo com `↑`:
`INT↑` = referência a uma coluna Integer.

---

## `ColumnSet` e seus metadados

Um pino `ColumnSet` carrega o **schema completo** da lista de colunas.
O tooltip exibe as primeiras 4 colunas com seus tipos:

```
ColumnSet[6] id:INT, name:TXT, email:TXT, created_at:DT, ...
```

`ColumnSetMeta` é uma lista ordenada de `ColumnRefMeta` — a ordem é a mesma do SELECT gerado.

---

## Onde cada tipo aparece no canvas

| Situação | Tipo de pino |
|---|---|
| Output de `TableSource` por coluna | `ColumnRef(ScalarType)` |
| Output `*` de `TableSource` | `ColumnSet` |
| Output de `TableSource` como tabela | `RowSet` |
| Input/Output de nós de string (`Upper`, `Trim`) | `Text` |
| Input/Output de nós de data (`DateAdd`, `DatePart`) | `DateTime` |
| Output de nós de comparação (`=`, `<`, `BETWEEN`) | `Boolean` |
| Input de `AND`, `OR`, `NOT` | `Boolean` |
| Input/Output de `ColumnSetBuilder` | `ColumnRef` (in) / `ColumnSet` (out) |
| Input/Output de nós de join (`Join`, `RowSetJoin`) | `RowSet` |
| Input de `SelectOutput` | `ColumnSet` ou `ColumnRef` |
| Input de `WhereOutput` | `Boolean` |
| Nós com fragmento SQL direto | `Expression` |
