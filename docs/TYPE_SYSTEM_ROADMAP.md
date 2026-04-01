# Roadmap: Sistema de Tipos Robusto para Pinos

> Inspirado no sistema de tipos dos Geometry Nodes do Blender — pinos fortemente tipados, sem `Any` genérico, com `ColumnSet` como tipo de primeira classe.

---

## 1. Diagnóstico do Estado Atual

### O problema com `PinDataType.Any`

O tipo `Any` existe hoje como válvula de escape para nós polimórficos (AND, OR, MIN, MAX, WindowFunction). O mecanismo de *type narrowing* atenua o problema, mas não o resolve:

| Sintoma | Consequência |
|---|---|
| Pinos `Any` aceitam qualquer conexão | Erros de tipo só aparecem na compilação |
| Narrowing é heurístico e baseado em irmãos | Pode narroar para tipo errado em grafos complexos |
| `ColumnList` usa `Any` para coletar colunas | Não há garantia de que as colunas são compatíveis entre si |
| Não há distinção entre "escalar" e "coleção de colunas" | Um nó `Sum` e um nó `ColumnList` parecem iguais para o motor |

### Ausência de tipos estruturais

Atualmente, um `TableSource` expõe cada coluna como um pino `Output` individual. Não existe um tipo que represente **"um conjunto de colunas"** como unidade. Isso força o usuário a cabiear cada coluna manualmente, mesmo quando quer passar todas elas para um `SELECT *`.

---

## 2. Inspiração: Blender Geometry Nodes

O Blender resolve o problema com quatro princípios:

1. **Forma + cor** — a forma diferencia a família semântica; a cor, o tipo específico
2. **Sem polimorfismo implícito** — cada nó declara exatamente o que aceita
3. **`Geometry` como tipo de primeira classe** — carrega a malha inteira, não ponto a ponto
4. **`Field`** — valor calculado *por elemento* (análogo a "valor por linha" no SQL)

Mapeamento para SQL — com adições específicas do domínio relacional:

| Blender | Forma Blender | SQL Architect | Forma proposta | Semântica |
|---|---|---|---|---|
| `Float` | Círculo cinza | `Decimal` | Círculo verde-claro | Número com casas decimais |
| `Integer` | Círculo azul-escuro | `Integer` | Círculo esmeralda | Número inteiro |
| `Boolean` | Círculo cinza-escuro | `Boolean` | Círculo âmbar | Verdadeiro / falso |
| `String` | Círculo rosa | `Text` | Círculo azul | Texto / VARCHAR |
| `Geometry` | Losango azul-roxo | **`RowSet`** | Losango achatado rosa | Tabela/subquery/CTE completa |
| `Field` | Losango de campo | **`ColumnRef`** | Losango laranja | Referência a coluna (valor por linha) |
| *(sem equiv.)* | — | **`ColumnSet`** | Losango vazado ouro | Coleção ordenada de `ColumnRef` |
| *(sem equiv.)* | — | `DateTime` | Círculo ciano | Data e hora |
| *(sem equiv.)* | — | `Json` | Círculo índigo | Blob JSON / JSONB |
| *(sem equiv.)* | — | `Expression` | Círculo tracejado cinza | Fragmento SQL bruto (escape hatch) |

> **Diferença chave em relação ao Blender:** no SQL Architect, a distinção entre *escalar* (`Decimal`, `Integer`…) e *referência de coluna* (`ColumnRef`) é explícita e tem forma diferente. No Blender não há essa distinção porque malhas não têm "colunas". Aqui, um `ColumnRef(Integer)` é semanticamente diferente de um `Integer` — o primeiro é uma *referência posicional a dados no resultado*, o segundo é um *valor literal/computado*.

---

## 3. Sistema de Tipos Proposto

### 3.1 Enum `PinDataType` revisado

```csharp
public enum PinDataType
{
    // Tipos escalares (valor por linha)
    Text,       // VARCHAR, NVARCHAR, TEXT
    Integer,    // INT, BIGINT, SMALLINT
    Decimal,    // FLOAT, DECIMAL, NUMERIC
    Boolean,    // BIT, BOOLEAN
    DateTime,   // DATE, DATETIME, TIMESTAMP
    Json,       // JSON

    // Tipos estruturais (novos)
    ColumnRef,  // Referência a uma coluna com tipo conhecido — ex: "u.name : Text"
    ColumnSet,  // Coleção ordenada de ColumnRef — ex: lista do SELECT
    RowSet,     // Resultado de tabela/subquery/CTE completo

    // Escape hatch (mantido por compatibilidade, uso restrito)
    Expression, // Fragmento SQL bruto — apenas para nós avançados
}
```

