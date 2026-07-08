-- ============================================================================
--  SGT-SGV  Â·  Migracion a somee Pro  Â·  Generado automaticamente el 2026-07-08 09:33
--  Origen: sgvActualizada (somee)  Â·  NO editar a mano salvo necesidad.
--  Ejecutar CONECTADO A LA NUEVA BASE DE PRODUCCION.
-- ============================================================================
-- 03 Â· INDICES SECUNDARIOS
SET NOCOUNT ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Abast_Camioneta' AND object_id = OBJECT_ID('dbo.AbastecimientoCombustible'))
CREATE NONCLUSTERED INDEX [IX_Abast_Camioneta] ON [dbo].[AbastecimientoCombustible] ([idCamioneta] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Abast_Volquete' AND object_id = OBJECT_ID('dbo.AbastecimientoCombustible'))
CREATE NONCLUSTERED INDEX [IX_Abast_Volquete] ON [dbo].[AbastecimientoCombustible] ([idVolquete] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditoriaLog_Accion' AND object_id = OBJECT_ID('dbo.AuditoriaLog'))
CREATE NONCLUSTERED INDEX [IX_AuditoriaLog_Accion] ON [dbo].[AuditoriaLog] ([Accion] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditoriaLog_FechaHora' AND object_id = OBJECT_ID('dbo.AuditoriaLog'))
CREATE NONCLUSTERED INDEX [IX_AuditoriaLog_FechaHora] ON [dbo].[AuditoriaLog] ([FechaHora] DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditoriaLog_IdUsuario' AND object_id = OBJECT_ID('dbo.AuditoriaLog'))
CREATE NONCLUSTERED INDEX [IX_AuditoriaLog_IdUsuario] ON [dbo].[AuditoriaLog] ([IdUsuario] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditoriaLog_TablaAfectada' AND object_id = OBJECT_ID('dbo.AuditoriaLog'))
CREATE NONCLUSTERED INDEX [IX_AuditoriaLog_TablaAfectada] ON [dbo].[AuditoriaLog] ([TablaAfectada] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UC_CarnetExtranjeria' AND object_id = OBJECT_ID('dbo.Conductor'))
CREATE UNIQUE NONCLUSTERED INDEX [UC_CarnetExtranjeria] ON [dbo].[Conductor] ([carnetExtranjeria] ASC) WHERE ([carnetExtranjeria] IS NOT NULL);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UC_DNI' AND object_id = OBJECT_ID('dbo.Conductor'))
CREATE UNIQUE NONCLUSTERED INDEX [UC_DNI] ON [dbo].[Conductor] ([DNI] ASC) WHERE ([DNI] IS NOT NULL);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DespObra_Fecha' AND object_id = OBJECT_ID('dbo.DespachoCombustibleObra'))
CREATE NONCLUSTERED INDEX [IX_DespObra_Fecha] ON [dbo].[DespachoCombustibleObra] ([fechaSalidaGrifo] DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DespObra_Obra' AND object_id = OBJECT_ID('dbo.DespachoCombustibleObra'))
CREATE NONCLUSTERED INDEX [IX_DespObra_Obra] ON [dbo].[DespachoCombustibleObra] ([idObra] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Despachos_Cliente' AND object_id = OBJECT_ID('dbo.Despachos'))
CREATE NONCLUSTERED INDEX [IX_Despachos_Cliente] ON [dbo].[Despachos] ([idCliente] ASC, [estadoDespacho] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Despachos_Conductor' AND object_id = OBJECT_ID('dbo.Despachos'))
CREATE NONCLUSTERED INDEX [IX_Despachos_Conductor] ON [dbo].[Despachos] ([idConductor] ASC, [fechaDespacho] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Despachos_Conductor_Fecha' AND object_id = OBJECT_ID('dbo.Despachos'))
CREATE NONCLUSTERED INDEX [IX_Despachos_Conductor_Fecha] ON [dbo].[Despachos] ([idConductor] ASC, [fechaDespacho] DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Despachos_Fecha' AND object_id = OBJECT_ID('dbo.Despachos'))
CREATE NONCLUSTERED INDEX [IX_Despachos_Fecha] ON [dbo].[Despachos] ([fechaDespacho] ASC, [estadoDespacho] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Despachos_Fecha_Estado' AND object_id = OBJECT_ID('dbo.Despachos'))
CREATE NONCLUSTERED INDEX [IX_Despachos_Fecha_Estado] ON [dbo].[Despachos] ([fechaDespacho] ASC, [estadoDespacho] ASC, [activo] ASC) INCLUDE ([idConductor], [idTracto], [idCarreta]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Despachos_FechaDespacho' AND object_id = OBJECT_ID('dbo.Despachos'))
CREATE NONCLUSTERED INDEX [IX_Despachos_FechaDespacho] ON [dbo].[Despachos] ([fechaDespacho] ASC, [estadoDespacho] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Despachos_Recursos' AND object_id = OBJECT_ID('dbo.Despachos'))
CREATE NONCLUSTERED INDEX [IX_Despachos_Recursos] ON [dbo].[Despachos] ([idConductor] ASC, [idTracto] ASC, [idCarreta] ASC, [fechaDespacho] ASC) WHERE ([activo]=(1));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Despachos_ViajeProgreso' AND object_id = OBJECT_ID('dbo.Despachos'))
CREATE NONCLUSTERED INDEX [IX_Despachos_ViajeProgreso] ON [dbo].[Despachos] ([idViajeProgreso] ASC) INCLUDE ([fechaDespacho], [tipoOperacion]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DetalleAlimentacion_NumeroOrdenViaje' AND object_id = OBJECT_ID('dbo.DetalleAlimentacion'))
CREATE NONCLUSTERED INDEX [IX_DetalleAlimentacion_NumeroOrdenViaje] ON [dbo].[DetalleAlimentacion] ([numeroOrdenViaje] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DetalleApoyoSeguridad_NumeroOrdenViaje' AND object_id = OBJECT_ID('dbo.DetalleApoyoSeguridad'))
CREATE NONCLUSTERED INDEX [IX_DetalleApoyoSeguridad_NumeroOrdenViaje] ON [dbo].[DetalleApoyoSeguridad] ([numeroOrdenViaje] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DetalleCombustible_NumeroOrdenViaje' AND object_id = OBJECT_ID('dbo.DetalleCombustible'))
CREATE NONCLUSTERED INDEX [IX_DetalleCombustible_NumeroOrdenViaje] ON [dbo].[DetalleCombustible] ([numeroOrdenViaje] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DetalleEncapada_NumeroOrdenViaje' AND object_id = OBJECT_ID('dbo.DetalleEncapada'))
CREATE NONCLUSTERED INDEX [IX_DetalleEncapada_NumeroOrdenViaje] ON [dbo].[DetalleEncapada] ([numeroOrdenViaje] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DetalleHospedaje_NumeroOrdenViaje' AND object_id = OBJECT_ID('dbo.DetalleHospedaje'))
CREATE NONCLUSTERED INDEX [IX_DetalleHospedaje_NumeroOrdenViaje] ON [dbo].[DetalleHospedaje] ([numeroOrdenViaje] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DetalleMovilidad_NumeroOrdenViaje' AND object_id = OBJECT_ID('dbo.DetalleMovilidad'))
CREATE NONCLUSTERED INDEX [IX_DetalleMovilidad_NumeroOrdenViaje] ON [dbo].[DetalleMovilidad] ([numeroOrdenViaje] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DetallePeajes_OrdenViaje' AND object_id = OBJECT_ID('dbo.DetallePeajes'))
CREATE NONCLUSTERED INDEX [IX_DetallePeajes_OrdenViaje] ON [dbo].[DetallePeajes] ([numeroOrdenViaje] ASC) INCLUDE ([estacion], [montoSoles], [montoDolares]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DetalleReparacionesVarios_NumeroOrdenViaje' AND object_id = OBJECT_ID('dbo.DetalleReparacionesVarios'))
CREATE NONCLUSTERED INDEX [IX_DetalleReparacionesVarios_NumeroOrdenViaje] ON [dbo].[DetalleReparacionesVarios] ([numeroOrdenViaje] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DetalleSegmento_Segmento' AND object_id = OBJECT_ID('dbo.DetalleSegmento'))
CREATE NONCLUSTERED INDEX [IX_DetalleSegmento_Segmento] ON [dbo].[DetalleSegmento] ([idSegmento] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentosCPIC_FechaSubida' AND object_id = OBJECT_ID('dbo.DocumentosCPIC'))
CREATE NONCLUSTERED INDEX [IX_DocumentosCPIC_FechaSubida] ON [dbo].[DocumentosCPIC] ([fechaSubida] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentosCPIC_idCPIC' AND object_id = OBJECT_ID('dbo.DocumentosCPIC'))
CREATE NONCLUSTERED INDEX [IX_DocumentosCPIC_idCPIC] ON [dbo].[DocumentosCPIC] ([idCPIC] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentosFactura_FechaSubida' AND object_id = OBJECT_ID('dbo.DocumentosFactura'))
CREATE NONCLUSTERED INDEX [IX_DocumentosFactura_FechaSubida] ON [dbo].[DocumentosFactura] ([fechaSubida] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentosFactura_idFactura' AND object_id = OBJECT_ID('dbo.DocumentosFactura'))
CREATE NONCLUSTERED INDEX [IX_DocumentosFactura_idFactura] ON [dbo].[DocumentosFactura] ([idFactura] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FirmaDigital_Documento' AND object_id = OBJECT_ID('dbo.FirmaDigital'))
CREATE NONCLUSTERED INDEX [IX_FirmaDigital_Documento] ON [dbo].[FirmaDigital] ([tipoDocumento] ASC, [idDocumento] ASC, [estadoFirma] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FirmaDigital_Hash' AND object_id = OBJECT_ID('dbo.FirmaDigital'))
CREATE NONCLUSTERED INDEX [IX_FirmaDigital_Hash] ON [dbo].[FirmaDigital] ([hashDocumento] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FirmaDigital_Usuario' AND object_id = OBJECT_ID('dbo.FirmaDigital'))
CREATE NONCLUSTERED INDEX [IX_FirmaDigital_Usuario] ON [dbo].[FirmaDigital] ([idUsuarioFirmante] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Liquidaciones_OrdenViaje' AND object_id = OBJECT_ID('dbo.Liquidaciones'))
CREATE NONCLUSTERED INDEX [IX_Liquidaciones_OrdenViaje] ON [dbo].[Liquidaciones] ([idOrdenViaje] ASC) INCLUDE ([tipo], [descripcion]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OperacionesSubTramo_Cliente' AND object_id = OBJECT_ID('dbo.OperacionesSubTramo'))
CREATE NONCLUSTERED INDEX [IX_OperacionesSubTramo_Cliente] ON [dbo].[OperacionesSubTramo] ([idCliente] ASC) INCLUDE ([esInternacional]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OperacionesSubTramo_SubTramo' AND object_id = OBJECT_ID('dbo.OperacionesSubTramo'))
CREATE NONCLUSTERED INDEX [IX_OperacionesSubTramo_SubTramo] ON [dbo].[OperacionesSubTramo] ([idSubTramo] ASC) INCLUDE ([tipoOperacion], [idCliente]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrdenViaje_idViajeProgreso' AND object_id = OBJECT_ID('dbo.OrdenViaje'))
CREATE NONCLUSTERED INDEX [IX_OrdenViaje_idViajeProgreso] ON [dbo].[OrdenViaje] ([idViajeProgreso] ASC) WHERE ([idViajeProgreso] IS NOT NULL);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrdenViajeAjuste_OrdenViaje' AND object_id = OBJECT_ID('dbo.OrdenViajeAjuste'))
CREATE NONCLUSTERED INDEX [IX_OrdenViajeAjuste_OrdenViaje] ON [dbo].[OrdenViajeAjuste] ([idOrdenViaje] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductosOperacion_Operacion' AND object_id = OBJECT_ID('dbo.ProductosOperacion'))
CREATE NONCLUSTERED INDEX [IX_ProductosOperacion_Operacion] ON [dbo].[ProductosOperacion] ([idOperacion] ASC) INCLUDE ([idProducto], [cantidadBolsas]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SegmentosOrdenViaje_Cliente' AND object_id = OBJECT_ID('dbo.SegmentosOrdenViaje'))
CREATE NONCLUSTERED INDEX [IX_SegmentosOrdenViaje_Cliente] ON [dbo].[SegmentosOrdenViaje] ([idCliente] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SegmentosOrdenViaje_CPIC' AND object_id = OBJECT_ID('dbo.SegmentosOrdenViaje'))
CREATE NONCLUSTERED INDEX [IX_SegmentosOrdenViaje_CPIC] ON [dbo].[SegmentosOrdenViaje] ([idCPIC] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SegmentosOrdenViaje_Factura' AND object_id = OBJECT_ID('dbo.SegmentosOrdenViaje'))
CREATE NONCLUSTERED INDEX [IX_SegmentosOrdenViaje_Factura] ON [dbo].[SegmentosOrdenViaje] ([idFactura] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SegmentosOrdenViaje_GuiaCliente' AND object_id = OBJECT_ID('dbo.SegmentosOrdenViaje'))
CREATE NONCLUSTERED INDEX [IX_SegmentosOrdenViaje_GuiaCliente] ON [dbo].[SegmentosOrdenViaje] ([guiaCliente] ASC) WHERE ([guiaCliente] IS NOT NULL);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SegmentosOrdenViaje_GuiaTransportista' AND object_id = OBJECT_ID('dbo.SegmentosOrdenViaje'))
CREATE NONCLUSTERED INDEX [IX_SegmentosOrdenViaje_GuiaTransportista] ON [dbo].[SegmentosOrdenViaje] ([guiaTransportista] ASC) WHERE ([guiaTransportista] IS NOT NULL);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SegmentosOrdenViaje_Manifiesto' AND object_id = OBJECT_ID('dbo.SegmentosOrdenViaje'))
CREATE NONCLUSTERED INDEX [IX_SegmentosOrdenViaje_Manifiesto] ON [dbo].[SegmentosOrdenViaje] ([manifiesto] ASC) WHERE ([manifiesto] IS NOT NULL);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SegmentosOrdenViaje_OrdenViaje' AND object_id = OBJECT_ID('dbo.SegmentosOrdenViaje'))
CREATE NONCLUSTERED INDEX [IX_SegmentosOrdenViaje_OrdenViaje] ON [dbo].[SegmentosOrdenViaje] ([idOrdenViaje] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SE_fhProgramacion' AND object_id = OBJECT_ID('dbo.SeguimientoExportacion'))
CREATE NONCLUSTERED INDEX [IX_SE_fhProgramacion] ON [dbo].[SeguimientoExportacion] ([fhProgramacion] ASC) INCLUDE ([cliente], [bodegaNacional], [bodegaDescarga], [estado]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeguimientoExportacion_Cliente' AND object_id = OBJECT_ID('dbo.SeguimientoExportacion'))
CREATE NONCLUSTERED INDEX [IX_SeguimientoExportacion_Cliente] ON [dbo].[SeguimientoExportacion] ([cliente] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeguimientoExportacion_ConductorOrigen' AND object_id = OBJECT_ID('dbo.SeguimientoExportacion'))
CREATE NONCLUSTERED INDEX [IX_SeguimientoExportacion_ConductorOrigen] ON [dbo].[SeguimientoExportacion] ([conductorOrigen] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeguimientoExportacion_Estado' AND object_id = OBJECT_ID('dbo.SeguimientoExportacion'))
CREATE NONCLUSTERED INDEX [IX_SeguimientoExportacion_Estado] ON [dbo].[SeguimientoExportacion] ([estado] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeguimientoExportacion_FechaRegistro' AND object_id = OBJECT_ID('dbo.SeguimientoExportacion'))
CREATE NONCLUSTERED INDEX [IX_SeguimientoExportacion_FechaRegistro] ON [dbo].[SeguimientoExportacion] ([fechaRegistro] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SubTramos_Liquidacion' AND object_id = OBJECT_ID('dbo.SubTramos'))
CREATE NONCLUSTERED INDEX [IX_SubTramos_Liquidacion] ON [dbo].[SubTramos] ([idLiquidacion] ASC) INCLUDE ([numeroSubTramo], [tipoOperacion]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SubTramos_TipoOperacion' AND object_id = OBJECT_ID('dbo.SubTramos'))
CREATE NONCLUSTERED INDEX [IX_SubTramos_TipoOperacion] ON [dbo].[SubTramos] ([tipoOperacion] ASC) INCLUDE ([origen], [destino]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UC_PlacaTracto' AND object_id = OBJECT_ID('dbo.Tracto'))
CREATE UNIQUE NONCLUSTERED INDEX [UC_PlacaTracto] ON [dbo].[Tracto] ([placaTracto] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ViajesEnProgreso_Conductor_Estado' AND object_id = OBJECT_ID('dbo.ViajesEnProgreso'))
CREATE NONCLUSTERED INDEX [IX_ViajesEnProgreso_Conductor_Estado] ON [dbo].[ViajesEnProgreso] ([idConductor] ASC, [estadoViaje] ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ViajesEnProgreso_FechaActividad' AND object_id = OBJECT_ID('dbo.ViajesEnProgreso'))
CREATE NONCLUSTERED INDEX [IX_ViajesEnProgreso_FechaActividad] ON [dbo].[ViajesEnProgreso] ([fechaUltimaActividad] DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ViajesEnProgreso_FechaCierre' AND object_id = OBJECT_ID('dbo.ViajesEnProgreso'))
CREATE NONCLUSTERED INDEX [IX_ViajesEnProgreso_FechaCierre] ON [dbo].[ViajesEnProgreso] ([fechaCierre] ASC) WHERE ([fechaCierre] IS NOT NULL);
