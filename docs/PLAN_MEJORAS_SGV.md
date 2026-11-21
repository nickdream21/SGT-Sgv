# Plan de mejoras SGV

> **Qué etapa sigue:** `docs/HOJA_DE_RUTA_CALIDAD.md` (leerla primero). **Historial:** `git log` (un commit por tarea); lo anterior al 2026-11-21, en `docs/archivo/HISTORIAL_2026.md` (congelado).

Última actualización: **2026-11-21**, commit anterior `0b46c1d` — E2-7: Etapa 2 cerrada con nota 7 (medición en `docs/HOJA_DE_RUTA_CALIDAD.md`). No contiene contraseñas: solo dice dónde están.

Último id usado: M-100 (el próximo pendiente toma el siguiente; ver "Reglas" al final).

## Estado

- El sitio publicado (`https://sgvtransporte.somee.com`, publicación `20261120_1311_60853e8`) usa la BD de **pruebas** `sgvActualizada`, que tiene **datos reales** desde el 2026-10-19: no se borra ni limpia nada allí sin confirmar.
- Producción (`sgvTransporte`) no está al día: el paso a producción es `docs/PASO_A_PRODUCCION.md`, en orden, tras la decisión U-011.
- Etapa 1 (operación) cerrada: respaldo diario de la BD y de los archivos en Google Drive, restauración ensayada, publicación con vuelta atrás (`herramientas/publicar/publicar.ps1`), monitores de UptimeRobot y cron del GPS activos.
- Etapa 2 (ordenar el plan) cerrada el 2026-11-21 con nota 7; **sigue la Etapa 3** (integridad de la BD), primero su plan. Las etapas 3 a 6 (integridad de la BD, seguridad, interfaz, arquitectura) están en la hoja de ruta.
- Lo abierto está en este plan (mejoras) y en los documentos de "Otros documentos" (usuario, datos, pruebas); cada uno se cuenta en su propio documento.

## Top 20

Propuesto por Claude en E2-4: primero las de prioridad A (riesgo de datos, dinero o seguridad, o bloquean el uso real), luego las B de más valor. El usuario lo ajusta según el negocio. Al cerrar una, entra la siguiente A o B.

| # | Id | Qué | Por qué importa | Quién |
|---|---|---|---|---|
| 1 | U-001 | Rotar el token de apisperu | el token está en el historial de git | Usuario |
| 2 | U-002 | Cambiar las contraseñas de `CREDENCIALES_USUARIOS.txt` | guarda la de producción de `nickdream` en texto plano y la vieron agentes | Usuario |
| 3 | U-003 | Contraseña de aplicación de Gmail | quedó escrita en un chat; con ella se envía correo como el sistema | Usuario |
| 4 | U-004 | Contraseña FTP de somee | quedó en un chat y da escritura sobre el sitio y los PDF firmados | Usuario |
| 5 | U-005 | Borrar `.env` y `dab-config.json` en cada PC | cadena de conexión real en disco | Usuario |
| 6 | U-011 | Decidir cómo pasar a producción (A o B) | con A, los PDF firmados y documentos del servidor quedan sin su fila | Usuario |
| 7 | D-002 | E001-18719 anulada sigue activa | la misma carga figura cobrada dos veces (US$ 14 731,20) | Oficina; Claude corrige |
| 8 | D-001 | Facturas de flete de Vitapro por registrar | dinero por cobrar que el sistema no ve | Oficina |
| 9 | D-011 | Descargas de Guayaquil de 9 pedidos | sin despacho de descarga no hay liquidación del chofer ni CPIC | Claude con el Word (orden del usuario) |
| 10 | M-046 | Subir al sitio los Uploads de la oficina | facturas registradas que en el sitio dicen "el archivo no existe" | Claude (orden del usuario) |
| 11 | M-040 | Bandeja con dos pestañas | puede registrar documentos en el despacho equivocado | Claude |
| 12 | M-069 | Coma decimal en el celular | un monto del conductor puede llegar ×100 | Claude; el usuario lo confirma en celular |
| 13 | P-001 | Prueba de punta a punta con un pedido real | robustez del flujo completo antes de presentarlo | Claude + usuario |
| 14 | U-012 | Activar `ConductoresEnSistema` | sin esto los conductores no liquidan en el sistema | Usuario (decide el día) |
| 15 | M-037 | Generar el CPI y el MCI | hoy se copian a mano en Excel, con errores encontrados | Claude, tras U-023 |
| 16 | M-070 | Saldos con monedas de signo opuesto | muestra dinero con un sentido engañoso | Claude |
| 17 | M-001 | Word sin coincidencia → despacho nuevo | riesgo de despacho de más | Claude |
| 18 | M-002 | Guía con fecha posterior a la programada | riesgo de despacho duplicado | Claude |
| 19 | M-003 | Guía emitida 2 días antes no encuentra su programado | deja guías sin programado y puede duplicar el despacho | Claude |
| 20 | U-015 | Desactivar usuarios de módulos retirados | 3 cuentas activas con contraseña conocida | Usuario |

