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
                LogSGV.Error("Descarga: la llave {Llave} no está registrada en la sesión (sesión nueva o enlace de otra sesión)", Request.QueryString["k"]);
                NoEncontrado("A");
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
                NoEncontrado("B");
                return;
            }

            if (!DocumentoHelper.EstaDentroDe(rutaFisica, DocumentoHelper.RaizFisicaUploads(Server)))
            {
                LogSGV.Error("Descarga: {Ruta} resuelve a {Fisica}, fuera de {Raiz}", rutaVirtual, rutaFisica, DocumentoHelper.RaizFisicaUploads(Server));
                NoEncontrado("C");
                return;
            }
            if (!File.Exists(rutaFisica))
            {
                LogSGV.Error("Descarga: no existe (o sin permiso de lectura) {Fisica} para {Ruta}", rutaFisica, rutaVirtual);
                NoEncontrado("D");
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

        /// <summary>El código (A sesión, B ruta, C fuera de Uploads, D archivo) ayuda a diagnosticar sin abrir el log.</summary>
        private void NoEncontrado(string codigo)
        {
            Response.Clear();
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            Response.ContentType = "text/plain";
            Response.Write("Documento no disponible. (" + codigo + ")");
            Response.End();
        }
    }
}
