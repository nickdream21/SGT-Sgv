# Perfiles de llegada (hora aproximada de llegada a planta)

El tablero del seguimiento (RegistroSeguimiento) muestra, para cada unidad que va en camino a **cargar a
Trujillo** o a **descargar a Jave / Inbalnor**, "Llega aprox. 23/10 14:30 (13:50–16:10)" y los km que le
faltan, y el botón **Reporte de llegadas** copia el texto para enviar. El cálculo (`Services/Exportacion/LlegadaEstimada.cs`)
no es distancia ÷ velocidad: compara la posición del GPS con lo que tardaron **de verdad** los viajes
anteriores desde ese mismo punto del camino y a la misma hora del día. Así entran solas las paradas de
siempre (descanso en Mórrope, la noche en Pacasmayo, el parqueo de Naranjal). El rango es lo que tardó la
mitad de esos viajes (percentiles 25–75).

Esos viajes salen de `WebSGV/Services/Exportacion/PerfilesLlegada.json` (va **dentro de WebSGV.dll** como
recurso), que arma esta herramienta:

| Ruta | Viajes | Desde → hasta |
|---|---|---|
| `CARGA_TRUJILLO` | tracto 1 | salida de base → llegada a Trujillo |
| `DESCARGA_JAVE`, `DESCARGA_INBALNOR` | tracto 2 | salida de TCI (o el cruce, si no pasó por TCI) → llegada a la planta |
| `RETORNO_GRIFO` | tracto 2 | salida de Jave o Inbalnor → primera parada de 10+ min en el grifo Santa Rosa (sale del GPS; reporte del grifo) |
| `RETORNO_BASE` | tracto 2 | salida de Jave o Inbalnor → llegada a la base (en el tablero, después del grifo) |

Para cada ruta guarda el camino (un punto cada km, tomado del viaje con más puntos del GPS) y, por cada
viaje, una muestra cada 5 km: `[km, hora del día, minutos que faltaban para llegar]`.

## Uso

```powershell
msbuild WebSGV.sln /p:Configuration=Debug /nologo /verbosity:minimal   # necesita WebSGV\bin\WebSGV.dll
.\herramientas\perfil-llegada\Generar-Perfil.ps1 -Desde 2026-08-15 -MaxLlamadas 900
msbuild WebSGV.sln ...   # otra vez: el JSON nuevo entra en la DLL; luego publicar
```

Usa los viajes del seguimiento de `sgvActualizada` (horas de salida y llegada ya detectadas por el GPS) y el
historial de Onway del mismo caché que `herramientas/cuadro-status-general` (`cache\`, por placa y día); lo que
falta lo pide al API (2 llamadas por segundo como máximo, ~2,3 s cada una). Conviene regenerarlo cada mes o
cuando cambie la ruta.

## Límites

- No estima en base, en la cola de Trujillo, en el Complex ni esperando en TCI (depende de la aduana, no del
  camino), ni si la unidad está a más de 8 km del camino.
- Si la posición es vieja (equipo sin señal), la hora sale desde esa posición y el reporte lo dice ("última señal").
- Clientes terceros: Crisar a La Troncal sí (abajo); Crisar a Cartavio, Novopan y Polinplast todavía no.

## Crisar a La Troncal (reporte del grifo Santa Rosa)

Crisar descarga azúcar en La Troncal, más al norte que Jave e Inbalnor y con otro camino, y casi no está en el
seguimiento desde atrás. `Generar-PerfilCrisar.ps1` saca los viajes de los **Word de programación** (secciones de
Crisar: tracto y día; por defecto `docs\DOCUMENTACION\2026`) y en el historial del GPS de cada tracto busca las
estadías en La Troncal y, alrededor de cada una, la salida de TCI, el parqueo de Naranjal, el grifo Santa Rosa y
la base. Agrega a `PerfilesLlegada.json`, **sin tocar las demás rutas**:

| Ruta / dato | Desde → hasta |
|---|---|
| `DESCARGA_LA_TRONCAL` | salida de TCI → llegada a La Troncal (con la noche en el parqueo de Naranjal) |
| `RETORNO_GRIFO_TRONCAL` | salida de La Troncal → primera parada en el grifo Santa Rosa |
| `duraciones["DESCARGA\|LA TRONCAL"]` | minutos dentro de La Troncal (llegada → salida) |

También aprende la **cola de La Troncal** (`ColaLaTroncal`): descargan una tras otra y en orden de llegada
(94 % de los pares del mismo día), así que guarda por turno (1.ª, 2.ª… en llegar ese día) cuánto después de la
primera llegada salió (`TURNO|LA TRONCAL|k`) y el ritmo de salidas de cada día (`RITMO|LA TRONCAL`).

Y **mide el error**: por cada día del historial aprende con los demás días y, a las 06–12 y 15 h de ese día,
estima con lo que se sabía a esa hora (posiciones, llegadas y salidas ya ocurridas) y lo compara con la llegada
real al grifo. Compara "hasta hoy" (sin horas de La Troncal), "solo descarga" y "cola". `--detalle=1` (corriendo
el .exe a mano) lista los casos con más de 3 h de error.

Deja el informe en `crisar-la-troncal.md` (tiempos de cada tramo, la cola, el error y cada viaje). Con los Word del 02/09 al 21/10/2026:
86 descargas; TCI → La Troncal 15,9 h (mediana; pasan la noche en Naranjal 10 h y entran a las 06–12 h),
descarga 6,0 h (4,1–7,1), La Troncal → grifo 4,0 h (3,4–4,5); **86 de 86 paran en el grifo**.

```powershell
msbuild WebSGV.sln ...                                     # necesita WebSGV\bin\WebSGV.dll
.\herramientas\perfil-llegada\Generar-PerfilCrisar.ps1     # -Word <carpeta> -MaxLlamadas 1500
msbuild WebSGV.sln ...                                     # el JSON entra en la DLL; luego publicar
```

Si se regenera `PerfilesLlegada.json` con `Generar-Perfil.ps1`, correr después `Generar-PerfilCrisar.ps1`
(el primero escribe el archivo entero y borraría las rutas de Crisar).
