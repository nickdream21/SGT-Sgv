# Plan de mejoras SGV — estado y tareas pendientes

Última actualización: **2026-10-22**, barra del seguimiento: a la vista los 5 Excel y Actualizar GPS, lo demás (copiar textos, nuevo viaje, importar, dashboard) en el menú Más; antes, botón Excel de llegadas en el seguimiento (sin migración ni SP); antes, mapa del viaje de terceros: empieza en la salida de base o en el fin del viaje anterior (sin migración ni SP; volver a publicar); antes, grifo Santa Rosa de vuelta de Ecuador (migración 35, botón "Cuadro grifo Santa Rosa", llegada aprox. a la base); antes, llegada estimada a planta (Trujillo / Jave / Inbalnor) y "Reporte de llegadas" en el tablero del seguimiento (sin migración ni SP; volver a publicar; sección 3); antes, desvíos: la lista de destinos del cliente cambia la bodega del viaje (Jave/Inbalnor) y el GPS manda al llegar (sobre `a1fef1d`; SPs `sp_DD_RegistrarDestino` y `sp_SE_SincronizarDespachos`; sección 3); antes, terceros validados con 110 viajes reales del Excel (Jave/Inbalnor para Crisar, Novopan ya en Quito, relevo de hasta 6 días, Chao con 3 puntos, cochera 20 h; sobre `4cc9f15`; sin migración ni SP; volver a publicar); antes, mapa de la ruta sin "saltos" (relevo de terceros y puntos sueltos del GPS; sobre `018ae52`; sin migración ni SP; volver a publicar); antes, terceros: clientes Crisar y Polinplast registrados, 7 cargas de Crisar del 19/10 con sus entregas, y corrección del relevo en el GPS (sobre `a5cdbe5`; sin migración ni SP; script de corrección; volver a publicar); antes, terceros: seguimiento y "Excel STATUS EXTRA" (migración 34; SP `sp_SE_SincronizarDespachos`, `sp_SE_RegistrarEntregaTercero` nuevo y `sp_SE_Dashboard_*`, ya en sgvActualizada; sobre `9ab3d9a`; sección 3 y 6); antes, terceros: la cochera de Trujillo sale del GPS de los 5 viajes "DESCARGADA EN COCHERA TRUJILLO" (esperan 1–4 días y cargan azúcar en Cartavio; geocercas `COCHERA_TRUJILLO` y `ESPERA_GRIFO_CURVA_SUN` en lugar de `NOVOPAN_TRUJILLO_2`) y ACP, TALSA y Rapel buscados en la web (sobre `ddf048e`; sin migración ni SP; sección 3); antes, recorrido GPS de los 5 viajes (sobre `ba168c5`); antes, terceros: respuestas de la oficina (-3.536, -80.184 es TCI; -8.1386, -79.0176 es un 2.º punto de descarga de Novopan, nueva geocerca; ACP es lugar de entrega) y el GPS muestra que Crisar y Novopan hacen el tramo de frontera de Vitapro (sobre `5775413`; sin migración ni SP; sección 3); antes, terceros: "Programar con el Word" reconoce Crisar, Novopan y Polinplast (selector de sección) y 14 geocercas desde el GPS (sobre `c4d9c38`; sin migración ni SP; sección 3); antes, análisis de los clientes terceros (Crisar, Novopan, Polinplast) en `docs/CLIENTES_TERCEROS.md` (sobre `da3782a`); antes, cargas de mañana visibles en el tablero del seguimiento y formulario manual de RegistroDespacho plegado (sobre `3a84af0`; sin migración ni SP; sección 3); antes, Word del 21/10 subido (cargas del 22/10 y descargas de 4400133950 y 4400133952; sobre `ec7d06e`); antes, STATUS de la unidad en el tablero (migración 33); antes, seguimiento: unidades en la cola de Trujillo, carga que tomaba la del viaje anterior del tracto y "Actualizar GPS" en paralelo (sobre `fa9f6ab`; sin migración ni SP; sección 3); antes, despacho en dos momentos (programar con el Word, completar con el correo) y caché del historial GPS para "Actualizar GPS" (sobre `5c94014`; SP `sp_CompletarDespachoProgramado` y `sp_SE_SincronizarDespachos`; sección 3); antes, al día con el seguimiento de exportación (`a22748b`) más el cuadro STATUS GENERAL de octubre armado desde los Word y el GPS (119 viajes de octubre ya en `sgvActualizada`) y el mapa de la ruta en el detalle del viaje, la salida en camino y la vuelta corrida (sin migración; SP `sp_SE_RegistrarHitoGps` y `sp_SE_SincronizarDespachos`; sección 4). Este documento es el punto de partida para seguir trabajando
desde cualquier máquina. No contiene contraseñas: solo indica dónde están.

## 1. Estado actual

| | Al empezar (2026-10-07) | Ahora |
|---|---|---|
| Puntuación general | 5.5 / 10 | **8 / 10** |
| Tests | 215 | **649** en verde (2026-10-21; incluye los de integración, varios escriben con rollback) |
| Paquetes NuGet | 40 | 23 |
| Procedimientos del repo vs BD de pruebas | sin control, 2 inexistentes | **79/79 iguales** (la BD tiene exactamente los del repo) |

- Todo se trabaja contra la BD de **PRUEBAS** (`sgvActualizada`). El sistema todavía no lo usa nadie.
- **Producción (`sgvTransporte`) NO está actualizada**: el código de `master` no se puede publicar
  hasta seguir la lista de la sección 6 (en orden).

## 2. Preparar la laptop de casa

1. **Clon del repo.** El historial se reescribió el 2026-07-10 (purga de PDFs). Si el clon de la laptop
   es anterior a esa fecha, **no hagas `git pull`**: vuelve a clonar
   (`git clone https://github.com/nickdream21/SGT-Sgv.git`) o `git fetch && git reset --hard origin/master`
   (esto descarta cambios locales sin subir). **Nunca hagas push desde un clon viejo**: volvería a subir
   el historial purgado (ya pasó una vez con la rama `claude/claude-md-docs-miUln`, que se borró).
2. **Archivos que NO están en git** (cópialos desde la PC de la oficina, p. ej. con la transferencia de
   archivos de AnyDesk, a la misma ruta dentro del repo):
   - `WebSGV/connectionStrings.config` → BD de pruebas (la que usa la app en local y los tests).
   - `WebSGV/connectionStrings.Production.config` → solo para el paso a producción.
   - `WebSGV/appSettings.Secrets.config` → SMTP, credenciales de Onway (GPS).
   - `privado/` → `CREDENCIALES_USUARIOS.txt` (un usuario por rol en pruebas, panel de somee) y
     `cambiar_password_nickdream_PROD.sql`.
   - `.env` y `.claude/settings.json` (solo si usas Data API Builder o el MCP de SQL).
3. **Herramientas:** Visual Studio 2022+ (MSBuild, IIS Express), .NET SDK (para `dotnet test`) y
   *Microsoft Command Line Utilities for SQL Server* (sqlcmd, ODBC 18).
4. **Comprobar:**
   ```powershell
   msbuild WebSGV.sln /p:Configuration=Debug /nologo /verbosity:minimal
   dotnet test WebSGV.Tests\WebSGV.Tests.csproj --no-build      # 457 en verde
   ```
   Sin `connectionStrings.config` los de integración salen como "omitidos" (no fallan).
5. **Para que Claude pruebe en el navegador** (una vez por máquina): extensión Claude in Chrome
   conectada con la misma cuenta de claude.ai (pasos en `herramientas/sitio-local/README.md`). Copiar
   también `docs/DOCUMENTACION/` (documentos reales de prueba, gitignored). El sitio se levanta con
   `herramientas/sitio-local/iniciar-sitio.ps1`; sin navegador, `probar-importacion.ps1` prueba por HTTP.

## 3. Lo que ya está hecho

