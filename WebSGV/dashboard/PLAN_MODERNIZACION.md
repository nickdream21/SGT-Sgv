# Plan de modernización · DashboardExportacion ↔ Power BI

> **Insumos auditados (orden estricto, como exige `prompt`):**
> 1. `WebSGV\dashboard\prompt` — define el rol y los 11 entregables.
> 2. `WebSGV\dashboard\formulasDash` — 25 fórmulas DAX extraídas directamente del archivo
>    *Dashboard Transporte - SGV-MARZO2025.pbix*.
> 3. `WebSGV\dashboard\STATUS GENERAL VIVIANA.xlsx` — origen de los datos del PBIX
>    (ahora reemplazado por `RegistroSeguimiento.aspx` / tabla `SeguimientoExportacion`).
> 4. Imágenes `im1.png` … `im5.png` — capturas del informe Power BI (referencia visual).
>
> **Regla de negocio innegociable (citada de `formulasDash` y `prompt`):**
> > *"LA COLUMNA: F.H. PROGRAMACION ahí se saca el total de camiones según el mes.
> > Si el viaje termina en julio pero la fecha de programación es en finales de mayo,
> > ese viaje pertenece a mayo."*
>
> Por tanto **todo agrupamiento por mes/año/cliente DEBE usar
> `SeguimientoExportacion.fhProgramacion`** como dimensión temporal canónica.

---

## 0. Estado actual del dashboard (línea base)

`WebSGV\Views\Exportacion\DashboardExportacion.aspx[.cs]` ya implementa:

| # | Visual | Fuente DAX referencia | Estado |
|---|---|---|---|
| K1 | % Cumplimiento Programado | `% Cumplimiento Programado` | ✅ |
| K2 | Total Camiones | `Camiones` | ✅ |
| K3 | Total Pedidos | `Pedidos` (DISTINCTCOUNT) | ✅ |
| K4 | Camiones / Pedido | `Camiones/Pedido` | ✅ |
| K5 | Horas promedio del viaje | (compuesto Base→Salida) | ⚠️ revisar fórmula |
| K6 | Total Incidencias | suma robados+rotos+mojados | ✅ |
| G1 | Trujillo · Carga / Espera ingreso / Espera carga / Permanencia | bloque Trujillo | ✅ |
| G2 | Trujillo → Planta Ecuador (días) | `Trujillo-PlantaEcu` | ✅ |
| G3 | Base (hrs) | `Base` | ✅ |
| G4 | Estado (donut) | — | ✅ |
| G5 | Incidencias (donut) | — | ✅ |
| G6 | TCI + Espera Nacionalización | `TCI`, `Espera Nacionalizacion` | ✅ |
| G7 | CEBAF (min) | `CEBAF` | ✅ |
| G8 | DEPSA + Espera DEPSA | `DEPSA`, `Espera para ingresar Depsa` | ✅ |
| G9 | Inbalnor (descarga + espera) | `Descarga`, `Espera para iniciar la descarga` | ✅ |
| G10 | Jave (descarga + espera) | idem | ✅ |
| G11 | Distancias DEPSA→Almacén / TCI→Almacén | `De Depsa a bodega`, `De TCI a bodega` | ⚠️ agrupado |
| G12 | Tendencia mensual por F.H. Programación | — | ✅ |
| G13 | Top 10 clientes | — | ✅ |

---

## 1. Brechas detectadas (`formulasDash` vs dashboard)

> Cada brecha cita textualmente la fórmula DAX origen.

### 1.1 Faltantes funcionales

1. **Tiempo promedio en Complex (Hrs)** — *No implementado*.
   La fórmula provista en `formulasDash` reutiliza el cálculo de DEPSA (líneas
   *"Tiempo promedio en Complex / DEPSA = (F.S.Depsa + H.S.Depsa - F.I. Depsa - H.I. Depsa) * 24"*).
   **[INFERENCIA]** El PBIX usa la misma columna física pero filtrada cuando
   `bodegaNacional = 'COMPLEX'`. Requiere validar contra `im*.png`.
   → **Acción:** añadir KPI/visual filtrado por bodega.

