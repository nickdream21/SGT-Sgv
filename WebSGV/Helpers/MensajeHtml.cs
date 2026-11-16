using System.Web;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Texto de un mensaje para el usuario (alertas de MostrarMensaje) convertido a HTML seguro:
    /// se codifica todo (un nombre o número escrito por el usuario no puede inyectar marcado) y los
    /// saltos de línea pasan a &lt;br/&gt;. Los llamadores mandan texto plano; para negritas o
    /// listas, usar "\n" en lugar de etiquetas.
    /// </summary>
    public static class MensajeHtml
    {
        public static string Texto(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return string.Empty;
            return HttpUtility.HtmlEncode(texto).Replace("\r\n", "\n").Replace("\n", "<br/>");
        }
    }
}
