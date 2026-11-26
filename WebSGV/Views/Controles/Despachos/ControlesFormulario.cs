using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Despachos;

namespace WebSGV.Views.Controles.Despachos
{
    /// <summary>
    /// Utilidades de formulario de los controles de RegistroDespacho (E6-24): leer y llenar listas, codificar texto,
    /// llevar al usuario hasta un aviso y refrescar el UpdatePanel que los contiene.
    /// </summary>
    internal static class ControlesFormulario
    {
        /// <summary>Lo elegido en un selector: su valor y su texto (null si no hay nada elegido).</summary>
        public static OpcionElegida Opcion(DropDownList ddl) => new OpcionElegida { Valor = ddl.SelectedValue, Texto = ddl.SelectedItem?.Text };

        /// <summary>Todas las opciones de un selector (valor y texto).</summary>
        public static List<OpcionElegida> Opciones(DropDownList ddl) =>
            ddl.Items.Cast<ListItem>().Select(i => new OpcionElegida { Valor = i.Value, Texto = i.Text }).ToList();

        /// <summary>Elige <paramref name="valor"/> si está entre las opciones; si no (o vacío), deja la que había.</summary>
        public static void RestaurarSeleccion(DropDownList ddl, string valor)
        {
            if (string.IsNullOrEmpty(valor)) return;
            if (ddl.Items.FindByValue(valor) != null) ddl.SelectedValue = valor;
        }

        /// <summary>Añade las filas de <paramref name="fuente"/> como opciones (texto y valor de sus columnas).</summary>
        public static void Agregar(DropDownList ddl, DataTable fuente, string texto, string valor)
        {
            foreach (DataRow r in fuente.Rows)
                ddl.Items.Add(new ListItem(r[texto].ToString(), r[valor].ToString()));
        }

        public static string Html(string texto) => HttpUtility.HtmlEncode(texto ?? string.Empty);

        /// <summary>Al log, con el contexto (el nombre del método) como dato.</summary>
        public static void RegistrarError(string contexto, Exception ex) => LogSGV.Error(ex, "Error en {Contexto}", contexto);

        /// <summary>Quién registra (auditoría y lote): el nombre de la sesión; sin él, el usuario autenticado o "Sistema".</summary>
        public static string UsuarioActual(Page pagina)
        {
            // MAJ-004: nombre de sesión (SesionHelper) consistente con el resto del proyecto
            string nombre = SesionHelper.Nombre(pagina.Session);
            if (nombre.Length > 0) return nombre;
            return pagina.User.Identity.IsAuthenticated ? pagina.User.Identity.Name : "Sistema";
        }

        /// <summary>Borra un archivo temporal (manifiesto, factura o CPIC) si existe (sin lanzar si falla).</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types",
            Justification = "Borrar un temporal es limpieza: un fallo se registra y nunca debe cortar la operación que ya terminó.")]
        public static void EliminarArchivoTemporal(string rutaTemp)
        {
            try
            {
                if (!string.IsNullOrEmpty(rutaTemp) && File.Exists(rutaTemp))
                    File.Delete(rutaTemp);
            }
            catch (Exception ex)
            {
                RegistrarError("EliminarArchivoTemporal", ex);
            }
        }

        /// <summary>Por qué no sirve el Word de la programación elegido (sin archivo, no .docx, pesado o falso); null si está bien.</summary>
        public static string ErrorWordProgramacion(FileUpload word)
        {
            if (!word.HasFile)
                return "Elija el Word de la programación (.docx).";
            if (!string.Equals(Path.GetExtension(word.FileName), ".docx", StringComparison.OrdinalIgnoreCase))
                return "La programación debe ser un Word .docx. Si es un .doc antiguo, guárdelo como .docx.";
            string error = ArchivoSubidoHelper.Validar(word.PostedFile, ArchivoSubidoHelper.MaxWord, ".docx");
            return error == null ? null : $"Word de la programación: {error}.";
        }

        /// <summary>Tras pintar, lleva al usuario hasta el elemento (los botones están al pie de tablas largas).</summary>
        public static void LlevarHasta(Page pagina, string clave, string idCliente) =>
            ScriptManager.RegisterStartupScript(pagina, pagina.GetType(), clave,
                $"setTimeout(function(){{var m=document.getElementById('{idCliente}');if(m)m.scrollIntoView({{behavior:'smooth',block:'center'}});}},50);", true);

        /// <summary>
        /// Llama a una función de <c>Scripts/paginas/RegistroDespacho.js</c> desde el servidor. En un postback completo
        /// el startup script se pinta dentro del form, antes de que cargue ese .js (va al final de la página): se
        /// espera al "load" (también deja actuar antes a MaintainScrollPositionOnPostback). En un postback parcial
        /// la página ya está cargada y se llama enseguida. <paramref name="funcion"/> es un nombre fijo, nunca dato.
        /// </summary>
        public static void LlamarFuncionJsAlCargar(Page pagina, string clave, string funcion) =>
            ScriptManager.RegisterStartupScript(pagina, pagina.GetType(), clave,
                $"(function(){{function ir(){{if(typeof window['{funcion}']==='function')window['{funcion}']();}}" +
                "if(document.readyState==='complete')ir();else window.addEventListener('load',ir);})();", true);

        /// <summary>
        /// Texto del aviso de un error de operación (E6-13): el mensaje de negocio (ErrorNegocioException o RAISERROR/THROW
        /// del SP) tal cual, o <paramref name="mensajeUsuario"/> si es técnico. Registra una sola vez: el técnico como
        /// ERR con la pila y el de negocio como INF (antes se mostraba siempre el texto fijo y todo quedaba como ERR).
        /// </summary>
        public static string TextoErrorOperacion(string mensajeUsuario, string contexto, Exception ex) =>
            MensajeErrorHelper.ParaUsuarioOTexto(ex, mensajeUsuario, "Error en {Contexto}", contexto);

        /// <summary>Refresca el UpdatePanel condicional que contiene a <paramref name="control"/> (UpdatePanelMain).</summary>
        public static void RefrescarPanel(Control control)
        {
            for (Control c = control.Parent; c != null; c = c.Parent)
                if (c is UpdatePanel panel)
                {
                    if (panel.UpdateMode == UpdatePanelUpdateMode.Conditional) panel.Update();
                    return;
                }
        }
    }
}
