using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WebSGV.Helpers;
using WebSGV.Models.Conductor;
using WebSGV.Services.Common;
using WebSGV.Services.OrdenViaje;

namespace WebSGV.Services.Conductor
{
    /// <summary>
    /// Transacción de envío de liquidación del conductor (antes en
    /// <c>DashboardConductor.aspx.cs &gt; btnEnviarLiquidacion_Click</c> y sus helpers
    /// <c>Insertar*</c>/<c>Cerrar*</c>). El code-behind conserva la sesión, validación,
    /// el parseo de <c>Request.Form</c>/hidden fields, la auditoría, la notificación y el
    /// redirect a la firma; este servicio sólo orquesta el SQL/SPs dentro de una única
    /// transacción. SQL y stored procedures movidos verbatim (sin cambios de comportamiento).
    ///
    /// El servicio no depende de <c>System.Web</c>: recibe un <see cref="LiquidacionConductorInput"/>
    /// con todos los valores ya parseados/validados y lanza la excepción para que el
    /// code-behind muestre el mensaje (el rollback ya se ejecutó).
    /// </summary>
    public static class LiquidacionConductorService
    {
        /// <summary>
        /// Ejecuta la transacción completa (orden + financieros + cierre de viajes) y
        /// devuelve el <c>idOrdenViaje</c> guardado. Si algo falla hace rollback y
        /// re-propaga la excepción original.
        /// </summary>
        public static int EnviarLiquidacion(LiquidacionConductorInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            int idOrdenGuardada = 0;

            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        if (input.EsReliquidacion)
                        {
                            EliminarDatosFinancierosOrdenExistente(conn, transaction, input.NumeroOrdenViaje);
                            ActualizarOrdenViajeConductor(conn, transaction, input);

                            using (SqlCommand cmdId = new SqlCommand(
                                "SELECT idOrdenViaje FROM OrdenViaje WHERE numeroOrdenViaje = @n", conn, transaction))
                            {
                                cmdId.Parameters.AddWithValue("@n", input.NumeroOrdenViaje);
                                object obj = cmdId.ExecuteScalar();
                                idOrdenGuardada = (obj != null && obj != DBNull.Value) ? Convert.ToInt32(obj) : 0;
                            }
                        }
                        else
                        {
                            idOrdenGuardada = InsertarOrdenViajeConductor(conn, transaction, input);
                        }

                        InsertarIngresosPrincipales(conn, transaction, input);
                        InsertarGastosPrincipales(conn, transaction, input);
                        InsertarIngresosAdicionales(conn, transaction, input);
                        InsertarGastosAdicionales(conn, transaction, input);
                        InsertarDescuentosReintegros(conn, transaction, input);

                        if (input.GastosFinancieros != null && input.GastosFinancieros.Count > 0)
                            InsertarGastosFinancierosDetallados(conn, transaction, input.NumeroOrdenViaje, input.GastosFinancieros);

                        CerrarViajesProgreso(conn, transaction, input.IdsViajesActivos, input.NumeroOrdenViaje, input.IdConductorActual);

                        transaction.Commit();
                    }
                    catch
                    {
                        // No enmascarar la excepción original si el rollback también falla.
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }

            return idOrdenGuardada;
        }

        // ------------------------------------------------------------------
        // Helpers de inserción (SQL/SP movido verbatim del code-behind)
        // ------------------------------------------------------------------

