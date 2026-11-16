using System;
using System.Data;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    /// <summary>
    /// Vista de firma digital del conductor. Se accede con ?id=[idOrdenViaje].
    /// Valida que la sesión sea de un CONDUCTOR y que la orden pertenezca al
    /// conductor autenticado antes de mostrar el canvas de firma (anti-IDOR).
    /// </summary>
    public partial class FirmarLiquidacion : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // Guardia fuera de cualquier try: termina la respuesta si no pasa.
            SecurityHelper.ExigirRolConductor();

            if (IsPostBack) return;

            SecurityHelper.AgregarHeadersSeguridad();

            int idOrdenViaje;
            if (!int.TryParse(Request.QueryString["id"], out idOrdenViaje) || idOrdenViaje <= 0)
            {
                Response.Redirect(ResolveUrl("~/Views/DashboardConductor.aspx"), true);
                return;
            }

            int idConductorActual = 0;
            if (Session["IdConductor"] != null)
                int.TryParse(Session["IdConductor"].ToString(), out idConductorActual);

            if (idConductorActual == 0 || !OrdenPendienteDeFirma(idOrdenViaje, idConductorActual))
            {
                // Ajena, ya firmada o fuera de estado: vuelve a su panel.
                Response.Redirect(ResolveUrl("~/Views/DashboardConductor.aspx"), true);
                return;
            }

            hfIdOrdenViaje.Value = idOrdenViaje.ToString();
        }

        /// <summary>
        /// Devuelve true si la orden pertenece al conductor y espera la firma de su declaración:
        /// PENDIENTE y sin firma vigente (la misma condición que sp_DC_RegistrarFirmaConductor).
        /// </summary>
        private bool OrdenPendienteDeFirma(int idOrdenViaje, int idConductor)
        {
            try
            {
                int count = Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                    SELECT COUNT(1)
                    FROM   OrdenViaje
                    WHERE  idOrdenViaje     = @idOrdenViaje
                      AND  idConductor      = @idConductor
                      AND  estadoAprobacion = 'PENDIENTE'
                      AND  idFirmaConductor IS NULL
                      AND  idFirmaAdmin     IS NULL",
                    DbHelper.Param("@idOrdenViaje", idOrdenViaje),
                    DbHelper.Param("@idConductor",  idConductor)));
                return count > 0;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al validar la orden {IdOrden} para la firma del conductor", idOrdenViaje);
                return false;
            }
        }
    }
}
