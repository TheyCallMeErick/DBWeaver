# Status de Implementação - Refatoração

**Atualizado:** 26 de março de 2026
**Timeline Total:** 3 sprints (6 semanas)

---

## 📊 Overview de Progresso

```
Fase 1: Fundação (Sprint 1-2) ░░░░░░░░░░ 0%
├─ Eixo 1: Abstração Providers         ░░░░░░░░░░ 0%
├─ Eixo 2: Abstração Metadata          ░░░░░░░░░░ 0%
└─ Eixo 4: Infraestrutura Testes       ░░░░░░░░░░ 0%

Fase 2: Query Engine (Sprint 3) ░░░░░░░░░░ 0%
├─ Eixo 3: Consolidação Query Building ░░░░░░░░░░ 0%
└─ Eixo 6: DI e Lifecycle              ░░░░░░░░░░ 0%

Fase 3: Polimento (Sprint 4-5) ░░░░░░░░░░ 0%
├─ Eixo 5: Organização Estrutural      ░░░░░░░░░░ 0%
├─ Eixo 7: Error Handling              ░░░░░░░░░░ 0%
└─ Eixo 8: Documentation               ░░░░░░░░░░ 0%
```

---

## 🎯 Fase 1: Fundação (Sprint 1-2)

### Eixo 1: Abstração de Providers

| Task | Status | Effort | Owner | Sprint |
|------|--------|--------|-------|--------|
| Criar ISqlDialect interface | `⬜ TODO` | 1d | - | 1 |
| Implementar PostgresDialect | `⬜ TODO` | 1.5d | - | 1 |
| Implementar SqlServerDialect | `⬜ TODO` | 1.5d | - | 1 |
| Implementar MySqlDialect | `⬜ TODO` | 1.5d | - | 1 |
| Refatorar BaseDbOrchestrator | `⬜ TODO` | 2d | - | 1 |
| Testes de dialetos | `⬜ TODO` | 1.5d | - | 1 |
| **Subtotal Eixo 1** | | **9 dias** | | |

**Entrada no Código:**
- [src/VisualSqlArchitect/Providers/Dialects/ISqlDialect.cs](../src/VisualSqlArchitect/Providers/Dialects/ISqlDialect.cs) (novo)
- [src/VisualSqlArchitect/Providers/Dialects/PostgresDialect.cs](../src/VisualSqlArchitect/Providers/Dialects/PostgresDialect.cs) (novo)
- [src/VisualSqlArchitect/Providers/Dialects/SqlServerDialect.cs](../src/VisualSqlArchitect/Providers/Dialects/SqlServerDialect.cs) (novo)
- [src/VisualSqlArchitect/Providers/Dialects/MySqlDialect.cs](../src/VisualSqlArchitect/Providers/Dialects/MySqlDialect.cs) (novo)
- [src/VisualSqlArchitect/Core/BaseDbOrchestrator.cs](../src/VisualSqlArchitect/Core/BaseDbOrchestrator.cs) (refator)

**Checklist de Completude:**
- [ ] Todas as dialetos implementadas (3x)
- [ ] BaseDbOrchestrator usa Dialect
- [ ] Tests coverage ≥80%
- [ ] Queries em dialetos testadas sem DB real

---

### Eixo 2: Abstração de Metadata

| Task | Status | Effort | Owner | Sprint |
|------|--------|--------|-------|--------|
| Criar IMetadataQueryProvider | `⬜ TODO` | 1d | - | 1-2 |
| Implementar SqlServerMetadataQueries | `⬜ TODO` | 1.5d | - | 2 |
| Implementar PostgresMetadataQueries | `⬜ TODO` | 1.5d | - | 2 |
| Implementar MySqlMetadataQueries | `⬜ TODO` | 1.5d | - | 2 |
| Integrar em BaseDbOrchestrator | `⬜ TODO` | 1d | - | 2 |
| Refatorar MetadataService | `⬜ TODO` | 1d | - | 2 |
| Testes parametrizados | `⬜ TODO` | 1.5d | - | 2 |
| **Subtotal Eixo 2** | | **9 dias** | | |

**Entrada no Código:**
- [src/VisualSqlArchitect/Metadata/IMetadataQueryProvider.cs](../src/VisualSqlArchitect/Metadata/IMetadataQueryProvider.cs) (novo)
- [src/VisualSqlArchitect/Metadata/Inspectors/SqlServerMetadataQueries.cs](../src/VisualSqlArchitect/Metadata/Inspectors/SqlServerMetadataQueries.cs) (novo)
- [src/VisualSqlArchitect/Metadata/MetadataService.cs](../src/VisualSqlArchitect/Metadata/MetadataService.cs) (refator)

