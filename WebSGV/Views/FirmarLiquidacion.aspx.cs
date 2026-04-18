using System;
using System.Web.UI;

namespace WebSGV.Views
{
    /// <summary>
    /// Vista de firma digital del conductor. Se accede con ?id=[idOrdenViaje]
    /// (y en Fase 3.B, adicionalmente con ?token=[guid] para acceso móvil sin
    /// sesión). Renderiza un canvas responsive donde el conductor dibuja su
    /// firma y la envía al WebMethod LiquidacionesPendientes.RegistrarFirmaConductor.
    /// </summary>
    public partial class FirmarLiquidacion : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;

            // 1) Requiere sesión de conductor autenticado (Fase 3.A).
            //    Fase 3.B añadirá soporte para ?token=<guid> sin sesión.
            var session = Session;
            int idUsuario = 0;
            if (session["IdUsuario"] != null)
                int.TryParse(session["IdUsuario"].ToString(), out idUsuario);

            string rol = (session["Rol"] as string ?? "").ToUpperInvariant();

            if (idUsuario == 0 || rol != "CONDUCTOR")
            {
                Response.Redirect(ResolveUrl("~/Views/Login.aspx?returnUrl=" +
                    Server.UrlEncode(Request.RawUrl)), true);
                return;
            }

            // 2) Leer id de la orden a firmar
            int idOrdenViaje;
            if (!int.TryParse(Request.QueryString["id"], out idOrdenViaje) || idOrdenViaje <= 0)
            {
                Response.Redirect(ResolveUrl("~/Views/DashboardConductor.aspx"), true);
                return;
            }

            hfIdOrdenViaje.Value = idOrdenViaje.ToString();
        }
    }
}