## Otros documentos

- `docs/PENDIENTES_USUARIO.md` — lo que solo puede hacer el usuario (`U-`): decisiones, credenciales y paneles externos, datos por conseguir.
- `docs/DATOS_POR_CORREGIR.md` — registros por corregir o cargar (`D-`), con su consulta.
- `docs/PRUEBAS_PENDIENTES.md` — lo que falta probar en el navegador, en celular o con un pedido real (`P-`), para el `probador` o el usuario.
- `docs/DECISIONES_NEGOCIO.md` — decisiones y reglas ya tomadas (`DN-`).
- `docs/PASO_A_PRODUCCION.md` — pasar el sitio a `sgvTransporte`; `herramientas/publicar/README.md` — cada publicación ("Antes y después de publicar").
- `docs/PREPARAR_MAQUINA.md` — preparar otra PC; `docs/OPERACION.md` y `docs/PROCEDIMIENTO_RESPALDO.md` — vigilancia, avisos y respaldo.

## Mejoras abiertas — Negocio

### Despachos y Registro de despacho

- [ ] **M-001** RegistroDespacho con Word: filas sin coincidencia arrancan como "Despacho nuevo" — una fila del Word sin placas coincidentes o una guía que no está en el Word proponen despacho nuevo aunque haya programados libres (`ImportacionPedidoService.cs:~598`, `ProgramacionCarga.cs:~182-186`): riesgo de despacho de más. Marcarlas `PorElegir` cuando haya programados libres. · B
- [ ] **M-002** Guía con fecha posterior a la programada — `EnVentana` no ve el programado del día anterior y propone un despacho nuevo (riesgo de duplicado). Ampliar 1 o 2 días hacia atrás y avisar si esa carreta ya está programada. · B
- [ ] **M-003** Guía emitida 2 días antes de cargar no encuentra su programado — `ImportacionPedidoService.UsarProgramados` busca hasta 1 día después de la guía (`ImportacionPedidoService.cs:580`, `AddDays(1)`). Llevar a la pantalla la regla de `CompletarConCorreo` (mismo tracto a +2 días, el más próximo) con pruebas en `ProgramacionCargaTests`. · B
- [ ] **M-004** Guía o factura escrita a mano en una carga programada — el ZIP ya no reconoce esa carga (`FiltroSinDocumentos` exige que no tenga guía ni factura) y crearía otro despacho; hoy solo hay un aviso en "Documentos". Que el ZIP complete también una carga con la misma guía ya escrita. · B
- [ ] **M-005** "Carga para exportación" se deduce del último despacho del cliente y planta — `ProgramacionDespachoService` (`p.EsExportacion`): un despacho atípico deja la programación siguiente sin exportación ni viaje en el tablero. Deducirlo de la ruta del Word o del cliente. · B
- [ ] **M-006** Lista de lotes: filtrar por la 2.ª factura o CPIC muestra solo esos despachos del lote — el filtro del SP va antes de agrupar; pasarlo a un `EXISTS` como el de conductor. · C
- [ ] **M-007** Facturado vs cargado (fase 2 del control de documentos) — guardar sacos y kg de la factura al leer el ZIP (migración nueva en `Factura`; los pesos del CPIC hoy se graban en 0) y un reporte por cliente/pedido contra `DespachoCarga`. Depende de saber qué trae la factura de cada tercero (U-020). · B
- [ ] **M-008** Descarga de una carga que todavía no tiene documentos — `DescargaPedidoService` exige pedido y detalle de carga; si el correo se retrasa más que la descarga, el tracto 2 se pone a mano en el detalle del viaje. Valorar permitirlo. · C
- [ ] **M-009** Cambio de chofer con varias guías sin unir — el descarte solo une si queda una guía y un programado; con más se eligen a mano. Proponer con el GPS qué tracto programado estuvo en la planta a la hora de la guía. · C
- [ ] **M-010** Programado editado a mano en ListaDespachos — si se le pone pedido o guía, deja de estar "esperando documentos" y el correo crearía otro despacho. Ver si hace falta avisarlo. · C
- [ ] **M-011** Hora de la programación del Word en el lote ZIP+Word — en el lote que se arma con ZIP + Word se guarda la hora del registro, no la columna HORARIO (al programar con el Word ya se guarda). · C
- [ ] **M-012** Alias de choferes — recordar la corrección cuando se elige a mano un nombre del Word no reconocido (tabla de alias por conductor, migración). Hoy no hace falta: 64/64 nombres reales se reconocen. · C
- [ ] **M-013** Finalizar lote: copias huérfanas tras un commit en duda — si el lote en realidad no se guardó, las copias definitivas del primer intento quedan sueltas en `~/Uploads` (rutas en `App_Data/logs`). Sin riesgo para los datos; limpiar a mano si pasa o automatizar la limpieza. · C