---

### Eixo 4: Infraestrutura de Testes

| Task | Status | Effort | Owner | Sprint |
|------|--------|--------|-------|--------|
| Setup Testcontainers | `⬜ TODO` | 1d | - | 1 |
| Criar ProviderTestFixture | `⬜ TODO` | 2d | - | 1 |
| Criar fixtures base (seed data) | `⬜ TODO` | 1d | - | 1 |
| Testes parametrizados para providers | `⬜ TODO` | 2d | - | 2 |
| Testes de query compilation | `⬜ TODO` | 1.5d | - | 2 |
| CI/CD pipeline setup | `⬜ TODO` | 1d | - | 2 |
| **Subtotal Eixo 4** | | **8.5 dias** | | |

**Entrada no Código:**
- [tests/VisualSqlArchitect.Tests/Unit/Providers/ProviderTestFixture.cs](../tests/VisualSqlArchitect.Tests/Unit/Providers/ProviderTestFixture.cs) (novo)
- [tests/VisualSqlArchitect.Tests/Unit/Providers/ProviderSchemaDiscoveryTests.cs](../tests/VisualSqlArchitect.Tests/Unit/Providers/ProviderSchemaDiscoveryTests.cs) (novo)

---

## 🎯 Fase 2: Query Engine (Sprint 3)

### Eixo 3: Consolidação Query Building

| Task | Status | Effort | Owner | Sprint |
|------|--------|--------|-------|--------|
| Criar IQueryBuilder interface | `⬜ TODO` | 1d | - | 3 |
| Implementar SqlKataQueryBuilder | `⬜ TODO` | 2.5d | - | 3 |
| Refatorar QueryBuilderService | `⬜ TODO` | 1.5d | - | 3 |
| Unificar FunctionRegistry | `⬜ TODO` | 2d | - | 3 |
| Tests para IQueryBuilder | `⬜ TODO` | 1.5d | - | 3 |
| **Subtotal Eixo 3** | | **8.5 dias** | | |

**Entrada no Código:**
- [src/VisualSqlArchitect/QueryEngine/Builders/IQueryBuilder.cs](../src/VisualSqlArchitect/QueryEngine/Builders/IQueryBuilder.cs) (novo)
- [src/VisualSqlArchitect/QueryEngine/Builders/SqlKataQueryBuilder.cs](../src/VisualSqlArchitect/QueryEngine/Builders/SqlKataQueryBuilder.cs) (novo)
- [src/VisualSqlArchitect/QueryEngine/QueryBuilderService.cs](../src/VisualSqlArchitect/QueryEngine/QueryBuilderService.cs) (refator)
- [src/VisualSqlArchitect/Registry/SqlFunctionRegistry.cs](../src/VisualSqlArchitect/Registry/SqlFunctionRegistry.cs) (refator)

---

### Eixo 6: Dependency Injection e Lifecycle

| Task | Status | Effort | Owner | Sprint |
|------|--------|--------|-------|--------|
| Criar ProviderRegistry | `⬜ TODO` | 1d | - | 3 |
| Criar extension methods ServiceCollection | `⬜ TODO` | 1.5d | - | 3 |
| Refatorar ServiceRegistration | `⬜ TODO` | 1d | - | 3 |
| Criar factories dinâmicas | `⬜ TODO` | 1.5d | - | 3 |
| Testes de DI container | `⬜ TODO` | 1d | - | 3 |
| **Subtotal Eixo 6** | | **6 dias** | | |

**Entrada no Código:**
- [src/VisualSqlArchitect/ServiceRegistration.cs](../src/VisualSqlArchitect/ServiceRegistration.cs) (refator)
- [src/VisualSqlArchitect/Providers/ProviderRegistry.cs](../src/VisualSqlArchitect/Providers/ProviderRegistry.cs) (novo)

---

## 🎯 Fase 3: Polimento (Sprint 4-5)

### Eixo 5: Organização Estrutural

| Task | Status | Effort | Owner | Sprint |
|------|--------|--------|-------|--------|
| Flatten Expressions hierarchy | `⬜ TODO` | 1d | - | 4 |
| Consolidar Nodes/Expressions | `⬜ TODO` | 2d | - | 4 |
| Atualizar imports | `⬜ TODO` | 1d | - | 4 |
| **Subtotal Eixo 5** | | **4 dias** | | |

---

### Eixo 7: Error Handling

