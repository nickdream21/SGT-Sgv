using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web.Services;
using WebSGV.Helpers;
using WebSGV.Services.Liquidaciones;

namespace WebSGV.Views
{
    public partial class LiquidacionesAprobadasContabilidad : PaginaBase
    {
        public class LiquidacionAprobadaItem
        {
            public int IdOrdenViaje { get; set; }
            public string NumeroOrdenViaje { get; set; }
            public string NombreConductor { get; set; }
            public string FechaSalida { get; set; }
            public string FechaLlegada { get; set; }
            public decimal BalanceSoles { get; set; }
            public decimal BalanceDolares { get; set; }
        }

        public class ConductorItem
        {
            public int IdConductor { get; set; }
            public string NombreCompleto { get; set; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("LIQUIDACIONES_CONTABILIDAD");
            SecurityHelper.AgregarHeadersSeguridad();
        }

        /// <summary>Respuesta de la lista: una página, el total con los filtros y, si algo falló, el mensaje.</summary>
        public class ResultadoLiquidacionesAprobadas
        {
            public List<LiquidacionAprobadaItem> Items { get; set; } = new List<LiquidacionAprobadaItem>();
            public int Total { get; set; }
            /// <summary>Página devuelta, desde 0.</summary>
            public int Pagina { get; set; }
            public int TamanoPagina { get; set; } = LiquidacionesContabilidadService.TamanoPagina;
            /// <summary>Null si todo fue bien; si no, el texto para el usuario.</summary>
            public string Mensaje { get; set; }
        }

        /// <summary>
        /// Una página (50) de las liquidaciones aprobadas. <paramref name="fechaDesde"/> y
        /// <paramref name="fechaHasta"/> filtran por fecha de salida (aaaa-mm-dd; vacías = sin límite: "Todo
        /// el historial"). La pantalla abre con los últimos 90 días (U-095), que pone el script.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static ResultadoLiquidacionesAprobadas ObtenerLiquidacionesAprobadasContabilidad(int idConductor, string numeroOrden, string nombreConductor,
            string fechaDesde, string fechaHasta, int pagina)
        {
            if (!SesionHelper.HaySesionActual)
                return new ResultadoLiquidacionesAprobadas();

            // Misma regla que la página: sin esto cualquier rol logueado (p. ej. CONDUCTOR)
            // podía listar las liquidaciones de todos los conductores.
            if (!RolesHelper.TienePermiso("LIQUIDACIONES_CONTABILIDAD"))
                return new ResultadoLiquidacionesAprobadas();

            if (idConductor < 0)
                return new ResultadoLiquidacionesAprobadas();

            numeroOrden     = (numeroOrden ?? string.Empty).Trim();
            nombreConductor = (nombreConductor ?? string.Empty).Trim();

            if (numeroOrden.Length > 30)
                numeroOrden = numeroOrden.Substring(0, 30);
            if (nombreConductor.Length > 120)
                nombreConductor = nombreConductor.Substring(0, 120);

            if (!string.IsNullOrWhiteSpace(numeroOrden) && !Regex.IsMatch(numeroOrden, "^[A-Za-z0-9_/-]+$"))
                return new ResultadoLiquidacionesAprobadas();

            if (!LeerFecha(fechaDesde, out DateTime? desde) || !LeerFecha(fechaHasta, out DateTime? hasta))
                return new ResultadoLiquidacionesAprobadas { Mensaje = "La fecha no es válida." };
            if (desde.HasValue && hasta.HasValue && desde.Value > hasta.Value)
                return new ResultadoLiquidacionesAprobadas { Mensaje = "La fecha \"desde\" es posterior a la fecha \"hasta\"." };

            PaginaAprobadas resultado;
            try
            {
                resultado = LiquidacionesContabilidadService.ObtenerPagina(idConductor, numeroOrden, nombreConductor, desde, hasta, pagina);
            }
            catch (Exception ex)
            {
                return new ResultadoLiquidacionesAprobadas { Mensaje = MensajeErrorHelper.ParaUsuario(ex) };
            }

            var lista = new List<LiquidacionAprobadaItem>();
            DataTable dt = resultado.Filas;

            foreach (DataRow reader in dt.Rows)
            {
                decimal ingresosSoles   = Convert.ToDecimal(reader["IngresosSoles"])   + Convert.ToDecimal(reader["IngresosAdSoles"]);
                decimal ingresosDolares = Convert.ToDecimal(reader["IngresosDolares"]) + Convert.ToDecimal(reader["IngresosAdDolares"]);
                decimal gastosSoles     = Convert.ToDecimal(reader["GastosSoles"])     + Convert.ToDecimal(reader["GastosAdSoles"]);
                decimal gastosDolares   = Convert.ToDecimal(reader["GastosDolares"])   + Convert.ToDecimal(reader["GastosAdDolares"]);
                decimal descuentoSoles  = Convert.ToDecimal(reader["DescuentoSoles"]);
                decimal descuentoDolares = Convert.ToDecimal(reader["DescuentoDolares"]);
                decimal reintegroSoles  = Convert.ToDecimal(reader["ReintegroSoles"]);
                decimal reintegroDolares = Convert.ToDecimal(reader["ReintegroDolares"]);

                lista.Add(new LiquidacionAprobadaItem
                {
                    IdOrdenViaje      = Convert.ToInt32(reader["idOrdenViaje"]),
                    NumeroOrdenViaje  = reader["numeroOrdenViaje"].ToString(),
                    NombreConductor   = reader["NombreConductor"].ToString(),
                    FechaSalida       = Convert.ToDateTime(reader["fechaSalida"]).ToString("dd/MM/yyyy"),
                    FechaLlegada      = Convert.ToDateTime(reader["fechaLlegada"]).ToString("dd/MM/yyyy"),
                    BalanceSoles      = WebSGV.Services.Liquidaciones.LiquidacionCalculos.SaldoFinal(ingresosSoles, gastosSoles, descuentoSoles, reintegroSoles),
                    BalanceDolares    = WebSGV.Services.Liquidaciones.LiquidacionCalculos.SaldoFinal(ingresosDolares, gastosDolares, descuentoDolares, reintegroDolares)
                });
            }

            return new ResultadoLiquidacionesAprobadas { Items = lista, Total = resultado.Total, Pagina = resultado.IndicePagina };
        }

