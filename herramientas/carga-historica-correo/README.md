# Carga histórica de los documentos del correo

Herramienta de una sola vez (usada el 2026-10-22 para octubre). Para los viajes del seguimiento que ya
pasaron y no tenían despacho, registra un **despacho de CARGA por viaje** con lo que manda Vitapro por
correo (las ZIP `semanaNNdocumentosap<pedido>viviana.zip`): pedido, factura, guía, N° de entrega y de
transporte, destino y la carga (productos, lotes, bolsas, kg), leídos con el mismo lector que
RegistroDespacho.

- **Sin viaje del chofer** (`idViajeProgreso` NULL): el despacho no aparece en el dashboard del chofer ni
  entra a ninguna liquidación; sí en ListaDespachos. Estado `COMPLETADO`, usuario `historico.correo` y
  una observación que lo dice.
- **Chofer, tracto y carreta salen del seguimiento** (el Word), no de la guía: Vitapro emite la guía con la
  unidad pedida y no la vuelve a emitir si cambia. En los 12 casos con tracto distinto, el GPS (geocerca de
  la planta de Trujillo) confirmó que cargó el del seguimiento.
- Unión guía ↔ viaje: 1) mismo tracto (0–4 días después de la guía; prefiere el mismo pedido), 2) lo que
  queda del mismo pedido, por orden de N° de guía y hora del Word, 3) lo que queda entre los pedidos del
  mismo día de guía. Una guía sin viaje es una guía que no se usó (no se registra).
- Enlaza el despacho con su viaje (`idDespachoOrigen`, origen `DESPACHO`) **sin tocar las horas**; después
  se corre `sp_SE_SincronizarDespachos`, que pone en el viaje el pedido y el nombre completo del chofer.
- Todo en una transacción; `prueba` hace todo y lo deshace.

## Uso

```powershell
msbuild WebSGV.sln /p:Configuration=Debug /nologo /verbosity:minimal
.\herramientas\carga-historica-correo\Cargar.ps1 -Pedidos 4400133184,4400133228 -Modo prueba
.\herramientas\carga-historica-correo\Cargar.ps1 -Pedidos 4400133184,4400133228 -Modo aplicar
.\WebSGV\Database\sql.ps1 "EXEC sp_SE_SincronizarDespachos @desde='2026-10-01'"
```

La ventana de viajes es 01–20/10 por defecto; para otro período use `-Desde` y `-Hasta` (yyyy-MM-dd) y, si los ZIP
están en otra carpeta, `-Zips`. Ejemplo (2026-11-05, cargas de fines de setiembre y las 3 unidades que faltaban en
octubre, halladas con la conciliación de facturas de flete):

```powershell
.\herramientas\carga-historica-correo\Cargar.ps1 -Pedidos 4400132637,4400133136 -Modo prueba -Zips 'docs\DOCUMENTACION\DOC TRANSPORTE' -Desde 2026-09-15 -Hasta 2026-09-30
```

La guía se une a un viaje del tablero: si la unidad no tiene viaje (o su tracto está mal escrito, p. ej. "F60-779" con
cero), créelo o corríjalo antes (`sp_SE_GuardarDatosViaje`, como en `Database/Scripts/viajes_faltantes_octubre_2026-11-05.sql`).

## Completar las cargas programadas con varios ZIP a la vez (2026-11-02)

Para cuando se juntan varios correos: en vez de leer los ZIP uno por uno en RegistroDespacho,
`CompletarConCorreo` completa los despachos de CARGA programados con el Word que esperan documentos, con los
mismos servicios de la pantalla (`ImportacionPedidoService.Leer/Asociar`, `sp_CrearFactura`,
`sp_CompletarDespachoProgramado`). Va pedido por pedido en orden (fecha de guía) y cada guía toma un programado una
sola vez: 1) mismo tracto, del día de la guía a 2 días después (Vitapro emite la guía hasta 2 días antes de
cargar); 2) misma carreta; 3) cambio de tracto solo si quedan exactamente una guía y un programado en su ventana.
Chofer, tracto y carreta quedan los del programado (el Word manda). `prueba` hace todo dentro de una
`TransactionScope` y la deshace; `aplicar` la confirma (usuario `carga.correo`). Después: `sp_SE_SincronizarDespachos`.

`AdjuntarPdfFacturas` adjunta el PDF de la factura del ZIP a la factura ya registrada sin PDF (despachos de octubre
en adelante). Los archivos quedan en `WebSGV\Uploads\Factura\aaaa\mm\` de **esta PC**: copiarlos por FTP a la
misma ruta del sitio publicado. `AnalizarCorreo` solo lee: por pedido, factura y guías y con qué despacho se une cada una.

```powershell
.\herramientas\carga-historica-correo\Ejecutar.ps1 -Herramienta AnalizarCorreo
.\herramientas\carga-historica-correo\Ejecutar.ps1 -Herramienta CompletarConCorreo -Modo prueba
.\herramientas\carga-historica-correo\Ejecutar.ps1 -Herramienta CompletarConCorreo -Modo aplicar
.\WebSGV\Database\sql.ps1 "EXEC sp_SE_SincronizarDespachos @desde='2026-10-20'"
.\herramientas\carga-historica-correo\Ejecutar.ps1 -Herramienta AdjuntarPdfFacturas -Modo aplicar
```
