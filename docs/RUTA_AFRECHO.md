# Rutas aprendidas del GPS: afrecho (Paita → Huaquillas) e importación (Jave → Trujillo)

Análisis del 25–27/10/2026 con el historial de Onway (`herramientas/reporte-gps/Paradas-Placa.ps1`: paradas de
30+ min con el lugar conocido o la dirección de Onway). Lo que ya está en el código está en la sección 5.

## 1. Afrecho: viajes conocidos

| Fecha carga | Tracto(s) | Fuente | El Excel / Word dice |
|---|---|---|---|
| 06/05/2025 | T7J-860 | STATUS EXTRA | GYQ |
| 06/05/2025 | AMQ-859 → relevo CBX-832 en La Brea (Talara) | STATUS EXTRA | GYQ |
| 10/01/2026 | BPT-919 | STATUS EXTRA | TCI |
| 10/01/2026 | BFH-852 | STATUS EXTRA | TCI |
| 24/10/2026 | AVM-877 / T4G-975 | Word 23/10 `AFRECHO-PAITA` (06:00) | Word 25/10 `AFRECHO-HUAQUILLAS` |
| 24/10/2026 | F6O-779 / T7A-996 | despacho 1923 | Word 25/10 `AFRECHO-HUAQUILLAS` |

**F6O-779 no tiene equipo en Onway** (151 equipos, ninguno con esa placa). El aproximado con su grupo pide 2
compañeras con señal y el afrecho solo tiene a AVM-877: queda sin posición (decisión pendiente en el plan).

## 2. Afrecho: el recorrido (hora Perú)

| Etapa | Lugar | Duración | Notas |
|---|---|---|---|
| Carga | Alicorp Paita, Av. Industrial (-5.0861, -81.0944) | 7,5–9,4 h | llegan 06:50–10:00, salen 14:45–18:00 |
| Espera con la carga | Base Sullana | 34–36 h | ene-2026 y oct-2026; may-2025 fue de frente por la costa (relevo en Talara) |
| Aduana Perú | Zarumilla (-3.5003, -80.2782) | 0,6–2,8 h | antes del Complex |
| **Descarga** | **Huaquillas (-3.5010, -80.2042)**, 0,8 km al este del cruce | **3,8–4,6 h** | los 4 viajes anteriores; no pasan por TCI |
| Descanso / combustible | Bellavista, Santa Rosa (-3.5262, -79.9953) o grifo Santa Rosa en El Retiro (-3.4286, -79.9628) | 1,7–2,9 h | vacíos; Bellavista es el estacionamiento de la Troncal de la Costa (ASU-862 durmió ahí 6 h bajando con Vitapro el 22/10) |
| Retorno | CEBAF Aguas Verdes | 0,5–1,5 h | la misma noche |
| Fin | Base Sullana | — | al día siguiente ~11:00–14:30 |

Primera lectura corregida: El Retiro es el **grifo Santa Rosa** (geocerca `GRIFO_SANTA_ROSA`) y Bellavista un
estacionamiento; ninguna de las dos es la descarga. La descarga es la parada de ~4 h en Huaquillas, que coincide
con el destino del Word.

### Viaje de octubre (AVM-877), al 27/10 10:00

Paita 24/10 06:52–14:45 → base Sullana 16:32 (GPS apagado de 24/10 19:01 a 26/10 ~04:30) → sale 26/10 04:48 por la
costa (Máncora) → Zarumilla 12:54 → cruce 13:47 → **TCI desde las 14:41** (este viaje sí pasa por TCI: otro
comprador, de Balsas). Sigue en TCI.

El sitio publicado le había puesto la salida de TCI y la descarga "en AGUAS VERDES" a las 07:42 del 27/10: al
reconectar la batería (cortada 26/10 16:18) el equipo mandó 2 posiciones viejas (Aguas Verdes 07:42, cruce 07:44) y
a las 07:50 volvió a TCI. Corregido (sección 5) y recalculado en sgvActualizada.

## 3. Importación: IMPORTACION-TRUJILLO (Word del 25/10)

