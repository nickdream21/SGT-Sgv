# Cuadro STATUS GENERAL desde los Word y el GPS

Arma el cuadro "Seguimiento EXPORTACION" de un mes (las mismas 62 columnas del Excel STATUS
GENERAL de la administradora) sin llenarlo a mano:

- **Viajes**: de las programaciones en Word (`DESPACHO dd-mm-aa.docx`). Cada carga de
  "TRUJILLO-VITAPRO-CARGAN dd" se une con su descarga de "GUAYAQUIL-VITAPRO" (y las secciones sin
  título que siguen, con su pedido) **por la carreta**; la columna CAMBIO dice qué chofer trajo la
  carreta y decide cuando la carreta aparece en dos cargas seguidas.
- **Horas**: del GPS de Onway con la misma lógica que "Actualizar GPS" del seguimiento
  (`TrazabilidadCargaService`, `TrazabilidadDescargaService`). La búsqueda de la carga empieza el día
  del despacho (el tracto sale de base la tarde anterior) y, si el tracto también cargó el día antes,
  se salta esa carga.
- **En gris** quedan las columnas que el GPS no sabe (registro, inicio/término de carga y descarga,
  autorización de nacionalización).
- Hoja **Detalle GPS**: cada hora con segundos, la regla que la detectó, coordenadas y enlace al mapa.

## Uso

```powershell
msbuild WebSGV.sln /p:Configuration=Debug /nologo /verbosity:minimal   # necesita WebSGV\bin\WebSGV.dll
cd herramientas\cuadro-status-general
.\Generar-Cuadro.ps1 -Mes 2026-10 -Carpetas ..\..\docs\DOCUMENTACION\2026\2026\SETIEMBRE, ..\..\docs\DOCUMENTACION\2026\2026\OCTUBRE
```

Incluir la carpeta del mes anterior: el Word del último día ya trae cargas del mes. La primera vez
consulta ~950 días de historial (unos 25 min por el límite de 2 req/s del API); queda en `cache\` por
placa y día, y la siguiente corrida tarda segundos. El Excel sale en `salida\`.

## Cargar los viajes en el sistema

```powershell
.\Generar-Cuadro.ps1 -Mes 2026-10 -Carpetas ... -Bd prueba    # crea y deshace: muestra cuántos
.\Generar-Cuadro.ps1 -Mes 2026-10 -Carpetas ... -Bd aplicar   # los deja
```

Solo escribe en **sgvActualizada** (se niega con otra BD). Salta los viajes que ya están (misma
placa de carga, carreta y día) y los que no tienen pedido (sin descarga en los Word todavía). Cada
viaje se crea como MANUAL con `sp_SE_GuardarDatosViaje` y sus horas con `sp_SE_RegistrarHitoGps`
(origen, coordenadas y regla, como "Actualizar GPS"); todo en una transacción.

## Archivos

| Archivo | Rol |
|---|---|
| `Cuadro.cs` | Lectura de los Word, unión carga-descarga, GPS, Excel y carga en la BD |
| `Generar-Cuadro.ps1` | Compila `Cuadro.cs` contra `WebSGV\bin` y lo corre |
| `bin\`, `cache\`, `salida\` | Compilado, historial del GPS y Excel (gitignored) |

La cadena de conexión y las credenciales de Onway se leen de `WebSGV\connectionStrings.config` y
`WebSGV\appSettings.Secrets.config` en su sitio: no se copian.
