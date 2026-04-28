---
name: verification-before-completion
description: Enforce running actual verification commands before claiming any work is complete, fixed, or passing — no success assertions without fresh evidence
license: MIT
compatibility: opencode
metadata:
  source: obra/superpowers (originally cloudflare/workerd)
  audience: developers
  workflow: quality
---

# Verification Before Completion

## Overview

Claiming work is complete without verification is dishonesty, not efficiency.

**Core principle:** Evidence before claims, always.

> **Context WebSGV**: Este proyecto no tiene CI. La verificación es manual: MSBuild, IIS Express, consultas SQL, y revisión visual. Aun así — SIEMPRE verificar antes de declarar algo listo.

## The Iron Law

```
NO COMPLETION CLAIMS WITHOUT FRESH VERIFICATION EVIDENCE
```

If you haven't run the verification command in this message, you cannot claim it passes.

## The Gate Function

```
BEFORE claiming any status or expressing satisfaction:

1. IDENTIFY: What command or action proves this claim?
2. RUN: Execute it fresh and complete
3. READ: Full output, check exit code or visual result
4. VERIFY: Does output confirm the claim?
   - If NO: State actual status with evidence
   - If YES: State claim WITH evidence
5. ONLY THEN: Make the claim
```

## Verification Matrix for WebSGV

| Claim | Verification Required | Not Sufficient |
|---|---|---|
| "Build succeeds" | MSBuild output: 0 errors | "Code looks correct" |
| "Page works" | Open in browser via IIS Express, no red screen | "Logic seems right" |
| "SQL query/proc correct" | Run against DB, check results | "Syntax looks fine" |
| "Role access works" | Login with that role, navigate to page | "RolesHelper call is there" |
| "PDF generates" | Actually generate PDF, open file | "Service code was updated" |
| "Session persists" | Navigate across pages, check session keys | "Session is set in code" |
| "Stored proc deployed" | Run `SELECT * FROM sys.objects WHERE name = 'sp_...'` | "File added to Database/" |
| "Migration applied" | Query the affected table/column | "Script was written" |

## Red Flags — STOP

- Using "should", "probably", "seems to"
- Expressing satisfaction before verification ("Done!", "Fixed!", "Ready!")
- About to commit without verifying build
- Trusting that code compiles without running MSBuild
- **ANY wording implying success without having verified**

## Common Rationalizations

| Excuse | Reality |
|---|---|
| "Should work now" | RUN the verification |
| "I'm confident" | Confidence ≠ evidence |
| "Just this once" | No exceptions |
| "Code looks right" | Looking ≠ running |
| "I added the code" | Added ≠ works |

## MSBuild Quick Verification

```powershell
# From solution root
msbuild WebSGV.sln /p:Configuration=Debug /nologo /verbosity:minimal
```

Exit code 0 = build OK. Any error lines = NOT done.
