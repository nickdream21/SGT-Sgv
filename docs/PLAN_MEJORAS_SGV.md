# Plan de mejoras SGV — estado y tareas pendientes

Última actualización: **2026-10-20**, al día con el seguimiento de exportación (`a22748b`) más el cuadro STATUS GENERAL de octubre armado desde los Word y el GPS (119 viajes de octubre ya en `sgvActualizada`) y el mapa de la ruta en el detalle del viaje, la salida en camino y la vuelta corrida (sin migración; SP `sp_SE_RegistrarHitoGps` y `sp_SE_SincronizarDespachos`; sección 4). Este documento es el punto de partida para seguir trabajando
desde cualquier máquina. No contiene contraseñas: solo indica dónde están.

## 1. Estado actual

| | Al empezar (2026-10-07) | Ahora |
|---|---|---|
| Puntuación general | 5.5 / 10 | **8 / 10** |
| Tests | 215 | **508** en verde (2026-10-20; incluye los de integración, varios escriben con rollback) |
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

Detalle de reglas y convenciones nuevas: ver `CLAUDE.md`.

## 4. Tareas pendientes (en pruebas)

Ordenadas por valor. Marca `[x]` al terminar. Todo pendiente va como su propio `[ ]` en esta lista
(no escondido dentro del texto de una tarea hecha).

### Abiertas

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
- [ ] **Decidir si el tablero crea los viajes desde el Word de programación** (antes de que llegue el pedido de
      Vitapro), para no depender de crearlos a mano como los 6 del 20/10.
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
- [ ] **Seguimiento: guardar la hora de la programación del Word** (columna HORARIO, p. ej. "05:50 AM") en el
      despacho y pasarla a F.H. Programación. Hoy se lee y se muestra en RegistroDespacho pero no se guarda:
      el viaje toma solo la fecha del despacho (para el GPS alcanza, busca desde ese día).
- [ ] **Seguimiento: crear también el viaje al registrar el despacho** (hoy se crea al abrir el tablero, con
      `sp_SE_SincronizarDespachos` sobre los despachos de carga de exportación de los últimos 60 días).
- [ ] **Seguimiento: confirmar que cada despacho de descarga en Ecuador se enlaza a su carga** (`idDespachoCarga`).
      Si no, la sincronización lo une por pedido y carreta; un despacho de descarga sin carreta o con otra
      carreta no completa el viaje.
- [ ] **Seguimiento, etapa 4: actualización automática del GPS** (tarea programada en la PC de la oficina o
      similar; somee no corre tareas en segundo plano) y guardar en BD el historial GPS ya descargado por
      tracto y día, para no volver a pedirlo al API. Hoy "Actualizar GPS de viajes abiertos" hace ~3 viajes por
      clic (~30 s por viaje: ~20 días de historial cada uno; medido el 2026-10-20 con los 10 viajes del pedido
      4400133436). Además, cortar la ventana de la descarga cuando el viaje ya volvió a base.
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
- [ ] **Destino de descarga: ¿el destino cambia después de enviada la lista (desvío en ruta)?** No se sabe.
      El modelo ya lo soporta (cada cambio deja el historial en `DestinoDescarga`); confirmar con transporte
      si hace falta avisar o bloquear cambios cuando la unidad ya se descargó.
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
   (hoy 01–32; incluye la 16, hora de Perú: el código nuevo usa `dbo.fn_AhoraPeru()`; la 20 amplía
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
7. Tras publicar: en `RegistroChoferes` → "Actualizar con la lista" con
   `LISTA DE CHOFERES ACTUALES (corregida).xlsx` (no hay migración; Victor Marzal con DNI 00243179, Yépez y
   Rivas con carnet de extranjería). Requiere el `sp_RegistrarConductorImportado` del paso 2.
8. Con `connectionStrings` de producción, correr los tests de integración **no** está permitido (solo
   aceptan pruebas): verificar a mano el login con cada rol y "Ver historial" en RegistroDespacho.
