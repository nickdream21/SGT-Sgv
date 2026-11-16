using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Cada $.ajax de Scripts/paginas que llama a 'Pagina.aspx/Metodo' con un JSON.stringify({...}) literal
    /// debe mandar exactamente los parámetros del [WebMethod]: si no, ASP.NET responde 500 ("Invalid web
    /// service call, missing value for parameter") y la pantalla falla sin aviso (pasó con el autocompletado
    /// de conductores de LiquidacionesAprobadasContabilidad, que mandaba "termino" a un método que espera "term").
    /// </summary>
    public class LlamadasWebMethodTests
    {
        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        /// <summary>Llamadas con discrepancia conocida, fuera del alcance de la tarea que agregó esta prueba
        /// ("archivo.js|Pagina.aspx/Metodo"). Vacía: no se encontró ninguna.</summary>
        private static readonly HashSet<string> Excluidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private sealed class Llamada
        {
            public string Archivo;
            public int Linea;
            public string Pagina;
            public string Metodo;
            public List<string> Parametros;
        }

        private static IEnumerable<Llamada> LlamadasConDatosLiterales(string raiz)
        {
            var url = new Regex(@"url\s*:\s*['""](?:[^'""]*/)?(\w+)\.aspx/(\w+)['""]");
            var datos = new Regex(@"data\s*:\s*JSON\.stringify\(\s*\{");
            foreach (string archivo in Directory.GetFiles(Path.Combine(raiz, "Scripts", "paginas"), "*.js", SearchOption.AllDirectories))
            {
                string js = File.ReadAllText(archivo);
                int desde = 0;
                while ((desde = js.IndexOf("$.ajax(", desde, StringComparison.Ordinal)) >= 0)
                {
                    int abre = js.IndexOf('(', desde);
                    int cierra = CierreBalanceado(js, abre, '(', ')');
                    desde = abre + 1;
                    if (cierra < 0) continue;
                    string bloque = js.Substring(abre, cierra - abre + 1);

                    Match mu = url.Match(bloque);
                    Match md = datos.Match(bloque);
                    if (!mu.Success || !md.Success) continue;

                    int llave = md.Index + md.Length - 1;
                    int finLlave = CierreBalanceado(bloque, llave, '{', '}');
                    if (finLlave < 0) continue;
                    string objeto = bloque.Substring(llave + 1, finLlave - llave - 1);
                    if (objeto.Contains("{") || objeto.Contains("...")) continue;   // solo objetos planos

                    yield return new Llamada
                    {
                        Archivo = archivo.Substring(raiz.Length + 1),
                        Linea = js.Take(abre).Count(c => c == '\n') + 1,
                        Pagina = mu.Groups[1].Value,
                        Metodo = mu.Groups[2].Value,
                        Parametros = ClavesDeObjeto(objeto)
                    };
                }
            }
        }

        /// <summary>Posición del cierre que corresponde a la apertura en <paramref name="inicio"/>, saltando cadenas.</summary>
        private static int CierreBalanceado(string texto, int inicio, char abre, char cierra)
        {
            int nivel = 0;
            char comilla = '\0';
            for (int i = inicio; i < texto.Length; i++)
            {
                char c = texto[i];
                if (comilla != '\0')
                {
                    if (c == '\\') i++;
                    else if (c == comilla) comilla = '\0';
                    continue;
                }
                if (c == '\'' || c == '"' || c == '`') comilla = c;
                else if (c == abre) nivel++;
                else if (c == cierra && --nivel == 0) return i;
            }
            return -1;
        }

        /// <summary>Claves del nivel superior de un objeto literal plano: "a: x, b: f(y, z), c" → a, b, c.</summary>
        private static List<string> ClavesDeObjeto(string objeto)
        {
            var partes = new List<string>();
            var actual = new StringBuilder();
            int nivel = 0;
            char comilla = '\0';
            for (int i = 0; i < objeto.Length; i++)
            {
                char c = objeto[i];
                if (comilla != '\0')
                {
                    actual.Append(c);
                    if (c == '\\' && i + 1 < objeto.Length) actual.Append(objeto[++i]);
                    else if (c == comilla) comilla = '\0';
                    continue;
                }
                if (c == '\'' || c == '"' || c == '`') comilla = c;
                if (c == '(' || c == '[') nivel++;
                if (c == ')' || c == ']') nivel--;
                if (c == ',' && nivel == 0) { partes.Add(actual.ToString()); actual.Clear(); continue; }
                actual.Append(c);
            }
            partes.Add(actual.ToString());

            var clave = new Regex(@"^\s*['""]?(\w+)['""]?\s*(?::|$)");
            return partes.Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => clave.Match(p))
                .Select(m => m.Success ? m.Groups[1].Value : "?")
                .ToList();
        }

        /// <summary>Parámetros del [WebMethod] público y estático <paramref name="metodo"/>, o null si no existe.</summary>
        private static List<string> ParametrosDelWebMethod(string codigo, string metodo)
        {
            var firma = new Regex(@"\[WebMethod[^\]]*\]\s*(?:\[[^\]]*\]\s*)*public\s+static\s+[\w<>\[\],\.\s]+?\s+"
                                  + Regex.Escape(metodo) + @"\s*\(([^)]*)\)");
            Match m = firma.Match(codigo);
            if (!m.Success) return null;
            return m.Groups[1].Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('=')[0].Trim())
                .Where(p => p.Length > 0)
                .Select(p => p.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Last())
                .ToList();
        }

        [Fact]
        public void AjaxDePaginas_MandanLosParametrosDelWebMethod()
        {
            string raiz = RaizWeb();
            var codigos = Directory.GetFiles(Path.Combine(raiz, "Views"), "*.aspx.cs", SearchOption.AllDirectories)
                .GroupBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(File.ReadAllText).ToList(), StringComparer.OrdinalIgnoreCase);

            var llamadas = LlamadasConDatosLiterales(raiz).ToList();
            Assert.True(llamadas.Count >= 10, "Se esperaban al menos 10 llamadas; se encontraron " + llamadas.Count);

            var errores = new List<string>();
            foreach (var ll in llamadas)
            {
                if (Excluidas.Contains(ll.Archivo.Replace('\\', '/') + "|" + ll.Pagina + ".aspx/" + ll.Metodo)) continue;
                string donde = ll.Archivo + ":" + ll.Linea + " → " + ll.Pagina + ".aspx/" + ll.Metodo;

                if (!codigos.TryGetValue(ll.Pagina + ".aspx.cs", out var fuentes))
                {
                    errores.Add(donde + ": no existe " + ll.Pagina + ".aspx.cs");
                    continue;
                }
                var esperados = fuentes.Select(f => ParametrosDelWebMethod(f, ll.Metodo)).FirstOrDefault(p => p != null);
                if (esperados == null)
                {
                    errores.Add(donde + ": no hay [WebMethod] public static " + ll.Metodo);
                    continue;
                }
                if (!new HashSet<string>(esperados).SetEquals(ll.Parametros) || esperados.Count != ll.Parametros.Count)
                    errores.Add(donde + ": manda {" + string.Join(", ", ll.Parametros) + "} y el método espera ("
                                + string.Join(", ", esperados) + ")");
            }
            Assert.True(errores.Count == 0, string.Join(Environment.NewLine, errores));
        }
    }
}
