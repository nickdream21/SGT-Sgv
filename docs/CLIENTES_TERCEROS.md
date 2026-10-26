# Clientes terceros: dónde cargan, dónde descargan y cómo encajan con Vitapro

Análisis del 2026-10-21 sobre `docs/DOCUMENTACION/STATUS EXTRA VIVIANA.xlsx` (602 viajes, 25/04/2025 – 19/10/2026;
columnas FECHA CARGA, CONDUCTOR/TRACTO DE CARGA y DE DESCARGA, SEMIREMOLQUE, CLIENTE, LUGAR DE CARGA, LUGAR DE
ENTREGA, STATUS), cruzado con los viajes de Vitapro de `SeguimientoExportacion` (julio–octubre 2026, 706 viajes),
con los 222 Word de programación de enero a octubre de 2026 (sección 2) y con las notas de la administradora.

## 1. Los clientes

| Cliente | Viajes (total / ago–oct 2026) | Carga | Descarga | Producto | Documentos | Relevo en base |
|---|---|---|---|---|---|---|
| **CRISAR** (operador logístico que nos contrata) | 110 / 95 | Cartavio (La Libertad, ingenio) | Guayaquil (bodegas: La Troncal, etc.) | Azúcar | El chofer envía la factura de lo que carga | 47 % (52 de 110) |
| **NOVOPAN** | 251 / 31 | Quito (Ecuador) | Piura (118) o Trujillo (130) | Melamina, tableros de madera aglomerada | Primero se carga; luego llegan por correo factura y packing list (serviciosviviana71) | 26 % (66 de 251) |
| **POLINPLAST** (nacional) | 144 / 55 | Chapaira (Piura) | Olmos 31, Virú 30, ACP 28, Casa Grande 25, Chao 11, Jayanca 8, Chepén 7, Salaverry 1 | Cartón | Guía de remisión remitente (digital, del cliente) + nuestra guía transportista; luego la factura del flete | casi nunca (2 de 144) |
| Otros esporádicos | 41 | Paita, Manta, Durán, Quevedo, Tumbes, Chiclayo | GYQ, TCI, Trujillo, Lambayeque | Seafrost, Molinera, Afrecho, Chicoma, Añejadoras, Polvillo | — | — |

Por mes (Excel): Novopan fue el grande en ago–oct 2025 (23, 34, 58); desde marzo 2026 entra Polinplast
(10–35 al mes) y desde mayo 2026 Crisar (15 en mayo, **60 en septiembre**, 35 hasta el 19/10). Vitapro en el
mismo periodo: 194 (jul), 211 (ago), 154 (sep), 147 (oct).

Ritmo de carga: Crisar sale en grupos de **5 unidades por día** (22 días con carga); Novopan en grupos de 2–8
(3,9 por día); Polinplast 1–4 casi a diario (84 días con carga).

## 2. Cómo aparecen en el Word de programación

Cruce con los 222 Word de `docs/DOCUMENTACION/Despachos Ene-Set 2026` (01/01 – 17/09/2026) más los de octubre que
hay en esta PC (09, 11, 18–21/10). Cada viaje de tercero del Excel se buscó en el Word por tracto o carreta:

