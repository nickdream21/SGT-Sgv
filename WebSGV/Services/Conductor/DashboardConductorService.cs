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
    /// controles/Request.Form, la auditoría y el logging permanecen en el code-behind;
    /// este servicio ejecuta el SQL y, desde E6-18, devuelve las lecturas del panel como
    /// DTO (datos del conductor, viajes activos, despachos, rechazo e historial filtrado
    /// en SQL). La página no tiene SQL. No se modifica ningún SP.
    ///
    /// Nota: el envío de liquidación (transacción en btnEnviarLiquidacion_Click y sus
    /// helpers Insertar*) sigue en el code-behind porque lee Request.Form/hidden fields
    /// y campos de control; mover eso requiere reestructurar el orquestador y queda para
    /// un pase dedicado.
    /// </summary>
    public static class DashboardConductorService
    {
        // ------------------------------------------------------------------
        // Lecturas del panel (E6-18): siempre con el conductor de la sesión o con
        // los viajes que ya se leyeron para él.
        // ------------------------------------------------------------------

        /// <summary>Nombre y documento del conductor (<c>sp_DC_ObtenerDatosConductor</c>); <c>null</c> si no existe.</summary>
        public static DatosConductorPanel ObtenerDatosConductor(int idConductor)
        {
            using (DataTable dt = DbHelper.ConsultarTablaSp("sp_DC_ObtenerDatosConductor",
                DbHelper.Param("@idConductor", idConductor)))
            {
                if (dt.Rows.Count == 0) return null;
                DataRow r = dt.Rows[0];
                return new DatosConductorPanel
                {
                    NombreCompleto = r["nombreCompleto"].ToString(),
                    Dni = r["DNI"].ToString()
                };
            }
        }

        /// <summary>
        /// Viajes ABIERTOS del conductor (suyos o con un despacho activo suyo), el más antiguo primero
        /// (<c>sp_DC_ObtenerViajesActivosConductor</c>).
        /// </summary>
        public static List<ViajeActivoConductor> ObtenerViajesActivos(int idConductor)
        {
            using (DataTable dt = DbHelper.ConsultarTablaSp("sp_DC_ObtenerViajesActivosConductor",
                DbHelper.Param("@idConductor", idConductor)))
            {
                return dt.Rows.Cast<DataRow>().Select(r => new ViajeActivoConductor
                {
                    IdViajeProgreso = Convert.ToInt32(r["idViajeProgreso"]),
                    NumeroViajeProgreso = r["numeroViajeProgreso"].ToString(),
                    IdConductor = Convert.ToInt32(r["idConductor"]),
                    NombreConductor = r["nombreConductor"].ToString(),
                    FechaInicio = Convert.ToDateTime(r["fechaInicio"]),
                    FechaUltimaActividad = Convert.ToDateTime(r["fechaUltimaActividad"]),
                    EstadoViaje = r["estadoViaje"].ToString(),
                    DescripcionViaje = r["descripcionViaje"]?.ToString() ?? "",
                    CantidadDespachos = Convert.ToInt32(r["cantidadDespachos"]),
                    EsInternacional = r["esInternacional"] != DBNull.Value && Convert.ToBoolean(r["esInternacional"])
                }).ToList();
            }
        }

        /// <summary>Despachos activos de esos viajes, el más reciente primero (<c>sp_DC_ObtenerDespachosViajesActivos</c>).</summary>
        public static List<DespachoViajeActivo> ObtenerDespachosViajesActivos(List<int> idsViajes)
        {
            using (DataTable dt = DbHelper.ConsultarTablaSp("sp_DC_ObtenerDespachosViajesActivos",
                DbHelper.Param("@idsViajes", string.Join(",", idsViajes))))
            {
                return dt.Rows.Cast<DataRow>().Select(r => new DespachoViajeActivo
                {
                    IdDespacho = Convert.ToInt32(r["idDespacho"]),
                    NumeroDespacho = r["numeroDespacho"].ToString(),
                    FechaDespacho = Convert.ToDateTime(r["fechaDespacho"]),
                    Cliente = r["nombreCliente"].ToString(),
                    TipoOperacion = r["tipoOperacion"].ToString(),
                    LugarOperacion = r["lugarOperacion"].ToString(),
                    PlacaTracto = r["placaTracto"].ToString(),
                    PlacaCarreta = r["placaCarreta"].ToString(),
                    EstadoDespacho = r["estadoDespacho"].ToString(),
                    GuiaRemitente = r["guiaRemitente"].ToString(),
                    GuiaTransportista = r["guiaTransportista"].ToString(),
                    NumeroCPIC = r["numeroCPIC"].ToString()
                }).ToList();
            }
        }

        /// <summary>
        /// Último rechazo con motivo (REABIERTO) de una orden de esos viajes
        /// (<c>sp_DC_VerificarObservacionesRechazo</c>); <c>null</c> si no hay.
        /// </summary>
        public static RechazoOrdenConductor ObtenerUltimoRechazo(List<int> idsViajes)
        {
            using (DataTable dt = DbHelper.ConsultarTablaSp("sp_DC_VerificarObservacionesRechazo",
                DbHelper.Param("@idsViajes", string.Join(",", idsViajes))))
            {
                if (dt.Rows.Count == 0) return null;
                DataRow r = dt.Rows[0];
                return new RechazoOrdenConductor
                {
                    NumeroOrdenViaje = r["numeroOrdenViaje"].ToString(),
                    Observaciones = r["observacionesRechazo"].ToString(),
                    FechaRechazo = r["fechaRechazo"] != DBNull.Value ? Convert.ToDateTime(r["fechaRechazo"]) : (DateTime?)null,
                    // DBNull.ToString() es "": "Administrador" solo si la columna no viniera (igual que antes).
                    RechazadoPor = r["rechazadoPor"]?.ToString() ?? "Administrador"
                };
            }
        }

        /// <summary>
        /// Grupo de un estado de la orden para el filtro del historial y su texto (la BD usó APROBADO o
        /// COMPLETADO, REABIERTO o RECHAZADO); cualquier otro valor es PENDIENTE ("En revisión").
        /// Debe coincidir con el CASE de <see cref="SqlHistorial"/>.
        /// </summary>
        public static string GrupoEstado(string estado)
        {
            switch ((estado ?? "").ToUpperInvariant())
            {
                case "APROBADO":
                case "COMPLETADO": return "APROBADO";
                case "REABIERTO":
                case "RECHAZADO": return "RECHAZADO";
                case "POR_CONFORMAR": return "POR_CONFORMAR";
                default: return "PENDIENTE";
            }
        }

        /// <summary>
        /// Historial del conductor (sus liquidaciones, la más reciente primero) filtrado en SQL por la fecha de
        /// salida (días completos; sin fecha de salida no entra si hay algún límite) y por el grupo de estado, más
        /// las POR_CONFORMAR sin filtros (el aviso de conformidad pendiente no depende de ellos). Mismas columnas
        /// y totales que <c>sp_DC_ObtenerHistorialLiquidaciones</c>, sin los ingresos que la pantalla no usa.
        /// </summary>
        private const string SqlHistorial = @"
SELECT
    ov.idOrdenViaje,
    ov.numeroOrdenViaje,
    ov.fechaSalida,
    ov.fechaLlegada,
    ISNULL(ov.estadoAprobacion, 'PENDIENTE') AS estadoAprobacion,
    CAST(CASE WHEN ov.idFirmaConductor IS NULL THEN 0 ELSE 1 END AS BIT) AS firmada,
    -- Ya la revisó la administración (se envió a su conformidad al menos una vez): no se puede retirar.
    CAST(CASE WHEN ov.fechaEnvioConformidad IS NULL THEN 0 ELSE 1 END AS BIT) AS revisada,
    (ISNULL(eg.peajesSoles, 0) + ISNULL(eg.alimentacionSoles, 0) +
     ISNULL(eg.apoyoseguridadSoles, 0) + ISNULL(eg.reparacionesVariosSoles, 0) +
     ISNULL(eg.movilidadSoles, 0) + ISNULL(eg.encarpada_desencarpadaSoles, 0) +
     ISNULL(eg.hospedajeSoles, 0) + ISNULL(eg.combustibleSoles, 0) +
     ISNULL((SELECT SUM(ca.soles) FROM CategoriasAdicionales ca WHERE ca.numeroOrdenViaje = ov.numeroOrdenViaje), 0)) AS totalGastosSoles,
    (ISNULL(eg.peajesDolares, 0) + ISNULL(eg.alimentacionDolares, 0) +
     ISNULL(eg.apoyoseguridadDolares, 0) + ISNULL(eg.repacionesVariosDolares, 0) +
     ISNULL(eg.movilidadDolares, 0) + ISNULL(eg.encarpada_desencarpadaDolares, 0) +
     ISNULL(eg.hospedajeDolares, 0) + ISNULL(eg.combustibleDolares, 0) +
     ISNULL((SELECT SUM(ca.dolares) FROM CategoriasAdicionales ca WHERE ca.numeroOrdenViaje = ov.numeroOrdenViaje), 0)) AS totalGastosDolares
FROM OrdenViaje ov
LEFT JOIN Egresos eg ON ov.numeroOrdenViaje = eg.numeroOrdenViaje
WHERE ov.idConductor = @idConductor
    AND ov.registradoPor = 'CONDUCTOR'
    AND (@desde IS NULL OR ov.fechaSalida >= @desde)
    AND (@hasta IS NULL OR ov.fechaSalida <= @hasta)
    AND (@grupoEstado IS NULL OR
         CASE UPPER(ISNULL(ov.estadoAprobacion, 'PENDIENTE'))
             WHEN 'APROBADO' THEN 'APROBADO' WHEN 'COMPLETADO' THEN 'APROBADO'
             WHEN 'REABIERTO' THEN 'RECHAZADO' WHEN 'RECHAZADO' THEN 'RECHAZADO'
             WHEN 'POR_CONFORMAR' THEN 'POR_CONFORMAR'
             ELSE 'PENDIENTE' END = @grupoEstado)
ORDER BY ov.fechaRegistro DESC, ov.idOrdenViaje DESC;

SELECT ov.idOrdenViaje, ov.numeroOrdenViaje
FROM OrdenViaje ov
WHERE ov.idConductor = @idConductor
    AND ov.registradoPor = 'CONDUCTOR'
    AND UPPER(ov.estadoAprobacion) = 'POR_CONFORMAR'
ORDER BY ov.fechaRegistro DESC, ov.idOrdenViaje DESC;";

        /// <summary>
        /// Historial de liquidaciones de <paramref name="idConductor"/> (el de la sesión) con los filtros de la
        /// pantalla: <paramref name="desde"/>/<paramref name="hasta"/> por la fecha de salida (se toma solo el día)
        /// y <paramref name="estadoFiltro"/> (valor del desplegable, vacío = todos; se compara por grupo).
        /// </summary>
        public static HistorialConductor ObtenerHistorial(int idConductor, DateTime? desde, DateTime? hasta, string estadoFiltro)
        {
            var resultado = new HistorialConductor();
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            using (SqlCommand cmd = new SqlCommand(SqlHistorial, conn))
            {
                cmd.Parameters.Add("@idConductor", SqlDbType.Int).Value = idConductor;
                cmd.Parameters.Add("@desde", SqlDbType.Date).Value = (object)desde?.Date ?? DBNull.Value;
                cmd.Parameters.Add("@hasta", SqlDbType.Date).Value = (object)hasta?.Date ?? DBNull.Value;
                cmd.Parameters.Add("@grupoEstado", SqlDbType.VarChar, 20).Value =
                    string.IsNullOrEmpty(estadoFiltro) ? (object)DBNull.Value : GrupoEstado(estadoFiltro);
                conn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        resultado.Filas.Add(new FilaHistorialConductor
                        {
                            IdOrdenViaje = Convert.ToInt32(r["idOrdenViaje"]),
                            NumeroViaje = r["numeroOrdenViaje"].ToString(),
                            FechaSalida = r["fechaSalida"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["fechaSalida"]),
                            FechaLlegada = r["fechaLlegada"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["fechaLlegada"]),
                            GastosSoles = Dec(r, "totalGastosSoles"),
                            GastosDolares = Dec(r, "totalGastosDolares"),
                            Estado = r["estadoAprobacion"].ToString().ToUpperInvariant(),
                            Firmada = r["firmada"] != DBNull.Value && Convert.ToBoolean(r["firmada"]),
                            Revisada = r["revisada"] != DBNull.Value && Convert.ToBoolean(r["revisada"])
                        });

                    r.NextResult();
                    while (r.Read())
                        resultado.PorConformar.Add(new OrdenPorConformarConductor
                        {
                            IdOrdenViaje = Convert.ToInt32(r["idOrdenViaje"]),
                            NumeroViaje = r["numeroOrdenViaje"].ToString()
                        });
                }
            }
            return resultado;
        }


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

        /// <summary>
        /// Tracto y carreta del viaje para la liquidación (<c>sp_DC_ObtenerDatosViajeParaLiquidacion</c>).
        /// Solo devuelve una fila si el viaje es de <paramref name="idConductor"/> (el de la sesión).
        /// </summary>
        public static DataTable ObtenerDatosViajeParaLiquidacion(int idViajeProgreso, int idConductor) =>
            DbHelper.ConsultarTablaSp("sp_DC_ObtenerDatosViajeParaLiquidacion",
                DbHelper.Param("@idViajeProgreso", idViajeProgreso),
                DbHelper.Param("@idConductor", idConductor));

        /// <summary>
        /// Cuenta cuántos de <paramref name="idsViajes"/> pertenecen a
        /// <paramref name="idConductor"/> (IN parametrizado, sin interpolar valores).
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
            Justification = "El SQL solo concatena los nombres @id0..@idN generados aquí; los ids van como parámetros.")]
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
                        // Los motivos (sp_DC_ObtenerDatosFinancierosOrden los devuelve desde 2026-11-15):
                        // sin ellos, al reenviar la orden el ajuste quedaba sin su motivo.
                        if (Tiene(r, "observacionesDescuento")) o.MotivoDescuento = Txt(r, "observacionesDescuento");
                        if (Tiene(r, "observacionesReintegro")) o.MotivoReintegro = Txt(r, "observacionesReintegro");
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

    /// <summary>Cabecera del panel: nombre y documento del conductor.</summary>
    public class DatosConductorPanel
    {
        public string NombreCompleto { get; set; }
        public string Dni { get; set; }
    }

    /// <summary>Viaje ABIERTO del conductor (pestaña Mi viaje y formulario de liquidación).</summary>
    public class ViajeActivoConductor
    {
        public int IdViajeProgreso { get; set; }
        public string NumeroViajeProgreso { get; set; }
        public int IdConductor { get; set; }
        public string NombreConductor { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaUltimaActividad { get; set; }
        public string EstadoViaje { get; set; }
        public string DescripcionViaje { get; set; }
        public int CantidadDespachos { get; set; }
        public bool EsInternacional { get; set; }
    }

    /// <summary>Despacho activo de un viaje en curso (lista de la pestaña Mi viaje).</summary>
    public class DespachoViajeActivo
    {
        public int IdDespacho { get; set; }
        public string NumeroDespacho { get; set; }
        public DateTime FechaDespacho { get; set; }
        public string Cliente { get; set; }
        public string TipoOperacion { get; set; }
        public string LugarOperacion { get; set; }
        public string PlacaTracto { get; set; }
        public string PlacaCarreta { get; set; }
        public string EstadoDespacho { get; set; }
        public string GuiaRemitente { get; set; }
        public string GuiaTransportista { get; set; }
        public string NumeroCPIC { get; set; }
    }

    /// <summary>Último rechazo con motivo de una orden de los viajes en curso.</summary>
    public class RechazoOrdenConductor
    {
        public string NumeroOrdenViaje { get; set; }
        public string Observaciones { get; set; }
        public DateTime? FechaRechazo { get; set; }
        public string RechazadoPor { get; set; }
    }

    /// <summary>Historial del conductor ya filtrado y sus órdenes por conformar (sin filtros).</summary>
    public class HistorialConductor
    {
        public List<FilaHistorialConductor> Filas { get; } = new List<FilaHistorialConductor>();
        public List<OrdenPorConformarConductor> PorConformar { get; } = new List<OrdenPorConformarConductor>();
    }

    /// <summary>Una liquidación del historial (los nombres son los que enlaza el repetidor).</summary>
    public class FilaHistorialConductor
    {
        public int IdOrdenViaje { get; set; }
        public string NumeroViaje { get; set; }
        public DateTime? FechaSalida { get; set; }
        public DateTime? FechaLlegada { get; set; }
        public decimal GastosSoles { get; set; }
        public decimal GastosDolares { get; set; }
        /// <summary>Estado tal como está en la BD, en mayúsculas (la página compara con "PENDIENTE" y "POR_CONFORMAR").</summary>
        public string Estado { get; set; }
        public bool Firmada { get; set; }
        public bool Revisada { get; set; }
    }

    /// <summary>Orden que espera la conformidad del conductor (aviso de arriba del panel).</summary>
    public class OrdenPorConformarConductor
    {
        public int IdOrdenViaje { get; set; }
        public string NumeroViaje { get; set; }
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
        public string MotivoDescuento { get; set; }
        public string MotivoReintegro { get; set; }
    }
}