> **`Any` é removido.** Todo pino deve declarar seu tipo. Nós polimórficos usam overloading declarativo (seção 4).

---

### 3.2 Identidade Visual — Gramática de Forma, Cor e Fio

#### 3.2.1 Princípio de design

> **Forma = família semântica. Cor = tipo específico. Tamanho = nível hierárquico.**

O usuário deve conseguir identificar o que um pino representa *sem ler o label*, assim como no Blender. Forma e cor juntos criam redundância visual que funciona mesmo com daltonismo parcial.

---

#### 3.2.2 Formas por família

Cada família tem uma geometria de conector própria. Abaixo a definição visual e o `Path` Avalonia correspondente (coordenadas centradas em `0,0`, caixa `10×10`):

| Família | Forma | Representação | Tipos |
|---|---|---|---|
| **Escalar** | Círculo sólido preenchido | `Ellipse W=10 H=10` | `Text`, `Integer`, `Decimal`, `Boolean`, `DateTime`, `Json` |
| **Referência** | Losango (diamante) sólido | `M 5,0 L 10,5 L 5,10 L 0,5 Z` | `ColumnRef` |
| **Coleção** | Losango com miolo vazado | Losango externo 10×10 + losango interno 4×4 recortado | `ColumnSet` |
| **Conjunto de linhas** | Losango achatado (largo) | `M 5,2 L 10,5 L 5,8 L 0,5 Z` (caixa 10×6) | `RowSet` |
| **Escape hatch** | Círculo com borda tracejada | `Ellipse W=10 H=10` com `StrokeDashArray="2 2"` | `Expression` |

```
  Escalar       ColumnRef      ColumnSet       RowSet        Expression
  (círculo)     (losango)    (losango vazado)  (achatado)    (tracejado)

    ●               ◆              ◇              ⬥              ○ - -
```

> **Tamanho base: 10×10 px** para escalares e referências. `RowSet` usa **12×8 px** para reforçar que é "mais largo" — carrega mais dados. `ColumnSet` usa **11×11 px**.

---

#### 3.2.3 Paleta de cores — sistema semântico

As cores são agrupadas em **três famílias cromáticas** com lógica semântica:

```
 FAMÍLIA FRIA (dados informativos — escalares de texto/tempo)
   Text       #60A5FA  azul médio        — "informação textual"
   DateTime   #38BDF8  azul-ciano claro  — "fluxo de tempo"
   Json       #818CF8  índigo suave      — "dado estruturado opaco"

 FAMÍLIA VERDE (dados quantitativos — números)
   Integer    #34D399  esmeralda         — "quantidade discreta"
   Decimal    #86EFAC  verde-lima suave  — "quantidade contínua"

 FAMÍLIA QUENTE (lógica e estrutura — decisão e agrupamento)
   Boolean    #FCD34D  âmbar             — "decisão, semáforo"
   ColumnRef  #FB923C  laranja           — "ponteiro de coluna"
   ColumnSet  #FBBF24  ouro              — "coleção de ponteiros"
   RowSet     #F472B6  rosa-magenta      — "conjunto de linhas completo"

 NEUTRO (sem tipo garantido)
   Expression #6B7280  cinza-ardósia     — "fragmento SQL bruto"
```

Tabela consolidada:

| Tipo | Hex | HSL aproximado | Justificativa semântica |
|---|---|---|---|
| `Text` | `#60A5FA` | H=217 S=94% L=68% | Azul informacional |
| `DateTime` | `#38BDF8` | H=199 S=93% L=60% | Azul mais frio, temporal |
| `Json` | `#818CF8` | H=238 S=89% L=74% | Índigo — estrutura complexa/opaca |
| `Integer` | `#34D399` | H=160 S=64% L=51% | Verde vibrante — discreto |
| `Decimal` | `#86EFAC` | H=142 S=76% L=73% | Verde mais claro — contínuo |
| `Boolean` | `#FCD34D` | H=43 S=96% L=65% | Âmbar — alerta/decisão |
| `ColumnRef` | `#FB923C` | H=27 S=96% L=61% | Laranja — ponteiro/referência |
| `ColumnSet` | `#FBBF24` | H=38 S=96% L=56% | Ouro — agrupamento de refs |
| `RowSet` | `#F472B6` | H=322 S=87% L=70% | Rosa magenta — nível de tabela |
| `Expression` | `#6B7280` | H=220 S= 8% L=46% | Cinza neutro — sem garantia de tipo |

