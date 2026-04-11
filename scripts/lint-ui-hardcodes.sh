#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if [[ $# -gt 0 ]]; then
  mapfile -t CANDIDATES < <(printf '%s\n' "$@")
else
  mapfile -t CANDIDATES < <(git diff --name-only --diff-filter=ACMRTUXB HEAD)
fi

mapfile -t TARGETS < <(
  printf '%s\n' "${CANDIDATES[@]:-}" \
    | rg '^(src/DBWeaver.UI/(Controls|Views|Assets/Themes)/.*\.(axaml|cs))$' -N || true
)

if [[ ${#TARGETS[@]} -eq 0 ]]; then
  echo "[lint-ui-hardcodes] no UI files to validate"
  exit 0
fi

fail=0

check_pattern() {
  local pattern="$1"
  local description="$2"
  local path
  for path in "${TARGETS[@]}"; do
    if [[ "$description" == "legacy teal token/style usage" && "$path" == "src/DBWeaver.UI/Assets/Themes/DesignTokens.axaml" ]]; then
      continue
    fi
    if [[ ! -f "$path" ]]; then
      continue
    fi
    if rg -n --pcre2 "$pattern" "$path" >/dev/null 2>&1; then
      echo "[lint-ui-hardcodes] $description: $path"
      rg -n --pcre2 "$pattern" "$path" | sed 's/^/  -> /'
      fail=1
    fi
  done
}

# Legacy token naming should not be reintroduced in UI controls/views.
check_pattern 'AccentTeal(Light|Mid)?Brush|AccentTeal\b|section-header-teal' 'legacy teal token/style usage'

# Hardcoded visual values that must move to tokens.
check_pattern 'CornerRadius="[1-9][0-9]*' 'hardcoded CornerRadius'
check_pattern '<Setter Property="CornerRadius" Value="[1-9][0-9]*(,[1-9][0-9]*){0,3}"' 'hardcoded CornerRadius setter'
check_pattern 'Foreground="White"' 'hardcoded Foreground white'
check_pattern 'Background="#[0-9A-Fa-f]{3,8}"|Foreground="#[0-9A-Fa-f]{3,8}"|BorderBrush="#[0-9A-Fa-f]{3,8}"' 'hardcoded hex color'
check_pattern 'FontSize="[0-9]+(\.[0-9]+)?"' 'hardcoded FontSize'
check_pattern '<Setter Property="FontSize" Value="[0-9]+(\.[0-9]+)?"' 'hardcoded FontSize setter'
check_pattern 'FontFamily="(?!\{StaticResource (UI|Node|Mono)Font\})[^"]+"' 'hardcoded FontFamily'

if [[ $fail -ne 0 ]]; then
  echo "[lint-ui-hardcodes] failed"
  exit 1
fi

echo "[lint-ui-hardcodes] ok"