| Commit | Contenido |
|---|---|
| `2979a41` | Guards que ahora sí detienen la página; WebMethods con sesión/rol; XSS; reset de contraseña con URL fija; página de error sin detalles; regeneración de sesión al login; bugs de datos (gastos de OV, `Session["Usuario"]`, correlativo de abastecimiento, CPIC en transacción); 22 SPs al repo; registro de migraciones; jQuery/Bootstrap unificados; Excel de Reportes a servicio. |
| `df1dd3f` | `/Uploads` privado (descarga por llave de sesión); cambio de contraseña obligatorio en servidor; bloqueo de login por cuenta; sin contraseñas en texto plano; auditoría faltante; errores técnicos fuera de la pantalla (`MensajeErrorHelper`). |
| `e09f4bf` | Hora de Perú en BD y código (`dbo.fn_AhoraPeru()`, migración 16); token GPS sin carreras (`sp_getapplock`) y reintentos 429/5xx; últimas consultas de ListaDespachos/RegistroDespacho a servicios. |
| `f96f145` | 17 paquetes sin uso fuera (MVC, bundling, FriendlyUrls, AjaxControlToolkit, compilador deprecado); tests de integración; `aplicar-procedimientos.ps1`; `sp_SE_Listar` corregido (la grilla de registros recientes salía vacía); tildes corruptas reparadas en BD y repo. |
| `8a2fd62`…`dfb2194` | (2026-10-12) Logo en los Excel de reportes; revisión en navegador por rol (ver sección 4); migraciones 24–25. |
| `4cf583d` | (2026-10-15) RegistroDespacho importa los documentos del pedido (factura, packing list y guías en PDF/ZIP, con PdfPig 0.1.9) y arma el lote con su carga; carga por despacho (`DespachoCarga`), destino de la carga y moneda de factura; migraciones 26–28; SPs de alta aprobada de productos y conductores. |
| `6616667` | (2026-10-15) Descarga por pedido enlazada a su carga (migración 29); lectura del Word de programación (chofer, tracto y planta por la carreta); avisos de error visibles. RegistroChoferes: "Actualizar con la lista" de choferes actuales (alta, licencia, nombre, carnet de extranjería y bajas) con vista previa. |
| `cc71bde` | (2026-10-16) Word de programación en la importación de carga (chofer, tracto y carreta del Word); `herramientas/sitio-local`. |
| _(destino de descarga)_ | (2026-10-18) Destino de descarga por unidad (pedido + carreta): pantalla `DestinoDescarga.aspx` (Excel o filas pegadas con vista previa, corrección manual, historial); la descarga por pedido propone la planta de cada tracto desde esa lista; plantas Milagro y Jave; migración 30 y `sp_DD_*`. |
| _(reporte GPS)_ | (2026-10-19) Reporte GPS por placas (`ReporteGps.aspx`, menú Despacho): historial de varias unidades a la vez desde el API de Onway (el portal solo deja 3), un Excel por placa con las columnas del portal o todos en ZIP; trae los tractos de carga y descarga de los pedidos; muestra la última posición de cada unidad y arma asunto y mensaje del correo. Comparado con el Excel del portal (BNG-862, 19/10, 983 filas en el mismo orden): idénticos fecha/hora (es la de recepción, `generateTime`), dirección, velocidad, batería (`internalBatteryLevel`) y alertas; coordenadas 978–961/983 (6.º decimal); odómetro (`mileage + odometerDeltaPatch`) 888/983 — en las filas de alerta el API no da odómetro y se repite el último. El horómetro solo sale en unidades con CANBUS (el portal lo calcula aparte) y *Tipo de carrocería* no está en el API. 11 placas en ~40 s. Sin migración ni SP. |
| _(trazabilidad, tramo carga)_ | (2026-10-19) "Verificar GPS" de RegistroSeguimiento detecta el tramo de carga del Tracto 1 (salida de base, llegada a Trujillo, ingreso y salida de planta, llegada a base) con geocercas en polígono y visitas en orden (`TrazabilidadCargaService`, `GeocercaGps`), en vez de círculos por columna: el círculo del ingreso tomaba la espera en la cola como ingreso (ANU-794 19/10: 07:05 en vez de 15:12). Usa las alertas de geocerca de Onway ("salió de Base Viviana Sullana") cuando existen. Banco de casos reales en `WebSGV.Tests/Datos/Trazabilidad` (historial guardado con `herramientas/reporte-gps/Guardar-HistorialCaso.ps1` + horas de la administradora): 5 casos en verde; contra el Excel STATUS GENERAL, 24/24 viajes de fines de septiembre con ingreso y salida de planta a ±2 min. Además el **tramo de descarga** del Tracto 2 (`TrazabilidadDescargaService`): salida de base, Complex (cola, patio), CEBAF, cruce a la E50, TCI (Transcomerinter) y Jave o Inbalnor, con la llegada final a base; geocercas dibujadas con OpenStreetMap. Con BFI-922 (pedido 4400133136, Jave) los 12 hitos salen a ±66 s y con BSD-716 (Inbalnor, 29-30/09) a ±33 s. Si la unidad pasa directo por TCI sin entrar (coordinado con TCI), llegada y salida de TCI se toman en el control de la Aduana del Ecuador ~400 m al noreste (`GeocercasTrazabilidad.TciControlAduana`). Probado de punta a punta sobre el seguimiento 7063 dentro de una transacción revertida: llena 16 de 18 columnas y detecta JAVE. Sin migración ni SP. |
| _(pantalla de seguimiento)_ | (2026-10-20) RegistroSeguimiento rehecho como **tablero**: un viaje por pedido y carreta, fase calculada por sus horas (en carga, en base, rumbo a / en Ecuador, retorno, cerrado), avance de cada tramo, último hito y avisos (falta inicio/término, descarga sin tracto, paso directo por TCI, espera larga en Trujillo/Complex/TCI); filtros por fecha de programación, texto (busca en todas las fechas) y planta; "Actualizar GPS de viajes abiertos" (6 por clic, primero los que llevan más tiempo sin consultar) y "Exportar STATUS GENERAL" (fecha + HORA por hito, se puede volver a importar). **Detalle** nuevo `SeguimientoViaje.aspx`: línea de tiempo de carga y descarga con el origen de cada hora (GPS, alerta Onway, registro anterior, corregido a mano), evidencia (regla, coordenadas, enlace al mapa, hora del GPS aunque la columna venga del Excel), corrección manual con motivo (el GPS ya no la pisa), datos que el GPS no sabe, datos del viaje, cerrar/reabrir y nuevo viaje a mano. La importación del Excel pasó a `ImportarSeguimiento.aspx` sin cambios de lógica. Migración 31 (`SeguimientoHito`, `fhUltimaConsultaGps`) y `sp_SE_RegistrarHitoGps`, `sp_SE_CorregirHito`, `sp_SE_GuardarDatosViaje`, `sp_SE_GuardarDatosManuales`, `sp_SE_CambiarEstado`. Probado por HTTP (tablero, pestañas, búsqueda, exportación, detalle, nuevo, importar) y con una prueba de integración con rollback de las escrituras. |
| _(cuadro de octubre)_ | (2026-10-20) `herramientas/cuadro-status-general`: arma el cuadro STATUS GENERAL de un mes desde los Word de programación (carga y descarga unidas por la carreta; CAMBIO decide si la carreta sale en dos cargas seguidas) y las horas del GPS con la lógica de "Actualizar GPS" (si el tracto cargó el día anterior, se salta esa carga). Octubre: 127 cargas de Vitapro del 01 al 20/10, 113 con descarga, 111 con la carga completa; los 10 viajes del 10/10 coinciden al minuto con el sistema y la carga de ANU-794 del 19/10 con las horas marcadas a mano. Excel en `docs/DOCUMENTACION/2026/2026/OCTUBRE/STATUS GENERAL OCTUBRE 2026.xlsx` (gitignored). Con `-Bd aplicar` se crearon en sgvActualizada **103 viajes MANUAL con 1 560 horas** (origen, coordenadas y regla en `SeguimientoHito`); quedan 119 viajes de octubre. |
| _(salida en camino)_ | (2026-10-20) "Actualizar GPS" no veía a las unidades que ya salieron de base y van en camino: la salida de base solo se registraba al encontrar la primera parada (planta de Trujillo o Complex), así que 7 descargas del 19/10 (BFH-852 por Mallares, CBX-832 ya en Zorritos…) y la carga de CBU-945 seguían "en base". Ahora, sin parada todavía, la salida es la última salida de la base tras la que la unidad no volvió y ya va por la ruta del tramo (descarga: ~10 km al norte, rumbo a Talara; carga: pasado Piura hacia el sur, para no confundirla con una vuelta al taller o al grifo). `sp_SE_RegistrarHitoGps @reemplazarGps`: la salida de base que puso el GPS se puede afinar en la siguiente consulta (nunca la corregida a mano ni la del Excel). El mensaje "no se detectó si fue por Jave o Inbalnor" sale solo si la unidad ya está en Ecuador. SP aplicado en pruebas; 6 tests nuevos de trazabilidad y 1 de integración. |
| _(vuelta corrida)_ | (2026-10-20) Revisión de las 30 unidades en movimiento contra el monitoreo de la administradora. (1) **6 cargas del 20/10 no tenían viaje** (BNG-758, ASU-862, TBM-815, CBU-928, BPT-788, BPS-816): solo se registró el pedido 4400133879 con los otros 6 despachos; al buscar la placa salía su viaje anterior. Se crearon a mano (7341–7346, pedido `PENDIENTE`), y `sp_SE_SincronizarDespachos` ahora **adopta** el viaje creado a mano (mismo tracto y carreta, ±1 día) cuando llega su despacho, en vez de duplicarlo. (2) **Vuelta corrida**: ASU-862 y TBM-815 volvieron de Jave y siguieron de frente a Trujillo sin entrar a la base; la descarga quedaba "Retorno de Ecuador" y la carga nueva sin salida. Ahora el regreso de la descarga y la salida de la carga siguiente son el paso frente a la base (≤ 2 km) antes de seguir al sur de Piura; una salida de base seguida de un paso por Ecuador ya no se toma como salida de la carga. (3) El regreso de la descarga se busca hasta 7 días (antes 3): 6 viajes de inicios de octubre (BFK-879 fue a Quito tras Inbalnor) seguían abiertos. (4) Una unidad estacionada junto a la cerca al final del historial ya no cuenta como salida (ASU-862 en la puerta de la planta). 8 descargas cerradas y 6 cargas creadas en sgvActualizada; 4 casos reales nuevos en `Datos/Trazabilidad` y 1 test de integración. |
| _(mapa de la ruta)_ | (2026-10-20) Detalle del viaje (`SeguimientoViaje.aspx`): panel **Ruta del viaje** con Leaflet (cdnjs) y teselas de OpenStreetMap: recorrido GPS del tracto de carga (azul) y del de descarga (naranja), hitos numerados, las 13 geocercas, km por tramo y reproducción animada (camión por tramo, línea que se va pintando, reloj con hora de Perú, velocidad y último hito; deslizador y velocidad de 10 min a 3 h por segundo). "Evidencia" de un hito lleva el mapa a ese punto y momento. `RutaViajeService` trae el historial de Onway (1 día por llamada), lo simplifica (≤ 2 500 puntos, la unidad parada queda en un punto) y guarda el tramo cerrado en `~/App_Data/RutasGps` (en curso: 10 min en memoria). `[WebMethod] ObtenerRuta` con sesión y rol; `Web.config` sube `maxJsonLength` a 4 MB. Probado en el navegador con un viaje cerrado, uno en curso y uno creado desde el cuadro; 3 tests de `Simplificar`. |
| _(caché del historial GPS)_ | (2026-10-21) "Actualizar GPS de viajes abiertos" volvía a pedir al API de Onway (1 día por llamada, 2 req/s) todos los días de cada viaje en cada clic y la lista completa de dispositivos una vez por viaje. Ahora `HistorialGpsCache` guarda comprimido en `~/App_Data/HistorialGps` cada día de un tracto que terminó hace más de 24 h (el equipo sin señal manda puntos atrasados) y lo comparte entre viajes; hoy y ayer quedan 3 min en memoria. La lista de dispositivos se comparte 30 min (si una placa no aparece se vuelve a pedir). La detección recibe los mismos puntos. Tope por clic de 6 a 40 viajes (el límite real sigue siendo el presupuesto de 80 s). Medido con T7J-860, 8 días: 16 s la primera vez, 5,2 s la siguiente, mismos 4 210 puntos. Sin migración ni SP. |
| _(despacho en dos momentos)_ | (2026-10-21) El correo con factura y guías puede llegar cuando la unidad ya salió. En RegistroDespacho, **subir solo el Word** muestra "Programar con el Word": la sección de carga ("TRUJILLO-VITAPRO-CARGAN 21 DE OCTUBRE") con cliente, planta y exportación del título, y por fila chofer, tracto, carreta (del catálogo, con los mismos avisos de la importación) y la hora del HORARIO; las unidades que ya tienen carga ese día salen "Ya programado". "Programar despachos" crea las cargas sin documentos (`sp_CrearDespacho`, `horaDespacho` = HORARIO, en el viaje abierto del chofer: la liquidación no espera al correo) y sincroniza el seguimiento, que crea el viaje con pedido `PENDIENTE` para que el GPS lo siga. Al subir después el ZIP, `ImportacionPedidoService.UsarProgramados` busca los programados del cliente en los días de las guías y cada guía **completa su despacho** (carreta → tracto → chofer → descarte, como con el Word; un programado sin guía es un cambio de chofer: queda lo programado y la guía no se reemite); cada guía tiene "Completa el despacho" para elegirlo a mano. Al finalizar el lote, `sp_CompletarDespachoProgramado` agrega pedido, factura, guía, N° de transporte, destino y carga sin tocar chofer, tracto, carreta ni viaje; el viaje del seguimiento toma el pedido. ListaDespachos muestra "Faltan documentos" en el pedido del lote. Con el Word del 20/10: 12 unidades, cliente Vitapro, planta Trujillo, exportación, 21/10, horas 05:30–06:25; 11/12 choferes reconocidos. **Programadas en sgvActualizada las 11 cargas del 21/10** (despachos 809–819, viajes del seguimiento 7371–7381); falta JUSTO CESPEDES (sin DNI). Además el **CPIC de la descarga internacional pasó a opcional** (puede llegar después; se agrega al lote en ListaDespachos; si se escribe el número se piden fecha y flete) y se registró con el Word del 20/10 la **descarga del pedido 4400133879** (despachos 820–825, unidos a las cargas 609–614, sin CPIC; los 6 viajes del seguimiento tomaron tracto 2 y chofer). LUIS ALBERTO PONCE MENA (DNI 75025996) estaba inactivo y descarga ANW-896: se reactivó (con auditoría). 4 tests de la unión y la hora, 1 de integración con rollback (programar → leer guías con un cambio de chofer → completar → seguimiento con el pedido). Sin migración. |
| _(cola de Trujillo y GPS en paralelo)_ | (2026-10-21) Revisión de las 11 cargas del 21/10 y las descargas en curso. (1) **La llegada a Trujillo esperaba al ingreso a la planta**: CBV-788 (en la cola desde las 08:39), CBV-761 (desde las 00:51), BNI-736, BNH-735 y BSC-900 seguían "Rumbo a Trujillo" horas después de llegar. Ahora, sin visita a la planta todavía, una estadía de 10+ min (o en curso) en el acceso de la Av. Gonzales Prada después de la salida de base es la llegada (`TrazabilidadCargaService.VisitaEnCola`), y el tablero avisa "X h en cola de Trujillo (sigue esperando)" (igual en el Complex). (2) **Una carga tomaba la del viaje anterior del mismo tracto**: el viaje 7381 (TBM-898, carreta TDX-994, 21/10) quedó con las 5 horas del 7095 (T0L-981, 20/10). La carga ahora se busca después de la salida de planta del viaje anterior del tracto; TBM-898 salió de base el 20/10 a las 21:44 y a las 10:45 ya estaba en la cola. Script `Database/Scripts/corregir_seguimiento_7381_2026-10-21.sql` para borrar esas horas. `PasoFrenteALaBase` ya no toma como paso la salida misma de la base ni se queda con el primer paso de la ventana. (3) **"Actualizar GPS" más rápido**: cada llamada al API tarda ~2,3 s y se hacían una tras otra; ahora el historial de los viajes del clic se trae antes en paralelo (4 llamadas a la vez, respetando las 2 por segundo: 6 días en 5,7 s en vez de 14,1 s), una página de 5 000 puntos trae el día entero en una llamada (antes 2) y ayer queda 30 min en memoria. Orden: primero las cargas (las programadas más recientes antes), luego descargas y retornos, y al final los viajes programados hace más de 10 días. El error dice placa y carreta, y "no tiene equipo GPS en Onway" si la placa no está. 3 casos reales nuevos en `Datos/Trazabilidad` y 2 tests. Sin migración ni SP. (4) **Salida de la bodega en Ecuador falsa**: al pasar la puerta de Jave la unidad cruza la esquina del polígono de bodegas y se estaciona en el patio de al lado, y se marcaba la salida al minuto de entrar (los dos convoyes del 21/10: BSD-900, BFI-890, BSD-716, BNH-869, BFK-729, T8V-933, BFK-879, CBU-930, BFI-922, T7J-860 y BFH-852 "salían" a los 1–5 min y seguían en el patio; 30 viajes de octubre con 0–29 min). Ahora la salida es la última de la bodega y solo cuando la unidad ya dejó el recinto (Jave e Inbalnor). Script `Database/Scripts/corregir_salidas_bodega_ecuador_2026-10-21.sql` para borrar esas salidas y volver a detectarlas. (5) Lo que el GPS ya había detectado igual (misma hora y regla) no se vuelve a escribir, y el tope por clic subió a 60: con el sitio local contra sgvActualizada, **los 46 viajes abiertos en un clic**, 25 s con la caché en memoria y ~80 s con el sitio recién iniciado. Caso real BSD-900 en Jave. |
| _(STATUS de la unidad)_ | (2026-10-21) La administradora escribía a mano, cada cierto rato, el STATUS de cada unidad (última columna amarilla del STATUS GENERAL: "UNIDAD EN PROCESO DE DESCARGA - JAVE", "UNIDAD DESCARGADA CAMINO A BASE - VIRGEN DE FATIMA") y lo enviaba. Ahora el tablero lo arma solo: `StatusUnidad` toma la fase del viaje (sus horas) y, en los tramos en camino, el lugar general donde está la unidad: "Actualizar GPS" guarda la última posición del tracto que lleva la carga (el de carga hasta salir a frontera, luego el de descarga; migración 33, columnas `fhUltimaPosicion`, `ultimaLatitud/Longitud`, `ultimaVelocidad`, `ultimaDireccion`, `ultimaPlacaGps`) y se usa la localidad de la dirección que da Onway ("Carretera Panamericana Norte, El Alto, Talara, Piura" → EL ALTO; "Troncal de la Costa, Virgen de Fátima, ..." → VIRGEN DE FÁTIMA). Textos: vacía camino a Trujillo, en cola / en proceso de carga (Trujillo), cargada camino a base Sullana, cargada en base, cargada camino a frontera (hasta llegar al Complex) y camino a Ecuador (después del Complex), en cola / en nacionalización (Complex), en frontera (CEBAF), cargada en Ecuador camino a Jave/Inbalnor, en cola / en proceso de descarga, descargada camino a base, descargada en su totalidad. Si la última señal tiene más de 2 h se agrega "(ÚLTIMA SEÑAL dd/MM HH:mm)". En RegistroSeguimiento: columna STATUS con botón de copiar por fila y "Copiar STATUS" (todas las filas, en orden de la tabla, para pegar en la columna del Excel); "Exportar STATUS GENERAL" suma la columna STATUS. 22 tests (`StatusUnidadTests`). Cuadro de octubre regenerado con la lógica corregida de Jave. Después: bajo cada STATUS se ve "Ubicación al dd/MM HH:mm" (hora del último punto del GPS; no se muestra en viajes cerrados) y el botón **Excel de STATUS** baja un Excel liviano con las columnas del cuadro de la administradora (fecha, pedido, carreta, conductores, placas de carga y de cruce, STATUS, hora de la ubicación) de lo que muestra la tabla: con el pedido en Buscar, solo ese pedido (`ExportacionStatusUnidades`). Falta el N° de factura (el seguimiento no lo guarda). |
| _(mapa: de dónde viene el tercero)_ | (2026-10-22) En el mapa del viaje, la carga de un cliente tercero empezaba **14 h antes de llegar a la planta de carga** (los terceros no tienen salida de base), así que la línea arrancaba en cualquier punto de la Panamericana. Ahora `RutaViajeService` busca el viaje anterior del mismo tracto en el seguimiento (lo que hizo ese tracto: su carga si era el tracto 1, su descarga o entrega si era el tracto 2) y el GPS de hasta 4 días antes de llegar a cargar: el recorrido empieza en la **última salida de la base** o, si no entró, donde **terminó su viaje anterior**, y el mapa lo explica ("Salió de la base el 17/10 11:16; parada de 30 h del 17/10 15:18 al 18/10 21:25", "Su viaje anterior (Vitapro) terminó al volver a Sullana el 18/10 12:41 y no entró a la base antes de ir a cargar"). Las paradas de 6 h o más antes de cargar también se avisan. Probado con los Crisar del 19/10 (BSC-900, ARN-917, BNI-736, ASU-748) y del 15/10. 4 tests (`RutaInicioTerceroTests`). Sin migración ni SP. |
| _(grifo Santa Rosa)_ | (2026-10-22) **Para qué** (audio de Eliana, oficina de frontera): avisarle a la encargada del grifo Santa Rosa cuántos y cuáles carros bajan a cargar combustible para que tenga capacidad; hoy se manda un cuadro en la mañana con una hora puesta al azar, no se actualiza y hubo carros que se quedaron sin combustible. **Dónde está el grifo**: Troncal de la Costa, El Retiro (Machala), ~2 km al norte de Santa Rosa y ~30 km de Huaquillas: -3.42855, -79.96242 (OpenStreetMap: estación "p&s"); cerca de Huaquillas no hay otra parada de combustible en el GPS. Con 112 descargas del 15/09 al 20/10: **111 paran ahí a la vuelta** (12 también a la ida), llegan 4,6 h después de salir de la planta (p10–p90 3,8–12,8 h), se quedan 2,2 h (0,7–11,9) y tardan 11,8 h más a la base; casi todas llegan entre las 18:00 y las 22:00. Geocerca `GeocercasTrazabilidad.GrifoSantaRosa` (150 m, 10 min mínimo); `TrazabilidadDescargaService` marca llegada y salida del grifo y "Actualizar GPS" las guarda (**migración 35**, `fhLlegadaGrifo`/`fhSalidaGrifo`); STATUS "UNIDAD DESCARGADA EN GRIFO SANTA ROSA". **Reporte grifo Santa Rosa** (botón que copia el texto para WhatsApp) y **Cuadro grifo Santa Rosa** (Excel), de Vitapro y Crisar, sin depender de los filtros de la tabla: en camino al grifo con su **hora aproximada de llegada al grifo** (perfil `RETORNO_GRIFO`, 102 viajes, 183 km), en el grifo ahora, descargando y yendo a descargar (con la hora de llegada a la planta); los que ya cargaron combustible salen. Se recalcula con cada consulta al GPS (el tablero la hace solo si tiene más de 15 min). Después del grifo, el tablero da la llegada aprox. a la base (perfil `RETORNO_BASE`, 102 viajes). Un viaje que lleva 4+ días sin avanzar no entra (C4Z-745 del 16/10). Con los datos del 22/10 10:33: 12 carros yendo a descargar a Jave/Inbalnor (llegan 10:50–14:20), ninguno en camino al grifo todavía. Tests en `GrifoSantaRosaTests` (con los casos reales de descarga). |
| _(llegada estimada)_ | (2026-10-22) Casi todos los días piden "el estado de las unidades que aún no llegan a planta" con una hora aproximada. En RegistroSeguimiento, bajo el STATUS de cada unidad que va en camino a **cargar a Trujillo** (salió de base y no llegó) o a **descargar a Jave/Inbalnor** (salió de TCI o cruzó y no llegó) sale "Llega aprox. 22/10 13:10 (12:50–13:30) · faltan 155 km a Trujillo", y el botón **Reporte de llegadas** copia el texto para enviar (todas las unidades en camino, por planta y por hora de llegada, con lugar, km, si está detenida y la última señal si tiene más de 2 h). No es distancia ÷ velocidad: `LlegadaEstimada` compara la última posición con lo que tardaron los viajes reales desde ese mismo punto del camino (±5 km) y a la misma hora del día (±1,5 h, se amplía si hay menos de 8), así entran las paradas de siempre; el rango son los percentiles 25–75. Los perfiles (`Services/Exportacion/PerfilesLlegada.json`, recurso de la DLL) los arma `herramientas/perfil-llegada` con los viajes del seguimiento desde el 15/08 y el historial de Onway: 140 viajes de carga (473 km), 224 a Jave y 130 a Inbalnor (desde TCI, ~215 km). Con las unidades del 22/10 a las 10:00: Pacasmayo → Trujillo 12:20–12:30, Chepén 13:10, Jesús María → Jave 11:00. No estima en base, en cola, en el Complex ni en TCI. Botón **Excel de llegadas** (2026-10-22, `ExportacionLlegadas`): las mismas unidades y orden que el texto, con operación, pedido, tracto, conductor, carreta, ubicación, señal (en marcha / detenida / última señal, en rojo si tiene más de 2 h), km restantes, llegada aprox. (en rojo si ya pasó), rango y observación; resumen por planta. 16 tests (`LlegadaEstimadaTests`). Sin migración ni SP. |
| _(desvíos Jave / Inbalnor)_ | (2026-10-22) El STATUS decía "CARGADA EN ECUADOR CAMINO A INBALNOR" a las 12 unidades de los pedidos 4400133879 y 4400133881, pero la lista del cliente del 22/10 manda 5 a JAVE (T0L-981, T0L-975, T4C-976, T4L-975, T5F-972). La bodega del viaje se ponía una sola vez y el despacho de descarga ganaba sobre la lista (el despacho toma "CD VITAPRO MILAGRO" de la factura para todo el pedido); además el GPS, al ver entrar la unidad a Jave, no cambiaba una bodega ya puesta. Ahora: (1) `sp_DD_RegistrarDestino` (pantalla Destino de descarga, Excel/pegado/manual) cambia la bodega del viaje del seguimiento (pedido + carreta) mientras la unidad no haya llegado a la planta; (2) `sp_SE_SincronizarDespachos` da prioridad a la lista sobre la planta del despacho; (3) "Actualizar GPS" pone la planta donde vio entrar a la unidad aunque el viaje ya tenga otra (auditoría "ruta detectada: JAVE, antes INBALNOR"). Antes de llegar el GPS no puede saber a cuál va (mismo camino hasta Guayaquil): sin la lista, el STATUS dice la del despacho. Lista del 22/10 registrada en sgvActualizada con `Database/Scripts/registrar_destinos_descarga_2026-10-22.sql` (12 unidades). 1 test de integración nuevo. Sin migración. |
| _(GPS automático al abrir)_ | (2026-10-21) `RegistroSeguimiento`: al abrir la pantalla, si la última consulta al GPS de los viajes abiertos tiene más de 15 minutos (o no hay ninguna), la página pulsa sola "Actualizar GPS" (aviso y un postback; no se repite). `?auto=0` lo evita; el botón sigue para forzar. Sin proceso en segundo plano (Somee puede dormir el sitio). Si sobran viajes, el mensaje "Faltan N" pide pulsar otra vez. |
| _(terceros: seguimiento y STATUS EXTRA)_ | (2026-10-21) Cada carga de Crisar, Novopan o Polinplast (cliente cuyo nombre lo dice) es un viaje del seguimiento: migración **34** (`SeguimientoExportacion.tipoRuta`, `lugarCarga`, `lugarEntrega`, `lugarFin`) y `sp_SE_SincronizarDespachos` (los crea con cliente = nombre del cliente, lugar de carga = planta, entrega = destino del despacho si lo tiene; anulado → CANCELADO; ya no los toma como exportación de Vitapro). La sección de descarga del Word (GUAYAQUIL-AZUCAR, NOVOPAN-PIURA, POLINPLAST-VIRU…) aparece en "Programar con el Word" como **"Entregas de clientes terceros en este Word"**: cada fila se une con la carga del mismo cliente por carreta (o tracto) de los 10 días anteriores y "Registrar entregas" pone el lugar de entrega y, si descarga otro tracto (CAMBIO de Crisar), tracto y chofer de descarga (`sp_SE_RegistrarEntregaTercero`, nuevo). Un Word solo con entregas también se lee. "Actualizar GPS": `TrazabilidadTercerosService` sigue el recorrido de cada ruta con `GeocercasTerceros` y las de Vitapro en la frontera, en las mismas columnas de hora (Crisar: Cartavio → base → relevo → Complex → TCI → La Troncal → fin; Novopan: Quito → TCI → CEBAF → Piura/Trujillo → fin; Polinplast: Chapaira → destino → fin); si el viaje no sabe a dónde va, el GPS lo deduce (primer destino del cliente donde se detuvo). El fin es la base, la cochera de Trujillo, el grifo de Curva de Sun o la carga siguiente (Vitapro Trujillo solo con 3+ h de espera). `RutaTercero`: hitos, fase, avisos (sin lugar de entrega, espera en TCI y en la planta de carga) y STATUS con las frases del STATUS EXTRA ("UNIDAD CARGADA CAMINO A BASE - PAIJAN", "UNIDAD EN COMPLEX", "UNIDAD EN BODEGA PARA DESCARGA - LA TRONCAL", "UNIDAD DESCARGADA EN COCHERA TRUJILLO"). Tablero: filtro **Cliente** (Vitapro / Terceros / cada uno), carga → entrega bajo la fase, avance con los hitos de la ruta, botón **"Excel STATUS EXTRA"** (FECHA CARGA, CONDUCTOR DE CARGA/DESCARGA, PLACA TRACTOR CARGA/DESCARGA, PLACA SEMIREMOLQUE, CLIENTE, LUGAR DE CARGA, LUGAR DE ENTREGA, STATUS); "Exportar STATUS GENERAL" y el dashboard (`sp_SE_Dashboard_KPIs/Graficos`) quedan solo con Vitapro; la pestaña "ecuador" se llama "Rumbo a Ecuador / a la descarga". Detalle del viaje y mapa con los tramos de la ruta. Probado con el GPS real de 3 viajes del Excel (Polinplast BNG-758 26/08 → cochera, Crisar BNG-758/CBX-832 14/10 con relevo y noche en TCI, Novopan BFK-879 11/10): mismas horas y STATUS que el Excel; 1 test de integración con rollback (programar → viaje → entrega del Word → tablero → anular); por HTTP en el sitio local: filtro, Excel STATUS EXTRA sin terceros (aviso), STATUS GENERAL, detalle y las 5 entregas de Crisar del Word del 19/10 ("Sin viaje de carga", aún no hay cargas de Crisar). 641 tests. |
| _(terceros: programar con el Word y geocercas)_ | (2026-10-21) `RutaProgramacion` reconoce por el título las secciones de Crisar (CARTAVIO-AZUCAR / GUAYAQUIL-AZUCAR), Novopan (NOVOPAN-QUITO / NOVOPAN-<destino>) y Polinplast (POLINPLAST-CHAPAIRA o -RAPEL, que carga en Chapaira / POLINPLAST-<destino>), con las erratas de los 222 Word; `EsSeccionDeCarga` la usa (NOVOPAN-QUITO sin hora es carga; NOVOPAN-PIURA y POLINPLAST-VIRU, con hora, son descargas). "Programar con el Word": selector "Sección del Word" cuando hay varias, pasa sola a la siguiente después de programar, propone el día siguiente al del Word si el título no dice el día, Novopan como carga internacional (planta Quito). `GeocercasTerceros`: 14 círculos desde las paradas del GPS de 39 viajes de mayo–octubre (Cartavio, Chapaira, Novopan Quito/Piura/Trujillo, La Troncal y su parqueo, Virú, Chao, Casa Grande, ACP/Chepén, Jayanca/Rapel, Olmos, Salaverry) y `DeLugar(cliente, lugar)`; todavía no marcan horas. Sin migración ni SP. Después, con las respuestas de la oficina: la parada de ~22 h de la ida de Crisar (-3.536, -80.184) es **TCI** (la geocerca de Vitapro; a veces duermen ahí) y el GPS muestra que Crisar (ida) y Novopan (ida y vuelta) hacen el mismo tramo de frontera que la descarga de Vitapro (Base Sullana → Complex → TCI; vuelta por el CEBAF), así que ese tramo se seguirá con `TrazabilidadDescargaService` cambiando el destino final; ACP es Agrícola Cerro Prieto (Pacanga, Chepén). **Cochera de Trujillo**: los 5 viajes de Polinplast "UNIDAD DESCARGADA EN COCHERA TRUJILLO" (25–27/08) descargan en su destino, esperan 1–4 días en Trujillo y el 02/09 cargan azúcar en Cartavio (Word del 01/09); lugares de espera: el estacionamiento de la calle Fundo Larrea (-8.1386, -79.0176, OpenStreetMap; `COCHERA_TRUJILLO`) y el grifo de Curva de Sun (-8.1479, -79.0128; `ESPERA_GRIFO_CURVA_SUN`). 16 geocercas. |
| _(cargas de mañana en el tablero; formulario manual plegado)_ | (2026-10-21) `RegistroSeguimiento`: el "Hasta" por defecto es hoy + 7 días, así que las cargas programadas con el Word para mañana (pedido PENDIENTE) salen arriba sin buscarlas (antes el rango terminaba hoy); "Actualizar GPS" no consulta las programadas para después de hoy. `RegistroDespacho`: "Configuración base del lote" pasa a "Registrar un lote a mano", plegado por defecto (`hfManualAbierto` lo recuerda entre postbacks); se abre solo con una importación o descarga preparadas, que esperan "Iniciar lote". Sin migración ni SP. |