> **Regra de acessibilidade:** nunca use cor como *único* diferenciador. A forma do pino é o primeiro identificador; a cor reforça. Usuários com protanopia/deuteranopia distinguem círculo de losango sem depender de verde vs. vermelho.

---

#### 3.2.4 Aparência dos fios (wires)

O fio herda a cor do pino de origem (`FromPin`). Além da cor, o estilo da linha varia por família:

| Família | Estilo do traço | Espessura normal | Espessura hover |
|---|---|---|---|
| Escalar | Sólido | 1.8 px | 2.5 px |
| `ColumnRef` | Sólido | 2.0 px | 2.8 px |
| `ColumnSet` | Traço longo `8 3` | 2.2 px | 3.0 px |
| `RowSet` | Traço largo `12 4` | 2.5 px | 3.5 px |
| `Expression` | Pontilhado `2 4` | 1.5 px | 2.2 px |

O padrão de traço reforça visualmente que fios estruturais (`ColumnSet`, `RowSet`) "carregam mais" — são mais espessos e com dash maior.

---

#### 3.2.5 Estados visuais dos pinos

| Estado | Aparência |
|---|---|
| **Default (desconectado)** | Borda colorida, interior transparente (hollow) |
| **Conectado** | Interior preenchido com a cor do tipo (solid) |
| **Hover / arraste válido** | Escala 1.7× + glow `rgba(cor, 0.35)` em 8px |
| **Drop target válido** | Borda âmbar `#FBBF24`, pulso de escala suave |
| **Drop target inválido** | Pino destino desaparece (opacity 0.2) — sem feedback de erro agressivo |
| **Erro de validação** | Borda vermelha `#F87171` + ícone `⚠` no label do pino |

> **Decisão de UX:** pinos incompatíveis ficam *apagados*, não destacados em vermelho. O vermelho é reservado para erros de validação estática (pino obrigatório sem conexão). Isso evita poluição visual durante o arraste.

---

#### 3.2.6 Labels dos pinos — convenção de exibição

```
[forma]  nome_do_pino  [tipo abreviado]
```

Exemplos:

```
◆ user_id      INT     ← ColumnRef(Integer), output do TableSource
◇ columns      SET     ← ColumnSet, input do SelectOutput
● condition    BOOL    ← Boolean escalar
⬥ users_table  ROWS    ← RowSet, output do TableSource
● price        DEC     ← Decimal escalar
```

Abreviações:
| Tipo | Label |
|---|---|
| `Text` | `TXT` |
| `Integer` | `INT` |
| `Decimal` | `DEC` |
| `Boolean` | `BOOL` |
| `DateTime` | `DT` |
| `Json` | `JSON` |
| `ColumnRef` | tipo do escalar interno, ex: `INT↑` (seta indica "é uma ref") |
| `ColumnSet` | `SET` |
| `RowSet` | `ROWS` |
| `Expression` | `SQL` |

---

#### 3.2.7 Estrutura visual dos cards (3 linhas)

Estado atual: os cards usam 2 linhas (título + linha única de I/O). Isso mistura semânticas de entrada e saída no mesmo bloco visual e dificulta leitura rápida em grafos densos.

**Proposta:** adotar layout em **3 linhas fixas**:

1. **Linha 1 — Título**
  - Nome do nó, ícone/categoria e ações rápidas (quando houver)
2. **Linha 2 — Inputs**
  - Apenas pinos de entrada, alinhados à esquerda
3. **Linha 3 — Outputs**
  - Apenas pinos de saída, alinhados à direita

Regras de UX:
- Inputs sempre aparecem **antes** dos outputs no fluxo vertical do card
- Linhas 2 e 3 usam **pequena diferença de background** para separação perceptiva sem ruído visual
- O contraste entre linha de input e output deve ser sutil (ex.: variação leve de luminosidade do mesmo token de superfície)
- O contêiner do node adota formato **squircle** (estética iOS), com suavização de cantos além do arredondamento padrão
- Priorizar curvas contínuas/suaves de canto (superellipse-like), evitando aparência de retângulo com `CornerRadius` rígido
- Não usar novas cores semânticas para essa divisão; a semântica de tipo continua nos pinos/fios
- Altura de linha consistente para evitar salto visual entre nós com quantidades diferentes de pinos

