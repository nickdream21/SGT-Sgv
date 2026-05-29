using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Web.Services;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class LiquidacionesAprobadasContabilidad : System.Web.UI.Page
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

            if (idConductor < 0)
                return new List<LiquidacionAprobadaItem>();

            numeroOrden = (numeroOrden ?? string.Empty).Trim();
            nombreConductor = (nombreConductor ?? string.Empty).Trim();

            if (numeroOrden.Length > 30)
                numeroOrden = numeroOrden.Substring(0, 30);
            if (nombreConductor.Length > 120)
                nombreConductor = nombreConductor.Substring(0, 120);

            if (!string.IsNullOrWhiteSpace(numeroOrden) && !Regex.IsMatch(numeroOrden, "^[A-Za-z0-9_/-]+$"))
                return new List<LiquidacionAprobadaItem>();

            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            var lista = new List<LiquidacionAprobadaItem>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT
                    ov.idOrdenViaje,
                    ov.numeroOrdenViaje,
                    c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS NombreConductor,
                    ov.fechaSalida,
                    ov.fechaLlegada,
                    ISNULL(dr.descuentoSoles, 0) AS DescuentoSoles,
                    ISNULL(dr.descuentoDolares, 0) AS DescuentoDolares,
                    ISNULL(dr.reintegroSoles, 0) AS ReintegroSoles,
                    ISNULL(dr.reintegroDolares, 0) AS ReintegroDolares,
                    ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) AS IngresosSoles,
                    ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0) AS IngresosDolares,
                    ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + ISNULL(e.movilidadSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) + ISNULL(e.hospedajeSoles, 0) + ISNULL(e.combustibleSoles, 0) AS GastosSoles,
                    ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + ISNULL(e.movilidadDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) + ISNULL(e.hospedajeDolares, 0) + ISNULL(e.combustibleDolares, 0) AS GastosDolares,
                    ISNULL((SELECT SUM(soles) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IngresosAdSoles,
                    ISNULL((SELECT SUM(dolares) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IngresosAdDolares,
                    ISNULL((SELECT SUM(soles) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GastosAdSoles,
                    ISNULL((SELECT SUM(dolares) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GastosAdDolares
                FROM OrdenViaje ov
                INNER JOIN Conductor c ON ov.idConductor = c.idConductor
                LEFT JOIN DescuentosReintegros dr ON dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1
                LEFT JOIN Ingresos i ON i.numeroOrdenViaje = ov.numeroOrdenViaje
                LEFT JOIN Egresos e ON e.numeroOrdenViaje = ov.numeroOrdenViaje
                WHERE ov.estadoViaje = 'COMPLETADO'
                  AND (@IdConductor <= 0 OR ov.idConductor = @IdConductor)
                  AND (@NumeroOrden = '' OR ov.numeroOrdenViaje LIKE @NumeroOrdenLike)
                  AND (@NombreConductor = '' OR (c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '')) LIKE @NombreConductorLike)
                ORDER BY ov.fechaSalida DESC", conn))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@IdConductor", SqlDbType.Int).Value = idConductor;
                cmd.Parameters.Add("@NumeroOrden", SqlDbType.NVarChar, 30).Value = numeroOrden;
                cmd.Parameters.Add("@NumeroOrdenLike", SqlDbType.NVarChar, 64).Value = "%" + numeroOrden + "%";
                cmd.Parameters.Add("@NombreConductor", SqlDbType.NVarChar, 120).Value = nombreConductor;
                cmd.Parameters.Add("@NombreConductorLike", SqlDbType.NVarChar, 140).Value = "%" + nombreConductor + "%";

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        decimal ingresosSoles = Convert.ToDecimal(reader["IngresosSoles"]) + Convert.ToDecimal(reader["IngresosAdSoles"]);
                        decimal ingresosDolares = Convert.ToDecimal(reader["IngresosDolares"]) + Convert.ToDecimal(reader["IngresosAdDolares"]);
                        decimal gastosSoles = Convert.ToDecimal(reader["GastosSoles"]) + Convert.ToDecimal(reader["GastosAdSoles"]);
                        decimal gastosDolares = Convert.ToDecimal(reader["GastosDolares"]) + Convert.ToDecimal(reader["GastosAdDolares"]);
                        decimal descuentoSoles = Convert.ToDecimal(reader["DescuentoSoles"]);
                        decimal descuentoDolares = Convert.ToDecimal(reader["DescuentoDolares"]);
                        decimal reintegroSoles = Convert.ToDecimal(reader["ReintegroSoles"]);
                        decimal reintegroDolares = Convert.ToDecimal(reader["ReintegroDolares"]);

                        lista.Add(new LiquidacionAprobadaItem
                        {
                            IdOrdenViaje = Convert.ToInt32(reader["idOrdenViaje"]),
                            NumeroOrdenViaje = reader["numeroOrdenViaje"].ToString(),
                            NombreConductor = reader["NombreConductor"].ToString(),
                            FechaSalida = Convert.ToDateTime(reader["fechaSalida"]).ToString("dd/MM/yyyy"),
                            FechaLlegada = Convert.ToDateTime(reader["fechaLlegada"]).ToString("dd/MM/yyyy"),
                            BalanceSoles = (ingresosSoles - gastosSoles) - descuentoSoles + reintegroSoles,
                            BalanceDolares = (ingresosDolares - gastosDolares) - descuentoDolares + reintegroDolares
                        });
                    }
                }
            }

            return lista;
        }
    }
}