Detalle de reglas y convenciones nuevas: ver `CLAUDE.md`.

## 4. Tareas pendientes (en pruebas)

Ordenadas por valor. Marca `[x]` al terminar. Todo pendiente va como su propio `[ ]` en esta lista
(no escondido dentro del texto de una tarea hecha).

### Abiertas

- [ ] **Llegada estimada: comparar con la llegada real** unos días (lo que dijo el reporte a cierta hora vs la
      llegada que marcó el GPS) y ajustar la ventana de km/hora si se desvía; regenerar `PerfilesLlegada.json`
      cada mes con `herramientas/perfil-llegada/Generar-Perfil.ps1` (y recompilar: va dentro de la DLL).
- [ ] **Grifo Santa Rosa para Crisar**: ya entran al reporte (en camino al grifo por la posición, en el grifo si la
      posición está ahí), pero su parada no se guarda (`TrazabilidadTercerosService` no marca el grifo): marcarla
      como en Vitapro para que salgan del reporte en cuanto cargan y no recién al alejarse.
- [ ] **Reporte del grifo sin abrir el tablero**: hoy se recalcula cuando alguien abre RegistroSeguimiento (GPS
      automático si tiene más de 15 min). Para que el grifo lo reciba solo cada cierto rato hace falta la
      actualización automática (ver "Seguimiento, etapa 4") y un envío (correo o WhatsApp).
