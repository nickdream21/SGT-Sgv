using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Tests.Integracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// El código de la web registra con <c>LogSGV</c> (Serilog: ventana Output y archivo en
    /// App_Data/logs). <c>Debug.WriteLine</c>/<c>Trace</c>/<c>Console</c> no dejan rastro en producción
    /// y llevaban a catch que "registraban" un error que nadie podía ver (auditoría de seguridad, 8D-1).
    /// </summary>
    public class CodigoFuenteLimpiezaTests
    {
        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static readonly string[] Extensiones = { "*.cs", "*.aspx", "*.ascx", "*.Master", "*.ashx", "*.asax" };
        private static readonly string[] CarpetasExcluidas = { "obj", "bin", "Database" };

        /// <summary>Archivos (ruta relativa a WebSGV, con '/') que pueden usar estas APIs y por qué. Vacío.</summary>
        private static readonly Dictionary<string, string> Excepciones =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Regex LlamadaProhibida = new Regex(
            @"\b(System\.Diagnostics\.)?(Debug|Trace)\.(Write|WriteLine|WriteIf|WriteLineIf|Print|TraceError|TraceWarning|TraceInformation)\b" +
            @"|\bConsole\.(Write|WriteLine|Error)\b" +
            // using static System.Diagnostics.Debug; / using D = System.Diagnostics.Debug;
            // permitirían llamar a WriteLine(...) sin el prefijo que detecta la expresión anterior.
            @"|^using\s+(static\s+)?(\w+\s*=\s*)?System\.Diagnostics\.(Debug|Trace)\b");

        private static IEnumerable<string> ArchivosFuente(string raiz)
        {
            return Extensiones
                .SelectMany(ext => Directory.GetFiles(raiz, ext, SearchOption.AllDirectories))
                .Where(f =>
                {
                    string relativo = f.Substring(raiz.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string primera = relativo.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                    return !CarpetasExcluidas.Contains(primera, StringComparer.OrdinalIgnoreCase);
                })
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static List<string> BuscarLlamadas(string raiz) => Buscar(raiz, LlamadaProhibida, Excepciones);

        private static List<string> Buscar(string raiz, Regex patron, IDictionary<string, string> excepciones)
        {
            var hallazgos = new List<string>();
            foreach (string archivo in ArchivosFuente(raiz))
            {
                string relativo = archivo.Substring(raiz.Length).TrimStart('\\', '/').Replace('\\', '/');
                if (excepciones.ContainsKey(relativo)) continue;

                string[] lineas = File.ReadAllLines(archivo);
                for (int i = 0; i < lineas.Length; i++)
                {
                    string linea = lineas[i].TrimStart();
                    if (linea.StartsWith("//", StringComparison.Ordinal)) continue; // también ///
                    if (patron.IsMatch(linea))
                        hallazgos.Add($"{relativo}:{i + 1}: {linea.Trim()}");
                }
            }
            return hallazgos;
        }

        /// <summary>
        /// Acceso directo a las claves de sesión de autenticación (UsuarioID, el antiguo IdUsuario,
        /// Rol y Nombre; sin importar mayúsculas): <c>Session["UsuarioID"]</c>, <c>ctx.Session?["Rol"]</c>,
        /// <c>sesion["Nombre"]</c>, <c>Session.Contents["Rol"]</c>, <c>Session.Add("Rol", ..)</c>,
        /// <c>Session.Remove("UsuarioID")</c> y las constantes <c>SesionHelper.Clave*</c> como índice o
        /// argumento. Otras claves ("NombreUsuario", "Usuario", "IdConductor"...) quedan fuera.
        /// </summary>
        private const string ObjetoSesion = @"\b\w*ses+ion\??(\.Contents)?\??";
        private const string ClaveSesion =
            @"(""(UsuarioID|IdUsuario|Rol|Nombre)""|(SesionHelper\.)?Clave(UsuarioId|Rol|Nombre)\b)";

        private static readonly Regex AccesoClaveSesion = new Regex(
            ObjetoSesion + @"\s*\[\s*" + ClaveSesion +                         // Session["Rol"], Session.Contents["Rol"]
            "|" + ObjetoSesion + @"\.(Add|Remove)\s*\(\s*" + ClaveSesion +      // Session.Add("Rol", ..), Session.Remove("Rol")
            @"|\[\s*(SesionHelper\.)?Clave(UsuarioId|Rol|Nombre)\s*\]",          // x[SesionHelper.ClaveRol]
            RegexOptions.IgnoreCase);

        private static readonly Dictionary<string, string> ExcepcionesClavesSesion =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Helpers/SesionHelper.cs", "único punto de lectura y escritura de esas claves" }
            };

        [Fact]
        public void ClavesDeSesion_SoloEnSesionHelper()
        {
            string raiz = RaizWeb();
            Assert.Matches(AccesoClaveSesion, "if (Session[\"UsuarioID\"] != null)");
            Assert.Matches(AccesoClaveSesion, "x = ctx.Session?[\"Rol\"];");
            Assert.Matches(AccesoClaveSesion, "sesion[ \"IdUsuario\" ] = 1;");
            Assert.Matches(AccesoClaveSesion, "string n = Convert.ToString(Session[\"Nombre\"]);");
            Assert.Matches(AccesoClaveSesion, "x = HttpContext.Current.Session.Contents[\"Rol\"];");
            Assert.Matches(AccesoClaveSesion, "Session.Add(\"UsuarioID\", 5);");
            Assert.Matches(AccesoClaveSesion, "Session.Remove( \"Nombre\" );");
            Assert.Matches(AccesoClaveSesion, "Session.Contents.Remove(\"Rol\");");
            Assert.Matches(AccesoClaveSesion, "x = Session[SesionHelper.ClaveRol];");
            Assert.Matches(AccesoClaveSesion, "Session.Remove(SesionHelper.ClaveUsuarioId);");
            Assert.Matches(AccesoClaveSesion, "x = ctx.Session?[SesionHelper.ClaveNombre];");
            // Otras claves de sesión no son de autenticación.
            Assert.DoesNotMatch(AccesoClaveSesion, "x = Session[\"NombreUsuario\"];");
            Assert.DoesNotMatch(AccesoClaveSesion, "x = Session[\"Usuario\"];");
            Assert.DoesNotMatch(AccesoClaveSesion, "x = Session[\"IdConductor\"];");
            Assert.DoesNotMatch(AccesoClaveSesion, "x = fila[\"rol\"];");

            List<string> hallazgos = Buscar(raiz, AccesoClaveSesion, ExcepcionesClavesSesion);

            Assert.True(hallazgos.Count == 0,
                "Lee y escribe UsuarioID/Rol/Nombre con SesionHelper (IdUsuario, Rol, Nombre, HaySesion, Iniciar), " +
                "no con la sesión directamente (auditoría de seguridad, 8D-2):" +
                Environment.NewLine + string.Join(Environment.NewLine, hallazgos));
        }

        /// <summary>
        /// Comparación de un nombre de rol escrito a mano (sin importar mayúsculas):
        /// <c>rol == "ADMIN"</c>, <c>"CHOFER" != rol</c>, <c>string.Equals(rol?.Trim(), "CHOFER", ..)</c>,
        /// <c>case "OPERADOR":</c>, patrones (<c>rol is "ADMIN" or "ADMINISTRADOR"</c>, <c>rol is not "CHOFER"</c>,
        /// brazos de switch <c>"SUPERVISOR" =&gt; ..</c>) y constantes propias (<c>const string X = "SUPERVISOR"</c>).
        /// Los nombres de rol viven solo en <c>RolesHelper</c> (ROL_*, EsRolAdmin, EsRolConductor).
        /// </summary>
        private const string LiteralRol =
            @"""(ADMIN|ADMINISTRADOR|CHOFER|CONDUCTOR|SUPERVISOR|OPERADOR|CONTABILIDAD|ADMINISTRADOR DE (SISTEMA|GRIFO|MAQUINARIA|TRANSPORTE))""";

        private static readonly Regex ComparacionLiteralRol = new Regex(
            @"(==|!=)\s*" + LiteralRol +
            "|" + LiteralRol + @"\s*(==|!=|=>)" +                                 // también brazo de switch: "ADMIN" => ..
            @"|\bEquals\s*\((?:[^()]|\([^()]*\))*" + LiteralRol +              // admite un nivel de paréntesis: rol?.Trim()
            @"|\bcase\s+" + LiteralRol + @"\s*:" +
            @"|\bis\s+(not\s+)?" + LiteralRol +                                 // rol is "ADMIN", rol is not "CHOFER"
            @"|\bor\s+" + LiteralRol +                                          // rol is X or "ADMINISTRADOR"
            @"|\b(const|static\s+readonly|readonly)\s+string\s+\w+\s*=\s*" + LiteralRol,
            RegexOptions.IgnoreCase);

        /// <summary>
        /// Archivos (ruta relativa a WebSGV, con '/') con coincidencias que no son el rol del usuario.
        /// <c>Permitido</c> es el texto exacto que se tolera: se quita de la línea antes de evaluarla, así que
        /// cualquier otra comparación de rol en ese archivo sí se detecta. null = archivo exento completo.
        /// </summary>
        private static readonly Dictionary<string, (string Motivo, Regex Permitido)> ExcepcionesLiteralesRol =
            new Dictionary<string, (string Motivo, Regex Permitido)>(StringComparer.OrdinalIgnoreCase)
            {
                { "Views/RolesHelper.cs", ("única definición de los nombres de rol", null) },
                { "Views/VerificarDocumento.aspx.cs", ("rolFirmante de FirmaDigital (ADMIN/CONDUCTOR), no el rol del usuario",
                    new Regex(@"\[""rolFirmante""\]\)\s*==\s*""ADMIN""")) },
                { "Views/ReportesOrdenesViaje.aspx.cs", ("\"conductor\" es la clave de orden/columna del reporte, no un rol",
                    new Regex(@"(==\s*""conductor""|\bcase\s+""conductor""\s*:)")) },
                { "Services/Reportes/ReportesOrdenesViajeService.cs", ("\"conductor\" es la clave de orden del reporte, no un rol",
                    new Regex(@"(==\s*""conductor""|\bcase\s+""conductor""\s*:)")) }
            };

        /// <summary>Líneas de código (sin comentarios //) de cada archivo fuente, con su ruta relativa a WebSGV.</summary>
        private static IEnumerable<(string Relativo, int Numero, string Linea)> LineasDeCodigo(string raiz)
        {
            foreach (string archivo in ArchivosFuente(raiz))
            {
                string relativo = archivo.Substring(raiz.Length).TrimStart('\\', '/').Replace('\\', '/');
                string[] lineas = File.ReadAllLines(archivo);
                for (int i = 0; i < lineas.Length; i++)
                {
                    string linea = lineas[i].TrimStart();
                    if (linea.StartsWith("//", StringComparison.Ordinal)) continue; // también ///
                    yield return (relativo, i + 1, linea.Trim());
                }
            }
        }

        private static List<string> BuscarLiteralesRol(string raiz)
        {
            var hallazgos = new List<string>();
            foreach (var l in LineasDeCodigo(raiz))
            {
                string evaluada = l.Linea;
                if (ExcepcionesLiteralesRol.TryGetValue(l.Relativo, out var excepcion))
                {
                    if (excepcion.Permitido == null) continue;
                    evaluada = excepcion.Permitido.Replace(evaluada, "");
                }
                if (ComparacionLiteralRol.IsMatch(evaluada))
                    hallazgos.Add($"{l.Relativo}:{l.Numero}: {l.Linea}");
            }
            return hallazgos;
        }

        [Fact]
        public void LiteralesDeRol_SoloEnRolesHelper()
        {
            string raiz = RaizWeb();
            Assert.Matches(ComparacionLiteralRol, "<% if (EsAdmin || RolUsuario == \"SUPERVISOR\")");
            Assert.Matches(ComparacionLiteralRol, "else if (rol.ToUpper() == \"OPERADOR\" && x)");
            Assert.Matches(ComparacionLiteralRol, "return rol == ROL_CONDUCTOR || rol == \"CHOFER\";");
            Assert.Matches(ComparacionLiteralRol, "if (\"ADMIN\" != rol)");
            Assert.Matches(ComparacionLiteralRol, "if (rol.ToLower() == \"admin\")");
            Assert.Matches(ComparacionLiteralRol, "|| string.Equals(rol?.Trim(), \"CHOFER\", StringComparison.OrdinalIgnoreCase);");
            Assert.Matches(ComparacionLiteralRol, "if (rol.Equals(\"ADMINISTRADOR DE GRIFO\"))");
            Assert.Matches(ComparacionLiteralRol, "case \"CONTABILIDAD\":");
            Assert.Matches(ComparacionLiteralRol, "private const string ROL_ADMIN_SISTEMA  = \"ADMINISTRADOR DE SISTEMA\";");
            Assert.Matches(ComparacionLiteralRol, "private static readonly string RolX = \"ADMINISTRADOR DE TRANSPORTE\";");
            // No son comparaciones de rol.
            Assert.DoesNotMatch(ComparacionLiteralRol, "if (rol == RolesHelper.ROL_CONTABILIDAD)");
            Assert.DoesNotMatch(ComparacionLiteralRol, "Col(\"CONDUCTOR\", TipoColumnaReporte.Texto, 2.2f, f => f[\"Conductor\"]);");
            Assert.DoesNotMatch(ComparacionLiteralRol, "int chofer = Columna(\"CHOFER\", \"CONDUCTOR\");");
            Assert.DoesNotMatch(ComparacionLiteralRol, "WHERE rol IN ('ADMIN', 'SUPERVISOR')");
            Assert.DoesNotMatch(ComparacionLiteralRol, "if (x == \"ADMINISTRADORA\")");
            Assert.Matches(ComparacionLiteralRol, "if (rol is \"ADMIN\")");
            Assert.Matches(ComparacionLiteralRol, "if (rol is not \"CHOFER\") return;");
            Assert.Matches(ComparacionLiteralRol, "bool a = rol is RolesHelper.ROL_ADMIN or \"ADMINISTRADOR\";");
            Assert.Matches(ComparacionLiteralRol, "\"SUPERVISOR\" => \"~/Views/Inicio.aspx\",");
            Assert.Matches(ComparacionLiteralRol, "return rol switch { \"operador\"=> 1, _ => 0 };");
            Assert.DoesNotMatch(ComparacionLiteralRol, "if (x is string s && s.Length > 0)");
            Assert.DoesNotMatch(ComparacionLiteralRol, "if (x is \"ADMINISTRADORA\" or \"CHOFERES\")");
            Assert.DoesNotMatch(ComparacionLiteralRol, "string t = \"Conductor or\" + \" chofer\";");
            Assert.DoesNotMatch(ComparacionLiteralRol, "Func<string> f = () => \"CONDUCTOR\";");
            Assert.DoesNotMatch(ComparacionLiteralRol, "{ \"ADMIN\", \"Administrador\" },");

            // Las excepciones toleran solo su texto exacto: otra comparación en el mismo archivo sí se detecta.
            Regex permitidoReporte = ExcepcionesLiteralesRol["Views/ReportesOrdenesViaje.aspx.cs"].Permitido;
            Assert.DoesNotMatch(ComparacionLiteralRol, permitidoReporte.Replace("case \"conductor\": return \"Conductor\";", ""));
            Assert.DoesNotMatch(ComparacionLiteralRol, permitidoReporte.Replace(".Where(c => c.Item1 == \"conductor\" || c.Item1 == \"numero\")", ""));
            Assert.Matches(ComparacionLiteralRol, permitidoReporte.Replace("if (rol == \"ADMIN\")", ""));
            Assert.Matches(ComparacionLiteralRol, permitidoReporte.Replace("if (rol == \"CONDUCTOR\")", ""));
            Regex permitidoFirma = ExcepcionesLiteralesRol["Views/VerificarDocumento.aspx.cs"].Permitido;
            Assert.DoesNotMatch(ComparacionLiteralRol, permitidoFirma.Replace("(Convert.ToString(r[\"rolFirmante\"]) == \"ADMIN\" ? \" (administración)\" : \" (conductor)\"));", ""));
            Assert.Matches(ComparacionLiteralRol, permitidoFirma.Replace("if (rol == \"ADMIN\")", ""));

            List<string> hallazgos = BuscarLiteralesRol(raiz);

            Assert.True(hallazgos.Count == 0,
                "Compara roles con RolesHelper (ROL_*, EsRolAdmin, EsRolConductor), no con el nombre escrito " +
                "a mano (auditoría de seguridad, 8D-3):" +
                Environment.NewLine + string.Join(Environment.NewLine, hallazgos));
        }

        [Fact]
        public void LiteralesDeRol_CadaExcepcionSigueSiendoNecesaria()
        {
            string raiz = RaizWeb();
            List<(string Relativo, int Numero, string Linea)> lineas = LineasDeCodigo(raiz).ToList();

            // Exento completo: el archivo debe seguir teniendo coincidencias. Con Permitido: ese texto
            // exacto debe seguir apareciendo en una línea que la búsqueda marcaría.
            var sobrantes = new List<string>();
            foreach (var e in ExcepcionesLiteralesRol)
            {
                bool necesaria = lineas
                    .Where(l => string.Equals(l.Relativo, e.Key, StringComparison.OrdinalIgnoreCase))
                    .Any(l => ComparacionLiteralRol.IsMatch(l.Linea)
                              && (e.Value.Permitido == null || e.Value.Permitido.IsMatch(l.Linea)));
                if (!necesaria) sobrantes.Add(e.Key);
            }

            Assert.True(sobrantes.Count == 0,
                "Estas excepciones ya no tienen coincidencias; quítalas de ExcepcionesLiteralesRol:" +
                Environment.NewLine + string.Join(Environment.NewLine, sobrantes));
        }

        // ---------- Data API Builder retirado (E1-8, 2026-11-19) ----------

        // En la raíz: dab-config*.json (exponía la BD por REST/GraphQL, Usuarios incluido) y .env/.env.*
        // (la cadena de conexión que leía DAB). Los locales siguen gitignorados; aquí se mira lo VERSIONADO.
        private static readonly Regex ArchivoDabEnRaiz = new Regex(
            @"^(dab-config[^/]*\.json|\.env(\.[^/]*)?)$", RegexOptions.IgnoreCase);

        private static List<string> ArchivosVersionados(string raizRepo)
        {
            var info = new ProcessStartInfo("git", "ls-files -z")
            {
                WorkingDirectory = raizRepo,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            Process proceso;
            try
            {
                proceso = Process.Start(info);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // Sin git la prueba debe fallar (no omitirse): en CI tiene que verse.
                throw new InvalidOperationException(
                    "git no está en el PATH: instálalo o agrégalo para correr esta prueba.", ex);
            }

            using (proceso)
            {
                // Las dos salidas en paralelo: leer una entera antes de la otra puede bloquearse
                // si git llena el búfer de la que nadie está leyendo.
                var salida = proceso.StandardOutput.ReadToEndAsync();
                var errores = proceso.StandardError.ReadToEndAsync();
                if (!proceso.WaitForExit(30000))
                {
                    try { proceso.Kill(); } catch (InvalidOperationException) { }
                    throw new TimeoutException("git ls-files no terminó en 30 s.");
                }
                Assert.True(proceso.ExitCode == 0, "git ls-files falló: " + errores.Result);
                return salida.Result.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }

        [Fact]
        public void Repo_NoVersionaDataApiBuilderNiEnv()
        {
            string raizRepo = BaseDePruebas.RaizRepo;
            Assert.NotNull(raizRepo);
            List<string> versionados = ArchivosVersionados(raizRepo);
            Assert.True(versionados.Count > 100, "git ls-files no devolvió los archivos del repo.");

            List<string> hallazgos = versionados.Where(a => ArchivoDabEnRaiz.IsMatch(a)).ToList();

            // .env.example no está en .gitignore: también falla si solo existe en disco (aún sin commit).
            if (File.Exists(Path.Combine(raizRepo, ".env.example")) && !hallazgos.Contains(".env.example"))
                hallazgos.Add(".env.example (en disco)");

            Assert.True(hallazgos.Count == 0,
                "Data API Builder se retiró del repo (E1-8): no versiones su configuración ni un .env. " +
                "Para consultar la BD usa WebSGV/Database/sql.ps1:" +
                Environment.NewLine + string.Join(Environment.NewLine, hallazgos));
        }

        [Theory]
        [InlineData("dab-config.json", true)]
        [InlineData("dab-config.production.json", true)]
        [InlineData(".env", true)]
        [InlineData(".env.example", true)]
        [InlineData("docs/dab-config.json", false)]
        [InlineData("WebSGV/Web.config", false)]
        [InlineData(".envrc", false)]
        public void ArchivoDabEnRaiz_DistingueLoRetirado(string ruta, bool esperado)
        {
            Assert.Equal(esperado, ArchivoDabEnRaiz.IsMatch(ruta));
        }

        [Fact]
        public void CodigoFuente_NoUsaDebugTraceNiConsole()
        {
            string raiz = RaizWeb();
            Assert.True(ArchivosFuente(raiz).Count() > 100, "No se encontraron los fuentes de WebSGV.");

            List<string> hallazgos = BuscarLlamadas(raiz);

            Assert.True(hallazgos.Count == 0,
                "Usa LogSGV.Info/Advertencia/Error en lugar de Debug/Trace/Console " +
                "(o MensajeErrorHelper.ParaUsuario(ex), que ya registra el error):" +
                Environment.NewLine + string.Join(Environment.NewLine, hallazgos));
        }
    }
}