Benefícios esperados:
- Leitura mais rápida da direção de fluxo (entrada → saída)
- Menor ambiguidade em nós com muitos pinos
- Melhor escaneabilidade em zoom baixo
- Percepção visual mais moderna e coesa dos cards no canvas

---

### 3.3 Metadados de `ColumnRef`

Um pino `ColumnRef` carrega, além do tipo de pino, metadados sobre a coluna que representa:

```csharp
public sealed record ColumnRefMeta(
    string ColumnName,      // ex: "user_id"
    string? TableAlias,     // ex: "u" (de "users AS u")
    PinDataType ScalarType, // ex: Integer — tipo do valor da coluna
    bool IsNullable
);
```

Isso permite:
- Tooltip detalhado ao hover: `u.user_id : Integer NOT NULL`
- Validação de tipo de valor downstream (ex: `Sum` só aceita `ColumnRef` com `ScalarType` numérico)
- Autocompletar nome de coluna em parâmetros de texto

---

### 3.4 Metadados de `ColumnSet`

```csharp
public sealed record ColumnSetMeta(
    IReadOnlyList<ColumnRefMeta> Columns
);
```

Um pino `ColumnSet` carrega o schema completo da coleção. Isso habilita:
- Validação de `SELECT *` vs colunas específicas
- Preview do schema no tooltip
- Verificação de compatibilidade entre dois `ColumnSet` em operações UNION

---

## 4. Eliminação do `Any` — Estratégias por Caso de Uso

### 4.1 Nós de Comparação (`=`, `<`, `>`, `BETWEEN`)

**Hoje:** ambos os lados são `Any`, narrowing propaga o tipo.

**Proposta:** Nós de comparação se tornam nós *genéricos* com um parâmetro de tipo declarado em tempo de instanciação, similar a templates:

```
CompareEquals<T>
  input_left  : T
  input_right : T
  output      : Boolean
```

Na UI, ao arrastar o primeiro fio para um dos lados, o nó "concretiza" `T` e os pinos se recoloram para o tipo escolhido. Isso é determínístico, sem heurística de irmãos.

### 4.2 Nós Lógicos (`AND`, `OR`, `NOT`)

**Hoje:** `Any` com narrowing.

**Proposta:** Pinos tipados explicitamente como `Boolean`. `AND`/`OR` são variádicos em `Boolean` apenas. Pinos adicionais são sempre `Boolean`.

### 4.3 Funções de Agregação (`MIN`, `MAX`, `AVG`, `SUM`)

**Hoje:** input `Any`, narrowed para escalar.

**Proposta:** Overloads declarados no `NodeDefinition`:

```csharp
// O usuário escolhe a variante ao criar o nó
MinInteger  : input ColumnRef(Integer)  → output Integer
MinDecimal  : input ColumnRef(Decimal)  → output Decimal
MinText     : input ColumnRef(Text)     → output Text
MinDateTime : input ColumnRef(DateTime) → output DateTime
```

Ou, alternativamente, um nó `Min` com um parâmetro escalar `ScalarType` que configura os pinos dinamicamente ao ser alterado.

### 4.4 `ColumnList` → substituído por `ColumnSet`

**Hoje:** `ColumnList` é um nó especial que coleta múltiplas entradas `Any` e produz a lista SELECT.

**Proposta:** `ColumnSet` é um tipo de pino de primeira classe. O nó `TableSource` passa a expor **dois tipos de output**:

| Pino | Tipo | Descrição |
|---|---|---|
| `*` (estrela) | `ColumnSet` | Todas as colunas da tabela como conjunto |
| `col_name` (um por coluna) | `ColumnRef` | Coluna individual |

O nó `SelectOutput` aceita `ColumnSet` diretamente, sem necessidade de `ColumnList` intermediário para o caso de "todas as colunas". Para listas customizadas, existe um nó `ColumnSetBuilder` variádico que aceita múltiplos `ColumnRef` e produz um `ColumnSet`.

---

## 5. Novos Nós Decorrentes do Sistema de Tipos

| Nó | Categoria | Input | Output | Descrição |
|---|---|---|---|---|
| `ColumnSetBuilder` | DataSource | N × `ColumnRef` | `ColumnSet` | Monta lista SELECT customizada |
| `ColumnSetMerge` | DataSource | 2 × `ColumnSet` | `ColumnSet` | Concatena dois conjuntos (UNION de schemas) |
| `ColumnRefCast` | TypeCast | `ColumnRef(A)` | `ColumnRef(B)` | CAST explícito de coluna |
| `RowSetFilter` | ResultModifier | `RowSet` + `Boolean` | `RowSet` | WHERE como operação no RowSet |
| `RowSetJoin` | DataSource | 2 × `RowSet` + `Boolean` | `RowSet` | JOIN entre dois RowSets |
| `RowSetAggregate` | Aggregate | `RowSet` + `ColumnSet` | `RowSet` | GROUP BY como operação no RowSet |
| `ScalarFromColumn` | TypeCast | `ColumnRef(T)` | `T` | Extrai o tipo escalar da referência de coluna |