- [ ] **Grifo Santa Rosa: confirmar con Eliana y el grifo** el formato del reporte y si necesitan algo más (galones/importe del
      abastecimiento, quién lo autoriza) o si alguna unidad carga en otro grifo.
- [ ] **Llegada estimada para terceros** (Crisar a Cartavio y a La Troncal, Novopan, Polinplast): hoy solo
      Vitapro (carga a Trujillo, descarga a Jave/Inbalnor). Mismo método con sus viajes del seguimiento.
- [ ] **Llegada estimada desde TCI y el Complex**: hoy no se estima mientras la unidad espera en la aduana
      (depende del trámite). Ver si con más datos conviene dar un rango igual.
- [ ] **Excel de STATUS: agregar el N° de factura** del cuadro de la administradora (hoy el seguimiento no lo guarda;      está en la factura del despacho de carga, se puede traer con el `idDespachoOrigen`).
- [ ] **Publicar** (la BD del sitio ya tiene la migración 33) el código del STATUS de la unidad, la cola de Trujillo, la salida de bodega en Ecuador y el GPS en paralelo: hasta
      entonces el sitio sigue mostrando "Rumbo a Trujillo" a las unidades en la cola. (El viaje 7381 ya se corrigió
      con `corregir_seguimiento_7381_2026-10-21.sql` el 21/10 y el GPS nuevo, corrido desde el sitio local, ya llenó
      las llegadas a Trujillo en sgvActualizada.)
- [x] **Descargas del pedido 4400133881** (2026-10-21): con `DESPACHO 20-10-26.docx` (sección GUAYAQUIL-VITAPRO) quedaron registradas las 6 descargas
      (despachos 874–879; viajes 7341–7346 con tracto 2 y salida de base). Conductores nuevos: HUGO JEFERSON FLORES VARGAS (id 144, DNI 44520592,
      lic. B44520592) y SEGUNDO IGNACIO SAAVEDRA BRAVO (id 145, DNI 45541126, lic. C45541126). El despacho 873 (guía T660-00016530, a nombre de
      PIEDRA VEGA) pasó a HUGO FLORES / BNG-758 / T0L-975 como manda el Word y el viaje 7341 lo adoptó; se borró el viaje 7402.
- [x] **Pedido 4400133879 y ficha de SEGUNDO SAAVEDRA** (2026-10-21): no había nada que corregir en el pedido: sus 6 cargas del 20/10 (despachos 609–614)
      ya tenían su descarga (820–825) y los viajes 7095–7100 su tracto 2, por eso no figura como "pendiente de descarga". A SEGUNDO se le guardó la fecha
      de nacimiento (04/05/1986) y la dirección (Calle Santa Rosa, Sexi, Santa Cruz, Cajamarca).
- [ ] **Registrar las descargas del 21/10 que no tienen viaje**: AMQ-859 (salió de base 10:55), ARN-917 (07:45) y
      ASU-748 (en la noche del 20/10) van a Ecuador sin despacho de descarga; las cargas del 20/10 con pedido
      `PENDIENTE` (BNG-758, ASU-862, TBM-815, CBU-928, BPT-788, BPS-816) siguen "Descarga sin tracto asignado".
      Con el Word de la descarga, como el pedido 4400133879 (los Word del 20 y 21/10 no estaban en la PC de la oficina).
- [ ] **Medir "Actualizar GPS" en el sitio publicado** después de subir el código (local: 46 viajes en un clic, 25 s
      con caché y ~80 s recién iniciado). Si no alcanza: pedir de hoy solo lo nuevo (desde el último punto).
- [ ] **GPS de BFR-730**: Onway no tiene ningún dato suyo en septiembre ni en octubre (descargas del 02, 07, 09
      y 17/10 sin trazabilidad). Revisar con Location World si el equipo funciona o si está registrado con otra placa.
- [ ] **F60-779 no está en Onway** (carga del 03/10, pedido 4400133288): confirmar si tiene GPS o con qué placa.
- [ ] **Cuadro de octubre, revisar con la administradora**: carreta T0Q-991 en dos cargas seguidas (AVO-818 el
      09/10 y ANU-794 el 10/10; el GPS ve a los dos en Vitapro, uno de los Word tiene mal la carreta; AVO-818 quedó
      sin viaje); BPT-919 con T4G-972 del 02/10 (el GPS no lo ve en Vitapro: ¿no fue?); BFK-729 con TDX-994 del
      15/10 (descarga completa, el GPS no ve la carga); BPS-816 → C4Z-745 del 15/10 (la descarga se corta después
      del CEBAF del 16/10).
- [ ] **Cuadro de octubre: 2 cargas sin pedido no se crearon** (AVO-818 del 09/10 y BPT-919 del 02/10). Las 6 del
      20/10 ya se crearon a mano (7341–7346, pedido `PENDIENTE`): al registrar su despacho, la sincronización las adopta.
- [ ] **CPIC del pedido 4400133879**: su descarga (despachos 820–825 del 21/10) se registró sin CPIC; cuando
      llegue, agregarlo al lote en ListaDespachos (editar lote → CPIC).
- [x] ~~**Planta de la descarga 4400133879 = Milagro**~~ (2026-10-22): la lista del 22/10 manda T0L-981 (viaje 7095) a
      Jave; registrada en DestinoDescarga, el viaje pasó a JAVE. La lista ahora manda sobre el despacho y el GPS sobre
      los dos al llegar (sección 3, "desvíos Jave / Inbalnor").
- [ ] **Publicar el cambio del GPS de los desvíos** (`SeguimientoExportacionGpsService`: la planta donde entra la
      unidad reemplaza la bodega ya puesta). Los dos SPs ya están en sgvActualizada; hasta publicar, si una unidad
      se desvía sin que se cargue la lista, el sitio sigue mostrando la planta anterior.
- [ ] **Planta del despacho de descarga vs lista de destinos**: el despacho de descarga del pedido 4400133879 (820–825)
      y del 4400133881 (874–879) sigue con planta Milagro aunque 5 unidades van a Jave; el seguimiento ya está bien.
      Decidir si la lista también debe cambiar la planta del despacho (ListaDespachos agrupa el lote por planta y
      la liquidación podría usarla).
- [ ] **Desvío avisado tarde**: si la lista llega cuando la unidad ya entró a la planta, el SP no la toca (manda el
      GPS). Confirmar con transporte si hace falta avisar en el tablero cuando la lista y el GPS no coinciden.
- [ ] **Despacho en dos momentos: probar en el navegador** la vista previa (select2, avisos) y "Completa el
      despacho" al leer el ZIP. "Programar despachos" ya se usó por HTTP el 2026-10-21 con el Word del 20/10
      (11 cargas del 21/10 en sgvActualizada).
- [ ] **JUSTO CESPEDES (ANU-794 / T3Y-995, 05:40 del 21/10) sin programar**: no está en el catálogo de
      conductores y su DNI no figura en ningún documento (solo el nombre corto en los Word del 18–20/10; maneja
      ANU-794 desde el 18/10). Con su DNI y nombre completo: registrarlo en RegistroChoferes y volver a subir el
      Word del 20/10 en RegistroDespacho (las otras 11 salen "Ya programado"; se programa solo la suya).
- [ ] **Completar las 10 cargas del 22/10 con el correo** cuando llegue (despachos 880–889, viajes del
      seguimiento 7403–7412 con pedido PENDIENTE; programadas con el Word del 21/10).
- [ ] **Jefes de convoy**: el WhatsApp del Word del 21/10 nombra dos por pedido (4400133950: Mercedes
      Chapoñan y Miguel Rivas; 4400133952: Adrián Hidalgo y Juan Ortiz). El sistema no tiene dónde guardarlo:
      decidir con la administradora si hace falta un campo en el lote de descarga o en el seguimiento.
- [ ] **Sección GUAYAQUIL-AZÚCAR del Word**: desde 2026-10-21 completa el viaje del seguimiento de Crisar
      (lugar de entrega y el relevo como tracto y chofer de descarga, "Registrar entregas"). Falta decidir si
      además se registra como despacho de descarga (factura que manda el chofer).
