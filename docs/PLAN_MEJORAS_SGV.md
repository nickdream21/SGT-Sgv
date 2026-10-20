# Plan de mejoras SGV — estado y tareas pendientes

Última actualización: **2026-10-19**, al día con el destino de descarga por unidad (`400682f`, migración 30) más el reporte GPS por placas (`ReporteGps.aspx`, sin migración; sección 4). Este documento es el punto de partida para seguir trabajando
desde cualquier máquina. No contiene contraseñas: solo indica dónde están.

## 1. Estado actual

| | Al empezar (2026-10-07) | Ahora |
|---|---|---|
| Puntuación general | 5.5 / 10 | **8 / 10** |
| Tests | 215 | **457** en verde (2026-10-18; incluye los de integración, varios escriben con rollback) |
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

Detalle de reglas y convenciones nuevas: ver `CLAUDE.md`.

## 4. Tareas pendientes (en pruebas)

Ordenadas por valor. Marca `[x]` al terminar. Todo pendiente va como su propio `[ ]` en esta lista
(no escondido dentro del texto de una tarea hecha).

### Abiertas

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

1. `aplicar-migraciones.ps1 -Entorno produccion -SoloListar`, revisar, y luego con `-ConfirmoProduccion`
   (hoy 01–30; incluye la 16, hora de Perú: el código nuevo usa `dbo.fn_AhoraPeru()`; la 20 amplía
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
   de producción poner el `OnwayAuth0ClientSecret` nuevo (cambió el 2026-10-09).
7. Tras publicar: en `RegistroChoferes` → "Actualizar con la lista" con
   `LISTA DE CHOFERES ACTUALES (corregida).xlsx` (no hay migración; Victor Marzal con DNI 00243179, Yépez y
   Rivas con carnet de extranjería). Requiere el `sp_RegistrarConductorImportado` del paso 2.
8. Con `connectionStrings` de producción, correr los tests de integración **no** está permitido (solo
   aceptan pruebas): verificar a mano el login con cada rol y "Ver historial" en RegistroDespacho.
