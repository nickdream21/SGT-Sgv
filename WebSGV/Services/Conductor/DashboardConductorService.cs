using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WebSGV.Helpers;

namespace WebSGV.Services.Conductor
{
    /// <summary>
    /// Acceso a datos (SQL y stored procedures) del panel del conductor
    /// (<c>DashboardConductor.aspx</c>). Extraído del code-behind: la sesión, los
    /// controles/Request.Form, el mapeo a DTO, la auditoría y el logging permanecen en
    /// el code-behind; este servicio sólo ejecuta el SQL. No se modifica ningún SP.
    ///
    /// Nota: el envío de liquidación (transacción en btnEnviarLiquidacion_Click y sus
    /// helpers Insertar*) sigue en el code-behind porque lee Request.Form/hidden fields
    /// y campos de control; mover eso requiere reestructurar el orquestador y queda para
    /// un pase dedicado.
    /// </summary>
    public static class DashboardConductorService
    {
        /// <summary>Estaciones de peaje (<c>sp_DC_ObtenerEstacionesPeaje</c>).</summary>
        public static DataTable ObtenerEstacionesPeaje() =>
            DbHelper.ConsultarTablaSp("sp_DC_ObtenerEstacionesPeaje");

        /// <summary>
        /// Genera el número de orden vía <c>sp_DC_GenerarNumeroOrden</c> (parámetro de
        /// salida). Devuelve el valor generado, o <c>null</c>/vacío si el SP no lo retornó.
        /// </summary>
        public static string GenerarNumeroOrden()
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            using (SqlCommand cmd = new SqlCommand("sp_DC_GenerarNumeroOrden", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                SqlParameter pNumeroGenerado = new SqlParameter("@numeroGenerado", SqlDbType.VarChar, 50)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(pNumeroGenerado);

                conn.Open();
                cmd.ExecuteNonQuery();

                return pNumeroGenerado.Value?.ToString();
            }
        }

        /// <summary>Datos del viaje para la liquidación (<c>sp_DC_ObtenerDatosViajeParaLiquidacion</c>).</summary>
        public static DataTable ObtenerDatosViajeParaLiquidacion(int idViajeProgreso, int idConductorFallback) =>
            DbHelper.ConsultarTablaSp("sp_DC_ObtenerDatosViajeParaLiquidacion",
                DbHelper.Param("@idViajeProgreso", idViajeProgreso),
                DbHelper.Param("@idConductorFallback", idConductorFallback));

        /// <summary>
        /// Cuenta cuántos de <paramref name="idsViajes"/> pertenecen a
        /// <paramref name="idConductor"/> (IN parametrizado, sin interpolar valores).
        /// </summary>
        public static int ContarViajesDelConductor(List<int> idsViajes, int idConductor)
        {
            var paramNames = idsViajes.Select((_, i) => $"@id{i}").ToList();
            string sql = $"SELECT COUNT(*) FROM ViajesEnProgreso " +
                         $"WHERE idViajeProgreso IN ({string.Join(",", paramNames)}) " +
                         $"AND idConductor = @idConductor";

            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@idConductor", idConductor);
                for (int i = 0; i < idsViajes.Count; i++)
                    cmd.Parameters.AddWithValue($"@id{i}", idsViajes[i]);

                conn.Open();
                return (int)cmd.ExecuteScalar();
            }
        }

