using System;
using System.Collections.Generic;
using System.Data;
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

        [WebMethod(EnableSession = true)]
        public static List<LiquidacionAprobadaItem> ObtenerLiquidacionesAprobadasContabilidad(int idConductor, string numeroOrden, string nombreConductor)
        {
            var contexto = System.Web.HttpContext.Current;
            if (contexto == null || contexto.Session == null || contexto.Session["UsuarioID"] == null)
                return new List<LiquidacionAprobadaItem>();

            // Misma regla que la página: sin esto cualquier rol logueado (p. ej. CONDUCTOR)
            // podía listar las liquidaciones de todos los conductores.
            if (!RolesHelper.TienePermiso("LIQUIDACIONES_CONTABILIDAD"))
                return new List<LiquidacionAprobadaItem>();

            if (idConductor < 0)
                return new List<LiquidacionAprobadaItem>();

            numeroOrden     = (numeroOrden ?? string.Empty).Trim();
            nombreConductor = (nombreConductor ?? string.Empty).Trim();

            if (numeroOrden.Length > 30)
                numeroOrden = numeroOrden.Substring(0, 30);
            if (nombreConductor.Length > 120)
                nombreConductor = nombreConductor.Substring(0, 120);

            if (!string.IsNullOrWhiteSpace(numeroOrden) && !Regex.IsMatch(numeroOrden, "^[A-Za-z0-9_/-]+$"))
                return new List<LiquidacionAprobadaItem>();

            var lista = new List<LiquidacionAprobadaItem>();

            DataTable dt = LiquidacionesContabilidadService.ObtenerAprobadas(idConductor, numeroOrden, nombreConductor);

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

            return lista;
        }

        /// <summary>
        /// Autocompletado del filtro "Conductor": solo conductores con liquidaciones aprobadas.
        /// Antes el JS llamaba a LiquidacionesPendientes.BuscarConductores, que espera otro parámetro
        /// (term), devuelve otro formato y exige admin/supervisor: a contabilidad nunca le sugería nada.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static List<ConductorItem> BuscarConductores(string termino)
        {
            var vacia = new List<ConductorItem>();
            var contexto = System.Web.HttpContext.Current;
            if (contexto == null || contexto.Session == null || contexto.Session["UsuarioID"] == null)
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