### Liquidaciones y saldos

- [ ] **M-014** Quitar `@idConductorFallback` de `sp_DC_ObtenerDatosViajeParaLiquidacion` — después de probar el ciclo con un conductor real (P-002). · B
- [ ] **M-015** PDF de la liquidación: respaldo legal — validar con el contador y el abogado laboral: ocultar "Comprobantes sustentatorios" si no hay (hoy lista los peajes con la fecha del envío y sin número); citar el D.S. 029-2021-PCM junto al 052-2008-PCM y decir "firma electrónica" simple; enviar al conductor copia del PDF firmado (correo o WhatsApp); viajes a Ecuador: declaración jurada por día para gastos sin comprobante (SUNAT la admite hasta el 30 % del tope) y planilla de movilidad local. La URL de verificación ya sale de `App.UrlPublica`. · B
- [ ] **M-016** GPS caído impide enviar un viaje nacional que cruzó a Ecuador — regla vigente: no se envía mientras el GPS no responda. Si molesta en la práctica, que la administración pueda marcar el viaje como internacional. · C

### Exportación, seguimiento y GPS

- [ ] **M-017** Retorno con carga hecho por otro tracto — `sp_SE_RegistrarRetornoConCarga` solo anota el retorno; no guarda tracto ni chofer de la vuelta y el GPS no la sigue (caso: carreta TBE-995 vuelve con BNH-712 en el Word del 08/11; la ida, viaje 7864, era AVN-717). Decidir si se guarda (como el tracto 2 de la descarga) y si marca la llegada a Trujillo. · B
- [ ] **M-018** "Actualizar GPS" del detalle de un viaje borra el aproximado con el grupo — pone `gpsAproximadoDe = NULL` hasta la siguiente actualización de todos (10 min con cron-job.org). Decidir si el detalle también aproxima con el grupo. · B
- [ ] **M-019** Excel de STATUS: agregar el N° de factura — el seguimiento no lo guarda; está en la factura del despacho de carga (`idDespachoOrigen`). · B
- [ ] **M-020** Reporte del grifo Santa Rosa enviado solo — con el GPS automático los datos están al día, pero el texto se arma al abrir RegistroSeguimiento: falta el envío (correo o WhatsApp). Depende de confirmar el formato (U-028). · B
- [ ] **M-021** Unidad sin equipo GPS (F6O-779) en el tablero — sus viajes no avanzan solos; marcarlos "sin GPS: llenar a mano" en vez de consultarlos en cada actualización. · C
- [ ] **M-022** ImportarSeguimiento: mostrar qué filas se omitieron — hoy solo cuántas: que `sp_SE_ImportarLote` devuelva un segundo resultado con fila/pedido/tracto de cada omitida. · C
- [ ] **M-023** Seguimiento: importados de 2024 cuentan como "Retorno" — con 2024 e importados: Activos 500, Retorno 482, Cerrados 0. Revisar la fase que `HitosSeguimiento` da a los importados sin hitos de cierre. · C
- [ ] **M-024** Mapa del viaje: paradas calculadas en el navegador — (250 m, 30 min). Si se quieren en el Excel o el dashboard, llevarlas al servidor (`ObtenerRuta`). · C
- [ ] **M-025** Cargas históricas de terceros crean viajes EN_CURSO — al registrar despachos pasados con `TercerosConDocumentos`, `sp_SE_SincronizarDespachos` crea sus viajes EN_CURSO; cerrarlos con `sp_SE_CambiarEstado` o que la herramienta lo haga sola. · C
- [ ] **M-026** Salida/llegada que el seguimiento no marca — los viajes que empiezan con un tercero (Crisar en Cartavio, Polinplast en Chapaira) no tienen salida de la base, y los que terminan en la cochera o en Vitapro Trujillo no tienen llegada. Buscar en el historial GPS la última salida de la base Sullana antes de la carga (como `HoraLlegadaGpsService`). · C
- [ ] **M-027** Llegada estimada para terceros — (Crisar a Cartavio y La Troncal, Novopan, Polinplast): hoy solo Vitapro. Mismo método con sus viajes. · C
- [ ] **M-028** Llegada estimada desde TCI y el Complex — hoy no se estima mientras la unidad espera en aduana; ver si con más datos conviene dar un rango. · C
- [ ] **M-029** TALSA, Rapel y destinos nuevos de Polinplast sin geocerca — un destino nuevo queda "Sin lugar de entrega" hasta agregar su geocerca en `GeocercasTerceros`. Depende de las preguntas de terceros (U-054). · C
- [ ] **M-030** Geocercas de terceros: dibujarlas sobre el terreno — hoy son círculos en `GeocercasTerceros` sacados de paradas del GPS; comprobarlas con casos marcados por la administradora antes de usarlas para marcar horas. · C
- [ ] **M-031** Hora de la programación en el seguimiento — `sp_SE_SincronizarDespachos.sql:50` pasa solo la fecha a F.H. Programación aunque el despacho guarda `horaDespacho`. · C
- [ ] **M-032** Seguimiento: crear el viaje al finalizar el lote — al programar con el Word ya se crea; el lote con documentos espera a que se abra el tablero (`sp_SE_SincronizarDespachos`). · C
- [ ] **M-033** Seguimiento: vincular el despacho en la pantalla — `idDespachoOrigen`/`idDespachoDestino`: la pantalla vieja tenía el buscador; la nueva no lo muestra (las columnas siguen y el GPS las usa). · C
- [ ] **M-034** Trazabilidad: unir los tramos por la carreta — con la columna CAMBIO del Word (qué tracto lleva la carreta en la carga y cuál en la descarga), para armar el viaje completo y completar tracto 2 y chofer de descarga sin esperar el pedido. · C
- [ ] **M-035** Perfiles de llegada: medir y regenerar cada mes — volver a medir la hora al grifo de Crisar con `Generar-PerfilCrisar.ps1` cuando haya más días (error típico 1,5 h); comparar la llegada estimada con la real unos días y ajustar la ventana de km/hora; regenerar cada mes `PerfilesLlegada.json` con `Generar-Perfil.ps1` y luego `Generar-PerfilCrisar.ps1` (y recompilar: va en la DLL). Ver `herramientas/perfil-llegada/README.md`. · C
- [ ] **M-036** Espacio de `App_Data/HistorialGps` en somee — 3–45 KB por tracto y día, se borra a los 30 días: revisar la cuota tras unas semanas de uso. · C

