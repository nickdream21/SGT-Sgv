using System;
using System.Linq;
using System.Web.UI;
using WebSGV.Helpers;
using WebSGV.Services.Despachos;

namespace WebSGV.Views
{
    public partial class Inicio : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdminOSupervisor();

            if (!IsPostBack)
                MostrarDocumentosPendientes();
        }

        /// <summary>
        /// Aviso de los lotes activos que esperan factura, guía o CPIC. Si la consulta falla
        /// solo se omite el aviso: la página de inicio no debe caerse por esto.
        /// </summary>
        private void MostrarDocumentosPendientes()
        {
            try
            {
                DateTime hoy = FechaHelper.Hoy();
                var lotes = ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, "ACTIVO",
                    documentos: ListaDespachosService.DOCUMENTOS_PENDIENTES);
                if (lotes.Count == 0) return;

                int atrasados = lotes.Count(l => EstadoDocumental.DeLote(l).Atrasado(l.FechaProgramacion, hoy));
                lblDocumentosPendientes.Text = (lotes.Count == 1 ? "1 lote con documentos pendientes" : $"{lotes.Count} lotes con documentos pendientes")
                    + (atrasados > 0 ? $" ({atrasados} con más de 3 días)" : "") + " — ver y completar";
                lnkDocumentosPendientes.Visible = true;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Inicio: no se pudo contar los documentos pendientes");
            }
        }
    }
}