        private static void EliminarDatosFinancierosOrdenExistente(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje)
        {
            using (SqlCommand cmd = new SqlCommand("sp_DC_EliminarDatosFinancierosOrden", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.ExecuteNonQuery();
            }
        }

        private static void ActualizarOrdenViajeConductor(SqlConnection conn, SqlTransaction transaction, LiquidacionConductorInput input)
        {
            using (SqlCommand cmd = new SqlCommand("sp_DC_ActualizarOrdenViajeConductor", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", input.NumeroOrdenViaje);
                cmd.Parameters.AddWithValue("@fechaSalida", FechaSeguraSQL(input.FechaSalida));
                cmd.Parameters.AddWithValue("@horaSalida", string.IsNullOrEmpty(input.HoraSalida) ? (object)DBNull.Value : input.HoraSalida);
                cmd.Parameters.AddWithValue("@fechaLlegada", FechaSeguraSQL(input.FechaLlegada));
                cmd.Parameters.AddWithValue("@horaLlegada", string.IsNullOrEmpty(input.HoraLlegada) ? (object)DBNull.Value : input.HoraLlegada);
                cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(input.Observaciones) ? (object)DBNull.Value : input.Observaciones);
                cmd.Parameters.AddWithValue("@idUsuarioRegistro", input.IdUsuarioRegistro > 0 ? (object)input.IdUsuarioRegistro : DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private static int InsertarOrdenViajeConductor(SqlConnection conn, SqlTransaction transaction, LiquidacionConductorInput input)
        {
            using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarOrdenViajeConductor", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", input.NumeroOrdenViaje);
                cmd.Parameters.AddWithValue("@fechaSalida", FechaSeguraSQL(input.FechaSalida));
                cmd.Parameters.AddWithValue("@horaSalida", string.IsNullOrEmpty(input.HoraSalida) ? (object)DBNull.Value : input.HoraSalida);
                cmd.Parameters.AddWithValue("@fechaLlegada", FechaSeguraSQL(input.FechaLlegada));
                cmd.Parameters.AddWithValue("@horaLlegada", string.IsNullOrEmpty(input.HoraLlegada) ? (object)DBNull.Value : input.HoraLlegada);
                cmd.Parameters.AddWithValue("@idConductor", input.IdConductor);
                cmd.Parameters.AddWithValue("@idTracto", input.IdTracto);
                cmd.Parameters.AddWithValue("@idCarreta", input.IdCarreta);
                cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(input.Observaciones) ? (object)DBNull.Value : input.Observaciones);
                cmd.Parameters.AddWithValue("@idViajeProgreso", input.IdViajeProgreso);
                cmd.Parameters.AddWithValue("@idUsuarioRegistro", input.IdUsuarioRegistro > 0 ? (object)input.IdUsuarioRegistro : DBNull.Value);

                object result = cmd.ExecuteScalar();

                if (result != null && result != DBNull.Value)
                    return Convert.ToInt32(result);

                throw new Exception("No se pudo insertar la orden de viaje");
            }
        }

        private static void InsertarIngresosPrincipales(SqlConnection conn, SqlTransaction transaction, LiquidacionConductorInput input)
        {
            if (input.DespachoSoles > 0 || input.DespachoDolares > 0 || input.PrestamoSoles > 0 || input.PrestamoDolares > 0 ||
                input.MensualidadSoles > 0 || input.MensualidadDolares > 0 || input.OtrosSoles > 0 || input.OtrosDolares > 0)
            {
                using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarIngresos", conn, transaction))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@numeroOrdenViaje", input.NumeroOrdenViaje);
                    cmd.Parameters.AddWithValue("@despachoSoles", input.DespachoSoles);
                    cmd.Parameters.AddWithValue("@despachoDolares", input.DespachoDolares);
                    cmd.Parameters.AddWithValue("@prestamoSoles", input.PrestamoSoles);
                    cmd.Parameters.AddWithValue("@prestamoDolares", input.PrestamoDolares);
                    cmd.Parameters.AddWithValue("@mensualidadSoles", input.MensualidadSoles);
                    cmd.Parameters.AddWithValue("@mensualidadDolares", input.MensualidadDolares);
                    cmd.Parameters.AddWithValue("@otrosSoles", input.OtrosSoles);
                    cmd.Parameters.AddWithValue("@otrosDolares", input.OtrosDolares);
                    cmd.Parameters.AddWithValue("@descDespacho", string.IsNullOrEmpty(input.DescDespacho) ? (object)DBNull.Value : input.DescDespacho);
                    cmd.Parameters.AddWithValue("@descMensualidad", string.IsNullOrEmpty(input.DescMensualidad) ? (object)DBNull.Value : input.DescMensualidad);
                    cmd.Parameters.AddWithValue("@descOtros", string.IsNullOrEmpty(input.DescOtros) ? (object)DBNull.Value : input.DescOtros);
                    cmd.Parameters.AddWithValue("@descPrestamo", string.IsNullOrEmpty(input.DescPrestamo) ? (object)DBNull.Value : input.DescPrestamo);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void InsertarGastosPrincipales(SqlConnection conn, SqlTransaction transaction, LiquidacionConductorInput input)
        {
            using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarEgresos", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", input.NumeroOrdenViaje);
                cmd.Parameters.AddWithValue("@peajesSoles", input.PeajesSoles);
                cmd.Parameters.AddWithValue("@peajesDolares", input.PeajesDolares);
                cmd.Parameters.AddWithValue("@descPeajes", string.IsNullOrEmpty(input.DescPeajes) ? (object)DBNull.Value : input.DescPeajes);
                cmd.Parameters.AddWithValue("@alimentacionSoles", input.AlimentacionSoles);
                cmd.Parameters.AddWithValue("@alimentacionDolares", input.AlimentacionDolares);
                cmd.Parameters.AddWithValue("@descAlimentacion", string.IsNullOrEmpty(input.DescAlimentacion) ? (object)DBNull.Value : input.DescAlimentacion);
                cmd.Parameters.AddWithValue("@apoyoSeguridadSoles", input.ApoyoSeguridadSoles);
                cmd.Parameters.AddWithValue("@apoyoSeguridadDolares", input.ApoyoSeguridadDolares);
                cmd.Parameters.AddWithValue("@descApoyoSeguridad", string.IsNullOrEmpty(input.DescApoyoSeguridad) ? (object)DBNull.Value : input.DescApoyoSeguridad);
                cmd.Parameters.AddWithValue("@reparacionesSoles", input.ReparacionesSoles);
                cmd.Parameters.AddWithValue("@reparacionesDolares", input.ReparacionesDolares);
                cmd.Parameters.AddWithValue("@descReparaciones", string.IsNullOrEmpty(input.DescReparaciones) ? (object)DBNull.Value : input.DescReparaciones);
                cmd.Parameters.AddWithValue("@movilidadSoles", input.MovilidadSoles);
                cmd.Parameters.AddWithValue("@movilidadDolares", input.MovilidadDolares);
                cmd.Parameters.AddWithValue("@descMovilidad", string.IsNullOrEmpty(input.DescMovilidad) ? (object)DBNull.Value : input.DescMovilidad);
                cmd.Parameters.AddWithValue("@encapadaSoles", input.EncapadaSoles);
                cmd.Parameters.AddWithValue("@encapadaDolares", input.EncapadaDolares);
                cmd.Parameters.AddWithValue("@descEncapada", string.IsNullOrEmpty(input.DescEncapada) ? (object)DBNull.Value : input.DescEncapada);
                cmd.Parameters.AddWithValue("@hospedajeSoles", input.HospedajeSoles);
                cmd.Parameters.AddWithValue("@hospedajeDolares", input.HospedajeDolares);
                cmd.Parameters.AddWithValue("@descHospedaje", string.IsNullOrEmpty(input.DescHospedaje) ? (object)DBNull.Value : input.DescHospedaje);
                cmd.Parameters.AddWithValue("@combustibleSoles", input.CombustibleSoles);
                cmd.Parameters.AddWithValue("@combustibleDolares", input.CombustibleDolares);
                cmd.Parameters.AddWithValue("@descCombustible", string.IsNullOrEmpty(input.DescCombustible) ? (object)DBNull.Value : input.DescCombustible);
                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertarIngresosAdicionales(SqlConnection conn, SqlTransaction transaction, LiquidacionConductorInput input)
        {
            if (input.IngresosAdicionales == null) return;

            foreach (var ingreso in input.IngresosAdicionales)
            {
                using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarIngresoAdicional", conn, transaction))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@numeroOrdenViaje", input.NumeroOrdenViaje);
                    cmd.Parameters.AddWithValue("@nombreCategoria", ingreso.Categoria ?? ingreso.NombreCategoria ?? "");
                    cmd.Parameters.AddWithValue("@soles", ValidarMonto(ingreso.Soles, "soles de ingreso adicional"));
                    cmd.Parameters.AddWithValue("@dolares", ValidarMonto(ingreso.Dolares, "dólares de ingreso adicional"));
                    cmd.Parameters.AddWithValue("@descripcion", ingreso.Descripcion ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void InsertarGastosAdicionales(SqlConnection conn, SqlTransaction transaction, LiquidacionConductorInput input)
        {
            if (input.GastosAdicionales == null) return;

            foreach (var gasto in input.GastosAdicionales)
            {
                using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarGastoAdicional", conn, transaction))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@numeroOrdenViaje", input.NumeroOrdenViaje);
                    cmd.Parameters.AddWithValue("@nombreCategoria", gasto.Categoria ?? gasto.NombreCategoria ?? "");
                    cmd.Parameters.AddWithValue("@soles", ValidarMonto(gasto.Soles, "soles de gasto adicional"));
                    cmd.Parameters.AddWithValue("@dolares", ValidarMonto(gasto.Dolares, "dólares de gasto adicional"));
                    cmd.Parameters.AddWithValue("@descripcion", gasto.Descripcion ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void InsertarDescuentosReintegros(SqlConnection conn, SqlTransaction transaction, LiquidacionConductorInput input)
        {
            if (input.DescuentoSoles > 0 || input.DescuentoDolares > 0 || input.ReintegroSoles > 0 || input.ReintegroDolares > 0)
            {
                using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarDescuentosReintegros", conn, transaction))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@numeroOrdenViaje", input.NumeroOrdenViaje);
                    cmd.Parameters.AddWithValue("@descuentoSoles", input.DescuentoSoles);
                    cmd.Parameters.AddWithValue("@descuentoDolares", input.DescuentoDolares);
                    cmd.Parameters.AddWithValue("@reintegroSoles", input.ReintegroSoles);
                    cmd.Parameters.AddWithValue("@reintegroDolares", input.ReintegroDolares);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void InsertarGastosFinancierosDetallados(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, List<GastoFinanciero> gastos)
        {
            foreach (var gasto in gastos)
            {
                string categoria = gasto.Categoria?.ToLower() ?? "";

                switch (categoria)
                {
                    case "peajes":
                        InsertarPeajeDetallado(conn, transaction, numeroOrdenViaje, gasto);
                        break;
                    case "reparaciones":
                        InsertarReparacionDetallada(conn, transaction, numeroOrdenViaje, gasto);
                        break;
                    case "hospedaje":
                        InsertarHospedajeDetallado(conn, transaction, numeroOrdenViaje, gasto);
                        break;
                    case "combustible":
                        InsertarCombustibleDetallado(conn, transaction, numeroOrdenViaje, gasto);
                        break;
                }
            }
        }

        private static void InsertarPeajeDetallado(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, GastoFinanciero gasto)
        {
            using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarDetallePeaje", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@estacion", gasto.Estacion ?? gasto.Lugar ?? "");
                cmd.Parameters.AddWithValue("@fecha", FechaSeguraSQL(gasto.Fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.Comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", ValidarMonto(gasto.Soles, "soles de peaje"));
                cmd.Parameters.AddWithValue("@montoDolares", ValidarMonto(gasto.Dolares, "dólares de peaje"));
                cmd.Parameters.AddWithValue("@observaciones", gasto.Observaciones ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertarReparacionDetallada(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, GastoFinanciero gasto)
        {
            using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarDetalleReparacion", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(gasto.Fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.Comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", ValidarMonto(gasto.Soles, "soles de reparación"));
                cmd.Parameters.AddWithValue("@montoDolares", ValidarMonto(gasto.Dolares, "dólares de reparación"));
                cmd.Parameters.AddWithValue("@observaciones", gasto.Observaciones ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertarHospedajeDetallado(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, GastoFinanciero gasto)
        {
            using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarDetalleHospedaje", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(gasto.Fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.Comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", ValidarMonto(gasto.Soles, "soles de hospedaje"));
                cmd.Parameters.AddWithValue("@montoDolares", ValidarMonto(gasto.Dolares, "dólares de hospedaje"));
                cmd.Parameters.AddWithValue("@observaciones", gasto.Observaciones ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertarCombustibleDetallado(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, GastoFinanciero gasto)
        {
            using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarDetalleCombustible", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(gasto.Fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.Comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", ValidarMonto(gasto.Soles, "soles de combustible"));
                cmd.Parameters.AddWithValue("@montoDolares", ValidarMonto(gasto.Dolares, "dólares de combustible"));
                cmd.Parameters.AddWithValue("@observaciones", gasto.Observaciones ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        private static void CerrarViajesProgreso(SqlConnection conn, SqlTransaction transaction, List<int> idsViajes, string numeroOrdenViaje, int idConductor)
        {
            using (SqlCommand cmd = new SqlCommand("sp_DC_CerrarViajesProgreso", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@idsViajes", string.Join(",", idsViajes));
                cmd.Parameters.AddWithValue("@idConductor", idConductor); // IDOR guard: el SP valida que los viajes pertenecen a este conductor

                SqlParameter pViajesCerrados = new SqlParameter("@filasViajesCerrados", SqlDbType.Int) { Direction = ParameterDirection.Output };
                SqlParameter pDespachosCerrados = new SqlParameter("@filasDespachosCerrados", SqlDbType.Int) { Direction = ParameterDirection.Output };
                cmd.Parameters.Add(pViajesCerrados);
                cmd.Parameters.Add(pDespachosCerrados);

                cmd.ExecuteNonQuery();

                int filasAfectadas = (int)pViajesCerrados.Value;

                if (filasAfectadas == 0)
                {
                    throw new Exception($"No se pudo cerrar ninguno de los viajes: {string.Join(", ", idsViajes)}");
                }
            }
        }

        // ------------------------------------------------------------------
        // Utilidades locales (sin System.Web)
        // ------------------------------------------------------------------

        private static object FechaSeguraSQL(DateTime fecha) =>
            OrdenViajeValidaciones.EsFechaValidaSQL(fecha) ? (object)fecha : DBNull.Value;

        private static decimal ValidarMonto(decimal? monto, string campo) => MontoHelper.ValidarMonto(monto, campo);
        private static decimal ValidarMonto(decimal monto, string campo) => MontoHelper.ValidarMonto(monto, campo);
    }
}
