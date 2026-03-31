# 🚀 Quick Start - Pre-Commit Setup

**Execute isto ANTES de começar qualquer refatoração**

---

## ⚡ Setup em 5 Minutos

### Passo 1: Instalar CSharpier

```bash
dotnet tool install CSharpier --global
```

✅ Resultado esperado:
```
Tool 'csharpier' (version X.X.X) was successfully installed.
```

### Passo 2: Instalar Husky

```bash
dotnet tool install husky --global
```

✅ Resultado esperado:
```
Tool 'husky' (version X.X.X) was successfully installed.
```

### Passo 3: Inicializar Git Hooks

```bash
cd c:\Users\azeve\Documents\VisualSqlArchtect
husky install
```

✅ Resultado esperado:
```
husky - Git hooks installed
```

### Passo 4: Testar (Opcional mas Recomendado)

```bash
git commit --allow-empty -m "test: validar setup"
```

✅ Resultado esperado:
```
🔍 Running CSharpier format check...
✅ CSharpier formatting applied successfully
[refactor/eixo-1 abc1234] test: validar setup
```

---

## ✅ Pronto!

Agora todos os commits serão formatados automaticamente:

```
git add .
git commit -m "refactor: fazer algo"
  ↓
🔍 Running CSharpier format check...
✅ CSharpier formatting applied successfully
  ↓
Commit aceito! ✨
```

---

## 🆘 Se Algo der Errado

### "CSharpier command not found"

```bash
# Reinstalar
dotnet tool uninstall CSharpier --global
dotnet tool install CSharpier --global
```

### "Husky not found"

```bash
# Reinstalar
dotnet tool uninstall husky --global
dotnet tool install husky --global
husky install
```

### "Pre-commit hook não executou"

```bash
# Verificar permissões
ls -la .husky/pre-commit
# Deve mostrar: -rwxr-xr-x (com execute permission)
```

---

## 📖 Documentação Completa

→ [SETUP_PRECOMMIT_HOOKS.md](./SETUP_PRECOMMIT_HOOKS.md)

---

**Status:** ✅ Pronto para começar refatoração!

