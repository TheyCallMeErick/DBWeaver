# Theme JSON Schema (Fase 5)

Arquivo padrao: `src//Themes/user-theme.json`

Objetivo:
- Permitir customizacao de tokens macro sem editar AXAML.
- Manter fallback seguro para valores invalidos.

## Estrutura

```json
{
  "meta": {
    "name": "Studio Dark",
    "version": "1.0"
  },
  "colors": {
    "macroBg0": "#0B0E14",
    "macroBg1": "#101523",
    "macroBg2": "#141B2D",
    "textPrimary": "#E8EAED",
    "textSecondary": "#9CA3AF",
    "btnPrimaryBg": "#2563EB",
    "btnPrimaryFg": "#F8FAFC",
    "btnWarningBg": "#7C2D12",
    "btnWarningFg": "#FED7AA"
  },
  "typography": {
    "uiFont": "Sora,Manrope,Segoe UI,Arial,sans-serif",
    "monoFont": "JetBrains Mono,Cascadia Code,Consolas,monospace",
    "titleSize": 13,
    "bodySize": 12,
    "metaSize": 11
  }
}
```

## Mapeamento de chaves

- `colors.macroBg0` -> `MacroBg0`, `MacroBg0Brush`
- `colors.macroBg1` -> `MacroBg1`, `MacroBg1Brush`
- `colors.macroBg2` -> `MacroBg2`, `MacroBg2Brush`
- `colors.textPrimary` -> `TextPrimary`, `TextPrimaryBrush`
- `colors.textSecondary` -> `TextSecondary`, `TextSecondaryBrush`
- `colors.btnPrimaryBg` -> `BtnPrimaryBg`, `BtnPrimaryBgBrush`
- `colors.btnPrimaryFg` -> `BtnPrimaryFg`, `BtnPrimaryFgBrush`
- `colors.btnWarningBg` -> `BtnWarningBg`, `BtnWarningBgBrush`
- `colors.btnWarningFg` -> `BtnWarningFg`, `BtnWarningFgBrush`
- `typography.uiFont` -> `UIFont`
- `typography.monoFont` -> `MonoFont`
- `typography.titleSize` -> `FontSizeTitle`
- `typography.bodySize` -> `FontSizeBody`
- `typography.metaSize` -> `FontSizeMeta`

## Regras de seguranca

- Arquivo ausente: aplica tema padrao (sem erro).
- JSON invalido: fallback para tema padrao.
- Chave invalida (cor/tamanho): ignorada individualmente com warning.
- Tokens sem valor valido permanecem no padrao.
- Escopo atual: apenas tokens macro/shell; nao altera tokens de nodes/wires.