| Task | Status | Effort | Owner | Sprint |
|------|--------|--------|-------|--------|
| Criar exception hierarchy | `⬜ TODO` | 1.5d | - | 4 |
| Implementar ResilientDbOrchestrator | `⬜ TODO` | 2d | - | 4 |
| Adicionar Polly policies | `⬜ TODO` | 1.5d | - | 5 |
| Logging estruturado | `⬜ TODO` | 1d | - | 5 |
| **Subtotal Eixo 7** | | **6 dias** | | |

**Entrada no Código:**
- [src/VisualSqlArchitect/Exceptions/VisualSqlArchitectException.cs](../src/VisualSqlArchitect/Exceptions/VisualSqlArchitectException.cs) (novo)
- [src/VisualSqlArchitect/Core/ResilientDbOrchestrator.cs](../src/VisualSqlArchitect/Core/ResilientDbOrchestrator.cs) (novo)

---

### Eixo 8: Documentação

| Task | Status | Effort | Owner | Sprint |
|------|--------|--------|-------|--------|
| Criar ADRs (Architecture Decision Records) | `⬜ TODO` | 1.5d | - | 5 |
| Gerar API docs (docfx) | `⬜ TODO` | 0.5d | - | 5 |
| Extension guide para providers | `⬜ TODO` | 1d | - | 5 |
| **Subtotal Eixo 8** | | **3 dias** | | |

**Entrada no Código:**
- [docs/adr/](../docs/adr/) (novo)
  - ADR-001-SqlKata-Selection.md
  - ADR-002-ISqlDialect-Strategy.md
  - ADR-003-IQueryBuilder-Abstraction.md
  - ADR-004-Provider-Registry.md

---

## � Setup Inicial (PRÉ-SPRINT)

### Pre-Commit Hooks com CSharpier

| Task | Status | Effort | Owner |
|------|--------|--------|-------|
| Instalar CSharpier (`dotnet tool install CSharpier --global`) | `⬜ TODO` | 0.5h | DevOps |
| Instalar Husky (`dotnet tool install husky --global`) | `⬜ TODO` | 0.5h | DevOps |
| Configurar `.husky/pre-commit` | `⬜ TODO` | 0.5h | DevOps |
| Testar pre-commit hook manualmente | `⬜ TODO` | 0.5h | Engenheiro |
| Documentar no SETUP_PRECOMMIT_HOOKS.md | ✅ DONE | - | - |
| Configurar CI/CD para validar formatting | `⬜ TODO` | 1h | DevOps |

**Objetivo:** Garantir formatação consistente em todos os commits

**Documentação:** Ver [SETUP_PRECOMMIT_HOOKS.md](./SETUP_PRECOMMIT_HOOKS.md)

---

## 📋 Sprint Planning

### Sprint 1 (Semana 1-2)

**Objetivo:** Fundação - Provider abstraction + Test infrastructure setup

```
Seg 30-Mar | SETUP: Configurar pre-commit hooks + CSharpier
Ter 31-Mar | Abstrair ISqlDialect, começar implementations
Qua 01-Abr | PostgresDialect + SqlServerDialect
Qui 02-Abr | MySqlDialect, refatorar BaseDbOrchestrator
Sex 03-Abr | Setup Testcontainers + ProviderTestFixture
Seg 06-Abr | Code review, validação, testes
```

**Deliverables:**
- [ ] Pre-commit hooks configurados e testados
- [ ] ISqlDialect + 3 implementações
- [ ] BaseDbOrchestrator refatorado
- [ ] Testcontainers configurado
- [ ] Tests coverage ≥70%

---

### Sprint 2 (Semana 3-4)

**Objetivo:** Metadata abstraction + parametrized tests

```
Seg 06-Abr | IMetadataQueryProvider + implementations
Ter 07-Abr | MetadataService refactor
Qua 08-Abr | Testes parametrizados (3 providers)
Qui 09-Abr | CI/CD pipeline setup
Sex 10-Abr | Code review, validação
```

**Deliverables:**
- [ ] IMetadataQueryProvider + 3 implementações
- [ ] MetadataService consolidado
- [ ] Testes coverage ≥80%
- [ ] CI/CD executando (GitHub Actions / Azure Pipelines)

---

### Sprint 3 (Semana 5)

**Objetivo:** Query engine refactor + DI consolidation

```
Seg 13-Abr | IQueryBuilder + SqlKataQueryBuilder
Ter 14-Abr | QueryBuilderService + FunctionRegistry unify
Qua 15-Abr | ProviderRegistry + ServiceCollection extension
Qui 16-Abr | DI factories dinâmicas
Sex 17-Abr | Code review, testes
```

**Deliverables:**
- [ ] IQueryBuilder implementado
- [ ] QueryBuilderService testável isoladamente
- [ ] DI container 100% configurável
- [ ] Testes coverage ≥85%

---

