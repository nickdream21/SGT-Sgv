using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.GpsIntegracion;

namespace WebSGV.Views
{
    /// <summary>
    /// Reporte GPS por placas: descarga del API de Onway el historial de varias unidades a la
    /// vez (el portal solo deja 3 por consulta) y entrega un Excel por placa, o todos en un ZIP.
    /// </summary>
    public partial class ReporteGps : PaginaBase
    {
        private const string SesionResultados = "ReporteGpsResultados";
        private const string FormatoFechaHora = "yyyy-MM-ddTHH:mm";

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdminOSupervisor();
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                PonerHoy();
                Session.Remove(SesionResultados);
            }
        }

        private void PonerHoy()
        {
            DateTime ahora = FechaHelper.Ahora();
            txtDesde.Text = ahora.Date.ToString(FormatoFechaHora);
            txtHasta.Text = ahora.ToString(FormatoFechaHora);
        }

        protected void btnHoy_Click(object sender, EventArgs e) => PonerHoy();

        protected void btnTraerPlacas_Click(object sender, EventArgs e)
        {
            var pedidos = ReporteHistorialGpsService.LeerPedidos(txtPedidos.Text);
            if (pedidos.Count == 0)
            {
                MostrarMensaje("Escriba uno o más números de pedido.");
                return;
            }

            try
            {
                var encontradas = ReporteHistorialGpsService.PlacasDePedidos(pedidos);
                var placas = ReporteHistorialGpsService.LeerPlacas(txtPlacas.Text);
                foreach (var x in encontradas)
                    if (!placas.Contains(x.Placa)) placas.Add(x.Placa);
                txtPlacas.Text = string.Join(", ", placas);

                litPlacasPedido.Text = ResumenPedidos(pedidos, encontradas);
                pnlMensaje.Visible = false;
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se pudieron traer los tractos: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        private static string ResumenPedidos(IList<string> pedidos, IList<ReporteHistorialGpsService.PlacaDePedido> encontradas)
        {
            var sb = new StringBuilder("<ul class=\"small mb-0 mt-2 pl-3\">");
            foreach (string pedido in pedidos)
            {
                var delPedido = encontradas.Where(x => x.Pedido == pedido).ToList();
                sb.Append("<li><strong>").Append(HttpUtility.HtmlEncode(pedido)).Append("</strong>: ");
                if (delPedido.Count == 0)
                    sb.Append("<span class=\"text-danger\">sin despachos registrados en el sistema</span>");
                else
                    sb.Append(string.Join(" · ", delPedido.GroupBy(x => x.Operacion).Select(g =>
                        HttpUtility.HtmlEncode(g.Key.ToLowerInvariant()) + " " + HttpUtility.HtmlEncode(string.Join(", ", g.Select(x => x.Placa))))));
                sb.Append("</li>");
            }
            return sb.Append("</ul>").ToString();
        }

        protected void btnGenerar_Click(object sender, EventArgs e)
        {
            if (!DateTime.TryParseExact(txtDesde.Text, FormatoFechaHora, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime desde) ||
                !DateTime.TryParseExact(txtHasta.Text, FormatoFechaHora, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime hasta))
            {
                MostrarMensaje("Revise las fechas Desde y Hasta.");
                return;
            }

            DateTime ahora = FechaHelper.Ahora();
            if (hasta > ahora) hasta = ahora;

            // Varias placas por varios días puede pasar del límite normal de 110 s de una petición.
            Server.ScriptTimeout = 600;

            try
            {
                var placas = ReporteHistorialGpsService.LeerPlacas(txtPlacas.Text);
                var resultados = ReporteHistorialGpsService.Generar(placas, desde, hasta);
                Session[SesionResultados] = resultados;

                gvResultados.DataSource = resultados;
                gvResultados.DataBind();
                int ok = resultados.Count(r => r.Contenido != null);
                litTituloResultados.Text = $"{ok} de {resultados.Count} reporte(s) listo(s) · {desde:dd/MM HH:mm} a {hasta:dd/MM HH:mm}";
                btnZip.Visible = ok > 0;

                var pedidos = ReporteHistorialGpsService.LeerPedidos(txtPedidos.Text);
                txtAsunto.Text = ReporteHistorialGpsService.AsuntoCorreo(pedidos);
                txtCuerpo.Text = ReporteHistorialGpsService.CuerpoCorreo(ahora);
                pnlResultados.Visible = true;
                pnlMensaje.Visible = false;

                AuditoriaHelper.Registrar("REPORTE_GPS", "Onway", null,
                    $"Historial GPS de {resultados.Count} placa(s) ({ok} con reporte), {desde:dd/MM/yyyy HH:mm} a {hasta:dd/MM/yyyy HH:mm}: {string.Join(", ", placas)}");
            }
            catch (Exception ex)
            {
                pnlResultados.Visible = false;
                MostrarMensaje("No se generó el reporte: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        private List<ReporteHistorialGpsService.ResultadoPlaca> Resultados() =>
            Session[SesionResultados] as List<ReporteHistorialGpsService.ResultadoPlaca>;

        protected void btnZip_Click(object sender, EventArgs e)
        {
            var resultados = Resultados();
            if (resultados == null)
            {
                MostrarMensaje("Los reportes expiraron. Vuelva a pulsar Generar reportes.");
                pnlResultados.Visible = false;
                return;
            }
            EnviarArchivo(ReporteHistorialGpsService.CrearZip(resultados), "application/zip",
                $"ReporteGPS_{FechaHelper.Ahora():yyyyMMdd_HHmm}.zip");
        }

        protected void gvResultados_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "Descargar") return;
            var resultados = Resultados();
            int i = Convert.ToInt32(e.CommandArgument);
            if (resultados == null || i < 0 || i >= resultados.Count || resultados[i].Contenido == null)
            {
                MostrarMensaje("Los reportes expiraron. Vuelva a pulsar Generar reportes.");
                pnlResultados.Visible = false;
                return;
            }
            EnviarArchivo(resultados[i].Contenido,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", resultados[i].NombreArchivo);
        }

        /// <summary>Hora, dirección (con enlace al mapa) y velocidad del último punto.</summary>
        protected string UltimaPosicion(object item)
        {
            var r = (ReporteHistorialGpsService.ResultadoPlaca)item;
            if (r.Ultimo == null)
                return r.Encontrada && r.Error == null ? "<span class=\"text-muted\">Sin datos en el rango</span>" : "";

            string url = ReporteHistorialGpsService.UrlMapa(r.Ultimo.Lat, r.Ultimo.Lng);
            return $"<span class=\"text-muted\">{r.UltimoLocal:dd/MM HH:mm}</span> · " +
                   $"<a href=\"{HttpUtility.HtmlAttributeEncode(url)}\" target=\"_blank\" rel=\"noopener\">{HttpUtility.HtmlEncode(r.Ultimo.Address ?? "ver mapa")}</a> · " +
                   $"{r.Ultimo.Speed:0} km/h";
        }

        private static void EnviarArchivo(byte[] contenido, string tipo, string nombreArchivo)
        {
            var resp = HttpContext.Current.Response;
            resp.Clear();
            resp.ClearContent();
            resp.ClearHeaders();
            resp.Buffer = true;
            resp.Charset = "";
            resp.ContentType = tipo;
            resp.AddHeader("Content-Disposition", "attachment; filename=" + nombreArchivo);
            resp.AddHeader("Content-Length", contenido.Length.ToString());
            resp.Cache.SetCacheability(HttpCacheability.NoCache);
            resp.BinaryWrite(contenido);
            resp.Flush();
            resp.End();
        }

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            pnlMensaje.Visible = true;
            string css = esExito ? "alert alert-success" : "alert alert-danger";
            lblMensaje.Text = $"<div class='{css}'>{HttpUtility.HtmlEncode(mensaje)}</div>";
        }
    }
}
