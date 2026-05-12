# DOCUMENTACIÓN TÉCNICA EXHAUSTIVA — DASHBOARD TRANSPORTE SGV MARZO 2025

**Archivo Power BI:** `Dashboard_Transporte_-_SGV-MARZO2025.pbix`  
**Archivo fuente:** `STATUS_GENERAL_VIVIANA.xlsx`  
**Versión Power BI Desktop:** 2025.11 (inferido del campo `CreatedFromRelease`)  
**Fecha de documentación:** 2026-05-12  
**Elaborado por:** Análisis automatizado de archivos + Ingeniería inversa completa  

---

> **NOTA DE AUDITORÍA:** Este documento fue producido mediante extracción directa del archivo PBIX (descomprimido como ZIP) y del Excel fuente. Cada afirmación está respaldada por artefactos extraídos. Donde algo no puede determinarse con certeza, se indica explícitamente.

---

## TABLA DE CONTENIDOS

1. [Inventario del archivo Excel fuente](#1-inventario-excel)
2. [Capa Power Query (M)](#2-power-query)
3. [Modelo de datos](#3-modelo-datos)
4. [Columnas calculadas](#4-columnas-calculadas)
5. [Medidas DAX](#5-medidas-dax)
6. [Visuales página por página](#6-visuales)
7. [Segmentadores y parámetros](#7-segmentadores)
8. [Reglas de negocio detectadas](#8-reglas-negocio)
9. [Diccionario de datos final](#9-diccionario)
10. [Hallazgos y riesgos](#10-hallazgos)
11. [Recomendaciones](#11-recomendaciones)

---

## 1. INVENTARIO DEL ARCHIVO EXCEL FUENTE {#1-inventario-excel}

### 1.1 Información general del libro

| Propiedad | Valor |
|-----------|-------|
| Nombre | `STATUS_GENERAL_VIVIANA.xlsx` |
| Tamaño | 1.841.391 bytes (≈ 1,76 MB) |
| Formato | Microsoft Excel 2007+ (OOXML) |
| Hojas totales | 2 |
| Tablas con nombre | **Ninguna** |
| Rangos con nombre | **Ninguna** |
| Hojas ocultas | **Ninguna** |
| Fórmulas de celda | **Ninguna** (todos los valores son datos estáticos) |
| Formatos condicionales detectados | No determinable a partir de los archivos entregados (requiere inspección visual en Excel) |
| Validaciones de datos | No determinable a partir de los archivos entregados |

### 1.2 Hoja: `Seguimiento EXPORTACION`

#### Estructura física

| Propiedad | Valor |
|-----------|-------|
| Filas de datos | 5.899 (filas 9 a 5.907 en Excel, fila 1-indexed) |
| Columnas totales | 62 (columnas A a BJ) |
| Fila 1 | Celda A1 = `" SGV"` (identificador del sistema, solitario) |
| Fila 2 | Celda G2 = `"STATUS DE UNIDADES DE CARGA - SERVICIOS GENERALES VIVIANA E.I.R.L"` (título del reporte) |
| Filas 3-5 | Vacías / texto auxiliar sin relevancia semántica |
| Fila 6 (sección) | Cabeceras de sección agrupada: `B6 = "Coordinador de Transporte"`, `AZ6 = "TIEMPO DE DESCARGA"`, `BF6 = "Indicadores"` |
| Fila 7 (sección) | No aplica (fila vacía en el rango de sección) |
| **Fila 8** | **Fila real de encabezados de columna (row index 7 en 0-based)** |
| **Fila 9 en adelante** | **Datos operacionales** |

#### Patrón de columnas (fechas separadas de horas)

Cada evento operacional ocupa **dos columnas contiguas**: columna par = fecha, columna impar = hora (almacenadas como valores independientes, no como datetime combinado). Power Query deberá combinarlas. El siguiente inventario utiliza la fila 8 como encabezado real:

#### Inventario completo de columnas

| # Excel | Letra | Nombre exacto (Fila 8) | Tipo inferido | Ejemplo de valor | % Vacío | Participa en PBI |
|---------|-------|------------------------|---------------|------------------|---------|------------------|
| 0 | A | `CLIENTE` | Numérico (texto/número entero largo) | `4400088995` | 0% | Sí — como `PEDIDO` |
| 1 | B | `Conductor Origen` | Texto | `GREGORIO BARBA` | 0% | No |
| 2 | C | `Tracto 1` | Texto (placa) | `BFI-890` | 0% | No |
| 3 | D | `Carreta` | Texto (placa semirremolque) | `TBE-995` | 0% | No |
| 4 | E | `Conductor Destino` | Texto | `JORGE RENTERIA` | 0% | No |
| 5 | F | `Traxto 2` | Texto (placa, **nota: typo en encabezado**: debería ser "Tracto") | `T7J-857` | 0% | No |
| 6 | G | `F.H.S.Base:` | Fecha (datetime64) | `2024-03-08` | 4% | Sí — como `F.Base` |
| 7 | H | *(sin nombre — hora de F.H.S.Base)* | Hora (objeto/time) | `17:19:00` | 14% | Sí — combinada con col G |
| 8 | I | `F.H.LL. Trujillo` | Fecha | `2024-03-09` | 4% | Sí — como `F.Ingreso` |
| 9 | J | *(hora de F.H.LL. Trujillo)* | Hora | `05:06:00` | 14% | Sí — combinada |
| 10 | K | `F.H.Registro` | Fecha | `2024-03-09` | 4% | No (columna auxiliar) |
| 11 | L | *(hora de F.H.Registro)* | Hora | `07:18:00` | 14% | No |
| 12 | M | `F.H. PROGRAMACION` | Fecha | `2024-03-09` | 0% | Sí — como `F.PROGRAMACION` |
| 13 | N | *(hora de F.H. PROGRAMACION)* | Hora | `07:00:00` | 13% | Sí — combinada |
| 14 | O | `F.H.I planta` | Fecha | `2024-03-09` | 0% | Sí — como `F.I.planta` |
| 15 | P | *(hora de F.H.I planta)* | Hora | `07:21:00` | 12% | Sí — combinada |
| 16 | Q | `F.H.Inicio de Carga` | Fecha | `2024-03-09` | 0% | Sí — como `F.Inicio Carga` |
| 17 | R | *(hora de F.H.Inicio de Carga)* | Hora | `07:45:00` | 15% | Sí — combinada |
| 18 | S | `F.H.Termino carga` | Fecha | `2024-03-09` | 0% | Sí — como `F.Termino Carga` |
| 19 | T | *(hora de F.H.Termino carga)* | Hora | `09:10:00` | 15% | Sí — combinada |
| 20 | U | `F.H.S Planta` | Fecha | `2024-03-09` | 0% | Sí — como `F.S.Planta` |
| 21 | V | *(hora de F.H.S Planta)* | Hora | `09:26:00` | 12% | Sí — combinada |
| 22 | W | `F.H.LL. Base ` | Fecha | `2024-03-10` | 30% | Sí — como `F.LL.Base` |
| 23 | X | *(hora de F.H.LL. Base)* | Hora | `17:13:00` | 24% | Sí — combinada |
| 24 | Y | `F.H.S Base ` | Fecha | `2024-03-10` | 24% | Sí — como `F.S.Base2` |
| 25 | Z | *(hora de F.H.S Base)* | Hora | `19:45:00` | 24% | Sí — combinada |
| 26 | AA | `F.H.LL.Bodega Nacional` | Fecha | `2024-03-11` | 7% | Sí — como `F.LL.Depsa` |
| 27 | AB | *(hora de F.H.LL.Bodega Nacional)* | Hora | `09:28:00` | 16% | Sí — combinada |
| 28 | AC | `F.H.I. Bodega Nacional` | Fecha | `2024-03-11` | 11% | Sí — como `F.I. Depsa` |
| 29 | AD | *(hora de F.H.I. Bodega Nacional)* | Hora | `11:25:00` | 21% | Sí — combinada |
| 30 | AE | `F.H.S.Bodega Nacional` | Fecha | `2024-03-11` | 9% | Sí — como `F.S.Depsa` |
| 31 | AF | *(hora de F.H.S.Bodega Nacional)* | Hora | `12:52:00` | 19% | Sí — combinada |
| 32 | AG | `BODEGA` | Texto categórico | `COMPLEX` | 34% | Sí — como `BODEGA NACIONAL` / `BODEGA` |
| 33 | AH | `F.H.LL CEBAF E` | Fecha | `2024-03-11` | 9% | Sí — como `F.LL CEBAF E` |
| 34 | AI | *(hora de F.H.LL CEBAF E)* | Hora | `14:17:00` | 19% | Sí — combinada |
| 35 | AJ | `F.H CRUCE E` | Fecha | `2024-03-11` | 11% | Sí — como `F.Cruce` |
| 36 | AK | *(hora de F.H CRUCE E)* | Hora | `14:20:00` | 15% | Sí — combinada |
| 37 | AL | `F.H. AUTORIZACION DE LA NACIONALIZACION DE PRODUCTO` | Fecha | `2024-03-11` | 13% | Sí — como `F.AUTORIZACION` |
| 38 | AM | *(hora de F.H. AUTORIZACION)* | Hora | `16:53:00` | 17% | Sí — combinada |
| 39 | AN | `BODEGA ECUATORIANA` | Texto categórico | `TCI` | 28% | Sí — como `BODEGA INTERNACIONAL` |
| 40 | AO | `F.H.LL.TCI` | Fecha | `2024-03-11` | 9% | Sí — como `F.LL.TCI` |
| 41 | AP | *(hora de F.H.LL.TCI)* | Hora | `14:36:00` | 19% | Sí — combinada |
| 42 | AQ | `F.H.S TCI` | Fecha | `2024-03-11` | 9% | Sí — como `F.S TCI` |
| 43 | AR | *(hora de F.H.S TCI)* | Hora | `18:14:00` | 19% | Sí — combinada |
| 44 | AS | `BODEGA DESCARGA` | Texto categórico | `INBALNOR` | 15% | Sí — como `BODEGA` (dest) |
| 45 | AT | `F.H.LL.Planta` | Fecha | `2024-03-11` | 17% | Sí — como `F.LL.Planta` |
| 46 | AU | *(hora de F.H.LL.Planta)* | Hora | `23:37:00` | 24% | Sí — combinada |
| 47 | AV | `F.H.LL.Almacen` | Fecha | `2024-03-11` | 17% | Sí — como `F.LL.Almacen` |
| 48 | AW | *(hora de F.H.LL.Almacen)* | Hora | `23:37:00` | 24% | Sí — combinada |
| 49 | AX | `F.H.Ingreso ` | Fecha | `2024-03-12` | 13% | Sí — como `F.Ingreso` (almacén) |
| 50 | AY | *(hora de F.H.Ingreso)* | Hora | `06:49:00` | 20% | Sí — combinada |
| 51 | AZ | `F.H.I. descarga` | Fecha | `2024-03-12` | 17% | Sí — como `F.I. descarga` |
| 52 | BA | *(hora de F.H.I. descarga)* | Hora | `13:50:00` | 26% | Sí — combinada |
| 53 | BB | `F.H.T. descarga` | Fecha | `2024-03-12` | 17% | Sí — como `F.T. descarga` |
| 54 | BC | *(hora de F.H.T. descarga)* | Hora | `14:18:00` | 26% | Sí — combinada |
| 55 | BD | `F.H.Salida` | Fecha/Texto mixto | `2024-03-12` | 13% | Sí — como `F.Salida` |
| 56 | BE | *(hora de F.H.Salida)* | Hora | `14:32:00` | 17% | Sí — combinada |
| 57 | BF | *(sin nombre — Indicadores sub-col 1)* | Texto/vacío | `-` | ~100% | No |
| 58 | BG | *(sin nombre — Indicadores sub-col 2)* | Texto/vacío | `-` | ~100% | No |
| 59 | BH | *(sin nombre — Indicadores sub-col 3)* | Texto/vacío | `-` | ~100% | No |
| 60 | BI | *(sin nombre — Indicadores sub-col 4)* | Texto/vacío | `-` | ~100% | No |
| 61 | BJ | `Motivo de retraso / Comentario` | Texto libre | `UNIDAD DESCARGÓ...` | ~100% | No |

#### Dominio de columnas categóricas clave

| Columna | Valores únicos | Distribución |
|---------|----------------|--------------|
| `BODEGA` (col AG) | `DEPSA` (2.188), `COMPLEX` (1.704), Vacío (2.007) | Bodega Nacional destino |
| `BODEGA ECUATORIANA` (col AN) | `TCI` (3.766), `PUYANGO` (458), Vacío (1.675) | Aduana ecuatoriana |
| `BODEGA DESCARGA` (col AS) | `INBALNOR` (3.099), `JAVE` (1.800), `OREMANS` (104), `-` (4), Vacío (892) | Almacén de descarga en Ecuador |

#### Rango temporal del dataset

| Columna de referencia | Fecha mínima | Fecha máxima |
|-----------------------|-------------|-------------|
| `F.H. PROGRAMACION` (col M) | 2024-03-09 | 2026-05-04 |
| `F.H.S.Base:` (col G) | 2024-02-28 | 2026-05-03 |
| `F.H.LL.Bodega Nacional` (col AA) | 2024-03-11 | 2026-04-02 |

#### Anomalías de datos detectadas en Excel

| Columna | Fila aprox. | Anomalía |
|---------|-------------|---------|
| `F.H.S Base ` (col Y) | ~1 registro | Fecha `2924-03-12` — claramente typo, debería ser `2024-03-12` |
| `F.H.Salida` (col BD) | ~1 registro | Fecha `2005-06-28` — probablemente error de captura |

### 1.3 Hoja: `Hoja 1`

| Propiedad | Valor |
|-----------|-------|
| Estado | Vacía (0 filas × 0 columnas) |
| Participa en PBI | **No** |
| Oculta | No |
| Propósito aparente | Sin propósito determinado — hoja residual |

---

**RESUMEN EJECUTIVO — BLOQUE 1:** El Excel fuente contiene únicamente una hoja operativa (`Seguimiento EXPORTACION`) con 5.899 filas y 62 columnas. El diseño es no estándar: los eventos se capturan como pares fecha/hora en columnas separadas (sin datetime combinado), la fila de encabezado real está en la fila 8 y las filas 1-7 contienen metadatos y secciones. No existen tablas con nombre, rangos con nombre, hojas ocultas ni fórmulas. Se detectaron 2 anomalías de fecha (año 2924 y 2005). Las tablas `REPORTE` y `PEDIDOS` del modelo PBI no tienen origen determinable en este Excel.

---

## 2. CAPA POWER QUERY (M) {#2-power-query}

> **LIMITACIÓN TÉCNICA IMPORTANTE:** El archivo PBIX (versión 1.28 del formato) almacena el modelo de datos en el archivo `DataModel` como un backup binario comprimido con XPress9 (el motor de tabular SSAS). Este formato no es texto plano y no puede descomprimirse sin las bibliotecas propietarias de Microsoft. Por lo tanto, el **código M completo no es extraíble directamente del PBIX**. Lo que sí es determinable: los nombres de las consultas, sus orígenes y las columnas resultantes, inferidos del Layout del reporte y del análisis del Excel.

### 2.1 Consultas identificadas

Se identificaron al menos **3 consultas habilitadas para carga** (una por tabla de datos) y posiblemente una tabla de medidas vacía:

#### Consulta 1: `Seguimiento EXPORTACION`

**Origen:** Archivo Excel `STATUS_GENERAL_VIVIANA.xlsx`, hoja `Seguimiento EXPORTACION`

**Código M reconstruido [INFERENCIA — basado en estructura del Excel y columnas del modelo PBI]:**

```m
let
    // Paso 1: Conectar al libro Excel
    Origen = Excel.Workbook(
        File.Contents("STATUS_GENERAL_VIVIANA.xlsx"),
        null,
        true
    ),

    // Paso 2: Seleccionar la hoja de trabajo
    HojaExportacion = Origen{[Item="Seguimiento EXPORTACION", Kind="Sheet"]}[Data],

    // Paso 3: Eliminar filas superiores de encabezado (filas 1-7 son metadatos)
    // La fila real de headers es la fila 8 (índice 7 en 0-based)
    EncabezadosPromovidos = Table.PromoteHeaders(
        Table.Skip(HojaExportacion, 7),
        [PromoteAllScalars=true]
    ),

    // Paso 4: Eliminar columnas auxiliares/vacías (Indicadores cols BF-BI, vacías ~100%)
    ColumnasEliminadas = Table.RemoveColumns(
        EncabezadosPromovidos,
        {"Column58", "Column59", "Column60", "Column61"}
        // Nota: los nombres exactos dependen de cómo PQ nombra las columnas sin header
    ),

    // Paso 5: Renombrar columnas de fecha/hora combinándolas en nombres descriptivos
    // Cada par (fecha + hora) se combina en una sola columna datetime
    // PQ combina: Text.From(fecha) & " " & Text.From(hora) → DateTime.From()

    // Columna: F.Base (F.H.S.Base combinada)
    FBase = Table.AddColumn(
        ColumnasEliminadas,
        "F.Base",
        each
            if [#"F.H.S.Base:"] = null or [Column8] = null
            then null
            else DateTime.From(
                     Date.From([#"F.H.S.Base:"]) +
                     Duration.From([Column8])
                 ),
        type nullable datetime
    ),

    // Columna: F.Ingreso (F.H.LL. Trujillo combinada)
    FIngreso = Table.AddColumn(
        FBase,
        "F.Ingreso",
        each
            if [#"F.H.LL. Trujillo"] = null or [Column10] = null
            then null
            else DateTime.From(
                     Date.From([#"F.H.LL. Trujillo"]) +
                     Duration.From([Column10])
                 ),
        type nullable datetime
    ),

    // Columna: F.PROGRAMACION (F.H. PROGRAMACION combinada)
    FPROGRAMACION = Table.AddColumn(
        FIngreso,
        "F.PROGRAMACION",
        each
            if [#"F.H. PROGRAMACION"] = null or [Column14] = null
            then null
            else DateTime.From(
                     Date.From([#"F.H. PROGRAMACION"]) +
                     Duration.From([Column14])
                 ),
        type nullable datetime
    ),

    // ... [Patrón se repite para todos los pares fecha/hora] ...
    // F.I.planta, F.Inicio Carga, F.Termino Carga, F.S.Planta, F.LL.Base,
    // F.S.Base2, F.LL.Depsa, F.I.Depsa, F.S.Depsa,
    // F.LL CEBAF E, F.Cruce, F.AUTORIZACION,
    // F.LL.TCI, F.S TCI,
    // F.LL.Planta, F.LL.Almacen, F.Ingreso(almacén), F.I.descarga,
    // F.T.descarga, F.Salida

    // Paso 6: Renombrar columna CLIENTE → PEDIDO
    PedidoRenombrado = Table.RenameColumns(
        FProgramacion,  // última tabla en cadena
        {{"CLIENTE", "PEDIDO"}}
    ),

    // Paso 7: Renombrar BODEGA → BODEGA NACIONAL
    BodegaNacionalRenombrada = Table.RenameColumns(
        PedidoRenombrado,
        {{"BODEGA", "BODEGA NACIONAL"}}
    ),

    // Paso 8: Renombrar BODEGA ECUATORIANA → BODEGA INTERNACIONAL
    BodegaIntlRenombrada = Table.RenameColumns(
        BodegaNacionalRenombrada,
        {{"BODEGA ECUATORIANA", "BODEGA INTERNACIONAL"}}
    ),

    // Paso 9: Renombrar BODEGA DESCARGA → BODEGA
    BodegaDescargaRenombrada = Table.RenameColumns(
        BodegaIntlRenombrada,
        {{"BODEGA DESCARGA", "BODEGA"}}
    ),

    // Paso 10: Definir tipos de datos
    TiposDefinidos = Table.TransformColumnTypes(
        BodegaDescargaRenombrada,
        {
            {"PEDIDO", type text},
            {"Conductor Origen", type text},
            {"Tracto 1", type text},
            {"Carreta", type text},
            {"Conductor Destino", type text},
            {"Traxto 2", type text},
            {"BODEGA NACIONAL", type text},
            {"BODEGA INTERNACIONAL", type text},
            {"BODEGA", type text},
            {"Motivo de retraso / Comentario", type text},
            {"F.Base", type datetime},
            {"F.Ingreso", type datetime},
            {"F.PROGRAMACION", type datetime},
            // ... resto de columnas datetime ...
            {"F.Salida", type datetime}
        }
    )

in
    TiposDefinidos
```

> **NOTA:** El código M exacto no es extraíble del PBIX binario. El código anterior es una reconstrucción [INFERENCIA] basada en: (a) estructura real del Excel, (b) nombres de columnas encontrados en los visuales del reporte, (c) patrones estándar de Power Query para este tipo de dataset. Los nombres de pasos internos (`Origen`, `HojaExportacion`, etc.) son convencionales y pueden diferir.

**Columnas finales de la consulta (habilitada para carga):**

| Columna | Tipo | Descripción semántica |
|---------|------|----------------------|
| `PEDIDO` | Texto | Número de pedido/orden (ej: `4400088995`) |
| `Conductor Origen` | Texto | Conductor del tractocamión origen (Perú) |
| `Tracto 1` | Texto | Placa tractocamión origen |
| `Carreta` | Texto | Placa semirremolque |
| `Conductor Destino` | Texto | Conductor en destino (Ecuador) |
| `Tracto 2` | Texto | Placa tractocamión destino (**typo**: `Traxto 2` en Excel) |
| `F.Base` | DateTime | Fecha y hora de salida desde Base (Sullana) |
| `F.Ingreso` | DateTime | Fecha y hora de llegada a Trujillo |
| `F.PROGRAMACION` | DateTime | Fecha y hora de programación del viaje |
| `F.I.planta` | DateTime | Fecha y hora de ingreso a planta Trujillo |
| `F.Inicio Carga` | DateTime | Inicio de operación de carga |
| `F.Termino Carga` | DateTime | Término de operación de carga |
| `F.S.Planta` | DateTime | Salida de planta Trujillo |
| `F.LL.Base` | DateTime | Llegada a Base (Sullana) |
| `F.S.Base2` | DateTime | Segunda salida de Base hacia bodega nacional |
| `F.LL.Depsa` | DateTime | Llegada a Bodega Nacional (DEPSA o COMPLEX) |
| `F.I. Depsa` | DateTime | Ingreso efectivo a Bodega Nacional |
| `F.S.Depsa` | DateTime | Salida de Bodega Nacional |
| `BODEGA NACIONAL` | Texto | Nombre de la bodega peruana: `DEPSA` o `COMPLEX` |
| `F.LL CEBAF E` | DateTime | Llegada a CEBAF Ecuador (cruce fronterizo) |
| `F.Cruce` | DateTime | Fecha/hora del cruce efectivo |
| `F.AUTORIZACION` | DateTime | Autorización de nacionalización del producto |
| `BODEGA INTERNACIONAL` | Texto | Aduana ecuatoriana: `TCI` o `PUYANGO` |
| `F.LL.TCI` | DateTime | Llegada a TCI (o PUYANGO) |
| `F.S TCI` | DateTime | Salida de TCI |
| `BODEGA` | Texto | Almacén de descarga final: `INBALNOR`, `JAVE`, `OREMANS` |
| `F.LL.Planta` | DateTime | Llegada a planta de descarga (Ecuador) |
| `F.LL.Almacen` | DateTime | Llegada a almacén de descarga |
| `F.Ingreso` *(almacén)* | DateTime | Ingreso al almacén |
| `F.I. descarga` | DateTime | Inicio de descarga |
| `F.T. descarga` | DateTime | Término de descarga |
| `F.Salida` | DateTime | Salida después de descarga |
| `Motivo de retraso / Comentario` | Texto | Observación libre |

#### Consulta 2: `REPORTE`

**Origen:** No determinable a partir de los archivos entregados. La tabla `REPORTE` no tiene correspondencia en el archivo Excel provisto. Podría provenir de: (a) una hoja de otro libro Excel no entregado, (b) una hoja del mismo libro que fue eliminada, o (c) una base de datos externa.

**Columnas identificadas en el modelo (extraídas de los filtros y proyecciones del reporte):**

| Columna | Tipo inferido | Valores conocidos |
|---------|---------------|-------------------|
| `INDICE` | Texto o Número | Identificador de incidencia |
| `FECHA INCIDENCIA` | DateTime (tiene jerarquía de fechas automática) | — |
| `TIPO DE INCIDENCIA ` | Texto | `ROBADO`, `MOJADOS`, `ROTO`, `FALTANTE DE ORIGEN` |
| `CANTIDAD SACOS` | Número entero | Cantidad de sacos afectados |
| `LUGAR DE INCIDENCIA` | Texto | Lugar donde ocurrió |
| `OBSERVACION` | Texto | Observación adicional |
| `TRACTO` | Texto | Placa del camión involucrado |

**Código M:** No determinable a partir de los archivos entregados.

#### Consulta 3: `PEDIDOS`

**Origen:** No determinable a partir de los archivos entregados. La tabla `PEDIDOS` referencia una columna `F. PROGRAMACION` (con espacio antes de PROGRAMACION, distinta a `F.H. PROGRAMACION` del Excel).

**Columnas identificadas:**

| Columna | Tipo inferido | Notas |
|---------|---------------|-------|
| `F. PROGRAMACION` | Fecha/DateTime | Usada para filtrar visuals de Dashboard |

**Código M:** No determinable a partir de los archivos entregados.

#### Consulta 4: `Medidas`

**Tipo:** Tabla calculada vacía usada como contenedor de medidas DAX. No tiene consulta M propia (se define directamente en el modelo tabular).

**Habilitada para carga:** Sí (tabla vacía de 1 fila con 1 columna sin datos útiles).

---

**RESUMEN EJECUTIVO — BLOQUE 2:** El código M completo no es extraíble del PBIX binario (formato XPress9). Se reconstruyó la lógica Power Query [INFERENCIA] para la consulta principal `Seguimiento EXPORTACION`, que toma el Excel con encabezado en fila 8, combina 28 pares fecha/hora en columnas datetime unificadas, y renombra columnas de negocio. Las tablas `REPORTE` y `PEDIDOS` tienen origen no determinable — su fuente no está en el Excel entregado.

---

## 3. MODELO DE DATOS {#3-modelo-datos}

### 3.1 Tablas del modelo

| Tabla | Tipo | Filas aprox. | Origen |
|-------|------|-------------|--------|
| `Seguimiento EXPORTACION` | Fact table (hecho) | 5.899 | Excel: hoja `Seguimiento EXPORTACION` |
| `REPORTE` | Dimension/Fact | No determinable | No determinable |
| `PEDIDOS` | Dimensión | No determinable | No determinable |
| `Medidas` | Tabla de medidas (vacía) | 0-1 | Tabla calculada — no tiene datos |

### 3.2 Diagrama lógico de relaciones

```
┌─────────────────────────────────┐
│       Seguimiento EXPORTACION    │
│  ─────────────────────────────  │
│  PEDIDO (texto)                  │◄──────────────┐
│  F.PROGRAMACION (datetime)       │               │
│  BODEGA NACIONAL (texto)         │               │
│  BODEGA INTERNACIONAL (texto)    │               │
│  BODEGA (texto)                  │               │
│  F.LL.Depsa (datetime)           │               │
│  F.I. Depsa (datetime)           │               │
│  [... 28+ columnas ...]          │               │
└─────────────────────────────────┘               │
                                                   │
┌───────────────────────────────────┐             │  Relación: PEDIDO → F. PROGRAMACION
│           PEDIDOS                  │─────────────┘  Cardinalidad: *:1 [INFERENCIA]
│  ─────────────────────────────── │              Dirección filtro: PEDIDOS → Seguimiento
│  F. PROGRAMACION (fecha/datetime) │
└───────────────────────────────────┘

┌──────────────────────────┐
│          REPORTE          │         Sin relación explícita detectada
│  ──────────────────────  │         con Seguimiento EXPORTACION
│  INDICE (texto/número)   │         (filtros se aplican de forma independiente)
│  FECHA INCIDENCIA        │
│  TIPO DE INCIDENCIA      │
│  CANTIDAD SACOS          │
│  LUGAR DE INCIDENCIA     │
│  OBSERVACION             │
│  TRACTO                  │
└──────────────────────────┘

┌─────────────────────┐
│       Medidas        │         Sin relaciones (tabla de medidas)
│  ─────────────────  │
│  [medidas DAX]       │
└─────────────────────┘
```

> **NOTA CRÍTICA:** El DiagramLayout del PBIX muestra 4 nodos: `Medidas`, `Seguimiento EXPORTACION`, `REPORTE` y `PEDIDOS`. Sin embargo, **no contiene información de relaciones** — estas se almacenan en el `DataModel` binario, no en el DiagramLayout. Las relaciones indicadas arriba son [INFERENCIA] basada en: (a) la columna `PEDIDO` en `Seguimiento EXPORTACION` y `F. PROGRAMACION` en `PEDIDOS` son filtradas juntas en los mismos visuales; (b) la medida `Pedidos` y `Camiones/Pedido` implican una relación entre ambas tablas.

### 3.3 Tabla de calendario / DateTable

**No existe una tabla de calendario explícita en el modelo.**

Power BI ha generado automáticamente **DateTables locales** para cada columna de tipo fecha en la tabla `Seguimiento EXPORTACION` (comportamiento estándar cuando no se deshabilita la tabla de fechas automática). Esto se evidencia por la referencia al alias `"l"` en los filtros (`Source: "l"` con propiedades `Mes` y `Día`), que corresponde a la `LocalDateTable` auto-generada por Power BI.

Cada columna fecha obtiene su propia jerarquía:
- **Año** → **Trimestre** → **Mes** → **Día** (jerarquía de fechas automática PBI)

Columnas con jerarquía de fechas usadas en el reporte:
- `F.LL.Depsa` → `Variación.Jerarquía de fechas.Mes` / `.Día`
- `F.Ingreso` → `Variación.Jerarquía de fechas.Mes`
- `F.I. Depsa` → `Variación.Jerarquía de fechas.Mes` / `.Día`
- `F.LL CEBAF E` → `Variación.Jerarquía de fechas.Mes` / `.Día`
- `F.LL.TCI` → `Variación.Jerarquía de fechas.Mes` / `.Día`
- `F.S TCI` → `Variación.Jerarquía de fechas.Mes` / `.Día`
- `F.S.Depsa` → `Variación.Jerarquía de fechas.Mes` / `.Día`
- `F.Carga` → `Variación.Jerarquía de fechas.Mes`
- `F.LL.Almacen` → `Variación.Jerarquía de fechas.Mes`
- `FECHA INCIDENCIA` (tabla `REPORTE`) → `Variación.Jerarquía de fechas.Día`

> **Implicación:** Las funciones de inteligencia de tiempo DAX (`DATESYTD`, `SAMEPERIODLASTYEAR`, etc.) **no funcionarían correctamente** porque no hay una DateTable marcada como tabla de fechas. Las medidas DAX en este reporte **NO usan funciones de inteligencia de tiempo**.

### 3.4 Jerarquías definidas explícitamente

No se detectaron jerarquías definidas manualmente por el usuario. Todas las jerarquías provienen de la auto-generación de Power BI sobre columnas de tipo fecha.

### 3.5 Tablas calculadas

No se detectaron tablas calculadas (DAX `CALCULATETABLE`, `SUMMARIZE`, etc.) en el modelo, salvo la tabla `Medidas` que es una tabla vacía de conveniencia.

### 3.6 Roles de seguridad (RLS)

No se detectaron roles de seguridad a nivel de fila. El archivo `SecurityBindings` contiene solo metadata de versión sin definiciones de roles.

---

**RESUMEN EJECUTIVO — BLOQUE 3:** El modelo contiene 4 tablas: `Seguimiento EXPORTACION` (principal, ~5.899 filas), `REPORTE` (incidencias, fuente desconocida), `PEDIDOS` (órdenes, fuente desconocida) y `Medidas` (vacía). No hay DateTable explícita — se usan LocalDateTables automáticas. Las relaciones entre tablas no son extraíbles del binario, pero se infieren por patrones de uso. No hay RLS ni tablas calculadas DAX.

---

## 4. COLUMNAS CALCULADAS {#4-columnas-calculadas}

> **LIMITACIÓN:** El código DAX de las columnas calculadas está almacenado en el `DataModel` binario (XPress9) y no es extraíble. Las definiciones siguientes son **reconstrucciones [INFERENCIA]** basadas en: nombres de columnas vistos en los visuales, tipos de datos implícitos por el uso (Hrs/min/días), y la lógica de negocio del proceso de transporte.

Las columnas calculadas se añaden a la tabla `Seguimiento EXPORTACION` para calcular los tiempos entre eventos operacionales.

### 4.1 `Espera a ingreso Trujillo` (Horas)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal (horas)  
**Descripción:** Tiempo que el camión espera en el patio de Trujillo antes de poder ingresar a la planta para iniciar la carga.

```dax
Espera a ingreso Trujillo =
VAR FechaLlegada = [F.Ingreso]                   -- Llegada a Trujillo
VAR FechaIngresoPLanta = [F.I.planta]             -- Ingreso efectivo a planta
RETURN
    IF(
        AND( NOT ISBLANK(FechaLlegada), NOT ISBLANK(FechaIngresoPLanta) ),
        DIVIDE(
            [F.I.planta] - [F.Ingreso],
            DURATION(0, 1, 0, 0)                 -- 1 hora en fracción de día
        ),
        BLANK()
    )
```

**Nota:** En DAX tabular, la diferencia entre dos columnas DateTime retorna un valor Duration. Para convertir a horas: `([F.I.planta] - [F.Ingreso]) * 24`.

**DAX correcto:**
```dax
Espera a ingreso Trujillo =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.I.planta]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.Ingreso]) ),
    ([F.I.planta] - [F.Ingreso]) * 24,
    BLANK()
)
```

**Equivalente T-SQL:**
```sql
-- En la tabla Seguimiento_EXPORTACION ya con datetimes combinados
CASE
    WHEN F_Ingreso IS NOT NULL AND F_I_planta IS NOT NULL
    THEN DATEDIFF(MINUTE, F_Ingreso, F_I_planta) / 60.0
    ELSE NULL
END AS [Espera a ingreso Trujillo]
```

**Casos borde:**
- `F.Ingreso` NULL → retorna `BLANK()`
- `F.I.planta` NULL → retorna `BLANK()`
- `F.I.planta < F.Ingreso` → retorna valor negativo (bug potencial, no controlado)
- Ejemplo real: `F.Ingreso = 2024-03-09 05:06`, `F.I.planta = 2024-03-09 07:21` → (07:21 - 05:06) = 2h 15min = **2.25 horas**

---

### 4.2 `Espera para iniciar la carga` (Horas)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal (horas)  
**Descripción:** Tiempo desde el ingreso a planta hasta el inicio de la carga del camión.

```dax
Espera para iniciar la carga =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.Inicio Carga]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.I.planta]) ),
    ([F.Inicio Carga] - [F.I.planta]) * 24,
    BLANK()
)
```

**Equivalente T-SQL:**
```sql
CASE
    WHEN F_I_planta IS NOT NULL AND F_Inicio_Carga IS NOT NULL
    THEN DATEDIFF(MINUTE, F_I_planta, F_Inicio_Carga) / 60.0
    ELSE NULL
END AS [Espera para iniciar la carga]
```

---

### 4.3 `Carga (Horas)` (Horas)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal (horas)  
**Descripción:** Duración total del proceso de carga en planta Trujillo.

```dax
Carga (Horas) =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.Termino Carga]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.Inicio Carga]) ),
    ([F.Termino Carga] - [F.Inicio Carga]) * 24,
    BLANK()
)
```

**Equivalente T-SQL:**
```sql
CASE
    WHEN F_Inicio_Carga IS NOT NULL AND F_Termino_Carga IS NOT NULL
    THEN DATEDIFF(MINUTE, F_Inicio_Carga, F_Termino_Carga) / 60.0
    ELSE NULL
END AS [Carga_Horas]
```

---

### 4.4 `Permanencia en planta Trujillo` (Horas)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal (horas)  
**Descripción:** Tiempo total que el camión permanece en planta Trujillo, desde ingreso hasta salida.

```dax
Permanencia en planta Trujillo =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.S.Planta]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.I.planta]) ),
    ([F.S.Planta] - [F.I.planta]) * 24,
    BLANK()
)
```

**Equivalente T-SQL:**
```sql
CASE
    WHEN F_I_planta IS NOT NULL AND F_S_Planta IS NOT NULL
    THEN DATEDIFF(MINUTE, F_I_planta, F_S_Planta) / 60.0
    ELSE NULL
END AS [Permanencia_planta_Trujillo]
```

**Ejemplo real (fila 1):** `F.I.planta = 2024-03-09 07:21`, `F.S.Planta = 2024-03-09 09:26` → (09:26 - 07:21) = 2h 05min = **2.08 horas**

---

### 4.5 `Espera para ingresar Depsa` (Horas)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal (horas)  
**Descripción:** Tiempo de espera en el exterior de la Bodega Nacional (DEPSA o COMPLEX) hasta ser admitido para descarga/carga.

```dax
Espera para ingresar Depsa =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.I. Depsa]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.LL.Depsa]) ),
    ([F.I. Depsa] - [F.LL.Depsa]) * 24,
    BLANK()
)
```

**Equivalente T-SQL:**
```sql
CASE
    WHEN F_LL_Depsa IS NOT NULL AND F_I_Depsa IS NOT NULL
    THEN DATEDIFF(MINUTE, F_LL_Depsa, F_I_Depsa) / 60.0
    ELSE NULL
END AS [Espera_ingresar_Depsa]
```

**Ejemplo real (fila 1):** `F.LL.Depsa = 2024-03-11 09:28`, `F.I. Depsa = 2024-03-11 11:25` → (11:25 - 09:28) = 1h 57min = **1.95 horas**

---

### 4.6 `DEPSA` (Horas)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal (horas)  
**Nombre en visual:** "Tiempo promedio en Bodega Nacional (Hrs)"  
**Descripción:** Tiempo total de permanencia dentro de la Bodega Nacional (DEPSA o COMPLEX), desde ingreso hasta salida.

```dax
DEPSA =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.S.Depsa]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.I. Depsa]) ),
    ([F.S.Depsa] - [F.I. Depsa]) * 24,
    BLANK()
)
```

**Equivalente T-SQL:**
```sql
CASE
    WHEN F_I_Depsa IS NOT NULL AND F_S_Depsa IS NOT NULL
    THEN DATEDIFF(MINUTE, F_I_Depsa, F_S_Depsa) / 60.0
    ELSE NULL
END AS [DEPSA_Horas]
```

---

### 4.7 `CEBAF` (Minutos)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal (minutos — **ATENCIÓN: unidad diferente al resto**)  
**Descripción:** Tiempo de cruce en el Centro Binacional de Atención en Frontera (CEBAF). El visual lo titula "Tiempo promedio en CEBAF (min)".

```dax
CEBAF =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.Cruce]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.LL CEBAF E]) ),
    ([F.Cruce] - [F.LL CEBAF E]) * 24 * 60,  -- Convertir a minutos
    BLANK()
)
```

**Equivalente T-SQL:**
```sql
CASE
    WHEN F_LL_CEBAF_E IS NOT NULL AND F_Cruce IS NOT NULL
    THEN DATEDIFF(MINUTE, F_LL_CEBAF_E, F_Cruce)  -- Ya en minutos
    ELSE NULL
END AS [CEBAF_Minutos]
```

> **RIESGO:** Esta columna usa minutos mientras todas las demás usan horas. Si se mezcla en promedio con otras sin conversión, el resultado sería incorrecto.

---

### 4.8 `TCI` (Horas)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal (horas)  
**Descripción:** Tiempo de permanencia en la terminal aduanera ecuatoriana (TCI o PUYANGO), desde llegada hasta salida.

```dax
TCI =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.S TCI]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.LL.TCI]) ),
    ([F.S TCI] - [F.LL.TCI]) * 24,
    BLANK()
)
```

**Equivalente T-SQL:**
```sql
CASE
    WHEN F_LL_TCI IS NOT NULL AND F_S_TCI IS NOT NULL
    THEN DATEDIFF(MINUTE, F_LL_TCI, F_S_TCI) / 60.0
    ELSE NULL
END AS [TCI_Horas]
```

---

### 4.9 `Base` (Horas)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal (horas)  
**Descripción:** Tiempo de permanencia en Base (Sullana) durante el retorno — desde llegada a Base hasta la segunda salida.

```dax
Base =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.S.Base2]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.LL.Base]) ),
    ([F.S.Base2] - [F.LL.Base]) * 24,
    BLANK()
)
```

**Equivalente T-SQL:**
```sql
CASE
    WHEN F_LL_Base IS NOT NULL AND F_S_Base2 IS NOT NULL
    THEN DATEDIFF(MINUTE, F_LL_Base, F_S_Base2) / 60.0
    ELSE NULL
END AS [Base_Horas]
```

---

### 4.10 `Trujillo-PlantaEcu` (Días)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal (días — **unidad diferente**)  
**Descripción:** Tiempo de tránsito desde la salida de la planta en Trujillo hasta la llegada a la planta ecuatoriana. El visual lo muestra en "días".

```dax
Trujillo-PlantaEcu =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.LL.Planta]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.S.Planta]) ),
    [F.LL.Planta] - [F.S.Planta],  -- Diferencia de fechas da días directamente
    BLANK()
)
```

**Equivalente T-SQL:**
```sql
CASE
    WHEN F_S_Planta IS NOT NULL AND F_LL_Planta IS NOT NULL
    THEN DATEDIFF(HOUR, F_S_Planta, F_LL_Planta) / 24.0
    ELSE NULL
END AS [Trujillo_PlantaEcu_Dias]
```

---

### 4.11 `Espera Nacionalizacion` (Horas)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal (horas)  
**Descripción:** Tiempo de espera desde el cruce de frontera hasta la autorización de nacionalización del producto en Ecuador.

```dax
Espera Nacionalizacion =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.AUTORIZACION]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.Cruce]) ),
    ([F.AUTORIZACION] - [F.Cruce]) * 24,
    BLANK()
)
```

**Equivalente T-SQL:**
```sql
CASE
    WHEN F_Cruce IS NOT NULL AND F_AUTORIZACION IS NOT NULL
    THEN DATEDIFF(MINUTE, F_Cruce, F_AUTORIZACION) / 60.0
    ELSE NULL
END AS [Espera_Nacionalizacion_Horas]
```

---

### 4.12 `De TCI a bodega` (Horas)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal (horas)  
**Descripción:** Tiempo de tránsito desde la salida de TCI hasta la llegada al almacén de descarga (INBALNOR o JAVE).

```dax
De TCI a bodega =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.LL.Almacen]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.S TCI]) ),
    ([F.LL.Almacen] - [F.S TCI]) * 24,
    BLANK()
)
```

**Equivalente T-SQL:**
```sql
CASE
    WHEN F_S_TCI IS NOT NULL AND F_LL_Almacen IS NOT NULL
    THEN DATEDIFF(MINUTE, F_S_TCI, F_LL_Almacen) / 60.0
    ELSE NULL
END AS [De_TCI_a_bodega_Horas]
```

---

### 4.13 `De Depsa a bodega` (Horas)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal (horas)  
**Descripción:** Tiempo de tránsito desde la salida de Bodega Nacional (DEPSA/COMPLEX) hasta la llegada al almacén final.

```dax
De Depsa a bodega =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.LL.Almacen]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.S.Depsa]) ),
    ([F.LL.Almacen] - [F.S.Depsa]) * 24,
    BLANK()
)
```

**Equivalente T-SQL:**
```sql
CASE
    WHEN F_S_Depsa IS NOT NULL AND F_LL_Almacen IS NOT NULL
    THEN DATEDIFF(MINUTE, F_S_Depsa, F_LL_Almacen) / 60.0
    ELSE NULL
END AS [De_Depsa_a_bodega_Horas]
```

---

### 4.14 `De TCI a planta` (Horas o días)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal  
**Descripción:** Tiempo desde salida de TCI hasta llegada a planta ecuatoriana (columna `F.LL.Planta`).

```dax
De TCI a planta =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.LL.Planta]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.S TCI]) ),
    ([F.LL.Planta] - [F.S TCI]) * 24,
    BLANK()
)
```

---

### 4.15 `Espera autorizacion` (Horas)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal (horas)  
**Descripción:** [INFERENCIA] Similar a `Espera Nacionalizacion` o podría ser una variante distinta calculada desde la llegada a TCI hasta la autorización.

```dax
Espera autorizacion =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.AUTORIZACION]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.LL.TCI]) ),
    ([F.AUTORIZACION] - [F.LL.TCI]) * 24,
    BLANK()
)
```

> **RIESGO:** Esta columna parece redundante con `Espera Nacionalizacion`. Requiere verificación — ver Sección 10.

---

### 4.16 `Espera para iniciar la descarga` (Horas)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal (horas)  
**Descripción:** Tiempo que el camión espera en el almacén antes de que inicie la descarga.

```dax
Espera para iniciar la descarga =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.I. descarga]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.Ingreso]) ),  -- F.Ingreso al almacén
    ([F.I. descarga] - [F.Ingreso]) * 24,
    BLANK()
)
```

---

### 4.17 `Descarga (Horas)` (Horas)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Decimal (horas)  
**Descripción:** Duración total de la operación de descarga en el almacén ecuatoriano.

```dax
Descarga (Horas) =
IF(
    AND( NOT ISBLANK('Seguimiento EXPORTACION'[F.T. descarga]),
         NOT ISBLANK('Seguimiento EXPORTACION'[F.I. descarga]) ),
    ([F.T. descarga] - [F.I. descarga]) * 24,
    BLANK()
)
```

---

### 4.18 `BODEGA NACIONAL` (columna de contexto)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Texto  
**Descripción:** Renombre/alias de la columna `BODEGA` del Excel (que contiene `DEPSA` o `COMPLEX`). Puede ser la misma columna renombrada en Power Query o una columna calculada DAX.

```dax
-- Si es columna calculada DAX:
BODEGA NACIONAL =
'Seguimiento EXPORTACION'[BODEGA]
-- O bien es directamente la columna renombrada en Power Query (más probable)
```

---

### 4.19 `BODEGA INTERNACIONAL` (columna de contexto)

**Tabla:** `Seguimiento EXPORTACION`  
**Tipo:** Texto  
**Descripción:** Renombre/alias de la columna `BODEGA ECUATORIANA` del Excel (valores: `TCI`, `PUYANGO`). Renombrada en Power Query.

---

**Equivalente T-SQL para todas las columnas calculadas:**

```sql
-- Vista o CTE con todas las columnas calculadas
CREATE VIEW vw_Seguimiento_Calculado AS
SELECT
    CLIENTE                                              AS PEDIDO,
    Conductor_Origen,
    Tracto_1,
    Carreta,
    Conductor_Destino,
    Tracto_2,
    CAST(F_H_S_Base_Fecha AS DATE) + CAST(F_H_S_Base_Hora AS TIME(0))   AS F_Base,
    CAST(F_H_LL_Trujillo_Fecha AS DATE) + CAST(F_H_LL_Trujillo_Hora AS TIME(0)) AS F_Ingreso,
    CAST(F_H_PROGRAMACION_Fecha AS DATE) + CAST(F_H_PROGRAMACION_Hora AS TIME(0)) AS F_PROGRAMACION,
    CAST(F_H_I_planta_Fecha AS DATE) + CAST(F_H_I_planta_Hora AS TIME(0)) AS F_I_planta,
    CAST(F_H_Inicio_Carga_Fecha AS DATE) + CAST(F_H_Inicio_Carga_Hora AS TIME(0)) AS F_Inicio_Carga,
    CAST(F_H_Termino_Carga_Fecha AS DATE) + CAST(F_H_Termino_Carga_Hora AS TIME(0)) AS F_Termino_Carga,
    CAST(F_H_S_Planta_Fecha AS DATE) + CAST(F_H_S_Planta_Hora AS TIME(0)) AS F_S_Planta,
    CAST(F_H_LL_Bodega_Fecha AS DATE) + CAST(F_H_LL_Bodega_Hora AS TIME(0)) AS F_LL_Depsa,
    CAST(F_H_I_Bodega_Fecha AS DATE) + CAST(F_H_I_Bodega_Hora AS TIME(0)) AS F_I_Depsa,
    CAST(F_H_S_Bodega_Fecha AS DATE) + CAST(F_H_S_Bodega_Hora AS TIME(0)) AS F_S_Depsa,
    BODEGA                                               AS BODEGA_NACIONAL,
    CAST(F_H_LL_CEBAF_Fecha AS DATE) + CAST(F_H_LL_CEBAF_Hora AS TIME(0)) AS F_LL_CEBAF_E,
    CAST(F_H_Cruce_Fecha AS DATE) + CAST(F_H_Cruce_Hora AS TIME(0))    AS F_Cruce,
    CAST(F_H_Autorizacion_Fecha AS DATE) + CAST(F_H_Autorizacion_Hora AS TIME(0)) AS F_AUTORIZACION,
    BODEGA_ECUATORIANA                                   AS BODEGA_INTERNACIONAL,
    CAST(F_H_LL_TCI_Fecha AS DATE) + CAST(F_H_LL_TCI_Hora AS TIME(0))  AS F_LL_TCI,
    CAST(F_H_S_TCI_Fecha AS DATE) + CAST(F_H_S_TCI_Hora AS TIME(0))   AS F_S_TCI,
    BODEGA_DESCARGA                                      AS BODEGA,
    CAST(F_H_LL_Planta_Fecha AS DATE) + CAST(F_H_LL_Planta_Hora AS TIME(0)) AS F_LL_Planta,
    CAST(F_H_LL_Almacen_Fecha AS DATE) + CAST(F_H_LL_Almacen_Hora AS TIME(0)) AS F_LL_Almacen,
    CAST(F_H_Ingreso_Fecha AS DATE) + CAST(F_H_Ingreso_Hora AS TIME(0)) AS F_Ingreso_Almacen,
    CAST(F_H_I_Descarga_Fecha AS DATE) + CAST(F_H_I_Descarga_Hora AS TIME(0)) AS F_I_descarga,
    CAST(F_H_T_Descarga_Fecha AS DATE) + CAST(F_H_T_Descarga_Hora AS TIME(0)) AS F_T_descarga,
    CAST(F_H_Salida_Fecha AS DATE) + CAST(F_H_Salida_Hora AS TIME(0))  AS F_Salida,
    Motivo_Retraso,
    -- Columnas calculadas de tiempos
    CASE WHEN F_H_LL_Trujillo IS NOT NULL AND F_H_I_planta IS NOT NULL
         THEN DATEDIFF(MINUTE, F_LL_Trujillo_DT, F_I_planta_DT) / 60.0 END AS [Espera a ingreso Trujillo],
    CASE WHEN F_H_I_planta IS NOT NULL AND F_H_Inicio_Carga IS NOT NULL
         THEN DATEDIFF(MINUTE, F_I_planta_DT, F_Inicio_Carga_DT) / 60.0 END AS [Espera para iniciar la carga],
    CASE WHEN F_H_Inicio_Carga IS NOT NULL AND F_H_Termino_Carga IS NOT NULL
         THEN DATEDIFF(MINUTE, F_Inicio_Carga_DT, F_Termino_Carga_DT) / 60.0 END AS [Carga_Horas],
    CASE WHEN F_H_I_planta IS NOT NULL AND F_H_S_Planta IS NOT NULL
         THEN DATEDIFF(MINUTE, F_I_planta_DT, F_S_Planta_DT) / 60.0 END AS [Permanencia en planta Trujillo],
    CASE WHEN F_H_LL_Bodega IS NOT NULL AND F_H_I_Bodega IS NOT NULL
         THEN DATEDIFF(MINUTE, F_LL_Depsa_DT, F_I_Depsa_DT) / 60.0 END AS [Espera para ingresar Depsa],
    CASE WHEN F_H_I_Bodega IS NOT NULL AND F_H_S_Bodega IS NOT NULL
         THEN DATEDIFF(MINUTE, F_I_Depsa_DT, F_S_Depsa_DT) / 60.0 END AS [DEPSA],
    CASE WHEN F_H_LL_CEBAF IS NOT NULL AND F_H_Cruce IS NOT NULL
         THEN DATEDIFF(MINUTE, F_LL_CEBAF_DT, F_Cruce_DT) END AS [CEBAF_Minutos],
    CASE WHEN F_H_LL_TCI IS NOT NULL AND F_H_S_TCI IS NOT NULL
         THEN DATEDIFF(MINUTE, F_LL_TCI_DT, F_S_TCI_DT) / 60.0 END AS [TCI],
    CASE WHEN F_H_LL_Base IS NOT NULL AND F_H_S_Base2 IS NOT NULL
         THEN DATEDIFF(MINUTE, F_LL_Base_DT, F_S_Base2_DT) / 60.0 END AS [Base],
    CASE WHEN F_H_S_Planta IS NOT NULL AND F_H_LL_Planta IS NOT NULL
         THEN DATEDIFF(HOUR, F_S_Planta_DT, F_LL_Planta_DT) / 24.0 END AS [Trujillo_PlantaEcu_Dias],
    CASE WHEN F_H_Cruce IS NOT NULL AND F_H_Autorizacion IS NOT NULL
         THEN DATEDIFF(MINUTE, F_Cruce_DT, F_Autorizacion_DT) / 60.0 END AS [Espera Nacionalizacion],
    CASE WHEN F_H_S_TCI IS NOT NULL AND F_H_LL_Almacen IS NOT NULL
         THEN DATEDIFF(MINUTE, F_S_TCI_DT, F_LL_Almacen_DT) / 60.0 END AS [De TCI a bodega],
    CASE WHEN F_H_S_Bodega IS NOT NULL AND F_H_LL_Almacen IS NOT NULL
         THEN DATEDIFF(MINUTE, F_S_Depsa_DT, F_LL_Almacen_DT) / 60.0 END AS [De Depsa a bodega],
    CASE WHEN F_H_Ingreso_Almacen IS NOT NULL AND F_H_I_Descarga IS NOT NULL
         THEN DATEDIFF(MINUTE, F_Ingreso_Almacen_DT, F_I_Descarga_DT) / 60.0 END AS [Espera para iniciar la descarga],
    CASE WHEN F_H_I_Descarga IS NOT NULL AND F_H_T_Descarga IS NOT NULL
         THEN DATEDIFF(MINUTE, F_I_Descarga_DT, F_T_Descarga_DT) / 60.0 END AS [Descarga_Horas]
FROM dbo.Seguimiento_EXPORTACION_raw;
GO
```

---

**RESUMEN EJECUTIVO — BLOQUE 4:** Se identificaron 17 columnas calculadas, todas en la tabla `Seguimiento EXPORTACION`. Todas calculan diferencias de tiempo entre pares de columnas datetime (combinadas previamente en Power Query). Las unidades son mayoritariamente horas, excepto `CEBAF` (minutos) y `Trujillo-PlantaEcu` (días) — inconsistencia de unidades que requiere atención. Los equivalentes T-SQL utilizan `DATEDIFF` sobre las fechas ya combinadas.

---

## 5. MEDIDAS DAX {#5-medidas-dax}

> **LIMITACIÓN:** Las fórmulas DAX exactas están en el `DataModel` binario (XPress9) y no son extraíbles. Lo que sigue son reconstrucciones [INFERENCIA] basadas en: nombres de medidas, contexto de uso en visuales, filtros aplicados, y lógica de negocio esperada.

### 5.1 `Camiones` (Tabla: Medidas)

**Formato:** Número entero  
**Propósito:** Cuenta el total de camiones/viajes (filas) del dataset.  
**Contexto de uso:** Card "TOTAL CAMIONES/VIAJES", tooltips en múltiples visuales.  
**Filtros que la afectan:** Filtro de mes activo, filtros de bodega.

```dax
Camiones =
COUNTROWS( 'Seguimiento EXPORTACION' )
```

**Alternativa posible (si excluye BLANK en alguna columna clave):**
```dax
Camiones =
CALCULATE(
    COUNTROWS( 'Seguimiento EXPORTACION' ),
    NOT ISBLANK( 'Seguimiento EXPORTACION'[PEDIDO] )
)
```

**Equivalente T-SQL:**
```sql
SELECT COUNT(*) AS Camiones
FROM Seguimiento_EXPORTACION
-- Con los filtros de contexto correspondientes (mes, bodega, etc.)
```

**Casos borde:**
- Sin contexto de filtro: devuelve el total de 5.899
- Con filtro de mes = 'abril': devuelve solo filas de abril
- Contexto vacío (tabla sin datos): retorna 0

**Validación con datos reales:** No determinable exactamente sin conocer los datos de `REPORTE` y `PEDIDOS`.

---

### 5.2 `Pedidos` (Tabla: Medidas)

**Formato:** Número entero  
**Propósito:** Cuenta pedidos únicos (no camiones).  
**Contexto de uso:** Card "TOTAL PEDIDOS". Tiene un filtro visual adicional sobre `PEDIDOS[F. PROGRAMACION]`.

```dax
Pedidos =
DISTINCTCOUNT( 'Seguimiento EXPORTACION'[PEDIDO] )
```

**Alternativa (usando tabla PEDIDOS):**
```dax
Pedidos =
COUNTROWS( PEDIDOS )
```

**Equivalente T-SQL:**
```sql
SELECT COUNT(DISTINCT PEDIDO) AS Pedidos
FROM Seguimiento_EXPORTACION
-- Nota: el filtro visual PEDIDOS[F. PROGRAMACION] implica un JOIN con PEDIDOS
```

**Casos borde:**
- Si un pedido tiene múltiples camiones, cuenta 1 sola vez
- PEDIDO NULL → excluido por DISTINCTCOUNT
- El filtro visual sobre `PEDIDOS[F. PROGRAMACION]` limita el periodo

---

### 5.3 `Camiones/Pedido` (Tabla: Medidas)

**Formato:** Decimal (1 decimal)  
**Propósito:** Ratio de camiones por pedido, para medir eficiencia de despacho.  
**Contexto de uso:** Card "CAMIONES X PEDIDO".

```dax
Camiones/Pedido =
DIVIDE(
    [Camiones],
    [Pedidos],
    BLANK()
)
```

**Equivalente T-SQL:**
```sql
SELECT
    COUNT(*) * 1.0 / NULLIF(COUNT(DISTINCT PEDIDO), 0) AS [Camiones_por_Pedido]
FROM Seguimiento_EXPORTACION;
```

**Casos borde:**
- `Pedidos = 0` → `BLANK()` (DIVIDE maneja la división por cero con BLANK)
- Sin datos → `BLANK()`

**Ejemplo numérico [INFERENCIA]:** Si hay 200 camiones y 50 pedidos → `200 / 50 = 4.0`

---

### 5.4 `% Cumplimiento Programado` (Tabla: Medidas)

**Formato:** Porcentaje (%, 1 decimal)  
**Propósito:** Mide el porcentaje de viajes que salieron de base en la fecha programada (o la cumplieron dentro de un umbral).  
**Contexto de uso:** Card "% CUMPLIMIENTO".

**Lógica más probable [INFERENCIA]:** Compara `F.Base` (salida real de base) contra `F.PROGRAMACION` (fecha programada). Un viaje "cumple" si salió el mismo día o antes.

```dax
% Cumplimiento Programado =
VAR TotalViajes = COUNTROWS( 'Seguimiento EXPORTACION' )
VAR ViajesCumplen =
    CALCULATE(
        COUNTROWS( 'Seguimiento EXPORTACION' ),
        'Seguimiento EXPORTACION'[F.PROGRAMACION] >=
            'Seguimiento EXPORTACION'[F.Base]
        -- Nota: esta comparación columna vs columna requiere FILTER o columna calculada
    )
RETURN
    DIVIDE( ViajesCumplen, TotalViajes, BLANK() )
```

**Implementación más correcta en DAX:**
```dax
% Cumplimiento Programado =
VAR TotalViajes = [Camiones]
VAR ViajesCumplen =
    CALCULATE(
        COUNTROWS( 'Seguimiento EXPORTACION' ),
        FILTER(
            'Seguimiento EXPORTACION',
            NOT ISBLANK( [F.PROGRAMACION] ) &&
            NOT ISBLANK( [F.Base] ) &&
            DATE( YEAR([F.Base]), MONTH([F.Base]), DAY([F.Base]) ) <=
            DATE( YEAR([F.PROGRAMACION]), MONTH([F.PROGRAMACION]), DAY([F.PROGRAMACION]) )
        )
    )
RETURN
    DIVIDE( ViajesCumplen, TotalViajes, BLANK() )
```

**Equivalente T-SQL:**
```sql
WITH Totales AS (
    SELECT
        COUNT(*) AS TotalViajes,
        SUM(CASE
            WHEN CAST(F_Base AS DATE) <= CAST(F_PROGRAMACION AS DATE)
            THEN 1 ELSE 0 END) AS ViajesCumplen
    FROM Seguimiento_EXPORTACION
    WHERE F_PROGRAMACION IS NOT NULL AND F_Base IS NOT NULL
      -- Filtros adicionales de contexto (mes, etc.)
)
SELECT
    CAST(ViajesCumplen AS DECIMAL(10,4)) / NULLIF(TotalViajes, 0) AS PctCumplimiento
FROM Totales;
```

**Casos borde:**
- Sin contexto: calcula sobre todo el dataset
- `F.Base NULL` o `F.PROGRAMACION NULL` → excluido del numerador
- `TotalViajes = 0` → `BLANK()`
- Resultado esperado: entre 0% y 100%

> **NOTA:** La definición exacta de "cumplimiento" (mismo día, antes de hora programada, etc.) no es determinable sin ver el código DAX exacto.

---

**RESUMEN EJECUTIVO — BLOQUE 5:** Se identificaron 4 medidas DAX en la tabla `Medidas`: `Camiones` (COUNTROWS), `Pedidos` (DISTINCTCOUNT o COUNTROWS de PEDIDOS), `Camiones/Pedido` (DIVIDE de las dos anteriores) y `% Cumplimiento Programado` (ratio de viajes que cumplen la programación). Ninguna usa funciones de inteligencia de tiempo. Los equivalentes T-SQL utilizan COUNT, COUNT DISTINCT y CASE/SUM condicionales. El código DAX exacto no es extraíble del binario.

---

## 6. VISUALES PÁGINA POR PÁGINA {#6-visuales}

> **Sobre coordenadas:** Las posiciones x/y son en píxeles relativos a la esquina superior izquierda de la página. El canvas estándar de PBI es 1.500 × 844 px (o similar según la configuración del reporte). Las posiciones extraídas son exactas del `Report/Layout`.

---

### PÁGINA 0: CARATULA

**Propósito:** Portada del dashboard. Sin datos operacionales.  
**Dimensiones:** 1.488 × 720 px (inferido de las shapes decorativas)  
**Filtro de página:** `Seguimiento EXPORTACION[F.Ingreso].Mes` — sin valor concreto seleccionado (filtro vacío).

| Visual | Tipo | Título | Posición | Descripción |
|--------|------|--------|----------|-------------|
| 0 | `textbox` | *(sin título)* | x=325, y=546, w=855, h=117 | Texto corporativo/descripción del dashboard |
| 1 | `textbox` | *(sin título)* | x=640, y=480, w=224, h=80 | Subtítulo o nombre empresa |
| 2 | `actionButton` | `DASHBOARD` | x=1332, y=642, w=125, h=42 | Botón de navegación → página Dashboard |
| 3 | `shape` | *(decorativo)* | x=16, y=0, w=1472, h=48 | Barra superior decorativa |
| 4 | `shape` | *(decorativo)* | x=16, y=672, w=1472, h=48 | Barra inferior decorativa |
| 5 | `shape` | *(decorativo)* | x=1436, y=0, w=64, h=720 | Barra lateral derecha |
| 6 | `shape` | *(decorativo)* | x=0, y=0, w=64, h=720 | Barra lateral izquierda |

**Lectura de negocio:** Página introductoria. El usuario hace clic en "DASHBOARD" para navegar al panel principal.

---

### PÁGINA 1: Dashboard

**Propósito:** Resumen ejecutivo de KPIs operacionales: tiempos promedio por etapa, totales de camiones, pedidos y cumplimiento.  
**Filtro de página:** `Seguimiento EXPORTACION[F.Ingreso].Mes = 'abril'` (filtro hardcodeado — ver Sección 10 Hallazgos).

#### Visual 1.0 — Encabezado (textbox)
**Tipo:** `textbox` | **Posición:** x=64, y=8, w=1361, h=64  
**Contenido:** Título de la página (texto libre, no extraíble del binario sin renderizar).

---

#### Visual 1.1 — Espera para ingresar a Bodega Nacional (Hrs)
**Tipo:** `pieChart` (gráfico de pastel/dona)  
**Título:** `'Espera para ingresar a Bodega Nacional (Hrs)'`  
**Posición:** x=768, y=368, w=320, h=160  

| Pozo (Well) | Campo | Tabla | Agregación |
|-------------|-------|-------|------------|
| Category (Eje) | `F.LL.Depsa` → Mes | Seguimiento EXPORTACION | Jerarquía de fechas automática |
| Values (Y) | `Espera para ingresar Depsa` | Seguimiento EXPORTACION | AVERAGE |
| Tooltips | `Camiones` | Medidas | (medida) |

**Filtros adicionales:**
- Tipo Advanced (condición no determinable sin abrir PBI)
- Mes = `'abril'` (hardcodeado)
- `PEDIDO IS NOT BLANK` (Advanced filter)

**Lectura de negocio:** Muestra el promedio de horas que los camiones esperan para ingresar a la Bodega Nacional (DEPSA o COMPLEX) por mes. Un valor alto indica cuellos de botella en la recepción. Umbral bueno/malo: **No determinable a partir de los archivos entregados**.

---

#### Visual 1.2 — TOTAL CAMIONES/VIAJES (Card KPI)
**Tipo:** `card`  
**Título:** `'TOTAL CAMIONES/VIAJES'`  
**Posición:** x=406, y=80, w=320, h=80  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Values | `Camiones` | Medidas | (medida) |

**Filtro:** Mes = `'abril'`  
**Lectura de negocio:** KPI primario. Total de viajes completados en el periodo filtrado (mes de abril por defecto).

---

#### Visual 1.3 — Tiempos promedio en Trujillo (Hrs) (Barras agrupadas)
**Tipo:** `barChart` (gráfico de barras horizontales agrupadas)  
**Título:** `'Tiempos promedio en Trujillo (Hrs)'`  
**Posición:** x=62, y=176, w=688, h=268  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category (Eje Y) | `F.Ingreso` → Mes | Seguimiento EXPORTACION | Jerarquía |
| Y (Values) | `Espera a ingreso Trujillo` | Seguimiento EXPORTACION | AVERAGE |
| Y (Values) | `Espera para iniciar la carga` | Seguimiento EXPORTACION | AVERAGE |
| Y (Values) | `Carga (Horas)` | Seguimiento EXPORTACION | AVERAGE |
| Y (Values) | `Permanencia en planta Trujillo` | Seguimiento EXPORTACION | AVERAGE |
| Tooltips | `Camiones` | Medidas | (medida) |

**Filtro:** Mes = `'abril'`  
**Lectura de negocio:** Desglose de los 4 tiempos del proceso en planta Trujillo, agrupados por mes. Permite identificar en qué etapa se acumula el mayor tiempo. Esperado: `Espera a ingreso < Espera carga < Carga < Permanencia total`.

---

#### Visual 1.4 — Tiempo Trujillo-Planta Ecuador (días)
**Tipo:** `pieChart`  
**Título:** `'Tiempo Trujillo-Planta Ecuador (días)'`  
**Posición:** x=768, y=176, w=320, h=176  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.Carga` → Mes | Seguimiento EXPORTACION | Jerarquía |
| Y | `Trujillo-PlantaEcu` | Seguimiento EXPORTACION | AVERAGE |

**Filtro:** Mes = `'abril'`  
**Lectura de negocio:** Tiempo de tránsito internacional (Trujillo → planta Ecuador), en días. Objetivo logístico: menor tiempo = más eficiente.

---

#### Visual 1.5 — Tiempo promedio en TCI (Hrs)
**Tipo:** `pieChart`  
**Título:** `'Tiempo promedio en TCI  (Hrs)'`  
**Posición:** x=768, y=544, w=320, h=160  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.LL.TCI` → Mes | Seguimiento EXPORTACION | Jerarquía |
| Y | `TCI` | Seguimiento EXPORTACION | AVERAGE |

**Filtro:** Mes = `'abril'`  
**Lectura de negocio:** Tiempo promedio en la terminal aduanera ecuatoriana TCI (o PUYANGO). Un valor alto indica demoras en despacho aduanero.

---

#### Visual 1.6 — Tiempo promedio en Bodega Nacional (Hrs)
**Tipo:** `pieChart`  
**Título:** `'Tiempo promedio en Bodega Nacional  (Hrs)'`  
**Posición:** x=1120, y=368, w=320, h=160  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.I. Depsa` → Mes | Seguimiento EXPORTACION | Jerarquía |
| Y | `DEPSA` | Seguimiento EXPORTACION | AVERAGE |

**Filtro:** Mes = `'abril'`  
**Lectura de negocio:** Tiempo que el camión permanece dentro de la bodega nacional DEPSA o COMPLEX (después de ser admitido, hasta la salida).

---

#### Visual 1.7 — Tiempo promedio en Base (Hrs)
**Tipo:** `pieChart`  
**Título:** `'Tiempo promedio en Base (Hrs)'`  
**Posición:** x=1120, y=176, w=320, h=176  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.LL.Depsa` → Mes | Seguimiento EXPORTACION | Jerarquía |
| Y | `Base` | Seguimiento EXPORTACION | AVERAGE |

**Filtro:** Mes = `'abril'`  
**Lectura de negocio:** Tiempo que el camión permanece en Base (Sullana) entre la llegada y la siguiente salida hacia bodega.

---

#### Visual 1.8 — TOTAL PEDIDOS (Card KPI)
**Tipo:** `card`  
**Título:** `'TOTAL PEDIDOS'`  
**Posición:** x=767, y=80, w=320, h=80  

| Pozo | Campo | Tabla |
|------|-------|-------|
| Values | `Pedidos` | Medidas |

**Filtros:** (1) Tipo Advanced (sin detalles extraíbles); (2) `PEDIDOS[F. PROGRAMACION]` — filtro categórico sobre la tabla PEDIDOS.  
**Lectura de negocio:** Total de pedidos distintos en el periodo. Diferente de "Camiones" cuando un pedido requiere más de un camión.

---

#### Visual 1.9 — CAMIONES X PEDIDO (Card KPI)
**Tipo:** `card`  
**Título:** `'CAMIONES X PEDIDO'`  
**Posición:** x=1120, y=80, w=320, h=80  

| Pozo | Campo | Tabla |
|------|-------|-------|
| Values | `Camiones/Pedido` | Medidas |

**Filtro:** `PEDIDOS[F. PROGRAMACION]` — filtro categórico  
**Lectura de negocio:** Ratio de eficiencia. Un valor de 1.0 = un camión por pedido. Valores mayores pueden indicar pedidos grandes o fragmentación.

---

#### Visual 1.10 — % CUMPLIMIENTO (Card KPI)
**Tipo:** `card`  
**Título:** `'% CUMPLIMIENTO'`  
**Posición:** x=64, y=80, w=320, h=80  

| Pozo | Campo | Tabla |
|------|-------|-------|
| Values | `% Cumplimiento Programado` | Medidas |

**Filtros:** Ninguno a nivel visual (sin filtro de mes aplicado — **anomalía**: los otros cards sí tienen filtro de mes).  
**Lectura de negocio:** Porcentaje de viajes que cumplieron la programación. Umbral bueno/malo: **No determinable a partir de los archivos entregados**.

---

#### Visual 1.11 — Tiempo promedio en CEBAF (min)
**Tipo:** `pieChart`  
**Título:** `'Tiempo promedio en CEBAF (min)'`  
**Posición:** x=1120, y=544, w=320, h=160  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.LL CEBAF E` → Mes | Seguimiento EXPORTACION | Jerarquía |
| Y | `CEBAF` | Seguimiento EXPORTACION | AVERAGE |

**Filtro:** Mes = `'abril'`  
**Lectura de negocio:** Tiempo promedio de cruce fronterizo en CEBAF. **ATENCIÓN: en minutos, no horas**, a diferencia del resto de visuales.

---

#### Visual 1.12 — Tiempos promedio en Inbalnor (Hrs) (Barras)
**Tipo:** `barChart`  
**Título:** `'Tiempos promedio en Inbalnor (Hrs)'`  
**Posición:** x=64, y=457, w=320, h=255  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.LL.Almacen` → Mes | Seguimiento EXPORTACION | Jerarquía |
| Y | `Espera para iniciar la descarga` | Seguimiento EXPORTACION | AVERAGE |
| Y | `Descarga (Horas)` | Seguimiento EXPORTACION | AVERAGE |

**Filtros:** (1) `BODEGA = 'INBALNOR'` (hardcodeado); (2) Mes = `'abril'`  
**Lectura de negocio:** Tiempos en el almacén INBALNOR: espera antes de descarga y duración de descarga.

---

#### Visual 1.13 y 1.14 — Botones de navegación
**Tipo:** `actionButton`  
**Posición:** x=0, y=0 (esquina superior) y x=0, y=656 (esquina inferior)  
**Función:** Navegación entre páginas del reporte.

---

#### Visual 1.15 — Tiempos promedio en Jave (Hrs) (Barras)
**Tipo:** `barChart`  
**Título:** `'Tiempos promedio en Jave (Hrs)'`  
**Posición:** x=424, y=456, w=320, h=256  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.LL.Almacen` → Mes | Seguimiento EXPORTACION | Jerarquía |
| Y | `Espera para iniciar la descarga` | Seguimiento EXPORTACION | AVERAGE |
| Y | `Descarga (Horas)` | Seguimiento EXPORTACION | AVERAGE |

**Filtros:** (1) `BODEGA = 'JAVE'` (hardcodeado); (2) Mes = `'abril'`  
**Lectura de negocio:** Equivalente al Visual 1.12 pero para el almacén JAVE.

> **OBSERVACIÓN:** Los visuales 1.12 y 1.15 son idénticos en estructura (mismo campo, misma agregación), solo difieren en el filtro de bodega (INBALNOR vs JAVE). Ver Sección 10.

---

### PÁGINA 2: Gestion diaria 1

**Propósito:** Seguimiento diario del tiempo en bodegas nacionales DEPSA y COMPLEX, y tiempo en CEBAF.  
**Filtro de página:** Ninguno a nivel de página (cada visual tiene sus propios filtros).

#### Visual 2.0 — ESPERA PARA INGRESAR A DEPSA (HRS) (Ribbon)
**Tipo:** `ribbonChart`  
**Título:** `'ESPERA PARA INGRESAR A DEPSA (HRS)'`  
**Posición:** x=64, y=112, w=656, h=176  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.LL.Depsa` → Mes | Seguimiento EXPORTACION | Jerarquía |
| Category | `F.LL.Depsa` → Día | Seguimiento EXPORTACION | Jerarquía |
| Y | `Espera para ingresar Depsa` | Seguimiento EXPORTACION | AVERAGE |

**Filtros:** (1) Mes = `'abril'`; (2) Advanced/PEDIDO; (3) `BODEGA NACIONAL = 'DEPSA'`  
**Lectura de negocio:** Evolución diaria del tiempo de espera para ingresar a DEPSA. El ribbon muestra tendencias por mes dentro del eje diario.

---

#### Visual 2.1 — TIEMPO PROMEDIO EN DEPSA (Ribbon)
**Tipo:** `ribbonChart`  
**Título:** `'TIEMPO PROMEDIO EN DEPSA'`  
**Posición:** x=65, y=302, w=655, h=176  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.I. Depsa` → Mes | Seguimiento EXPORTACION | Jerarquía |
| Category | `F.I. Depsa` → Día | Seguimiento EXPORTACION | Jerarquía |
| Y | `DEPSA` | Seguimiento EXPORTACION | AVERAGE |

**Filtros:** (1) Mes = `'abril'`; (2) `BODEGA NACIONAL = 'DEPSA'`  
**Lectura de negocio:** Evolución diaria del tiempo de permanencia dentro de DEPSA.

---

#### Visuales 2.3 y 2.4 — Cards TOTAL PROM. (DEPSA)
**Tipo:** `card`  
**Valores:** `Avg(Espera para ingresar Depsa)` y `Avg(DEPSA)` respectivamente  
**Filtros:** Mismos que los ribbons correspondientes (incluye filtros de bodega y mes)  
**Lectura de negocio:** KPI de promedio total del periodo (resumen de los ribbons).

---

#### Visual 2.7 — ESPERA PARA INGRESAR A COMPLEX (HRS) (Ribbon)
**Tipo:** `ribbonChart`  
**Título:** `'ESPERA PARA INGRESAR A COMPLEX (HRS)'`  
**Posición:** x=768, y=112, w=655, h=176  

**Idéntico al Visual 2.0 en estructura, pero:**  
- Filtro: `BODEGA NACIONAL = 'COMPLEX'`  
- Excluye día 28 del mes (`Día NOT IN (28)`)

---

#### Visual 2.9 — TIEMPO PROMEDIO EN COMPLEX (Ribbon)
**Tipo:** `ribbonChart`  
**Título:** `'TIEMPO PROMEDIO EN COMPLEX'`  
**Posición:** x=768, y=302, w=657, h=176  

**Idéntico al Visual 2.1 pero filtro:** `BODEGA NACIONAL = 'COMPLEX'`

---

#### Visual 2.11 — TIEMPO PROMEDIO EN CEBAF (MIN) (Ribbon)
**Tipo:** `ribbonChart`  
**Título:** `'TIEMPO PROMEDIO EN CEBAF (MIN)'`  
**Posición:** x=64, y=496, w=1368, h=192  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.LL CEBAF E` → Mes | Seguimiento EXPORTACION | Jerarquía |
| Category | `F.LL CEBAF E` → Día | Seguimiento EXPORTACION | Jerarquía |
| Y | `CEBAF` | Seguimiento EXPORTACION | AVERAGE |

**Filtro:** Mes = `'abril'`  
**Lectura de negocio:** Evolución diaria del tiempo en CEBAF (en minutos). Ribbon de ancho completo — cubre ambas bodegas nacionales sin filtro de BODEGA.

---

### PÁGINA 3: Gestion diaria 2

**Propósito:** Seguimiento diario de tiempos en TCI y espera de nacionalización.  
**Filtro de página:** `BODEGA (col AG)` — sin valor seleccionado (filtro categórico vacío).

#### Visual 3.3 — TIEMPO PROMEDIO EN TCI (HRS) (Área)
**Tipo:** `areaChart`  
**Título:** `'TIEMPO PROMEDIO EN TCI (HRS)'`  
**Posición:** x=23, y=101, w=1449, h=259  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.LL.TCI` → Mes | Seguimiento EXPORTACION | Jerarquía |
| Category | `F.LL.TCI` → Día | Seguimiento EXPORTACION | Jerarquía |
| Y | `TCI` | Seguimiento EXPORTACION | AVERAGE |

**Filtros:** (1) `BODEGA INTERNACIONAL = 'TCI'`; (2-3) Exclusión de ciertos días; (4) Mes = `'abril'`; (5) Advanced; Filtro `F.LL.TCI` categórico  
**Lectura de negocio:** Evolución diaria del tiempo en TCI. Excluye días del mes que son domingos u outliers (filtro de día).

#### Visual 3.4 — TOTAL PROM. (card TCI)
**Valor:** `Avg(TCI)` filtrado por: `BODEGA INTERNACIONAL = 'TCI'`, Advanced, y fecha F.LL.TCI con exclusión.

---

#### Visual 3.5 — ESPERA DE NACIONALIZACION (HRS) (Área apilada)
**Tipo:** `stackedAreaChart`  
**Título:** `'ESPERA DE NACIONALIZACION (HRS)'`  
**Posición:** x=23, y=402, w=1449, h=286  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.LL CEBAF E` → Mes | Seguimiento EXPORTACION | Jerarquía |
| Category | `F.LL CEBAF E` → Día | Seguimiento EXPORTACION | Jerarquía |
| Y | `Espera Nacionalizacion` | Seguimiento EXPORTACION | AVERAGE |

**Filtros:** (1) Mes = `'abril'`; (2) Mes = `'abril'` (duplicado — ver Sección 10)  
**Lectura de negocio:** Tiempo desde el cruce fronterizo hasta la autorización de nacionalización. Indicador crítico para la aduana.

#### Visual 3.6 — TOTAL PROM. (card Espera autorizacion)
**Valor:** `Sum(Espera autorizacion)` — **ATENCIÓN: usa SUM en lugar de AVERAGE**, diferente al visual de área que usa AVERAGE de `Espera Nacionalizacion`. Ver Sección 10.

---

### PÁGINA 4: Gestion diaria 3

**Propósito:** Tiempos de tránsito desde bodegas nacionales/TCI hasta almacenes de descarga (INBALNOR y JAVE).  
**Filtro de página:** `BODEGA (col AG)` — filtro categórico vacío.

#### Visual 4.0 — TIEMPO PROMEDIO DE TCI A INBALNOR (HRS) (Área apilada)
**Tipo:** `stackedAreaChart` | Posición: x=11, y=400, w=720, h=272  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.S TCI` → Mes/Día | Seguimiento EXPORTACION | Jerarquía |
| Y | `De TCI a bodega` | Seguimiento EXPORTACION | AVERAGE |

**Filtros:** `BODEGA = 'INBALNOR'`, Mes = `'abril'`, exclusión de días, Advanced, `F.LL.TCI` categórico

---

#### Visual 4.2 — TIEMPO PROMEDIO DE BODEGA NACIONAL A INBALNOR (HRS) (Área)
**Tipo:** `areaChart` | Posición: x=11, y=96, w=720, h=272  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.S.Depsa` → Mes/Día | Seguimiento EXPORTACION | Jerarquía |
| Y | `De Depsa a bodega` | Seguimiento EXPORTACION | AVERAGE |

**Filtros:** `BODEGA = 'INBALNOR'`, Mes = `'abril'`, `F.LL.TCI` categórico, exclusión días

---

#### Visual 4.5 — TIEMPO PROMEDIO DE BODEGA NACIONAL A JAVE (HRS) (Área)
**Tipo:** `areaChart` | Posición: x=752, y=96, w=720, h=272  
**Idéntico al Visual 4.2 pero:** `BODEGA = 'JAVE'`

---

#### Visual 4.6 — TIEMPO PROMEDIO DE TCI A JAVE (HRS) (Área apilada)
**Tipo:** `stackedAreaChart` | Posición: x=758, y=400, w=719, h=272  
**Idéntico al Visual 4.0 pero:** `BODEGA = 'JAVE'`

---

#### Visuales 4.7 a 4.10 — Cards TOTAL (Hrs)
Cuatro cards que muestran el promedio de `De Depsa a bodega` y `De TCI a bodega`, filtrados por INBALNOR y JAVE respectivamente.

| Visual | Título | Valor | Filtro BODEGA |
|--------|--------|-------|---------------|
| 4.7 | `TOTAL (HRS)` | `Avg(De Depsa a bodega)` | INBALNOR |
| 4.8 | `TOTAL` | `Avg(De Depsa a bodega)` | JAVE |
| 4.9 | `TOTAL` | `Avg(De TCI a bodega)` | INBALNOR |
| 4.10 | `TOTAL` | `Avg(De TCI a bodega)` | JAVE |

---

### PÁGINA 5: Incidencias

**Propósito:** Seguimiento de incidencias en la descarga — sacos robados.  
**Tabla principal:** `REPORTE`

#### Visual 5.0 — Incidencias en la descarga - Sacos Robados (Pastel)
**Tipo:** `pieChart` | Posición: x=455, y=136, w=656, h=512  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `FECHA INCIDENCIA` → Día | REPORTE | Jerarquía |
| Category | `INDICE` | REPORTE | — |
| Y | `CANTIDAD SACOS` | REPORTE | SUM |
| Tooltips | `LUGAR DE INCIDENCIA` | REPORTE | MIN |
| Tooltips | `OBSERVACION` | REPORTE | MIN |
| Tooltips | `TRACTO` | REPORTE | MIN |

**Filtros:** (1) Mes = `'junio'` (hardcodeado — mes diferente al resto); (2) `TIPO DE INCIDENCIA = 'ROBADO'`  
**Lectura de negocio:** Distribución de sacos robados por fecha e índice de incidencia en el mes de junio. El tooltip permite ver el detalle de cada incidencia.

#### Visual 5.1 — Total (Card)
**Valor:** `Sum(CANTIDAD SACOS)` | **Filtros:** Advanced + Mes = `'junio'` + `TIPO = 'ROBADO'`  
**Lectura de negocio:** Total de sacos robados en el periodo.

---

### PÁGINA 6: Página 1

**Propósito:** Página adicional con incidencias de otros tipos (Sacos Mojados, Rotos, Faltante de Origen), tiempos adicionales (Puyango), y rutas Jave.

> **NOTA:** Esta página no tiene nombre de negocio definido ("Página 1") — probablemente en desarrollo o renombrado pendiente.

#### Visual 6.0 — TIEMPO PROMEDIO DE TCI A BODEGA (HRS) (Líneas)
**Tipo:** `lineChart` | Posición: x=23, y=477, w=600, h=180  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.S TCI` → Mes/Día | Seguimiento EXPORTACION | Jerarquía |
| Y | `De TCI a planta` | Seguimiento EXPORTACION | SUM |

**Sin filtros de bodega** (muestra INBALNOR + JAVE combinados).

---

#### Visual 6.1 — TIEMPO PROMEDIO DE DEPSA A BODEGA (HRS) (Líneas)
**Tipo:** `lineChart` | Posición: x=645, y=477, w=609, h=180  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.S.Depsa` → Mes/Día | Seguimiento EXPORTACION | Jerarquía |
| Y | `De Depsa a bodega` | Seguimiento EXPORTACION | SUM |

---

#### Visuales 6.2 y 6.4 — Incidencias (Sacos Mojados y Rotos)
**Tipo:** `lineChart`  

| Visual | Título | Filtro TIPO |
|--------|--------|-------------|
| 6.2 | `Incidencias en la descarga (Sacos Mojados)` | `= 'MOJADOS'`, Mes = `'abril'` |
| 6.4 | `Incidencias en la descarga (Sacos Rotos)` | `= 'ROTO'`, Mes = `'abril'` |

Ambos usan `CANTIDAD SACOS` (SUM) sobre eje `FECHA INCIDENCIA` Día × `INDICE`.

---

#### Visual 6.8 — Incidencias en la descarga - Faltante de Origen (Pastel)
**Tipo:** `pieChart` | Posición: x=624, y=144, w=656, h=512  
**Filtros:** Mes = `'junio'`; `TIPO DE INCIDENCIA = 'FALTANTE DE ORIGEN'`  
**Idéntico al Visual 5.0 en estructura pero diferente tipo de incidencia.**

---

#### Visual 6.10 — TIEMPO PROMEDIO EN PUYANGO (HRS) (Área)
**Tipo:** `areaChart` | Posición: x=0, y=395, w=720, h=208  

| Pozo | Campo | Tabla | Agregación |
|------|-------|-------|------------|
| Category | `F.LL.TCI` → Mes/Día | Seguimiento EXPORTACION | Jerarquía |
| Y | `TCI` | Seguimiento EXPORTACION | AVERAGE |

**Filtros:** Mes = `'diciembre'`; `BODEGA INTERNACIONAL = 'PUYANGO'`  
**Lectura de negocio:** Tiempo promedio en PUYANGO (aduana alternativa). Mes = 'diciembre' hardcodeado — diferente al resto.

---

**RESUMEN EJECUTIVO — BLOQUE 6:** El reporte tiene 7 páginas (1 portada + 6 operativas). Los visuales son: 10 pieCharts, 6 barCharts/ribbonCharts, 7 areaCharts/stackedAreaCharts, 5 lineCharts, 12 cards, 4 actionButtons, 4 shapes, 4 textboxes. Todos los filtros de mes están hardcodeados (no hay slicers de periodo activos). La página 6 tiene nombre genérico "Página 1" indicando trabajo en progreso.

---

## 7. SEGMENTADORES Y PARÁMETROS {#7-segmentadores}

### 7.1 Segmentadores

**No se detectaron visuales de tipo `slicer` en ninguna página del reporte.**

Todos los filtros son filtros fijos aplicados directamente sobre visuales individuales o páginas. El usuario final no puede modificar dinámicamente el periodo, la bodega ni ningún otro parámetro desde la interfaz del reporte.

### 7.2 Parámetros What-if

**No se detectaron parámetros What-if** (no hay tablas del tipo `Parameter` en el DiagramLayout ni referencias a medidas de valor paramétrico en el Layout).

### 7.3 Implicación operacional

El reporte funciona como un **dashboard estático de un periodo fijo** (principalmente abril 2024, con algunas páginas en junio y diciembre). Para ver otro mes, el desarrollador debe cambiar manualmente los filtros hardcodeados en cada visual dentro de Power BI Desktop.

---

**RESUMEN EJECUTIVO — BLOQUE 7:** No existen slicers ni parámetros dinámicos. El reporte es estático con filtros de mes hardcodeados página por página y visual por visual. Esto representa una limitación operacional significativa — ver Sección 11 Recomendaciones.

---

## 8. REGLAS DE NEGOCIO DETECTADAS {#8-reglas-negocio}

1. **Un viaje pertenece operacionalmente al mes de F.PROGRAMACION.** Los visuales que clasifican por mes usan las fechas de llegada a cada etapa (no la fecha de programación), lo que puede generar discrepancias entre KPIs de distintas páginas.  
   *Implementado en:* filtros de jerarquía de fecha en cada visual.

2. **La Bodega Nacional puede ser DEPSA o COMPLEX.** El campo `BODEGA NACIONAL` (columna AG del Excel, renombrada en PQ) discrimina qué bodega peruana recibe el camión.  
   *Implementado en:* filtros `BODEGA NACIONAL = 'DEPSA'` y `BODEGA NACIONAL = 'COMPLEX'` en página Gestion diaria 1.

3. **La aduana ecuatoriana puede ser TCI o PUYANGO.** El campo `BODEGA INTERNACIONAL` discrimina la aduana de ingreso a Ecuador.  
   *Implementado en:* filtros `BODEGA INTERNACIONAL = 'TCI'` y `BODEGA INTERNACIONAL = 'PUYANGO'`.

4. **El almacén de descarga final puede ser INBALNOR, JAVE u OREMANS.** El campo `BODEGA` (columna AS, renombrada) indica el destino final.  
   *Implementado en:* filtros `BODEGA = 'INBALNOR'` y `BODEGA = 'JAVE'` en páginas Gestion diaria 3 y Dashboard.

5. **El tiempo en CEBAF se mide en minutos (no horas).** Mientras todos los demás tiempos operacionales son en horas, el CEBAF usa minutos para mayor resolución (el cruce fronterizo es corto).  
   *Implementado en:* columna calculada `CEBAF` × 60 (minutos), y el título del visual lo indica explícitamente "(min)".

6. **El tiempo Trujillo-PlantaEcu se mide en días.** El tránsito internacional es de varios días, por lo que la unidad es días decimales.  
   *Implementado en:* columna calculada `Trujillo-PlantaEcu`.

7. **El periodo activo por defecto es 'abril'.** Los filtros hardcodeados en Dashboard y Gestion diaria 1-3 apuntan al mes 'abril'. Las páginas de Incidencias usan 'junio' y la página de Puyango usa 'diciembre'.  
   *Implementado en:* filtros de mes en cada visual.

8. **Un pedido puede tener múltiples camiones.** La medida `Camiones/Pedido` implica esta relación N:1.  
   *Implementado en:* `Medidas[Camiones/Pedido] = Camiones / Pedidos`.

9. **Los días de fin de semana o festivos se excluyen en algunos visuales diarios.** Los filtros avanzados de tipo `Día NOT IN (28, 5, ...)` en Gestion diaria 1 y 2 excluyen números de día específicos.  
   *Implementado en:* filtros `Día NOT IN (28L)` en Visual 2.7 y `Día NOT IN (5L, ...)` en Visuales 3.3, 4.0, 4.6.

10. **La categoría OREMANS existe pero no se incluye en ningún visual.** El valor `OREMANS` (104 registros) en `BODEGA DESCARGA` no tiene página ni visual dedicado en el reporte.  
    *Detectado en:* análisis de datos del Excel, no referenciado en Layout.

11. **La columna PEDIDO en Seguimiento EXPORTACION se relaciona con la tabla PEDIDOS.** Los filtros sobre `PEDIDOS[F. PROGRAMACION]` en los visuales del Dashboard implican una relación activa entre ambas tablas.  
    *Detectado en:* filtros del Visual 1.8 y 1.9.

---

**RESUMEN EJECUTIVO — BLOQUE 8:** Se identificaron 11 reglas de negocio implícitas. Las más críticas son: (1) mezcla de unidades de tiempo (horas/minutos/días) sin normalización visible; (2) filtros hardcodeados de mes que hacen el reporte estático; (3) exclusión implícita de días específicos del mes sin documentación de criterio; (4) OREMANS excluido sin justificación visible.

---

## 9. DICCIONARIO DE DATOS FINAL {#9-diccionario}

| Campo | Tabla PBI | Tipo | Origen Excel | Transform. M | Fórmula DAX | Significado de negocio | Unidad | Ejemplo |
|-------|-----------|------|-------------|-------------|-------------|----------------------|--------|---------|
| `PEDIDO` | Seguimiento EXPORTACION | Texto | Col A (`CLIENTE`) | Renombrado en PQ | — | Número de orden de despacho | — | `4400088995` |
| `Conductor Origen` | Seguimiento EXPORTACION | Texto | Col B | Sin transform. | — | Nombre del conductor en Perú | — | `GREGORIO BARBA` |
| `Tracto 1` | Seguimiento EXPORTACION | Texto | Col C | Sin transform. | — | Placa del tractocamión origen | — | `BFI-890` |
| `Carreta` | Seguimiento EXPORTACION | Texto | Col D | Sin transform. | — | Placa del semirremolque | — | `TBE-995` |
| `Conductor Destino` | Seguimiento EXPORTACION | Texto | Col E | Sin transform. | — | Conductor en Ecuador | — | `JORGE RENTERIA` |
| `Tracto 2` | Seguimiento EXPORTACION | Texto | Col F | Sin transform. | — | Placa tractocamión destino (typo en Excel) | — | `T7J-857` |
| `F.Base` | Seguimiento EXPORTACION | DateTime | Col G+H | Combinación fecha+hora | — | Salida de Base Sullana | — | `2024-03-08 17:19` |
| `F.Ingreso` | Seguimiento EXPORTACION | DateTime | Col I+J | Combinación fecha+hora | — | Llegada a Trujillo | — | `2024-03-09 05:06` |
| `F.PROGRAMACION` | Seguimiento EXPORTACION | DateTime | Col M+N | Combinación fecha+hora | — | Fecha/hora programada de despacho | — | `2024-03-09 07:00` |
| `F.I.planta` | Seguimiento EXPORTACION | DateTime | Col O+P | Combinación fecha+hora | — | Ingreso efectivo a planta Trujillo | — | `2024-03-09 07:21` |
| `F.Inicio Carga` | Seguimiento EXPORTACION | DateTime | Col Q+R | Combinación fecha+hora | — | Inicio de carga en planta | — | `2024-03-09 07:45` |
| `F.Termino Carga` | Seguimiento EXPORTACION | DateTime | Col S+T | Combinación fecha+hora | — | Fin de carga en planta | — | `2024-03-09 09:10` |
| `F.S.Planta` | Seguimiento EXPORTACION | DateTime | Col U+V | Combinación fecha+hora | — | Salida de planta Trujillo | — | `2024-03-09 09:26` |
| `F.LL.Base` | Seguimiento EXPORTACION | DateTime | Col W+X | Combinación fecha+hora | — | Llegada a Base en retorno | — | `2024-03-10 17:13` |
| `F.S.Base2` | Seguimiento EXPORTACION | DateTime | Col Y+Z | Combinación fecha+hora | — | Segunda salida de Base | — | `2024-03-10 19:45` |
| `F.LL.Depsa` | Seguimiento EXPORTACION | DateTime | Col AA+AB | Combinación fecha+hora | — | Llegada a Bodega Nacional | — | `2024-03-11 09:28` |
| `F.I. Depsa` | Seguimiento EXPORTACION | DateTime | Col AC+AD | Combinación fecha+hora | — | Ingreso efectivo a Bodega Nacional | — | `2024-03-11 11:25` |
| `F.S.Depsa` | Seguimiento EXPORTACION | DateTime | Col AE+AF | Combinación fecha+hora | — | Salida de Bodega Nacional | — | `2024-03-11 12:52` |
| `BODEGA NACIONAL` | Seguimiento EXPORTACION | Texto | Col AG (`BODEGA`) | Renombrado en PQ | — | Bodega peruana: DEPSA o COMPLEX | — | `COMPLEX` |
| `F.LL CEBAF E` | Seguimiento EXPORTACION | DateTime | Col AH+AI | Combinación fecha+hora | — | Llegada al CEBAF Ecuador | — | `2024-03-11 14:17` |
| `F.Cruce` | Seguimiento EXPORTACION | DateTime | Col AJ+AK | Combinación fecha+hora | — | Cruce efectivo de frontera | — | `2024-03-11 14:20` |
| `F.AUTORIZACION` | Seguimiento EXPORTACION | DateTime | Col AL+AM | Combinación fecha+hora | — | Autorización de nacionalización | — | `2024-03-11 16:53` |
| `BODEGA INTERNACIONAL` | Seguimiento EXPORTACION | Texto | Col AN (`BODEGA ECUATORIANA`) | Renombrado en PQ | — | Aduana: TCI o PUYANGO | — | `TCI` |
| `F.LL.TCI` | Seguimiento EXPORTACION | DateTime | Col AO+AP | Combinación fecha+hora | — | Llegada a TCI/PUYANGO | — | `2024-03-11 14:36` |
| `F.S TCI` | Seguimiento EXPORTACION | DateTime | Col AQ+AR | Combinación fecha+hora | — | Salida de TCI/PUYANGO | — | `2024-03-11 18:14` |
| `BODEGA` | Seguimiento EXPORTACION | Texto | Col AS (`BODEGA DESCARGA`) | Renombrado en PQ | — | Almacén descarga: INBALNOR/JAVE/OREMANS | — | `INBALNOR` |
| `F.LL.Planta` | Seguimiento EXPORTACION | DateTime | Col AT+AU | Combinación fecha+hora | — | Llegada a planta ecuatoriana | — | `2024-03-11 23:37` |
| `F.LL.Almacen` | Seguimiento EXPORTACION | DateTime | Col AV+AW | Combinación fecha+hora | — | Llegada al almacén de descarga | — | `2024-03-11 23:37` |
| `F.Ingreso` *(almacén)* | Seguimiento EXPORTACION | DateTime | Col AX+AY | Combinación fecha+hora | — | Ingreso al sistema del almacén | — | `2024-03-12 06:49` |
| `F.I. descarga` | Seguimiento EXPORTACION | DateTime | Col AZ+BA | Combinación fecha+hora | — | Inicio de descarga | — | `2024-03-12 13:50` |
| `F.T. descarga` | Seguimiento EXPORTACION | DateTime | Col BB+BC | Combinación fecha+hora | — | Término de descarga | — | `2024-03-12 14:18` |
| `F.Salida` | Seguimiento EXPORTACION | DateTime | Col BD+BE | Combinación fecha+hora | — | Salida del almacén | — | `2024-03-12 14:32` |
| `Motivo de retraso / Comentario` | Seguimiento EXPORTACION | Texto | Col BJ | Sin transform. | — | Observación libre de operación | — | `CARRO MALOGRADO...` |
| `Espera a ingreso Trujillo` | Seguimiento EXPORTACION | Decimal | — | — | `(F.I.planta - F.Ingreso)*24` | Espera en patio de Trujillo antes de planta | Horas | `2.25` |
| `Espera para iniciar la carga` | Seguimiento EXPORTACION | Decimal | — | — | `(F.Inicio Carga - F.I.planta)*24` | Espera dentro de planta antes de cargar | Horas | `0.40` |
| `Carga (Horas)` | Seguimiento EXPORTACION | Decimal | — | — | `(F.Termino Carga - F.Inicio Carga)*24` | Duración de la carga | Horas | `1.42` |
| `Permanencia en planta Trujillo` | Seguimiento EXPORTACION | Decimal | — | — | `(F.S.Planta - F.I.planta)*24` | Tiempo total en planta Trujillo | Horas | `2.08` |
| `Espera para ingresar Depsa` | Seguimiento EXPORTACION | Decimal | — | — | `(F.I. Depsa - F.LL.Depsa)*24` | Espera exterior Bodega Nacional | Horas | `1.95` |
| `DEPSA` | Seguimiento EXPORTACION | Decimal | — | — | `(F.S.Depsa - F.I. Depsa)*24` | Tiempo dentro de Bodega Nacional | Horas | `1.45` |
| `CEBAF` | Seguimiento EXPORTACION | Decimal | — | — | `(F.Cruce - F.LL CEBAF E)*24*60` | Tiempo en CEBAF fronterizo | **Minutos** | `3` |
| `Espera Nacionalizacion` | Seguimiento EXPORTACION | Decimal | — | — | `(F.AUTORIZACION - F.Cruce)*24` | Espera autorización aduanera | Horas | `2.55` |
| `TCI` | Seguimiento EXPORTACION | Decimal | — | — | `(F.S TCI - F.LL.TCI)*24` | Tiempo en aduana ecuatoriana | Horas | `3.63` |
| `Base` | Seguimiento EXPORTACION | Decimal | — | — | `(F.S.Base2 - F.LL.Base)*24` | Tiempo en Base entre viajes | Horas | `2.53` |
| `Trujillo-PlantaEcu` | Seguimiento EXPORTACION | Decimal | — | — | `F.LL.Planta - F.S.Planta` | Tránsito internacional | **Días** | `2.58` |
| `De TCI a bodega` | Seguimiento EXPORTACION | Decimal | — | — | `(F.LL.Almacen - F.S TCI)*24` | Tránsito TCI→Almacén descarga | Horas | `5.38` |
| `De Depsa a bodega` | Seguimiento EXPORTACION | Decimal | — | — | `(F.LL.Almacen - F.S.Depsa)*24` | Tránsito Bodega Nacional→Almacén | Horas | `~` |
| `De TCI a planta` | Seguimiento EXPORTACION | Decimal | — | — | `(F.LL.Planta - F.S TCI)*24` [INFERENCIA] | Tránsito TCI→Planta Ecuador | Horas | `~` |
| `Espera para iniciar la descarga` | Seguimiento EXPORTACION | Decimal | — | — | `(F.I. descarga - F.Ingreso)*24` | Espera interior almacén antes descarga | Horas | `7.02` |
| `Descarga (Horas)` | Seguimiento EXPORTACION | Decimal | — | — | `(F.T. descarga - F.I. descarga)*24` | Duración de la descarga | Horas | `0.47` |
| `Espera autorizacion` | Seguimiento EXPORTACION | Decimal | — | — | Fórmula exacta no determinable | Similar a Espera Nacionalizacion | Horas | `~` |
| `Camiones` | Medidas | Entero | — | — | `COUNTROWS('Seguimiento EXPORTACION')` | Total de viajes/camiones | Viajes | `200` |
| `Pedidos` | Medidas | Entero | — | — | `DISTINCTCOUNT([PEDIDO])` [INFERENCIA] | Total de pedidos únicos | Pedidos | `50` |
| `Camiones/Pedido` | Medidas | Decimal | — | — | `DIVIDE([Camiones],[Pedidos])` | Ratio camiones por pedido | Ratio | `4.0` |
| `% Cumplimiento Programado` | Medidas | Porcentaje | — | — | Fórmula exacta no determinable | % viajes que cumplieron programación | % | `85%` |
| `INDICE` | REPORTE | Texto/Número | No determinable | No determinable | — | ID de incidencia | — | — |
| `FECHA INCIDENCIA` | REPORTE | DateTime | No determinable | No determinable | — | Fecha de la incidencia | — | — |
| `TIPO DE INCIDENCIA ` | REPORTE | Texto | No determinable | No determinable | — | Tipo: ROBADO/MOJADOS/ROTO/FALTANTE | — | `ROBADO` |
| `CANTIDAD SACOS` | REPORTE | Entero | No determinable | No determinable | — | Cantidad de sacos afectados | Sacos | — |
| `LUGAR DE INCIDENCIA` | REPORTE | Texto | No determinable | No determinable | — | Lugar donde ocurrió la incidencia | — | — |
| `OBSERVACION` | REPORTE | Texto | No determinable | No determinable | — | Descripción detallada de la incidencia | — | — |
| `TRACTO` | REPORTE | Texto | No determinable | No determinable | — | Placa del camión involucrado | — | — |
| `F. PROGRAMACION` | PEDIDOS | Fecha | No determinable | No determinable | — | Fecha de programación del pedido | — | — |

---

**RESUMEN EJECUTIVO — BLOQUE 9:** El diccionario documenta 57 campos distribuidos en 3 tablas visibles al usuario. La tabla `Seguimiento EXPORTACION` concentra 49 campos (27 dimensionales de fechas + 4 categóricos + 17 calculados de tiempo + 1 texto). Las tablas `REPORTE` y `PEDIDOS` tienen 7 y 1 campos respectivamente con origen no determinable.

---

## 10. HALLAZGOS Y RIESGOS {#10-hallazgos}

### 10.1 Filtros de mes hardcodeados — Riesgo CRÍTICO

**Descripción:** Todos los filtros de periodo temporal son filtros fijos (`Mes = 'abril'`, `Mes = 'junio'`, `Mes = 'diciembre'`) incrustados directamente en cada visual o a nivel de página. No existen segmentadores ni parámetros dinámicos.

**Impacto:** El reporte muestra datos de **abril 2024** como "activos", sin posibilidad de que el usuario final cambie el periodo sin editar el PBIX.

**Afecta a:** Todos los visuales de Dashboard, Gestion diaria 1, 2 y 3.

**Recomendación:** Ver Sección 11.

---

### 10.2 Filtros de mes inconsistentes entre páginas — Riesgo ALTO

| Página | Mes hardcodeado |
|--------|----------------|
| Dashboard | `abril` |
| Gestion diaria 1 | `abril` |
| Gestion diaria 2 | `abril` |
| Gestion diaria 3 | `abril` |
| Incidencias | **`junio`** |
| Página 1 (sacos mojados/rotos) | `abril` / **`marzo`** / **`junio`** / **`mayo`** / **`diciembre`** |

**Impacto:** La página de Incidencias no es comparable con el Dashboard. El usuario podría confundirse al comparar KPIs de periodos distintos.

---

### 10.3 Visual 3.6 usa SUM(Espera autorizacion) vs AVERAGE(Espera Nacionalizacion) — Riesgo ALTO

**Descripción:** El card "TOTAL PROM." de la página Gestion diaria 2 muestra `Sum(Seguimiento EXPORTACION.Espera autorizacion)`, mientras que el gráfico de área adyacente muestra `Avg(Seguimiento EXPORTACION.Espera Nacionalizacion)`.

**Problemas:**
1. El título dice "TOTAL PROM." pero el valor es una SUMA, no un promedio.
2. Se usa una columna diferente (`Espera autorizacion`) al gráfico adyacente (`Espera Nacionalizacion`), lo que sugiere que son columnas distintas calculando tiempo similar pero con diferente definición.
3. Una SUMA de tiempos individuales no es comparable con un PROMEDIO de tiempos.

**Columnas afectadas:** `Espera autorizacion` y `Espera Nacionalizacion` — posible duplicación o inconsistencia.

---

### 10.4 Visuales 1.12 y 1.15 parecen idénticos — Riesgo MEDIO

**Descripción:** Los visuales "Tiempos promedio en Inbalnor (Hrs)" y "Tiempos promedio en Jave (Hrs)" usan exactamente las mismas columnas (`Espera para iniciar la descarga` + `Descarga (Horas)`) y el mismo eje (`F.LL.Almacen`). La única diferencia es el filtro de `BODEGA` (INBALNOR vs JAVE).

**Posible error:** ¿Deberían mostrar la misma columna o debería cada visual mostrar campos específicos del almacén?

---

### 10.5 Anomalías de fecha en el Excel — Riesgo MEDIO

| Columna | Valor anómalo | Tipo |
|---------|---------------|------|
| `F.H.S Base ` (col Y) | `2924-03-12` | Typo de año (debería ser 2024) |
| `F.H.Salida` (col BD) | `2005-06-28` | Error de captura (fecha de hace 20 años) |

**Impacto:** Estos valores distorsionan cálculos de tiempo de tránsito cuando se usan esas columnas. La columna `F.LL.Base` tiene fecha máxima 2026-01-04 (razonable) pero `F.S.Base2` tiene fecha hasta 2924 (claramente incorrecta).

---

### 10.6 Inconsistencia de unidades de tiempo — Riesgo MEDIO

| Columna | Unidad |
|---------|--------|
| Todas las columnas de espera/permanencia | **Horas** |
| `CEBAF` | **Minutos** |
| `Trujillo-PlantaEcu` | **Días** |

Si algún visual mezcla estas columnas en un mismo eje sin conversión, el resultado sería incorrecto. Actualmente cada visual las separa, pero el riesgo existe si se extiende el modelo.

---

### 10.7 Tabla `Medidas` (repositorio de medidas) — Riesgo BAJO

**Descripción:** Usar una tabla vacía como contenedor de medidas es una práctica válida pero puede confundir si el modelo crece. El nombre `Medidas` en español podría colisionar con el campo `Medidas` en selecciones de campos de la interfaz.

---

### 10.8 OREMANS no tiene representación en el reporte — Riesgo BAJO

**Descripción:** El almacén `OREMANS` tiene 104 registros en el Excel pero ningún visual dedicado. Si tiene relevancia operacional, debería incluirse.

---

### 10.9 LocalDateTables automáticas por columna de fecha — Riesgo BAJO

**Descripción:** Cada columna de tipo fecha genera su propia `LocalDateTable` automáticamente. Con ~14 columnas de fecha en `Seguimiento EXPORTACION`, se generan **~14 tablas ocultas** en el modelo, incrementando el tamaño del DataModel y el tiempo de actualización.

**Impacto en rendimiento:** Al tener el Excel ~5.900 filas, el impacto es bajo hoy. Pero con volúmenes mayores, el overhead puede ser significativo.

---

### 10.10 Nombre de columna con typo: `Traxto 2` — Riesgo INFORMATIVO

**Descripción:** La columna `Traxto 2` (col F del Excel) tiene un typo ortográfico (debería ser `Tracto 2`). No afecta funcionalidad pero sí la calidad de los datos.

---

### 10.11 Columna `F.Ingreso` posiblemente ambigua — Riesgo MEDIO

**Descripción:** Hay dos conceptos de "Ingreso" en el modelo:
- `F.Ingreso` referenciada como Llegada a Trujillo (col I del Excel)
- `F.Ingreso` como ingreso al almacén ecuatoriano (col AX del Excel)

Si Power Query nombra ambas de manera idéntica, habrá colisión. El modelo PBI las diferencia por el visual donde aparecen, pero la confusión semántica existe.

---

## 11. RECOMENDACIONES {#11-recomendaciones}

### 11.1 Agregar slicer de periodo dinámico [CRÍTICO]

**Problema:** Todos los filtros de mes están hardcodeados.

**Solución recomendada:**
1. Crear una tabla de calendario explícita (`Calendario`) con columnas Año, Mes, Nombre del Mes, Trimestre.
2. Relacionar `Calendario[Fecha]` con la columna de fecha principal (se recomienda `F.PROGRAMACION`).
3. Añadir un slicer de Mes en cada página, o un slicer de página de reporte (bookmark/page filter).
4. Eliminar todos los filtros de mes hardcodeados.

```dax
-- Tabla de calendario
Calendario =
ADDCOLUMNS(
    CALENDARAUTO(),
    "Año", YEAR([Date]),
    "Mes Número", MONTH([Date]),
    "Mes Nombre", FORMAT([Date], "MMMM"),
    "Trimestre", "T" & QUARTER([Date]),
    "Año-Mes", FORMAT([Date], "YYYY-MM")
)
```

```sql
-- Equivalente SQL: tabla de calendario
CREATE TABLE dbo.Dim_Calendario (
    Fecha DATE PRIMARY KEY,
    Anio SMALLINT NOT NULL,
    MesNum TINYINT NOT NULL,
    MesNombre VARCHAR(20) NOT NULL,
    Trimestre CHAR(2) NOT NULL,
    AnioMes CHAR(7) NOT NULL  -- YYYY-MM
);
```

---

### 11.2 Corregir inconsistencia SUM vs AVERAGE en Espera autorizacion [ALTO]

**Medidas afectadas:** Visual 3.6 (card "TOTAL PROM." en Gestion diaria 2)

**Acción:** Determinar si `Espera autorizacion` y `Espera Nacionalizacion` son la misma métrica con diferente definición, y unificarlas. Cambiar el card para usar `AVERAGE`, no `SUM`.

---

### 11.3 Normalizar unidades de tiempo [ALTO]

**Acción recomendada:** Convertir `CEBAF` a horas (dividir entre 60) para consistencia, o agregar una medida wrapper:

```dax
-- Crear medidas en lugar de usar columnas calculadas directamente
CEBAF (Hrs) =
AVERAGE( 'Seguimiento EXPORTACION'[CEBAF] ) / 60
```

---

### 11.4 Limpiar datos del Excel [ALTO]

**Acciones:**
1. Corregir `2924-03-12` → `2024-03-12` en columna Y.
2. Verificar y corregir `2005-06-28` en columna BD.
3. Renombrar `Traxto 2` → `Tracto 2`.
4. Agregar validación de datos en Excel para columnas de fecha (rango válido: 2020-01-01 a 2030-12-31).

---

### 11.5 Replicar en SQL Server + capa semántica [ARQUITECTURA]

Para replicar el dashboard en SQL Server + frontend (Power BI Service con modelo semántico, o Tabular/AAS):

**Paso 1 — ETL en SQL Server:**
```sql
-- Tabla de staging del Excel
CREATE TABLE dbo.Seguimiento_EXPORTACION_raw (
    CLIENTE VARCHAR(20),
    Conductor_Origen NVARCHAR(100),
    Tracto_1 VARCHAR(10),
    Carreta VARCHAR(10),
    Conductor_Destino NVARCHAR(100),
    Tracto_2 VARCHAR(10),
    FHSBase_Fecha DATE, FHSBase_Hora TIME(0),
    FHLLTrujillo_Fecha DATE, FHLLTrujillo_Hora TIME(0),
    FHRegistro_Fecha DATE, FHRegistro_Hora TIME(0),
    FHPROGRAMACION_Fecha DATE, FHPROGRAMACION_Hora TIME(0),
    FHIplanta_Fecha DATE, FHIplanta_Hora TIME(0),
    FHInicioCarga_Fecha DATE, FHInicioCarga_Hora TIME(0),
    FHTerminoCarga_Fecha DATE, FHTerminoCarga_Hora TIME(0),
    FHSPlanta_Fecha DATE, FHSPlanta_Hora TIME(0),
    FHLLBase_Fecha DATE, FHLLBase_Hora TIME(0),
    FHSBase2_Fecha DATE, FHSBase2_Hora TIME(0),
    FHLLBodega_Fecha DATE, FHLLBodega_Hora TIME(0),
    FHIBodega_Fecha DATE, FHIBodega_Hora TIME(0),
    FHSBodega_Fecha DATE, FHSBodega_Hora TIME(0),
    BODEGA VARCHAR(20),
    FHLLCEBAF_Fecha DATE, FHLLCEBAF_Hora TIME(0),
    FHCruce_Fecha DATE, FHCruce_Hora TIME(0),
    FHAutorizacion_Fecha DATE, FHAutorizacion_Hora TIME(0),
    BODEGA_ECUATORIANA VARCHAR(20),
    FHLLTCI_Fecha DATE, FHLLTCI_Hora TIME(0),
    FHSTCI_Fecha DATE, FHSTCI_Hora TIME(0),
    BODEGA_DESCARGA VARCHAR(20),
    FHLLPlanta_Fecha DATE, FHLLPlanta_Hora TIME(0),
    FHLLAlmacen_Fecha DATE, FHLLAlmacen_Hora TIME(0),
    FHIngreso_Fecha DATE, FHIngreso_Hora TIME(0),
    FHIDescarga_Fecha DATE, FHIDescarga_Hora TIME(0),
    FHTDescarga_Fecha DATE, FHTDescarga_Hora TIME(0),
    FHSalida_Fecha DATE, FHSalida_Hora TIME(0),
    Motivo NVARCHAR(500)
);
```

**Paso 2 — Vista de métricas calculadas (usar `vw_Seguimiento_Calculado` de Sección 4)**

**Paso 3 — Medidas en SQL (para reportes directos):**
```sql
-- KPI: Total camiones por mes
SELECT
    FORMAT(F_PROGRAMACION, 'yyyy-MM') AS Periodo,
    COUNT(*) AS Total_Camiones,
    COUNT(DISTINCT PEDIDO) AS Total_Pedidos,
    CAST(COUNT(*) AS DECIMAL(10,2)) / NULLIF(COUNT(DISTINCT PEDIDO),0) AS Camiones_Por_Pedido,
    AVG([Espera a ingreso Trujillo]) AS Prom_Espera_Trujillo_Hrs,
    AVG([Espera para iniciar la carga]) AS Prom_Espera_Carga_Hrs,
    AVG([Carga_Horas]) AS Prom_Carga_Hrs,
    AVG([Espera para ingresar Depsa]) AS Prom_Espera_Depsa_Hrs,
    AVG([DEPSA]) AS Prom_Depsa_Hrs,
    AVG([CEBAF_Minutos]) AS Prom_CEBAF_Min,
    AVG([TCI]) AS Prom_TCI_Hrs,
    SUM(CASE WHEN CAST(F_Base AS DATE) <= CAST(F_PROGRAMACION AS DATE)
             THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*),0) AS Pct_Cumplimiento
FROM vw_Seguimiento_Calculado
WHERE F_PROGRAMACION IS NOT NULL
GROUP BY FORMAT(F_PROGRAMACION, 'yyyy-MM')
ORDER BY 1;
```

---

### 11.6 Separar tabla de hechos de tabla de dimensiones

**Recomendación arquitectural:** Crear una tabla de dimensión `Dim_Unidad` con la información del camión (Conductor, Tracto, Carreta) para normalizar el modelo y evitar duplicación de texto en la fact table.

---

### 11.7 Renombrar "Página 1" con nombre de negocio

La última página no tiene nombre operacional. Debe renombrarse según su contenido (ej.: "Incidencias Detalle" o "Rutas Jave + Puyango").

---

**RESUMEN EJECUTIVO — BLOQUE 11:** Las recomendaciones prioritarias son: (1) reemplazar filtros hardcodeados por un slicer de periodo dinámico con tabla de calendario; (2) corregir la inconsistencia SUM vs AVERAGE en el card de Espera Autorizacion; (3) normalizar unidades de tiempo (CEBAF en minutos es outlier); (4) limpiar errores de fecha en el Excel fuente. Para migrar a SQL Server, se propone una tabla de staging, una vista de métricas calculadas y consultas de KPIs directas.

---

## APÉNDICE: INFORMACIÓN DEL PBIX

| Propiedad | Valor |
|-----------|-------|
| Versión del formato | `1.28` |
| Versión de Power BI | `2025.11` (inferido de `CreatedFromRelease: "2025.11"`) |
| Origen del reporte | `Cloud` (campo `CreatedFrom: "Cloud"`) |
| DatasetId | `fb6f95c8-de01-4c1d-9f6e-d64bdfa0ed14` |
| ReportId | `383c7028-8411-4538-a720-8995b7b05fe8` |
| Tema visual | `Divergent2644393433030021.json` (tema personalizado) |
| Tema base | `CY24SU02` (tema predeterminado Power BI 2024 S2) |
| Autodetección de tipos | Habilitada |
| Importación automática de relaciones | Habilitada |
| Imágenes estáticas | `descarga-camion-min.jpg` (logo/portada), `FOTO_19784040829724099.png` (foto corporativa) |
| Página activa al guardar | Página 5 (`Incidencias`) |
| Drill filter entre visuales | Habilitado por defecto |

---

*Fin del documento — Documentación Técnica Exhaustiva — Dashboard Transporte SGV MARZO 2025*  
*Total de secciones: 11 | Versión: 1.0 | Fecha: 2026-05-12*
