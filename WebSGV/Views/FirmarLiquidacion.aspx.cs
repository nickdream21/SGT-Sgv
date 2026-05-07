using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    /// <summary>
    /// Vista de firma digital del conductor. Se accede con ?id=[idOrdenViaje].
    /// Valida que la sesión sea de un CONDUCTOR y que la orden pertenezca al
    /// conductor autenticado antes de mostrar el canvas de firma (anti-IDOR).
    /// </summary>
    public partial class FirmarLiquidacion : Page
    {
        private string ConnectionString =>
            ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;

            // Cabeceras de seguridad HTTP (anti-clickjacking, nosniff, no-cache)
            SecurityHelper.AgregarHeadersSeguridad();

            // 1) Requiere sesión de conductor autenticado.
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

            // 2) Leer y validar el id de la orden a firmar.
            int idOrdenViaje;
            if (!int.TryParse(Request.QueryString["id"], out idOrdenViaje) || idOrdenViaje <= 0)
            {
                Response.Redirect(ResolveUrl("~/Views/DashboardConductor.aspx"), true);
                return;
            }

            // 3) Anti-IDOR: verificar que la orden pertenece al conductor en sesión.
            int idConductorActual = 0;
            if (session["IdConductor"] != null)
                int.TryParse(session["IdConductor"].ToString(), out idConductorActual);

            if (idConductorActual == 0 || !OrdenPerteneceAlConductor(idOrdenViaje, idConductorActual))
            {
                // Registrar intento de acceso no autorizado sin revelar detalles al cliente.
                System.Diagnostics.Debug.WriteLine(
                    $"⚠️ Intento de acceso IDOR bloqueado: " +
                    $"idOrdenViaje={idOrdenViaje}, idConductor={idConductorActual}");
                Response.Redirect(ResolveUrl("~/Views/DashboardConductor.aspx"), true);
                return;
            }

            hfIdOrdenViaje.Value = idOrdenViaje.ToString();
        }

        /// <summary>
        /// Devuelve true si la orden de viaje indicada pertenece al conductor
        /// y está en un estado que permite firmar (PENDIENTE).
        /// </summary>
        private bool OrdenPerteneceAlConductor(int idOrdenViaje, int idConductor)
        {
            try
            {
                const string sql = @"
                    SELECT COUNT(1)
                    FROM   OrdenViaje
                    WHERE  idOrdenViaje   = @idOrdenViaje
                      AND  idConductor    = @idConductor
                      AND  estadoAprobacion IN ('PENDIENTE', 'REABIERTO')";

                using (var conn = new SqlConnection(ConnectionString))
                using (var cmd  = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@idOrdenViaje", SqlDbType.Int).Value = idOrdenViaje;
                    cmd.Parameters.Add("@idConductor",  SqlDbType.Int).Value = idConductor;
                    conn.Open();
                    return (int)cmd.ExecuteScalar() > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"❌ Error en OrdenPerteneceAlConductor: {ex.Message}");
                return false; // fail-closed: ante duda, denegar acceso
            }
        }
    }
}
