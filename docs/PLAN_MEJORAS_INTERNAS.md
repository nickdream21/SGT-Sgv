# Plan — Mejoras internas de código (sin alterar funcionalidad)

> Continuación de `PLAN_REFACTOR_SERVICES.md` (refactor a `Services/` ~95% completo).
> Este documento recoge mejoras **estructurales y de mantenibilidad** que **NO cambian
> el comportamiento observable** del sistema: mismo SQL, mismas pantallas, mismos PDFs.
> Creado: 2026-06-17.

## 0. Alcance acordado

- **Dentro:** flujo de dinero (despacho → viaje → liquidación → revisión), reportes ya
  migrados, y **helpers transversales** del proyecto.
- **Fuera (igual que el plan original):** grifo/combustible (Abastecimiento, DashboardGrifo,
  CargarExcel de combustible), operadores/maquinaria (RegistroOperadores,
  RegistroEquiposMaquinaria, AsignacionesMaquinaria) y exportación
  (`Views/Exportacion/*`). No se tocan en este plan.
- **Regla de oro:** ningún cambio altera SQL, SPs ni UI. Verificación = **MSBuild limpio +
  los tests xUnit en verde**. Un tema = un commit que compila.

## 1. Diagnóstico (hallazgos concretos del código actual)

Medido sobre el árbol actual (`WebSGV/Views/`):

| Hallazgo | Evidencia | Nota de alcance |
|---|---|---|
| **SQL crudo residual** (`new SqlConnection`) en páginas **en alcance** | `DashboardConductor` (6), `ListaDespachos` (4), `RegistroDespacho` (2), `AgregarCPIC` (1), `AgregarFactura` (1), `BuscarFactura` (1), `Dashboard` (2) | binders de UI / colas que el refactor dejó como "opcional" |
| **DbHelper infrautilizado** | 14 referencias directas a `ConfigurationManager.ConnectionStrings`/`ConexionSGV` en code-behind | el helper ya existe y centraliza el patrón |
| **Logging informativo sin migrar** | Serilog/`LogSGV` solo cubre los 5 catch de dinero; quedan `Debug.WriteLine`/`Trace` y ~cientos de `catch` de lectura/UI | base ya hecha (`LogSGV`) |
| **Dedup cosmético** | `FormatearTamano`/`ObtenerContentType`/`ObtenerIconoArchivo` duplicados en `BuscarFactura`+`BuscarCPIC`; familia `ObtenerClaseEstado/ObtenerTextoEstado/ObtenerClaseBoton` repetida en ~10 `Registro*` | ya señalado en el plan original §5 |
| **Dos helpers de autorización solapados** | `Helpers/SecurityHelper.cs` (nuevo) vs `Views/RolesHelper.cs` | CLAUDE.md pide "preferir SecurityHelper" |
| **Magic strings de sesión** | `Session["UsuarioID"]`/`["Rol"]`/`["Nombre"]` repetidos ~49 veces; `.ToUpper().Trim()` de rol disperso | sin constantes centralizadas |
| **Cobertura de tests limitada a lógica pura** | 8 archivos de test (helpers + validaciones/cálculos); 0 sobre la lógica extraída a Services en Fase B–E que sea pura | los Services con BD no se testean (decisión), pero hay lógica pura no cubierta |

## 2. Mejoras propuestas (cada una = un commit que compila)

### M1 — Terminar la migración a `LogSGV` (logging) 🟢 bajo riesgo
**Qué:** reemplazar `Debug.WriteLine`/`Trace.Write` informativos y los `catch` de lectura/UI
**de las páginas en alcance** por `LogSGV.Information/Warning/Error`, sin cambiar el mensaje
mostrado al usuario ni el control de flujo.
**Por qué:** los 5 flujos de dinero ya loguean a producción; el resto del flujo (lectores,
binders, validaciones de UI) sigue ciego en producción.
**Alcance:** `DashboardConductor`, `LiquidacionesPendientes`, `RegistroDespacho`,
`ListaDespachos`, `BuscarOrdenViaje`, `AgregarOrdenViaje`, `AgregarFactura`, `BuscarFactura`,
`AgregarCPIC`, `BuscarCPIC`, `Reportes*`, `DetalleOrdenViaje`, `EditarDespacho`,
`LiquidacionesAprobadasContabilidad`.
**No tocar:** nada bajo grifo/maquinaria/exportación.
**Verificación:** build + tests; revisar que ningún `catch` cambie de "tragar" a "propagar".