        /// <summary>
        /// Cuenta las órdenes con ese número que el conductor puede volver a enviar: suyas y
        /// rechazadas (REABIERTO). El número llega en un campo oculto del formulario, así que sin
        /// el estado un conductor podría reescribir una liquidación suya ya firmada o aprobada.
        /// </summary>
        public static int ContarOrdenDelConductor(string numeroOrden, int idConductor) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                @"SELECT COUNT(*) FROM OrdenViaje
                  WHERE numeroOrdenViaje = @numeroOrden AND idConductor = @idConductor
                    AND estadoAprobacion = 'REABIERTO' AND registradoPor = 'CONDUCTOR'",
                DbHelper.Param("@numeroOrden", numeroOrden),
                DbHelper.Param("@idConductor", idConductor)));

        /// <summary>Resultado del SP <c>sp_DC_RetirarLiquidacion</c> (parámetros de salida).</summary>
        public class RetiroLiquidacionResultado
        {
            public int Resultado { get; set; }
            public string Mensaje { get; set; }
            public string NumeroOrdenViaje { get; set; }
            public int IdViajeProgreso { get; set; }
        }

        /// <summary>Retira (reabre) una liquidación propia del conductor vía SP.</summary>
        public static RetiroLiquidacionResultado RetirarLiquidacion(int idOrdenViaje, int idConductor)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            using (SqlCommand cmd = new SqlCommand("sp_DC_RetirarLiquidacion", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@idOrdenViaje", idOrdenViaje);
                cmd.Parameters.AddWithValue("@idConductor", idConductor);

                SqlParameter pResultado = new SqlParameter("@resultado", SqlDbType.Int) { Direction = ParameterDirection.Output };
                SqlParameter pMensaje = new SqlParameter("@mensaje", SqlDbType.VarChar, 500) { Direction = ParameterDirection.Output };
                SqlParameter pNumeroOrden = new SqlParameter("@numeroOrdenViajeSalida", SqlDbType.VarChar, 50) { Direction = ParameterDirection.Output };
                SqlParameter pIdViaje = new SqlParameter("@idViajeProgresoSalida", SqlDbType.Int) { Direction = ParameterDirection.Output };

                cmd.Parameters.Add(pResultado);
                cmd.Parameters.Add(pMensaje);
                cmd.Parameters.Add(pNumeroOrden);
                cmd.Parameters.Add(pIdViaje);

                conn.Open();
                cmd.ExecuteNonQuery();

                return new RetiroLiquidacionResultado
                {
                    Resultado = (int)pResultado.Value,
                    Mensaje = pMensaje.Value?.ToString() ?? "",
                    NumeroOrdenViaje = pNumeroOrden.Value?.ToString() ?? "",
                    IdViajeProgreso = pIdViaje.Value != DBNull.Value ? (int)pIdViaje.Value : 0
                };
            }
        }

        // ------------------------------------------------------------------
        // Orden rechazada (REABIERTO): datos para volver a llenar el formulario
        // ------------------------------------------------------------------

        /// <summary>
        /// Orden rechazada de los viajes, con sus gastos por nombre de campo del formulario
        /// (<c>alimentacionSoles</c>, <c>descAlimentacion</c>…) para volver a mostrarlos, y lo que
        /// registró la administración (dinero entregado, descuentos), que se conserva al reenviar.
        /// </summary>
        public static OrdenRechazadaConductor ObtenerOrdenRechazada(List<int> idsViajes)
        {
            DataTable dt = DbHelper.ConsultarTablaSp("sp_DC_ObtenerOrdenRechazada",
                DbHelper.Param("@idsViajes", string.Join(",", idsViajes)));
            if (dt.Rows.Count == 0) return null;

            DataRow r = dt.Rows[0];
            var orden = new OrdenRechazadaConductor
            {
                NumeroOrdenViaje = r["numeroOrdenViaje"].ToString(),
                FechaSalida = r["fechaSalida"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["fechaSalida"]),
                FechaLlegada = r["fechaLlegada"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["fechaLlegada"]),
                // TIME llega como TimeSpan ("06:00:00"); los inputs usan HH:mm.
                HoraSalida = r["horaSalida"] is TimeSpan hs ? hs.ToString(@"hh\:mm") : "",
                HoraLlegadaDeclarada = dt.Columns.Contains("horaLlegadaDeclarada") && r["horaLlegadaDeclarada"] is TimeSpan hd ? hd.ToString(@"hh\:mm") : "",
                Observaciones = Liquidaciones.LiquidacionCalculos.ObservacionesDelConductor(r["observaciones"]?.ToString())
            };
            if (string.IsNullOrEmpty(orden.NumeroOrdenViaje)) return null;

            CargarFinancieros(orden);
            return orden;
        }

        /// <summary>
        /// Lo que registró la administración en una orden (dinero entregado, ingresos adicionales
        /// antiguos, descuentos y reintegros). Al reenviar una orden rechazada se borran sus datos
        /// financieros y se vuelven a insertar: esto evita que se pierda lo de la administración.
        /// </summary>
        public static OrdenRechazadaConductor ObtenerDatosAdministracion(string numeroOrdenViaje)
        {
            var orden = new OrdenRechazadaConductor { NumeroOrdenViaje = numeroOrdenViaje };
            CargarFinancieros(orden);
            return orden;
        }

        private static decimal Dec(SqlDataReader r, string col) => r[col] == DBNull.Value ? 0m : Convert.ToDecimal(r[col]);
        private static string Txt(SqlDataReader r, string col) => r[col] == DBNull.Value ? "" : r[col].ToString();
        private static bool Tiene(SqlDataReader r, string col)
        {
            for (int i = 0; i < r.FieldCount; i++)
                if (string.Equals(r.GetName(i), col, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void CargarFinancieros(OrdenRechazadaConductor o)
        {
            var g = o.Gastos;
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            using (SqlCommand cmd = new SqlCommand("sp_DC_ObtenerDatosFinancierosOrden", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", o.NumeroOrdenViaje);
                conn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    // 1. Ingresos (los registra la administración)
                    if (r.Read())
                    {
                        o.Ingresos.DespachoSoles = Dec(r, "despachoSoles");
                        o.Ingresos.DespachoDolares = Dec(r, "despachoDolares");
                        o.Ingresos.PrestamoSoles = Dec(r, "prestamoSoles");
                        o.Ingresos.PrestamoDolares = Dec(r, "prestamosDolares");
                        o.Ingresos.MensualidadSoles = Dec(r, "mensualidadSoles");
                        o.Ingresos.MensualidadDolares = Dec(r, "mensualidadDolares");
                        o.Ingresos.OtrosSoles = Dec(r, "otrosSoles");
                        o.Ingresos.OtrosDolares = Dec(r, "otrosDolares");
                        o.Ingresos.DescDespacho = Txt(r, "descDespacho");
                        o.Ingresos.DescPrestamo = Txt(r, "descPrestamo");
                        o.Ingresos.DescMensualidad = Txt(r, "descMensualidad");
                        o.Ingresos.DescOtros = Txt(r, "descOtrosAutorizados");
                    }

                    // 2. Egresos (totales por concepto)
                    r.NextResult();
                    if (r.Read())
                    {
                        Poner(g, "alimentacion", Dec(r, "alimentacionSoles"), Dec(r, "alimentacionDolares"), "descAlimentacion", Txt(r, "descAlimentacion"));
                        Poner(g, "apoyoSeguridad", Dec(r, "apoyoseguridadSoles"), Dec(r, "apoyoseguridadDolares"), "descApoyoSeguridad", Txt(r, "descApoyoSeguridad"));
                        Poner(g, "movilidad", Dec(r, "movilidadSoles"), Dec(r, "movilidadDolares"), "descMovilidad", Txt(r, "descMovilidad"));
                        Poner(g, "encapada", Dec(r, "encarpada_desencarpadaSoles"), Dec(r, "encarpada_desencarpadaDolares"), "descEncapada", Txt(r, "descEncarpadaDesencarpada"));
                        if (Tiene(r, "combustibleSoles"))
                        {
                            Poner(g, "reparaciones", Dec(r, "reparacionesSoles"), Dec(r, "reparacionesDolares"), "descReparaciones", Txt(r, "descReparaciones"));
                            Poner(g, "hospedaje", Dec(r, "hospedajeSoles"), Dec(r, "hospedajeDolares"), "descHospedaje", Txt(r, "descHospedaje"));
                            Poner(g, "combustible", Dec(r, "combustibleSoles"), Dec(r, "combustibleDolares"), "descCombustible", Txt(r, "descCombustible"));
                        }
                    }

                    // 3. Peajes: nacionales en soles y de Ecuador en dólares
                    r.NextResult();
                    decimal pNac = 0, pExt = 0; string dNac = "", dExt = "";
                    while (r.Read())
                    {
                        decimal s = Dec(r, "montoSoles"), d = Dec(r, "montoDolares");
                        if (s > 0) { pNac += s; if (dNac == "") dNac = Txt(r, "observaciones"); }
                        if (d > 0) { pExt += d; if (dExt == "") dExt = Txt(r, "observaciones"); }
                    }
                    if (pNac > 0) { g["peajesNacSoles"] = pNac.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); g["descPeajesNacionales"] = dNac; }
                    if (pExt > 0) { g["peajesExtDolares"] = pExt.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); g["descPeajesExtranjeros"] = dExt; }

                    // 4–6. Detalle por comprobante (liquidaciones antiguas): ya está sumado en Egresos.
                    r.NextResult(); r.NextResult(); r.NextResult();

                    // 7. Ingresos adicionales (formato antiguo; se conservan)
                    r.NextResult();
                    while (r.Read())
                        o.IngresosAdicionales.Add(new Models.Conductor.IngresoAdicionalData
                        {
                            Categoria = Txt(r, "nombreCategoria"), Descripcion = Txt(r, "descripcion"),
                            Soles = Dec(r, "soles"), Dolares = Dec(r, "dolares")
                        });

                    // 8. Gastos adicionales del conductor
                    r.NextResult();
                    while (r.Read())
                        o.GastosAdicionales.Add(new Models.Conductor.GastoAdicionalData
                        {
                            Categoria = Txt(r, "nombreCategoria"), Descripcion = Txt(r, "descripcion"),
                            Soles = Dec(r, "soles"), Dolares = Dec(r, "dolares")
                        });

                    // 9. Descuentos y reintegros (administración)
                    r.NextResult();
                    if (r.Read())
                    {
                        o.DescuentoSoles = Dec(r, "descuentoSoles");
                        o.DescuentoDolares = Dec(r, "descuentoDolares");
                        o.ReintegroSoles = Dec(r, "reintegroSoles");
                        o.ReintegroDolares = Dec(r, "reintegroDolares");
                    }
                }
            }
        }

        private static void Poner(Dictionary<string, string> g, string campo, decimal soles, decimal dolares, string campoDesc, string desc)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            if (soles > 0) g[campo + "Soles"] = soles.ToString("0.00", ic);
            if (dolares > 0) g[campo + "Dolares"] = dolares.ToString("0.00", ic);
            if ((soles > 0 || dolares > 0) && !string.IsNullOrWhiteSpace(desc)) g[campoDesc] = desc;
        }
    }

    /// <summary>Orden rechazada (o reenviada) del conductor, lista para el formulario y para conservar lo de la administración.</summary>
    public class OrdenRechazadaConductor
    {
        public string NumeroOrdenViaje { get; set; }
        public DateTime? FechaSalida { get; set; }
        public DateTime? FechaLlegada { get; set; }
        public string HoraSalida { get; set; }
        public string HoraLlegadaDeclarada { get; set; }
        public string Observaciones { get; set; }

        /// <summary>Valores del formulario de gastos por nombre de campo (montos en formato invariante).</summary>
        public Dictionary<string, string> Gastos { get; } = new Dictionary<string, string>();
        public List<Models.Conductor.GastoAdicionalData> GastosAdicionales { get; } = new List<Models.Conductor.GastoAdicionalData>();

        public Models.Liquidaciones.IngresosEntregados Ingresos { get; } = new Models.Liquidaciones.IngresosEntregados();
        public List<Models.Conductor.IngresoAdicionalData> IngresosAdicionales { get; } = new List<Models.Conductor.IngresoAdicionalData>();
        public decimal DescuentoSoles { get; set; }
        public decimal DescuentoDolares { get; set; }
        public decimal ReintegroSoles { get; set; }
        public decimal ReintegroDolares { get; set; }
    }
}
