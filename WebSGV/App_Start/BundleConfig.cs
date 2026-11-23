using System.Web.UI;

namespace WebSGV
{
    public class BundleConfig
    {
        public static void RegisterJQueryScriptManager()
        {
            // Site.Master ya carga jQuery (con Bootstrap) en el head. El recurso "jquery"
            // que pide la validación unobtrusive apunta a un script que solo carga jQuery si falta:
            // antes inyectaba una segunda copia (3.7.0) que reemplazaba $ y rompía los plugins.
            ScriptManager.ScriptResourceMapping.AddDefinition("jquery",
                new ScriptResourceDefinition
                {
                    Path = "~/Scripts/jquery-si-falta.js",
                    DebugPath = "~/Scripts/jquery-si-falta.js"
                });

            RegistrarMsAjaxConTextos();
        }

        /// <summary>Scripts de MS Ajax que se sirven desde System.Web.Extensions (ScriptResource.axd).</summary>
        public static readonly string[] ScriptsMsAjaxConTextos = { "MicrosoftAjax.js", "MicrosoftAjaxWebForms.js" };

        /// <summary>
        /// True si el script (con o sin ensamblado en la búsqueda) se sirve desde System.Web.Extensions
        /// y no desde un archivo estático. Lo usan las pruebas.
        /// </summary>
        public static bool MsAjaxSeSirveDesdeEnsamblado(string nombre)
        {
            var ensamblado = typeof(ScriptManager).Assembly;
            foreach (var definicion in new[]
            {
                ScriptManager.ScriptResourceMapping.GetDefinition(nombre, ensamblado),
                ScriptManager.ScriptResourceMapping.GetDefinition(nombre)
            })
            {
                if (definicion == null || !string.IsNullOrEmpty(definicion.Path)
                    || definicion.ResourceName != nombre || definicion.ResourceAssembly != ensamblado)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// El paquete Microsoft.AspNet.ScriptManager.MSAjax mapea MicrosoftAjax.js y
        /// MicrosoftAjaxWebForms.js a archivos estáticos (~/Scripts/WebForms/MSAjax) que no traen los
        /// textos de recursos (Sys.Res, Sys.WebForms.Res). Sin ellos, cualquier error de un postback
        /// parcial (sin conexión, 500 de IIS, respuesta mal formada) revienta dentro de MS Ajax
        /// ("Cannot read properties of undefined (reading 'PRM_ServerError')") antes del endRequest,
        /// y la capa "Procesando..." se queda fija. Servidos desde el ensamblado, ScriptResource.axd
        /// les agrega esos textos.
        /// </summary>
        private static void RegistrarMsAjaxConTextos()
        {
            var ensamblado = typeof(ScriptManager).Assembly;
            foreach (string nombre in ScriptsMsAjaxConTextos)
            {
                var definicion = new ScriptResourceDefinition
                {
                    ResourceName = nombre,
                    ResourceAssembly = ensamblado
                };
                ScriptManager.ScriptResourceMapping.AddDefinition(nombre, definicion);
                ScriptManager.ScriptResourceMapping.AddDefinition(nombre, ensamblado, definicion);
            }
        }
    }
}