### M2 — Centralizar acceso a datos en `DbHelper` 🟢 bajo riesgo
**Qué:** sustituir las 14 lecturas directas de `ConfigurationManager.ConnectionStrings["ConexionSGV"]`
y el SQL crudo residual de las páginas **en alcance** por llamadas a `DbHelper`
(`ConsultarTabla`/`EjecutarEscalar`/`EnTransaccion`), **SQL verbatim**.
**Por qué:** unifica el ciclo de vida de conexión y el manejo de `DBNull`; reduce fugas de
`SqlConnection`. El helper y el patrón ya están probados en Fase B–E.
**Sugerencia de orden** (1 commit por página): `Dashboard` → `AgregarCPIC` → `AgregarFactura`
→ `BuscarFactura` → residuales de `DashboardConductor`/`ListaDespachos`/`RegistroDespacho`.
**Verificación:** build + tests; diff debe mostrar SQL idéntico, solo cambia el "cómo se ejecuta".

### M3 — Deduplicar helpers de presentación (cosmético) 🟢 bajo riesgo
**Qué:**
- Mover `FormatearTamano(long)`, `ObtenerContentType(string)`, `ObtenerIconoArchivo(string)`
  a un único `Helpers/ArchivoHelper.cs` (o `UiHelper`); `BuscarFactura`+`BuscarCPIC` delegan.
- Mover la familia `ObtenerClaseEstado/ObtenerTextoEstado/ObtenerClaseBoton` a
  `Helpers/EstadoUiHelper.cs`; los `Registro*` en alcance la consumen.
**Por qué:** ~10 copias divergibles del mismo mapeo estado→clase CSS.
**Cuidado:** verificar que las copias sean **idénticas** antes de unificar; si alguna difiere,
documentarlo y dejar esa fuera (no "arreglar" comportamiento aquí).
**Nota de alcance:** `BuscarCPIC`/`RegistroClientes`/`RegistroPeajes`/etc. están **en alcance**
(no son grifo/maquinaria). `RegistroOperadores`/`RegistroEquiposMaquinaria` quedan **fuera**.

### M4 — Constantes de sesión y consolidación de autorización 🟡 riesgo medio
**Qué:**
- Crear `Helpers/SesionHelper.cs` (o constantes en `SecurityHelper`) con `UsuarioId`,
  `Rol`, `Nombre` tipados; reemplazar las ~49 lecturas crudas de `Session[...]`.
- Marcar `Views/RolesHelper.cs` como obsoleto y migrar sus llamadas a `SecurityHelper`
  (CLAUDE.md ya indica preferir `SecurityHelper`). **No** borrar hasta migrar todos los usos.
**Por qué:** elimina magic strings y la duplicidad de dos helpers de rol.
**Cuidado:** las comparaciones de rol usan `.ToUpper().Trim()` y aceptan alias (ADMIN/
ADMINISTRADOR, CONDUCTOR/CHOFER) — preservar esa semántica exacta. Cualquier cambio aquí
toca **autorización**, así que requiere revisión cuidadosa y build+tests.

