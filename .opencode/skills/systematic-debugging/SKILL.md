---
name: systematic-debugging
description: Structured 4-phase debugging methodology — root cause investigation, pattern analysis, hypothesis testing, and implementation — that blocks symptom-based patching and requires evidence before any fix
license: MIT
compatibility: opencode
metadata:
  source: obra/superpowers
  audience: developers
  workflow: debugging
---

# Systematic Debugging

## Overview

Random fixes waste time and create new bugs. Quick patches mask underlying issues.

**Core principle:** ALWAYS find root cause before attempting fixes. Symptom fixes are failure.

> **Context WebSGV**: Aplica a bugs en C# (code-behind .aspx.cs), stored procedures SQL Server, errores de sesión/roles, fallos de PDF con iTextSharp, y errores de MSBuild.

## The Iron Law

```
NO FIXES WITHOUT ROOT CAUSE INVESTIGATION FIRST
```

If you haven't completed Phase 1, you cannot propose fixes.

## The Four Phases

### Phase 1: Root Cause Investigation

**BEFORE attempting ANY fix:**

1. **Read Error Messages Carefully** — stack traces, line numbers, file paths, error codes
2. **Reproduce Consistently** — exact steps, every time? If not reproducible → gather data, don't guess
3. **Check Recent Changes** — git diff, recent commits, new NuGet packages, config changes
4. **Gather Evidence in Multi-Component Systems**

   For WebSGV, component boundaries to check:
   - Session (`Session["UsuarioID"]`, `Session["Rol"]`) → present and correct value?
   - `RolesHelper.ValidarAccesoSeccion()` → returning expected result?
   - SQL connection (`ConexionSGV` in `connectionStrings.config`) → accessible?
   - Stored procedure → deployed to DB? Correct parameters?
   - iTextSharp / PDF path (`OrdenViaje.RutaArchivo` in `Web.config`) → directory exists?

5. **Trace Data Flow** — where does the bad value originate? Trace backwards up the call stack.

### Phase 2: Pattern Analysis

1. Find working examples of similar code in the codebase
2. Compare against references — read completely, don't skim
3. Identify every difference between working and broken, however small
4. Understand dependencies: config, session state, DB objects

### Phase 3: Hypothesis and Testing

1. **Form Single Hypothesis** — "I think X is the root cause because Y"
2. **Test Minimally** — smallest possible change to test hypothesis, one variable at a time
3. **Verify Before Continuing** — worked? → Phase 4. Didn't work? → new hypothesis. Don't stack fixes.

### Phase 4: Implementation

1. **Create Failing Test Case** — simplest reproduction (manual HTTP request, SQL query, etc.)
2. **Implement Single Fix** — address root cause, ONE change
3. **Verify Fix** — issue resolved? No regressions?
4. **If Fix Doesn't Work** — STOP. Count attempts. If ≥ 3: question the architecture.

## Red Flags — STOP and Follow Process

- "Quick fix for now, investigate later"
- "Just try changing X and see if it works"
- "It's probably X, let me fix that"
- Proposing solutions before tracing data flow
- "One more fix attempt" (when already tried 2+)

**All of these mean: STOP. Return to Phase 1.**

## WebSGV-Specific Checklist

Before proposing any fix, verify:

- [ ] Error message read completely (including inner exception)
- [ ] `Application_Error` in `Global.asax.cs` — is it swallowing the real error?
- [ ] Session keys confirmed present (`Session["UsuarioID"]` not null)
- [ ] Role value trimmed and uppercased (see `RolesHelper.cs`)
- [ ] Stored procedure exists in DB (not just in `Database/StoredProcedures/`)
- [ ] NuGet packages restored (`packages/` directory)
- [ ] `connectionStrings.config` present and correct
