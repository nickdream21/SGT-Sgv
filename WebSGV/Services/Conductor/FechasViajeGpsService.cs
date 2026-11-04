using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Helpers;

namespace WebSGV.Services.Conductor
{
    /// <summary>Un tramo del seguimiento que manejó el conductor, según el GPS.</summary>
    public class TramoGps
    {
        /// <summary>Salida de la base Sullana (null si el tramo no empieza en la base, p. ej. un tercero que carga en Trujillo).</summary>
        public DateTime? Salida { get; set; }
        /// <summary>Primer hito del tramo (salida de base o llegada a la planta de carga): ordena los tramos.</summary>
        public DateTime? Inicio { get; set; }
        /// <summary>Vuelta a la base Sullana (null si aún no vuelve o el tramo termina en otro lugar, p. ej. la cochera de Trujillo).</summary>
        public DateTime? Llegada { get; set; }
        /// <summary>El tramo pasa a Ecuador con este tracto (peajes en dólares, gastos en dólares).</summary>
        public bool CruzaFrontera { get; set; }
    }

    /// <summary>
    /// Fechas del viaje para la liquidación. Lo que el GPS no dio queda en <c>null</c> y el
    /// conductor lo registra a mano.
    /// </summary>
    public class FechasViajeGps
    {
        public DateTime? Salida { get; set; }
        public DateTime? Llegada { get; set; }
        /// <summary>Algún tramo del viaje cruza a Ecuador.</summary>
        public bool CruzaFrontera { get; set; }
        /// <summary>Hay seguimiento GPS del viaje (aunque falten la salida o la llegada a la base).</summary>
        public bool TieneSeguimiento { get; set; }

        public bool SalidaPorGps => Salida.HasValue;
        public bool LlegadaPorGps => Llegada.HasValue;
    }

    /// <summary>
    /// Salida y llegada a la base de los viajes que el conductor va a liquidar, tomadas de los
    /// hitos del seguimiento (GPS), para que no las escriba a mano.
    ///
    /// Cada despacho del viaje está enlazado a un seguimiento y el tramo que hizo su tracto depende de la ruta:
    /// <list type="bullet">
    /// <item>Vitapro, despacho de carga (<c>idDespachoOrigen</c>): base → Trujillo → base
    ///   (<c>fhSalidaBase1</c> → <c>fhLlegadaBase2</c>); si el mismo tracto sigue a Ecuador y no hay
    ///   despacho de descarga aparte, termina en <c>fhLlegadaBaseFinal</c>.</item>
    /// <item>Vitapro, despacho de descarga (<c>idDespachoDestino</c>): base → Ecuador → base
    ///   (<c>fhSalidaBase2</c> → <c>fhLlegadaBaseFinal</c>).</item>
    /// <item>Terceros (<c>tipoRuta</c>): no salen de la base, empiezan al llegar a la planta de carga
    ///   (<c>fhLlegadaTrujillo</c>: Cartavio, Chapaira, Paita). Crisar con relevo vuelve a la base en
    ///   <c>fhLlegadaBase2</c>; Crisar sin relevo, Afrecho y Polinplast terminan en
    ///   <c>fhLlegadaBaseFinal</c>, que solo es llegada a la base si <c>lugarFin</c> = BASE (si quedó
    ///   en la cochera o en Vitapro Trujillo, el chofer sigue trabajando y no volvió).</item>
    /// </list>
    /// La salida es la primera salida de la base de los tramos; la llegada, la del último tramo.
    /// </summary>
    public static class FechasViajeGpsService
    {
        public static FechasViajeGps Obtener(IList<int> idsViajes)
        {
            if (idsViajes == null || idsViajes.Count == 0) return new FechasViajeGps();

            string enViajes = string.Join(",", idsViajes.Select((_, i) => "@v" + i));
            var parametros = idsViajes.Select((id, i) => DbHelper.Param("@v" + i, id)).ToArray();

            DataTable dt = DbHelper.ConsultarTabla($@"
                SELECT
                    s.tipoRuta,
                    CASE WHEN s.idDespachoDestino = d.idDespacho THEN 1 ELSE 0 END AS esDescarga,
                    CASE WHEN s.idDespachoDestino IS NULL THEN 0 ELSE 1 END AS hayDescargaAparte,
                    CASE WHEN s.tracto2 IS NULL OR LTRIM(RTRIM(s.tracto2)) = '' OR s.tracto2 = s.tracto1 THEN 1 ELSE 0 END AS mismoTracto,
                    s.lugarFin,
                    s.fhSalidaBase1, s.fhLlegadaTrujillo, s.fhLlegadaBase2, s.fhSalidaBase2, s.fhLlegadaBaseFinal
                FROM Despachos d
                INNER JOIN SeguimientoExportacion s
                        ON s.idDespachoOrigen = d.idDespacho OR s.idDespachoDestino = d.idDespacho
                WHERE d.idViajeProgreso IN ({enViajes}) AND d.activo = 1",
                parametros);

            var tramos = dt.Rows.Cast<DataRow>().Select(r => ArmarTramo(
                r["tipoRuta"] as string,
                Convert.ToInt32(r["esDescarga"]) == 1,
                Convert.ToInt32(r["hayDescargaAparte"]) == 1,
                Convert.ToInt32(r["mismoTracto"]) == 1,
                r["lugarFin"] as string,
                F(r["fhSalidaBase1"]), F(r["fhLlegadaTrujillo"]), F(r["fhLlegadaBase2"]),
                F(r["fhSalidaBase2"]), F(r["fhLlegadaBaseFinal"]))).ToList();

            FechasViajeGps resultado = Combinar(tramos);
            resultado.TieneSeguimiento = tramos.Count > 0;
            return resultado;
        }

