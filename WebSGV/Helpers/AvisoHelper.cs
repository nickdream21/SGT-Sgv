using System;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Mensajes al usuario con una sola forma en todo el sitio (E5-14):
    /// <list type="bullet">
    /// <item><see cref="Avisar"/>: aviso flotante (SGV.avisar) para los éxitos; se cierra solo a los 10 s y se pausa
    /// con el cursor o el foco encima.</item>
    /// <item><see cref="Pintar(Label, string, string, string)"/>: dentro de la página, con <c>role="alert"</c>, para
    /// errores y advertencias; no se oculta solo.</item>
    /// </list>
    /// El texto que llega es texto plano (p. ej. de <see cref="MensajeErrorHelper.ParaUsuario"/>): se codifica siempre
    /// (HTML al pintarlo, JavaScript al avisarlo) y los saltos de línea pasan a &lt;br/&gt;. Es el único lugar de
    /// Views que arma las clases <c>alert alert-*</c> (lo vigila AvisoHelperTests).
    /// </summary>
    public static class AvisoHelper
    {
        public const string Exito = "success";
        public const string Peligro = "danger";
        public const string Advertencia = "warning";
        public const string Info = "info";

        /// <summary>
        /// Tipo de Bootstrap a partir del que manda la página: acepta los de Bootstrap y los nombres en español
        /// (ok, exito, peligro, error, alerta). Lo desconocido es "info": nunca entra texto libre a la clase.
        /// </summary>
        public static string TipoSeguro(string tipo)
        {
            switch ((tipo ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "success":
                case "ok":
                case "exito":
                case "éxito":
                    return Exito;
                case "danger":
                case "peligro":
                case "error":
                    return Peligro;
                case "warning":
                case "alerta":
                case "advertencia":
                    return Advertencia;
                default:
                    return Info;
            }
        }

        public static bool EsExito(string tipo) => TipoSeguro(tipo) == Exito;

        /// <summary>Clases del aviso dentro de la página: "alert alert-{tipo} d-block" más las de la página.</summary>
        public static string Clases(string tipo, string clasesExtra = null)
        {
            string clases = "alert alert-" + TipoSeguro(tipo) + " d-block";
            string extra = (clasesExtra ?? string.Empty).Trim();
            return extra.Length == 0 ? clases : clases + " " + extra;
        }

        /// <summary>Script de inicio que muestra el aviso flotante (texto codificado como cadena de JavaScript).</summary>
        public static string ScriptAvisar(string texto, string tipo)
        {
            string t = HttpUtility.JavaScriptStringEncode(texto ?? string.Empty, true);
            string x = HttpUtility.JavaScriptStringEncode(TipoSeguro(tipo), true);
            // SiteMaster.js define SGV.avisar antes de los scripts de inicio; si no, se espera a que cargue la página.
            return "(function(){function f(){if(window.SGV&&SGV.avisar)SGV.avisar(" + t + ",{tipo:" + x + "});}" +
                   "if(window.SGV&&SGV.avisar)f();else document.addEventListener('DOMContentLoaded',f);})();";
        }

        /// <summary>
        /// Aviso flotante para un éxito. Sirve en postback completo y dentro de un UpdatePanel
        /// (ScriptManager.RegisterStartupScript con la página); varios avisos en la misma respuesta se muestran todos.
        /// </summary>
        public static void Avisar(Page pagina, string texto, string tipo = Exito)
        {
            if (pagina == null) throw new ArgumentNullException(nameof(pagina));
            ScriptManager.RegisterStartupScript(pagina, typeof(AvisoHelper), ClaveNueva(), ScriptAvisar(texto, tipo), true);
        }

        /// <summary>Aviso dentro de la página en un Label (se vuelve visible). No se oculta solo.</summary>
        public static void Pintar(Label etiqueta, string texto, string tipo, string clasesExtra = null) =>
            PintarHtml(etiqueta, MensajeHtml.Texto(texto), tipo, clasesExtra);

        /// <summary>
        /// Como <see cref="Pintar(Label, string, string, string)"/> pero con HTML armado por el código (negritas,
        /// renglones), con todo dato del usuario ya codificado. Nunca con texto que venga de afuera.
        /// </summary>
        public static void PintarHtml(Label etiqueta, string htmlDeConfianza, string tipo, string clasesExtra = null)
        {
            if (etiqueta == null) throw new ArgumentNullException(nameof(etiqueta));
            etiqueta.Text = htmlDeConfianza ?? string.Empty;
            etiqueta.CssClass = Clases(tipo, clasesExtra);
            etiqueta.Attributes["role"] = "alert";
            etiqueta.Visible = true;
        }

        /// <summary>Aviso dentro de la página en un Literal: escribe el bloque completo.</summary>
        public static void Pintar(Literal literal, string texto, string tipo, string clasesExtra = null)
        {
            if (literal == null) throw new ArgumentNullException(nameof(literal));
            literal.Text = Html(texto, tipo, clasesExtra);
            literal.Visible = true;
        }

        /// <summary>Bloque &lt;div class="alert ..." role="alert"&gt; con el texto codificado.</summary>
        public static string Html(string texto, string tipo, string clasesExtra = null) =>
            "<div class=\"" + HttpUtility.HtmlAttributeEncode(Clases(tipo, clasesExtra)) + "\" role=\"alert\">" +
            MensajeHtml.Texto(texto) + "</div>";

        /// <summary>
        /// Lo que hacen casi todas las páginas: un éxito va flotante (y se esconde el panel, para que no quede a la
        /// vista un error anterior guardado en el ViewState); lo demás se pinta en el Label y se muestra su panel.
        /// <paramref name="enPagina"/>: también el éxito se pinta, cuando trae una indicación o un resumen que hay
        /// que leer (más de unos 120 caracteres, "pulse", "revise", "ahora"...): un aviso flotante se va a los 10 s.
        /// </summary>
        public static void Mostrar(Page pagina, Control panel, Label etiqueta, string texto, string tipo, string clasesExtra = null,
            bool enPagina = false)
        {
            if (EsExito(tipo) && !enPagina)
            {
                if (panel != null) panel.Visible = false;
                Avisar(pagina, texto, tipo);
                return;
            }
            Pintar(etiqueta, texto, tipo, clasesExtra);
            if (panel != null) panel.Visible = true;
        }

        /// <summary>Igual que <see cref="Mostrar(Page, Control, Label, string, string, string, bool)"/> con un Literal.</summary>
        public static void Mostrar(Page pagina, Control panel, Literal literal, string texto, string tipo, string clasesExtra = null,
            bool enPagina = false)
        {
            if (EsExito(tipo) && !enPagina)
            {
                if (panel != null) panel.Visible = false;
                Avisar(pagina, texto, tipo);
                return;
            }
            Pintar(literal, texto, tipo, clasesExtra);
            if (panel != null) panel.Visible = true;
        }

        private static string ClaveNueva()
        {
            var ctx = HttpContext.Current;
            int n = 0;
            if (ctx != null)
            {
                n = ctx.Items["sgvAvisoN"] is int k ? k + 1 : 1;
                ctx.Items["sgvAvisoN"] = n;
            }
            else
            {
                n = Environment.TickCount;
            }
            return "sgvAviso" + n;
        }
    }
}
