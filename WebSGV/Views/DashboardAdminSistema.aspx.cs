using System;
using System.Data;
using System.Globalization;
using WebSGV.Helpers;
using WebSGV.Services.Auditoria;
using WebSGV.Services.Usuarios;

namespace WebSGV.Views
{
    public partial class DashboardAdminSistema : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdminSistema();
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                lblFechaHora.Text = FechaHelper.Ahora().ToString("dddd, dd 'de' MMMM 'de' yyyy · HH:mm",
                    new CultureInfo("es-PE"));
                CargarEstadisticas();
                CargarRoles();
                CargarAuditoria();
            }
        }

        private void CargarEstadisticas()
        {
            var (total, activos, inactivos) = GestionUsuariosService.ContarPorEstado();

            int? eventosHoy;
            try
            {
                eventosHoy = ConsultaAuditoriaService.ContarRegistrosDeHoy();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "DashboardAdminSistema: error contando los eventos de auditoría de hoy");
                eventosHoy = null;
            }

            litResumen.Text = HtmlResumen(total, activos, inactivos, eventosHoy);
        }

        /// <summary>
        /// Cifras del panel en una línea (sgv-resumen, E5-17; antes cuatro tarjetas): "95 usuarios: 89 activos y
        /// 6 inactivos · 12 eventos de auditoría hoy". Sin el conteo de auditoría (falló la consulta) va solo la
        /// primera parte. Solo números y textos fijos.
        /// </summary>
        internal static string HtmlResumen(int total, int activos, int inactivos, int? eventosHoy)
        {
            var es = CultureInfo.GetCultureInfo("es-PE");
            string N(int n) => "<strong>" + n.ToString("N0", es) + "</strong>";
            string html = "<p class=\"sgv-resumen\"><span>" + N(total) + (total == 1 ? " usuario: " : " usuarios: ") +
                          N(activos) + (activos == 1 ? " activo y " : " activos y ") +
                          N(inactivos) + (inactivos == 1 ? " inactivo" : " inactivos") + "</span>";
            if (eventosHoy.HasValue)
                html += "<span>" + N(eventosHoy.Value) + (eventosHoy.Value == 1 ? " evento" : " eventos") + " de auditoría hoy</span>";
            return html + "</p>";
        }

        /// <summary>Sello de la acción, igual que en Auditoria.aspx (nombre en español y tono); el texto sale codificado.</summary>
        protected static string SelloAccion(object accion) =>
            EstadoUiHelper.Sello(ConsultaAuditoriaService.NombreAccion(accion), EstadoUiHelper.TonoAccionAuditoria(accion));

        private void CargarRoles()
        {
            DataTable dt = GestionUsuariosService.ContarPorRol();

            if (dt.Rows.Count > 0)
            {
                rptRoles.DataSource = dt;
                rptRoles.DataBind();
            }
            else
            {
                pnlSinRoles.Visible = true;
            }
        }

        private void CargarAuditoria()
        {
            try
            {
                DataTable dt = ConsultaAuditoriaService.ObtenerUltimosEventos();

                if (dt.Rows.Count > 0)
                {
                    rptAuditoria.DataSource = dt;
                    rptAuditoria.DataBind();
                }
                else
                {
                    pnlSinAuditoria.Visible = true;
                }
            }
            catch (Exception ex)
            {
                // Se muestra "sin auditoría", pero la falla queda en el log (E6-13).
                LogSGV.Advertencia(ex, "DashboardAdminSistema: no se pudieron leer los últimos eventos de auditoría");
                pnlSinAuditoria.Visible = true;
            }
        }
    }
}
