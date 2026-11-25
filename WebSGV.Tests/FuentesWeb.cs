using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Tests.Integracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Lectura común de los fuentes de WebSGV para las pruebas que barren el código como texto (E6-4).
    /// Las páginas (<c>.aspx</c>) y los controles de usuario (<c>.ascx</c>) se leen juntos: lo que se mueva
    /// de una página a un control (RegistroDespacho, ListaDespachos) sigue revisado por las mismas reglas.
    /// </summary>
    internal static class FuentesWeb
    {
        /// <summary>Marcado de páginas y controles.</summary>
        public static readonly string[] PatronesMarcado = { "*.aspx", "*.ascx" };

        /// <summary>Code-behind de páginas y controles (sin los .designer.cs).</summary>
        public static readonly string[] PatronesCodigo = { "*.aspx.cs", "*.ascx.cs" };

        /// <summary>Carpeta WebSGV del repositorio.</summary>
        public static string RaizWeb()
        {
            Assert.NotNull(BaseDePruebas.RaizRepo);
            return Path.Combine(BaseDePruebas.RaizRepo, "WebSGV");
        }

        /// <summary>Ruta relativa a <paramref name="raiz"/>, con '/'.</summary>
        public static string Relativo(string raiz, string archivo) =>
            archivo.Substring(raiz.Length).TrimStart('\\', '/').Replace('\\', '/');

        /// <summary>
        /// Archivos de <paramref name="directorio"/> (y sus subcarpetas) que cumplen alguno de los patrones, sin
        /// obj/ ni bin/ ni *.designer.cs, sin repetidos y en orden. El patrón se vuelve a comprobar con el final del
        /// nombre: en Windows un patrón también puede encontrar extensiones más largas (nombres cortos 8.3).
        /// </summary>
        public static List<string> Archivos(string directorio, params string[] patrones)
        {
            if (!Directory.Exists(directorio)) return new List<string>();
            return patrones
                .SelectMany(p => Directory.GetFiles(directorio, p, SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(p.TrimStart('*'), StringComparison.OrdinalIgnoreCase)))
                .Where(f => !f.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase))
                .Where(f => !EnCarpeta(directorio, f, "obj") && !EnCarpeta(directorio, f, "bin"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool EnCarpeta(string directorio, string archivo, string carpeta)
        {
            string rel = Relativo(directorio, archivo);
            return rel.StartsWith(carpeta + "/", StringComparison.OrdinalIgnoreCase)
                || rel.IndexOf("/" + carpeta + "/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Páginas y controles (.aspx y .ascx) bajo <paramref name="directorio"/>.</summary>
        public static List<string> Marcado(string directorio) => Archivos(directorio, PatronesMarcado);

        /// <summary>Code-behind de páginas y controles (.aspx.cs y .ascx.cs) bajo <paramref name="directorio"/>.</summary>
        public static List<string> CodigoDeMarcado(string directorio) => Archivos(directorio, PatronesCodigo);

        /// <summary>
        /// Es un control de usuario (.ascx o su .ascx.cs). Un control no es una página: no tiene URL propia (IIS no
        /// sirve .ascx), ni directiva Page, ni Title, ni master, ni encabezado propio, ni [WebMethod] (PageMethods
        /// solo los busca en la página). Las reglas que solo valen para páginas lo excluyen con esto.
        /// </summary>
        public static bool EsControl(string archivo) =>
            archivo.EndsWith(".ascx", StringComparison.OrdinalIgnoreCase)
            || archivo.EndsWith(".ascx.cs", StringComparison.OrdinalIgnoreCase);

        private static readonly Regex Registro = new Regex(
            @"<%@\s*Register\b[^%]*?\bSrc\s*=\s*[""'](?<s>[^""']+\.ascx)[""'][^%]*%>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        /// <summary>
        /// Controles .ascx que registra el marcado de <paramref name="archivoMarcado"/> con
        /// <c>&lt;%@ Register Src="..." %&gt;</c> (ruta con ~ o relativa a la página), con su ruta completa.
        /// </summary>
        public static List<string> ControlesRegistrados(string raizWeb, string archivoMarcado)
        {
            string texto = File.ReadAllText(archivoMarcado);
            string carpeta = Path.GetDirectoryName(archivoMarcado);
            return Registro.Matches(texto).Cast<Match>()
                .Select(m => m.Groups["s"].Value.Replace('/', Path.DirectorySeparatorChar))
                .Select(s => s.StartsWith("~", StringComparison.Ordinal)
                    ? Path.Combine(raizWeb, s.TrimStart('~', Path.DirectorySeparatorChar))
                    : Path.Combine(carpeta, s))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