### Sprint 4-5 (Semana 6-7)

**Objetivo:** Polimento, error handling, documentação

```
Seg 20-Abr | Flatten hierarchies + Consolidate Nodes
Ter 21-Abr | Exception hierarchy + ResilientDbOrchestrator
Qua 22-Abr | Polly policies integration
Qui 23-Abr | Criar ADRs + API docs
Sex 24-Abr | Code review final, release prep
```

**Deliverables:**
- [ ] Codebase refatorado 100%
- [ ] Testes coverage ≥50%+
- [ ] ADRs documentadas
- [ ] API reference gerada

---

## 🔍 Métricas de Sucesso

### Quantitativas

| Métrica | Baseline | Target | Sprint |
|---------|----------|--------|--------|
| **Lines of Code (duplicação)** | ~8,000 | ~5,500 | 2 |
| **Cyclomatic Complexity** | avg 4.2 | avg 2.8 | 3 |
| **Test Coverage** | ~15% | ~50% | 3 |
| **Build Time (Release)** | ~45s | ~25s | 3 |
| **Time-to-new-provider** | 5 dias | 2 dias | 3 |

### Qualitativas

| Aspecto | Antes | Depois |
|---------|-------|--------|
| **Discoverability** | Hierarquias profundas | Fluxo linear claro |
| **Onboarding Dev** | 1-2 semanas | 2-3 dias |
| **Provider extensibility** | Copy-paste 5+ files | Implementar 1 interface |
| **Test isolation** | Requer DB real | Testável com mocks |
| **Query predictability** | Snapshot manual | Snapshot automático |

---

## 🚨 Riscos & Mitigações

### Risco Alto: Regressão em Providers Existentes

**Mitigation:**
- Testes parametrizados ANTES de refator
- Manter providers antigos em paralelo durante transição
- Code review obrigatório (2 aprovadores)
- Smoke tests em staging com dados reais

**Owner:** Engenheiro de Qualidade
**Timeline:** Sprint 1

---

### Risco Médio: Aumentar Complexidade Temporariamente

**Mitigation:**
- Refactors pequenos (PR <400 linhas)
- Documentação inline clara
- Daily standup com revisão de progresso
- "Feature flags" para desabilitar se necessário

**Owner:** Tech Lead
**Timeline:** All sprints

---

### Risco Médio: Equipe desatualizada em mudanças

**Mitigation:**
- ADRs criadas DURANTE desenvolvimento (não depois)
- Code walkthroughs em team meetings
- Slack thread documentando decisões
- Wiki atualizado com exemplos

**Owner:** Arquiteto
**Timeline:** Each sprint

---

## 📞 Checkpoints

### End-of-Sprint Reviews

```
Sprint 1 (03-Abr): ISqlDialect + BaseDbOrchestrator refactor
  ✓ Dialects funcionando
  ✓ Tests verdes (no DB real)
  ✓ Nenhuma regressão em orchestrators

Sprint 2 (10-Abr): IMetadataQueryProvider + Testes parametrizados
  ✓ Metadata consolidada
  ✓ Tests rodando em 3 providers (Docker)
  ✓ CI/CD automático

Sprint 3 (17-Abr): IQueryBuilder + DI consolidation
  ✓ QueryBuilder agnóstico de SqlKata
  ✓ DI container 100% funcional
  ✓ Testes > 85% coverage

Sprint 4-5 (24-Abr): Polimento + Documentação
  ✓ Codebase refatorado
  ✓ Error handling robusto
  ✓ ADRs + API docs completos
```

---

## 🎓 Knowledge Base

### Documentação Criada

- [REFACTORING_ROADMAP.md](./REFACTORING_ROADMAP.md) — Overview estratégico
- [IMPLEMENTATION_EXAMPLES.md](./IMPLEMENTATION_EXAMPLES.md) — Exemplos práticos (este arquivo)
- [docs/adr/](../docs/adr/) — Architecture Decision Records
- [docs/PROVIDER_EXTENSION_GUIDE.md](../docs/PROVIDER_EXTENSION_GUIDE.md) — Como adicionar novo provider

### Referências Externas

- [Refactoring Guru - Patterns](https://refactoring.guru)
- [Clean Code - Robert Martin](https://www.oreilly.com/library/view/clean-code-a/9780136083238/)
- [xUnit Docs](https://xunit.net/docs/getting-started)
- [Testcontainers.DotNet](https://github.com/testcontainers/testcontainers-dotnet)
- [Polly Resilience Library](https://github.com/App-vNext/Polly)

---

**Last Updated:** 26 de março de 2026
**Next Review:** 30 de março de 2026 (Sprint Planning)