### CPIC y MCI

- [ ] **M-037** Generar el CPI y el MCI desde el sistema — hoy la administradora los arma a mano en Excel (plantillas y un .xls de choferes y unidades) y el sistema solo guarda el escaneo; el sistema ya tiene los datos. Origen: 51 CPI y 288 MCI de su carpeta de escaneos `docs/DOCUMENTACION/DOC TRANSPORTE/DOCS TRANSPORTE` (gitignored, solo en la PC de la oficina; tiene documentos ajenos y sensibles: abrir solo los C.P.I/MCI). Reglas observadas en `docs/DECISIONES_NEGOCIO.md` (DN-21 a DN-24); diseñar el generador (plantilla por cliente/ruta; ella pone número y día de salida) cuando respondan las preguntas del CPI (U-023). · A
- [ ] **M-038** Correlativos de CPIC usados y faltantes — registrar cada CPIC usado (pedido o cliente, o anulado) para ver los números que faltan: en octubre a Vitapro se usaron 31 CPIC entre el 0003892 y el 0003946 y quedan 24 sin explicar. · B

### Facturación

- [ ] **M-039** Cobranza de facturas de flete — registrar pagos (fecha, monto, saldo, vencidas); hoy solo se muestra el vencimiento. Falta decidir de dónde sale el pago (estado de cuenta del banco, aviso del cliente). · B

### Bandeja de documentos

- [ ] **M-040** Bandeja con dos pestañas registra la lectura equivocada — hay una sola lectura por sesión: si se lee en la pestaña B y se registra en la A, se registra la lectura de B con los índices de A. Poner un id de lectura en un campo oculto y compararlo al registrar. · A
- [ ] **M-041** Bandeja une una CPIC de Vitapro a una carga de Polinplast — las C.P.I 0003886 y 0003888 se proponían como carga de Polinplast porque una carreta de sus MCI coincidía. La unión por carreta de un tercero debe exigir que el cliente o remitente de la CPIC sea el del despacho. · B
- [ ] **M-042** Documentos en espera en la bandeja — guardar en la BD lo que no se pudo unir y reintentar cuando llegue el Word, el ZIP o la CPIC (hoy se avisa, pero se pierde). Por diseñar. · B
- [ ] **M-043** Bandeja como entrada única — que acepte también el Word de programación (hoy solo en Registro de despacho). · C
- [ ] **M-044** Subir un PDF suelto desde el expediente — guía escaneada o MCI de un conductor: un escaneo sin texto no se reconoce por su contenido y solo queda el registro manual. · C

### Reportes

- [ ] **M-045** Reporte de cargas sin el MCI — `ReporteCargasService` tiene sus propios conteos de documentos (no usa el perfil del cliente ni cuenta el manifiesto): alinearlo con `sp_LD_ObtenerLotesRegistrados`. · B

