using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Helpers;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Datos del período del Dashboard de exportación: los viajes de Vitapro del período y del anterior, listos para
    /// <see cref="IndicadoresExportacion.Resumir"/>. Un viaje CANCELADO (su despacho de carga se anuló,
    /// sp_SE_SincronizarDespachos) no se hizo: no cuenta en los viajes programados, los "sin llegada", los tránsitos ni
    /// los pedidos.
    /// </summary>
    public static class DashboardExportacionService
    {
        /// <summary>Rango de un período por F.H. Programación: el mes (o el año con <paramref name="mes"/> 0) y el anterior.</summary>
        public sealed class Rango
        {
            public DateTime Desde { get; set; }
            public DateTime Hasta { get; set; }
            public DateTime DesdeAnterior { get; set; }
        }

        public static Rango RangoDelPeriodo(int anio, int mes)
        {
            bool porMes = mes >= 1 && mes <= 12;
            DateTime desde = porMes ? new DateTime(anio, mes, 1) : new DateTime(anio, 1, 1);
            return new Rango
            {
                Desde = desde,
                Hasta = porMes ? desde.AddMonths(1) : desde.AddYears(1),
                DesdeAnterior = porMes ? desde.AddMonths(-1) : desde.AddYears(-1),
            };
        }

        /// <summary>Años con algún viaje activo con F.H. Programación (para el combo Año del dashboard; sin orden).</summary>
        public static List<int> AniosConViajes()
        {
            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT DISTINCT YEAR(fhProgramacion) AS anio
                FROM SeguimientoExportacion
                WHERE activo = 1 AND fhProgramacion IS NOT NULL");
            return dt.Rows.Cast<DataRow>().Where(r => r["anio"] != DBNull.Value).Select(r => Convert.ToInt32(r["anio"])).ToList();
        }

        /// <summary>Viajes de Vitapro del período y del anterior en una consulta, con cuántas de sus horas puso el GPS.</summary>
        public static ResumenPeriodo CargarPeriodo(int anio, int mes)
        {
            var rango = RangoDelPeriodo(anio, mes);

            string columnas = string.Join(", ", IndicadoresExportacion.ColumnasHito.Concat(new[] { "fhLlegadaAlmacen" })
                .Distinct().Select(c => "se." + c));
            string enLista = string.Join(", ", IndicadoresExportacion.ColumnasHito.Select(c => "'" + c + "'"));

            // Las columnas vienen de HitosSeguimiento (constantes del código), no del usuario.
            DataTable dt = DbHelper.ConsultarTabla($@"
                SELECT se.idSeguimiento, se.cliente, se.bodegaDescarga, se.bodegaEcuatoriana, se.carreta,
                       se.tracto1, se.conductorOrigen, se.tracto2, se.fhProgramacion,
                       se.sacosRobados, se.sacosRotos, se.sacosMojados, {columnas},
                       (SELECT COUNT(*) FROM SeguimientoHito h
                         WHERE h.idSeguimiento = se.idSeguimiento AND h.columna IN ({enLista})
                           AND h.fuente IN ('GPS', 'ONWAY', 'GRUPO') AND h.idUsuarioCorreccion IS NULL) AS hitosGps
                FROM SeguimientoExportacion se
                WHERE se.activo = 1 AND se.tipoRuta IS NULL AND ISNULL(se.estado, '') <> 'CANCELADO'
                  AND se.fhProgramacion >= @desdeAnt AND se.fhProgramacion < @hasta",
                DbHelper.Param("@desdeAnt", rango.DesdeAnterior),
                DbHelper.Param("@hasta", rango.Hasta));

            var viajes = dt.Rows.Cast<DataRow>().Select(Viaje).ToList();
            return IndicadoresExportacion.Resumir(
                viajes.Where(v => v.FhProgramacion >= rango.Desde).ToList(),
                viajes.Where(v => v.FhProgramacion < rango.Desde).ToList(),
                anio, mes);
        }

        private static ViajeIndicador Viaje(DataRow r)
        {
            var horas = IndicadoresExportacion.ColumnasHito.Concat(new[] { "fhLlegadaAlmacen" }).Distinct()
                .ToDictionary(c => c, c => r.IsNull(c) ? (DateTime?)null : Convert.ToDateTime(r[c]), StringComparer.OrdinalIgnoreCase);
            var v = new ViajeIndicador
            {
                Id = Convert.ToInt32(r["idSeguimiento"]),
                Pedido = Texto(r, "cliente"),
                Planta = Texto(r, "bodegaDescarga") ?? Texto(r, "bodegaEcuatoriana"),
                Carreta = Texto(r, "carreta"),
                Tracto1 = Texto(r, "tracto1"),
                Chofer1 = Texto(r, "conductorOrigen"),
                Tracto2 = Texto(r, "tracto2"),
                FhProgramacion = Convert.ToDateTime(r["fhProgramacion"]),
                Horas = horas,
                SacosRobados = Entero(r, "sacosRobados"),
                SacosRotos = Entero(r, "sacosRotos"),
                SacosMojados = Entero(r, "sacosMojados"),
            };
            // Un hito del GPS sin la hora en el viaje (borrada a mano) no cuenta.
            v.HitosGps = Math.Min(Entero(r, "hitosGps"), IndicadoresExportacion.ColumnasHito.Count(c => v.Hora(c).HasValue));
            return v;
        }

        private static string Texto(DataRow r, string c) =>
            r.IsNull(c) || string.IsNullOrWhiteSpace(r[c].ToString()) ? null : r[c].ToString().Trim();

        private static int Entero(DataRow r, string c) => r.IsNull(c) ? 0 : Convert.ToInt32(r[c]);
    }
}
