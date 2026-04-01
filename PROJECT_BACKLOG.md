# Project Backlog - Source of Truth

Updated: 2026-03-31
Owner: Core team

---

## Purpose

This file is the canonical backlog for current execution.

When status conflicts exist across documents, use this precedence:
1. `PROJECT_BACKLOG.md` (this file)
2. `README.md`
3. legacy planning/status docs under `docs/refactoring/` and `BUG_FIXES_AND_FEATURES.md`

---

## Active

### P0 - Execution now

1. **Complex queries completion (highest priority)**
   - Scope:
     - Execute the roadmap in `docs/COMPLEX_QUERIES_ROADMAP.md` as the top delivery track
     - Conclude critical gaps in sequence: typed window hierarchy, window OVER aggregates, explicit join node
     - Continue with subqueries and set operations in roadmap order
   - Success criteria:
     - Reference complex query can be produced through the visual flow without manual SQL patches
     - Roadmap completion criteria are checked as done
   - Primary references:
     - `docs/COMPLEX_QUERIES_ROADMAP.md`

2. **Close Wire Sync investigation and cleanup debug traces**
   - Scope:
     - Confirm final behavior for wire sync during node drag
     - Remove temporary debug instrumentation from canvas/wire rendering flow
     - Keep build and tests green
   - Primary references:
     - `docs/refactoring/EIXO_8_STATUS.md`
     - `docs/refactoring/EIXO_8_RESUMO.md`

3. **Normalize status docs to current reality**
   - Scope:
     - Update or archive stale documents still marked as `TODO`/`EM PROGRESSO`
     - Keep one short “current state” summary synced with codebase
   - Primary references:
     - `docs/refactoring/EIXO_8_*`
     - `PROJECT_BACKLOG.md`

### P1 - Next increments

4. **Typed pins roadmap hardening**
   - Scope:
     - Plan migration away from broad polymorphism to robust structural typing (`ColumnRef`, `ColumnSet`, `RowSet`)
     - Define phased adoption strategy compatible with existing nodes
   - Primary references:
     - `docs/TYPE_SYSTEM_ROADMAP.md`

### P2 - Operational quality

5. **Pre-commit and formatting governance verification**
   - Scope:
     - Verify hooks/toolchain are fully active for all contributors
     - Ensure CI formatting validation is consistently enforced
   - Primary references:
     - `docs/refactoring/SETUP_PRECOMMIT_HOOKS.md`
     - `.husky/`

---

## Completed (recent/high confidence)

- README modernization and bilingual structure with shared Mermaid diagrams
  - Reference: `README.md`
- Build baseline healthy in current workspace
  - Reference: `files.sln` build output (local)
- Legacy bug-fix plan consolidated into this backlog source of truth
  - Reference: `PROJECT_BACKLOG.md`

---

## Archived or Legacy Planning

Legacy planning docs were retired from active tracking on 2026-03-31.

Removed from repository:

- `docs/refactoring/REFACTORING_ROADMAP.md`
- `docs/refactoring/STATUS_AND_TRACKING.md`
- `docs/refactoring/QUICK_REFERENCE.md`
- `BUG_FIXES_AND_FEATURES.md`

---

## Working Agreement

- Only add new work items here.
- Any completed item must be moved from **Active** to **Completed** in the same PR.
- If a legacy document is updated, mirror the final status here in the same change.