ASU-748 / T0L-975 y ASU-862 / T4C-976, 06:00: las mismas unidades que cargan Vitapro en Trujillo el 26/10.

| Hora | Dónde | ASU-748 | ASU-862 |
|---|---|---|---|
| 22/10 | **Jave (Vitapro, Durán)**: descargan la exportación y cargan la importación | 10:56–20:27 (3 paradas, 9,5 h) | 10:59–20:29 |
| 22–23/10 | Parqueo antes de La Troncal (noche) | 23:02–07:04 | 23:00–07:04 |
| 23/10 | Grifo Santa Rosa (El Retiro) | 09:43–11:50 | 09:40–11:48 |
| 23/10 | TCI (aduana Ecuador) | 12:40–17:32 (4,9 h) | 12:36–17:32 |
| 23/10 | CEBAF Aguas Verdes | 17:45–18:52 | 17:45–18:51 |
| 24–25/10 | base Sullana (ASU-748 también 23 h en Pimentel, Chiclayo) | 7,6 h | 38 h |
| 26/10 | **Trujillo, Av. Gonzales Prada (acceso Vitapro): descarga de la importación** | 06:17–13:28 | 08:21–13:46 |
| 26/10 | Planta Vitapro Trujillo: carga de exportación | 13:48–18:17 | 13:46–18:29 |

- Es el "retorno Ecuador → Perú" del plan, ahora con Vitapro: el camión baja con la exportación, carga la
  importación en la misma visita a Jave y la descarga en Trujillo justo antes de la carga siguiente.
- Hitos que tendría la ruta: carga en Jave (la misma visita de la descarga de la exportación), TCI, CEBAF, base,
  llegada / salida de la descarga en Trujillo. Falta decidir con la oficina si es un viaje aparte o el tramo de
  vuelta del viaje de exportación (sección 4 del plan).
- Mientras tanto "Programar con el Word" ya no la ofrece como sección de carga (se creaba como una exportación).

## 4. Unidades "sin actualizar": el GPS está apagado, no es el cron-job

El 25/10 a las 10:00 cron-job.org sí llamaba (última corrida 09:50, 32 viajes consultados). En Onway, 25 de los
33 tractos activos no reportaban hacía más de 2 h: 15 estacionados en TCI (hasta 34 h), 5 en la base Sullana, 2 en
Lambayeque, BPT-788 en Trujillo y BFR-730 (sin señal desde marzo). El historial de AVM-877 lo explica: llega a la
base, "Fuente principal desconectada" (cortan la batería) y el equipo sigue con su batería interna hasta que se
agota ("Dispositivo apagado o fuera de cobertura").

## 5. En el código (27/10/2026)

- `TrazabilidadCargaService.SinPuntosViejosAlReconectar`: tras un silencio de 30+ min, hasta 3 posiciones a más de
  1 km que en menos de 1 h vuelven a menos de 300 m de donde estaba la unidad se descartan (todas las rutas).
- Afrecho: llegada / salida de la base Sullana si llega en las 6 h siguientes a la carga; descarga en la geocerca
  `AFRECHO_HUAQUILLAS` o, si no paró ahí, la primera parada larga después de TCI, **nunca en el lado peruano**.
  Fase "En base, cargado" y STATUS "UNIDAD CARGADA EN BASE SULLANA".
- Word: `AFRECHO-HUAQUILLAS` es la descarga ("Registrar entregas"), no una carga; `IMPORTACION-...` no es sección
  de carga.
- Polinplast: 2.ª geocerca de Jayanca en el pueblo (`POLINPLAST_JAYANCA_2`, -6.2802, -79.9053): BPT-788 descargó
  ahí el 24/10 08:23–13:02 y el tablero seguía con "Descarga 0/3".
- Tablero: "GPS sin reportar desde dd/MM HH:mm (n h): apagado o sin cobertura" en vez de "Ubicación al", y junto a
  la última consulta cuántos viajes tienen el GPS sin reportar más de 2 h.
