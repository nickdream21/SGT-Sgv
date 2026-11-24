# Decisiones de negocio de SGV

Este documento es la fuente de las **reglas de negocio acordadas con el usuario** que no se deducen solas del código:
qué significa "aprobada", qué se bloquea, qué se deja como está y por qué. Antes vivían solo en el historial del plan; el
detalle técnico de cada cambio (pruebas, revisiones, SP) sigue en `docs/archivo/HISTORIAL_2026.md` (congelado) y en
git (`git show <commit>`). Las reglas técnicas y de arquitectura están en `CLAUDE.md`; el flujo y los permisos por rol,
en `docs/FLUJO_DE_TRABAJO_SGV.md`; los clientes terceros, en `docs/CLIENTES_TERCEROS.md`.

**Cómo agregar una decisión**: una entrada nueva `### DN-xx` con el número siguiente al `Último id usado` (y esa línea
se actualiza en el mismo cambio), en el módulo que corresponda y en el índice, con
la regla en pocas líneas, `Desde:` (fecha y commit), `En el código:` (archivo:línea o clave de `Web.config`; "solo
procedimiento" si no la aplica el código) y `Estado:` (vigente / pendiente de reactivar / aplicada una vez / a confirmar). Si una
decisión cambia, no se borra: se marca "reemplazada por DN-yy" y se agrega la nueva. Nunca datos personales (DNI,
nombres, teléfonos, contraseñas): usar el rol ("un conductor", "la administradora de transporte").
Las demás reglas (dónde va cada cosa) están en "Reglas", al final.

Último id usado: DN-32 (la próxima decisión toma el siguiente; ver "Reglas" al final).

## Índice

| Id | Decisión |
|---|---|
| DN-01 | "Aprobada" = conformidad del conductor firmada |
| DN-02 | Saldos de conductores como neto por conductor y moneda |
| DN-03 | Los conductores liquidan en papel hasta que el usuario diga |
| DN-04 | Reenviar o retirar una liquidación anula la firma anterior |
| DN-05 | Viaje nacional: sin peajes ni dólares |
| DN-06 | Sin liquidaciones de prueba y número de orden que nunca se repite |
| DN-07 | "Dinero y pagos del viaje": apagada, con sus decisiones para reactivarla |
| DN-08 | Cambiar uno mismo la contraseña cierra las demás sesiones |
| DN-09 | Recuperar la contraseña por correo quita el cambio obligatorio |
| DN-10 | Recuperar contraseña sin revelar qué cuentas existen |
| DN-11 | Un lote se anula, no se elimina (D1) |
| DN-12 | Un despacho con liquidación no se anula ni cambia fecha, tipo o ámbito (D2) |
| DN-13 | Factura única por número y cliente; CPIC única por número (D3) |
| DN-14 | Guía sin coincidencia de tracto ni carreta: elegir el camión a mano (D5) |
| DN-15 | Finalizar lote es todo o nada, y se bloquea sin PDF o con guía repetida (D6) |
| DN-16 | Decisiones del bloque 4 aplicadas una sola vez (D4 y D7) |
| DN-17 | La placa de un vehículo con historial no se cambia |
| DN-18 | Los viajes del seguimiento nacen de los despachos |
| DN-19 | Inicio y término de carga estimados desde la salida de planta |
| DN-20 | Los 15 viajes antiguos que siguen en "Activos" se dejan como están |
| DN-21 | Un CPI por factura y un MCI por camión (observada, a confirmar) |
| DN-22 | Vitapro (ida): todo sale de la factura F222 y el packing list (observada, a confirmar) |
| DN-23 | Cargas y flete por camión de los demás clientes (observada, a confirmar) |
| DN-24 | En qué columna va el flete (observada, a confirmar) |
| DN-25 | Se borra el portador del sobre aunque la pantalla de dinero siga apagada |
| DN-26 | Permisos del SUPERVISOR y del administrador como funcionan hoy (opción recomendada, a confirmar) |
| DN-27 | Segundo factor: quién lo lleva, apagado por interruptor y cómo se recupera (opción recomendada, a confirmar) |
| DN-28 | Aspecto del sistema: dirección "Expediente", iconos solo en acciones y avisos, sin emojis |
| DN-29 | Contabilidad: liquidaciones aprobadas de los últimos 90 días al abrir, 50 por página (opción recomendada, a confirmar) |
| DN-30 | Gráficos con su paleta propia de datos; tarjetas grandes de totales fuera (aspecto aprobado de E5-12) |
| DN-31 | Pantallas de buscar y editar (E5-17): saldo por moneda, PDF firmado, filtros plegados en el celular (opción recomendada, a confirmar) |
| DN-32 | Factura y CPIC registradas a mano: número y fecha obligatorios, valor opcional |

---

## 1. Liquidaciones y firmas

### DN-01 — "Aprobada" = conformidad del conductor firmada
Una liquidación cuenta como aprobada solo cuando el conductor firmó su conformidad (`estadoAprobacion = 'APROBADO'`).
Una orden POR_CONFORMAR (revisada por el administrador pero sin conformidad) no aparece en Control de aprobadas,
Contabilidad, Reportes de órdenes ni Saldos de conductores.
- Desde: 2026-11-16, `9ccb1c8`.
- En el código: `WebSGV/Services/Liquidaciones/EstadosOrden.cs:16` (`Aprobado = "APROBADO"`, definición única).
- Estado: vigente.

### DN-02 — Saldos de conductores como neto por conductor y moneda
Los totales de Saldos de conductores se suman como neto por conductor y por moneda (lo que la empresa le debe menos
lo que él debe), iguales en pantalla y en Excel, con una fila "Total pendiente". El total suma solo lo pendiente:
lo ya saldado no se vuelve a cobrar ni pagar.
- Desde: 2026-11-16, `9ccb1c8`.
- En el código: `WebSGV/Services/Liquidaciones/SaldosConductorService.cs:264-266` (solo lo pendiente,
  "Total pendiente") y `:288-291` (`Totales`).
- Estado: vigente.

### DN-03 — Los conductores liquidan en papel hasta que el usuario diga
Mientras los conductores no usen el sistema, los viajes de liquidación abiertos se cierran solos en cada corrida del
GPS automático (estado CERRADO, usuario SISTEMA, motivo "Liquidado en papel…"), sin tocar despachos ni seguimiento.
Para que los conductores empiecen a liquidar en el sistema se cambia la clave a `true` y se publica.
- Desde: 2026-11-13, `f2e77ab`.
- En el código: `WebSGV/Web.config:38` (`Liquidaciones.ConductoresEnSistema` = `false`);
  `WebSGV/Services/Liquidaciones/CierreViajesSinLiquidacion.cs:21-27`.
- Estado: vigente (se cambia cuando el usuario presente el sistema a los conductores).

### DN-04 — Reenviar o retirar una liquidación anula la firma anterior
Si el conductor corrige y reenvía una orden rechazada, la declaración firmada antes (sobre otros montos) se anula y
debe firmar de nuevo. Lo mismo al retirar una liquidación: primero se anula su firma.
- Desde: 2026-11-15, `2f81ea9`.
- En el código: `WebSGV/Database/StoredProcedures/sp_DC_ActualizarOrdenViajeConductor.sql:38` (solo REABIERTO) y
  `:41-60` (anulación de las firmas vigentes); al retirar,
  `WebSGV/Database/StoredProcedures/sp_DC_RetirarLiquidacion.sql:93-115` (anula la declaración antes de borrar la
  orden).
- Estado: vigente.

### DN-05 — Viaje nacional: sin peajes ni dólares
En un viaje nacional el servidor pone en cero los peajes y los montos en dólares, solo si la página también mostraba
nacional. Si el GPS no respondió o la página mostraba internacional, rechaza el envío con un aviso en vez de borrar
dinero en silencio.
- Desde: 2026-11-15, `e028677`.
- En el código: `WebSGV/Services/Conductor/ReglasLiquidacionConductor.cs:79` (`DecidirAmbito`).
- Estado: vigente.

### DN-06 — Sin liquidaciones de prueba y número de orden que nunca se repite
Antes de que entren los conductores no debe quedar ninguna liquidación anterior (las de prueba se retiraron con
`Database/Scripts/retirar_liquidaciones_prueba_2026-11-16.sql`). El correlativo `OV-AAAA-NNNNNN` no reutiliza un
número ya emitido aunque la orden se haya borrado (considera firmas, movimientos y auditoría).
- Desde: 2026-11-16, `873947a`.
- En el código: `WebSGV/Database/StoredProcedures/sp_DC_GenerarNumeroOrden.sql:6-12` y `:39`.
- Estado: vigente.

### DN-07 — "Dinero y pagos del viaje": apagada, con sus decisiones para reactivarla
La pantalla para registrar en el momento las entregas de dinero al conductor y los pagos directos a proveedores del
viaje está apagada (la empresa sabe cuánto entrega y prefiere enfocarse en otras cosas); no se borró nada. Si se
reactiva, valen las decisiones del 2026-11-11, a confirmar antes con el usuario:
- sin confirmación "Recibí" del conductor: basta su conformidad final, que lista cada entrega;
- registran administración y monitoreo;
- el pago del saldo de la liquidación queda fuera por ahora;
- sobre sin conductor portador: no se registra quién lleva el sobre; la gerencia sabe cuánto entregó y el conductor
  lo acepta al firmar.
- Desde: 2026-11-11, `56c0d81` (decisiones) y `1e3652d` (sobre sin portador); apagada 2026-11-13, `f2e77ab`.
- En el código: `WebSGV/Web.config:33` (`MovimientosViaje.Habilitado` = `false`);
  `WebSGV/Services/Liquidaciones/MovimientosViajeService.cs:61-67`.
- Estado: pendiente de reactivar (U-040 en `docs/PENDIENTES_USUARIO.md`). La condición de no borrar
  `MovimientoDineroViaje.idConductorPortador` mientras siga apagada fue reemplazada por DN-25.

### DN-25 — Se borra el portador del sobre aunque la pantalla de dinero siga apagada
El usuario aprobó el 2026-11-21 borrar `MovimientoDineroViaje.idConductorPortador` sin esperar a reactivar "Dinero y
pagos del viaje": desde la migración 43 la pantalla ya no registra portador (DN-07) y solo un movimiento de prueba
anulado lo tenía. Al reactivarla se prueba la pantalla de nuevo (P-029).
- Desde: 2026-11-21, migración `59_BorrarRevocarFirmaYPortador.sql`.
- En el código: `WebSGV/Database/StoredProcedures/sp_LD_RegistrarMovimientoViaje.sql` (sin `@idConductorPortador`).
- Estado: aplicada en pruebas (`sgvActualizada`); en producción con `docs/PASO_A_PRODUCCION.md`.

### DN-29 — Contabilidad: liquidaciones aprobadas de los últimos 90 días al abrir, 50 por página (opción recomendada, a confirmar)
La pantalla de liquidaciones aprobadas de contabilidad abre con las de los últimos 90 días por fecha de salida, de a 50
por página, con "Todo el historial" para quitar el rango. Antes traía toda la historia sin paginar.
- Desde: 2026-11-22. U-095: el usuario pidió terminar la Etapa 5 tomando las decisiones pendientes con la opción
  recomendada; queda a confirmar por él.
- En el código: `WebSGV/Services/Liquidaciones/LiquidacionesContabilidadService.cs` (`DiasRangoPorDefecto`, `TamanoPagina`)
  y `WebSGV/Scripts/paginas/LiquidacionesAprobadasContabilidad.js` (E5-10).
- Estado: vigente, a confirmar.

### DN-30 — Gráficos con su paleta propia de datos; tarjetas grandes de totales fuera (aspecto aprobado de E5-12)
El usuario aprobó el aspecto "Expediente" de E5-12 sobre las comparaciones antes y después (Gestión de liquidaciones, panel
del conductor, Lista de despachos, Login y Seguimiento de viaje). Los gráficos (Reporte de cargas, tablero de Exportación)
conservan sus colores vivos para distinguir las series: no pasan a la paleta de la dirección. Las tarjetas grandes de totales
de Lista de despachos "se notan muy IA": se quitan como ya dice DN-28 (los contadores van en las pestañas), en E5-18 (M-082).
- Desde: 2026-11-23.
- En el código: `WebSGV/Content/sgv-base.css` (E5-12); gráficos en `ReporteCargas` y `DashboardExportacion`; tarjetas, E5-18.
- Estado: vigente.

### DN-31 — Pantallas de buscar y editar (E5-17): saldo por moneda, PDF firmado, filtros plegados en el celular (opción recomendada, a confirmar)
Decisiones tomadas por el orquestador con la opción recomendada mientras el usuario no estaba (pidió terminar la Etapa 5
decidiendo lo necesario):
1. Saldos de conductores: cuando las dos monedas van en sentidos opuestos se nombran las dos ("A descontar S/ 100.00 ·
   A reintegrar US$ 50.00"); antes el sentido salía solo de la moneda en soles. Las cifras no cambian (M-070).
2. El número de liquidación de Saldos abre el PDF firmado y guardado (`DescargarPdfOrdenViaje.aspx`, verificado por hash),
   no el detalle de la orden, que no abre para la oficina (M-071).
3. El filtro de acción de Auditoría sale de las acciones que hay en la BD, con su nombre en español donde lo hay y el
   código en las demás (M-135; nombrarlas todas queda en M-165).
4. En el celular (≤ 767 px) los filtros, los gráficos y las cargas de archivos arrancan plegados (`data-plegar-movil`); en
   escritorio, abiertos.
5. En los selectores de Bandeja y Registro de despacho, la opción que ya completa otra guía o transporte se rotula
   ("ya lo completa la guía …"), no se quita: el servidor sigue rechazando el doble uso al registrar (M-080).
- Desde: 2026-11-24.
- En el código: `Services/Liquidaciones/SaldosConductorService.cs` (`Sentido`, `TextoSaldo`, `ClaseSentido`),
  `Views/SaldosConductores.aspx`, `Services/Auditoria/ConsultaAuditoriaService.cs` (`ObtenerAcciones`, `NombreAccion`),
  `Scripts/paginas/SiteMaster.js` (`data-plegar-movil`), `BandejaDocumentosService.GuiaQueYaLoCompleta` y
  `ProgramacionCarga.TransporteQueLoCompleta`.
- Estado: vigente, a confirmar.

## 2. Usuarios y acceso

### DN-08 — Cambiar uno mismo la contraseña cierra las demás sesiones
Cuando un usuario cambia su propia contraseña, la sesión desde la que la cambió sigue abierta y se cierran todas sus
otras sesiones (otro navegador, otro celular).
- Desde: 2026-11-16, `825ae9d`.
- En el código: `WebSGV/Site.Master.cs:295-307` (nuevo `selloSesion` guardado en la sesión actual).
- Estado: vigente.

### DN-09 — Recuperar la contraseña por correo quita el cambio obligatorio
Quien restablece su contraseña con el enlace del correo ya eligió una clave nueva: no se le vuelve a pedir el cambio
obligatorio, y sus sesiones abiertas se cierran.
- Desde: 2026-11-16, `825ae9d`.
- En el código: `WebSGV/Views/RestablecerContrasena.aspx.cs:188-190` (`requiereCambioContrasena = 0`,
  `selloSesion = NEWID()`).
- Estado: vigente.

### DN-10 — Recuperar contraseña sin revelar qué cuentas existen
La pantalla de recuperación responde siempre el mismo mensaje y en el mismo tiempo, exista o no la cuenta y tenga o
no correo; si no tiene correo, se pide al administrador. Hay límites por cuenta (3 por hora) y por IP (10 en 15 min).
- Desde: 2026-11-15, `d8a6b4d`.
- En el código: `WebSGV/Helpers/SolicitudRecuperacion.cs:27` (`MensajeUnico`) y `:31-34` (límites).
- Estado: vigente.

### DN-26 — Permisos del SUPERVISOR y del administrador como funcionan hoy (opción recomendada, a confirmar)
El SUPERVISOR no entra a Facturas de flete, Productos ni Rutas (solo administración), y el administrador no tiene el
panel del conductor: la matriz de permisos se ajustó a lo que ya hacían las páginas y el menú; nadie ganó ni perdió acceso.
- Desde: 2026-11-22, E4-3. El usuario pidió terminar la Etapa 4 sin decidir U-081; se aplicó la opción que el pendiente
  recomendaba. Si decide otra cosa, se cambia la matriz y `GuardasAccesoTests.AccesoEsperado` a propósito (U-090).
- En el código: `WebSGV/Views/RolesHelper.cs` (`Matriz`), guardas de `FacturasFlete`, `RegistroProductos` y `RegistroRutas`.
- Estado: vigente (observada, a confirmar).

### DN-27 — Segundo factor: quién lo lleva, apagado por interruptor y cómo se recupera (opción recomendada, a confirmar)
Lo llevan administración, SUPERVISOR, CONTABILIDAD y ADMINISTRADOR DE SISTEMA, nunca el conductor (ya reescribe su
contraseña para firmar). Mientras el interruptor `Seguridad.ExigirSegundoFactor` esté apagado es voluntario (se activa en
"Mi seguridad"), pero quien lo activó da el código siempre; encendido, quien no lo tiene lo activa antes de entrar. Hay 10
códigos de recuperación de un solo uso; si también se pierden, otro ADMINISTRADOR DE SISTEMA lo quita (o el script de
emergencia). Sin "recordar este equipo".
- Desde: 2026-11-22, E4-14. El usuario pidió terminar la Etapa 4 sin decidir U-079; se aplicaron sus opciones recomendadas
  y, para "obligatorio o voluntario", el interruptor apagado (encenderlo es U-092).
- En el código: `WebSGV/Views/RolesHelper.cs` (`ExigeSegundoFactor`), `WebSGV/Services/Usuarios/SegundoFactorReglas.cs`
  (`Decidir`), `WebSGV/Web.config` (`Seguridad.ExigirSegundoFactor`).
- Estado: vigente (observada, a confirmar).

## 3. Despachos y documentos

Las decisiones D1–D7 del bloque 4 (2026-11-16) quedan en DN-11 a DN-16.

### DN-11 — Un lote se anula, no se elimina (D1)
La única acción es "Anular lote": los despachos quedan ANULADO y `activo = 0`, y salen de la liquidación del conductor
y del seguimiento de exportación. El botón "Eliminar" se quitó.
- Desde: 2026-11-16, `9bc0fec`.
- En el código: `WebSGV/Database/StoredProcedures/sp_LD_AnularLote.sql:110-113` (ANULADO, `activo = 0`);
  `WebSGV/Views/ListaDespachos.aspx:843-846`.
- Estado: vigente.

### DN-12 — Un despacho con liquidación no se anula ni cambia fecha, tipo o ámbito (D2)
Si el viaje del despacho ya tiene orden de viaje (liquidación del conductor), no se puede anular el lote ni cambiar
su fecha, tipo de operación o ámbito (el código también bloquea planta y conductor,
`sp_LD_ActualizarDespachoEnLote.sql:100-104`).
- Desde: 2026-11-16, `9bc0fec` (anular) y 2026-11-17, `8c0b59b` (editar lote).
- En el código: `WebSGV/Database/StoredProcedures/sp_LD_AnularLote.sql:107` (THROW 50010);
  `WebSGV/Database/StoredProcedures/sp_LD_ActualizarDespachoEnLote.sql:89-104` (THROW 50024).
- Estado: vigente.

### DN-13 — Factura única por número y cliente; CPIC única por número (D3)
Una factura se identifica por (número, cliente) y una CPIC por su número. La CPIC es una por factura comercial
(embarque) y cada camión lleva su MCI. Escribir un número que ya existe en un lote significa "estos despachos van con
ese documento": se enlaza, no se renombra.
- Desde: 2026-11-17, `56c758c`.
- En el código: `WebSGV/Database/Schema/49_UnicidadFacturaCpic.sql:6-7` (`UX_CPIC_numeroCPIC`,
  `UX_Factura_numero_cliente`).
- Estado: vigente.

### DN-14 — Guía sin coincidencia de tracto ni carreta: elegir el camión a mano (D5)
En la Bandeja de documentos y en RegistroDespacho, una guía que no coincide con ningún despacho programado por tracto
ni por carreta nunca se asigna "por descarte": queda "Elija el camión" hasta que el usuario elija o la marque como no
registrar.
- Desde: 2026-11-17, `2e413ce`.
- En el código: Bandeja, `WebSGV/Services/Despachos/BandejaDocumentosService.cs:44` (`MensajePorElegir`) y `:947`;
  RegistroDespacho, `WebSGV/Services/Despachos/ProgramacionCarga.cs:156-163` (con el Word la única guía sobrante se
  propone, pero el camión lo elige el usuario).
- Estado: vigente.

### DN-15 — Finalizar lote es todo o nada, y se bloquea sin PDF o con guía repetida (D6)
"Finalizar lote" crea factura, CPIC y despachos en una sola transacción: si algo falla no queda nada a medias, y
reintentar no duplica documentos. Antes de escribir revalida y, por decisión del usuario, se bloquea:
- si el PDF temporal adjuntado al iniciar el lote ya no está ("Cancele el lote y vuelva a iniciarlo…");
- si una guía de remitente se repite (mismo cliente + guía + tipo, en la BD o dentro del lote).
- Desde: 2026-11-17, `c92546c`.
- En el código: `WebSGV/Services/Despachos/FinalizarLoteService.cs:40` (servicio), `:48-56` (`Revalidar`),
  `:119-129` (documentos que ya no están), `:136-139` (`GuiasRepetidasEnLote`) y `:166-174` (`GuiasRepetidasEnBd`).
- Estado: vigente.

### DN-16 — Decisiones del bloque 4 aplicadas una sola vez (D4 y D7)
D4: `EditarDespacho.aspx` se borró (la edición se hace desde "Editar lote" en ListaDespachos). D7: los despachos de
prueba `demo-video` 2353–2355 quedaron ANULADO y `activo = 0`.
- Desde: D4 2026-11-17, `8c0b59b`; D7 2026-11-17 (datos de sgvActualizada).
- En el código: D4, la página ya no existe en `WebSGV/Views/`; D7, solo procedimiento (datos).
- Estado: aplicada una vez.

### DN-17 — La placa de un vehículo con historial no se cambia
Un tracto o semirremolque que ya tiene despachos u otros registros no puede cambiar de placa, salvo de formato (se corrompería el
historial); las placas son únicas sin importar guiones ("ABC123" = "ABC-123"). Decisión del orquestador por
delegación del usuario (bloque 5, D2).
- Desde: 2026-11-17, `c5c4d21` (y `b06752f` para placas únicas).
- En el código: `WebSGV/Services/Vehiculos/VehiculoService.cs:21-26` y `:212` (cambiar solo el formato siempre se
  permite).
- Estado: vigente.

### DN-32 — Factura y CPIC registradas a mano: número y fecha obligatorios, valor opcional
En "Documentos del lote" de ListaDespachos, al registrar una factura o una CPIC a mano se exigen el número y la fecha;
el "Valor total" de la factura y el "Valor del flete" de la CPIC son opcionales (quedan en 0), porque el valor lo toma
la Bandeja de documentos al leer el ZIP del pedido (factura y CPIC). En RegistroDespacho sigue exigiéndose un valor
mayor que 0. Decisión del usuario (2026-11-24, E5-20).
- Desde: 2026-11-24, E5-20b.
- En el código: `WebSGV/Views/ListaDespachos.aspx` (grupos `vgFacturaLote` y `vgCpicLote`, sin validador en los valores);
  `WebSGV/Services/Despachos/BandejaDocumentosService.cs:1141` y `:1367` (valor leído de los documentos).
- Estado: vigente.

## 4. Seguimiento de exportación

### DN-18 — Los viajes del seguimiento nacen de los despachos
Cada despacho de carga de exportación crea su viaje en el seguimiento, que se completa con el despacho de descarga en
Ecuador; el despacho manda en pedido, carreta, tractos y choferes, y un despacho anulado deja el viaje cancelado. Lo
importado del Excel antiguo ya no sirve para seguir los viajes de ahora: el tablero lo oculta salvo que se marque
"Incluir los importados del Excel".
- Desde: 2026-10-20, `a22748b` (migración `32_SeguimientoDesdeDespachos`).
- En el código: `WebSGV/Database/StoredProcedures/sp_SE_SincronizarDespachos.sql:134-140` (viajes nuevos desde
  `#carga`) y `:160-163` (despacho de carga anulado → CANCELADO; `:90-93`, lo mismo para terceros);
  `WebSGV/Views/Exportacion/RegistroSeguimiento.aspx:102`.
- Estado: vigente.

### DN-19 — Inicio y término de carga estimados desde la salida de planta
Cuando no hay horas reales de carga: término = salida de planta − 10 min; inicio = término − 12 min; nunca antes del
ingreso a planta (si la estadía es corta, se reparte en proporción). Se calculan al leer, no se guardan; si alguien
escribe la hora real, manda la escrita.
- Desde: 2026-11-13, `f2e77ab` (regla del usuario).
- En el código: `WebSGV/Services/Exportacion/HitosSeguimiento.cs:69-86` (`CargaEstimada`).
- Estado: vigente.

### DN-20 — Los 15 viajes antiguos que siguen en "Activos" se dejan como están
Los 15 viajes de setiembre y octubre que quedaron en "Activos" del seguimiento no se cierran a mano (decisión del
usuario). El aviso "Falta inicio y término de carga" que se decidió dejar ese mismo día quedó superado al día
siguiente por DN-19.
- Desde: 2026-11-12, `03afb60`.
- En el código: solo procedimiento.
- Estado: vigente.

## 5. CPI y manifiesto (MCI) de exportación

**Estas no son decisiones del usuario sino reglas observadas**: se sacaron el 2026-10-23 de 51 CPI y 288 MCI hechos
a mano por la administradora de transporte, para cuando el sistema los genere (M-037 en
`docs/PLAN_MEJORAS_SGV.md`). Varias siguen preguntadas en U-023 (`docs/PENDIENTES_USUARIO.md`):
hay que confirmarlas con ella antes de programarlas. Hoy se arman a mano en Excel y el sistema solo guarda el escaneo.

### DN-21 — Un CPI por factura y un MCI por camión (observada)
Un CPI por factura y un MCI por camión; el CPI suma los sacos y pesos de sus MCI. Los correlativos son los propios de
la administradora de transporte. Si hay varias versiones del archivo vale la última modificada. El BORRADOR se hace
al llegar la factura, sin el día; el día se pone cuando salen los camiones.
- Desde: 2026-10-23, `991d44b`.
- En el código: solo procedimiento (generación pendiente).
- Estado: a confirmar con la administradora (U-023 en `docs/PENDIENTES_USUARIO.md`).

### DN-22 — Vitapro (ida): todo sale de la factura F222 y el packing list (observada)
Casilla 16 = total CPT; flete = TOT. CARGO; peso bruto = total del packing list. Flete por camión:
1188 bolsas × 1,55 = 1841,40, repartido en tramos de 0,725 / 0,25 / 0,575 por bolsa.
- Desde: 2026-10-23, `991d44b`.
- En el código: solo procedimiento.
- Estado: a confirmar con la administradora (U-023 en `docs/PENDIENTES_USUARIO.md`).

### DN-23 — Cargas y flete por camión de los demás clientes (observada)
- Cartavio: 620 bolsas de 50 kg por camión (+0,2 kg de tara); flete 2540 por camión (¿subió a 2600?) + 50 de otros gastos
  por CPI.
- Novopan (Quito → Piura/Trujillo, CPT Tumbes): 14 pallets y 715 por camión; volumen en m3 y anexo de productos.
- Inbalnor → Vitapro: 1300 sacos de 25 kg (+0,1 kg por saco en el bruto); 1700 por camión.
- Tadel / Productos Pesqueros (harina de Manta → Vitapro): 2000 sacos; 7200.
- Desde: 2026-10-23, `991d44b`.
- En el código: solo procedimiento.
- Estado: a confirmar con la administradora (U-023 en `docs/PENDIENTES_USUARIO.md`).

### DN-24 — En qué columna va el flete (observada)
El flete va en la columna del remitente (Vitapro ida, Cartavio, Novopan) o en la del destinatario (los que traen
carga a Vitapro); cómo lo decide ella sigue preguntado. Errores de la copia a mano que la generación debe evitar: año equivocado en el CPI o el MCI, tramos
que no suman el flete, peso bruto mal escrito y nombres de producto mal copiados.
- Desde: 2026-10-23, `991d44b`.
- En el código: solo procedimiento.
- Estado: a confirmar con la administradora (U-023 en `docs/PENDIENTES_USUARIO.md`).

## 6. Interfaz

### DN-28 — Aspecto del sistema: dirección "Expediente", iconos solo en acciones y avisos, sin emojis
El sistema es formal y no debe parecer una plantilla ni algo generado: aspecto de documento administrativo (hoja blanca
sobre gris, un solo azul tinta, tablas tipo planilla con los montos alineados, estados como sello rectangular con texto),
con una tipografía propia servida desde el sitio. Cero emojis y exclamaciones en los mensajes. Los iconos se quedan solo
en los botones compactos de acción de las tablas y en los avisos de error o advertencia (más los funcionales: cerrar, menú
del celular, desplegable); nunca en títulos, etiquetas de campo, menú, pestañas ni botones con texto. Sin tarjetas grandes
de totales: los contadores van en las pestañas. Se conservan los colores suaves de estado y los montos en verde o rojo.
- Desde: 2026-11-22. El usuario eligió "A y 1" entre tres direcciones (Expediente, Sobrio de sistema, Tablero operativo)
  y tres reglas de iconos, mostradas sobre la pantalla de Gestión de liquidaciones y la del conductor en el celular.
- En el código: paleta, tipografía y foco en `WebSGV/Content/sgv-base.css` (E5-12, commit 5589b6f); emojis y redacción (E5-24a, 221bcfb) e iconos solo en acciones de tabla, avisos de error/advertencia y funcionales (E5-24b), fijados por
  `WebSGV.Tests/InterfazFormalTests.cs` (topes de iconos que solo bajan; las tarjetas KPI de E5-17/E5-18 en lista cerrada).
- Estado: vigente.

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