### M5 — Extraer y testear lógica pura residual 🟢 bajo riesgo, alto valor
**Qué:** identificar cálculos/validaciones aún embebidos en code-behind **de páginas en
alcance** (p.ej. parsing de `Request.Form`, recálculo de totales, reglas de formato) que
sean lógica pura, moverlos a `Services/.../*Validaciones`/`*Calculos` y **añadir tests xUnit**.
**Por qué:** es la única vía de cubrir el flujo de dinero con pruebas (los Services con BD no
se testean por decisión). Sigue exactamente el patrón de Fase A.
**Verificación:** nuevos tests verdes + los existentes intactos.

### M6 — Limpieza de artefactos y `var`/nullable menores 🟢 bajo riesgo
**Qué:** revisar `TestGenerarPdfOrdenViaje.aspx` (la memoria lo marca como **endpoint vivo —
NO borrar**), usings sin usar, `catch` vacíos restantes (grep dio 0, confirmar), y warnings
de compilación del proyecto en alcance.
**Por qué:** higiene; reduce ruido en el build.
**Cuidado:** no eliminar `TestGenerarPdfOrdenViaje` ni `App_Data/Uploads` (ver memorias).

## 3. Orden sugerido y riesgo

| # | Mejora | Riesgo | Toca dinero | Esfuerzo |
|---|---|---|---|---|
| 1 | M1 Logging restante | 🟢 | indirecto | medio |
| 2 | M3 Dedup presentación | 🟢 | no | bajo |
| 3 | M6 Limpieza/warnings | 🟢 | no | bajo |
| 4 | M2 DbHelper residual | 🟢 | sí (lectura) | medio |
| 5 | M5 Lógica pura + tests | 🟢 | sí | medio-alto |
| 6 | M4 Sesión/autorización | 🟡 | autorización | medio |

> Recomendado empezar por **M3/M6** (cero riesgo, ganan momentum), luego **M1**, y dejar
> **M4** al final por tocar autorización.

## 3.1 Progreso ⏳

> Se actualiza tras cada commit para no perder el punto de avance entre sesiones.

| Mejora | Estado | Commit(s) | Nota |
|---|---|---|---|
| **M3-A** ArchivoHelper (BuscarFactura+BuscarCPIC) | ✅ Hecho | _(este commit)_ | `Helpers/ArchivoHelper.cs`; code-behind delega (markup `<%# %>` intacto). Build limpio + 177 tests. |
| **M3-B** Familia `ObtenerClaseEstado/Texto/Boton` | ✅ Hecho | _(este commit)_ | `Helpers/EstadoUiHelper.cs`; dedup en 6 `Registro*` en alcance (Clientes, Choferes, Plantas, Peajes, Semiremolques, Tractos). Excluidos: maquinaria/operadores/obra y DashboardConductor/ReportesOrdenesViaje (estado de flujo, semántica distinta). Build limpio + 177 tests. |
| **M6** Limpieza warnings/usings | ⏳ Pendiente | — | — |
| **M1** Logging restante | ⏳ Pendiente | — | — |
| **M2** DbHelper residual | ⏳ Pendiente | — | — |
| **M5** Lógica pura + tests | ⏳ Pendiente | — | — |
| **M4** Sesión/autorización | ⏳ Pendiente | — | — |

## 4. Lo que NO se hará (decisiones explícitas)

- **No** convertir a EF/ORM, async/await ni inyección de dependencias: cambia el modelo de
  ejecución de Web Forms y el riesgo no compensa.
- **No** tocar grifo/combustible, operadores/maquinaria ni exportación (exclusión heredada).
- **No** introducir dobles de prueba ni interfaces de repositorio (decisión del plan original).
- **No** cambiar SQL, SPs, ViewState ni la firma digital.

## 5. Cómo verificar

```powershell
& "<ruta>\MSBuild.exe" WebSGV\WebSGV.csproj /t:Build /p:Configuration=Debug /nologo /verbosity:minimal
dotnet test WebSGV.Tests\WebSGV.Tests.csproj -c Debug
```

Para mejoras que tocan el flujo de dinero (M2/M5), idealmente además una corrida real
(envío y re-liquidación), igual que recomienda el plan original §7.
