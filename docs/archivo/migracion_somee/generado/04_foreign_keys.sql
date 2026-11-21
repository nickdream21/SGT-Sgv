-- ============================================================================
--  SGT-SGV  Â·  Migracion a somee Pro  Â·  Generado automaticamente el 2026-07-08 09:33
--  Origen: sgvActualizada (somee)  Â·  NO editar a mano salvo necesidad.
--  Ejecutar CONECTADO A LA NUEVA BASE DE PRODUCCION.
-- ============================================================================
-- 04 Â· FOREIGN KEYS (ejecutar DESPUES de cargar los datos maestros)
SET NOCOUNT ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Abastecim__idCar__0D0FEE32')
ALTER TABLE [dbo].[AbastecimientoCombustible] ADD CONSTRAINT [FK__Abastecim__idCar__0D0FEE32] FOREIGN KEY ([idCarreta]) REFERENCES [dbo].[Carreta] ([idCarreta]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Abastecim__idCar__3B40CD36')
ALTER TABLE [dbo].[AbastecimientoCombustible] ADD CONSTRAINT [FK__Abastecim__idCar__3B40CD36] FOREIGN KEY ([idCarreta]) REFERENCES [dbo].[Carreta] ([idCarreta]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Abastecim__idCon__0E04126B')
ALTER TABLE [dbo].[AbastecimientoCombustible] ADD CONSTRAINT [FK__Abastecim__idCon__0E04126B] FOREIGN KEY ([idConductor]) REFERENCES [dbo].[Conductor] ([idConductor]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Abastecim__idCon__395884C4')
ALTER TABLE [dbo].[AbastecimientoCombustible] ADD CONSTRAINT [FK__Abastecim__idCon__395884C4] FOREIGN KEY ([idConductor]) REFERENCES [dbo].[Conductor] ([idConductor]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Abastecim__idLug__0EF836A4')
ALTER TABLE [dbo].[AbastecimientoCombustible] ADD CONSTRAINT [FK__Abastecim__idLug__0EF836A4] FOREIGN KEY ([idLugarAbastecimiento]) REFERENCES [dbo].[LugarAbastecimiento] ([idLugarAbastecimiento]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Abastecim__idLug__3A4CA8FD')
ALTER TABLE [dbo].[AbastecimientoCombustible] ADD CONSTRAINT [FK__Abastecim__idLug__3A4CA8FD] FOREIGN KEY ([idLugarAbastecimiento]) REFERENCES [dbo].[LugarAbastecimiento] ([idLugarAbastecimiento]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Abastecim__idOrd__0FEC5ADD')
ALTER TABLE [dbo].[AbastecimientoCombustible] ADD CONSTRAINT [FK__Abastecim__idOrd__0FEC5ADD] FOREIGN KEY ([idOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([idOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Abastecim__idOrd__3D2915A8')
ALTER TABLE [dbo].[AbastecimientoCombustible] ADD CONSTRAINT [FK__Abastecim__idOrd__3D2915A8] FOREIGN KEY ([idOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([idOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Abastecim__idRut__10E07F16')
ALTER TABLE [dbo].[AbastecimientoCombustible] ADD CONSTRAINT [FK__Abastecim__idRut__10E07F16] FOREIGN KEY ([idRuta]) REFERENCES [dbo].[Ruta] ([idRuta]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Abastecim__idRut__3C34F16F')
ALTER TABLE [dbo].[AbastecimientoCombustible] ADD CONSTRAINT [FK__Abastecim__idRut__3C34F16F] FOREIGN KEY ([idRuta]) REFERENCES [dbo].[Ruta] ([idRuta]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Abastecim__idTra__11D4A34F')
ALTER TABLE [dbo].[AbastecimientoCombustible] ADD CONSTRAINT [FK__Abastecim__idTra__11D4A34F] FOREIGN KEY ([idTracto]) REFERENCES [dbo].[Tracto] ([idTracto]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Abastecim__idTra__3864608B')
ALTER TABLE [dbo].[AbastecimientoCombustible] ADD CONSTRAINT [FK__Abastecim__idTra__3864608B] FOREIGN KEY ([idTracto]) REFERENCES [dbo].[Tracto] ([idTracto]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Abast_Camioneta')
ALTER TABLE [dbo].[AbastecimientoCombustible] ADD CONSTRAINT [FK_Abast_Camioneta] FOREIGN KEY ([idCamioneta]) REFERENCES [dbo].[camionetas] ([id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Abast_Volquete')
ALTER TABLE [dbo].[AbastecimientoCombustible] ADD CONSTRAINT [FK_Abast_Volquete] FOREIGN KEY ([idVolquete]) REFERENCES [dbo].[volquetes] ([id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Abastecimiento_TipoCarro')
ALTER TABLE [dbo].[AbastecimientoCombustible] ADD CONSTRAINT [FK_Abastecimiento_TipoCarro] FOREIGN KEY ([idTipoCarro]) REFERENCES [dbo].[TipoCarro] ([idTipoCarro]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Asignacion_Equipo')
ALTER TABLE [dbo].[AsignacionesMaquinaria] ADD CONSTRAINT [FK_Asignacion_Equipo] FOREIGN KEY ([idEquipo]) REFERENCES [dbo].[EquiposMaquinaria] ([idEquipo]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Asignacion_Obra')
ALTER TABLE [dbo].[AsignacionesMaquinaria] ADD CONSTRAINT [FK_Asignacion_Obra] FOREIGN KEY ([idObra]) REFERENCES [dbo].[Obras] ([idObra]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Asignacion_Operador')
ALTER TABLE [dbo].[AsignacionesMaquinaria] ADD CONSTRAINT [FK_Asignacion_Operador] FOREIGN KEY ([idOperador]) REFERENCES [dbo].[Operadores] ([idOperador]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Categoria__numer__3F115E1A')
ALTER TABLE [dbo].[CategoriasAdicionales] ADD CONSTRAINT [FK__Categoria__numer__3F115E1A] FOREIGN KEY ([numeroOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([numeroOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__CPIC__idFactura__40058253')
ALTER TABLE [dbo].[CPIC] ADD CONSTRAINT [FK__CPIC__idFactura__40058253] FOREIGN KEY ([idFactura]) REFERENCES [dbo].[Factura] ([idFactura]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__CPIC_Prod__idCPI__40F9A68C')
ALTER TABLE [dbo].[CPIC_Productos] ADD CONSTRAINT [FK__CPIC_Prod__idCPI__40F9A68C] FOREIGN KEY ([idCPIC]) REFERENCES [dbo].[CPIC] ([idCPIC]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__CPIC_Prod__idPro__41EDCAC5')
ALTER TABLE [dbo].[CPIC_Productos] ADD CONSTRAINT [FK__CPIC_Prod__idPro__41EDCAC5] FOREIGN KEY ([idProducto]) REFERENCES [dbo].[Producto] ([idProducto]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Departame__idPai__42E1EEFE')
ALTER TABLE [dbo].[Departamento] ADD CONSTRAINT [FK__Departame__idPai__42E1EEFE] FOREIGN KEY ([idPais]) REFERENCES [dbo].[Pais] ([idPais]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DescuentosReintegros_OrdenViaje')
ALTER TABLE [dbo].[DescuentosReintegros] ADD CONSTRAINT [FK_DescuentosReintegros_OrdenViaje] FOREIGN KEY ([numeroOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([numeroOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DespObra_Abast')
ALTER TABLE [dbo].[DespachoCombustibleObra] ADD CONSTRAINT [FK_DespObra_Abast] FOREIGN KEY ([idAbastecimientoOrigen]) REFERENCES [dbo].[AbastecimientoCombustible] ([idAbastecimientoCombustible]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DespObra_Conductor')
ALTER TABLE [dbo].[DespachoCombustibleObra] ADD CONSTRAINT [FK_DespObra_Conductor] FOREIGN KEY ([idConductor]) REFERENCES [dbo].[Conductor] ([idConductor]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DespObra_Obra')
ALTER TABLE [dbo].[DespachoCombustibleObra] ADD CONSTRAINT [FK_DespObra_Obra] FOREIGN KEY ([idObra]) REFERENCES [dbo].[Obras] ([idObra]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DespObra_Tracto')
ALTER TABLE [dbo].[DespachoCombustibleObra] ADD CONSTRAINT [FK_DespObra_Tracto] FOREIGN KEY ([idTracto]) REFERENCES [dbo].[Tracto] ([idTracto]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Despachos_Carreta')
ALTER TABLE [dbo].[Despachos] ADD CONSTRAINT [FK_Despachos_Carreta] FOREIGN KEY ([idCarreta]) REFERENCES [dbo].[Carreta] ([idCarreta]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Despachos_Cliente')
ALTER TABLE [dbo].[Despachos] ADD CONSTRAINT [FK_Despachos_Cliente] FOREIGN KEY ([idCliente]) REFERENCES [dbo].[Cliente] ([idCliente]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Despachos_Conductor')
ALTER TABLE [dbo].[Despachos] ADD CONSTRAINT [FK_Despachos_Conductor] FOREIGN KEY ([idConductor]) REFERENCES [dbo].[Conductor] ([idConductor]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Despachos_CPIC')
ALTER TABLE [dbo].[Despachos] ADD CONSTRAINT [FK_Despachos_CPIC] FOREIGN KEY ([idCPIC]) REFERENCES [dbo].[CPIC] ([idCPIC]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Despachos_Factura')
ALTER TABLE [dbo].[Despachos] ADD CONSTRAINT [FK_Despachos_Factura] FOREIGN KEY ([idFactura]) REFERENCES [dbo].[Factura] ([idFactura]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Despachos_OrdenViaje')
ALTER TABLE [dbo].[Despachos] ADD CONSTRAINT [FK_Despachos_OrdenViaje] FOREIGN KEY ([idOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([idOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Despachos_Producto')
ALTER TABLE [dbo].[Despachos] ADD CONSTRAINT [FK_Despachos_Producto] FOREIGN KEY ([idProducto]) REFERENCES [dbo].[Producto] ([idProducto]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Despachos_Tracto')
ALTER TABLE [dbo].[Despachos] ADD CONSTRAINT [FK_Despachos_Tracto] FOREIGN KEY ([idTracto]) REFERENCES [dbo].[Tracto] ([idTracto]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Despachos_ViajeProgreso')
ALTER TABLE [dbo].[Despachos] ADD CONSTRAINT [FK_Despachos_ViajeProgreso] FOREIGN KEY ([idViajeProgreso]) REFERENCES [dbo].[ViajesEnProgreso] ([idViajeProgreso]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DetalleAlimentacion_OrdenViaje')
ALTER TABLE [dbo].[DetalleAlimentacion] ADD CONSTRAINT [FK_DetalleAlimentacion_OrdenViaje] FOREIGN KEY ([numeroOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([numeroOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DetalleApoyoSeguridad_OrdenViaje')
ALTER TABLE [dbo].[DetalleApoyoSeguridad] ADD CONSTRAINT [FK_DetalleApoyoSeguridad_OrdenViaje] FOREIGN KEY ([numeroOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([numeroOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DetalleCombustible_OrdenViaje')
ALTER TABLE [dbo].[DetalleCombustible] ADD CONSTRAINT [FK_DetalleCombustible_OrdenViaje] FOREIGN KEY ([numeroOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([numeroOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DetalleEncapada_OrdenViaje')
ALTER TABLE [dbo].[DetalleEncapada] ADD CONSTRAINT [FK_DetalleEncapada_OrdenViaje] FOREIGN KEY ([numeroOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([numeroOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DetalleHospedaje_OrdenViaje')
ALTER TABLE [dbo].[DetalleHospedaje] ADD CONSTRAINT [FK_DetalleHospedaje_OrdenViaje] FOREIGN KEY ([numeroOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([numeroOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DetalleMovilidad_OrdenViaje')
ALTER TABLE [dbo].[DetalleMovilidad] ADD CONSTRAINT [FK_DetalleMovilidad_OrdenViaje] FOREIGN KEY ([numeroOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([numeroOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__DetalleOr__idGui__15A53433')
ALTER TABLE [dbo].[DetalleOrdenViaje] ADD CONSTRAINT [FK__DetalleOr__idGui__15A53433] FOREIGN KEY ([idGuia]) REFERENCES [dbo].[GuiasTransportista] ([idGuia]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__DetalleOr__idGui__43D61337')
ALTER TABLE [dbo].[DetalleOrdenViaje] ADD CONSTRAINT [FK__DetalleOr__idGui__43D61337] FOREIGN KEY ([idGuia]) REFERENCES [dbo].[GuiasTransportista] ([idGuia]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__DetalleOr__idPro__1699586C')
ALTER TABLE [dbo].[DetalleOrdenViaje] ADD CONSTRAINT [FK__DetalleOr__idPro__1699586C] FOREIGN KEY ([idProducto]) REFERENCES [dbo].[Producto] ([idProducto]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__DetalleOr__idPro__44CA3770')
ALTER TABLE [dbo].[DetalleOrdenViaje] ADD CONSTRAINT [FK__DetalleOr__idPro__44CA3770] FOREIGN KEY ([idProducto]) REFERENCES [dbo].[Producto] ([idProducto]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DetallePeajes_OrdenViaje')
ALTER TABLE [dbo].[DetallePeajes] ADD CONSTRAINT [FK_DetallePeajes_OrdenViaje] FOREIGN KEY ([numeroOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([numeroOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DetalleReparacionesVarios_OrdenViaje')
ALTER TABLE [dbo].[DetalleReparacionesVarios] ADD CONSTRAINT [FK_DetalleReparacionesVarios_OrdenViaje] FOREIGN KEY ([numeroOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([numeroOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DetalleSegmento_Producto')
ALTER TABLE [dbo].[DetalleSegmento] ADD CONSTRAINT [FK_DetalleSegmento_Producto] FOREIGN KEY ([idProducto]) REFERENCES [dbo].[Producto] ([idProducto]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DetalleSegmento_Segmento')
ALTER TABLE [dbo].[DetalleSegmento] ADD CONSTRAINT [FK_DetalleSegmento_Segmento] FOREIGN KEY ([idSegmento]) REFERENCES [dbo].[SegmentosOrdenViaje] ([idSegmento]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TicketEc_Ingreso')
ALTER TABLE [dbo].[DetalleTicketEcuador] ADD CONSTRAINT [FK_TicketEc_Ingreso] FOREIGN KEY ([idIngreso]) REFERENCES [dbo].[IngresoCombustibleEcuador] ([idIngreso]) ON DELETE CASCADE;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Distrito__idProv__45BE5BA9')
ALTER TABLE [dbo].[Distrito] ADD CONSTRAINT [FK__Distrito__idProv__45BE5BA9] FOREIGN KEY ([idProvincia]) REFERENCES [dbo].[Provincia] ([idProvincia]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DocumentosCPIC_CPIC')
ALTER TABLE [dbo].[DocumentosCPIC] ADD CONSTRAINT [FK_DocumentosCPIC_CPIC] FOREIGN KEY ([idCPIC]) REFERENCES [dbo].[CPIC] ([idCPIC]) ON DELETE CASCADE;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DocumentosFactura_Factura')
ALTER TABLE [dbo].[DocumentosFactura] ADD CONSTRAINT [FK_DocumentosFactura_Factura] FOREIGN KEY ([idFactura]) REFERENCES [dbo].[Factura] ([idFactura]) ON DELETE CASCADE;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Egresos__numeroO__489AC854')
ALTER TABLE [dbo].[Egresos] ADD CONSTRAINT [FK__Egresos__numeroO__489AC854] FOREIGN KEY ([numeroOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([numeroOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Factura_Cliente')
ALTER TABLE [dbo].[Factura] ADD CONSTRAINT [FK_Factura_Cliente] FOREIGN KEY ([idCliente]) REFERENCES [dbo].[Cliente] ([idCliente]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_FirmaDigital_Anulada')
ALTER TABLE [dbo].[FirmaDigital] ADD CONSTRAINT [FK_FirmaDigital_Anulada] FOREIGN KEY ([idFirmaAnulada]) REFERENCES [dbo].[FirmaDigital] ([idFirma]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__GuiasTran__numer__498EEC8D')
ALTER TABLE [dbo].[GuiasTransportista] ADD CONSTRAINT [FK__GuiasTran__numer__498EEC8D] FOREIGN KEY ([numeroOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([numeroOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__GuiasTran__numer__5FD33367')
ALTER TABLE [dbo].[GuiasTransportista] ADD CONSTRAINT [FK__GuiasTran__numer__5FD33367] FOREIGN KEY ([numeroOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([numeroOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_IngresoEc_Conductor')
ALTER TABLE [dbo].[IngresoCombustibleEcuador] ADD CONSTRAINT [FK_IngresoEc_Conductor] FOREIGN KEY ([idConductor]) REFERENCES [dbo].[Conductor] ([idConductor]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_IngresoEc_Tracto')
ALTER TABLE [dbo].[IngresoCombustibleEcuador] ADD CONSTRAINT [FK_IngresoEc_Tracto] FOREIGN KEY ([idTracto]) REFERENCES [dbo].[Tracto] ([idTracto]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_IngresoEc_Viaje')
ALTER TABLE [dbo].[IngresoCombustibleEcuador] ADD CONSTRAINT [FK_IngresoEc_Viaje] FOREIGN KEY ([idViajeProgreso]) REFERENCES [dbo].[ViajesEnProgreso] ([idViajeProgreso]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Ingresos__numero__4A8310C6')
ALTER TABLE [dbo].[Ingresos] ADD CONSTRAINT [FK__Ingresos__numero__4A8310C6] FOREIGN KEY ([numeroOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([numeroOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__IngresosA__numer__4B7734FF')
ALTER TABLE [dbo].[IngresosAdicionales] ADD CONSTRAINT [FK__IngresosA__numer__4B7734FF] FOREIGN KEY ([numeroOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([numeroOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Liquidaciones_OrdenViaje')
ALTER TABLE [dbo].[Liquidaciones] ADD CONSTRAINT [FK_Liquidaciones_OrdenViaje] FOREIGN KEY ([idOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([idOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Manifiest__idCPI__4D5F7D71')
ALTER TABLE [dbo].[Manifiesto] ADD CONSTRAINT [FK__Manifiest__idCPI__4D5F7D71] FOREIGN KEY ([idCPIC]) REFERENCES [dbo].[CPIC] ([idCPIC]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Manifiest__idGui__4C6B5938')
ALTER TABLE [dbo].[Manifiesto] ADD CONSTRAINT [FK__Manifiest__idGui__4C6B5938] FOREIGN KEY ([idGuia]) REFERENCES [dbo].[GuiasTransportista] ([idGuia]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Obras_ClienteObra')
ALTER TABLE [dbo].[Obras] ADD CONSTRAINT [FK_Obras_ClienteObra] FOREIGN KEY ([idClienteObra]) REFERENCES [dbo].[ClientesObra] ([idClienteObra]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OperacionesSubTramo_Cliente')
ALTER TABLE [dbo].[OperacionesSubTramo] ADD CONSTRAINT [FK_OperacionesSubTramo_Cliente] FOREIGN KEY ([idCliente]) REFERENCES [dbo].[Cliente] ([idCliente]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OperacionesSubTramo_CPIC')
ALTER TABLE [dbo].[OperacionesSubTramo] ADD CONSTRAINT [FK_OperacionesSubTramo_CPIC] FOREIGN KEY ([idCPIC]) REFERENCES [dbo].[CPIC] ([idCPIC]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OperacionesSubTramo_Factura')
ALTER TABLE [dbo].[OperacionesSubTramo] ADD CONSTRAINT [FK_OperacionesSubTramo_Factura] FOREIGN KEY ([idFactura]) REFERENCES [dbo].[Factura] ([idFactura]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OperacionesSubTramo_PlantaCarga')
ALTER TABLE [dbo].[OperacionesSubTramo] ADD CONSTRAINT [FK_OperacionesSubTramo_PlantaCarga] FOREIGN KEY ([idPlantaCarga]) REFERENCES [dbo].[PlantaCarga] ([idPlantaCarga]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OperacionesSubTramo_PlantaDescarga')
ALTER TABLE [dbo].[OperacionesSubTramo] ADD CONSTRAINT [FK_OperacionesSubTramo_PlantaDescarga] FOREIGN KEY ([idPlantaDescarga]) REFERENCES [dbo].[PlantaDescarga] ([idPlanta]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OperacionesSubTramo_SubTramo')
ALTER TABLE [dbo].[OperacionesSubTramo] ADD CONSTRAINT [FK_OperacionesSubTramo_SubTramo] FOREIGN KEY ([idSubTramo]) REFERENCES [dbo].[SubTramos] ([idSubTramo]) ON DELETE CASCADE;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__OrdenViaj__idCar__4707859D')
ALTER TABLE [dbo].[OrdenViaje] ADD CONSTRAINT [FK__OrdenViaj__idCar__4707859D] FOREIGN KEY ([idCarreta]) REFERENCES [dbo].[Carreta] ([idCarreta]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__OrdenViaj__idCar__5224328E')
ALTER TABLE [dbo].[OrdenViaje] ADD CONSTRAINT [FK__OrdenViaj__idCar__5224328E] FOREIGN KEY ([idCarreta]) REFERENCES [dbo].[Carreta] ([idCarreta]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__OrdenViaj__idCli__47FBA9D6')
ALTER TABLE [dbo].[OrdenViaje] ADD CONSTRAINT [FK__OrdenViaj__idCli__47FBA9D6] FOREIGN KEY ([idCliente]) REFERENCES [dbo].[Cliente] ([idCliente]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__OrdenViaj__idCli__4F47C5E3')
ALTER TABLE [dbo].[OrdenViaje] ADD CONSTRAINT [FK__OrdenViaj__idCli__4F47C5E3] FOREIGN KEY ([idCliente]) REFERENCES [dbo].[Cliente] ([idCliente]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__OrdenViaj__idCon__48EFCE0F')
ALTER TABLE [dbo].[OrdenViaje] ADD CONSTRAINT [FK__OrdenViaj__idCon__48EFCE0F] FOREIGN KEY ([idConductor]) REFERENCES [dbo].[Conductor] ([idConductor]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__OrdenViaj__idCon__503BEA1C')
ALTER TABLE [dbo].[OrdenViaje] ADD CONSTRAINT [FK__OrdenViaj__idCon__503BEA1C] FOREIGN KEY ([idConductor]) REFERENCES [dbo].[Conductor] ([idConductor]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__OrdenViaj__idPro__49E3F248')
ALTER TABLE [dbo].[OrdenViaje] ADD CONSTRAINT [FK__OrdenViaj__idPro__49E3F248] FOREIGN KEY ([idProducto]) REFERENCES [dbo].[Producto] ([idProducto]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__OrdenViaj__idPro__4E53A1AA')
ALTER TABLE [dbo].[OrdenViaje] ADD CONSTRAINT [FK__OrdenViaj__idPro__4E53A1AA] FOREIGN KEY ([idProducto]) REFERENCES [dbo].[Producto] ([idProducto]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__OrdenViaj__idTra__4AD81681')
ALTER TABLE [dbo].[OrdenViaje] ADD CONSTRAINT [FK__OrdenViaj__idTra__4AD81681] FOREIGN KEY ([idTracto]) REFERENCES [dbo].[Tracto] ([idTracto]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__OrdenViaj__idTra__51300E55')
ALTER TABLE [dbo].[OrdenViaje] ADD CONSTRAINT [FK__OrdenViaj__idTra__51300E55] FOREIGN KEY ([idTracto]) REFERENCES [dbo].[Tracto] ([idTracto]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrdenViaje_CPIC')
ALTER TABLE [dbo].[OrdenViaje] ADD CONSTRAINT [FK_OrdenViaje_CPIC] FOREIGN KEY ([idCPIC]) REFERENCES [dbo].[CPIC] ([idCPIC]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrdenViaje_FirmaAdmin')
ALTER TABLE [dbo].[OrdenViaje] ADD CONSTRAINT [FK_OrdenViaje_FirmaAdmin] FOREIGN KEY ([idFirmaAdmin]) REFERENCES [dbo].[FirmaDigital] ([idFirma]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrdenViaje_FirmaConductor')
ALTER TABLE [dbo].[OrdenViaje] ADD CONSTRAINT [FK_OrdenViaje_FirmaConductor] FOREIGN KEY ([idFirmaConductor]) REFERENCES [dbo].[FirmaDigital] ([idFirma]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrdenViaje_UsuarioAprobacion')
ALTER TABLE [dbo].[OrdenViaje] ADD CONSTRAINT [FK_OrdenViaje_UsuarioAprobacion] FOREIGN KEY ([idUsuarioAprobacion]) REFERENCES [dbo].[Usuarios] ([idUsuario]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrdenViaje_UsuarioRegistro')
ALTER TABLE [dbo].[OrdenViaje] ADD CONSTRAINT [FK_OrdenViaje_UsuarioRegistro] FOREIGN KEY ([idUsuarioRegistro]) REFERENCES [dbo].[Usuarios] ([idUsuario]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrdenViaje_ViajesEnProgreso')
ALTER TABLE [dbo].[OrdenViaje] ADD CONSTRAINT [FK_OrdenViaje_ViajesEnProgreso] FOREIGN KEY ([idViajeProgreso]) REFERENCES [dbo].[ViajesEnProgreso] ([idViajeProgreso]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrdenViajeAjuste_Firma')
ALTER TABLE [dbo].[OrdenViajeAjuste] ADD CONSTRAINT [FK_OrdenViajeAjuste_Firma] FOREIGN KEY ([idFirmaAdmin]) REFERENCES [dbo].[FirmaDigital] ([idFirma]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrdenViajeAjuste_OrdenViaje')
ALTER TABLE [dbo].[OrdenViajeAjuste] ADD CONSTRAINT [FK_OrdenViajeAjuste_OrdenViaje] FOREIGN KEY ([idOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([idOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Parte_Asignacion')
ALTER TABLE [dbo].[PartesDiariosTrabajo] ADD CONSTRAINT [FK_Parte_Asignacion] FOREIGN KEY ([idAsignacion]) REFERENCES [dbo].[AsignacionesMaquinaria] ([idAsignacion]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Parte_Operador')
ALTER TABLE [dbo].[PartesDiariosTrabajo] ADD CONSTRAINT [FK_Parte_Operador] FOREIGN KEY ([idOperador]) REFERENCES [dbo].[Operadores] ([idOperador]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PlantaCarga_Cliente')
ALTER TABLE [dbo].[PlantaCarga] ADD CONSTRAINT [FK_PlantaCarga_Cliente] FOREIGN KEY ([idCliente]) REFERENCES [dbo].[Cliente] ([idCliente]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PlantaDescarga_Cliente')
ALTER TABLE [dbo].[PlantaDescarga] ADD CONSTRAINT [FK_PlantaDescarga_Cliente] FOREIGN KEY ([idCliente]) REFERENCES [dbo].[Cliente] ([idCliente]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Producto_Cliente')
ALTER TABLE [dbo].[Producto] ADD CONSTRAINT [FK_Producto_Cliente] FOREIGN KEY ([idCliente]) REFERENCES [dbo].[Cliente] ([idCliente]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ProductosOperacion_Operacion')
ALTER TABLE [dbo].[ProductosOperacion] ADD CONSTRAINT [FK_ProductosOperacion_Operacion] FOREIGN KEY ([idOperacion]) REFERENCES [dbo].[OperacionesSubTramo] ([idOperacion]) ON DELETE CASCADE;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ProductosOperacion_Producto')
ALTER TABLE [dbo].[ProductosOperacion] ADD CONSTRAINT [FK_ProductosOperacion_Producto] FOREIGN KEY ([idProducto]) REFERENCES [dbo].[Producto] ([idProducto]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Provincia__idDep__55F4C372')
ALTER TABLE [dbo].[Provincia] ADD CONSTRAINT [FK__Provincia__idDep__55F4C372] FOREIGN KEY ([idDepartamento]) REFERENCES [dbo].[Departamento] ([idDepartamento]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Ruta_Cliente')
ALTER TABLE [dbo].[Ruta] ADD CONSTRAINT [FK_Ruta_Cliente] FOREIGN KEY ([idCliente]) REFERENCES [dbo].[Cliente] ([idCliente]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SegmentosOrdenViaje_Cliente')
ALTER TABLE [dbo].[SegmentosOrdenViaje] ADD CONSTRAINT [FK_SegmentosOrdenViaje_Cliente] FOREIGN KEY ([idCliente]) REFERENCES [dbo].[Cliente] ([idCliente]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SegmentosOrdenViaje_CPIC')
ALTER TABLE [dbo].[SegmentosOrdenViaje] ADD CONSTRAINT [FK_SegmentosOrdenViaje_CPIC] FOREIGN KEY ([idCPIC]) REFERENCES [dbo].[CPIC] ([idCPIC]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SegmentosOrdenViaje_Factura')
ALTER TABLE [dbo].[SegmentosOrdenViaje] ADD CONSTRAINT [FK_SegmentosOrdenViaje_Factura] FOREIGN KEY ([idFactura]) REFERENCES [dbo].[Factura] ([idFactura]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SegmentosOrdenViaje_OrdenViaje')
ALTER TABLE [dbo].[SegmentosOrdenViaje] ADD CONSTRAINT [FK_SegmentosOrdenViaje_OrdenViaje] FOREIGN KEY ([idOrdenViaje]) REFERENCES [dbo].[OrdenViaje] ([idOrdenViaje]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SubTramos_Liquidacion')
ALTER TABLE [dbo].[SubTramos] ADD CONSTRAINT [FK_SubTramos_Liquidacion] FOREIGN KEY ([idLiquidacion]) REFERENCES [dbo].[Liquidaciones] ([idLiquidacion]) ON DELETE CASCADE;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Usuarios_Conductor')
ALTER TABLE [dbo].[Usuarios] ADD CONSTRAINT [FK_Usuarios_Conductor] FOREIGN KEY ([idConductor]) REFERENCES [dbo].[Conductor] ([idConductor]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Usuarios_Operador')
ALTER TABLE [dbo].[Usuarios] ADD CONSTRAINT [FK_Usuarios_Operador] FOREIGN KEY ([idOperador]) REFERENCES [dbo].[Operadores] ([idOperador]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ViajesEnProgreso_Conductor')
ALTER TABLE [dbo].[ViajesEnProgreso] ADD CONSTRAINT [FK_ViajesEnProgreso_Conductor] FOREIGN KEY ([idConductor]) REFERENCES [dbo].[Conductor] ([idConductor]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ViajesEnProgreso_OrdenViaje')
ALTER TABLE [dbo].[ViajesEnProgreso] ADD CONSTRAINT [FK_ViajesEnProgreso_OrdenViaje] FOREIGN KEY ([idOrdenViajeGenerada]) REFERENCES [dbo].[OrdenViaje] ([idOrdenViaje]);