### Operación pendiente (una vez)

- [ ] **M-046** Subir al sitio publicado los Uploads que solo están en la PC de la oficina — con `herramientas/publicar/subir-uploads.ps1` (orden del usuario): los 23 PDF de factura de `Uploads\Factura\2026\11\` (la BD ya los registra y el sitio dice "el archivo no existe"), el de F222-00008545 (está en `Uploads` de la laptop), los CPIC y MCI de Crisar/Novopan (`Uploads\CPIC|Manifiesto\2026\11\`) y las facturas comerciales. Las 76 facturas de flete ya se subieron. Los 14 pedidos del 01–19/10 no tienen PDF de factura adjunto: el usuario decide si se suben a mano desde ListaDespachos. · A

## Mejoras abiertas — Calidad (por etapa de la hoja de ruta)

### Etapa 3 — Integridad de la BD

- [ ] **M-048** Correr `Database/Scripts/borrar-sp-se-sin-uso.sql` en `sgvActualizada` — los 8 `sp_SE_*` sin uso siguen en la BD y `60853e8` (publicado) no los llama. Antes, el SELECT de control de `docs/PASO_A_PRODUCCION.md`, paso 6. Con orden del usuario. · B
- [ ] **M-049** Migración 52: DROP de los 9 SP de módulos retirados — `sp_InsertarIndicador`, 3 de grifo y 5 `sp_MQ_*` (siguen en `sgvActualizada`). Pedir confirmación antes de aplicarla (es la BD del sitio). Las tablas y sus datos se conservan. · B
- [ ] **M-050** Quitar el DEFAULT 'APROBADO' de `OrdenViaje.estadoAprobacion` — un INSERT que omita la columna daría la orden por aprobada sin firma (hoy ningún INSERT del repo la omite). · B
- [ ] **M-051** Número de viaje por conteo — `VP-AAAA-NNN` = cantidad de viajes del año + 1 (`sp_CrearDespacho.sql:110`, `sp_CrearViajeProgreso.sql:18`, `sp_LD_ReubicarDespacho…:78`): si se borran viajes puede repetirse. Pasar a un correlativo (hoy sin repetidos). · B
- [ ] **M-052** Ejecución de humo antes de aplicar un SP — un SP con error rompió 15 min la lista de lotes del sitio publicado (tarea 17). Añadir a `aplicar-procedimientos.ps1` una ejecución de prueba de los SP de lectura (o `SET PARSEONLY`/`sp_describe_first_result_set`) antes de dar "Listo". · B
- [ ] **M-053** Query Store acotado en `sgvActualizada` — tope de 500 MB de datos; el Query Store puede crecer hasta 1000 MB (`max_storage_size_mb` medido en pruebas). Bajarlo a 100 MB (`ALTER DATABASE … SET QUERY_STORE (MAX_STORAGE_SIZE_MB = 100)`). Crecimiento estimado con todos los choferes liquidando: 11–16 MB al mes (2 a 3 años); `sgvTransporte` (Premium) tiene 2000 MB. · B
- [ ] **M-054** `sp_DC_RevocarFirma` y `FirmaService.RevocarFirma` sin uso — siguen en el repo (`sp_DC_RevocarFirma.sql`, `FirmaService.cs:229`) y en la BD: borrarlos. · C
- [ ] **M-055** Borrar `sp_LD_ActualizarConductorDominanteViajes` — de la BD y del repo: ya no se llama y el código nuevo está publicado (`60853e8`). · C
- [ ] **M-056** Borrar `MovimientoDineroViaje.idConductorPortador` — y su FK, con una migración nueva; aún lo usa `sp_LD_RegistrarMovimientoViaje.sql:21`. No antes de decidir si se retoma "Dinero y pagos del viaje" (U-040) y volver a probar la pantalla. · C
- [ ] **M-057** Restos sin uso tras retirar Grifo — la regla `:where(div).total-row` de `Content/responsive-mobile.css` y los scripts `Database/Scripts/script_{AgregarColumnaRutaDescripcion,AgregarColumnaTipoAbastecimiento,AgregarPdfAbastecimiento,CrearTablasDespachoObra,CrearTablasIngresoEcuador,TrazabilidadVolqueteCamioneta}.sql`. No borrar `AbastecimientoCombustible`, `DespachoCombustibleObra` ni `IngresoCombustibleEcuador` sin tocar antes `VehiculoService.cs` (las consulta al eliminar tractos y carretas). · C
- [ ] **M-058** Restos tras retirar Maquinaria — `Database/Scripts/script_CrearTablasMaquinaria.sql` (en el csproj) y la columna `Usuarios.idOperador` con su FK (ningún código la lee). Decidir junto con las tablas de maquinaria (1 fila de maestros cada una). · C
- [ ] **M-059** FacturasFlete: PDF anterior huérfano al corregir — al corregir una factura (ACTUALIZADA) el PDF anterior queda en `Uploads/FacturasFlete` sin fila que lo apunte; limpieza de huérfanos. · C
- [ ] **M-060** Tabla `PuntoControlGps` de exportación casi sin uso — "Verificar GPS" ya no usa sus 20 puntos, pero `HoraLlegadaGpsService.cs:52` usa el punto `BASE`: no borrar la tabla entera. · C

### Etapa 4 — Seguridad

- [ ] **M-061** Cambiar el carnet de extranjería real del ejemplo por uno ficticio — en `Services/Conductor/DocumentoConductor.cs`, `Services/Conductor/RegistroConductorService.cs`, `WebSGV.Tests/DocumentoConductorTests.cs` y `WebSGV.Tests/Integracion/ConductorLargosBdTests.cs` (que la prueba inserte su propio conductor dentro de `TransaccionDePrueba`). Es de un conductor real (conductor 218). · B
- [ ] **M-062** Login de una cuenta inactiva no deja `LOGIN_FALLIDO` — en AuditoriaLog (observado en B5-8). Va con "registrar LOGIN_FALLIDO y bloqueos" de la etapa 4. · B
- [ ] **M-063** Mensaje propio para un usuario con rol retirado o desconocido — hoy vuelve a Login con "Tu sesión ha expirado…" y cada intento deja un LOGIN en auditoría. Rechazarlo antes de crear la sesión con "tu cuenta no tiene un rol vigente". Deja de importar si se desactivan los usuarios de roles retirados (U-015). · C
- [ ] **M-064** Secciones de `TienePermiso` sin página — FACTURA, CPIC, CONSULTAS, INDICADORES, AUDITORIA… (`RolesHelper.cs:169-182`) y ADMIN con acceso a DASHBOARD_CONDUCTOR: depurarlas (cambia `RolesHelperTests` a propósito). · C
- [ ] **M-065** ReportesOrdenesViaje: `ex.Message` de cualquier `ArgumentException` al usuario — `ResponderSolicitudInvalida(ex.Message)` (`ReportesOrdenesViaje.aspx.cs:~1123`): si ClosedXML/QuestPDF lanza una, su texto técnico llega al usuario. · C
- [ ] **M-066** No quedarse sin administrador de sistema activo — impedir que dos se desactiven a la vez (`NOT EXISTS` de otro ADMINISTRADOR DE SISTEMA activo en el `WHERE`) y ocultar "Desactivar" en la fila propia (hoy el servidor lo rechaza con aviso). · C
- [ ] **M-067** Bandeja: documentos dentro de un ZIP sin comprobación de firma de archivo — los CPIC/MCI, GRE y facturas de flete de un ZIP no tienen comprobación propia (solo las guías al guardarse). · C
- [ ] **M-068** Ajustar `Seguridad.IteracionesContrasena` — con los ms de "Hash de contraseña … convertido" en `App_Data/logs` del servidor (tarea 7); no volver a un DLL anterior. · C

### Etapa 5 — Interfaz

- [ ] **M-069** Celular: coma decimal en `type=number` — "12,50" podría enviarse como 1250 (visto en Edge headless es-PE): monto ×100. Validar en el JS antes de enviar y confirmarlo en un celular real (P-003). · A
- [ ] **M-070** Saldos: encabezado del conductor con monedas de signo opuesto — `SaldosConductores.aspx.cs:79-90`: con neto +100 S/ y −50 US$ dice "A descontar S/ 100,00 · US$ 50,00" mientras el resumen pone los US$ en "A reintegrar". Mostrar el sentido por moneda. · B
- [ ] **M-071** SaldosConductores: el enlace a la orden no sirve para el administrador — `SaldosConductores.aspx:93` enlaza a `DetalleOrdenViaje.aspx?id=`, que redirige a quien no es conductor. Apuntarlo a la revisión/PDF del administrador o permitirle ver el detalle. · B
- [ ] **M-072** Celular: filtros antes del resultado — en ReporteCargas el listado empieza a ~3 pantallas (y=2902) y en el tablero de RegistroSeguimiento a y≈1151; DashboardExportacion con chips de 26 px y textos de 11,5 px. Rediseño con filtros plegables. · B
- [ ] **M-073** Unificar el aspecto de las pantallas de administración — pedido del usuario: solo 6 pantallas usan `sgv-base.css`; ~20 encabezados distintos y scripts con `alert()`. Por fases con captura antes/después: (1) encabezado y botones comunes; (2) catálogos `Registro*` con una plantilla; (3) las pantallas de buscar y editar. · B
- [ ] **M-074** Manual de uso del sistema por rol — pedido del usuario al terminar de regularizar desde octubre: punta a punta, por rol, con capturas (programar con el Word; ZIP, PDF y Bandeja; vincular documentos; seguimiento y GPS; facturas de flete; el conductor liquida, firma y da conformidad desde el celular). Incluir que "Editar lote" no pasa un lote a una pareja factura+CPIC existente (se hace desde "Factura y CPIC por despacho"). Reemplaza la guía de una hoja de la puesta en marcha de conductores. · B
- [ ] **M-075** Videos de uso con voz, uno por tema — programar, subir y vincular documentos, seguimiento, revisión y aprobación, el conductor liquidando desde el celular; faltan el viaje internacional (peajes en soles y dólares), liquidación rechazada y corregida y retirar liquidación. Grabarlos con el flujo estable y datos reales de octubre en adelante. · B
- [ ] **M-076** "Auditoría del Sistema" sale dos veces en el menú del administrador de sistema — `Site.Master:199-200` y `241-242` (Administración y Configuración). Dejar una. · C
- [ ] **M-077** ReportesOrdenesViaje: vuelve a la pestaña "Liquidaciones" tras Buscar en "Viajes activos" — y en esa pestaña "Todos los estados" da lo mismo que "Abierto". · C
- [ ] **M-078** Mensajes de conductor duplicado con los espacios de las columnas CHAR — "NOMBRE      APELLIDO": colapsar espacios en `RegistroConductorService.BuscarDuplicado`/`ObtenerDocumentos` (`RegistroConductorService.cs:126`). · C
- [ ] **M-079** Regex de carnet del navegador más laxa que el servidor — acepta `ABCDEFG` (sin dígitos) que el servidor rechaza: alinearla con `DocumentoConductor.EsCarnetValido`. · C
- [ ] **M-080** Detalles menores del emparejamiento de la bandeja (tarea 15) — el aviso de una CPIC en espera dice "no se eligió el camión" aunque las guías se pusieran en "No registrar ahora" a propósito; la lista de programados de RegistroDespacho ofrece también el que ya completa otra guía. · C
- [ ] **M-081** Registro de despacho: vistas previas del ZIP y de "Registrar la descarga" — conservan su diseño antiguo (se ordenó solo la del Word). · C
- [ ] **M-082** Lista de despachos: tarjetas y pestaña fuera de lugar — en "Registro manual" y en el detalle de un lote siguen las 4 tarjetas de totales y "Viajes activos" marcada: ocultarlas fuera de las dos listas. · C
- [ ] **M-083** Pasar a `data-confirmar` los `OnClientClick="return confirm(...)"` restantes — unos 24 en aspx/cs (catálogos, CargarExcel, DestinoDescarga, RegistroChoferes, GestionUsuarios.aspx.cs); si el botón valida, hoy sale el diálogo nativo. · C
- [ ] **M-084** Registro de clientes: celda de contacto — el texto sale más bajo que el resto de la fila. · C
- [ ] **M-085** Expediente completo muestra igual "Para completar, suba…" — ocultar esa ayuda cuando no falta nada (`BandejaDocumentos.aspx.cs:770`). · C
- [ ] **M-086** Registro de despacho: tira de pasos y "Ver lotes pendientes" — "1 Word → 2 ZIP → 3 Revisar pendientes" y el botón al terminar de programar. · C

### Etapa 6 — Arquitectura y medición

- [ ] **M-087** Test de integración "dinero entregado no menor que las entregas unidas" — regla en `LiquidacionesPendientesService.cs:305` (el caso de rechazo va al final del test) y limpiar los PDF que dejan los tests en `App_Data/OrdenesViaje`. · C
- [ ] **M-088** Quitar el panel viejo "Documentos del lote" de ListaDespachos — hoy solo se llega con `?manual=1&lote=`; pasar ese registro manual al expediente y borrar `pnlManifiestosLote` y su código (con la partición de ListaDespachos). · C
- [ ] **M-089** Juntar pantallas que hacen lo mismo — los catálogos `Registro*` (misma grilla + formulario) sobre una plantilla común; `LiquidacionesAprobadasContabilidad` como pestaña de Gestión de liquidaciones. · C
- [ ] **M-090** El log no distingue la corrida manual del GPS de la del cron — ambas escriben "Actualización automática del GPS" (`ActualizacionGpsAutomatica.cs:123`): añadir "(manual)"/"(programada)". · C
- [ ] **M-091** Tablero de seguimiento: respaldo por postback si `GpsAutomatico.ashx` cae — el respaldo hace la consulta pesada por postback (`RegistroSeguimiento.js:~73-98`). Nota para probadores: abrir el tablero con `?auto=0` para no actualizar el GPS. · C
- [ ] **M-092** `DbHelper.FijarConexionParaPruebas` sigue `public` — (`DbHelper.cs:51`) porque lo usan `herramientas/cuadro-status-general` y `herramientas/perfil-llegada`: darles `InternalsVisibleTo` y hacerlo `internal`. · C
- [ ] **M-093** El mapa del viaje (`ObtenerRuta`) toma la sesión mientras carga — con la caché fría son hasta 10 s en que otra pantalla del mismo usuario espera. Pasarlo a un servicio sin sesión de escritura si molesta; lo mismo los Excel grandes. · C
- [ ] **M-094** Mapa del viaje: primera apertura tras publicar lenta — la caché en memoria está vacía y hoy y ayer se piden al API: guardar también el día en curso en disco con su hora. · C
- [ ] **M-095** `FacturasFlete.aspx` tardó 11 s una vez — en el sitio local (0,6 s las demás); no se repitió. Si vuelve, medir sus tres consultas. · C
- [ ] **M-096** Seguimiento: cortar la ventana de la descarga al volver a base — hoy se leen hasta 12 días desde la salida de planta. · C
- [ ] **M-097** Trazabilidad: geocercas en BD con editor en mapa — hoy en código (`GeocercasTrazabilidad`); cuando haya más zonas que Sullana y Trujillo. · C
- [ ] **M-098** Mapa: teselas de OpenStreetMap — `tile.openstreetmap.org` sirve para poco tráfico; si el sistema crece, proveedor con clave (CARTO, MapTiler). · C
- [ ] **M-099** Serilog 2.12 → 4.x — opcional (`packages.config:17`); requiere dependencias nuevas y binding redirects. · C
- [ ] **M-100** Script de ingreso `herramientas/sitio-local/entrar-sgv.ps1` — que lea la credencial de `privado/CREDENCIALES_USUARIOS.txt` como `probar-importacion.ps1` y se nombre en su README, para probar por HTTP sin abrir el archivo (salió de U-002). · C

## Reglas

Las mismas en `docs/PLAN_MEJORAS_SGV.md`, `docs/PENDIENTES_USUARIO.md`, `docs/DATOS_POR_CORREGIR.md`, `docs/PRUEBAS_PENDIENTES.md`, `docs/DECISIONES_NEGOCIO.md` y `CLAUDE.md` ("Keeping the plan up to date"). `CodigoFuenteLimpiezaTests` comprueba una parte: sin `[x]`, formato del id, ids únicos y no mayores que el último usado, tamaño del plan y números con forma de DNI en `docs/`.

| Qué es | Dónde va |
|---|---|
| Mejora de código o de la BD | `docs/PLAN_MEJORAS_SGV.md` (`M-`) |
| Lo que solo puede hacer el usuario: decisión, credencial, panel externo, dato o documento por conseguir | `docs/PENDIENTES_USUARIO.md` (`U-`) |
| Registros que corregir, completar o cargar | `docs/DATOS_POR_CORREGIR.md` (`D-`) |
| Probar en el navegador, en celular, con un rol, pedido o conductor real, o en el sitio publicado | `docs/PRUEBAS_PENDIENTES.md` (`P-`) |
| Regla de negocio acordada con el usuario | `docs/DECISIONES_NEGOCIO.md` (`DN-`) |
| Paso del paso a producción (`sgvTransporte`) | `docs/PASO_A_PRODUCCION.md` |
| Paso de cada publicación | `herramientas/publicar/README.md`, "Antes y después de publicar" |

- **Id nuevo**: el siguiente al `Último id usado` de la cabecera del documento, y esa línea se actualiza en el mismo cambio. Los ids no se reutilizan (tampoco los de ítems borrados o retirados) ni se renumeran.
- **Formato**: cada pendiente es su propio `- [ ] **M-001** título — qué y dónde (archivo si se sabe) · A/B/C`, nunca escondido en el texto de otro ni solo en un chat. A: riesgo de datos, dinero o seguridad, o bloquea el uso real; B: valor claro; C: menor.
- **Al terminar**, el ítem se borra de su documento en el mismo commit (nunca `[x]`): el commit es el historial (`git log`; `docs/archivo/HISTORIAL_2026.md` está congelado y no se escribe). Si deja algo pendiente, va con un id nuevo. Las decisiones (`DN-`) no se borran: si una cambia, se marca "reemplazada por DN-yy".
- **En el mismo commit que una feature, fix, migración o SP**: la línea "Última actualización" de `docs/PLAN_MEJORAS_SGV.md` (fecha y commit anterior), y `docs/PASO_A_PRODUCCION.md` o el README de publicar si afecta a producción o a la publicación.
- **Sin datos personales en `docs/`**: ni DNI, carnets, teléfonos, fechas de nacimiento, direcciones ni nombres de conductores (tampoco contraseñas ni tokens): usar el rol, el id, la placa o el número del documento.
- El plan pesa menos de 80 KB.
