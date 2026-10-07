using System;
using System.IO;
using System.Web;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    /// <summary>
    /// Entrega un archivo de ~/Uploads solo si la sesión actual tiene registrada la llave
    /// (ver <see cref="DocumentoHelper"/>). Uploads no se sirve por URL directa.
    /// </summary>
    public partial class DescargarDocumento : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirSesion();
            SecurityHelper.AgregarHeadersSeguridad();

            string rutaVirtual = DocumentoHelper.ObtenerRutaPermitida(Context, Request.QueryString["k"]);
            if (rutaVirtual == null)
            {
                NoEncontrado();
                return;
            }

            string rutaFisica;
            try
            {
                rutaFisica = Path.GetFullPath(Server.MapPath(rutaVirtual));
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Ruta de documento inválida: {Ruta}", rutaVirtual);
                NoEncontrado();
                return;
            }

            if (!DocumentoHelper.EstaDentroDe(rutaFisica, DocumentoHelper.RaizFisicaUploads(Server)) || !File.Exists(rutaFisica))
            {
                NoEncontrado();
                return;
            }

            string nombre = Path.GetFileName(rutaFisica);
            string tipo = MimeMapping.GetMimeMapping(nombre);
            bool verEnNavegador = tipo == "application/pdf" || tipo.StartsWith("image/", StringComparison.Ordinal);

            Response.Clear();
            Response.ContentType = tipo;
            Response.AddHeader("Content-Disposition",
                (verEnNavegador ? "inline" : "attachment") + "; filename=\"" + nombre.Replace("\"", "") + "\"");
            Response.TransmitFile(rutaFisica);
            Response.End();
        }

        private void NoEncontrado()
        {
            Response.Clear();
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            Response.ContentType = "text/plain";
            Response.Write("Documento no disponible.");
            Response.End();
        }
    }
}
