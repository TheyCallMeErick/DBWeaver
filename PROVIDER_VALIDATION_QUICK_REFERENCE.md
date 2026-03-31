# Provider Validation: Quick Reference Guide

## 🎯 Key Findings

### 1. Blocklist (SQL Server Only)
Only **2 functions** are truly blocked per provider:
```
SQL Server BLOCKLIST:
  ❌ REGEX_REPLACE  → Use CLR function or switch provider
  ❌ REGEX_EXTRACT  → Use CLR function or switch provider

PostgreSQL & MySQL:
  ✓ Full support for both functions
```

### 2. Validation Happens in STAGES

| Stage | When | Where | Result |
|-------|------|-------|--------|
| **Pre-Compile** | Canvas changes (real-time) | `QueryValidationService.CheckPortability()` | GuardIssue.Block shown in UI |
| **SQL Generation** | Compile phase | `SqlFunctionRegistry.GetFunction()` | NotSupportedException thrown |
| **Preview Guard** | Before execution | `PreviewService.RunPreviewAsync()` | Blocks if mutating or warnings |
| **Runtime** | Query execution | Database engine | Database error (+ diagnostic classification) |

### 3. Canvas Prevents Invalid Queries

**Before** SQL is even generated, the UI tells you:
- ❌ What functions aren't supported
- 💡 How to fix it (switch provider or use alternative)
- 🚫 Preview is blocked until issue resolved

```csharp
// This happens in real-time:
if (NodeType.RegexReplace is in canvas && Provider == SqlServer)
  => Add GuardIssue("REGEX_REPLACE not supported on SQL Server")
  => Block preview execution
```

---

## 📍 Code Locations

