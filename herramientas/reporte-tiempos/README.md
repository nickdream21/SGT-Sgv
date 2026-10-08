# Reporte de tiempos por tramo (exportación Perú / Ecuador)

Herramienta **independiente de la web SGV**. Lee el Excel de seguimiento de exportación
(`WebSGV/dashboard/STATUS GENERAL VIVIANA ...xlsx`, hoja `Seguimiento EXPORTACION`) y
produce dos libros:

| Archivo | Para qué sirve |
|---|---|
| `Tiempos por tramo - Exportacion 2026.xlsx` | El reporte: dashboard, tramos punto por punto, ciclos, serie mensual, comparativo por destino, tabla dinámica con segmentadores, datos crudos y metodología. |
| `Importar al sistema - Seguimiento 2026.xlsx` | Los mismos viajes en el formato que espera el importador de `Views/Exportacion/RegistroSeguimiento.aspx`, pestaña **Importar Excel**, para que el dashboard del sistema muestre 2026. |

No se compila ni se despliega con `WebSGV`: son scripts sueltos.

## Cómo se usa

```powershell
cd herramientas\reporte-tiempos
.\Generar.ps1
```

Requiere **Node** (lee el `.xlsx` y hace el cálculo) y **Excel instalado** (arma los libros
por COM). No toca la base de datos.

Para apuntar a otro archivo de origen o a otra carpeta de salida:

```powershell
.\Generar.ps1 -Excel "C:\ruta\STATUS GENERAL.xlsx" -Salida "C:\ruta\salida"
```

## Qué hace cada pieza

| Archivo | Rol |
|---|---|
| `lib.js` | Lector mínimo de `.xlsx` descomprimido: shared strings, celdas y series de fecha. |
| `modelo.js` | El cálculo: reconstruye la llegada a base, mide los 18 tramos y los 6 ciclos, estima el retorno y arma la serie mensual y por destino. |
| `export.js` | Convierte el modelo en las tablas que van a cada hoja del libro. |
| `importable.js` | Convierte el modelo al layout de 34 columnas del importador del sistema. |
| `construir.ps1` | Arma el libro del reporte con Excel COM: gráficos, formato condicional, tabla dinámica y segmentadores. |
| `importable.ps1` | Arma el libro de importación con fechas reales. |

## Decisiones que conviene conocer antes de leer los números

Están todas escritas en la hoja **Metodología** del libro generado. Las dos que más pesan:

- **La llegada a base después de Trujillo venía con la fecha mal.** La hora es correcta,
  la fecha no: sobre los 648 viajes que permiten contrastarla, 318 quedaban antes de salir
  de planta y 105 después de salir de base. Se reconstruye anclando la hora al primer
  instante posterior a la salida de planta. Con esa regla solo 1 viaje de 648 sigue siendo
  incoherente.
- **El retorno Guayaquil → base no existe en la fuente.** La columna está vacía en 1,723 de
  1,726 viajes. Se estima en 0.94 días por tres caminos independientes que coinciden: los 3
  viajes que sí lo tienen (0.84), la rotación de flota en su percentil 10 (1.06) y la suma
  de los tránsitos en vacío (0.92). Todo indicador que lo use aparece marcado como
  **ESTIMADO**.

## Importar al sistema

El importador hace *upsert* por `cliente + fhProgramacion + tracto1`, así que volver a subir
el mismo archivo actualiza en vez de duplicar. Conviene probar primero contra la base de
pruebas (`sgvActualizada`) antes de correrlo sobre producción.
