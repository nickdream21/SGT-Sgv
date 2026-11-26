using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Un solo envío a la vez (E5-2). SiteMaster.js bloquea la página mientras corre un postback
    /// ("Procesando…") y la libera al volver la respuesta; un botón que devuelve un archivo no recarga la
    /// página, así que lleva <c>data-descarga</c> y se libera a los 3 s; un envío largo lleva
    /// <c>data-espera</c> (segundos). Estas pruebas leen los fuentes como texto: (a) todo botón de descarga
    /// lleva la marca, (b) los envíos largos llevan su espera y (c) no quedan bloqueos propios de página
    /// (deshabilitar el botón) repetidos con el global.
    /// </summary>
    public class EnvioUnicoTests
    {
        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Relativo(string raiz, string archivo) =>
            archivo.Substring(raiz.Length).TrimStart('\\', '/').Replace('\\', '/');

        private static string Leer(string raiz, string relativo) =>
            File.ReadAllText(Path.Combine(raiz, relativo.Replace('/', Path.DirectorySeparatorChar)));

        /// <summary>Páginas, controles de usuario y maestras (Views y la raíz del sitio).</summary>
        private static IEnumerable<string> ArchivosDeMarcado(string raiz) =>
            new[] { "*.aspx", "*.ascx", "*.Master" }
                .SelectMany(ext => Directory.GetFiles(Path.Combine(raiz, "Views"), ext, SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(raiz, ext, SearchOption.TopDirectoryOnly)));

        /// <summary>
        /// Etiquetas de apertura &lt;asp:...&gt; (pueden ocupar varias líneas). Los valores entre comillas y
        /// los bloques &lt;% %&gt; pueden llevar '&gt;': se saltan enteros.
        /// </summary>
        private static IEnumerable<string> EtiquetasAsp(string texto)
        {
            int i = 0;
            while ((i = texto.IndexOf("<asp:", i, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                int j = i + 5;
                char comilla = '\0';
                while (j < texto.Length)
                {
                    if (comilla == '\0' && string.CompareOrdinal(texto, j, "<%", 0, 2) == 0)
                    {
                        int fin = texto.IndexOf("%>", j + 2, StringComparison.Ordinal);
                        j = fin < 0 ? texto.Length : fin + 2;
                        continue;
                    }
                    char c = texto[j];
                    if (comilla != '\0') { if (c == comilla) comilla = '\0'; }
                    else if (c == '"' || c == '\'') comilla = c;
                    else if (c == '>') break;
                    j++;
                }
                yield return texto.Substring(i, Math.Min(j + 1, texto.Length) - i);
                i = j;
            }
        }

        // ── (a) Descargas por postback ─────────────────────────────────────────

        /// <summary>
        /// Lista cerrada: cada control de servidor (página, atributo que lo identifica) cuyo manejador
        /// escribe un archivo al Response. Un botón de descarga nuevo se agrega aquí y lleva data-descarga.
        /// </summary>
        private static readonly (string Pagina, string Control, string Manejador)[] Descargas =
        {
            ("Views/Auditoria.aspx", "OnClick=\"btnExportarExcel_Click\"", "btnExportarExcel_Click"),
            ("Views/Exportacion/ImportarSeguimiento.aspx", "OnClick=\"btnDescargarPlantilla_Click\"", "btnDescargarPlantilla_Click"),
            ("Views/Exportacion/RegistroSeguimiento.aspx", "OnClick=\"btnExportarStatus_Click\"", "btnExportarStatus_Click"),
            ("Views/Exportacion/RegistroSeguimiento.aspx", "OnClick=\"btnImagenStatus_Click\"", "btnImagenStatus_Click"),
            ("Views/Exportacion/RegistroSeguimiento.aspx", "OnClick=\"btnExportar_Click\"", "btnExportar_Click"),
            ("Views/Exportacion/RegistroSeguimiento.aspx", "OnClick=\"btnExportarStatusExtra_Click\"", "btnExportarStatusExtra_Click"),
            ("Views/Exportacion/RegistroSeguimiento.aspx", "OnClick=\"btnImagenStatusExtra_Click\"", "btnImagenStatusExtra_Click"),
            ("Views/Exportacion/RegistroSeguimiento.aspx", "OnClick=\"btnExportarGrifo_Click\"", "btnExportarGrifo_Click"),
            ("Views/Exportacion/RegistroSeguimiento.aspx", "OnClick=\"btnImagenGrifo_Click\"", "btnImagenGrifo_Click"),
            ("Views/Exportacion/RegistroSeguimiento.aspx", "OnClick=\"btnExportarLlegadas_Click\"", "btnExportarLlegadas_Click"),
            ("Views/Exportacion/RegistroSeguimiento.aspx", "OnClick=\"btnImagenLlegadas_Click\"", "btnImagenLlegadas_Click"),
            ("Views/Exportacion/SeguimientoViaje.aspx", "OnClick=\"btnExportar_Click\"", "btnExportar_Click"),
            ("Views/FacturasFlete.aspx", "OnClick=\"btnExcel_Click\"", "btnExcel_Click"),
            ("Views/ReporteCargas.aspx", "OnClick=\"btnExcel_Click\"", "btnExcel_Click"),
            ("Views/ReporteCargas.aspx", "OnClick=\"btnPdf_Click\"", "btnPdf_Click"),
            ("Views/ReporteGps.aspx", "OnClick=\"btnZip_Click\"", "btnZip_Click"),
            // LinkButton de cada fila de gvResultados: lo atiende gvResultados_RowCommand.
            ("Views/ReporteGps.aspx", "CommandName=\"Descargar\"", "gvResultados_RowCommand"),
            ("Views/SaldosConductores.aspx", "OnClick=\"btnExcel_Click\"", "btnExcel_Click"),
            ("Views/SaldosConductores.aspx", "OnClick=\"btnImagen_Click\"", "btnImagen_Click"),
        };

        /// <summary>Código de Views que manda archivos sin postback (no hay botón que marcar).</summary>
        private static readonly Dictionary<string, string> DescargasSinPostback =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Views/DescargarDocumento.aspx.cs"] = "enlace directo (GET) con la llave de sesión",
                ["Views/AyudaImagen.aspx.cs"] = "imagen de un tema de ayuda (GET desde el <img> del fragmento)",
                ["Views/DescargarPdfOrdenViaje.aspx.cs"] = "enlace directo (GET) al PDF archivado",
                ["Views/ReportesOrdenesViaje.aspx.cs"] = "exporta por GET (?action=...) desde ReportesOrdenesViaje.js",
                ["Views/Exportacion/GpsAutomatico.ashx.cs"] = "manejador JSON, no es una página",
            };

        private static readonly Regex EscribeArchivo = new Regex(
            @"Content-Disposition|Response\.(?:BinaryWrite|TransmitFile|WriteFile|OutputStream)|\.ContentType\s*=",
            RegexOptions.IgnoreCase);

        // Lo que escribe un archivo al Response (sin ".ContentType =": un JSON también lo pone).
        private static readonly Regex DescargaEnCuerpo = new Regex(
            @"Content-Disposition|\.(?:BinaryWrite|TransmitFile|WriteFile)\s*\(|\.OutputStream\b",
            RegexOptions.IgnoreCase);

        private static readonly Regex InicioMetodo = new Regex(
            @"\b(?:void|string|bool|int|byte\[\])\s+(\w+)\s*\([^)]*\)\s*(=>|\{)");

        private static readonly Regex Llamada = new Regex(@"\b(\w+)\s*\(");

        private static readonly Regex MarcaDescarga = new Regex(@"\sdata-descarga\s*=", RegexOptions.IgnoreCase);

        /// <summary>Cuerpo de cada método de la clase (por nombre; las sobrecargas se juntan).</summary>
        private static Dictionary<string, string> Metodos(string codigo)
        {
            var metodos = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match m in InicioMetodo.Matches(codigo))
            {
                int inicio = m.Index + m.Length, fin;
                if (m.Groups[2].Value == "=>")
                    fin = codigo.IndexOf(';', inicio);
                else
                {
                    int nivel = 1;
                    fin = inicio;
                    while (fin < codigo.Length && nivel > 0)
                    {
                        if (codigo[fin] == '{') nivel++;
                        else if (codigo[fin] == '}') nivel--;
                        fin++;
                    }
                }
                string cuerpo = codigo.Substring(inicio, Math.Max(0, fin - inicio));
                string nombre = m.Groups[1].Value;
                metodos[nombre] = metodos.TryGetValue(nombre, out string previo) ? previo + Environment.NewLine + cuerpo : cuerpo;
            }
            return metodos;
        }

        /// <summary>
        /// Manejadores de eventos (*_Click, *_Command, *_RowCommand, *_ItemCommand) cuyo cuerpo, o un método
        /// de la misma clase al que llaman (directa o indirectamente), escribe un archivo al Response.
        /// </summary>
        private static HashSet<string> ManejadoresQueDescargan(string codigo)
        {
            var metodos = Metodos(codigo);
            var descargan = new HashSet<string>(
                metodos.Where(m => DescargaEnCuerpo.IsMatch(m.Value)).Select(m => m.Key), StringComparer.Ordinal);
            bool cambio = true;
            while (cambio)
            {
                cambio = false;
                foreach (var m in metodos)
                {
                    if (descargan.Contains(m.Key)) continue;
                    if (Llamada.Matches(m.Value).Cast<Match>().Any(l => l.Groups[1].Value != m.Key && descargan.Contains(l.Groups[1].Value)))
                    {
                        descargan.Add(m.Key);
                        cambio = true;
                    }
                }
            }
            return new HashSet<string>(
                descargan.Where(n => Regex.IsMatch(n, @"_(?:Click|Command|RowCommand|ItemCommand)$")), StringComparer.Ordinal);
        }

        [Fact]
        public void LosManejadoresDeDescargaSiguenExistiendoYSonLosQueDescargan()
        {
            string raiz = RaizWeb();
            var faltan = new List<string>();
            foreach (var d in Descargas)
            {
                string codigo = Leer(raiz, d.Pagina + ".cs");
                if (!Regex.IsMatch(codigo, @"\bvoid\s+" + Regex.Escape(d.Manejador) + @"\s*\("))
                    faltan.Add(d.Pagina + ".cs: no está " + d.Manejador);
                else if (!ManejadoresQueDescargan(codigo).Contains(d.Manejador))
                    faltan.Add(d.Pagina + ".cs: " + d.Manejador + " ya no escribe un archivo al Response (¿se fue la descarga?)");
            }
            Assert.True(faltan.Count == 0, "Lista de descargas desactualizada:\n" + string.Join("\n", faltan));
        }

        [Fact]
        public void TodoManejadorQueDescargaEstaEnLaLista()
        {
            // Un segundo botón de descarga en una página que ya está en la lista también tiene que entrar.
            string raiz = RaizWeb();
            var listados = new HashSet<string>(Descargas.Select(d => d.Pagina + ".cs#" + d.Manejador), StringComparer.OrdinalIgnoreCase);
            // Páginas y controles (.ascx.cs): un manejador de descarga movido a un control también entra (E6-4).
            var nuevos = FuentesWeb.CodigoDeMarcado(Path.Combine(raiz, "Views"))
                .SelectMany(f => ManejadoresQueDescargan(File.ReadAllText(f)).Select(m => Relativo(raiz, f) + "#" + m))
                .Where(x => !listados.Contains(x))
                .ToList();
            Assert.True(nuevos.Count == 0,
                "Manejadores que devuelven un archivo y no están en Descargas (agregue su control con data-descarga):\n" +
                string.Join("\n", nuevos));
        }

        [Fact]
        public void ElDetectorDeDescargasSigueLasLlamadasDentroDeLaClase()
        {
            const string codigo = @"
                protected void btnA_Click(object s, EventArgs e) => Exportar(true);
                protected void btnB_Click(object s, EventArgs e) { Cargar(); }
                protected void gv_RowCommand(object s, GridViewCommandEventArgs e) { if (x) { Enviar(new byte[0]); } }
                private void Exportar(bool pdf) { var b = Armar(); Enviar(b); }
                private void Cargar() { lbl.Text = ""{0}""; }
                private static void Enviar(byte[] c) { var r = HttpContext.Current.Response; r.AddHeader(""Content-Disposition"", ""x""); r.BinaryWrite(c); }";
            var m = ManejadoresQueDescargan(codigo);
            Assert.Contains("btnA_Click", m);
            Assert.Contains("gv_RowCommand", m);
            Assert.DoesNotContain("btnB_Click", m);
        }

        [Fact]
        public void TodoBotonDeDescargaLlevaDataDescarga()
        {
            string raiz = RaizWeb();
            var malos = new List<string>();
            foreach (var d in Descargas)
            {
                var etiquetas = EtiquetasAsp(Leer(raiz, d.Pagina)).Where(t => t.Contains(d.Control)).ToList();
                if (etiquetas.Count == 0) malos.Add(d.Pagina + ": no hay control con " + d.Control);
                foreach (string t in etiquetas.Where(t => !MarcaDescarga.IsMatch(t)))
                    malos.Add(d.Pagina + ": " + d.Control + " sin data-descarga=\"true\"");
            }
            Assert.True(malos.Count == 0,
                "Un botón que devuelve un archivo no recarga la página: sin data-descarga queda \"Procesando…\" 20 s.\n" +
                string.Join("\n", malos));
        }

        [Fact]
        public void SoloLosBotonesDeDescargaLlevanDataDescarga()
        {
            // data-descarga libera a los 3 s: en un botón que no descarga dejaría pasar un segundo envío.
            string raiz = RaizWeb();
            var permitidos = Descargas.ToLookup(d => d.Pagina, d => d.Control, StringComparer.OrdinalIgnoreCase);
            var sobran = new List<string>();
            foreach (string archivo in ArchivosDeMarcado(raiz))
            {
                string rel = Relativo(raiz, archivo);
                foreach (string t in EtiquetasAsp(File.ReadAllText(archivo)).Where(t => MarcaDescarga.IsMatch(t)))
                    if (!permitidos[rel].Any(t.Contains))
                        sobran.Add(rel + ": " + t.Split('\n')[0].Trim());
            }
            Assert.True(sobran.Count == 0, "data-descarga fuera de la lista de descargas:\n" + string.Join("\n", sobran));
        }

        [Fact]
        public void TodaPaginaQueEscribeUnArchivoEstaEnLaLista()
        {
            string raiz = RaizWeb();
            var paginas = new HashSet<string>(Descargas.Select(d => d.Pagina + ".cs"), StringComparer.OrdinalIgnoreCase);
            var nuevas = Directory.GetFiles(Path.Combine(raiz, "Views"), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase))
                .Where(f => EscribeArchivo.IsMatch(File.ReadAllText(f)))
                .Select(f => Relativo(raiz, f))
                .Where(rel => !paginas.Contains(rel) && !DescargasSinPostback.ContainsKey(rel))
                .ToList();
            Assert.True(nuevas.Count == 0,
                "Código que manda archivos y no está en Descargas ni en DescargasSinPostback (agregue su botón con data-descarga):\n" +
                string.Join("\n", nuevas));
            foreach (string rel in DescargasSinPostback.Keys)
                Assert.True(File.Exists(Path.Combine(raiz, rel.Replace('/', Path.DirectorySeparatorChar))), rel + " ya no existe: quítelo de la lista");
        }

        [Fact]
        public void ElEscanerDeEtiquetasSaltaLosBloquesDeServidor()
        {
            var t = EtiquetasAsp("<asp:LinkButton ID=\"x\" CommandArgument='<%# Container.DataItemIndex %>' CommandName=\"Descargar\" data-descarga=\"true\">texto</asp:LinkButton>").Single();
            Assert.Contains("CommandName=\"Descargar\"", t);
            Assert.Matches(MarcaDescarga, t);
            Assert.EndsWith("\">", t);
        }

        // ── (b) Envíos largos: data-espera ─────────────────────────────────────

        /// <summary>
        /// Lista cerrada de controles con data-espera (segundos) y el rango que deben tener. Sin la marca,
        /// el bloqueo se libera a los 20 s y un segundo clic vuelve a enviar mientras el primero sigue en el
        /// servidor (subidas, lectura de documentos, GPS, la firma del conductor). Cerrar sesión lleva 2 s: si
        /// el usuario cancela el "¿salir del sitio?", la página vuelve a responder enseguida.
        /// </summary>
        private static readonly (string Pagina, string Id, int Minimo, int Maximo)[] Esperas =
        {
            ("Views/DashboardConductor.aspx", "btnEnviarLiquidacion", 60, 600),
            ("Views/FacturasFlete.aspx", "btnSubir", 60, 600),
            ("Views/Exportacion/ImportarSeguimiento.aspx", "btnImportar", 60, 600),
            ("Views/MovimientosViaje.aspx", "btnRegistrar", 60, 600),
            ("Views/RegistroDespacho.aspx", "btnLeerDocumentos", 60, 600),
            ("Views/Controles/Despachos/DescargaPedido.ascx", "btnLeerProgramacionDescarga", 60, 600),
            ("Views/Controles/Despachos/ConfiguracionBaseLote.ascx", "btnIniciarLote", 60, 600),
            ("Views/BandejaDocumentos.aspx", "btnLeer", 60, 600),
            ("Views/BandejaDocumentos.aspx", "btnRegistrar", 60, 600),
            ("Views/ReporteGps.aspx", "btnGenerar", 60, 600),
            ("Views/Exportacion/RegistroSeguimiento.aspx", "btnActualizarGps", 60, 600),
            ("Site.Master", "btnCerrarSesion", 1, 5),
        };

        private static readonly Regex AtributoEspera = new Regex(@"\sdata-espera\s*=\s*""(\d+)""", RegexOptions.IgnoreCase);
        private static readonly Regex IdDeEtiqueta = new Regex(@"\sID\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);

        [Fact]
        public void LosEnviosLargosLlevanDataEspera()
        {
            string raiz = RaizWeb();
            var malos = new List<string>();
            foreach (var x in Esperas)
            {
                var etiqueta = EtiquetasAsp(Leer(raiz, x.Pagina))
                    .FirstOrDefault(t => IdDeEtiqueta.Match(t).Groups[1].Value == x.Id);
                if (etiqueta == null) { malos.Add(x.Pagina + ": no está " + x.Id); continue; }
                var m = AtributoEspera.Match(etiqueta);
                int segundos = m.Success ? int.Parse(m.Groups[1].Value) : 0;
                if (segundos < x.Minimo || segundos > x.Maximo)
                    malos.Add(x.Pagina + ": " + x.Id + " con data-espera=" + (m.Success ? m.Groups[1].Value : "(sin marca)") +
                              " (debe estar entre " + x.Minimo + " y " + x.Maximo + ")");
            }
            Assert.True(malos.Count == 0, "Envíos largos sin su data-espera:\n" + string.Join("\n", malos));
        }

        [Fact]
        public void SoloLosControlesDeLaListaLlevanDataEspera()
        {
            string raiz = RaizWeb();
            var permitidos = new HashSet<string>(Esperas.Select(x => x.Pagina + "#" + x.Id), StringComparer.OrdinalIgnoreCase);
            var sobran = new List<string>();
            foreach (string archivo in ArchivosDeMarcado(raiz))
            {
                string rel = Relativo(raiz, archivo);
                foreach (string t in EtiquetasAsp(File.ReadAllText(archivo)).Where(t => AtributoEspera.IsMatch(t)))
                {
                    var id = IdDeEtiqueta.Match(t);
                    if (!permitidos.Contains(rel + "#" + (id.Success ? id.Groups[1].Value : "?")))
                        sobran.Add(rel + ": " + t.Split('\n')[0].Trim());
                }
            }
            Assert.True(sobran.Count == 0, "data-espera fuera de la lista Esperas:\n" + string.Join("\n", sobran));
        }

        // ── (c) Bloqueos propios ───────────────────────────────────────────────

        private static readonly Regex Deshabilita = new Regex(
            @"\.disabled\s*=\s*true|\.prop\(\s*['""]disabled['""]\s*,\s*true|\.attr\(\s*['""]disabled['""]|setAttribute\(\s*['""]disabled['""]",
            RegexOptions.IgnoreCase);

        /// <summary>
        /// Lista cerrada de botones que se deshabilitan en los scripts de página, con su motivo: ninguno es
        /// un postback (fetch, $.ajax / PageMethods o estado del formulario). Para un postback no se
        /// deshabilita nada: lo frena SiteMaster.js, y un submit deshabilitado no envía su nombre.
        /// Clave: archivo | línea recortada; valor: cuántas veces aparece y por qué.
        /// </summary>
        private static readonly Dictionary<string, (int Veces, string Motivo)> DeshabilitarPermitido =
            new Dictionary<string, (int, string)>(StringComparer.Ordinal)
            {
                ["ConformarLiquidacion.js|btnFirmar.disabled = true;"] = (1, "firma por PageMethod (RegistrarConformidad)"),
                ["ConformarLiquidacion.js|btn.disabled = true;"] = (1, "observación por PageMethod (ObservarLiquidacion)"),
                ["DashboardConductor.js|$btn.prop('disabled', true).text('Retirando...');"] = (1, "$.ajax RetirarLiquidacion"),
                ["Exportacion/RegistroSeguimiento.js|boton.disabled = true;"] = (1, "fetch del GPS en segundo plano"),
                ["FirmarLiquidacion.js|btnFirmar.disabled = true;"] = (2, "sin orden válida y firma por $.ajax (RegistrarFirmaConductor)"),
                ["LiquidacionesPendientes.js|$btn.prop('disabled', true).html('<i class=\"fas fa-spinner fa-spin\"></i> Consultando...');"] = (1, "$.ajax hora de llegada GPS"),
                ["LiquidacionesPendientes.js|$btn.prop('disabled', true).html('<i class=\"fas fa-spinner fa-spin mr-1\"></i>Guardando...');"] = (1, "$.ajax corrección de salida"),
                ["LiquidacionesPendientes.js|$btn.prop('disabled', true).html('<i class=\"fas fa-spinner fa-spin mr-1\"></i>Enviando...');"] = (1, "$.ajax aprobación"),
                ["LiquidacionesPendientes.js|$btn.prop('disabled', true).html('<i class=\"fas fa-spinner fa-spin mr-1\"></i>Procesando...');"] = (2, "$.ajax rechazo y reversión"),
                ["RegistroChoferes.js|if (boton) boton.disabled = true;"] = (1, "$.ajax BuscarPorDNI"),
                ["RegistroClientes.js|if (boton) boton.disabled = true;"] = (1, "$.ajax BuscarPorRuc"),
            };

        private static Dictionary<string, int> BloqueosPropios(string raiz)
        {
            string carpeta = Path.Combine(raiz, "Scripts", "paginas");
            var encontrados = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string archivo in Directory.GetFiles(carpeta, "*.js", SearchOption.AllDirectories))
            {
                string rel = Relativo(carpeta, archivo);
                if (rel.Equals("SiteMaster.js", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (string linea in File.ReadAllLines(archivo))
                {
                    string t = linea.Trim();
                    if (t.StartsWith("//") || !Deshabilita.IsMatch(t)) continue;
                    string clave = rel + "|" + t;
                    encontrados[clave] = encontrados.TryGetValue(clave, out int n) ? n + 1 : 1;
                }
            }
            return encontrados;
        }

        [Fact]
        public void NoHayBloqueosPropiosDeDobleEnvioFueraDeSiteMaster()
        {
            var encontrados = BloqueosPropios(RaizWeb());
            var nuevos = encontrados
                .Where(e => !DeshabilitarPermitido.TryGetValue(e.Key, out var p) || e.Value > p.Veces)
                .Select(e => e.Key + " (" + e.Value + " vez/veces)")
                .ToList();
            Assert.True(nuevos.Count == 0,
                "Botón deshabilitado en un script de página: si es un postback, quítelo (el doble envío lo frena " +
                "SiteMaster.js; use data-procesando para el texto). Si es fetch o PageMethod, agréguelo a " +
                "DeshabilitarPermitido con su motivo.\n" + string.Join("\n", nuevos));

            var sobran = DeshabilitarPermitido
                .Where(p => !encontrados.TryGetValue(p.Key, out int n) || n < p.Value.Veces)
                .Select(p => p.Key)
                .ToList();
            Assert.True(sobran.Count == 0, "Excepciones que ya no existen (quítelas de la lista):\n" + string.Join("\n", sobran));
        }

        [Theory]
        [InlineData("setTimeout(function () { b.disabled = true; b.value = texto; }, 0);")]
        [InlineData("$(btn).prop('disabled', true).val('Enviando...');")]
        [InlineData("$('#x').attr('disabled', 'disabled');")]
        [InlineData("boton.setAttribute(\"disabled\", \"\");")]
        public void ElPatronReconoceLosBloqueosQueSeQuitaron(string linea) =>
            Assert.Matches(Deshabilita, linea);

        [Fact]
        public void SiteMasterBloqueaSinDeshabilitarYCubreLasTresVias()
        {
            string js = Leer(RaizWeb(), "Scripts/paginas/SiteMaster.js");
            Assert.DoesNotMatch(Deshabilita, js);
            Assert.Contains("document.addEventListener('submit'", js);
            Assert.Contains("form.submit = function", js);
            Assert.Contains("add_initializeRequest", js);
            Assert.Contains("add_endRequest", js);
            Assert.Contains("'pageshow'", js);
            Assert.Contains("data-descarga", js);
            // Postback completo con archivos elegidos: 300 s (no los 20 s de seguridad).
            Assert.Contains("ESPERA_SUBIDA = 300000", js);
            Assert.Contains("hayArchivos(document.forms[0])", js);
            // La × de los avisos funciona durante el bloqueo; la región que anuncia "Procesando…" vive siempre en el árbol.
            Assert.Contains("closest('#sgvAvisos')", js);
            Assert.Contains("estado.setAttribute('role', 'status')", js);
            // El listener que anula los clics se registra antes que el freno de montos (también en window):
            // en el mismo destino y fase, corre primero el que se registró primero.
            int anula = js.IndexOf("if (bloqueado) { if (!enAvisos(e)) anular(e); return; }", StringComparison.Ordinal);
            int montos = js.IndexOf("var boton = botonQueEnvia(e);", StringComparison.Ordinal);
            Assert.True(anula > 0 && montos > anula, "El bloqueo de clics debe registrarse antes que el de montos.");
        }

        /// <summary>
        /// M-131 (punto 1, E6-26): la guardia "¿salir?" de RegistroDespacho con un lote a medio armar no pregunta durante
        /// un postback completo (la página se va a recargar y el lote sigue en el servidor), pero sí durante uno parcial
        /// (UpdatePanel): la página se queda y cerrar la pestaña o F5 perdería lo que no se guardó.
        /// </summary>
        [Fact]
        public void LaGuardiaDelLote_SoloCallaDuranteUnPostbackCompleto()
        {
            string raiz = RaizWeb();
            string sitio = Leer(raiz, "Scripts/paginas/SiteMaster.js");
            Assert.Contains("var bloqueado = false, completo = false,", sitio);
            string bloquear = sitio.Substring(sitio.IndexOf("function bloquear(el, asincrono)", StringComparison.Ordinal));
            bloquear = bloquear.Substring(0, bloquear.IndexOf("\n    }", StringComparison.Ordinal));
            Assert.True(bloquear.IndexOf("bloqueado = true;", StringComparison.Ordinal) < bloquear.IndexOf("completo = !asincrono;", StringComparison.Ordinal));
            string liberar = sitio.Substring(sitio.IndexOf("function liberar()", StringComparison.Ordinal));
            liberar = liberar.Substring(0, liberar.IndexOf("\n    }", StringComparison.Ordinal));
            Assert.Contains("completo = false;", liberar);
            Assert.Contains("SGV.envioCompletoEnCurso = function () { return bloqueado && completo; };", sitio);
            // envioEnCurso sigue igual para las demás páginas (DashboardConductor).
            Assert.Contains("SGV.envioEnCurso = function () { return bloqueado; };", sitio);

            string js = Leer(raiz, "Scripts/paginas/RegistroDespacho.js");
            string guardia = js.Substring(js.IndexOf("function _guardiaLote(e)", StringComparison.Ordinal));
            guardia = guardia.Substring(0, guardia.IndexOf("\n}", StringComparison.Ordinal));
            Assert.Contains("if (SGV.envioCompletoEnCurso && SGV.envioCompletoEnCurso() && !SGV.sesionCerrandose) return;", guardia);
            Assert.DoesNotContain("SGV.envioEnCurso", guardia);
        }
    }
}