2. **Espera para ingresar a Complex (Hrs)** — *No implementado*.
   En `formulasDash` la fórmula nominalmente nombrada *"Espera para ingresar a Complex"*
   es **idéntica** a *"Espera para ingresar Depsa"*. **[INFERENCIA]** el PBIX
   muestra el mismo cálculo segmentado por `bodegaNacional`.
   → **Acción:** mismo SP, segmentar por `bodegaNacional IN ('DEPSA','COMPLEX')`.

3. **DEPSA → Jave (Hrs)** y **TCI → Jave (Hrs)** — *No implementados*.
   En el dashboard actual se promedia un único `De Depsa a bodega` / `De TCI a bodega`
   sin separar por destino (`bodegaDescarga`). En el PBIX existen como visuales
   independientes. → **Acción:** segmentar por `bodegaDescarga`.

4. **DEPSA → Inbalnor (Hrs)** y **TCI → Inbalnor (Hrs)** — *Implementados de forma agrupada*.
   → **Acción:** mismas particiones que el punto 3.

5. **Cumplimiento (horas de retraso por viaje)** — Sólo existe el % agregado.
   Fórmula `Cumplimiento` devuelve horas positivas (si llegó tarde) o BLANK.
   → **Acción:** añadir histograma + tabla detalle de viajes incumplidos.

### 1.2 Visuales del PBIX que faltan replicar (según `prompt` §6)

A confirmar contra `im1.png` … `im5.png`:

- **Donut/radial de % Cumplimiento** (gauge KPI).
- **Tarjeta de "Camiones/Pedido"** con sparkline.
- **Tabla cruzada Cliente × Mes** (drill).
- **Slicer jerárquico Año → Mes → Cliente**.
- **Tarjetas comparativas vs. mes anterior** (DAX `SAMEPERIODLASTMONTH`).

> ⚠️ Texto literal de `prompt` regla 2: si no se puede ver, declarar
> *"No determinable a partir de los archivos entregados"*. La validación final
> de estos visuales **requiere abrir el `.pbix` o inspeccionar las `im*.png`**.

---

## 2. Mapeo DAX → T-SQL (núcleo del entregable §4 y §5 del `prompt`)

Convención: cada `'Seguimiento EXPORTACION'[F.X] + 'Seguimiento EXPORTACION'[H.X]` del PBIX
equivale en la BD a una sola columna `fhX` (`datetime`) en `SeguimientoExportacion`,
porque en el modelo SQL ya unimos fecha+hora en un solo campo (ver
`RegistroSeguimiento.aspx.cs` → `ExcelHeaderMap`).

Multiplicar por `24` en DAX equivale en T-SQL a `DATEDIFF(MINUTE, a, b) / 60.0`
(o `SECOND / 3600.0` para mayor precisión).

