using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using WebSGV.Models.Conductor;
using WebSGV.Models.Despachos;
using WebSGV.Services.Common;
using WebSGV.Services.Conductor;
using WebSGV.Services.Despachos;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Tests de integración que ESCRIBEN en la BD de pruebas, siempre dentro de una
    /// <see cref="TransaccionDePrueba"/> que se deshace al terminar: no dejan datos.
    /// Recorren el flujo principal con los mismos servicios que usan las páginas:
    /// despacho → liquidación del conductor → firma → aprobación con ajustes / rechazo y reliquidación.
    /// La firma se registra llamando a su SP: <c>FirmaService</c> genera y archiva el PDF en disco
    /// (HostingEnvironment), algo que no corresponde a estos tests.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class FlujoDespachoLiquidacionTests
    {
        public FlujoDespachoLiquidacionTests() => BaseDePruebas.Preparar();

        // Montos de la liquidación de prueba y los totales que deben resultar.
        private const decimal DespachoSoles = 1000m, IngresoAdicionalSoles = 100m;
        private const decimal PeajesSoles = 150m, AlimentacionSoles = 200m, GastoAdicionalSoles = 30m, CombustibleDolares = 50m;
        private const decimal TotalIngresosSoles = DespachoSoles + IngresoAdicionalSoles;                     // 1100
        private const decimal TotalGastosSoles = PeajesSoles + AlimentacionSoles + GastoAdicionalSoles;       // 380

        [FactBD]
        public void TransaccionDePrueba_DeshaceTambienLoQueConfirmaElServicio()
        {
            // Garantía de la que dependen los demás tests: un commit hecho dentro (como el de
            // DbHelper.EnTransaccion o el de un SP) no sobrevive al rollback de la transacción.
            string marca = "TEST-ROLLBACK-" + Guid.NewGuid().ToString("N");
            const string insertar = @"INSERT INTO AuditoriaLog (FechaHora, NombreUsuario, Accion, TablaAfectada, Descripcion)
                                      VALUES (dbo.fn_AhoraPeru(), 'test', 'TEST', 'TEST', @m)";
            const string contar = "SELECT COUNT(*) FROM AuditoriaLog WHERE Descripcion = @m";

            using (new TransaccionDePrueba())
            {
                DbHelper.EnTransaccion((c, t) => DbHelper.EjecutarNonQuery(c, t, insertar, DbHelper.Param("@m", marca)));
                Assert.Equal(1, Convert.ToInt32(DbHelper.EjecutarEscalar(contar, DbHelper.Param("@m", marca))));
            }

            int quedaron = Convert.ToInt32(DbHelper.EjecutarEscalar(contar, DbHelper.Param("@m", marca)));
            if (quedaron > 0) // no debería pasar; limpiar para no ensuciar la BD de pruebas
                DbHelper.EjecutarNonQuery("DELETE FROM AuditoriaLog WHERE Descripcion = @m", DbHelper.Param("@m", marca));
            Assert.Equal(0, quedaron);
        }

        [FactBD]
        public void GenerarNumeroOrden_DevuelveUnCorrelativoLibreDelAnio()
        {
            // Sin transacción: el SP solo lee (con TABLOCKX, que dentro de un scope bloquearía
            // OrdenViaje hasta el rollback; por eso el flujo usa números de orden propios).
            string numero = DashboardConductorService.GenerarNumeroOrden();

            Assert.Matches(new Regex(@"^OV-" + FechaHelper.Hoy().Year + @"-\d{6}$"), numero);
            Assert.Null(LiquidacionesPendientesService.ObtenerNumeroOrden(-1));
            Assert.Equal(0, Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM OrdenViaje WHERE numeroOrdenViaje = @n", DbHelper.Param("@n", numero))));
        }

        [FactBD]
        public void RegistrarDespacho_ConFacturaYCpic_AbreUnViajeConElDespacho()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                string sufijo = Sufijo();
                var lote = NuevoLote(datos, "F-PRB-" + sufijo, "CPIC-PRB-" + sufijo);

                var antes = RegistroDespachoService.ValidarDocumentosDuplicados(lote.Documentacion.NumeroFactura, lote.Documentacion.NumeroCPIC);
                Assert.False(antes.FacturaExiste);
                Assert.False(antes.CpicExiste);

                int idViaje = CrearDespacho(datos, lote, out int idDespacho);

                var despues = RegistroDespachoService.ValidarDocumentosDuplicados(lote.Documentacion.NumeroFactura, lote.Documentacion.NumeroCPIC);
                Assert.True(despues.FacturaExiste);
                Assert.True(despues.CpicExiste);

                ViajeEnProgreso viaje = RegistroDespachoService.ObtenerViajesAbiertosConductor(datos.IdConductor)
                    .SingleOrDefault(v => v.IdViajeProgreso == idViaje);
                Assert.NotNull(viaje);
                Assert.Equal("ABIERTO", viaje.EstadoViaje);
                Assert.Equal(1, viaje.CantidadDespachos);
                Assert.Equal(false, viaje.EsInternacional);

                DataRow d = Fila("SELECT estadoDespacho, idFactura, idCPIC, idPlanta FROM Despachos WHERE idDespacho = @id", idDespacho);
                Assert.Equal("PROGRAMADO", d["estadoDespacho"]);
                Assert.NotEqual(DBNull.Value, d["idFactura"]);
                Assert.NotEqual(DBNull.Value, d["idCPIC"]);
                Assert.Equal(datos.IdPlanta, Convert.ToInt32(d["idPlanta"]));

                // Un segundo despacho en el mismo viaje lo suma, no abre otro.
                lote.Conductores[0].IdViajeProgreso = idViaje;
                RegistroDespachoService.CrearDespachoIndividual(lote, lote.Conductores[0], null, null);
                Assert.Equal(2, RegistroDespachoService.ObtenerInfoViaje(idViaje).CantidadDespachos);
            }
        }

        [FactBD]
        public void ViajeCreadoAMano_TomaElAmbitoDeSusDespachos()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = RegistroDespachoService.CrearNuevoViajeProgreso(datos.IdConductor, "Viaje manual (test)", "test");
                Assert.Null(RegistroDespachoService.ObtenerInfoViaje(idViaje).EsInternacional);

                var lote = NuevoLote(datos);
                lote.Conductores[0].IdViajeProgreso = idViaje;
                RegistroDespachoService.CrearDespachoIndividual(lote, lote.Conductores[0], null, null);
                Assert.Equal(false, RegistroDespachoService.ObtenerInfoViaje(idViaje).EsInternacional);

                // Basta un despacho internacional para que el viaje lo sea (y la liquidación pida peajes extranjeros).
                lote.EsInternacional = true;
                RegistroDespachoService.CrearDespachoIndividual(lote, lote.Conductores[0], null, null);
                Assert.Equal(true, RegistroDespachoService.ObtenerInfoViaje(idViaje).EsInternacional);

                lote.EsInternacional = false;
                RegistroDespachoService.CrearDespachoIndividual(lote, lote.Conductores[0], null, null);
                Assert.Equal(true, RegistroDespachoService.ObtenerInfoViaje(idViaje).EsInternacional);
            }
        }

        [FactBD]
        public void EnviarLiquidacion_DejaLaOrdenPendienteConSusMontosYCierraElViaje()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out int idDespacho);
                var input = NuevaLiquidacion(datos, idViaje);

                int idOrden = LiquidacionConductorService.EnviarLiquidacion(input);

                Assert.True(idOrden > 0);
                DataRow ov = Fila("SELECT estadoViaje, estadoAprobacion, idViajeProgreso, idFirmaConductor FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden);
                Assert.Equal("PENDIENTE", ov["estadoViaje"]);
                Assert.Equal("PENDIENTE", ov["estadoAprobacion"]);
                Assert.Equal(idViaje, Convert.ToInt32(ov["idViajeProgreso"]));
                Assert.Equal(DBNull.Value, ov["idFirmaConductor"]);

                Assert.Equal("CERRADO", RegistroDespachoService.ObtenerInfoViaje(idViaje).EstadoViaje);
                Assert.Equal("COMPLETADO", Fila("SELECT estadoDespacho FROM Despachos WHERE idDespacho = @id", idDespacho)["estadoDespacho"]);

                var detalle = LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrden);
                Assert.Equal(input.NumeroOrdenViaje, detalle.NumeroOrdenViaje);
                Assert.Equal(DespachoSoles, detalle.DespachoSoles);
                Assert.Equal(PeajesSoles, detalle.GastosPeajesSoles);
                Assert.Equal(AlimentacionSoles, detalle.GastosAlimentacionSoles);
                Assert.Equal(CombustibleDolares, detalle.GastosCombustibleDolares);
                Assert.Equal(IngresoAdicionalSoles, detalle.IngresosAdicionalesSoles);
                Assert.Equal(GastoAdicionalSoles, detalle.GastosAdicionalesSoles);
                Assert.Single(detalle.DetallesPeajes);
                Assert.Equal(PeajesSoles, detalle.DetallesPeajes[0].Soles);

                // Llega a la bandeja del administrador recién cuando el conductor firma, con los totales sumados.
                Assert.DoesNotContain(LiquidacionesPendientesService.ObtenerPendientes(datos.IdConductor, null, null).Rows.Cast<DataRow>(),
                    p => Convert.ToInt32(p["idOrdenViaje"]) == idOrden);
                FirmarComoConductor(idOrden, datos.IdUsuarioConductor);
                DataRow pendiente = LiquidacionesPendientesService.ObtenerPendientes(datos.IdConductor, null, null).Rows
                    .Cast<DataRow>().Single(r => Convert.ToInt32(r["idOrdenViaje"]) == idOrden);
                Assert.Equal(TotalIngresosSoles, Convert.ToDecimal(pendiente["TotalIngresosSoles"]));
                Assert.Equal(TotalGastosSoles, Convert.ToDecimal(pendiente["TotalGastosSoles"]));
                Assert.Equal(TotalIngresosSoles - TotalGastosSoles, Convert.ToDecimal(pendiente["BalanceSoles"]));
                Assert.Equal(-CombustibleDolares, Convert.ToDecimal(pendiente["BalanceDolares"]));
            }
        }

        [FactBD]
        public void FirmarYAprobarConAjustes_CompletaLaOrdenConDescuentoYReintegro()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                var input = NuevaLiquidacion(datos, idViaje);
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(input);

                int idFirma = FirmarComoConductor(idOrden, datos.IdUsuarioConductor);
                Assert.Equal(idFirma, Convert.ToInt32(Fila("SELECT idFirmaConductor FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["idFirmaConductor"]));

                var r = LiquidacionesPendientesService.AprobarConAjustes(idOrden, 40m, 0m, 15m, 5m, "Aprobado en test", datos.IdUsuarioAdmin);

                Assert.True(r.Exito, r.Mensaje);
                Assert.Equal(input.NumeroOrdenViaje, r.NumeroOrdenViaje);
                DataRow ov = Fila("SELECT estadoViaje, estadoAprobacion, idUsuarioAprobacion, observacionesAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden);
                Assert.Equal("COMPLETADO", ov["estadoViaje"]);
                Assert.Equal("APROBADO", ov["estadoAprobacion"]);
                Assert.Equal(datos.IdUsuarioAdmin, Convert.ToInt32(ov["idUsuarioAprobacion"]));
                Assert.Equal("Aprobado en test", ov["observacionesAprobacion"]);

                // La aprobación guarda los ajustes en DescuentosReintegros sin tocar la firma del conductor.
                var detalle = LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrden);
                Assert.Equal(40m, detalle.DescuentoSoles);
                Assert.Equal(15m, detalle.ReintegroSoles);
                Assert.Equal(5m, detalle.ReintegroDolares);
                Assert.Equal("V", Fila("SELECT estadoFirma FROM FirmaDigital WHERE idFirma = @id", idFirma)["estadoFirma"]);

                Assert.DoesNotContain(LiquidacionesPendientesService.ObtenerPendientes(datos.IdConductor, null, null).Rows.Cast<DataRow>(),
                    p => Convert.ToInt32(p["idOrdenViaje"]) == idOrden);
                DataRow aprobada = LiquidacionesPendientesService.ObtenerAprobadas(datos.IdConductor, null, null, input.NumeroOrdenViaje, null, null)
                    .Rows.Cast<DataRow>().Single();
                Assert.Equal(40m, Convert.ToDecimal(aprobada["DescuentoSoles"]));
                Assert.Equal(TotalGastosSoles - GastoAdicionalSoles, Convert.ToDecimal(aprobada["GastosSoles"]));

                // Corregir los ajustes de una orden ya aprobada reemplaza los montos (no los suma).
                var correccion = LiquidacionesPendientesService.CorregirAjustesAprobada(idOrden, 10m, 0m, 0m, 0m, "Corrección en test", datos.IdUsuarioAdmin);
                Assert.True(correccion.OrdenEncontrada);
                detalle = LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrden);
                Assert.Equal(10m, detalle.DescuentoSoles);
                Assert.Equal(0m, detalle.ReintegroSoles);
                Assert.Contains("[CORRECCION AJUSTES", detalle.Observaciones);
            }
        }

        [FactBD]
        public void AprobarSinFirmaDelConductor_NoSeAprueba()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(NuevaLiquidacion(datos, idViaje));

                var r = LiquidacionesPendientesService.AprobarConAjustes(idOrden, 0m, 0m, 0m, 0m, null, datos.IdUsuarioAdmin);

                // Último paso: el rollback interno del servicio puede anular la transacción del test.
                Assert.False(r.Exito);
                Assert.Contains("firma", r.Mensaje);
            }
        }

        [FactBD]
        public void RechazarYReliquidar_ReabreElViajeYReemplazaLosMontos()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out int idDespacho);
                var input = NuevaLiquidacion(datos, idViaje);
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(input);
                FirmarComoConductor(idOrden, datos.IdUsuarioConductor);

                DataTable r = LiquidacionesPendientesService.Rechazar(input.NumeroOrdenViaje, datos.IdUsuarioAdmin, "Falta comprobante de peaje");

                Assert.Equal(1, Convert.ToInt32(r.Rows[0]["Resultado"]));
                DataRow ov = Fila("SELECT estadoAprobacion, observacionesRechazo FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden);
                Assert.Equal("REABIERTO", ov["estadoAprobacion"]);
                Assert.Equal("Falta comprobante de peaje", ov["observacionesRechazo"]);
                Assert.Equal("ABIERTO", RegistroDespachoService.ObtenerInfoViaje(idViaje).EstadoViaje);
                Assert.Equal("EN_PROCESO", Fila("SELECT estadoDespacho FROM Despachos WHERE idDespacho = @id", idDespacho)["estadoDespacho"]);

                // El conductor corrige y vuelve a enviar con el mismo número de orden.
                input.EsReliquidacion = true;
                input.PeajesSoles = 90m;
                input.GastosFinancieros[0].Soles = 90m;
                input.GastosAdicionales.Clear();
                Assert.Equal(idOrden, LiquidacionConductorService.EnviarLiquidacion(input));

                Assert.Equal(1, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM OrdenViaje WHERE numeroOrdenViaje = @n", DbHelper.Param("@n", input.NumeroOrdenViaje))));
                Assert.Equal("PENDIENTE", Fila("SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["estadoAprobacion"]);
                Assert.Equal("CERRADO", RegistroDespachoService.ObtenerInfoViaje(idViaje).EstadoViaje);

                var detalle = LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrden);
                Assert.Equal(90m, detalle.GastosPeajesSoles);
                Assert.Equal(0m, detalle.GastosAdicionalesSoles);
                Assert.Single(detalle.DetallesPeajes);           // reemplazado, no duplicado
                Assert.Equal(DespachoSoles, detalle.DespachoSoles);
            }
        }

        [FactBD]
        public void EnviarLiquidacion_ConUnViajeDeOtroConductor_SeRechaza()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViajePropio = CrearDespacho(datos, NuevoLote(datos), out _);
                int idViajeAjeno = RegistroDespachoService.CrearNuevoViajeProgreso(datos.IdOtroConductor, "Viaje de otro conductor (test)", "test");

                var input = NuevaLiquidacion(datos, idViajePropio);
                input.IdsViajesActivos = new List<int> { idViajePropio, idViajeAjeno };

                // Último paso: el SP hace RAISERROR y el servicio, rollback.
                var ex = Assert.Throws<SqlException>(() => LiquidacionConductorService.EnviarLiquidacion(input));
                Assert.Contains("no pertenecen al conductor", MensajeError.ExtraerMensajeDeNegocio(ex));
            }
        }

        // ------------------------------------------------------------------
        // Datos y pasos comunes
        // ------------------------------------------------------------------

        /// <summary>Registros existentes en la BD de pruebas que usan los flujos.</summary>
        private sealed class DatosBase
        {
            public int IdConductor, IdUsuarioConductor, IdOtroConductor, IdUsuarioAdmin;
            public int IdTracto, IdCarreta, IdCliente, IdPlanta;

            public static DatosBase Cargar()
            {
                DataTable conductores = DbHelper.ConsultarTabla(@"
                    SELECT TOP 2 c.idConductor, u.idUsuario
                    FROM Conductor c
                    INNER JOIN Usuarios u ON u.idConductor = c.idConductor AND u.activo = 1
                         AND UPPER(LTRIM(RTRIM(u.rol))) IN ('CONDUCTOR', 'CHOFER')
                    WHERE c.activo = 1
                    ORDER BY c.idConductor");
                Assert.True(conductores.Rows.Count == 2, "La BD de pruebas necesita 2 conductores activos con usuario.");

                return new DatosBase
                {
                    IdConductor = Convert.ToInt32(conductores.Rows[0]["idConductor"]),
                    IdUsuarioConductor = Convert.ToInt32(conductores.Rows[0]["idUsuario"]),
                    IdOtroConductor = Convert.ToInt32(conductores.Rows[1]["idConductor"]),
                    IdUsuarioAdmin = Primero(@"SELECT TOP 1 idUsuario FROM Usuarios WHERE activo = 1
                                               AND UPPER(LTRIM(RTRIM(rol))) NOT IN ('CONDUCTOR', 'CHOFER') ORDER BY idUsuario", "usuario no conductor"),
                    IdTracto = Primero("SELECT TOP 1 idTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto", "tracto"),
                    IdCarreta = Primero("SELECT TOP 1 idCarreta FROM Carreta WHERE activo = 1 ORDER BY idCarreta", "carreta"),
                    IdCliente = Primero("SELECT TOP 1 idCliente FROM Cliente WHERE activo = 1 ORDER BY idCliente", "cliente"),
                    IdPlanta = Primero("SELECT TOP 1 idPlanta FROM Planta WHERE activo = 1 ORDER BY idPlanta", "planta")
                };
            }

            private static int Primero(string sql, string que)
            {
                object v = DbHelper.EjecutarEscalar(sql);
                Assert.True(v != null && v != DBNull.Value, "La BD de pruebas no tiene ningún registro activo de " + que + ".");
                return Convert.ToInt32(v);
            }
        }

        private static string Sufijo() => Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();

        private static LoteDespachos NuevoLote(DatosBase datos, string numeroFactura = null, string numeroCpic = null)
        {
            var lote = new LoteDespachos
            {
                FechaProgramacion = FechaHelper.Hoy().ToString("yyyy-MM-dd"),
                IdCliente = datos.IdCliente,
                TipoOperacion = "CARGA",
                EsInternacional = false,
                IdPlanta = datos.IdPlanta,
                UsuarioCreacion = "test",
                Documentacion = new DocumentacionBase
                {
                    NumeroFactura = numeroFactura,
                    ValorTotalFactura = 5000m,
                    NumeroCPIC = numeroCpic,
                    ValorFlete = 1200m
                }
            };
            lote.Conductores.Add(new ConductorLote
            {
                IdConductor = datos.IdConductor,
                IdTracto = datos.IdTracto,
                IdCarreta = datos.IdCarreta,
                GuiaRemitente = "GR-TEST",
                GuiaTransportista = "GT-TEST"
            });
            return lote;
        }

        /// <summary>Crea los documentos (si el lote los trae) y el despacho; devuelve el viaje que abrió.</summary>
        private static int CrearDespacho(DatosBase datos, LoteDespachos lote, out int idDespacho)
        {
            int? idFactura = lote.Documentacion.NumeroFactura != null
                ? RegistroDespachoService.CrearDocumentoBaseSeparado("FACTURA", lote) : null;
            int? idCpic = lote.Documentacion.NumeroCPIC != null
                ? RegistroDespachoService.CrearDocumentoBaseSeparado("CPIC", lote, idFactura) : null;

            idDespacho = RegistroDespachoService.CrearDespachoIndividual(lote, lote.Conductores[0], idFactura, idCpic);
            Assert.True(idDespacho > 0);
            return Convert.ToInt32(Fila("SELECT idViajeProgreso FROM Despachos WHERE idDespacho = @id", idDespacho)["idViajeProgreso"]);
        }

        /// <summary>Liquidación como la arma DashboardConductor a partir del formulario.</summary>
        private static LiquidacionConductorInput NuevaLiquidacion(DatosBase datos, int idViaje)
        {
            DataRow viaje = DashboardConductorService.ObtenerDatosViajeParaLiquidacion(idViaje, datos.IdConductor).Rows[0];
            string hoy = FechaHelper.Hoy().ToString("yyyy-MM-dd");

            return new LiquidacionConductorInput
            {
                NumeroOrdenViaje = "OV-TEST-" + Sufijo(),
                FechaSalida = FechaHelper.Hoy().AddDays(-1),
                FechaLlegada = FechaHelper.Hoy(),
                HoraSalida = "08:00",
                HoraLlegada = "18:00",
                HoraLlegadaDeclarada = "17:45",
                Observaciones = "Liquidación de prueba",
                IdConductor = Convert.ToInt32(viaje["idConductor"]),
                IdTracto = Convert.ToInt32(viaje["idTracto"]),
                IdCarreta = Convert.ToInt32(viaje["idCarreta"]),
                IdViajeProgreso = idViaje,
                IdUsuarioRegistro = datos.IdUsuarioConductor,
                IdConductorActual = datos.IdConductor,
                IdsViajesActivos = new List<int> { idViaje },
                DespachoSoles = DespachoSoles,
                PeajesSoles = PeajesSoles,
                AlimentacionSoles = AlimentacionSoles,
                CombustibleDolares = CombustibleDolares,
                IngresosAdicionales = { new IngresoAdicionalData { Categoria = "Bono", Soles = IngresoAdicionalSoles, Dolares = 0m } },
                GastosAdicionales = { new GastoAdicionalData { Categoria = "Lavado", Soles = GastoAdicionalSoles, Dolares = 0m } },
                GastosFinancieros =
                {
                    new GastoFinanciero { Categoria = "peajes", Estacion = "Peaje de prueba", FechaString = hoy, Comprobante = "P-001", Soles = PeajesSoles }
                }
            };
        }

        /// <summary>Registra la firma del conductor con un trazo ficticio (lo que hace FirmaService sin el PDF).</summary>
        private static int FirmarComoConductor(int idOrden, int idUsuarioConductor)
        {
            var pIdFirma = new SqlParameter("@idFirmaSalida", SqlDbType.Int) { Direction = ParameterDirection.Output };
            var pResultado = new SqlParameter("@resultado", SqlDbType.Int) { Direction = ParameterDirection.Output };
            var pMensaje = new SqlParameter("@mensaje", SqlDbType.VarChar, 500) { Direction = ParameterDirection.Output };

            DbHelper.EjecutarNonQuerySp("sp_DC_RegistrarFirmaConductor",
                DbHelper.Param("@idOrdenViaje", idOrden),
                DbHelper.Param("@idUsuarioFirmante", idUsuarioConductor),
                DbHelper.Param("@dniFirmante", "00000000"),
                DbHelper.Param("@nombreFirmante", "Conductor de prueba"),
                DbHelper.Param("@imagenTrazoPng", new byte[200]),
                DbHelper.Param("@hashDocumento", new string('a', 64)),
                DbHelper.Param("@textoConsentimiento", "Consentimiento de prueba"),
                DbHelper.Param("@ipOrigen", "127.0.0.1"),
                DbHelper.Param("@userAgent", "xunit"),
                DbHelper.Param("@rutaPdfFirmado", null),
                pIdFirma, pResultado, pMensaje);

            Assert.True(Convert.ToInt32(pResultado.Value) == 1, pMensaje.Value as string);
            return Convert.ToInt32(pIdFirma.Value);
        }

        private static DataRow Fila(string sql, int id)
        {
            DataTable dt = DbHelper.ConsultarTabla(sql, DbHelper.Param("@id", id));
            Assert.True(dt.Rows.Count == 1, "Se esperaba una fila para id " + id + ": " + sql);
            return dt.Rows[0];
        }
    }
}