        /// <summary>Fecha aaaa-mm-dd del input type="date"; vacía = null (sin límite). False si no se entiende.</summary>
        internal static bool LeerFecha(string texto, out DateTime? fecha)
        {
            fecha = null;
            texto = (texto ?? string.Empty).Trim();
            if (texto.Length == 0)
                return true;
            if (!DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime f)
                || f.Year < 2000 || f.Year > 2100)
                return false;
            fecha = f;
            return true;
        }

        /// <summary>
        /// Autocompletado del filtro "Conductor": solo conductores con liquidaciones aprobadas.
        /// Antes el JS llamaba a LiquidacionesPendientes.BuscarConductores, que espera otro parámetro
        /// (term), devuelve otro formato y exige administración: a contabilidad nunca le sugería nada.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static List<ConductorItem> BuscarConductores(string termino)
        {
            var vacia = new List<ConductorItem>();
            if (!SesionHelper.HaySesionActual)
                return vacia;
            if (!RolesHelper.TienePermiso("LIQUIDACIONES_CONTABILIDAD"))
                return vacia;

            termino = (termino ?? string.Empty).Trim();
            if (termino.Length < 2)
                return vacia;
            if (termino.Length > 120)
                termino = termino.Substring(0, 120);

            try
            {
                foreach (DataRow fila in LiquidacionesContabilidadService.BuscarConductoresConAprobadas(termino).Rows)
                {
                    vacia.Add(new ConductorItem
                    {
                        IdConductor    = Convert.ToInt32(fila["idConductor"]),
                        NombreCompleto = Convert.ToString(fila["nombreCompleto"]).Trim()
                    });
                }
                return vacia;
            }
            catch (Exception ex)
            {
                // Autocompletado: sin sugerencias no se bloquea la búsqueda; el error queda en el log.
                LogSGV.Error(ex, "Error en BuscarConductores de LiquidacionesAprobadasContabilidad");
                return new List<ConductorItem>();
            }
        }
    }
}