| Medida DAX (de `formulasDash`) | T-SQL equivalente (SQL Server 2019+) |
|---|---|
| `% Cumplimiento Programado` = `DIVIDE(COUNTBLANK([Cumplimiento]), COUNT([PEDIDO]))` | `1.0 * SUM(CASE WHEN cumplimientoHrs IS NULL OR cumplimientoHrs <= 0 THEN 1 ELSE 0 END) / NULLIF(COUNT(*),0)` |
| `Cumplimiento` (hrs de retraso) | `CASE WHEN DATEDIFF(MINUTE, fhProgramacion, fhLlegadaTrujillo)/60.0 > 0 THEN DATEDIFF(MINUTE, fhProgramacion, fhLlegadaTrujillo)/60.0 END` |
| `Camiones` = `COUNT(PEDIDO)` | `COUNT(*)` sobre `SeguimientoExportacion` activos |
| `Pedidos` = `DISTINCTCOUNT(PEDIDO)` | **[INFERENCIA]** — el modelo SQL aún no tiene columna `pedido`. Usar `COUNT(DISTINCT NULLIF(cliente,''))` provisionalmente. ⚠️ riesgo §10. |
| `Camiones/Pedido` | `ROUND(1.0 * Camiones / NULLIF(Pedidos,0), 0)` |
| `Carga (Horas)` = `(H.Termino - H.Carga)*24` | `AVG(DATEDIFF(MINUTE, fhInicioCarga, fhTerminoCarga)/60.0)` |
| `Espera a ingreso Trujillo` (IF<0 → 0) | `AVG(CASE WHEN DATEDIFF(MINUTE, fhProgramacion, fhIngresoPlanta) < 0 THEN 0 ELSE DATEDIFF(MINUTE, fhProgramacion, fhIngresoPlanta)/60.0 END)` |
| `Espera para iniciar la carga` | `AVG(DATEDIFF(MINUTE, fhIngresoPlanta, fhInicioCarga)/60.0)` |
| `Permanencia en planta Trujillo` | `AVG(DATEDIFF(MINUTE, fhIngresoPlanta, fhSalidaPlanta)/60.0)` ⚠️ ver §10 |
| `Trujillo-PlantaEcu` (días) | `AVG(DATEDIFF(MINUTE, fhSalidaPlanta, fhLlegadaPlantaEcuador)/1440.0)` |
| `Base` (hrs) | `AVG(DATEDIFF(MINUTE, fhLlegadaBase2, fhSalidaBase2)/60.0)` |
| `Espera para ingresar Depsa` (con regla domingo / 8–22h) | Ver bloque T-SQL en §3 |
| `DEPSA` (hrs) | `AVG(DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional)/60.0)` filtrado por bodega |
| `TCI` (hrs, con IF de Nacionalización) | Ver bloque en §3 |
| `Espera Nacionalizacion` (IF>0 → 0) | `AVG(CASE WHEN DATEDIFF(MI, fhLlegadaTCI, fhAutorizacionNacionalizacion) <= 0 THEN 0 ELSE DATEDIFF(MI, fhLlegadaTCI, fhAutorizacionNacionalizacion)/60.0 END)` |
| `CEBAF` (min) | `AVG(DATEDIFF(MINUTE, fhLlegadaCEBAF, fhCruceEcuador))` |
| `De Depsa a bodega` | `AVG(DATEDIFF(MI, fhSalidaBodegaNacional, fhLlegadaAlmacen)/60.0)` segmentado por `bodegaDescarga` |
| `De TCI a bodega` | `AVG(DATEDIFF(MI, fhSalidaTCI, fhLlegadaAlmacen)/60.0)` segmentado por `bodegaDescarga` |

### 2.1 Regla "domingo / 8h-22h" para Espera DEPSA — T-SQL

```sql
CASE
    WHEN DATENAME(WEEKDAY, fhLlegadaBodegaNacional) = 'Sunday'
        THEN DATEDIFF(MINUTE, '1900-01-01', CAST(fhIngresoBodegaNacional AS TIME))/60.0 - 8
    WHEN CAST(fhLlegadaBodegaNacional AS TIME) BETWEEN '08:00' AND '22:00'
        THEN DATEDIFF(MINUTE, fhLlegadaBodegaNacional, fhIngresoBodegaNacional)/60.0
    ELSE
        DATEDIFF(MINUTE, '1900-01-01', CAST(fhIngresoBodegaNacional AS TIME))/60.0 - 8
END
```

### 2.2 Regla TCI con Nacionalización

```sql
CASE
    WHEN fhLlegadaTCI < fhAutorizacionNacionalizacion
        THEN DATEDIFF(MI, fhAutorizacionNacionalizacion, fhSalidaTCI)/60.0
    ELSE DATEDIFF(MI, fhLlegadaTCI, fhSalidaTCI)/60.0
END
```