| Cliente | Viajes del Excel con Word | Encontrados en el Word | Cómo los programa el Word |
|---|---|---|---|
| Crisar | 60 | **60 (100 %)** | Word del día D, sección `CARTAVIO-AZUCAR` (o `AZUCAR-CARTAVIO`, a veces con "-CARGAN dd DE mes"), HORARIO 06:00 (62) u 08:00: cargan el día D+1. La descarga sale después en `GUAYAQUIL-AZUCAR` con CAMBIO (solo en mayo–junio y septiembre–octubre: 38 de 77). En la mitad (19) CAMBIO es el chofer que cargó (relevo en base); en la otra mitad descarga el mismo tracto. |
| Novopan | 77 | **72 (94 %)**; faltan 4 del 02/01 y 1 desde Tumbes | Word del día D, sección `NOVOPAN-QUITO` **sin hora**: cargan en Quito el D o el D+1. De 2 a 5 días después, otro Word la lleva en `NOVOPAN-PIURA` o `NOVOPAN-TRUJILLO` **con hora** (07:00): es la descarga programada. 70 de 71 coinciden con el lugar de entrega del Excel. El mismo tracto descarga en 61 de 74; en 13 hay relevo. |
| Polinplast | 126 | **126 (100 %)** | Word del día D, sección `POLINPLAST-CHAPAIRA` (o `POLINPLAST-RAPEL`, que también carga en Chapaira: el GPS muestra que Rapel es el destino, en Jayanca) con HORARIO 06:00 (131): cargan el D+1. Al día siguiente (104) o a los dos días (22), el Word repite el tracto en `POLINPLAST-<destino>` (VIRU, ACP, OLMOS PROYECTO, CASAGRANDE, JAYANCA, CHEPEN, CHAO, TALSA) con la hora de entrega (08:00): **la sección con el destino es la descarga**. |

Así, **el Word ya programa a todos los terceros**, con la misma regla que Vitapro: la carga del día siguiente
lleva HORARIO y la descarga se une por la carreta, con CAMBIO cuando hay relevo. El Excel de la
administradora es el resultado de esas programaciones.

Diferencias entre las dos fuentes: el Excel no tiene las 5 cargas de Crisar de junio ni las 4 de Novopan de
febrero que sí están en el Word (el Excel no tiene ningún viaje de febrero). El Word de septiembre llega solo
hasta el 17/09.

## 3. El circuito: un cliente lleva al siguiente

Siguiendo cada tracto día a día (Excel + Vitapro), los terceros no son viajes sueltos: **llenan los tramos que
un tracto haría vacío** alrededor de Vitapro. Transiciones del mismo tracto (siguiente viaje en ≤ 20 días,
desde julio 2026):

| Viaje | Siguiente viaje del mismo tracto | Veces | Días (mediana) |
|---|---|---|---|
| Polinplast (Chapaira → La Libertad) | carga Vitapro en Trujillo | 55 | 2 |
| carga Vitapro | Polinplast | 51 | 2 |
| Crisar (Cartavio) | carga Vitapro | 33 | 4 |
| carga Vitapro | Crisar | 23 | 4 |
| Novopan (Quito → Piura/Trujillo) | carga Vitapro | 21 + 9 | 5 / 1 |
| descarga Vitapro en Ecuador | Crisar o Novopan | 20 + 18 + 16 | 4–5 |

Hay dos tipos de tracto, igual que en Vitapro:

- **Tractos de Perú** (base Sullana ↔ Trujillo; p. ej. TBM-898, BPT-788, T6F-864, BNG-758). Bajan desde la base
  con **cartón de Polinplast** cargado en Chapaira y lo entregan en Casa Grande, Chao o Virú, al lado de Trujillo.
  Ahí cargan **Vitapro en Trujillo** o **azúcar de Crisar en Cartavio** y suben a la base.
- **Tractos de Ecuador** (base ↔ Guayaquil; p. ej. BFI-922, CBU-930). En la base reciben la carreta (relevo) y
  llevan a Guayaquil **Vitapro o el azúcar de Crisar**. De vuelta, en vez de regresar vacíos, suben a Quito y traen
  **Novopan** a Piura o a Trujillo. Desde Trujillo cargan Vitapro al día siguiente (mediana: 1 día).

```
        Chapaira (Piura) ──Polinplast──► Casa Grande / Chao / Virú
              ▲                                   │
         base Sullana ◄──Vitapro (Trujillo)───────┤
              │      ◄──Crisar (Cartavio)─────────┘
              │ relevo (CAMBIO)
              ▼
         Guayaquil ──vacío──► Quito ──Novopan──► Piura / Trujillo
```

