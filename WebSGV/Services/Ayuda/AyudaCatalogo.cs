using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Newtonsoft.Json;
using WebSGV.Helpers;

namespace WebSGV.Services.Ayuda
{
    /// <summary>Receta de una captura del tema para <c>herramientas/manual/capturar.mjs</c> (no la usa el sitio).</summary>
    public sealed class RecetaCaptura
    {
        /// <summary>Nombre del archivo en <c>img/&lt;clave&gt;/</c> ("01.webp").</summary>
        [JsonProperty("archivo")] public string Archivo { get; set; }

        /// <summary>Página relativa al sitio ("Views/RegistroTractos.aspx").</summary>
        [JsonProperty("url")] public string Url { get; set; }

        /// <summary>Ancho de la ventana: 1366 (oficina) o 390 (celular).</summary>
        [JsonProperty("ancho")] public int Ancho { get; set; }

        /// <summary>Selector CSS del bloque que se recorta.</summary>
        [JsonProperty("selector")] public string Selector { get; set; }

        /// <summary>Selectores en los que se hace clic antes de capturar (opcional).</summary>
        [JsonProperty("clics")] public List<string> Clics { get; set; } = new List<string>();
    }

    /// <summary>Un tema de la ayuda tal como está en <c>ContenidoAyuda/temas.json</c>.</summary>
    public sealed class TemaAyuda
    {
        [JsonProperty("clave")] public string Clave { get; set; }
        [JsonProperty("titulo")] public string Titulo { get; set; }

        /// <summary>Sección del índice (como el menú: "Despacho", "Catálogos"...).</summary>
        [JsonProperty("seccion")] public string Seccion { get; set; }
        [JsonProperty("orden")] public int Orden { get; set; }

        /// <summary>Grupos de rol que lo ven: A, C, K o Y (ver <see cref="AyudaCatalogo.Grupo"/>).</summary>
        [JsonProperty("roles")] public List<string> Roles { get; set; } = new List<string>();

        /// <summary>Páginas cuyo "?" abre este tema ("Views/X.aspx").</summary>
        [JsonProperty("paginas")] public List<string> Paginas { get; set; } = new List<string>();

        /// <summary>appSetting que debe estar en "true" para mostrar el tema (vacío: siempre).</summary>
        [JsonProperty("interruptor")] public string Interruptor { get; set; }

        /// <summary>Enlace al video del tema (E5-22); vacío: sin video.</summary>
        [JsonProperty("video")] public string Video { get; set; }

        /// <summary>Tema todavía sin texto revisado (E5-21b): no se lista ni se puede abrir.</summary>
        [JsonProperty("borrador")] public bool Borrador { get; set; }

        [JsonProperty("capturas")] public List<RecetaCaptura> Capturas { get; set; } = new List<RecetaCaptura>();
    }

    /// <summary>
    /// Ayuda dentro de la app (E5-21, M-074). Lee <c>ContenidoAyuda/temas.json</c> (única fuente de los temas,
    /// para el sitio, las pruebas y el manual) y decide qué ve cada rol. Puro: no lee la sesión ni la
    /// configuración (el interruptor llega como función). Se lee el archivo en cada pedido (pocos KB):
    /// no hay caché que invalidar al publicar. La carpeta no se sirve por URL (hiddenSegments en Web.config):
    /// los fragmentos los muestra <c>Views/Ayuda.aspx</c> y las imágenes <c>Views/AyudaImagen.aspx</c>.
    /// </summary>
    public sealed class AyudaCatalogo
    {
        public const string ArchivoTemas = "temas.json";
        public const string CarpetaImagenes = "img";

        /// <summary>Grupos de rol de la ayuda (los de GuardasAccesoTests.AccesoEsperado).</summary>
        public const string GrupoAdmin = "A", GrupoConductor = "C", GrupoContabilidad = "K", GrupoSistema = "Y";

        public static readonly IReadOnlyList<string> Grupos = new[] { GrupoAdmin, GrupoConductor, GrupoContabilidad, GrupoSistema };

        // \z y no $: "$" aceptaría un salto de línea final.
        public static readonly Regex ClaveValida = new Regex(@"^[a-z0-9-]{1,40}\z", RegexOptions.CultureInvariant);
        public static readonly Regex ImagenValida = new Regex(@"^[0-9]{2}\.webp\z", RegexOptions.CultureInvariant);

