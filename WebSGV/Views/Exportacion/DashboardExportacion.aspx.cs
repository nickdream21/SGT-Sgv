using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using WebSGV.Helpers;
using WebSGV.Services.Exportacion;

namespace WebSGV.Views.Exportacion
{
    /// <summary>
    /// Dashboard del Seguimiento de Exportación (viajes de Vitapro). El período se toma por F.H. Programación y los
    /// tiempos salen de los hitos que marca el GPS (<see cref="IndicadoresExportacion"/>): la cadena del viaje por tramos,
    /// el cumplimiento de la llegada a Trujillo y el tránsito al cliente, comparados con el período anterior. Arriba, la
    /// operación de ahora (<see cref="OperacionActual"/>), que no depende del período.
    /// </summary>
    public partial class DashboardExportacion : PaginaBase
    {
        /// <summary>Días hacia atrás para buscar los viajes abiertos de la operación de ahora (igual que el Reporte GPS).</summary>
        private const int DiasOperacion = 15;

        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("SEGUIMIENTO_EXPORTACION");

            if (!IsPostBack)
            {
                CargarAniosDisponibles();
                ddlMes.SelectedValue  = FechaHelper.Ahora().Month.ToString();
                ddlAnio.SelectedValue = FechaHelper.Ahora().Year.ToString();
                CargarDashboard();
            }
        }

        protected void btnFiltrar_Click(object sender, EventArgs e) { CargarDashboard(); }

        protected void btnMesActual_Click(object sender, EventArgs e)
        {
            ddlMes.SelectedValue  = FechaHelper.Ahora().Month.ToString();
            ddlAnio.SelectedValue = FechaHelper.Ahora().Year.ToString();
            CargarDashboard();
        }

        private void CargarAniosDisponibles()
        {
            ddlAnio.Items.Clear();
            int actual = FechaHelper.Ahora().Year;
            var anios = new HashSet<int> { actual };
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(@"
                    SELECT DISTINCT YEAR(fhProgramacion) AS anio
                    FROM SeguimientoExportacion
                    WHERE activo = 1 AND fhProgramacion IS NOT NULL");
                foreach (DataRow r in dt.Rows)
                    if (r["anio"] != DBNull.Value) anios.Add(Convert.ToInt32(r["anio"]));
            }
            catch (Exception ex)
            {
                LogSGV.Advertencia("Dashboard de exportación: no se leyeron los años: {Error}", ex.Message);
            }

            foreach (var a in anios.OrderByDescending(a => a))
                ddlAnio.Items.Add(new System.Web.UI.WebControls.ListItem(a.ToString(), a.ToString()));
        }

        private void CargarDashboard()
        {
            pnlAlert.Visible = false;
            int mes  = int.Parse(ddlMes.SelectedValue);
            int anio = int.Parse(ddlAnio.SelectedValue);

            object periodo = null, operacion = null;
            try
            {
                periodo = DashboardExportacionService.CargarPeriodo(anio, mes);
            }
            catch (Exception ex)
            {
                MostrarError("No se pudieron calcular los indicadores del período: " + MensajeErrorHelper.ParaUsuario(ex));
            }
            try
            {
                DateTime hoy = FechaHelper.Hoy();
                operacion = OperacionActual.Calcular(SeguimientoViajesService.ListarTablero(new FiltroTablero
                {
                    Desde = hoy.AddDays(-DiasOperacion), Hasta = hoy.AddDays(1), Cliente = "VITAPRO",
                }), FechaHelper.Ahora());
            }
            catch (Exception ex)
            {
                LogSGV.Advertencia("Dashboard de exportación: no se cargó la operación de ahora: {Error}", ex.Message);
            }

            hdnDatos.Value = JsonConvert.SerializeObject(new { periodo, operacion }, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                DateFormatString = "yyyy-MM-dd'T'HH:mm",
                Formatting = Formatting.None,
            });
        }

        private void MostrarError(string mensaje)
        {
            // Aviso común (alert alert-danger, role="alert", texto codificado): AvisoHelper.
            AvisoHelper.Pintar(litAlert, mensaje, AvisoHelper.Peligro);
            pnlAlert.Visible = true;
        }
    }
}
