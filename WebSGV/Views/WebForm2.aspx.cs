using System;
using System.Web.UI;

namespace WebSGV.Views
{
    public partial class WebForm2 : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Código de inicialización si es necesario
            }
        }

        protected void btnGuardar_Click(object sender, EventArgs e)
        {
            // Aquí va el código para manejar el evento del botón
            string pedido = txtPedido.Text;
            string conductorOrigen = txtConductorOrigen.Text;
            string tracto1 = txtTracto1.Text;
            string carreta = txtCarreta.Text;
            string conductorDestino = txtConductorDestino.Text;
            string tracto2 = txtTracto2.Text;
            string fhsBase = txtFHSBase.Text;
            string fhllTrujillo = txtFHLLTrujillo.Text;

            // Mensaje de confirmación (opcional)
            Response.Write("<script>alert('Datos guardados correctamente');</script>");
        }
    }
}
