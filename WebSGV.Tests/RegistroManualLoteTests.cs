using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-27 (M-088): el registro manual de documentos de un lote deja de ser la vista "?manual=1" de ListaDespachos y pasa
    /// a <c>Views/RegistroManualLote.aspx?lote=</c>, con sus reglas puras en <c>RegistroManualLoteReglas</c> y los archivos en
    /// <c>ArchivosRegistroManual</c>. El enlace viejo redirige; los topes de la tarea: ListaDespachos.aspx.cs ≤ 1 450 líneas
    /// y su .aspx ≤ 700, la página nueva ≤ 700 y ningún método de más de 60 en ella ni en los dos servicios.
    /// </summary>
    public class RegistroManualLoteTests
    {
        private static string Ruta(params string[] ruta) => Path.Combine(FuentesWeb.RaizWeb(), Path.Combine(ruta));
        private static string Leer(params string[] ruta) => File.ReadAllText(Ruta(ruta));
        private static int Lineas(params string[] ruta) => TamanoCodigoTests.ContarLineas(Leer(ruta));
        private static string Marcado(string pagina) => AccesibilidadMarcadoTests.SinComentarios(Leer("Views", pagina + ".aspx"));

        /// <summary>Cuerpo del método cuya firma empieza con <paramref name="firma"/> (de su primera llave a la que la cierra).</summary>
        private static string Cuerpo(string cs, string firma)
        {
            int i = cs.IndexOf(firma, StringComparison.Ordinal);
            Assert.True(i >= 0, "No está " + firma);
            int ini = cs.IndexOf('{', i), nivel = 0;
            for (int j = ini; j < cs.Length; j++)
            {
                if (cs[j] == '{') nivel++;
                else if (cs[j] == '}' && --nivel == 0) return cs.Substring(ini, j - ini + 1);
            }
            throw new InvalidOperationException("Llaves sin cerrar en " + firma);
        }

        // ── URL de la página ────────────────────────────────────────────────────────────

        [Fact]
        public void Url_CodificaElLote_YAbrePorDespachoSiSePide()
        {
            Assert.Equal("~/Views/RegistroManualLote.aspx?lote=1_4400134786_2026-11-11_CARGA_0_2",
                RegistroManualLoteReglas.Url("1_4400134786_2026-11-11_CARGA_0_2"));
            Assert.Equal("~/Views/RegistroManualLote.aspx?lote=a%26b%3dc+d&despachos=1", RegistroManualLoteReglas.Url("a&b=c d", porDespacho: true));
            Assert.Equal("~/Views/RegistroManualLote.aspx?lote=", RegistroManualLoteReglas.Url(null));
        }

        [Theory]
        [InlineData(ErrorAplicacionHelper.AvisoPaginaVencida, true)]
        [InlineData(ErrorAplicacionHelper.AvisoArchivoGrande, true)]
        [InlineData("otro", false)]
        [InlineData("PAGINA_VENCIDA", false)]
        [InlineData("pagina_vencida&x=1", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Url_SoloReenviaLosDosAvisosDeVuelta(string aviso, bool reenvia)
        {
            string url = RegistroManualLoteReglas.Url("L1", false, aviso);
            Assert.Equal(reenvia ? "~/Views/RegistroManualLote.aspx?lote=L1&aviso=" + aviso : "~/Views/RegistroManualLote.aspx?lote=L1", url);
        }

        // ── El enlace viejo y los que llevan a la página ──────────────────────────────────

        [Fact]
        public void ListaDespachos_ManualUnoRedirige_FueraDelTryYTrasLasGuardas()
        {
            string cs = Leer("Views", "ListaDespachos.aspx.cs");
            string carga = Cuerpo(cs, "protected void Page_Load(");
            int guardas = carga.IndexOf("SecurityHelper.AgregarHeadersSeguridad();", StringComparison.Ordinal);
            int redirige = carga.IndexOf("RedirigirAlRegistroManual();", StringComparison.Ordinal);
            int intento = Regex.Match(carga, @"\n\s*try\s*\r?\n").Index;
            Assert.True(guardas > 0 && redirige > guardas && intento > redirige, "La redirección va tras las guardas y antes del try");
            Assert.Contains("if (!Page.IsPostBack && Request.QueryString[\"manual\"] == \"1\")", carga);

            string metodo = Cuerpo(cs, "private void RedirigirAlRegistroManual(");
            Assert.Contains("Response.Redirect(ResolveUrl(RegistroManualLoteReglas.Url(Request.QueryString[\"lote\"], false, Request.QueryString[\"aviso\"])), true)", cs);
            Assert.DoesNotContain("MostrarManifiestosLote", cs);
            Assert.NotNull(metodo);
        }

        [Fact]
        public void NingunEnlaceArmaYaElManualUnoDeListaDespachos()
        {
            // Fuera de la ayuda (Ayuda.aspx?manual=1 es el manual completo) y sin contar comentarios.
            var casos = new List<string>();
            foreach (string carpeta in new[] { "Views", "Services", "Helpers", "Scripts" + Path.DirectorySeparatorChar + "paginas" })
                foreach (string f in Directory.GetFiles(Ruta(carpeta), "*.*", SearchOption.AllDirectories)
                             .Where(f => Regex.IsMatch(f, @"\.(aspx|ascx|cs|js)$") && !Path.GetFileName(f).StartsWith("Ayuda", StringComparison.Ordinal)))
                {
                    string texto = Regex.IsMatch(f, @"\.as[pc]x$") ? AccesibilidadMarcadoTests.SinComentarios(File.ReadAllText(f)) : File.ReadAllText(f);
                    foreach (string linea in texto.Split('\n'))
                    {
                        string l = linea.Trim();
                        if (l.StartsWith("//", StringComparison.Ordinal) || l.StartsWith("*", StringComparison.Ordinal)) continue;
                        if (l.Contains("manual=1")) casos.Add(FuentesWeb.Relativo(FuentesWeb.RaizWeb(), f) + ": " + l);
                    }
                }
            Assert.True(casos.Count == 0, "Todavía arman ?manual=1:\n" + string.Join("\n", casos));

            Assert.Contains("RegistroManualLoteReglas.Url(e.IdLote)", Leer("Views", "BandejaDocumentos.aspx.cs"));
            Assert.Contains("hlIrDocsPorDespacho.NavigateUrl = RegistroManualLoteReglas.Url(idLoteVirtual, porDespacho: true);", Leer("Views", "ListaDespachos.aspx.cs"));
            Assert.Contains("<asp:HyperLink ID=\"hlIrDocsPorDespacho\"", Marcado("ListaDespachos"));
        }

        [Fact]
        public void ListaDespachos_SinElRegistroManual()
        {
            string aspx = Marcado("ListaDespachos"), cs = Leer("Views", "ListaDespachos.aspx.cs");
            string js = Leer("Scripts", "paginas", "ListaDespachos.js"), css = Leer("Content", "paginas", "ListaDespachos.css");
            foreach (string texto in new[] { aspx, cs, js, css })
            {
                Assert.DoesNotContain("pnlManifiestosLote", texto);
                Assert.DoesNotContain("ld-docs-page", texto);
                Assert.DoesNotContain("lnkIrDocsPorDespacho", texto);
                Assert.DoesNotContain("btnVolverManifiestos", texto);
            }
            Assert.DoesNotContain("<asp:FileUpload", aspx);
            Assert.DoesNotContain("FileUpload", cs);
            Assert.DoesNotContain("iniciarDocumentosLote", js);
            Assert.DoesNotContain(".ld-doc-card", css);
        }

        // ── La página ───────────────────────────────────────────────────────────────────

        [Fact]
        public void Pagina_GuardaArribaDePageLoad_YAvisoSiElLoteNoEsta()
        {
            string cs = Leer("Views", "RegistroManualLote.aspx.cs");
            string carga = Cuerpo(cs, "protected void Page_Load(");
            Assert.StartsWith("{\n            SecurityHelper.ExigirRolAdmin();\n            SecurityHelper.AgregarHeadersSeguridad();", carga.Replace("\r\n", "\n"));
            Assert.DoesNotContain("try", carga);

            string abrir = Cuerpo(cs, "private void AbrirLote(");
            Assert.Contains("ListaDespachosReglas.EsIdLoteValido(idLote)", abrir);
            Assert.Contains("ListaDespachosService.ObtenerLotePorId(idLote) == null", abrir);
            Assert.Contains("pnlLoteNoDisponible.Visible = true;", abrir);
            Assert.Contains("Request.QueryString[\"despachos\"] == \"1\"", abrir);

            string aspx = Marcado("RegistroManualLote");
            Assert.Contains("El lote no existe, está anulado o cambió.", aspx);
            Assert.Matches(@"<asp:Panel ID=""pnlRegistroManual"" runat=""server"" Visible=""false"">", aspx);
            Assert.Contains("<asp:HyperLink ID=\"hlVerLote\"", aspx);
            Assert.Contains("hlVerLote.NavigateUrl = ResolveUrl(\"~/Views/ListaDespachos.aspx\") + \"?lote=\" + HttpUtility.UrlEncode(idLoteVirtual);", cs);
            Assert.Contains("data-ayuda=\"bandeja-documentos\"", aspx);
            Assert.DoesNotContain("Session[", cs);
            Assert.DoesNotContain("ex.Message", cs);
            Assert.Contains("RecursoHelper.Url(\"~/Content/paginas/RegistroManualLote.css\")", aspx);
            Assert.Contains("RecursoHelper.Url(\"~/Scripts/paginas/RegistroManualLote.js\")", aspx);
        }

        [Theory]
        [InlineData("ListaDespachos")]
        [InlineData("RegistroManualLote")]
        public void CadaControlIdDeLosTriggersExisteEnSuMarcado(string pagina)
        {
            string aspx = Marcado(pagina);
            string triggers = Regex.Match(aspx, @"<Triggers>(?<t>.*?)</Triggers>", RegexOptions.Singleline).Groups["t"].Value;
            var ids = Regex.Matches(triggers, @"ControlID=""(?<id>\w+)""").Cast<Match>().Select(m => m.Groups["id"].Value).ToList();
            Assert.NotEmpty(ids);
            foreach (string id in ids)
                Assert.True(Regex.IsMatch(aspx, @"\bID=""" + id + @""""), pagina + ": el trigger " + id + " no tiene control");
        }

        [Fact]
        public void LasSubidasHacenPostbackCompleto()
        {
            string aspx = Marcado("RegistroManualLote");
            foreach (string id in new[] { "btnSubirPdfFactura", "btnSubirPdfCpic", "rptDocumentosLote", "gvManifiestosLote" })
                Assert.Contains("<asp:PostBackTrigger ControlID=\"" + id + "\" />", aspx);
            Assert.DoesNotContain("btnVolverManifiestos", aspx);
            Assert.Contains("data-enter=\"<%= btnGuardarGuias.ClientID %>\"", aspx);
        }

        [Fact]
        public void WebConfigYCsproj()
        {
            string config = Leer("Web.config");
            Assert.Contains("<location path=\"Views/RegistroManualLote.aspx\">", config);
            Assert.DoesNotContain("<location path=\"Views/ListaDespachos.aspx\">", config);

            string csproj = Leer("WebSGV.csproj");
            foreach (string entrada in new[]
            {
                "<Content Include=\"Views\\RegistroManualLote.aspx\" />",
                "<Compile Include=\"Views\\RegistroManualLote.aspx.cs\">",
                "<Compile Include=\"Views\\RegistroManualLote.aspx.designer.cs\">",
                "<Content Include=\"Content\\paginas\\RegistroManualLote.css\" />",
                "<Content Include=\"Scripts\\paginas\\RegistroManualLote.js\" />",
                "<Compile Include=\"Services\\Despachos\\RegistroManualLoteReglas.cs\" />",
                "<Compile Include=\"Services\\Despachos\\ArchivosRegistroManual.cs\" />",
            })
                Assert.Contains(entrada, csproj);
            Assert.Matches(@"<Compile Include=""Views\\RegistroManualLote\.aspx\.cs"">\s*<DependentUpon>RegistroManualLote\.aspx</DependentUpon>\s*<SubType>ASPXCodeBehind</SubType>", csproj);
        }

        // ── "Asignar a los marcados" ───────────────────────────────────────────────────

        [Fact]
        public void LeerAsignacion_SinNumeros_PideAlMenosUno()
        {
            var a = RegistroManualLoteReglas.LeerAsignacion(" ", "", "", "", "", "");
            Assert.Equal(new[] { "Escriba el número de la factura, de la CPIC o de ambas." }, a.Errores);
        }

        [Theory]
        [InlineData("123456")]
        [InlineData("12345678")]
        [InlineData("12a4567")]
        public void LeerAsignacion_CpicSinSieteDigitos(string cpic)
        {
            var a = RegistroManualLoteReglas.LeerAsignacion("", "", "", cpic, "", "");
            Assert.Equal(new[] { "El número de CPIC debe tener 7 dígitos." }, a.Errores);
        }

        [Fact]
        public void LeerAsignacion_MontosInvalidos_DicenSuCampoEnOrden()
        {
            var a = RegistroManualLoteReglas.LeerAsignacion("f1", "", "12.345", "1234567", "", "-5");
            Assert.Equal(2, a.Errores.Count);
            Assert.Contains("Valor total de la factura", a.Errores[0]);
            Assert.Contains("Valor del flete", a.Errores[1]);
            Assert.Null(a.ValorFactura);
            Assert.Null(a.Flete);
        }

        [Fact]
        public void LeerAsignacion_SoloFactura()
        {
            var a = RegistroManualLoteReglas.LeerAsignacion("  f001-12 ", "2026-11-20", "1200,50", "", "", "");
            Assert.Empty(a.Errores);
            Assert.Equal("F001-12", a.Factura);
            Assert.Equal("", a.Cpic);
            Assert.Equal(new DateTime(2026, 11, 20), a.FechaFactura);
            Assert.Equal(1200.50m, a.ValorFactura);
            Assert.Null(a.Flete);
        }

        [Fact]
        public void LeerAsignacion_SoloCpic()
        {
            var a = RegistroManualLoteReglas.LeerAsignacion("", "", "", " 1234567 ", "2026-11-21", "");
            Assert.Empty(a.Errores);
            Assert.Equal("", a.Factura);
            Assert.Equal("1234567", a.Cpic);
            Assert.Equal(new DateTime(2026, 11, 21), a.FechaCpic);
            Assert.Null(a.FechaFactura);
            Assert.Null(a.ValorFactura);
        }

        [Fact]
        public void LeerAsignacion_FechaIlegible_EsSinFecha()
        {
            var a = RegistroManualLoteReglas.LeerAsignacion("F1", "no", "", "", "", "");
            Assert.Empty(a.Errores);
            Assert.Null(a.FechaFactura);
        }

        // ── Celdas de la tabla por despacho ─────────────────────────────────────────────

        [Fact]
        public void Celdas_CodificanElTexto()
        {
            string doc = RegistroManualLoteReglas.CeldaDocumento("<b>F1</b>", true);
            Assert.Contains("<span class=\"ld-docs-num\">&lt;b&gt;F1&lt;/b&gt;</span>", doc);
            Assert.Contains(RegistroManualLoteReglas.InsigniaDocumento(true, "PDF"), doc);
            Assert.Contains(RegistroManualLoteReglas.InsigniaDocumento(false, "sin PDF"), RegistroManualLoteReglas.CeldaDocumento("F1", null));
            Assert.Equal("<span class=\"text-muted\">—</span>", RegistroManualLoteReglas.CeldaDocumento("  ", true));

            string guia = RegistroManualLoteReglas.CeldaGuiaMci("T1<x>", 2);
            Assert.StartsWith("T1&lt;x&gt;<br />", guia);
            Assert.Contains(RegistroManualLoteReglas.InsigniaDocumento(true, "2 MCI"), guia);
            Assert.Contains(RegistroManualLoteReglas.InsigniaDocumento(true, "MCI"), RegistroManualLoteReglas.CeldaGuiaMci("T1", 1));
            Assert.Equal("<span class=\"text-muted\">sin guía</span><br /><span class=\"text-muted small\">sin MCI</span>",
                RegistroManualLoteReglas.CeldaGuiaMci(null, null));
        }

        // ── Subidas: postback completo y formulario multipart ───────────────────────────

        [Fact]
        public void SubirPdfDeCadaDocumento_TieneId_YSeRegistraComoPostbackCompleto()
        {
            // Sin ID se pintaba id="" y el PostBackTrigger de rptDocumentosLote (sin elemento en la página) no lo
            // cubría: el envío salía parcial, sin el archivo, y siempre decía "Seleccione el archivo de la factura.".
            string aspx = Marcado("RegistroManualLote");
            Assert.Matches(@"<asp:Button ID=""btnSubirPdfDocumentoLote"" runat=""server"" Text=""Subir PDF""[^>]*CommandName=""SubirPdf""", aspx);
            Assert.Contains("OnItemCreated=\"rptDocumentosLote_ItemCreated\"", aspx);

            string creado = Cuerpo(Leer("Views", "RegistroManualLote.aspx.cs"), "protected void rptDocumentosLote_ItemCreated(");
            Assert.Contains("e.Item.FindControl(\"btnSubirPdfDocumentoLote\") is Button boton", creado);
            Assert.Contains("ScriptManager.GetCurrent(Page)?.RegisterPostBackControl(boton);", creado);
        }

        [Fact]
        public void PageLoad_FormularioSiempreMultipart_TrasLasGuardas()
        {
            // Un lote nacional de CARGA sin factura no pinta ningún FileUpload: sin esto, tras registrar la factura por
            // AJAX, "Adjuntar" no subía el PDF.
            string carga = Cuerpo(Leer("Views", "RegistroManualLote.aspx.cs"), "protected void Page_Load(");
            int guardas = carga.IndexOf("SecurityHelper.AgregarHeadersSeguridad();", StringComparison.Ordinal);
            int multipart = carga.IndexOf("Page.Form.Enctype = \"multipart/form-data\";", StringComparison.Ordinal);
            int abrir = carga.IndexOf("AbrirLote(", StringComparison.Ordinal);
            Assert.True(carga.IndexOf("SecurityHelper.ExigirRolAdmin();", StringComparison.Ordinal) >= 0 && guardas > 0, "Faltan las guardas");
            Assert.True(multipart > guardas && abrir > multipart, "El multipart va tras las guardas y antes de abrir el lote");
        }

        [Fact]
        public void LoteNoDisponible_OcultaElResumenYVerElLote()
        {
            string aspx = Marcado("RegistroManualLote");
            Assert.Matches(@"<asp:PlaceHolder ID=""phResumenLote"" runat=""server"">\s*<p class=""sgv-resumen"">", aspx);
            string abrir = Cuerpo(Leer("Views", "RegistroManualLote.aspx.cs"), "private void AbrirLote(");
            Assert.Contains("pnlLoteNoDisponible.Visible = true;", abrir);
            Assert.Contains("phResumenLote.Visible = hlVerLote.Visible = false;", abrir);
        }

        // ── Topes de la tarea ───────────────────────────────────────────────────────────

        [Fact]
        public void Topes_DeLineas()
        {
            int cs = Lineas("Views", "ListaDespachos.aspx.cs"), aspx = Lineas("Views", "ListaDespachos.aspx");
            Assert.True(cs <= 1450, "ListaDespachos.aspx.cs tiene " + cs + " líneas (tope de E6-27: 1 450).");
            Assert.True(aspx <= 700, "ListaDespachos.aspx tiene " + aspx + " líneas (tope de E6-27: 700).");
            int nueva = Lineas("Views", "RegistroManualLote.aspx.cs");
            Assert.True(nueva <= 700, "RegistroManualLote.aspx.cs tiene " + nueva + " líneas (tope de E6-27: 700).");
        }

        [Fact]
        public void NingunMetodoDeMasDeSesenta_EnLaPaginaNiEnLosServicios()
        {
            var archivos = new[]
            {
                new[] { "Views", "RegistroManualLote.aspx.cs" },
                new[] { "Services", "Despachos", "RegistroManualLoteReglas.cs" },
                new[] { "Services", "Despachos", "ArchivosRegistroManual.cs" },
            };
            var largos = archivos.SelectMany(a => TamanoCodigoTests.Metodos(Leer(a))
                    .Where(m => m.Value > 60).Select(m => string.Join("/", a) + "::" + m.Key + " (" + m.Value + ")"))
                .ToList();
            Assert.True(largos.Count == 0, "Métodos de más de 60 líneas:\n" + string.Join("\n", largos));
        }
    }
}
