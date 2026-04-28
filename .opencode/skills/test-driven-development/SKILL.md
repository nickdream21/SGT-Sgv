---
name: test-driven-development
description: Write tests first, watch them fail, then implement minimal code to pass — red-green-refactor cycle adapted for ASP.NET Web Forms and SQL stored procedures
license: MIT
compatibility: opencode
metadata:
  source: obra/superpowers
  audience: developers
  workflow: testing
---

# Test-Driven Development (TDD)

## Overview

Write the test first. Watch it fail. Write minimal code to pass.

**Core principle:** If you didn't watch the test fail, you don't know if it tests the right thing.

> **Context WebSGV**: Este proyecto no tiene framework de test automatizado (xUnit, NUnit, etc.). Aun así, TDD aplica como disciplina de verificación:
> - **Helpers/Services C#**: se puede agregar un proyecto de test MSTest/xUnit al solution.
> - **Stored Procedures SQL**: escribe primero el `SELECT` / `EXEC` esperado, verifica que falla, luego crea el proc.
> - **Páginas .aspx**: verificación manual sistemática con checklist antes y después.

## The Iron Law

```
NO PRODUCTION CODE WITHOUT A FAILING TEST FIRST
```

Write code before the test? Delete it. Start over.

## Red-Green-Refactor Cycle

### RED — Write Failing Test

Write one minimal test showing what should happen.

**Para Helpers/Services C#:**
```csharp
[TestMethod]
public void PasswordHelper_HashAndVerify_Roundtrip()
{
    var hash = PasswordHelper.Hash("miClave123");
    Assert.IsTrue(PasswordHelper.Verify("miClave123", hash));
}
```

**Para Stored Procedures SQL:**
```sql
-- Primero: verifica que el proc NO existe aún
SELECT * FROM sys.objects WHERE name = 'sp_DC_GetOrdenesViaje' AND type = 'P'
-- Debe devolver 0 filas → test "rojo"
```

**Para páginas .aspx (manual):**
```
Checklist ANTES de implementar:
[ ] Navegar a la URL → ¿devuelve 404 o error? → TEST ROJO confirmado
[ ] El comportamiento esperado está documentado
```

### Verify RED — Watch It Fail

**MANDATORY. Never skip.**

Confirmar:
- Test falla (no errores de compilación)
- El mensaje de fallo es el esperado
- Falla porque la feature no existe

### GREEN — Minimal Code

Escribe el código más simple posible para pasar el test. Sin features extra, sin refactoring "de paso".

### Verify GREEN — Watch It Pass

**MANDATORY.**

Confirmar:
- Test pasa
- Otros tests siguen pasando (si hay)
- No hay warnings nuevos en MSBuild

### REFACTOR — Clean Up

Solo después de GREEN:
- Eliminar duplicación
- Mejorar nombres de variables/métodos
- Extraer helpers

Mantener tests verdes durante el refactor.

## Adding a Test Project to WebSGV

Si se quiere test automatizado para Helpers/Services:

1. Agregar proyecto `WebSGV.Tests` (MSTest o xUnit) al solution `WebSGV.sln`
2. Referenciar `WebSGV` project
3. Registrar en `WebSGV.sln` el nuevo proyecto
4. Tests viven en `WebSGV.Tests/` por categoría: `Helpers/`, `Services/`

## Verification Checklist Before Marking Complete

- [ ] Cada nueva función/método tiene un test
- [ ] Vi el test fallar antes de implementar
- [ ] El test falló por la razón esperada (feature faltante, no typo)
- [ ] Escribí código mínimo para pasar
- [ ] Todos los tests pasan
- [ ] Sin errores/warnings nuevos en MSBuild

## Common Rationalizations

| Excuse | Reality |
|---|---|
| "Demasiado simple para testear" | Código simple se rompe. El test tarda 30 segundos. |
| "Lo voy a testear después" | Tests que pasan inmediatamente no prueban nada. |
| "Ya lo probé manualmente" | Ad-hoc ≠ sistemático. No se puede re-ejecutar. |
| "No hay framework de tests" | Agrega uno, o usa verificación manual con checklist. |
