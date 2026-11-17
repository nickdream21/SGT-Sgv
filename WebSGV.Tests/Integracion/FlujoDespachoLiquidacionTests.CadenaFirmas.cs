using System;
using System.Collections.Generic;
using System.Data;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Conductor;
using WebSGV.Services.Despachos;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Cadena de firmas y estados de la liquidación (auditoría de seguridad, tarea 2):
    /// el conductor solo liquida sus viajes a su nombre, una declaración se firma una vez, el
    /// reenvío tras un rechazo y el retiro anulan la firma anterior (visible en la verificación
    /// pública) y la administración solo rechaza lo que aún no revisó.
    /// Todo dentro de <see cref="TransaccionDePrueba"/>; los pasos que fallan van al final.
    /// </summary>
    public partial class FlujoDespachoLiquidacionTests
    {
        [FactBD]
        public void EnviarLiquidacion_ConViajePrincipalDeOtroConductor_SeRechaza()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViajePropio = CrearDespacho(datos, NuevoLote(datos), out _);
                int idViajeAjeno = RegistroDespachoService.CrearNuevoViajeProgreso(datos.IdOtroConductor, "Viaje de otro conductor (test)", "test");

                // El SP ya no entrega datos de un viaje ajeno (antes devolvía su conductor).
                Assert.Empty(DashboardConductorService.ObtenerDatosViajeParaLiquidacion(idViajeAjeno, datos.IdConductor).Rows);
                Assert.Single(DashboardConductorService.ObtenerDatosViajeParaLiquidacion(idViajePropio, datos.IdConductor).Rows);

                var input = NuevaLiquidacion(datos, idViajePropio);
                input.IdViajeProgreso = idViajeAjeno;   // viaje principal manipulado; los activos son los suyos

                var ex = Assert.Throws<ErrorNegocioException>(() => LiquidacionConductorService.EnviarLiquidacion(input));
                Assert.Contains("viajes activos", ex.Message);
                Assert.Equal(0, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM OrdenViaje WHERE numeroOrdenViaje = @n", DbHelper.Param("@n", input.NumeroOrdenViaje))));
                Assert.Equal("ABIERTO", RegistroDespachoService.ObtenerInfoViaje(idViajePropio).EstadoViaje);
            }
        }

        [FactBD]
        public void EnviarLiquidacion_IdConductorDistintoAlDeLaSesion_SeRechaza()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);

                var input = NuevaLiquidacion(datos, idViaje);
                input.IdConductor = datos.IdOtroConductor;   // la orden quedaría a nombre de otro

                var ex = Assert.Throws<ErrorNegocioException>(() => LiquidacionConductorService.EnviarLiquidacion(input));
                Assert.Contains("conductor de la sesión", ex.Message);
                Assert.Equal(0, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM OrdenViaje WHERE numeroOrdenViaje = @n", DbHelper.Param("@n", input.NumeroOrdenViaje))));
                Assert.Equal("ABIERTO", RegistroDespachoService.ObtenerInfoViaje(idViaje).EstadoViaje);
            }
        }

        [FactBD]
        public void RechazarYReliquidar_AnulaLaDeclaracionYExigeNuevaFirma()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                var input = NuevaLiquidacion(datos, idViaje);
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(input);
                string codigo = CodigoVerificacion.Generar();
                int idDeclaracion = FirmarComoConductor(idOrden, datos.IdUsuarioConductor, codigo);

                DataTable rechazo = LiquidacionesPendientesService.Rechazar(input.NumeroOrdenViaje, datos.IdUsuarioAdmin, "Corregir alimentación (test)");
                Assert.Equal(1, Convert.ToInt32(rechazo.Rows[0]["Resultado"]));

                // Rechazada (REABIERTO) no se firma: primero se corrige y se reenvía.
                Assert.Equal(0, IntentarFirmarComoConductor(idOrden, datos.IdUsuarioConductor, null, out _, out string mensaje));
                Assert.Contains("rechazada", mensaje);

                // Reenvío con otros montos: la declaración anterior se anula y se desvincula.
                input.EsReliquidacion = true;
                input.AlimentacionSoles = 120m;
                Assert.Equal(idOrden, LiquidacionConductorService.EnviarLiquidacion(input));

                DataRow ov = Fila("SELECT estadoAprobacion, idFirmaConductor, rutaPdfFirmado, hashPdfFirmado FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden);
                Assert.Equal("PENDIENTE", ov["estadoAprobacion"]);
                Assert.Equal(DBNull.Value, ov["idFirmaConductor"]);
                Assert.Equal(DBNull.Value, ov["rutaPdfFirmado"]);
                Assert.Equal(DBNull.Value, ov["hashPdfFirmado"]);

                DataTable anulacion = DbHelper.ConsultarTabla(
                    "SELECT estadoFirma, etapa, idUsuarioFirmante, idDocumento FROM FirmaDigital WHERE idFirmaAnulada = @id",
                    DbHelper.Param("@id", idDeclaracion));
                Assert.Single(anulacion.Rows);
                Assert.Equal("A", anulacion.Rows[0]["estadoFirma"]);
                Assert.Equal("ANULACION", anulacion.Rows[0]["etapa"]);
                Assert.Equal(datos.IdUsuarioConductor, Convert.ToInt32(anulacion.Rows[0]["idUsuarioFirmante"]));
                Assert.Equal(idOrden.ToString(), anulacion.Rows[0]["idDocumento"]);

                // La verificación pública muestra la declaración anterior como anulada.
                DataRow verificacion = DbHelper.ConsultarTablaSp("sp_LD_VerificarDocumento",
                    DbHelper.Param("@codigoVerificacion", codigo)).Rows[0];
                Assert.True(Convert.ToBoolean(verificacion["anulada"]));

                // Sin firma nueva la administración no la puede enviar a conformidad (se llama al SP, que
                // no hace ROLLBACK, para poder seguir en la misma transacción de la prueba).
                DataRow envio = DbHelper.ConsultarTablaSp("sp_LD_EnviarLiquidacionAConformidad",
                    DbHelper.Param("@numeroOrdenViaje", input.NumeroOrdenViaje),
                    DbHelper.Param("@idUsuarioAprobacion", datos.IdUsuarioAdmin)).Rows[0];
                Assert.Equal(0, Convert.ToInt32(envio["Resultado"]));
                Assert.Contains("firma", (string)envio["Mensaje"]);

                // Con la firma nueva, sí.
                int idNuevaDeclaracion = FirmarComoConductor(idOrden, datos.IdUsuarioConductor, CodigoVerificacion.Generar());
                Assert.NotEqual(idDeclaracion, idNuevaDeclaracion);
                Assert.Equal(idNuevaDeclaracion, Convert.ToInt32(Fila("SELECT idFirmaConductor FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["idFirmaConductor"]));
                var r = LiquidacionesPendientesService.EnviarAConformidad(idOrden, AjustesDePrueba(), null, datos.IdUsuarioAdmin);
                Assert.True(r.Exito, r.Mensaje);
                Assert.Equal("POR_CONFORMAR", Fila("SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["estadoAprobacion"]);
            }
        }

        [FactBD]
        public void FirmarDeclaracionDosVeces_LaSegundaSeRechaza()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(NuevaLiquidacion(datos, idViaje));
                int idPrimera = FirmarComoConductor(idOrden, datos.IdUsuarioConductor);

                Assert.Equal(0, IntentarFirmarComoConductor(idOrden, datos.IdUsuarioConductor, null, out int idSegunda, out string mensaje));
                Assert.Equal(0, idSegunda);
                Assert.Contains("ya está firmada", mensaje);

                Assert.Equal(idPrimera, Convert.ToInt32(Fila("SELECT idFirmaConductor FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["idFirmaConductor"]));
                Assert.Equal(1, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM FirmaDigital WHERE tipoDocumento = 'ORDEN_VIAJE' AND idDocumento = @id AND etapa = 'DECLARACION'",
                    DbHelper.Param("@id", idOrden.ToString()))));
            }
        }

        [FactBD]
        public void RetirarLiquidacionFirmada_AnulaSuFirma()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(NuevaLiquidacion(datos, idViaje));
                string codigo = CodigoVerificacion.Generar();
                int idDeclaracion = FirmarComoConductor(idOrden, datos.IdUsuarioConductor, codigo);

                var r = DashboardConductorService.RetirarLiquidacion(idOrden, datos.IdConductor);
                Assert.Equal(1, r.Resultado);
                Assert.Equal(0, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM OrdenViaje WHERE idOrdenViaje = @id", DbHelper.Param("@id", idOrden))));
                Assert.Equal("ABIERTO", RegistroDespachoService.ObtenerInfoViaje(idViaje).EstadoViaje);

                // La firma no se borra: queda su anulación (append-only) con el motivo del retiro.
                DataTable anulacion = DbHelper.ConsultarTabla(
                    "SELECT estadoFirma, etapa, motivoAnulacion FROM FirmaDigital WHERE idFirmaAnulada = @id",
                    DbHelper.Param("@id", idDeclaracion));
                Assert.Single(anulacion.Rows);
                Assert.Equal("A", anulacion.Rows[0]["estadoFirma"]);
                Assert.Equal("ANULACION", anulacion.Rows[0]["etapa"]);
                Assert.StartsWith("RETIRO_CONDUCTOR", (string)anulacion.Rows[0]["motivoAnulacion"]);

                // VerificarDocumento: el código del PDF retirado sale como anulado (la orden ya no existe).
                DataRow verificacion = DbHelper.ConsultarTablaSp("sp_LD_VerificarDocumento",
                    DbHelper.Param("@codigoVerificacion", codigo)).Rows[0];
                Assert.True(Convert.ToBoolean(verificacion["anulada"]));
                Assert.Equal(DBNull.Value, verificacion["numeroOrdenViaje"]);
                Assert.Equal("DECLARACION", verificacion["etapa"]);
            }
        }

        [FactBD]
        public void Rechazar_LiquidacionPorConformar_NoCambiaElEstado()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                var input = NuevaLiquidacion(datos, idViaje);
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(input);
                FirmarComoConductor(idOrden, datos.IdUsuarioConductor);
                Assert.True(LiquidacionesPendientesService.EnviarAConformidad(idOrden, AjustesDePrueba(), null, datos.IdUsuarioAdmin).Exito);
                FirmarRevision(idOrden, datos.IdUsuarioAdmin,
                    HuellaLiquidacion.Calcular(LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrden)));

                DataTable r = LiquidacionesPendientesService.Rechazar(input.NumeroOrdenViaje, datos.IdUsuarioAdmin, "Rechazo a destiempo (test)");

                Assert.Equal(0, Convert.ToInt32(r.Rows[0]["Resultado"]));
                Assert.Contains("ya fue procesada", (string)r.Rows[0]["Mensaje"]);
                DataRow ov = Fila("SELECT estadoAprobacion, idFirmaAdmin, observacionesRechazo FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden);
                Assert.Equal("POR_CONFORMAR", ov["estadoAprobacion"]);
                Assert.NotEqual(DBNull.Value, ov["idFirmaAdmin"]);
                Assert.Equal(DBNull.Value, ov["observacionesRechazo"]);
                Assert.Equal("CERRADO", RegistroDespachoService.ObtenerInfoViaje(idViaje).EstadoViaje);
            }
        }

        [FactBD]
        public void ObtenerDatosViaje_ConElParametroAnteriorDelSitioPublicado_FiltraPorEseConductor()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);

                // El código publicado (HEAD) llama al SP con @idConductorFallback = conductor de la sesión.
                DataTable propio = DbHelper.ConsultarTablaSp("sp_DC_ObtenerDatosViajeParaLiquidacion",
                    DbHelper.Param("@idViajeProgreso", idViaje),
                    DbHelper.Param("@idConductorFallback", datos.IdConductor));
                Assert.Single(propio.Rows);
                Assert.Equal(datos.IdConductor, Convert.ToInt32(propio.Rows[0]["idConductor"]));
                Assert.Equal(datos.IdTracto, Convert.ToInt32(propio.Rows[0]["idTracto"]));
                Assert.Equal(datos.IdCarreta, Convert.ToInt32(propio.Rows[0]["idCarreta"]));
                Assert.Equal("DESPACHOS", propio.Rows[0]["origen"]);

                // Con el parámetro anterior tampoco entrega el viaje a otro conductor.
                Assert.Empty(DbHelper.ConsultarTablaSp("sp_DC_ObtenerDatosViajeParaLiquidacion",
                    DbHelper.Param("@idViajeProgreso", idViaje),
                    DbHelper.Param("@idConductorFallback", datos.IdOtroConductor)).Rows);
            }
        }

        [FactBD]
        public void ReenviarUnaOrdenQueNoEstaRechazada_ElSpLanzaYElLoteSeDeshace()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                var input = NuevaLiquidacion(datos, idViaje);
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(input);
                FirmarComoConductor(idOrden, datos.IdUsuarioConductor);
                Assert.Equal("PENDIENTE", Fila("SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["estadoAprobacion"]);

                // Último paso: sp_DC_ActualizarOrdenViajeConductor hace THROW (orden PENDIENTE, no
                // REABIERTO) y el servicio hace rollback del lote (y de la transacción de la prueba).
                // El viaje se reabre a mano: con el viaje CERRADO el lote ya se detiene antes, en
                // sp_DC_BloquearViajesParaLiquidar (50012), y aquí se prueba la guarda del SP de la orden.
                DbHelper.EjecutarNonQuery("UPDATE ViajesEnProgreso SET estadoViaje = 'ABIERTO', fechaCierre = NULL WHERE idViajeProgreso = @v",
                    DbHelper.Param("@v", idViaje));
                input.EsReliquidacion = true;
                input.AlimentacionSoles = 1m;
                var ex = Assert.Throws<System.Data.SqlClient.SqlException>(() => LiquidacionConductorService.EnviarLiquidacion(input));
                Assert.Contains("ya no está rechazada", MensajeError.ExtraerMensajeDeNegocio(ex));
            }
        }

        [FactBD]
        public void Rechazar_Aprobada_NoCambiaElEstado()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                var input = NuevaLiquidacion(datos, idViaje);
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(input);
                FirmarComoConductor(idOrden, datos.IdUsuarioConductor);
                Assert.True(LiquidacionesPendientesService.EnviarAConformidad(idOrden, AjustesDePrueba(), null, datos.IdUsuarioAdmin).Exito);
                string huella = HuellaLiquidacion.Calcular(LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrden));
                FirmarRevision(idOrden, datos.IdUsuarioAdmin, huella);
                Assert.Equal(1, FirmarConformidad(idOrden, datos.IdUsuarioConductor, huella, out string mensaje));

                DataTable r = LiquidacionesPendientesService.Rechazar(input.NumeroOrdenViaje, datos.IdUsuarioAdmin, "Rechazo a destiempo (test)");

                Assert.Equal(0, Convert.ToInt32(r.Rows[0]["Resultado"]));
                DataRow ov = Fila("SELECT estadoAprobacion, estadoViaje, idFirmaConformidad FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden);
                Assert.Equal("APROBADO", ov["estadoAprobacion"]);
                Assert.Equal("COMPLETADO", ov["estadoViaje"]);
                Assert.NotEqual(DBNull.Value, ov["idFirmaConformidad"]);
                Assert.Equal("CERRADO", RegistroDespachoService.ObtenerInfoViaje(idViaje).EstadoViaje);
            }
        }
    }
}