---

## 6. Compatibilidade e Regras de Conexão

### 6.1 Hierarquia de compatibilidade

```
RowSet  ─── contém ──→  ColumnSet  ─── contém ──→  ColumnRef(T)  ─── escalariza ──→  T (escalar)
```

Regras:
- `ColumnSet` é compatível apenas com `ColumnSet`
- `ColumnRef` é compatível apenas com `ColumnRef` do mesmo `ScalarType`
- `RowSet` é compatível apenas com `RowSet`
- Escalares (`Text`, `Integer`, `Decimal`, `Boolean`, `DateTime`, `Json`) são compatíveis apenas entre si do mesmo tipo
- `Expression` é compatível com qualquer escalar (escape hatch explícito)
- Conversões explícitas requerem nó `ColumnRefCast` ou `CAST`

### 6.2 Regras de `CanAccept` revisadas

```csharp
public bool CanAccept(PinViewModel other)
{
    if (other.Owner == Owner) return false;
    if (other.Direction == Direction) return false;

    var src = other.EffectiveDataType;
    var dst = EffectiveDataType;

    // Expression é escape hatch para escalares
    if (src == PinDataType.Expression && dst.IsScalar()) return true;
    if (dst == PinDataType.Expression && src.IsScalar()) return true;

    // Tipos devem ser idênticos
    if (src != dst) return false;

    // Para ColumnRef: ScalarType deve ser compatível
    if (src == PinDataType.ColumnRef)
        return other.ColumnRefMeta?.ScalarType == ColumnRefMeta?.ScalarType;

    return true;
}
```

---

## 7. Inferência de Tipos de Coluna via Metadados de Schema

Quando o usuário conecta uma string de conexão e faz "fetch schema", cada coluna do banco deve mapear para um `PinDataType` com `ColumnRefMeta`:

| SQL Type | `ScalarType` mapeado |
|---|---|
| `INT`, `BIGINT`, `SMALLINT` | `Integer` |
| `FLOAT`, `DOUBLE`, `DECIMAL`, `NUMERIC`, `REAL` | `Decimal` |
| `VARCHAR`, `NVARCHAR`, `TEXT`, `CHAR` | `Text` |
| `BIT`, `BOOLEAN` | `Boolean` |
| `DATE`, `DATETIME`, `TIMESTAMP`, `TIME` | `DateTime` |
| `JSON`, `JSONB` | `Json` |
| Outros / desconhecido | `Expression` (com aviso visual) |

---

## 8. Fases de Implementação

### Fase 1 — Fundações do novo sistema de tipos
*Pré-requisito para todas as demais fases*

- [x] Adicionar `Integer`, `Decimal`, `ColumnRef`, `ColumnSet`, `RowSet` ao enum `PinDataType`
- [x] Deprecar (não remover ainda) `PinDataType.Any`
- [x] Adicionar `ColumnRefMeta` e `ColumnSetMeta` records ao core
- [x] Atualizar `PinDescriptor` para aceitar `ColumnRefMeta?` e `ColumnSetMeta?`
- [x] Implementar `IsScalar()`, `IsStructural()` extension methods no enum
- [x] Atualizar mapeamento de schema SQL → `PinDataType`

### Fase 2 — Identidade Visual
*Pode rodar em paralelo com a Fase 1 nas partes de UI puras*