**Crisar es la sección "GUAYAQUIL-AZÚCAR" del Word de programación.** Las 7 filas del Word del 21/10 tienen en
CAMBIO a los choferes que cargaron azúcar en Cartavio el 19/10 según el Excel (Muñoz CBV-761, Rengifo BNI-736,
Ipanaque CBV-788, Manco BSC-900, Segundo Saavedra AMQ-859, Julio Imán ASU-748, Wiler Cruz ARN-917). Es el
mismo relevo por carreta que Vitapro, y cuatro de esos choferes cargaron Vitapro en Trujillo el 21/10
(pedido 4400133952).

## 4. Qué significa para el sistema

Como el Word ya trae a los tres clientes con la misma estructura que Vitapro, el camino más corto es que
**"Programar con el Word" (RegistroDespacho) reconozca también sus secciones**. Hoy `LectorProgramacionWord`
lee todas las tablas, pero solo se programan las de Vitapro, y GUAYAQUIL-AZÚCAR se ignora.

| Cliente | Parecido con lo que ya existe | Qué falta |
|---|---|---|
| Crisar | Igual que la exportación de Vitapro: carga cerca de Trujillo, relevo en base por la carreta (CAMBIO) y descarga en Guayaquil. | Programar `CARTAVIO-AZUCAR` (carga) y `GUAYAQUIL-AZUCAR` (descarga, CAMBIO); geocerca de Cartavio y bodegas de azúcar en Ecuador; documento: la factura que manda el chofer (foto o PDF), sin ZIP. |
| Novopan | Importación al revés: carga en Ecuador, cruce de frontera de norte a sur y descarga en Perú. | Programar `NOVOPAN-QUITO` (carga, sin hora) y `NOVOPAN-PIURA/TRUJILLO` (descarga con hora, unida por la carreta); hitos de importación (planta Quito, CEBAF/Huaquillas, Piura/Trujillo); la factura y el packing list llegan **después** de cargar, igual que el despacho programado de Vitapro que se completa con el correo. |
| Polinplast | Despacho nacional simple: un tracto, sin relevo. | Programar `POLINPLAST-CHAPAIRA/RAPEL` (carga) y `POLINPLAST-<destino>` (descarga, el mismo tracto al día siguiente); geocercas de Chapaira, Rapel y los destinos; guía remitente digital del cliente más nuestra guía transportista. |

El GPS podría proponer el siguiente viaje de cada tracto. Por ejemplo, un tracto que descargó cartón en Casa
Grande casi siempre carga Vitapro o Crisar a los 2–4 días.

### 4.1 Hecho (2026-10-21): programar con el Word

"Programar con el Word" (RegistroDespacho) reconoce las secciones de carga de los tres clientes
(`RutaProgramacion`, probado con todos los títulos de los 222 Word). Cuando el Word trae varias, un selector
"Sección del Word" permite programar una y luego la siguiente sin volver a subirlo. Si el título no dice el día,
se propone el día siguiente al del Word, y Novopan se programa como carga internacional (Quito). Para que cliente
y planta se completen solos hay que registrar antes en el catálogo los clientes **CRISAR** y **POLINPLAST**
(Clientes) y las plantas **Cartavio** y **Chapaira** como nacionales (Plantas); Quito ya existe como internacional.
POLINPLAST-RAPEL se propone como carga en Chapaira.

### 4.2 Hecho (2026-10-21): control y STATUS de los terceros (el STATUS EXTRA que se envía)

**Cómo se usa:**

1. Registrar una vez los clientes CRISAR y POLINPLAST (Novopan ya está) y las plantas Cartavio y Chapaira.
2. Subir el Word del día en RegistroDespacho. Cada sección de carga de terceros se programa como las de Vitapro
   y crea su viaje en el seguimiento. Las secciones de descarga (GUAYAQUIL-AZUCAR, NOVOPAN-PIURA,
   POLINPLAST-VIRU…) salen en **"Entregas de clientes terceros en este Word"**: "Registrar entregas" pone el lugar
   de entrega y el relevo (tracto y chofer de descarga) en el viaje de la carga con la misma carreta.
3. En el seguimiento, filtrar **Cliente: Terceros** (o uno). "Actualizar GPS" marca las horas y el STATUS sale
   solo. **"Excel STATUS EXTRA"** baja el cuadro con las columnas del Excel de la administradora.

