# Convenções de Código — 2026-04-01

## Objetivo

Padronizar estilo mínimo em arquivos novos e arquivos tocados por refactor, reduzindo inconsistência e warnings evitáveis.

## Regras

1. Strings vazias/brancas
- Preferir `string.IsNullOrWhiteSpace` para entradas de usuário, texto de UI e SQL livre.
- Usar `string.IsNullOrEmpty` apenas quando espaço em branco for valor válido.

2. Inicializadores de coleção
- Em código moderno (C# 12), preferir `[]` quando o tipo já estiver explícito no lado esquerdo.
- Em APIs públicas com legibilidade sensível, manter inicializador clássico se melhorar clareza.

3. Nullable reference types
- Tratar warnings de nullability como dívida técnica ativa em arquivos tocados.
- Evitar `!` sem comentário/justificativa.
- Em parâmetros opcionais, preferir anotação explícita (`string?`, `ILogger<T>?`).

4. Logging
- Evitar `Debug.WriteLine` em fluxos de produção.
- Preferir `ILogger<T>` com mensagens estruturadas (`{Token}`).
- Nível recomendado:
  - `LogDebug`: detalhe técnico de execução
  - `LogInformation`: milestones funcionais
  - `LogWarning`: comportamento degradado sem falha total
  - `LogError`: falha com exceção

5. XML docs em API pública
- Toda classe/interface/enum pública nova deve ter `<summary>`.
- Para membros públicos principais, adicionar `<summary>` e `<param>` quando útil.

## Aplicação incremental

- Não reformatar o projeto inteiro de uma vez.
- Aplicar essas regras em cada PR nos arquivos modificados.
- Revisores devem recusar novos `Debug.WriteLine` em caminhos críticos.
