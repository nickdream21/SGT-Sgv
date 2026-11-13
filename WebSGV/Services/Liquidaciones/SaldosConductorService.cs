using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Reportes;

namespace WebSGV.Services.Liquidaciones
{
    /// <summary>
    /// Cuenta corriente del conductor (2026-11-13): el saldo de cada liquidación aprobada
    /// (<see cref="LiquidacionCalculos.SaldoFinal"/>: positivo = se le DESCUENTA, negativo = se le REINTEGRA),
    /// el reporte de la quincena y el registro de cómo se saldó (<c>PagoSaldoConductor</c>).
    /// </summary>
    public static class SaldosConductorService
    {
        public static readonly IReadOnlyDictionary<string, string> Medios = new Dictionary<string, string>
        {
            { "PLANILLA", "Planilla de la quincena" },
            { "EFECTIVO", "Efectivo" },
            { "TRANSFERENCIA", "Transferencia" },
            { "DEPOSITO", "Depósito" },
            { "ADELANTO", "Del próximo adelanto" },
        };

        private static readonly CultureInfo Pe = CultureInfo.GetCultureInfo("es-PE");

        /// <summary>Quincena que contiene la fecha: del 1 al 15 o del 16 al fin de mes.</summary>
        public static Tuple<DateTime, DateTime> Quincena(DateTime fecha)
        {
            var d = fecha.Date;
            return d.Day <= 15
                ? Tuple.Create(new DateTime(d.Year, d.Month, 1), new DateTime(d.Year, d.Month, 15))
                : Tuple.Create(new DateTime(d.Year, d.Month, 16), new DateTime(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month)));
        }

        /// <summary>"1.ª quincena de noviembre 2026" / "2.ª quincena de ...".</summary>
        public static string NombreQuincena(DateTime desde) =>
            (desde.Day <= 15 ? "1.ª" : "2.ª") + " quincena de " + Pe.DateTimeFormat.GetMonthName(desde.Month).ToLowerInvariant() + " " + desde.Year;

        /// <summary>"A descontar" (el conductor devuelve), "A reintegrar" (la empresa le paga) o "Cuadrado".</summary>
        public static string Sentido(decimal saldoSoles, decimal saldoDolares)
        {
            decimal referencia = saldoSoles != 0 ? saldoSoles : saldoDolares;
            return referencia > 0 ? "A descontar" : referencia < 0 ? "A reintegrar" : "Cuadrado";
        }