        private static readonly Regex SrcImagen = new Regex(@"\bsrc=""(?<a>[0-9]{2}\.webp)""", RegexOptions.CultureInvariant);
        private static readonly Regex HrefTema = new Regex(@"\bhref=""\?tema=(?<c>[a-z0-9-]{1,40})""", RegexOptions.CultureInvariant);

        private readonly string _carpeta;

        private AyudaCatalogo(string carpeta, List<TemaAyuda> temas)
        {
            _carpeta = carpeta;
            Temas = temas;
        }

        /// <summary>Carpeta física de ContenidoAyuda (con la barra final quitada).</summary>
        public string Carpeta => _carpeta;

        /// <summary>Todos los temas, también los borradores, en el orden del archivo.</summary>
        public IReadOnlyList<TemaAyuda> Temas { get; }

        private sealed class Raiz
        {
            [JsonProperty("temas")] public List<TemaAyuda> Temas { get; set; } = new List<TemaAyuda>();
        }

        /// <summary>Lee <c>temas.json</c> de la carpeta indicada (física). Lanza si falta o está mal formado.</summary>
        public static AyudaCatalogo Cargar(string carpeta)
        {
            if (string.IsNullOrWhiteSpace(carpeta)) throw new ArgumentException("Falta la carpeta de la ayuda.", nameof(carpeta));
            string raiz = Path.GetFullPath(carpeta).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string json = File.ReadAllText(Path.Combine(raiz, ArchivoTemas), Encoding.UTF8);
            var datos = JsonConvert.DeserializeObject<Raiz>(json) ?? new Raiz();
            return new AyudaCatalogo(raiz, datos.Temas ?? new List<TemaAyuda>());
        }

        private static readonly object CandadoCache = new object();
        private static AyudaCatalogo _cache;
        private static string _cacheCarpeta;
        private static DateTime _cacheFecha;

        /// <summary>
        /// <see cref="Cargar"/> guardado en memoria mientras temas.json no cambie (fecha del archivo): lo usa el menú
        /// de Site.Master en cada página.
        /// </summary>
        public static AyudaCatalogo CargarConCache(string carpeta)
        {
            string raiz = Path.GetFullPath(carpeta ?? "").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            DateTime fecha = File.GetLastWriteTimeUtc(Path.Combine(raiz, ArchivoTemas));
            lock (CandadoCache)
            {
                if (_cache != null && _cacheFecha == fecha && string.Equals(_cacheCarpeta, raiz, StringComparison.OrdinalIgnoreCase))
                    return _cache;
            }
            var nuevo = Cargar(raiz);
            lock (CandadoCache) { _cache = nuevo; _cacheCarpeta = raiz; _cacheFecha = fecha; }
            return nuevo;
        }

        /// <summary>
        /// Grupo de la ayuda de un rol de sesión (cualquier forma de mayúsculas o espacios; alias incluidos):
        /// A administración, C conductor, K contabilidad, Y administrador de sistema. Rol retirado o desconocido: null.
        /// </summary>
        public static string Grupo(string rol)
        {
            string r = (rol ?? "").Trim().ToUpperInvariant();
            if (RolesHelper.EsRolAdmin(r)) return GrupoAdmin;
            if (RolesHelper.EsRolConductor(r)) return GrupoConductor;
            if (r == RolesHelper.ROL_CONTABILIDAD) return GrupoContabilidad;
            if (r == RolesHelper.ROL_ADMIN_SISTEMA) return GrupoSistema;
            return null;
        }

        /// <summary>Tema por su clave (también borradores), o null.</summary>
        public TemaAyuda Tema(string clave) =>
            string.IsNullOrEmpty(clave) ? null : Temas.FirstOrDefault(t => string.Equals(t.Clave, clave, StringComparison.Ordinal));

        private static bool Encendido(TemaAyuda t, Func<string, bool> interruptor) =>
            string.IsNullOrWhiteSpace(t.Interruptor) || (interruptor != null && interruptor(t.Interruptor));