- [ ] **Seguir los 7 viajes de Crisar del 19/10** (seguimiento 7466–7472, primeros de terceros): hoy "UNIDAD
      CARGADA EN BASE SULLANA"; los tractos de descarga del Word del 21/10 salen con las carretas el 22/10.
      Comparar el STATUS y el "Excel STATUS EXTRA" con el Excel de la administradora y ver el detalle
      (SeguimientoViaje) de uno en el navegador. Los de GUAYAQUIL-AZÚCAR del Word del 19/10 (carretas T6P-989,
      T0Q-991, TAN-976, TDN-986, TBE-995) no tienen viaje: su carga es la sección AZÚCAR-CARTAVIO del Word del
      **14/10** (cargaron el 15/10; el Word ya está en la PC de la oficina). El GPS (2026-10-22) los ve cruzar el
      20/10, dormir en el parqueo de Naranjal, descargar en La Troncal el 21/10 (2–9 h) y volver a la base el 22/10.
      **Hecho el 22/10** (con el visto bueno del usuario, por los mismos servicios de la pantalla): despachos
      1151–1155 (Crisar, Cartavio, 15/10) y entregas del Word del 19/10 → viajes 7524–7528 con su relevo; el GPS
      ya los marcó (11–12 hitos): 3 descargados en su totalidad y 2 retornando a base.
- [ ] **Terceros: viajes ya en curso sin despacho**: los de antes de registrar a los clientes no tienen despacho
      de carga, así que no entran al seguimiento; si se quieren, programarlos con su Word (fecha de carga de hasta
      7 días atrás) o crearlos a mano.
- [ ] **TALSA, Rapel y destinos nuevos de Polinplast** sin geocerca: el GPS busca la entrega entre los destinos
      conocidos; uno nuevo queda "Sin lugar de entrega" hasta agregar su geocerca en `GeocercasTerceros`.
- [ ] **Registrar en el catálogo los clientes CRISAR y POLINPLAST y las plantas nacionales Cartavio y
      Chapaira** (RegistroClientes / RegistroPlantas) para que "Programar con el Word" los proponga solos en
      las secciones de terceros (hoy avisa y deja elegirlos). Luego probar en el navegador el selector "Sección
      del Word" con un Word que traiga Vitapro y azúcar (p. ej. el del 18/10).
- [ ] **Geocercas de terceros: dibujarlas sobre el terreno** (hoy círculos en `GeocercasTerceros`, sacados de
      las paradas del GPS) antes de usarlas para marcar horas, y comprobarlas con casos marcados por la
      administradora. (-3.536, -80.184 ya se sabe que es TCI y -8.1386, -79.0176 un 2.º punto de Novopan.)
- [ ] **Preguntas de terceros**: Rapel (la agrícola Rapel está en Piura, pero el GPS lleva esos viajes a Jayanca:
      ¿otro fundo o un packing?) y dónde se entrega a TALSA (agroindustrial del Grupo Rocío, campos en Virú, Chao
      y Zaña); la guía de Polinplast (Rosmery); cómo se registra el falso flete. Resueltos con GPS y web:
      ACP = Agrícola Cerro Prieto (Pacanga, Chepén) y la cochera de Trujillo (estacionamiento de la calle Fundo
      Larrea, -8.1386, -79.0176; `docs/CLIENTES_TERCEROS.md` §4.3 y §6).
- [ ] **Completar las 11 cargas del 21/10 con el correo** cuando llegue (despachos 809–819, viajes del
      seguimiento 7371–7381 con pedido PENDIENTE): primera prueba real de "completar con el correo".
      5 de ellas se sumaron al viaje abierto de su chofer (VP 342, 343, 345, 347, 349), con la misma regla de la
      importación: revisar al liquidar que sea lo esperado.
- [ ] **Descarga de una carga que todavía no tiene documentos**: `DescargaPedidoService` exige pedido y detalle
      de carga, así que la descarga a Ecuador se registra después de completar la carga. Si el correo se
      retrasa más que la descarga, el tracto 2 del seguimiento se pone a mano en el detalle del viaje.
- [ ] **Cambio de chofer con varias guías sin unir**: el descarte solo une si queda una guía y un programado;
      con más, se eligen a mano. Se podría proponer con el GPS (qué tracto programado estuvo en la planta a la
      hora de la guía).
- [ ] **Programado editado a mano**: si en ListaDespachos se le pone pedido o guía a un despacho programado,
      ya no aparece como "esperando documentos" y el correo crearía otro despacho. Revisar si hace falta avisarlo.
- [ ] **Hora de la programación en el seguimiento**: el despacho programado guarda la hora del Word
      (`horaDespacho`), pero `sp_SE_SincronizarDespachos` sigue pasando solo la fecha a F.H. Programación
      (los despachos del lote guardan en `horaDespacho` la hora del registro, no la programada).
- [ ] **Mapa de la ruta: teselas de OpenStreetMap** (`tile.openstreetmap.org`) sirven para poco tráfico; si el
      sistema crece, pasar a un proveedor con clave (CARTO, MapTiler). CARTO ya pide clave.
- [ ] **Trazabilidad: revisar las 10 diferencias de más de 1 h de la prueba de septiembre** (8 en el Complex:
      el GPS ve la unidad en la cola u horas antes o después que el Excel) y las 2 plantas cruzadas (TBM-898 →
      CBU-945 y BSD-716 → T7J-861 del 07/09: el GPS las ve en la planta contraria a la del Excel; probable fila
      invertida en el Excel). Hay que mirarlas con la administradora.

- [ ] **Seguimiento: probar en el navegador lo que escribe** (corregir una hora, guardar los datos que el GPS
      no sabe, editar datos del viaje, nuevo viaje, cerrar/reabrir, "Actualizar GPS") y los paneles de
      evidencia/corrección (JS). Por HTTP solo se probó lo que lee; las escrituras, con la prueba de
      integración `SeguimientoViajesBdTests` (rollback). No había navegador conectado el 2026-10-20.
- [ ] **Seguimiento: guardar la hora de la programación del Word en el lote con documentos** (columna HORARIO):
      al programar con el Word ya se guarda (ver "Hora de la programación en el seguimiento"); en el lote que se
      arma con ZIP + Word todavía se guarda la hora del registro.
- [ ] **Seguimiento: crear también el viaje al finalizar el lote** (al programar con el Word ya se crea; el
      lote con documentos sigue esperando a que se abra el tablero, con `sp_SE_SincronizarDespachos`).
- [ ] **Seguimiento: confirmar que cada despacho de descarga en Ecuador se enlaza a su carga** (`idDespachoCarga`).
      Si no, la sincronización lo une por pedido y carreta; un despacho de descarga sin carreta o con otra
      carreta no completa el viaje.
- [ ] **Seguimiento, etapa 4: actualización automática del GPS** sin clic: una URL protegida con clave que
      llama algo externo cada 15–30 min (cron-job.org o el Programador de tareas de la PC de la oficina; somee
      apaga la aplicación sin uso, así que un temporizador interno no sirve) y en el tablero "GPS actualizado
      hace X min", con una consulta liviana a la BD para recargar solo si hubo cambios. El historial por tracto
      y día ya no se vuelve a pedir (`HistorialGpsCache`, 2026-10-21).
- [ ] **Seguimiento: cortar la ventana de la descarga cuando el viaje ya volvió a base** (hoy se leen hasta 12
      días desde la salida de planta; con la caché cuesta poco, pero sigue leyendo días de más).
- [ ] **Seguimiento: medir "Actualizar GPS" en el sitio con los viajes abiertos** (cuántos viajes alcanza por
      clic con la caché). Solo se midió la caché sola con T7J-860 (8 días: 16 s la primera vez, 5,2 s después).
- [ ] **Seguimiento: espacio de `~/App_Data/HistorialGps` en somee** (3–45 KB por tracto y día, se borra a los
      30 días): revisar la cuota tras unas semanas de uso.