### Blocklist Definition
[SqlFunctionRegistry.cs](src/VisualSqlArchitect/Registry/SqlFunctionRegistry.cs#L123-L155)
```csharp
UnsupportedByProvider[DatabaseProvider.SqlServer] =
    new HashSet { SqlFn.RegexReplace, SqlFn.RegexExtract }
```

### Pre-Compilation Check
[QueryValidationService.cs](src/VisualSqlArchitect.UI/ViewModels/QueryPreview/Services/QueryValidationService.cs#L54-L85)
- Maps canvas nodes → canonical functions
- Calls `registry.CheckPortability(functions)`
- Returns GuardIssues with actionable suggestions

### Function Mapping
[SqlFunctionRegistry.cs](src/VisualSqlArchitect/Registry/SqlFunctionRegistry.cs#L189-L450)
- `PostgresMap()` — Full Postgres syntax
- `MySqlMap()` — Full MySQL syntax
- `SqlServerMap()` — SQL Server syntax (some functions throw NotSupportedException)

### LiveSQL Bar Integration
[LiveSqlBarViewModel.cs](src/VisualSqlArchitect.UI/ViewModels/LiveSqlBarViewModel.cs#L204-L215)
- Calls `CheckPortability()` on each recompile
- Populates `GuardIssues` observable collection
- UI watches for `HasGuardWarning`

### Safe Preview Mode
[PreviewService.cs](src/VisualSqlArchitect.UI/Services/PreviewService.cs#L148-L200)
- Blocks if `IsMutatingCommand`
- Logs warnings as debug output (non-blocking)
- Executes with 1000-row limit + auto-rollback

---

## 🔧 Function Fragment System

Each provider has provider-specific syntax for common operations:

### REGEX Examples
```sql
PostgreSQL: column ~ 'pattern'
MySQL:      column REGEXP 'pattern'
SQL Server: PATINDEX('pattern', column) > 0
```

### JSON_EXTRACT Examples
```sql
PostgreSQL: column->>'key'
MySQL:      JSON_EXTRACT(column, '$.key')
SQL Server: JSON_VALUE(column, '$.key')
```

### DATE_DIFF Examples
```sql
PostgreSQL: EXTRACT(DAY FROM (to::timestamp - from::timestamp))
MySQL:      TIMESTAMPDIFF(DAY, from, to)
SQL Server: DATEDIFF(DAY, from, to)
```

---

## 🛡️ Safe Preview Mode

Prevents accidental data mutations by blocking:
```
❌ INSERT  ❌ UPDATE  ❌ DELETE  ❌ DROP  ❌ ALTER  ❌ TRUNCATE  ❌ CREATE  ❌ REPLACE  ❌ MERGE
```

Detection: Uses keyword pattern matching (case-insensitive, word-boundary aware)

---

## 🚨 Error Classification

When execution fails, errors are categorized:

| Category | Pattern Match | Suggestion |
|----------|---------------|-----------|
| **Compatibility** | "function", "not supported", "does not support" | Switch provider or use alternative |
| **Syntax** | "syntax error", "unexpected token", "parse error" | Check SQL typos and parentheses |
| **Schema** | "table", "column", "not found" | Verify table/column names and schema |
| **Connection** | "connection refused", "timeout" | Check host/port and firewall |
| **Authorization** | "permission denied", "access denied" | Verify user privileges |

---

## 📊 Validation Architecture

```
Canvas Changes
    ↓
LiveSqlBarViewModel.Recompile() [120ms debounce]
    ├→ QueryValidationService.CheckPortability()
    │  └→ SqlFunctionRegistry.CheckPortability()
    │     └→ Returns warnings from UnsupportedByProvider dict
    │
    ├→ QueryGraphBuilder.BuildSql()
    │  └→ SqlKataQueryBuilder.Filter()
    │     └→ SqlFunctionRegistry.GetFunction() [may throw]
    │
    ├→ QueryValidationService.IsMutating()
    │  └→ Detects INSERT/UPDATE/DELETE/DROP/ALTER/TRUNCATE
    │
    └→ QueryGuardrails.Check()
       └→ Additional SQL safety checks

      ↓

ObservableCollections Updated
    • GuardIssues (warnings, blocks)
    • ErrorHints (compile errors)
    • Tokens (syntax highlighting)

      ↓

UI Reflects State
    • Show/hide warnings
    • Disable/enable preview button
    • Display error panel
```

---

## 💡 Usage Patterns

### For Users
1. Add a **RegexReplace** node to canvas
2. Switch provider to **SQL Server**
3. ❌ **GuardIssue appears**: "REGEXP_REPLACE not supported"
4. UI shows suggestion: "Use CLR function or switch provider"
5. 🚫 **Preview button disabled** until resolved

### For Developers
- **Add new function**: Define in all 3 maps (`PostgresMap()`, `MySqlMap()`, `SqlServerMap()`)
- **Unsupported function**: Add to `UnsupportedByProvider[provider]` and `PortabilityMessages`
- **New provider**: Extend `SqlFunctionRegistry.CreateFragmentProvider()` + `BuildMap()`
- **Node type**: Map to function in `NodeTypeCanonicalFunctions`

---

## 📝 Summary Table

| Question | Answer | Evidence |
|----------|--------|----------|
| **Blocklist of unsupported functions?** | Yes, SQL Server: RegexReplace, RegexExtract | UnsupportedByProvider dict in SqlFunctionRegistry |
| **Prevent canvas operations before execution?** | Yes, via GuardIssue.Block | CheckPortability() called before SQL generation |
| **Only validate at runtime?** | No, validates at compile time too | Two-stage validation: pre-compile + at-generation |
| **Warnings/errors shown to user?** | Yes, with suggestions | GuardIssues observable + ErrorDiagnostics classifier |
| **Provider-specific functions?** | Yes, different syntax per provider | Dedicated function maps + fragment providers |
| **Can blocklist be extended?** | Yes, easily | Add to UnsupportedByProvider dict + PortabilityMessages |

---

## 🔍 Testing the System

Try these scenarios:

### Scenario 1: SQL Server + Regex
1. Open canvas, add **RegexReplace** node
2. Switch provider to **SQL Server**
→ Expected: GuardIssue appears, preview blocked

### Scenario 2: Multi-Provider JSON
1. Add **JsonExtract** node
2. Try each provider (Postgres, MySQL, SQL Server)
→ Expected: SQL uses provider-specific syntax in each case

### Scenario 3: Mutating Command Guard
1. Type `INSERT INTO table VALUES (...)` in query text
2. Try to execute preview
→ Expected: "Blocked by Safe Preview Mode" message

### Scenario 4: Connection Validation
1. Test with invalid credentials
→ Expected: ActiveConnectionContext.SwitchAsync() throws, shows "Connection failed" diagnostic
