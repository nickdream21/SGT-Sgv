using System;
using System.Web.UI;
using WebSGV.Helpers;
using WebSGV.Services.Despachos;

namespace WebSGV.Views
{
    public partial class Inicio : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdmin();
            SecurityHelper.AgregarHeadersSeguridad();

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
                // Contados en la BD (E6-29, sp_LD_ContarLotesActivos): antes se traían todos los lotes pendientes.
                ContadoresLotes c = ListaDespachosService.ContarLotesActivos(FechaHelper.Hoy());
                int pendientes = c.ConDocumentosPendientes, atrasados = c.Atrasados;
                if (pendientes == 0) return;

                lblDocumentosPendientes.Text = (pendientes == 1 ? "1 lote con documentos pendientes" : $"{pendientes} lotes con documentos pendientes")
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
