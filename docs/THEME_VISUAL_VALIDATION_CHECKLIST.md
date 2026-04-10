# Theme Visual Validation Checklist

Objetivo: validar criterios finais de aceite com tema customizado via JSON.

## Preparacao

1. Copiar o arquivo [src//Themes/user-theme.high-contrast.sample.json](src/DBWeaver.Uer-theme.high-contrast.sample.json).
2. Substituir temporariamente o conteudo de [src//Themes/user-theme.json](src/DBWeaver.Uer-theme.json) pelo tema de contraste.
3. Executar a aplicacao normalmente.

## Checklist (aprovacao visual)

- [ ] App inicia sem erro e sem regressao funcional no shell.
- [ ] Background macro muda de forma perceptivel (janela/shell/sidebar).
- [ ] Tipografia permanece legivel em tabs, headers e listas.
- [ ] Botao primario permanece com contraste adequado (texto legivel).
- [ ] Botao warning permanece com contraste adequado (texto legivel).
- [ ] Focus-visible continua evidente em tabs e campos de busca da sidebar.
- [ ] Estados disabled continuam legiveis no tema customizado.
- [ ] Nao ha alteracao visual indevida em nodes/pins/wires.

## Validacao de fallback

1. Introduzir propositalmente um valor invalido em `user-theme.json` (ex.: `macroBg0: "invalid"`).
2. Reiniciar app.
3. Confirmar:
- [ ] app continua abrindo (sem crash),
- [ ] apenas chave invalida e ignorada,
- [ ] restante do tema continua aplicado.

## Resultado final

- [ ] Criterios de aceite finais aprovados.
- [ ] Retornar [src//Themes/user-theme.json](src/DBWeaver.Uer-theme.json) para o perfil desejado (default ou customizado).
