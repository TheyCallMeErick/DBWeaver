# Setup Pre-Commit Hooks - Visual SQL Architect

**Data:** 26 de março de 2026
**Objetivo:** Garantir formatação consistente com CSharpier antes de cada commit

---

## 📋 Pre-Requisitos

### 1. Instalar CSharpier

```bash
dotnet tool install CSharpier --global
```

Ou atualizar se já instalado:
```bash
dotnet tool update CSharpier --global
```

Verificar instalação:
```bash
dotnet csharpier --version
```

### 2. Instalar Husky (Gerenciador de Git Hooks)

```bash
dotnet tool install husky --global
```

### 3. Ferramentas Necessárias

```bash
# Windows
choco install git  # Se não tiver Git instalado

# macOS
brew install git
```

---

## 🚀 Configuração Inicial

### Passo 1: Clonar ou Navegar ao Repositório

```bash
cd c:\Users\azeve\Documents\VisualSqlArchtect
```

### Passo 2: Inicializar Git Hooks com Husky

```bash
husky install
```

Isto criará a pasta `.husky/` no repositório.

### Passo 3: Criar Pre-Commit Hook

O arquivo `.husky/pre-commit` já foi criado no repositório.

Se precisar recriar:

```bash
husky add .husky/pre-commit "dotnet csharpier format ."
```

### Passo 4: Tornar o Hook Executável

**Windows (PowerShell como Admin):**
```powershell
chmod +x .husky/pre-commit
```

**macOS/Linux:**
```bash
chmod +x .husky/pre-commit
```

---

## ✅ Validar Setup

### Testar o Hook Manualmente

```bash
# Fazer uma mudança em qualquer arquivo .cs
echo "// test" >> src/VisualSqlArchitect/ServiceRegistration.cs

# Tentar commit (deve rodar CSharpier automaticamente)
git add .
git commit -m "test: validar pre-commit hook"
```

Se o hook funcionou corretamente:
- ✅ CSharpier formatou o arquivo
- ✅ Commit foi aceito
- ✅ Mensagem: "✅ CSharpier formatting applied successfully"

### Verificar Hook Status

```bash
# Ver o conteúdo do hook
cat .husky/pre-commit

# Verificar permissões (Windows)
Get-Item .husky/pre-commit | Select-Object Mode
```

---

## 🔧 Troubleshooting

### Problema: "CSharpier command not found"

**Solução:**
```bash
# Reinstalar globalmente
dotnet tool uninstall CSharpier --global
dotnet tool install CSharpier --global

# Ou usar localmente
dotnet tool install CSharpier --local
```

### Problema: Hook não executa

**Verificar:**
```bash
# 1. Husky está instalado?
husky --version

# 2. Reinicializar Husky
husky install

# 3. Verificar permissões do arquivo
ls -la .husky/pre-commit  # macOS/Linux
Get-Item .husky/pre-commit  # Windows
```

### Problema: Commit falha após formatação

**Isso é esperado na primeira execução.**

O hook formata os arquivos, mas o commit falha porque os arquivos foram modificados. Solução:

```bash
# 1. Adicionar novamente os arquivos formatados
git add .

# 2. Tentar commit novamente
git commit -m "seu mensagem"
```

### Problema: Quer fazer skip do hook (excepcional)

```bash
# Usar --no-verify para pular o hook
git commit -m "mensagem" --no-verify
```

⚠️ **Não recomendado!** Use apenas em emergências.

---

## 📝 Configuração Alternativa: .pre-commit-config.yaml

Se preferir usar `pre-commit` (framework independente):

```yaml
# .pre-commit-config.yaml
repos:
  - repo: https://github.com/CSharpier/CSharpier
    rev: v0.24.0
    hooks:
      - id: csharpier
        language: dotnet
        entry: dotnet csharpier format --write-stdout
        types: [csharp]
```

Setup:
```bash
pip install pre-commit
pre-commit install
pre-commit run --all-files
```

---

## 🎯 Fluxo de Trabalho Diário

### Antes de Começar Refatoração

1. **Setup (uma única vez):**
   ```bash
   cd projeto/
   husky install
   dotnet csharpier --version
   ```

2. **Fazer mudanças:**
   ```bash
   git checkout -b refactor/eixo-1-provider-abstraction
   # ... editar arquivos .cs ...
   ```

3. **Commitar (automático):**
   ```bash
   git add .
   git commit -m "refactor: implementar ISqlDialect"
   # Hook executa CSharpier automaticamente ✨
   ```

### Resultados do Hook

```
$ git commit -m "refactor: teste"

🔍 Running CSharpier format check...
✅ CSharpier formatting applied successfully
[refactor/eixo-1 a1b2c3d] refactor: teste
 1 file changed, 2 insertions(+), 2 deletions(-)
```

---

## 🔐 CI/CD Integration

### Validar Formatting em Pipeline

**GitHub Actions:**
```yaml
name: Code Quality

on: [push, pull_request]

jobs:
  csharpier:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0'
      - run: dotnet tool install CSharpier --global
      - run: dotnet csharpier --check .
```

**Azure Pipelines:**
```yaml
trigger:
  - main
  - develop

pool:
  vmImage: 'ubuntu-latest'

steps:
  - task: UseDotNet@2
    inputs:
      version: '9.0.x'
  - script: dotnet tool install CSharpier --global
  - script: dotnet csharpier --check .
    displayName: 'CSharpier Format Check'
```

---

## 📚 Referências

- [CSharpier Documentation](https://csharpier.io/)
- [Husky Git Hooks](https://typicode.github.io/husky/)
- [pre-commit Framework](https://pre-commit.com/)
- [Git Hooks Documentation](https://git-scm.com/docs/githooks)

---

## ✅ Checklist de Setup

- [ ] CSharpier instalado (`dotnet csharpier --version`)
- [ ] Husky instalado (`husky --version`)
- [ ] Repositório inicializado (`git init`)
- [ ] `.husky/` folder criada (`husky install`)
- [ ] `.husky/pre-commit` existe e é executável
- [ ] Testado manualmente (commit com mudanças)
- [ ] CI/CD pipeline valida formatting

---

## 🎓 Próximos Passos

Após completar setup:

1. ✅ **Refatoração pronta para começar** (Sprint 1)
2. ✅ **Todo commit será formatado automaticamente**
3. ✅ **Código consistente em toda equipe**
4. ✅ **CI/CD garante padrão em PRs**

---

**Status:** ✅ Ready to use
**Data:** 26 de março de 2026