- [ ] **Borrar los `sp_SE_*` que dejó de usar la pantalla vieja**: `sp_SE_ListarEnCurso`, `sp_SE_Insertar`,
      `sp_SE_Listar`, `sp_SE_ObtenerPorId`, `sp_SE_BuscarDespachosDisponibles`, `sp_SE_ObtenerDespachoPorId`
      (ya no se llaman desde C#; revisar antes que no los use otra cosa, con respaldo y migración).
- [ ] **Seguimiento: vincular el despacho** (`idDespachoOrigen`/`idDespachoDestino`): la pantalla vieja tenía
      el buscador de despachos; la nueva todavía no lo muestra (las columnas siguen y el GPS las usa).

- [ ] **Trazabilidad: qué escribir en F.H.LL. Base cuando la unidad pasa directo** sin entrar a la base. El
      motor ya lo detecta (`PasoDirectoBase`) y "Verificar GPS" lo avisa, pero no llena la columna.
- [ ] **Trazabilidad: más casos reales** de la administradora (pasa directo, entra y sale de inmediato,
      duerme en base, cambio de tracto en base) para el banco de `WebSGV.Tests/Datos/Trazabilidad`.
- [ ] **Trazabilidad: más casos de descarga en Ecuador** (hoy dos: BFI-922 a Jave y BSD-716 a Inbalnor
      con paso directo por TCI); falta uno en que la unidad **duerma en TCI** hasta el otro día y uno que no
      pase por el Complex, si existe.
- [ ] **Trazabilidad: tabla `PuntoControlGps` de exportación sin uso.** "Verificar GPS" ya no usa sus 20
      puntos (`BASE_*`, `TRUJILLO_*`, `BODEGA_NACIONAL_*`, `CEBAF_LLEGADA`, `CRUCE_ECUADOR`, `TCI_*`, Jave e
      Inbalnor). Revisar si `HoraLlegadaGpsService` usa otros puntos de esa tabla antes de borrarlos.
- [ ] **Trazabilidad: geocercas en BD con editor en mapa** (hoy en código, `GeocercasTrazabilidad`), cuando
      haya más zonas que las de Sullana y Trujillo.
- [ ] **Trazabilidad: unir los tramos por la carreta** con el Word de programación (columna CAMBIO: qué
      tracto lleva la carreta en la carga y cuál en la descarga) para armar el viaje completo sin escribir
      los tractos a mano.
- [ ] **GPS: revisar el `OnwayAuth0ClientSecret` en cada máquina** contra el correo oficial de Location World
      (credenciales del Customer API). En una máquina le faltaba un carácter (63 de 64) y Auth0 respondía 401;
      se corrigió el 2026-10-19. Recordar: **un solo token cada 24 h**; vive en `OnwayAuthCache` de la BD de
      pruebas y lo reusan la web y `herramientas/reporte-gps`.
- [ ] **Reporte GPS, fase 2: status sugerido por unidad** (dónde está y si va cargada o vacía, para la
      columna STATUS del Google Sheets "PROGRAMACION Y STATUS VITAPRO"), desde la última posición y los
      puntos de control. Primero afinar las reglas con casos reales de la administradora de transporte.
- [ ] **Destino de descarga: probar con el Excel real** que manda el cliente a la administradora de transporte
      (se espera el 2026-10-19). Hoy el lector se armó con una captura (columnas Fecha, Pedido, Unidad,
      Placa Camión "tracto - carreta", Empresa, Lugar de recepción); ajustarlo si el Excel real difiere.
- [ ] **Destino de descarga: pedir a quien envía la lista que la mande en Excel**, no en imagen por WhatsApp
      (decisión del usuario: se le hablará a la administradora/remitente).
- [ ] **Destino de descarga: probar en el navegador la descarga por pedido con un pedido real** repartido
      entre Milagro y Jave (no había cargas pendientes en pruebas; solo se probó por HTTP la pantalla nueva
      y la carga de RegistroDespacho). Verificar que el lote se registre con una planta por tracto y que
      `ListaDespachos` lo muestre como dos lotes (agrupa por planta).
- [x] ~~**Destino de descarga: ¿el destino cambia después de enviada la lista (desvío en ruta)?**~~ (2026-10-22):
      sí, el cliente manda la lista con la unidad ya en Ecuador; ahora cambia la bodega del viaje hasta que llega
      (ver "Desvío avisado tarde" en esta lista para el caso en que llega después).
- [ ] **Destino de descarga: la lista también podría decir la fecha de recepción en bodega** (se guarda en
      `fechaRecepcion`, hoy no se usa en ninguna pantalla).
- [ ] **Conectar la extensión Claude in Chrome en la PC de casa** (en la oficina ya está, en Edge). Pasos
      en `herramientas/sitio-local/README.md`.
- [ ] **Alias de choferes** (recordar la corrección del usuario cuando elige a mano un nombre del Word que no
      se reconoció). Requiere migración (tabla de alias por conductor). Hoy no hace falta: 64/64 nombres
      reales se reconocen.
- [ ] **Otros clientes del Word** (Polinplast, Cartavio): fuera hasta mapear sus facturas y su forma de
      trabajo. Hoy sus secciones se ignoran.
- [ ] **Prueba de punta a punta con un pedido real** (correo + Word → carga → descarga → viaje →
      liquidación y firma → aprobación → PDF → contabilidad), en pruebas.
- [ ] **Dashboards / indicadores**: el usuario tiene temas que comentar antes (al final).

- [x] ~~Decisión de la empresa: conductor duplicado DERLIS RICARDO CEDEÑO CHOURIO~~ (2026-10-18): **ya no
      trabaja en la empresa desde ~agosto 2026**; sus dos registros (DNI `00271627` y `02716271`) están
      inactivos en pruebas, que es lo correcto. Nada que reasignar; solo falta que en producción también
      queden inactivos (la lista de choferes actuales del paso 7 de la sección 6 lo hace).
- [ ] **Alinear `PdfAbastecimientoService`** al estilo del nuevo PDF de la Orden de Viaje (fuera de alcance
      mientras Grifo siga fuera; ver memoria de alcance).
- [ ] **Serilog 2.12 → 4.x** (opcional; requiere nuevas dependencias y binding redirects).

### Hechas

- [x] **Clientes terceros registrados y primeras cargas programadas** (2026-10-21, pedido del usuario, después de
      publicar el sitio): POLINPLAST S.A.C. (RUC 20505520638, datos de su guía T700-00001683) y CRISAR LOGISTICA
      S.A.C. (RUC 20603930844, datos de la web), plantas nacionales Cartavio y Chapaira (con auditoría). Con el
      sitio local: las 7 cargas de azúcar del Word del 18/10 (despachos 1039–1045, carga 19/10, seguimiento
      7466–7472) y sus 7 entregas con relevo del Word del 21/10 ("Registrar entregas"). La primera consulta al
      GPS tomó para el tracto de descarga el TCI y La Troncal de su viaje anterior: corregido (con relevo, la
      descarga empieza cuando ese tracto sale de la base con la carreta; test) y horas borradas con
      `Database/Scripts/corregir_seguimiento_crisar_2026-10-21.sql`. Publicado (2.ª vez) el 21/10.
- [x] **Seguimiento de terceros validado con 110 viajes reales** (2026-10-22, pedido del usuario): los viajes de
      Crisar, Novopan y Polinplast del Excel STATUS EXTRA del 15/09 al 20/10, pasados por `TrazabilidadTercerosService`
      con su GPS (sin escribir en la BD). STATUS igual al Excel: Crisar 65/71, Polinplast 19/20, Novopan 19/19; el
      destino deducido por el GPS coincide en todos. Correcciones que salieron: Novopan carga aunque la unidad ya
      esté en Quito antes de la fecha (el Excel pone el día siguiente); el azúcar de Crisar ("GUAYAQUIL") también se
      descarga en **Jave** e **Inbalnor**, no solo en La Troncal; la carreta puede esperar hasta 6 días en la base al
      tracto de relevo; Crisar sin tracto de descarga asignado se queda "en base" (el de carga puede irse con otra
      carreta) y "Registrar entregas" asigna el tracto de descarga también sin relevo; Chao tiene 3 puntos (fundos de
      Hortifrut); la cochera cuenta solo con 20 h o más (si no, "EN SU TOTALIDAD"). Las 7 diferencias que quedan:
      BFR-730 sin GPS en Onway (3), ARN-917 sin señal desde el 19/09, 2 STATUS viejos del Excel y una fila con el
      cliente cambiado. 5 tests. Hay que volver a publicar.
- [ ] **BFR-730 no tiene equipo GPS en Onway** (o tiene otra placa): sus relevos de Crisar no se pueden seguir.
      Revisar con quien administra Onway.
- [x] **Mapa de la ruta: "saltos"** (2026-10-22, pedido del usuario con el viaje 7472): (1) en los viajes de
      terceros con relevo el tramo de descarga se dibujaba desde la salida de Cartavio con el tracto de descarga,
      o sea su viaje anterior (AVO-818 de Talara a Guayaquil); ahora la carga va hasta la base y la descarga
      empieza cuando ese tracto sale de la base con la carreta. (2) El GPS reporta cada minuto, sin huecos, pero
      a veces marca un punto suelto lejos de la ruta que vuelve al instante (AVO-818 20/10 04:39: 3,5 km en 6 s):
      `RutaViajeService.SinSaltos` descarta los puntos a los que habría que llegar a más de 160 km/h (para todos los
      viajes, también Vitapro). 2 tests. Hay que volver a publicar.
- [x] **Seguimiento y STATUS EXTRA de los clientes terceros** (2026-10-21, pedido del usuario): migración 34,
      `sp_SE_SincronizarDespachos`, `sp_SE_RegistrarEntregaTercero`, `RutaTercero`, `TrazabilidadTercerosService`,
      "Registrar entregas" en RegistroDespacho, filtro Cliente y "Excel STATUS EXTRA" en el tablero. Ver la fila
      de la sección 3.
- [x] **Clientes terceros: análisis, programar con el Word y geocercas** (2026-10-21): análisis del Excel
      STATUS EXTRA cruzado con los 222 Word y con los viajes de Vitapro (`docs/CLIENTES_TERCEROS.md`);
      "Programar con el Word" reconoce las secciones de carga de Crisar, Novopan y Polinplast (`RutaProgramacion`)
      con selector de sección; 16 geocercas de terceros desde las paradas reales del GPS de 39 viajes
      (`GeocercasTerceros`, con la cochera de Trujillo y el grifo de espera). Ver la fila de la sección 3.
- [x] **Tablero del seguimiento con las cargas de mañana y formulario manual de RegistroDespacho plegado**
      (2026-10-21, pedido del usuario): ver la fila de la sección 3. Probado por HTTP en el sitio local (10 cargas
      del 22/10 arriba del tablero; formulario plegado); falta verlo en el navegador.
- [x] **Word del 21/10 subido a mano por el usuario en RegistroDespacho** (2026-10-21): 10 cargas del 22/10
      programadas (despachos 880–889, viajes 7403–7412; JUAN ALBERTO APONTE GRANDA, que estaba inactivo, en
      BNH-795) y descargas a Guayaquil de 4400133950 (despachos 890–895) y 4400133952 (896–901) con la
      "Programación del día": las 12 enlazadas a su carga y con tracto 2 en el seguimiento (7371–7381 y 7401).
- [x] **Documentos del correo del 21/10 importados** (2026-10-21): ZIP 4400133950 (6 guías, F222-00008539) y 4400133952      (6 guías, F222-00008541) como dos lotes, cada guía completó su despacho programado y los 12 viajes del seguimiento      tomaron su pedido. La guía de JULIO FLORES PARDO / P1Y-906 se asignó al despacho de VICTOR MARZAL / BNH-735 (cambio de      chofer). Se aprobó el producto nuevo NICOVITA KATAL CAMARON 35% 1.2 EX (código 7530977, id 62). Avisos de la guía      T660-00016548 (C6K-799): dice 330 bolsas y 8 283 kg y el packing list 1 188 bolsas y 29 818,8 kg (lotes distintos): es el      documento, revisar con quien lo emitió. Guía T660-00016547 repite el N° de transporte 1296281.
- [x] **Pedido 4400133881 (carga del 20/10) importado** (2026-10-21): la factura F222-00008535 y las 6 guías del correo no estaban
      asociadas a nada (el Word del 19/10 solo había programado las 6 primeras placas). Se programaron ASU-862, TBM-815, CBU-928,
      BPT-788 y BPS-816 con `DESPACHO 19-10-26.docx` y se importó el ZIP: despachos 868–872 completados y adoptados por los viajes
      7342–7346. Quedó el despacho 873 (guía T660-00016530, chofer PIEDRA VEGA DANILO, tracto T7J-861) sin viaje.
- [ ] **Guía T660-00016530 del pedido 4400133881**: dice chofer PIEDRA VEGA DANILO y tracto T7J-861, pero la administradora tiene
      HUGO FLORES / BNG-758 (viaje manual 7341, pedido `PENDIENTE`). Confirmar con quien emitió la guía y enlazar el despacho 873
      con el viaje 7341 (o corregir el chofer/tracto).
- [x] **ANU-794, carga del 21/10** (2026-10-21): conductor JUSTO CESPEDES VASQUEZ registrado (id 143, DNI 03673697,      licencia B03673697), despacho 867 programado con el Word `DESPACHO 20-10-26.docx` y adoptado por el viaje 7401.      Sigue con pedido `PENDIENTE` hasta que llegue el ZIP del correo.
- [x] **Seguimiento: unidades en la cola de Trujillo seguían "Rumbo a Trujillo", viaje 7381 con las horas del anterior,
      salida falsa de la bodega en Ecuador y "Actualizar GPS" por partes** (2026-10-21): ver la fila de la sección 3.
      Revisadas las 42 placas del monitoreo de la administradora. Con `corregir_salidas_bodega_ecuador_2026-10-21.sql`
      se borraron 32 salidas falsas en sgvActualizada y se volvieron a detectar: los 21 viajes ya cerrados quedaron
      con 4–17 h en Jave (antes 0–29 min) y los 11 del 21/10 sin salida (siguen en el patio).
- [x] **Despacho en dos momentos** (2026-10-21): el Word programa los despachos de carga y el correo los completa;
      la liquidación no espera a los documentos. Resuelve "decidir si el tablero crea los viajes desde el Word"
      (el viaje sale del despacho programado, con pedido `PENDIENTE`). Ver la fila de la sección 3.
- [x] **"Actualizar GPS" más rápido sin cambiar la detección** (2026-10-21): ver la fila de la sección 3.

- [x] **Seguimiento: unidades que no se veían o salían en otro lado** (2026-10-20, vuelta corrida de ASU-862 y
      TBM-815, 6 cargas del 20/10 sin viaje): ver la fila de la sección 3. Las 30 unidades del monitoreo coinciden.
      En el viaje 7342 (ASU-862) se borró la salida de planta 15:01:34 que el GPS grabó antes del arreglo del borde
      de la cerca (seguía parado en la puerta); queda "En planta".
- [x] **Seguimiento: unidades en camino seguían "en base"** (2026-10-20, caso BFH-852): ver la fila de la sección 3.
      Los 8 viajes del 19/10 y el CBU-945 del 20/10 quedaron actualizados en sgvActualizada.
- [x] **`WebSGV.csproj` registraba `sp_InsertarAbastecimientoCombustible_ALTER.sql`, borrado en `f96f145`**: la
      publicación fallaba al empaquetar. Ahora registra `sp_InsertarAbastecimientoCombustible.sql`.
- [x] **Cuadro STATUS GENERAL de octubre desde los Word y el GPS, cargado en el seguimiento** (2026-10-20): ver
      la fila de la sección 3. 103 viajes MANUAL nuevos en sgvActualizada (la BD del sitio publicado).
- [x] **Mapa de la ruta del viaje** (carga y descarga con su línea, hitos, geocercas y reproducción) en el
      detalle del seguimiento (2026-10-20): ver la fila de la sección 3.

- [x] **Prueba histórica de la trazabilidad con septiembre** (2026-10-20): 78 viajes de Vitapro del 7 al 18/09
      armados solo con las programaciones en Word (`docs/DOCUMENTACION/Despachos Ene-Set 2026`; carga en
      Trujillo + descarga en Guayaquil unidas por la carreta y la columna CAMBIO: en los 72 con descarga el
      tracto coincide con el Excel) y comparados con el Excel STATUS GENERAL: de 908 horas, 80 % a ±2 min,
      97 % a ±10 min, 1 % a más de 1 h. Se corrigió la descarga: el CEBAF y el cruce se pasan de noche entre
      dos puntos del GPS sin ninguno adentro (ahora se estima el paso entre los dos puntos) y, sin ancla, el
      resto del viaje se tomaba del viaje siguiente (ahora CEBAF, cruce y TCI se buscan solo entre el Complex
      y la planta de descarga; caso T7J-861 en el banco de pruebas). Decisión: la llegada a la planta en
      Ecuador es la del GPS (orilla de la carretera); en septiembre el Excel de Jave la anotaba ~4 min después,
      en el camino de servicio. Prueba con octubre: los 10 viajes del despacho del 10/10 (pedido 4400133436)
      salen completos (17 de 17 hitos, 5 a Jave y 5 a Inbalnor) y la carga de ANU-794 del 19/10 coincide con las
      horas marcadas a mano. Se corrigió que el tracto de descarga, volviendo de Ecuador de su viaje anterior,
      pasara por el CEBAF y se tomara como hito de este viaje: la descarga empieza recién cuando el tracto sale
      de la base después de la carga.

- [x] **Los viajes del seguimiento nacen de los despachos** (2026-10-20, decisión del usuario: lo importado del
      Excel ya pasó y no sirve para seguir los viajes de ahora). `sp_SE_SincronizarDespachos` crea un viaje por
      cada despacho de carga de exportación y lo completa con su despacho de descarga en Ecuador (tracto, chofer y
      planta Jave/Inbalnor desde el despacho o la lista de destinos); el despacho manda en pedido, carreta,
      tractos y choferes (en el detalle quedan de solo lectura) y uno anulado deja el viaje anulado. El tablero
      muestra solo los de despacho y los creados a mano; los importados del Excel (origen NULL), con la casilla
      "Incluir los importados del Excel". Migración 32 (`origen`). Al abrir el tablero en pruebas se crearon
      los 10 viajes del pedido 4400133436 (10/10) con sus tractos de carga y descarga emparejados.
- [x] **Pantalla nueva de seguimiento de exportación** (2026-10-20): tablero, detalle del viaje e importación
      aparte; ver la fila de la sección 3. Para publicarla: la migración 31 y los 5 `sp_SE_*` nuevos ya están
      en `sgvActualizada` (la BD que usa el sitio publicado); solo falta publicar el sitio.

- [x] **Trazabilidad GPS, tramos de carga (Base → Trujillo → Base) y descarga (Base → Complex → CEBAF → TCI → Jave/Inbalnor → Base)** en "Verificar GPS" (2026-10-19): ver la
      fila de la sección 3. Para sumar un caso: `herramientas/reporte-gps/Guardar-HistorialCaso.ps1 -Placa X
      -Desde ... -Hasta ... -Salida WebSGV.Tests\Datos\Trazabilidad\X_fecha.json` y agregarlo a `casos.json`.
      Decisión del 2026-10-19: **F.H.S Planta = cuando sale por la puerta de la planta**, aunque luego se
      quede unos minutos parado en la avenida (AMQ-859 del 03/10: puerta 17:51, se va 17:56), como anota el
      Excel STATUS GENERAL; la diferencia de minutos no afecta.
- [x] **Borrar procedimientos de legado sin uso (40).** Hecho el 2026-10-08 con la migración
      `Schema/17_BorrarProcedimientosLegado.sql` (aplicada en pruebas: 140 → 100 procedimientos).
      Respaldo en `Database/Scripts/respaldo_procedimientos_legado_2026-10-08.sql`. También se borró
      `Database/Despliegue_Exportacion.sql` (obsoleto: recreaba la versión vieja de `sp_SE_Listar` y
      apuntaba a producción). Lista:
      `ActualizarTipoViaje, ActualizarTipoViajeAutomatico, CrearCPICTemporal, GenerarNumeroOrdenViaje,
      InsertarDetalleSegmento, InsertarEgresos, InsertarGastoAdicional, InsertarIngresoAdicional,
      InsertarIngresos, InsertarLiquidacionCompleta, InsertarSegmentoOrdenViaje,
      InsertarSegmentoOrdenViaje_Legacy, InsertarTracto, ObtenerEstadisticasLiquidaciones,
      ObtenerLiquidacionesPorOrden, ObtenerPlantasCargaPorCliente, ObtenerPlantasDescargaPorCliente,
      ObtenerSegmentosOrden, sp_ActualizarAbastecimientoCombustible, sp_ActualizarFactura,
      sp_ActualizarIndicador, sp_BuscarIndicadorPorNumeroPedido, sp_GenerarReporteFinanciero_BalanceGeneral,
      sp_InsertarDespacho, sp_InsertarFactura, sp_InsertarOrdenViaje, sp_ObtenerDespachosViajeActivo,
      sp_ObtenerHistorialLiquidacionesConductor, sp_ObtenerObservacionesRechazo,
      sp_ObtenerViajeActivoConductor, sp_ObtenerViajesActivosParaGrifo, sp_PruebaDespacho,
      sp_RegistrarCPIC, sp_ReporteRendimientoPorRuta, sp_SE_Dashboard_Mensual, sp_SE_Eliminar,
      sp_SE_GridListar, ValidarPlantaCliente, ValidarSegmentosOrden, ValidarUnicidadGuias`.
- [x] **Borrar 2 procedimientos más sin uso**: `InsertarOperacionSubTramo` e
      `InsertarSegmentoOrdenViajeConGuias` (estaban en la BD pero no en el repo). Hecho el 2026-10-08 con
      la migración `Schema/18_BorrarProcedimientosLegado2.sql`; respaldo en
      `Database/Scripts/respaldo_procedimientos_legado_2_2026-10-08.sql`. La BD de pruebas queda con
      exactamente los 98 procedimientos del repo.
- [x] **"Verificar GPS" contra el API real de Onway — funciona en pruebas (2026-10-09).** El 401 de
      Auth0 era el *Client Secret*: había cambiado (el Client ID es el mismo). Se actualizó en
      `appSettings.Secrets.config` de pruebas (gitignored). **En producción también hay que poner el
      secreto nuevo** antes de usar el GPS. Recordar: Location World permite **un solo token activo por
      client_id**; pruebas y producción comparten ese cupo de 24 h.
      Además: "Verificar GPS" ya no toma hitos de otro viaje del mismo tracto (cada hito se busca entre el
      hito anterior y el siguiente ya registrado) y pasó de ~7 min a ~10 s (páginas de 1 000 puntos de
      historial en vez de 50). La bandeja de viajes en curso ordena por F.H. Programación y busca por
      placa en BD.
- [x] ~~Partir `Views/Reportes.aspx.cs`~~ → **descartado: la página se eliminó** (2026-10-08). "Reportes
      Avanzados" ya no se usaba (sin enlace en el menú desde `144ee1e`). Se borraron `Reportes.aspx`,
      `ReporteResultado.aspx`, `ReportesService`, `ReporteExcelBuilder`, `ReporteFiltros` y sus tests, y
      sus 19 procedimientos con la migración `Schema/19_BorrarProcedimientosReportes.sql` (los archivos
      quedan en el historial de git, commit `5414eca`). Los reportes en uso son `ReportesOrdenesViaje.aspx`
      y `ReporteAbastecimiento.aspx`.
- [x] **Sacar el JavaScript y CSS en línea a archivos** — hecho el 2026-10-08: ~8 000 líneas de JS y
      ~10 000 de CSS de 41 páginas pasaron a 37 `.js` en `Scripts/paginas/` y 34 `.css` en
      `Content/paginas/` (las `.aspx` bajaron ~18 000 líneas). Los `<%= control.ClientID %>` pasan al
      objeto `SGV` declarado en la página; URLs con versión vía `RecursoHelper.Url`. Verificado: sintaxis
      de cada `.js` (node --check), precompilación de todas las páginas (aspnet_compiler) y tests.
      Bloques de menos de 10 líneas y `Error*.aspx` quedan en línea. Convención en `CLAUDE.md`.
- [x] **`FirmarLiquidacion.aspx` unificada a Bootstrap 4.6.2** (2026-10-08): usa las mismas librerías
      y versiones que `Site.Master` (CDN). Además se corrigió Font Awesome, que apuntaba a
      `Content/fontawesome/` (no existía: los iconos de la firma no se veían). Fuera los paquetes NuGet
      `bootstrap 5.2.3` y `jQuery 3.7.0` (sus 51 archivos locales ya no los usaba nadie) y `Bundle.config`.
      Todo el sitio queda en Bootstrap 4.6.2 + jQuery 3.6.4. Migrar a Bootstrap 5 sería un proyecto
      aparte (41 páginas, revisión visual completa).
- [x] **`Empresa.Web`** (sale impreso en los PDF) pasó de `www.serviciosgviviana.somee.com`, que no
      respondía, a `sgvtransporte.somee.com` (2026-10-11). `LiquidacionesPendientes.js` ya no la tiene
      escrita a mano: la toma de `Web.config` vía `SGV.empresaWeb`.
- [x] **Confirmar el CI en verde** en GitHub → Actions: verificado el 2026-10-11, todos los commits
      hasta `2e4c017` pasan.
- [x] **Tests de integración que escriben** (2026-10-11): `Integracion/FlujoDespachoLiquidacionTests.cs`,
      9 tests que recorren despacho → liquidación → firma → aprobación con ajustes / rechazo y
      reliquidación con los mismos servicios de las páginas, dentro de `TransaccionDePrueba`
      (`TransactionScope` sin `Complete()`: no dejan datos; verificado en la BD). La firma se registra con
      su SP (`FirmaService` escribe el PDF en disco). Los casos de error van al final de cada test: los SP
      que hacen `ROLLBACK` en su `CATCH` anulan también la transacción del test.
- [x] **Bug encontrado por los tests (corregido el 2026-10-11):** `ViajesEnProgreso.esInternacional`
      nunca se llenaba (los 12 viajes de pruebas estaban en NULL). Efectos: en `DashboardConductor` ningún
      viaje contaba como internacional, así que **se ocultaban los peajes extranjeros en la liquidación**;
      el historial decía siempre NACIONAL; y `RegistroDespacho` podía mezclar ámbitos en un mismo viaje.
      Ahora `sp_CrearDespacho` lo fija con cada despacho (basta uno internacional; un viaje creado a mano
      queda en NULL hasta su primer despacho) y la migración `23_ViajesEnProgresoEsInternacional.sql`
      rellena los existentes. Aplicado en pruebas (SP + migración); en producción entra con los pasos 1 y 2
      de la sección 6.
- [x] **Revisión en navegador por rol (2026-10-12): Conductor, Administrador de Transporte, Contabilidad y
      Administrador de Sistema.** Corregido: CSS globales con `?v=` (se veían estilos viejos tras desplegar);
      `DashboardOperador` ya no cierra la sesión de otros roles (`SecurityHelper.ExigirRolOperador`); numeración
      de filas de la liquidación sin saltos; `form-select` y otras clases de Bootstrap 5 con compatibilidad en
      `sgv-base.css`; `confirm()` nativo reemplazado por el modal `SGV.confirmar` (y un interceptor para los
      `OnClientClick="return confirm('...')"` simples); GestionUsuarios normaliza el rol, valida el formato del
      usuario, registra el id del usuario nuevo en la auditoría y pide confirmación con el nombre y el efecto.
      Migraciones **24** (rol en MAYÚSCULAS: 12 conductores estaban en minúscula) y **25** ("Ð" por "Ñ" en 5
      conductores y 5 usuarios), aplicadas en pruebas; en producción entran con el paso 1 de la sección 6.
      **Pendiente de decisión de la empresa:** el conductor DERLIS RICARDO CEDEÑO CHOURIO está duplicado (DNI
      `00271627` y `02716271`). Quedan sin revisar Operador, Grifo y Maquinaria (fuera de alcance).
- [x] **PDF de la Orden de Viaje (SGV-CDF-F-05) rediseñado** (2026-10-11, `Services/PdfOrdenViajeService.cs`):
      cabe en una página en el caso habitual (antes la firma saltaba sola a la página 2 y el encabezado decía
      "1 de 1"); muestra descuentos, reintegros y saldo final (antes el PDF aprobado no los tenía), las horas
      de llegada (declarada y GPS), los comprobantes y las descripciones del conductor; omite las notas
      internas de rechazo/corrección; conserva la fecha real de firma del conductor al aprobar; y al
      regenerar un PDF faltante incluye la firma. Recibe `DetalleLiquidacion` (sin reflexión) y
      `CrearDocumento` permite probarlo sin BD (`WebSGV.Tests/PdfOrdenViajeTests.cs`). Pendiente: alinear
      `PdfAbastecimientoService` al mismo estilo.
- [x] **Reportes del administrador de transporte rediseñados** (2026-10-11). Las 5 exportaciones de
      `ReportesOrdenesViaje` (liquidaciones Excel/PDF, viajes activos Excel y ahora PDF, personalizado
      Excel/PDF) usan `Services/Reportes/ReporteTabular.cs`: encabezado con empresa, RUC, título, filtros y
      usuario; Excel con fechas y montos como valores, totales `SUBTOTAL` (respetan el filtro), autofiltro,
      fila de títulos fija e impresión horizontal ajustada al ancho; PDF con cabecera de tabla repetida,
      "Página X de Y" y tamaño de letra según la cantidad de columnas. El reporte de liquidaciones muestra
      descuento, reintegro y neto. El botón Imprimir de Liquidaciones Pendientes abre el PDF oficial en vez
      de la copia HTML (350 líneas de JS menos). Tests en `WebSGV.Tests/ReporteTabularTests.cs`.
- [x] **Importación de documentos del pedido en RegistroDespacho** (2026-10-15, `4cf583d`): factura,
      packing list y guías en PDF o ZIP (`Services/Despachos/Importacion/`, texto con PdfPig 0.1.9) arman el
      lote con su carga; productos y conductores nuevos se dan de alta solo tras aprobarlos
      (`sp_RegistrarProductoImportado`, `sp_RegistrarConductorImportado`, `sp_ActualizarLicenciaConductor`).
      Migraciones **26** (`DespachoCarga`, `numeroEntrega`/`numeroTransporte`, `Producto.codigoCliente`,
      `Conductor.numeroLicencia`; **borra `Despachos.idProducto`**), **27** (`destinoCarga`, `esExportacion`)
      y **28** (`Factura.moneda`, existentes = PEN). Aplicado en pruebas. Tests: `LectorDocumentosPedidoTests`,
      `Integracion/ImportacionCargaTests`.
- [x] **Descarga por pedido y Word de programación** (2026-10-15, `6616667`): cada descarga queda enlazada a
      su carga (migración **29**, `Despachos.idDespachoCarga`; `sp_CrearDespacho` con `@idDespachoCarga`); el
      Word de programación de transporte llena chofer, tracto y planta según la carreta (no toca la BD).
      Tests: `ProgramacionDescargaTests`.
- [x] **Lista de choferes actuales en RegistroChoferes** (2026-10-15, `6616667`): "Actualizar con la lista"
      (Excel) con vista previa de altas, licencias, nombres, carnet de extranjería y bajas
      (`Services/Conductor/ListaChoferesService.cs`, `sp_RegistrarConductorImportado` con
      `@carnetExtranjeria`). Aplicada en pruebas con `LISTA DE CHOFERES ACTUALES (corregida).xlsx` (en
      `docs/DOCUMENTACION/`, gitignored). Tests: `ListaChoferesTests`.
- [x] **Word de programación en la importación de CARGA** (2026-10-16, definido con el usuario). El Word que
      la jefa de flota manda por WhatsApp (~19:30, para el día siguiente; el correo llega ~16:30) es el que
      manda: chofer y tracto cambian juntos, la carreta no; la guía NO se reemite. Se selecciona junto con
      el ZIP/PDF del correo en el mismo control. `Services/Despachos/ProgramacionCarga.cs` (lógica pura)
      elige la sección de carga (más tractos en común / nombre del cliente), lee la fecha de "CARGAN 10 DE
      OCTUBRE" y une cada guía con su fila por carreta → tracto → nombre del chofer → descarte 1 a 1.
      Chofer, tracto y carreta salen del Word; la diferencia con la guía es un dato informativo (y queda en
      la auditoría de la importación). Si el chofer cambió, no se ofrece registrar al de la guía ni se le
      copia su licencia. Nombres: exacto → por parecido (aviso para confirmar) → desempate con quien figura
      en la guía y con el último que manejó el tracto (también en la descarga:
      `ProgramacionDescarga.ResolverConductor`). Planta de carga por el título si las guías no la dan.
      Probado con el pedido real 4400133436 + `DESPACHO 09-10-26.docx`: 10/10 guías unidas, carretas
      exactas (antes se adivinaban por historial) y un cambio real detectado (guía T660-00016352 emitida
      con JULIO FLORES PARDO / C4Z-745; sale DANTE CHINCHAY / BSD-900). Sin cambios en la BD (ni migración
      ni SP). Solo Vitapro. Tests: `ProgramacionCargaTests` (14).
      **Probado en el navegador de punta a punta (2026-10-16, Edge + extensión):** se anuló en pruebas el
      pedido 4400133436 mal registrado el 15/10 solo con las guías
      (`Database/Scripts/anular_pedido_4400133436_pruebas_2026-10-16.sql`) y se volvió a importar: cargas
      417-426 con la fecha del Word (10/10) y el transporte 1288129 con DANTE CHINCHAY / BSD-900 / T4Q-995;
      descargas 427-436 con el Word del 11/10 (10/10 por carreta, ahora coherente: "sin cambio de chofer"),
      enlazadas a su carga y con el CPIC 0003923. La auditoría guarda el cambio respecto de la guía.
- [x] **Destino de descarga por unidad** (2026-10-18). Un pedido de Vitapro no descarga todo en una planta: la
      administradora recibe del cliente la lista pedido / placa del camión ("tracto - carreta") / lugar de
      recepción (MILAGRO = Inbalnor, o JAVE). Migración **30** (`Schema/30_DestinoDescargaPorUnidad.sql`):
      plantas `Milagro` y `Jave` (internacionales) en `Planta` y tabla `DestinoDescarga` (pedido + carreta; un
      cambio de planta da de baja la fila vigente y crea otra: historial de desvíos). SPs `sp_DD_RegistrarDestino`
      y `sp_DD_QuitarDestino`. Pantalla `Views/DestinoDescarga.aspx` (menú Despacho › Destino de descarga;
      Admin/Supervisor): Excel (.xlsx) o filas pegadas con vista previa (nuevo / cambia / igual / error, avisos de
      placas fuera de flota) y corrección manual por unidad. Lectura en
      `Services/Despachos/Importacion/LectorDestinosDescarga.cs` (columnas por título o por contenido; las dos
      placas se separan por caracteres, 6 + 6) y reglas en `Services/Despachos/DestinoDescargaService.cs`
      (alias INBALNOR → Milagro). En RegistroDespacho, la descarga por pedido ahora tiene **una planta por
      tracto** (`ConductorLote.IdPlanta`, desplegable "Planta de descarga" en cada fila, propuesta desde la
      lista); la planta de arriba pasa a ser la de por defecto. El Word sigue mandando chofer y tracto; la
      lista del cliente manda la planta. Tests: `DestinoDescargaTests` (35) y `Integracion/DestinoDescargaBdTests`
      (4, con rollback). Aplicado en pruebas (migración + SPs).

## 5. Herramientas del repo

| Script | Para qué |
|---|---|
| `WebSGV/Database/sql.ps1 "<consulta>" \| -Archivo x.sql [-Texto] [-Salida f] [-Entorno produccion -ConfirmoProduccion]` | Consulta o script contra la BD (pruebas por defecto) sin exponer la clave: la lee de `connectionStrings.config`. `-Texto` devuelve textos largos sin recortar (definiciones de objetos). Úsalo en lugar de escribir sqlcmd a mano. |
| `WebSGV/Database/aplicar-migraciones.ps1 -Entorno pruebas\|produccion [-SoloListar] [-ConfirmoProduccion]` | Aplica en orden las migraciones de `Database/Schema/` pendientes y las registra en `dbo.SchemaVersion`. |
| `WebSGV/Database/aplicar-procedimientos.ps1 -Entorno ... [-SoloListar] [-Todos]` | Compara cada SP del repo con la BD (FALTA / DISTINTO) y aplica los que difieren. El repo es la fuente de verdad. |
| `WebSGV/Database/Scripts/migrar-contrasenas-texto-plano.ps1 -Entorno ...` | Convierte a PBKDF2 las contraseñas que sigan en texto plano (requiere compilar antes). |
| `WebSGV/Database/Scripts/reparar-tildes-bd.ps1 -Entorno ...` | Repara tildes corruptas en procedimientos, triggers y vistas de la BD que no están en el repo. |
| `privado/cambiar_password_nickdream_PROD.sql` | Cambio de contraseña de la cuenta de administrador de sistema en producción (fuera de git). |

Con sqlcmd contra somee usar siempre `-C -I -f 65001` (certificado, QUOTED_IDENTIFIER, UTF-8).

## 6. Paso a producción (al final, en este orden)

> **Decisión del 2026-10-19:** el sitio `sgvtransporte.somee.com` se publica (FTP desde Visual Studio,
> Release) **contra la BD de pruebas `sgvActualizada`**: se sube el `connectionStrings.config` local. La BD
> `sgvTransporte` no se usa por ahora, así que los pasos 1–5 de abajo quedan solo para si algún día se
> vuelve a ella. Desde la publicación, sgvActualizada tiene datos reales: no borrar ni limpiar sin confirmar.
> Pendiente: cambiar o desactivar los usuarios de prueba con contraseña conocida (`privado/CREDENCIALES_USUARIOS.txt`).

1. `aplicar-migraciones.ps1 -Entorno produccion -SoloListar`, revisar, y luego con `-ConfirmoProduccion`
   (hoy 01–34; la 34 agrega `tipoRuta`, `lugarCarga`, `lugarEntrega` y `lugarFin` al seguimiento, para los
   clientes terceros, y va antes de `sp_SE_SincronizarDespachos`, `sp_SE_RegistrarEntregaTercero` y
   `sp_SE_Dashboard_*`; la 33 agrega la última posición GPS al viaje, para el STATUS; incluye la 16, hora de Perú: el código nuevo usa `dbo.fn_AhoraPeru()`; la 20 amplía
   `OrdenViaje.observaciones`; la 21–22 van antes de los SP de exportación; la **26 borra
   `Despachos.idProducto`** y la 26–29 van antes de `sp_CrearDespacho`; la **30 agrega las plantas Milagro y
   Jave** y la tabla `DestinoDescarga`: revisar que producción no tenga ya plantas con esos nombres). Antes de reimportar el STATUS
   GENERAL, revisar si producción tiene los registros de seguimiento mal importados (en pruebas se limpió
   con `Database/Scripts/limpiar_seguimiento_importacion_2026-09-14.sql`).
2. `aplicar-procedimientos.ps1 -Entorno produccion -SoloListar` y revisar cada DISTINTO (si la BD tuviera
   algo más nuevo que el repo, actualizar primero el archivo del repo); luego con `-ConfirmoProduccion`.
3. `Scripts/reparar-tildes-bd.ps1 -Entorno produccion -SoloListar` y luego con `-ConfirmoProduccion`.
4. **Crítico:** `Scripts/migrar-contrasenas-texto-plano.ps1 -Entorno produccion -SoloListar` y luego con
   `-ConfirmoProduccion`. Si se publica sin esto, los usuarios con contraseña en texto plano (en pruebas
   eran 66 conductores) no podrán iniciar sesión.
5. `privado/cambiar_password_nickdream_PROD.sql` en SSMS (paso 1 lista la cuenta; el nombre exacto no
   se pudo confirmar).
6. Publicar desde Visual Studio con configuración **Release** (perfil `FTPProfile1`, sitio
   `https://sgvtransporte.somee.com`). Release fija `App.UrlPublica`, quita `debug`, activa
   `customErrors` y HTTPS. Verificar que se suba `bin\runtimes\win-x86\` (app pool de 32 bits; QuestPDF)
   y los DLL de **PdfPig 0.1.9** (importación de documentos del pedido). En el `appSettings.Secrets.config`
   de producción poner el `OnwayAuth0ClientSecret` nuevo (cambió el 2026-10-09). El mapa de la ruta del
   seguimiento escribe en `~/App_Data/RutasGps` (como `App_Data/OrdenesViaje`: la carpeta debe tener permiso
   de escritura) y necesita el `maxJsonLength` nuevo de `Web.config`. `sp_SE_RegistrarHitoGps` cambió
   (2026-10-20, `@reemplazarGps`): ya está en sgvActualizada; en otra BD, `aplicar-procedimientos` antes de publicar.
   `sp_SE_SincronizarDespachos` cambió (2026-10-20, adopta el viaje creado a mano): igual, ya en sgvActualizada.
   2026-10-21: `sp_CompletarDespachoProgramado` (nuevo) y `sp_SE_SincronizarDespachos` (viaje con pedido
   PENDIENTE para la carga programada con el Word) ya están en sgvActualizada; el sitio publicado no los usa
   hasta subir el código nuevo. La caché del historial GPS escribe en `~/App_Data/HistorialGps` (permiso de escritura).
   2026-10-21 (terceros): migración 34, `sp_SE_SincronizarDespachos`, `sp_SE_RegistrarEntregaTercero` (nuevo) y
   `sp_SE_Dashboard_KPIs/Graficos` (filtran `tipoRuta IS NULL`) ya están en sgvActualizada. **El sitio publicado
   con el código anterior sigue funcionando** con la migración (columnas nuevas opcionales), pero el SP de
   sincronización nuevo crea viajes de terceros que el código viejo mostraría como de Vitapro: publicar el código
   junto con (o antes de) registrar los clientes CRISAR/POLINPLAST.
   2026-10-22: `sp_DD_RegistrarDestino` (cambia la bodega del viaje) y `sp_SE_SincronizarDespachos` (la lista manda
   sobre el despacho) ya están en sgvActualizada; en otra BD, `aplicar-procedimientos` antes de publicar.
   2026-10-22: **migración 35** (`fhLlegadaGrifo`, `fhSalidaGrifo` en SeguimientoExportacion) ya aplicada en
   sgvActualizada; el código nuevo la necesita (en otra BD, `aplicar-migraciones` antes de publicar). La llegada
   estimada no toca la BD: sus perfiles van dentro de WebSGV.dll.
7. Tras publicar: en `RegistroChoferes` → "Actualizar con la lista" con
   `LISTA DE CHOFERES ACTUALES (corregida).xlsx` (no hay migración; Victor Marzal con DNI 00243179, Yépez y
   Rivas con carnet de extranjería). Requiere el `sp_RegistrarConductorImportado` del paso 2.
8. Con `connectionStrings` de producción, correr los tests de integración **no** está permitido (solo
   aceptan pruebas): verificar a mano el login con cada rol y "Ver historial" en RegistroDespacho.
