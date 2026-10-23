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

La ventana de viajes (01–20/10) está fija en `CargaHistorica.cs`: cámbiela para otro mes.