---

## 3. Cambios concretos en código

### 3.1 Base de datos (nuevo script `sp_SE_Dashboard_v2.sql`)

- Reescribir `sp_SE_Dashboard_KPIs` para que **TODO filtro por mes/año** sea sobre
  `MONTH(fhProgramacion) / YEAR(fhProgramacion)` (regla innegociable).
- Reescribir `sp_SE_Dashboard_Graficos` añadiendo result-sets:
  - `complex` (tiempo + espera, filtrado `bodegaNacional='COMPLEX'`)
  - `depsaInbalnor`, `depsaJave`, `tciInbalnor`, `tciJave`
  - `cumplimientoBuckets` (distribución de horas de retraso)
  - `tendenciaPorCliente` (tabla cruzada Mes × Cliente)
- Añadir índices: `IX_SE_fhProgramacion (fhProgramacion) INCLUDE (cliente, bodegaNacional, bodegaDescarga)`.

### 3.2 Code-behind (`DashboardExportacion.aspx.cs`)

- Añadir lectura de los nuevos result-sets (`r.NextResult()` adicionales).
- Mantener `JsonConvert.SerializeObject` con `CultureInfo.InvariantCulture`.
- Validar que `mes=0` → `NULL` (todos) — ya está hecho.

### 3.3 Vista (`DashboardExportacion.aspx`)

- Mantener Plus Jakarta Sans + paleta actual.
- Agregar pestañas: **General · Trujillo · Trámite · Bodegas · Distancias · Operativo · Cumplimiento**.
- Nuevos canvases: `chartComplex`, `chartDepsaInbalnor`, `chartDepsaJave`,
  `chartTciInbalnor`, `chartTciJave`, `chartCumplimientoBuckets`, `chartClienteMes`.
- Drill-down: click en barra de "Top clientes" filtra `txtCliente` y re-postback.
- Toggle "Horas" / "Días" en charts donde aplique.

---

## 4. Pendientes que requieren validación humana

Citando regla 2 de `prompt` — *"No inventes"*:

1. **Columna `PEDIDO`** del PBIX: en la BD no existe un campo `pedido` aún.
   Se necesita confirmar si se mapea a `cliente`, a una nueva columna, o a un
   identificador de carga distinto. → **No determinable a partir de los archivos entregados.**
2. **`bodegaDescarga` vs `bodegaEcuatoriana`**: en `RegistroSeguimiento.aspx.cs`
   existen ambas. Confirmar cuál es la dimensión que el PBIX usa para
   "Inbalnor / Jave". → Se asume `bodegaDescarga` ⇒ marcar `[INFERENCIA]`.
3. **Cálculo de Permanencia en planta Trujillo**: en DAX usa `H.S Planta - H.I planta`
   *sin sumar la fecha* (sólo horas) — produce valores extraños si cruza medianoche.
   ⇒ Riesgo §10. En SQL se usa `DATEDIFF` completo.

---

## 5. Siguientes pasos de ejecución (machine-readable)

| Step | Archivo | Acción |
|---|---|---|
| step-2 | — | listar columnas reales de `SeguimientoExportacion` |
| step-3 | (este doc §2) | confirmar mapeo DAX→SQL |
| step-4 | `Database\Scripts\sp_SE_Dashboard_v2.sql` (nuevo) | rewrite KPIs |
| step-5 | mismo | rewrite Gráficos (12 → 19 result-sets) |
| step-6 | mismo | nuevos result-sets Complex, splits, buckets |
| step-7 | `DashboardExportacion.aspx` | nuevas tabs + canvases |
| step-8 | mismo | JS Chart.js + drill |
| step-9 | `DashboardExportacion.aspx.cs` | lectura de result-sets |
| step-10 | — | `run_build` y validar |
| step-11 | — | comparar muestra abril/mayo vs PBIX |
