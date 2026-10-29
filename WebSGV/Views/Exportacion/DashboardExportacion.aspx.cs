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
                periodo = CargarPeriodo(anio, mes);
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

        /// <summary>Viajes de Vitapro del período y del anterior en una consulta, con cuántas de sus horas puso el GPS.</summary>
        private static ResumenPeriodo CargarPeriodo(int anio, int mes)
        {
            bool porMes = mes >= 1 && mes <= 12;
            DateTime desde = porMes ? new DateTime(anio, mes, 1) : new DateTime(anio, 1, 1);
            DateTime hasta = porMes ? desde.AddMonths(1) : desde.AddYears(1);
            DateTime desdeAnt = porMes ? desde.AddMonths(-1) : desde.AddYears(-1);

            string columnas = string.Join(", ", IndicadoresExportacion.ColumnasHito.Concat(new[] { "fhLlegadaAlmacen" })
                .Distinct().Select(c => "se." + c));
            string enLista = string.Join(", ", IndicadoresExportacion.ColumnasHito.Select(c => "'" + c + "'"));

            DataTable dt = DbHelper.ConsultarTabla($@"
                SELECT se.idSeguimiento, se.cliente, se.bodegaDescarga, se.bodegaEcuatoriana, se.carreta,
                       se.tracto1, se.conductorOrigen, se.tracto2, se.fhProgramacion,
                       se.sacosRobados, se.sacosRotos, se.sacosMojados, {columnas},
                       (SELECT COUNT(*) FROM SeguimientoHito h
                         WHERE h.idSeguimiento = se.idSeguimiento AND h.columna IN ({enLista})
                           AND h.fuente IN ('GPS', 'ONWAY', 'GRUPO') AND h.idUsuarioCorreccion IS NULL) AS hitosGps
                FROM SeguimientoExportacion se
                WHERE se.activo = 1 AND se.tipoRuta IS NULL
                  AND se.fhProgramacion >= @desdeAnt AND se.fhProgramacion < @hasta",
                DbHelper.Param("@desdeAnt", desdeAnt),
                DbHelper.Param("@hasta", hasta));

            var viajes = dt.Rows.Cast<DataRow>().Select(Viaje).ToList();
            return IndicadoresExportacion.Resumir(
                viajes.Where(v => v.FhProgramacion >= desde).ToList(),
                viajes.Where(v => v.FhProgramacion < desde).ToList(),
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

        private void MostrarError(string mensaje)
        {
            pnlAlert.CssClass = "de-alert de-alert-danger";
            litAlert.Text     = Server.HtmlEncode(mensaje);
            pnlAlert.Visible  = true;
        }
    }
}
