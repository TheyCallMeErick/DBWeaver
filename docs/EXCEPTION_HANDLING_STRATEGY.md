# Estratégia de Tratamento de Exceções — 2026-04-01
Escopo: domínio, infraestrutura e UI

## Objetivo

Padronizar como falhas são propagadas, logadas e exibidas ao usuário para evitar combinações contraditórias entre módulos.

## Contrato por camada

1. Domínio/Core (`ações de validação e configuração inválida devem lançar exceção.
- Operações de execução tolerante (ex.: preview/teste de conexão) podem retornar `Result` com `Success=false` quando a falha for operacional esperada.
- Nunca engolir exceções sem transformar em sinal explícito (`Result` ou evento de warning).

2. Infraestrutura/Serviços (`es`)
- Serviços de execução devem usar `ILogger<T>` para logs estruturados.
- Exceções inesperadas devem ser logadas em `LogError` e repropagadas quando o chamador tiver contexto para decisão de UX.
- Em persistência local de melhor-esforço, emitir warning observável (evento/callback) em vez de falha silenciosa.

3. UI/ViewModels
- A UI é o limite de apresentação: capturar exceções e converter para mensagem amigável (`DataPreview.ShowError` ou status equivalente).
- Não expor stack trace ao usuário final.

## Regras práticas

- `ArgumentException` / `InvalidOperationException`: para uso incorreto de API e pré-condições.
- `OperationCanceledException`: não tratar como erro; registrar em nível `Information` e encerrar fluxo.
- `Exception` genérica: somente no boundary de serviço/UI, sempre com log estruturado.

## Logging

- `LogDebug`: detalhe técnico de execução.
- `LogInformation`: marco funcional e cancelamento esperado.
- `LogWarning`: degradação controlada, fallback, dados corrompidos recuperáveis.
- `LogError`: falha inesperada com exceção.

## Padrões de retorno recomendados

- Core read-model e probes: `Result` (`Success`, `ErrorMessage`, métricas).
- Serviços de execução síncronos/assíncronos críticos: lançar após log para o boundary de UI.
- Persistência local best-effort: retornar fallback + `WarningRaised`.

## Checklist para PR

- Existe um boundary claro de captura de exceção?
- Há log estruturado no nível correto?
- Há consistência com o contrato da camada?
- Existe teste cobrindo o caminho de falha?