Si el viaje no tiene lugar de entrega, el GPS lo deduce del primer destino del cliente donde la unidad se detuvo.
Probado con el GPS real de tres viajes del Excel, con las mismas horas y el mismo STATUS final:

- Polinplast BNG-758: Casa Grande y luego la cochera.
- Crisar BNG-758 → CBX-832: con relevo y una noche en TCI.
- Novopan BFK-879: Quito → Trujillo.

Lo que sigue es la propuesta tal como se aprobó:

El Excel `STATUS EXTRA VIVIANA` es al de terceros lo que el STATUS GENERAL es a Vitapro: una fila por unidad con
FECHA CARGA, CONDUCTOR DE CARGA / DE DESCARGA, PLACA TRACTOR CARGA / DESCARGA, PLACA SEMIREMOLQUE, CLIENTE, LUGAR DE
CARGA, LUGAR DE ENTREGA y STATUS. Sus STATUS usan las mismas frases que `StatusUnidad` ya genera para Vitapro
("UNIDAD DESCARGADA EN SU TOTALIDAD", "UNIDAD CARGADA CAMINO A BASE - PAIJAN", "UNIDAD EN COMPLEX", "UNIDAD EN BODEGA
PARA DESCARGA - LA TRONCAL", "UNIDAD DESCARGADA RETORNO A BASE - ARENILLAS").

Propuesta, sobre lo que ya funciona para Vitapro:

1. **Viaje del seguimiento por cada carga programada de un tercero.** `sp_SE_SincronizarDespachos` hoy crea
   viajes solo de las cargas de exportación; se extiende a las cargas de Crisar, Novopan y Polinplast, con una
   columna nueva `tipoRuta` (VITAPRO, CRISAR, NOVOPAN, POLINPLAST) más `lugarCarga` y `lugarEntrega` (migración
   nueva). La columna `cliente` sigue siendo el N° de pedido, que los terceros no tienen.
2. **La sección de descarga del Word completa el viaje** por la carreta, como el relevo de Vitapro:
   GUAYAQUIL-AZUCAR pone el tracto y el chofer de descarga (CAMBIO), NOVOPAN-PIURA/TRUJILLO y POLINPLAST-<destino>
   ponen el lugar de entrega y la hora programada.
3. **Hitos por tipo de ruta**, que son subconjuntos de las columnas que ya existen:
   - Crisar: los de Vitapro, con la planta de carga en Cartavio y la descarga en La Troncal. El GPS lo confirma: la
     ida hace **el mismo tramo que la descarga de Vitapro** (Base Sullana → Complex → TCI → Arenillas), luego el
     parqueo antes de La Troncal y la descarga; la vuelta pasa por el CEBAF. Así que el tramo de frontera se sigue
     con `TrazabilidadDescargaService` tal como está, cambiando solo el destino final (La Troncal en vez de Jave o
     Inbalnor). En TCI a veces la unidad se queda a dormir y sigue al día siguiente (hasta ~22 h en la muestra):
     eso ya lo marca el aviso de estadía larga en TCI.
   - Novopan, de Ecuador a Perú: la ida vacía hace el mismo tramo (Complex → TCI); luego llegada y salida de planta
     en Quito y, de vuelta cargada, Aguas Verdes (-3.4896, -80.212), TCI, CEBAF, llegada a base, llegada y salida de
     la descarga en Piura o Trujillo.
   - Polinplast, nacional: salida de base, carga en Chapaira o Rapel, llegada y salida del destino, retorno.
4. **El GPS los detecta con las geocercas de la sección 4.3** y el mismo "Actualizar GPS" del tablero.
5. **STATUS por tipo de ruta** con el mismo motor (`StatusUnidad`), cambiando los nombres de lugar.
6. **Tablero y Excel.** En el tablero del seguimiento, un filtro "Cliente" (Vitapro / terceros) y un botón
   **"Excel STATUS EXTRA"** con exactamente las columnas del Excel de la administradora.

### 4.3 Geocercas desde el GPS (hecho 2026-10-21)

Historial de Onway de 39 viajes de la muestra (mayo–octubre 2026, unos 180 días-tracto): se tomaron las paradas
de 20 min o más a 3 km/h o menos y se agruparon a 500 m entre los viajes al mismo destino. Quedan en
`Services/GpsIntegracion/GeocercasTerceros.cs` (pruebas con las paradas reales en `GeocercasTercerosTests`), como
círculos. Antes de usarlas para marcar horas hay que dibujarlas sobre el terreno, como las de Vitapro.

| Geocerca | Centro | Radio | Viajes con parada / muestra | Parada típica |
|---|---|---|---|---|
| Cartavio (carga azúcar, Crisar) | [-7.88922, -79.21766](https://www.google.com/maps?q=-7.88922,-79.21766) | 500 m | 6/6 | 2 h; a veces esperan la noche (15 h) |
| Chapaira (carga cartón, Polinplast) | [-5.03606, -80.58935](https://www.google.com/maps?q=-5.03606,-80.58935) | 250 m | todos, también los de "RAPEL" | 2–5 h |
| Novopan Quito (Itulcachi) | [-0.27143, -78.33215](https://www.google.com/maps?q=-0.27143,-78.33215) | 400 m | 6/6 | ~2 h |
| La Troncal (descarga azúcar, Crisar) | [-2.44208, -79.35272](https://www.google.com/maps?q=-2.44208,-79.35272) | 400 m | 5/5 | 2–5 h en varias paradas seguidas |
| Parqueo antes de La Troncal | [-2.77225, -79.69339](https://www.google.com/maps?q=-2.77225,-79.69339) | 200 m | 5/5 | pasan la noche (6–13 h, hasta 56 h) |
| Novopan Piura | [-5.11323, -80.70448](https://www.google.com/maps?q=-5.11323,-80.70448) | 300 m | 3/3 | ~1,5 h |
| Novopan Trujillo | [-8.14396, -79.01987](https://www.google.com/maps?q=-8.14396,-79.01987) | 250 m | 2/3 | 2–4 h (a 1 km de Vitapro) |
| Cochera Trujillo (estacionamiento, calle Fundo Larrea) | [-8.13855, -79.01755](https://www.google.com/maps?q=-8.13855,-79.01755) | 150 m | 5 estadías de 3 tractos | 1–4 días esperando la carga siguiente |
| Grifo Curva de Sun (espera en Trujillo) | [-8.14790, -79.01282](https://www.google.com/maps?q=-8.14790,-79.01282) | 150 m | 2 tractos + Novopan después de descargar | 6 h a 4 días |
| Virú | [-8.40868, -78.81249](https://www.google.com/maps?q=-8.40868,-78.81249) | 300 m | 3/3 | ~4 h (esperan 1,5 km al norte) |
| Chao | [-8.58301, -78.64513](https://www.google.com/maps?q=-8.58301,-78.64513) | 300 m | 3/3 | ~1,5 h |
| Casa Grande | [-7.70956, -79.26271](https://www.google.com/maps?q=-7.70956,-79.26271) | 400 m | 3/3 | ~1 h |
| ACP y Chepén (mismo punto) | [-7.06923, -79.55870](https://www.google.com/maps?q=-7.06923,-79.55870) | 300 m | 3/3 de ACP y 3/3 de Chepén | 2–3 h |
| Jayanca, también "RAPEL" | [-6.37473, -79.80202](https://www.google.com/maps?q=-6.37473,-79.80202) | 250 m | 1 de Jayanca y 2 de Rapel | 3–6 h |
| Olmos (proyecto) | [-6.31556, -80.24402](https://www.google.com/maps?q=-6.31556,-80.24402) | 200 m | 3/3, en el mismo punto (30 m) | de noche |
| Salaverry | [-8.20400, -78.98486](https://www.google.com/maps?q=-8.20400,-78.98486) | 300 m | 1/1 | ~4 h (por confirmar) |

Lo que enseñó el GPS:

- **Crisar descarga en La Troncal**, no en la ciudad de Guayaquil. Pasan la noche en el parqueo de -2.772 y entran
  temprano. La parada larga de la ida (~22 h en -3.536, -80.184) es **TCI**, el mismo punto de la descarga de
  Vitapro (geocerca `TCI` ya existente): a veces la unidad duerme ahí y sigue al día siguiente. Las de Novopan
  también pasan por TCI a la ida y a la vuelta.
- **Rapel no es otra planta de carga**: las secciones POLINPLAST-RAPEL cargan en Chapaira como las demás, y los
  viajes van al punto de Jayanca. Rapel es a quién va el cartón.
- **ACP y Chepén son el mismo punto de descarga.**
- Paradas que se repiten en casi todos los viajes y no son ni carga ni descarga (grifos o descanso): -6.536,
  -80.026 (al norte de Chiclayo), -7.414, -79.551 (Pacasmayo), -7.932, -79.101 (Paiján), -6.063, -80.509 y
  -5.272, -80.612. Las de Novopan de vuelta: -3.526, -79.995 (Arenillas, de noche) y -3.4896, -80.212 (Aguas
  Verdes, hasta 44 h: ¿aduana de importación?).
- **"UNIDAD DESCARGADA EN COCHERA TRUJILLO"** (5 viajes de Polinplast, carga 25–27/08). El GPS muestra que las 5
  unidades **descargaron primero en su destino** (Casa Grande 1,5–3 h; BPT-788 en ACP), después fueron a Trujillo y
  se quedaron de 1 a 4 días, y el Word del 01/09 las programa a las cinco en `CARTAVIO-AZUCAR-CARGAN 02 DE
  SEPTIEMBRE`: **esperaban en Trujillo la carga siguiente** (azúcar de Crisar en Cartavio, a ~30 km), en vez de
  volver vacías a Sullana. El STATUS es el final del viaje, no otra descarga. Dos lugares de espera:
  - **Cochera** [-8.13855, -79.01755](https://www.google.com/maps?q=-8.13855,-79.01755): OpenStreetMap lo marca
    como estacionamiento (calle Fundo Larrea, Curva de Sun, Moche), a ~500 m de Vitapro. T6F-857, BNG-758 y
    BPT-788 (y BPT-788 otras dos veces, ~24 h, en julio y agosto). La oficina lo había señalado como Novopan, pero
    Novopan descarga en 2–4 h en su almacén (calle Camino Real, -8.14396, -79.01987); este punto es la cochera.
  - **Grifo de Curva de Sun** [-8.14790, -79.01282](https://www.google.com/maps?q=-8.14790,-79.01282): grifo
    de la Panamericana (OpenStreetMap), ~1,4 km al sur de Vitapro. BNG-862 y T6F-864 (28/08–01/09); ahí también
    esperan 6–7 h las de Novopan después de descargar.

### 4.4 Afrecho de Alicorp Paita a Ecuador (desde 2026-10-24, se aprende del GPS)

- Sección **AFRECHO-PAITA** del Word del 23/10/2026 (cargan el día siguiente): afrecho de trigo en sacos de 40 kg
  que Alicorp vende FCA Paita, puerto de descarga Huaquillas. El comprador contrata el flete y es el cliente
  (primera factura F319-00021039 a MALDONADO TORRES JOSE RODRIGO, Balsas, El Oro); como puede cambiar, al leer el
  Word se propone el cliente de la última carga en Paita.
- La ruta la da la planta: toda carga en la planta **Paita** es `tipoRuta = AFRECHO` (`sp_SE_SincronizarDespachos`).
- Recorrido supuesto: Alicorp Paita (geocerca `ALICORP_PAITA`, de AVM-877 el 24/10: llega 06:51 por la Av.
  Industrial, espera en la puerta 1 h y carga desde las 08:06) → Complex → TCI (como Crisar) → **primera parada
  de 1 h o más después de TCI** como descarga, con el lugar que da la dirección de Onway → fin (base, Paita…).
- Por aprender con los primeros viajes: el punto de descarga (geocerca en Balsas u otro), si pasan por la base
  antes de la frontera, si cruzan por el Complex y TCI o por otro paso, y cuánto esperan en cada uno.

## 5. Calidad de los datos del Excel

- 7 filas sin cliente (las del 19/10 en Cartavio son de Crisar); la de Wiler Cruz (ARN-917, 19/10) dice
  POLINPLAST pero cargó en Cartavio, así que es Crisar.
- Destinos con varios nombres: GYQ / GUAYAQUIL, CHEPE / CHEPEN; 12 filas sin lugar de entrega.
- Nombres mal escritos: IGANSIO/IGNASIO MUÑOZ, PRIEER SILVA, NOLBERTO SUNCIÓN/SUNCION.
- No hay viajes en febrero de 2026 (el Word tiene 4 de Novopan) ni de Crisar en junio (el Word tiene 5).
- STATUS: 573 "UNIDAD DESCARGADA EN SU TOTALIDAD", 6 **FALSO FLETE** (5 de Crisar el 03/09), 5 descargadas en
  la cochera de Trujillo (Polinplast, fines de agosto).
- En el Word, títulos con erratas que un lector tiene que tolerar: GUAAYQUIL, VITPAPRO, GUAYAQUIL.VITAPRO,
  POLINPAST, OLMOS OROYECTO, TRUJILO; AZÚCAR con y sin tilde; "CARTAVIO-AZUCAR" y "AZUCAR-CARTAVIO". Dos archivos
  con la fecha mal puesta en el nombre: `DESPACHO 18-12-26` y `DESPACHO 19-02-25` (son de febrero de 2026).

## 6. Preguntas abiertas (también en el plan)

- Resuelto (2026-10-21, GPS y web):
  - **ACP = Agrícola Cerro Prieto**, agroexportadora con fundo en la Panamericana Norte km 733, Pacanga (Chepén)
    ([acpagro.com](https://www.acpagro.com/aboutus)); el punto del GPS está en la Panamericana en Pacanga. Por eso
    el Word dice ACP o CHEPEN para el mismo lugar.
  - **TALSA** = Agroindustrial TALSA (Grupo Rocío), palta y arándano con campos en Virú, Chao y Zaña
    ([agraria.pe](https://agraria.pe/index.php/noticias/talsa-contara-con-mil-hectareas-de-palta-al-18061)). Falta
    un viaje con GPS para ubicar dónde se entrega.
  - -3.536, -80.184 es TCI; -8.1386, -79.0176 es la cochera de Trujillo (sección 4.3).
- **Guía de Polinplast** (T700-00001683, 14/10/2026, C6K-799 / TEC-993, la recibió el usuario): la emite
  Polinplast (guía de remitente electrónica, con su factura F700-00001751); nosotros vamos como transportista
  (RUC 20483851171). Punto de partida: Car. Piura–Tambogrande km 19, caserío San Vicente, "costado comedor
  Olivares **Rapel**", Tambogrande: es la planta de Chapaira, y Rapel es el nombre del lugar (por eso
  POLINPLAST-RAPEL carga en Chapaira). Llegada: Hortifrut Perú, Fundos Armonía (Panamericana Norte km 495,
  Chao, Virú): el destino CHAO es Hortifrut.
- **Rapel**: Sociedad Agrícola Rapel (capital chileno, uva, arándano y limón) está en Piura, fundo El Papayo
  ([FreshPlaza](https://www.freshplaza.com/latin-america/article/9498952/sociedad-agricola-rapel-exports-some-4-500-containers-of-fruit-each-year/)),
  pero los viajes "RAPEL" del GPS terminan en Jayanca (carretera Lambayeque–Olmos, El Camal). Puede ser otro
  fundo suyo o un packing de terceros: confirmar con quien arma el Word.
- Polinplast: confirmar con Rosmery cómo es la guía (remitente digital + transportista).
- Crisar y Novopan: ¿se registran en el SGV como despacho con su factura, como Vitapro, o solo se siguen?
- ¿Cómo se cobra el falso flete y cómo se registra?