        private static DateTime? F(object v) => v == null || v == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(v);

        /// <summary>Tramo que manejó el chofer de un despacho, según la ruta del seguimiento.</summary>
        public static TramoGps ArmarTramo(string tipoRuta, bool esDescarga, bool hayDescargaAparte, bool mismoTracto,
            string lugarFin, DateTime? salidaBase1, DateTime? llegadaCarga, DateTime? llegadaBase2,
            DateTime? salidaBase2, DateTime? llegadaFinal)
        {
            string ruta = (tipoRuta ?? "").Trim().ToUpperInvariant();
            bool finEnBase = string.IsNullOrWhiteSpace(lugarFin) || lugarFin.Trim().ToUpperInvariant() == "BASE";

            if (ruta == "")
            {
                // Vitapro
                if (esDescarga)
                    return new TramoGps { Salida = salidaBase2, Inicio = salidaBase2, Llegada = llegadaFinal, CruzaFrontera = true };
                bool sigueAEcuador = mismoTracto && !hayDescargaAparte && salidaBase2.HasValue;
                return new TramoGps
                {
                    Salida = salidaBase1,
                    Inicio = salidaBase1,
                    Llegada = sigueAEcuador ? llegadaFinal : llegadaBase2,
                    CruzaFrontera = sigueAEcuador
                };
            }

            // Terceros: empiezan en la planta de carga; la salida de la base no es parte del tramo.
            DateTime? inicio = salidaBase1 ?? llegadaCarga;
            if (ruta == "CRISAR" && !mismoTracto)
                return new TramoGps { Salida = salidaBase1, Inicio = inicio, Llegada = llegadaBase2, CruzaFrontera = false };

            bool cruza = ruta == "CRISAR" || ruta == "AFRECHO" || ruta == "NOVOPAN";
            return new TramoGps
            {
                Salida = salidaBase1,
                Inicio = inicio,
                Llegada = finEnBase ? llegadaFinal : null,
                CruzaFrontera = cruza
            };
        }

        /// <summary>
        /// Une los tramos del viaje: la salida de base más temprana y la llegada a base del último
        /// tramo (por su inicio). Sin inicio no se sabe dónde va el tramo y se ignora; una llegada
        /// anterior al inicio de su tramo se descarta.
        /// </summary>
        public static FechasViajeGps Combinar(IEnumerable<TramoGps> tramos)
        {
            var lista = (tramos ?? Enumerable.Empty<TramoGps>())
                .Where(t => t != null)
                .Select(t => { if (!t.Inicio.HasValue) t.Inicio = t.Salida; return t; })
                .Where(t => t.Inicio.HasValue)
                .OrderBy(t => t.Inicio.Value)
                .ToList();

            if (lista.Count == 0) return new FechasViajeGps();

            TramoGps ultimo = lista[lista.Count - 1];
            DateTime? llegada = ultimo.Llegada.HasValue && ultimo.Llegada.Value > ultimo.Inicio.Value ? ultimo.Llegada : null;

            // La salida de base solo vale si es del primer tramo: si el viaje empieza con un tercero
            // (que no sale de la base) y después hay uno de Vitapro, la salida real fue antes y no se sabe.
            DateTime? salida = lista[0].Salida;

            return new FechasViajeGps
            {
                Salida = salida,
                Llegada = llegada.HasValue && salida.HasValue && llegada <= salida ? null : llegada,
                CruzaFrontera = lista.Any(t => t.CruzaFrontera)
            };
        }
    }
}
