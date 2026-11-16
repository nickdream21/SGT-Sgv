using Newtonsoft.Json;

namespace WebSGV.Helpers
{
    /// <summary>
    /// JSON para incrustar dentro de un &lt;script&gt; (RegisterStartupScript, bloques inline).
    /// JsonConvert por defecto deja "&lt;/script&gt;" tal cual: un texto del conductor con ese
    /// contenido cerraría el bloque y lo que sigue se ejecutaría como HTML/JS. Con EscapeHtml
    /// los caracteres &lt; &gt; &amp; ' " dentro de las cadenas salen como \u003c, \u003e, etc.,
    /// y el navegador los lee igual al parsear el JSON.
    /// </summary>
    public static class JsonSeguro
    {
        private static readonly JsonSerializerSettings Ajustes = new JsonSerializerSettings
        {
            StringEscapeHandling = StringEscapeHandling.EscapeHtml
        };

        public static string Serializar(object valor) => JsonConvert.SerializeObject(valor, Ajustes);
    }
}