        /// <summary>
        /// True si el rol puede abrir el tema: existe, no es borrador, su interruptor está encendido y el grupo
        /// del rol está en sus roles. <paramref name="interruptor"/> dice si un appSetting está en "true".
        /// </summary>
        public bool PuedeVer(string clave, string rol, Func<string, bool> interruptor)
        {
            var t = Tema(clave);
            string g = Grupo(rol);
            return t != null && g != null && !t.Borrador && Encendido(t, interruptor)
                && t.Roles != null && t.Roles.Contains(g);
        }

        /// <summary>Temas que el rol puede abrir, por sección (en el orden en que aparece cada una) y orden.</summary>
        public IReadOnlyList<TemaAyuda> TemasDelRol(string rol, Func<string, bool> interruptor)
        {
            var visibles = Temas.Where(t => PuedeVer(t.Clave, rol, interruptor)).ToList();
            var secciones = Temas.Select(t => t.Seccion).Distinct().ToList();
            return visibles.OrderBy(t => secciones.IndexOf(t.Seccion)).ThenBy(t => t.Orden).ToList();
        }

        /// <summary>Tema cuyo "?" corresponde a la página ("~/Views/X.aspx", "/Views/X.aspx" o "Views/X.aspx"), o null.</summary>
        public TemaAyuda TemaDePagina(string ruta)
        {
            string r = MenuSitio.NormalizarRuta(ruta);
            return Temas.FirstOrDefault(t => t.Paginas != null && t.Paginas.Any(p => MenuSitio.NormalizarRuta(p) == r));
        }

        /// <summary>Ruta física del fragmento HTML del tema (no comprueba permisos), o null si la clave no es válida.</summary>
        public string RutaFragmento(string clave) =>
            clave != null && ClaveValida.IsMatch(clave) ? Path.Combine(_carpeta, clave + ".html") : null;

        /// <summary>Texto del fragmento del tema, o null si no existe.</summary>
        public string LeerFragmento(string clave)
        {
            string ruta = RutaFragmento(clave);
            return ruta != null && File.Exists(ruta) ? File.ReadAllText(ruta, Encoding.UTF8) : null;
        }

        /// <summary>
        /// Ruta física de una imagen del tema ("img/&lt;clave&gt;/NN.webp"). Rechaza todo lo que no sea una clave
        /// válida y un nombre de dos dígitos con extensión .webp en minúsculas (sin "..", barras ni escapes), y
        /// comprueba que la ruta resuelta quede dentro de la carpeta de imágenes y que el archivo exista.
        /// </summary>
        public bool TryRutaImagen(string clave, string archivo, out string ruta)
        {
            ruta = null;
            if (clave == null || archivo == null || !ClaveValida.IsMatch(clave) || !ImagenValida.IsMatch(archivo)) return false;
            string raizImg = Path.GetFullPath(Path.Combine(_carpeta, CarpetaImagenes)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string completa = Path.GetFullPath(Path.Combine(raizImg, clave, archivo));
            if (!completa.StartsWith(raizImg, StringComparison.OrdinalIgnoreCase) || !File.Exists(completa)) return false;
            ruta = completa;
            return true;
        }

        /// <summary>
        /// Cambia cada <c>src="NN.webp"</c> del fragmento por la URL que da <paramref name="url"/> (la de
        /// AyudaImagen.aspx). Nada más del fragmento se toca.
        /// </summary>
        public static string ReescribirImagenes(string html, Func<string, string> url) =>
            html == null ? null : SrcImagen.Replace(html, m => "src=\"" + HttpUtility.HtmlAttributeEncode(url(m.Groups["a"].Value)) + "\"");

        /// <summary>
        /// Cambia cada <c>href="?tema=X"</c> del fragmento por la URL de Ayuda.aspx del tema y le agrega
        /// <c>data-ayuda="X"</c>, para que dentro del modal abra el otro tema en el mismo modal.
        /// </summary>
        public static string ReescribirEnlaces(string html, Func<string, string> url) =>
            html == null ? null : HrefTema.Replace(html, m =>
                "href=\"" + HttpUtility.HtmlAttributeEncode(url(m.Groups["c"].Value)) + "\" data-ayuda=\"" + m.Groups["c"].Value + "\"");
    }
}