        /// <summary>
        /// Liquidaciones aprobadas (con la conformidad firmada) cuya fecha de aprobación cae en el rango y, con
        /// <paramref name="conPendientesAnteriores"/>, también las de antes que siguen sin saldar.
        /// Columnas calculadas: EntregadoSoles/Dolares, GastosSoles/Dolares, SaldoSoles/Dolares y Sentido.
        /// </summary>
        public static DataTable Listar(DateTime desde, DateTime hasta, int idConductor, bool conPendientesAnteriores, bool soloPendientes,
            IEnumerable<int> idsOrdenes = null)
        {
            string ids = idsOrdenes == null ? null : string.Join(",", idsOrdenes);
            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT * FROM (
                    SELECT
                        ov.idOrdenViaje, ov.numeroOrdenViaje, ov.idConductor,
                        LTRIM(RTRIM(c.nombre)) + ' ' + LTRIM(RTRIM(ISNULL(c.apPaterno, ''))) + ' ' + LTRIM(RTRIM(ISNULL(c.apMaterno, ''))) AS Conductor,
                        c.dni AS Dni,
                        ov.fechaSalida, ov.fechaLlegada,
                        CAST(COALESCE(ov.fechaConformidad, ov.fechaAprobacion) AS DATE) AS FechaAprobacion,
                        ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0)
                          + ISNULL((SELECT SUM(soles) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS EntregadoSoles,
                        ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0)
                          + ISNULL((SELECT SUM(dolares) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS EntregadoDolares,
                        ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0)
                          + ISNULL(e.movilidadSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) + ISNULL(e.hospedajeSoles, 0) + ISNULL(e.combustibleSoles, 0)
                          + ISNULL((SELECT SUM(soles) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GastosSoles,
                        ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0)
                          + ISNULL(e.movilidadDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) + ISNULL(e.hospedajeDolares, 0) + ISNULL(e.combustibleDolares, 0)
                          + ISNULL((SELECT SUM(dolares) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GastosDolares,
                        ISNULL(dr.descuentoSoles, 0) AS DescuentoSoles, ISNULL(dr.descuentoDolares, 0) AS DescuentoDolares,
                        ISNULL(dr.reintegroSoles, 0) AS ReintegroSoles, ISNULL(dr.reintegroDolares, 0) AS ReintegroDolares,
                        p.idPagoSaldo, p.fechaPago, p.medio, p.numeroOperacion, p.observacion AS ObservacionPago
                    FROM OrdenViaje ov
                    INNER JOIN Conductor c ON c.idConductor = ov.idConductor
                    LEFT JOIN DescuentosReintegros dr ON dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1
                    LEFT JOIN Ingresos i ON i.numeroOrdenViaje = ov.numeroOrdenViaje
                    LEFT JOIN Egresos e ON e.numeroOrdenViaje = ov.numeroOrdenViaje
                    LEFT JOIN PagoSaldoConductorDetalle pd ON pd.idOrdenViaje = ov.idOrdenViaje AND pd.activo = 1
                    LEFT JOIN PagoSaldoConductor p ON p.idPagoSaldo = pd.idPagoSaldo
                    WHERE ov.estadoAprobacion = 'APROBADO'
                      AND (@idConductor <= 0 OR ov.idConductor = @idConductor)
                ) t
                WHERE (@ids IS NOT NULL AND t.idOrdenViaje IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@ids, ',')))
                   OR (@ids IS NULL
                       AND (t.FechaAprobacion BETWEEN @desde AND @hasta
                            OR (@conPendientes = 1 AND t.FechaAprobacion < @desde AND t.idPagoSaldo IS NULL))
                       AND (@soloPendientes = 0 OR t.idPagoSaldo IS NULL))
                ORDER BY t.Conductor, t.FechaAprobacion, t.numeroOrdenViaje",
                DbHelper.Param("@desde", desde.Date),
                DbHelper.Param("@hasta", hasta.Date),
                DbHelper.Param("@idConductor", idConductor),
                DbHelper.Param("@conPendientes", conPendientesAnteriores),
                DbHelper.Param("@soloPendientes", soloPendientes),
                DbHelper.Param("@ids", (object)ids ?? DBNull.Value));

            dt.Columns.Add("SaldoSoles", typeof(decimal));
            dt.Columns.Add("SaldoDolares", typeof(decimal));
            dt.Columns.Add("Sentido", typeof(string));
            dt.Columns.Add("Estado", typeof(string));
            foreach (DataRow r in dt.Rows)
            {
                decimal s = LiquidacionCalculos.SaldoFinal(D(r, "EntregadoSoles"), D(r, "GastosSoles"), D(r, "DescuentoSoles"), D(r, "ReintegroSoles"));
                decimal d = LiquidacionCalculos.SaldoFinal(D(r, "EntregadoDolares"), D(r, "GastosDolares"), D(r, "DescuentoDolares"), D(r, "ReintegroDolares"));
                r["SaldoSoles"] = s;
                r["SaldoDolares"] = d;
                r["Sentido"] = Sentido(s, d);
                r["Estado"] = r.IsNull("idPagoSaldo")
                    ? "Pendiente"
                    : "Saldado " + Convert.ToDateTime(r["fechaPago"]).ToString("dd/MM/yyyy") + " · " + NombreMedio(Convert.ToString(r["medio"]));
            }
            return dt;
        }

        public static string NombreMedio(string medio) =>
            medio != null && Medios.TryGetValue(medio, out var n) ? n : medio;

        /// <summary>
        /// Registra cómo se saldaron las liquidaciones elegidas de un conductor. El saldo se vuelve a calcular aquí
        /// (no se confía en lo que muestra la pantalla). Devuelve el id del registro.
        /// </summary>
        public static int RegistrarPago(int idConductor, IList<int> idsOrdenes, DateTime fechaPago, string medio,
            string numeroOperacion, string observacion, string usuario)
        {
            if (idsOrdenes == null || idsOrdenes.Count == 0) throw new ErrorNegocioException("Elige al menos una liquidación.");
            if (!Medios.ContainsKey(medio ?? "")) throw new ErrorNegocioException("Elige cómo se saldó (planilla, efectivo...).");

            DataTable dt = Listar(new DateTime(2000, 1, 1), new DateTime(2100, 1, 1), idConductor, false, false, idsOrdenes);
            if (dt.Rows.Count != idsOrdenes.Distinct().Count())
                throw new ErrorNegocioException("Alguna liquidación no es de este conductor o no está aprobada. Actualiza la lista.");

            string detalle = "[" + string.Join(",", dt.Rows.Cast<DataRow>().Select(r =>
                "{\"idOrdenViaje\":" + Convert.ToInt32(r["idOrdenViaje"]) +
                ",\"saldoSoles\":" + D(r, "SaldoSoles").ToString(CultureInfo.InvariantCulture) +
                ",\"saldoDolares\":" + D(r, "SaldoDolares").ToString(CultureInfo.InvariantCulture) + "}")) + "]";

            var salida = new SqlParameter("@idPagoSaldo", SqlDbType.Int) { Direction = ParameterDirection.Output };
            DbHelper.EjecutarNonQuerySp("sp_LD_RegistrarPagoSaldo",
                DbHelper.Param("@idConductor", idConductor),
                DbHelper.Param("@fechaPago", fechaPago.Date),
                DbHelper.Param("@medio", medio),
                DbHelper.Param("@numeroOperacion", string.IsNullOrWhiteSpace(numeroOperacion) ? (object)DBNull.Value : numeroOperacion.Trim()),
                DbHelper.Param("@observacion", string.IsNullOrWhiteSpace(observacion) ? (object)DBNull.Value : observacion.Trim()),
                DbHelper.Param("@detalle", detalle),
                DbHelper.Param("@usuario", usuario),
                salida);
            int id = Convert.ToInt32(salida.Value);

            decimal totS = dt.Rows.Cast<DataRow>().Sum(r => D(r, "SaldoSoles"));
            decimal totD = dt.Rows.Cast<DataRow>().Sum(r => D(r, "SaldoDolares"));
            AuditoriaHelper.Registrar("INSERT", "PagoSaldoConductor", id.ToString(),
                $"{NombreMedio(medio)} del {fechaPago:dd/MM/yyyy}: {string.Join(", ", dt.Rows.Cast<DataRow>().Select(r => r["numeroOrdenViaje"]))}. " +
                LiquidacionCalculos.DescribirSaldo(totS, totD));
            return id;
        }

        public static void AnularPago(int idPagoSaldo, string motivo, string usuario)
        {
            DbHelper.EjecutarNonQuerySp("sp_LD_AnularPagoSaldo",
                DbHelper.Param("@idPagoSaldo", idPagoSaldo),
                DbHelper.Param("@motivo", motivo ?? ""),
                DbHelper.Param("@usuario", usuario));
            AuditoriaHelper.Registrar("ANULAR", "PagoSaldoConductor", idPagoSaldo.ToString(), "Pago o descuento anulado. Motivo: " + motivo);
        }

        /// <summary>Excel / PDF de la quincena: una sección por conductor con el neto a descontar o reintegrar.</summary>
        public static ReporteTabular Reporte(DataTable dt, IEnumerable<string> filtros, string generadoPor)
        {
            var r = new ReporteTabular
            {
                Titulo = "Descuentos y reintegros a conductores",
                NombreHoja = "Quincena",
                Filas = dt.Rows.Cast<DataRow>().ToList(),
                GeneradoPor = generadoPor,
                Seccion = f => Convert.ToString(f["Conductor"]),
                Nota = "Saldo de cada liquidación aprobada = entregado − gastos + descuentos − reintegros. " +
                       "Positivo: el conductor devuelve (se le descuenta). Negativo: la empresa le reintegra.",
            };
            r.Filtros.AddRange(filtros);
            void Col(string t, TipoColumnaReporte tipo, float ancho, Func<DataRow, object> v) =>
                r.Columnas.Add(new ColumnaReporte { Titulo = t, Tipo = tipo, Ancho = ancho, Valor = v });
            Col("N° LIQ.", TipoColumnaReporte.Codigo, 1.2f, f => f["numeroOrdenViaje"]);
            Col("CONDUCTOR", TipoColumnaReporte.Texto, 2.2f, f => f["Conductor"]);
            Col("DNI", TipoColumnaReporte.Codigo, 0.9f, f => f["Dni"]);
            Col("APROBADA", TipoColumnaReporte.Fecha, 1f, f => f["FechaAprobacion"]);
            Col("ENTREGADO S/", TipoColumnaReporte.Soles, 1f, f => f["EntregadoSoles"]);
            Col("GASTOS S/", TipoColumnaReporte.Soles, 1f, f => f["GastosSoles"]);
            Col("SALDO S/", TipoColumnaReporte.Soles, 1f, f => f["SaldoSoles"]);
            Col("SALDO US$", TipoColumnaReporte.Dolares, 1f, f => f["SaldoDolares"]);
            Col("RESULTADO", TipoColumnaReporte.Codigo, 1.1f, f => f["Sentido"]);
            Col("ESTADO", TipoColumnaReporte.Texto, 1.8f, f => f["Estado"]);

            var filas = dt.Rows.Cast<DataRow>().ToList();
            decimal descontarS = filas.Where(f => D(f, "SaldoSoles") > 0).Sum(f => D(f, "SaldoSoles"));
            decimal reintegrarS = -filas.Where(f => D(f, "SaldoSoles") < 0).Sum(f => D(f, "SaldoSoles"));
            r.Resumen.Add(new LineaResumen { Etiqueta = "A descontar a conductores (S/)", Valor = descontarS, Tipo = TipoColumnaReporte.Soles });
            r.Resumen.Add(new LineaResumen { Etiqueta = "A reintegrar a conductores (S/)", Valor = reintegrarS, Tipo = TipoColumnaReporte.Soles });
            decimal netoD = filas.Sum(f => D(f, "SaldoDolares"));
            if (netoD != 0)
                r.Resumen.Add(new LineaResumen { Etiqueta = "Neto en dólares (+ descontar / − reintegrar)", Valor = netoD, Tipo = TipoColumnaReporte.Dolares });
            r.Resumen.Add(new LineaResumen { Etiqueta = "Liquidaciones pendientes de saldar", Valor = filas.Count(f => f.IsNull("idPagoSaldo")), Tipo = TipoColumnaReporte.Entero, Destacada = true });
            return r;
        }

        private static decimal D(DataRow r, string c) => r.IsNull(c) ? 0m : Convert.ToDecimal(r[c]);
    }
}
