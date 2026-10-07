using System.Web.UI;

namespace WebSGV
{
    public class BundleConfig
    {
        public static void RegisterJQueryScriptManager()
        {
            // Site.Master ya carga jQuery (con Bootstrap y jQuery UI) en el head. El recurso "jquery"
            // que pide la validación unobtrusive apunta a un script que solo carga jQuery si falta:
            // antes inyectaba una segunda copia (3.7.0) que reemplazaba $ y rompía los plugins.
            ScriptManager.ScriptResourceMapping.AddDefinition("jquery",
                new ScriptResourceDefinition
                {
                    Path = "~/Scripts/jquery-si-falta.js",
                    DebugPath = "~/Scripts/jquery-si-falta.js"
                });
        }
    }
}