- [x] Substituir `Border.pin-dot` por um controle `PinShapeControl` que renderiza via `Path` baseado na família do tipo
- [x] Reestruturar card de nó para 3 linhas: título, faixa de inputs (topo) e faixa de outputs (base)
- [x] Aplicar variação sutil de background entre a linha de inputs e a linha de outputs (mesma família de superfície)
- [x] Atualizar shape do card para estilo **squircle** (cantos suavizados/contínuos, inspirado em iOS)
- [x] Implementar as quatro geometrias: círculo (`Ellipse`), losango sólido, losango vazado e losango achatado
- [x] Implementar `Expression` com `StrokeDashArray="2 2"` e interior transparente
- [x] Aplicar a nova paleta de cores (seção 3.2.3): três famílias cromáticas (fria / verde / quente)
- [x] Diferenciar pino **desconectado** (hollow — borda colorida, interior transparente) de **conectado** (solid — preenchido)
- [x] Estilo de fio variável por família: sólido para escalares, traço longo para `ColumnSet`, traço largo para `RowSet`, pontilhado para `Expression`
- [x] Espessura de fio diferenciada por família (1.8 → 2.5 px escalares; 2.5 → 3.5 px `RowSet`)
- [x] Labels dos pinos com abreviações de tipo (`INT`, `DEC`, `TXT`, `SET`, `ROWS`, `SQL`) e indicador `↑` em `ColumnRef`
- [x] Tooltip rico com `ColumnRefMeta`: `u.user_id : Integer NOT NULL`
- [x] Preview de `ColumnSet` no tooltip: lista inline das colunas com suas formas e cores
- [x] Substituir feedback de "drop target inválido" de vermelho por opacity-fade (0.2) para reduzir poluição visual

### Fase 3 — Nós estruturais (`ColumnRef`, `ColumnSet`, `RowSet`)

- [x] Atualizar `TableSource` para emitir pinos `ColumnRef` tipados + pino `ColumnSet` para `*`
- [x] Criar nó `ColumnSetBuilder` (substitui o papel secundário de `ColumnList`)
- [x] Atualizar `SelectOutput` para aceitar tanto `ColumnSet` quanto `ColumnRef` individual
- [x] Criar nó `ColumnSetMerge`
- [x] Tipar `WhereOutput` para receber `Boolean` explicitamente

### Fase 4 — Eliminação do `Any`

- [x] Converter `AND`, `OR`, `NOT` para pinos `Boolean` explícitos
- [x] Converter funções de agregação para overloads ou parâmetro de tipo configurável
- [x] Converter nós de comparação para nós genéricos com concretização ao primeiro fio
- [x] Remover lógica de *type narrowing* heurístico (`PinManager.NarrowPinTypes`)
- [x] Remover `PinDataType.Any` do enum

### Fase 5 — Nós estruturais avançados

- [x] `RowSetJoin` — JOIN visual com dois `RowSet` + condição `Boolean`
- [x] `RowSetFilter` — WHERE integrado ao `RowSet`
- [x] `RowSetAggregate` — GROUP BY integrado ao `RowSet`
- [x] `ColumnRefCast` — CAST explícito de coluna
- [x] `ScalarFromColumn` — "desempacota" `ColumnRef(T)` para `T` escalar

### Fase 6 — Validação e experiência do usuário

- [x] Validação estática de grafo: detectar pinos `Expression` não justificados
- [x] Highlight de incompatibilidades de tipo ao tentar conectar pinos errados (mensagem clara)
- [ ] Preview de `ColumnSet` no tooltip (lista de colunas com tipos)
- [x] Preview de `RowSet` no tooltip (shape resumida do schema)
- [x] Migração de grafos salvos (`.vsqa` ou equivalente) do formato antigo para o novo

---

## 9. Não-objetivos (explicitamente fora de escopo)

- **Inferência de tipos automática do grafo** — tipos são declarados explicitamente, não inferidos por fluxo (como TypeScript)
- **Subtyping entre escalares** — `Integer` não é subtipo de `Decimal`; cast sempre explícito
- **Tipos genéricos parametrizados em múltiplos pinos** — overloads simples são suficientes
- **Compatibilidade binária retroativa perfeita** — grafos antigos precisarão de migração assistida

---

## 10. Referências

- [Geometry Nodes — Blender Manual](https://docs.blender.org/manual/en/latest/modeling/geometry_nodes/)
- [ISqlExpression.cs](../src/VisualSqlArchitect/Nodes/ISqlExpression.cs) — enum `PinDataType` atual
- [NodeDefinition.cs](../src/VisualSqlArchitect/Nodes/NodeDefinition.cs) — `PinDescriptor`, `NodeDefinition`
- [PinViewModel.cs](../src/VisualSqlArchitect.UI/ViewModels/Canvas/PinViewModel.cs) — `CanAccept`, `PinColor`
- [PinManager.cs](../src/VisualSqlArchitect.UI/ViewModels/Canvas/PinManager.cs) — narrowing atual
- [COMPLEX_QUERIES_ROADMAP.md](COMPLEX_QUERIES_ROADMAP.md) — roadmap de funcionalidades SQL avançadas
