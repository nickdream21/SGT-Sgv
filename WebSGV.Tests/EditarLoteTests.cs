using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-28: la edición de un lote deja de ser una vista de ListaDespachos y pasa a <c>Views/EditarLote.aspx?lote=</c>, con sus
    /// reglas puras en <c>EditarLoteReglas</c>. "Editar" de la lista y "Editar lote" del detalle son enlaces; "Guardar cambios"
    /// vuelve por GET al detalle del lote. Topes de la tarea: ListaDespachos.aspx.cs ≤ 1 100 líneas y su .aspx ≤ 500, ningún
    /// método de más de 60 en ListaDespachos, EditarLote ni EditarLoteReglas.
    /// </summary>
    public class EditarLoteTests
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

        private const string Lote = "1_4400134786_2026-11-11_CARGA_0_2";

        // ── Reglas puras ────────────────────────────────────────────────────────────────

        [Fact]
        public void Url_CodificaElLote()
        {
            Assert.Equal("~/Views/EditarLote.aspx?lote=" + Lote, EditarLoteReglas.Url(Lote));
            Assert.Equal("~/Views/EditarLote.aspx?lote=a%26b%3dc+d", EditarLoteReglas.Url("a&b=c d"));
            Assert.Equal("~/Views/EditarLote.aspx?lote=", EditarLoteReglas.Url(null));
        }

        [Fact]
        public void IdLoteVirtual_ComoElConcatDelSp()
        {
            var fecha = new DateTime(2026, 11, 5, 14, 30, 0);
            Assert.Equal("12_NOPEDIDO_2026-11-05_DESCARGA_1_7", EditarLoteReglas.IdLoteVirtual(12, null, fecha, "DESCARGA", true, 7));
            // Un pedido '' guardado no pasa por ISNULL: queda vacío (hay lotes activos así).
            Assert.Equal("1__2026-11-05_CARGA_0_2", EditarLoteReglas.IdLoteVirtual(1, "", fecha, "CARGA", false, 2));
            Assert.Equal(Lote, EditarLoteReglas.IdLoteVirtual(1, "4400134786", new DateTime(2026, 11, 11), "CARGA", false, 2));
            foreach (string id in new[]
            {
                EditarLoteReglas.IdLoteVirtual(12, null, fecha, "DESCARGA", true, 7),
                EditarLoteReglas.IdLoteVirtual(1, "", fecha, "CARGA", false, 2),
                EditarLoteReglas.IdLoteVirtual(1, "4400134786", fecha, "CARGA", false, 2),
            })
                Assert.True(ListaDespachosReglas.EsIdLoteValido(id), id);
        }

        [Fact]
        public void IdLoteVirtual_FechaSinCulturaLocal()
        {
            var cultura = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
                Assert.Equal("1_X_2026-11-05_CARGA_0_2", EditarLoteReglas.IdLoteVirtual(1, "X", new DateTime(2026, 11, 5), "CARGA", false, 2));
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = cultura;
            }
        }

        private static LoteRegistrado Antes() => new LoteRegistrado
        {
            IdLoteVirtual = Lote, IdCliente = 1, NumeroPedido = "4400134786", FechaProgramacion = new DateTime(2026, 11, 11),
            TipoOperacion = "CARGA", EsInternacional = false, IdPlanta = 2, IdsDespachos = new List<int> { 5, 6 }
        };

        [Fact]
        public void IdTrasGuardar_UsaElClienteDeAntesYLoEscritoEnElFormulario()
        {
            var input = new GuardarCambiosLoteInput
            {
                FechaDespacho = new DateTime(2026, 11, 9), NumeroPedido = "1234567890", TipoOperacion = "DESCARGA",
                EsInternacional = true, IdPlanta = 4
            };
            Assert.Equal("1_1234567890_2026-11-09_DESCARGA_1_4", EditarLoteReglas.IdTrasGuardar(Antes(), input));

            // Sin cambios de clave: el mismo id.
            input = new GuardarCambiosLoteInput
            {
                FechaDespacho = new DateTime(2026, 11, 11), NumeroPedido = "4400134786", TipoOperacion = "CARGA", IdPlanta = 2
            };
            Assert.Equal(Lote, EditarLoteReglas.IdTrasGuardar(Antes(), input));

            // Pedido vacío: el servicio guarda NULL, el SP arma NOPEDIDO.
            input.NumeroPedido = "";
            Assert.Equal("1_NOPEDIDO_2026-11-11_CARGA_0_2", EditarLoteReglas.IdTrasGuardar(Antes(), input));
            input.NumeroPedido = null;
            Assert.Equal("1_NOPEDIDO_2026-11-11_CARGA_0_2", EditarLoteReglas.IdTrasGuardar(Antes(), input));
        }

        [Fact]
        public void DestinoTrasGuardar_LosCuatroCasos()
        {
            const string nuevo = "1_NOPEDIDO_2026-11-11_CARGA_0_2";
            Assert.Equal("~/Views/ListaDespachos.aspx?lote=" + Lote + "&msg=lote_sin_cambios",
                EditarLoteReglas.DestinoTrasGuardar(Lote, nuevo, true, _ => throw new InvalidOperationException("sin cambios no consulta")));
            Assert.Equal("~/Views/ListaDespachos.aspx?lote=" + nuevo + "&msg=lote_guardado",
                EditarLoteReglas.DestinoTrasGuardar(Lote, nuevo, false, id => id == nuevo));
            Assert.Equal("~/Views/ListaDespachos.aspx?lote=" + Lote + "&msg=lote_guardado",
                EditarLoteReglas.DestinoTrasGuardar(Lote, nuevo, false, id => id == Lote));
            Assert.Equal("~/Views/ListaDespachos.aspx?vista=lotes&msg=lote_guardado",
                EditarLoteReglas.DestinoTrasGuardar(Lote, nuevo, false, _ => false));
            // El lote va codificado.
            Assert.Equal("~/Views/ListaDespachos.aspx?lote=a%26b&msg=lote_guardado",
                EditarLoteReglas.DestinoTrasGuardar("x", "a&b", false, _ => true));
        }

        [Theory]
        [InlineData("lote_guardado", "Cambios del lote guardados.", "success")]
        [InlineData("lote_sin_cambios", "No había cambios que guardar.", "info")]
        public void TextoAviso_SoloLosDosCodigos(string codigo, string texto, string tipo)
        {
            var aviso = EditarLoteReglas.TextoAviso(codigo);
            Assert.Equal(texto, aviso.Item1);
            Assert.Equal(tipo, aviso.Item2);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("LOTE_GUARDADO")]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("pagina-vencida")]
        public void TextoAviso_NullParaCualquierOtro(string codigo) => Assert.Null(EditarLoteReglas.TextoAviso(codigo));

        [Fact]
        public void EsEditable_ActivoConDespachos()
        {
            Assert.True(EditarLoteReglas.EsEditable(Antes()));
            Assert.False(EditarLoteReglas.EsEditable(null));
            var sin = Antes(); sin.IdsDespachos = new List<int>();
            Assert.False(EditarLoteReglas.EsEditable(sin));
            var anulado = Antes(); anulado.EstadoLote = "ANULADO";
            Assert.False(EditarLoteReglas.EsEditable(anulado));
        }

        [Fact]
        public void DescripcionAuditoria_ElTextoDeSiempre()
        {
            var input = new GuardarCambiosLoteInput { NumeroFactura = " F1 ", NumeroCPIC = " 1234567 " };
            var r = new ResultadoEdicionLote
            {
                Factura = new DocumentoGestionado { Id = 7, Accion = "CREADA" },
                Cpic = new DocumentoGestionado { Id = 8, Accion = "ENLAZADA" },
                CpicsQuitadas = new List<int> { 3 },
                FacturasQuitadas = new List<int> { 4, 5 },
                ConductoresCambiados = new Dictionary<int, int> { [11] = 90 }
            };
            Assert.Equal("Lote editado - despachos 11,12; conductor cambiado en despacho 11 → idConductor 90; factura F1 (id 7, CREADA)"
                + "; CPIC 1234567 (id 8, ENLAZADA); CPIC quitada (id 3); factura quitada (id 4, 5)",
                EditarLoteReglas.DescripcionAuditoria("11,12", input, r));
            Assert.Equal("Lote editado - despachos 11", EditarLoteReglas.DescripcionAuditoria("11", input, new ResultadoEdicionLote()));
        }

        [Fact]
        public void ValidarMontoObligatorio_VacioYNoNumerico()
        {
            var errores = new List<string>();
            EditarLoteReglas.ValidarMontoObligatorio(" ", "Valor del flete", errores);
            EditarLoteReglas.ValidarMontoObligatorio("abc", "Valor del flete", errores);
            EditarLoteReglas.ValidarMontoObligatorio("12,50", "Valor del flete", errores);
            Assert.Equal(2, errores.Count);
            Assert.Equal("El campo «Valor del flete» es obligatorio", errores[0]);
            Assert.False(errores[1].EndsWith(".", StringComparison.Ordinal));
        }

        // ── La página ───────────────────────────────────────────────────────────────────

        [Fact]
        public void Pagina_GuardasArribaSinTry_YAbreSoloUnLoteEditable()
        {
            string cs = Leer("Views", "EditarLote.aspx.cs");
            string carga = Cuerpo(cs, "protected void Page_Load(");
            Assert.StartsWith("{\n            SecurityHelper.ExigirRolAdmin();\n            SecurityHelper.AgregarHeadersSeguridad();", carga.Replace("\r\n", "\n"));
            Assert.DoesNotContain("try", carga);
            Assert.Contains("AbrirLote(Request.QueryString[\"lote\"])", carga);

            string abrir = Cuerpo(cs, "private void AbrirLote(");
            Assert.Contains("ListaDespachosReglas.EsIdLoteValido(idLote)", abrir);
            Assert.Contains("ListaDespachosService.ObtenerLotePorId(idLote)", abrir);
            Assert.Contains("EditarLoteReglas.EsEditable(lote)", abrir);
            Assert.Contains("pnlLoteNoDisponible.Visible = true;", abrir);
            Assert.Contains("phResumenLote.Visible = hlVolverLote.Visible = false;", abrir);

            Assert.DoesNotContain("Session[", cs);
            Assert.DoesNotContain("ex.Message", cs);
            Assert.Contains("ControlesFormulario.UsuarioActual(this)", cs);
            Assert.Contains("LecturaSegura.Leer(c => reader[c], columnName, \"EditarLote\", defaultValue)", cs);
        }

        [Fact]
        public void Guardar_ReleeElLote_YVuelveFueraDelTry()
        {
            string cs = Leer("Views", "EditarLote.aspx.cs");
            string guardar = Cuerpo(cs, "protected void btnGuardarCambios_Click(").Replace("\r\n", "\n");
            Assert.StartsWith("{\n            if (!Page.IsValid) return;", guardar);
            int intento = guardar.IndexOf("try", StringComparison.Ordinal);
            int relee = guardar.IndexOf("LoteEditable()", StringComparison.Ordinal);
            int valida = guardar.IndexOf("ValidarEdicionLote()", StringComparison.Ordinal);
            int escribe = guardar.IndexOf("ListaDespachosService.GuardarCambiosLote(", StringComparison.Ordinal);
            int vuelve = guardar.IndexOf("Volver(destino)", StringComparison.Ordinal);
            int captura = guardar.IndexOf("catch (Exception ex)", StringComparison.Ordinal);
            Assert.True(intento < relee && relee < valida && valida < escribe, "Relee el lote y valida antes de escribir");
            Assert.True(vuelve > captura, "Volver va fuera del try (el redirect no debe caer en el catch)");
            Assert.Contains("EditarLoteReglas.DestinoTrasGuardar(", guardar);
            Assert.Contains("if (!resultado.SinCambios) AuditarEdicionLote(", guardar);

            string volver = Cuerpo(cs, "private void Volver(");
            Assert.Contains("Response.Redirect(ResolveUrl(destino), false);", volver);
            Assert.Contains("Context.ApplicationInstance.CompleteRequest();", volver);
            Assert.Contains("CA3007", cs);

            string anular = Cuerpo(cs, "protected void btnAnularLote_Click(");
            Assert.True(anular.IndexOf("LoteEditable()", StringComparison.Ordinal) < anular.IndexOf("ListaDespachosService.AnularLote(", StringComparison.Ordinal));
            Assert.Contains("hlVolverLote.Visible = false;", anular);
            Assert.Contains("enPagina: true", anular);
            Assert.DoesNotContain("EstablecerContadores", cs);

            string editable = Cuerpo(cs, "private LoteRegistrado LoteEditable(");
            Assert.Contains("El lote ya no está activo: se anuló o cambió en otra pestaña.", editable);
            Assert.Contains("pnlEdicionLote.Visible = hlVolverLote.Visible = false;", editable);
        }

        [Fact]
        public void Auditoria_UnRegistroDelLoteYUnoPorDocumento()
        {
            string auditar = Cuerpo(Leer("Views", "EditarLote.aspx.cs"), "private void AuditarEdicionLote(");
            Assert.Contains("EditarLoteReglas.DescripcionAuditoria(idsCsv, input, resultado)", auditar);
            Assert.Equal(5, Regex.Matches(auditar, @"AuditoriaHelper\.Registrar\(").Count);
        }

        [Fact]
        public void Marcado_EncabezadoAyudaYFormulario()
        {
            string aspx = Marcado("EditarLote");
            Assert.Contains("<%@ Page Title=\"Editar lote\"", aspx);
            Assert.Contains("<h1 class=\"h2\">Editar lote</h1>", aspx);
            Assert.Contains("data-ayuda=\"lista-despachos\"", aspx);
            Assert.Contains("<asp:HyperLink ID=\"hlVolverLote\"", aspx);
            Assert.Contains("<asp:HyperLink ID=\"hlVolverLotes\" runat=\"server\" NavigateUrl=\"~/Views/ListaDespachos.aspx?vista=lotes\"", aspx);
            Assert.Contains("El lote no existe, está anulado o cambió.", aspx);
            Assert.Contains("data-montos-grupo", aspx);
            Assert.Contains("data-enter=\"<%= btnGuardarCambios.ClientID %>\"", aspx);
            Assert.Contains("txtNumeroPedidoEdit: '<%= txtNumeroPedidoEdit.ClientID %>'", aspx);
            Assert.Contains("RecursoHelper.Url(\"~/Content/paginas/EditarLote.css\")", aspx);
            Assert.Contains("RecursoHelper.Url(\"~/Scripts/paginas/EditarLote.js\")", aspx);
            Assert.DoesNotContain("btnCancelarEdicion", aspx);
            Assert.Contains("conductor-select", aspx);
            Assert.Contains(".conductor-select", Leer("Scripts", "paginas", "EditarLote.js"));
        }

        [Theory]
        [InlineData("ListaDespachos")]
        [InlineData("EditarLote")]
        public void CadaControlIdDeLosTriggersExisteEnSuMarcado(string pagina)
        {
            string aspx = Marcado(pagina);
            string triggers = Regex.Match(aspx, @"<Triggers>(?<t>.*?)</Triggers>", RegexOptions.Singleline).Groups["t"].Value;
            var ids = Regex.Matches(triggers, @"ControlID=""(?<id>\w+)""").Cast<Match>().Select(m => m.Groups["id"].Value).ToList();
            Assert.NotEmpty(ids);
            foreach (string id in ids)
                Assert.True(Regex.IsMatch(aspx, @"\bID=""" + id + @""""), pagina + ": el trigger " + id + " no tiene control");
        }

        // ── ListaDespachos sin la edición ───────────────────────────────────────────────

        [Fact]
        public void ListaDespachos_SinLaEdicion()
        {
            string aspx = Marcado("ListaDespachos"), cs = Leer("Views", "ListaDespachos.aspx.cs");
            string js = Leer("Scripts", "paginas", "ListaDespachos.js"), css = Leer("Content", "paginas", "ListaDespachos.css");
            foreach (string texto in new[] { aspx, cs, js })
                foreach (string retirado in new[] { "pnlEdicionLote", "gvConductoresLote", "btnGuardarCambios", "btnAnularLote",
                    "RechazarSiLoteAnulado", "select2", "conductor-select", "txtNumeroPedidoEdit", "btnEditarDesdeDetal",
                    "btnCancelarEdicion", "MostrarEdicionLote" })
                    Assert.DoesNotContain(retirado, texto);
            Assert.DoesNotContain("select2", css);
            Assert.DoesNotContain(".doc-panel", css);
            Assert.DoesNotContain("EdicionLote", Enum.GetNames(typeof(VistaListaDespachos)));
            Assert.DoesNotContain("EditarLote", Enum.GetNames(typeof(AccionFilaLista)));
        }

        [Fact]
        public void ListaDespachos_EditarEsUnEnlace_YLeeLosAvisos()
        {
            string aspx = Marcado("ListaDespachos"), cs = Leer("Views", "ListaDespachos.aspx.cs");
            Assert.Contains("<a href='<%#: UrlEditar(Eval(\"IdLoteVirtual\")) %>' class=\"btn btn-sm btn-outline-secondary\"", aspx);
            Assert.Contains("<asp:HyperLink ID=\"hlEditarLote\"", aspx);
            Assert.Contains("ResolveUrl(EditarLoteReglas.Url(Convert.ToString(idLoteVirtual)))", cs);

            string detalle = Cuerpo(cs, "private void MostrarDetallesLote(");
            Assert.Contains("hlEditarLote.Visible = !anulado;", detalle);
            Assert.Contains("hlEditarLote.NavigateUrl = UrlEditar(idLoteVirtual);", detalle);

            string carga = Cuerpo(cs, "protected void Page_Load(");
            Assert.Contains("Request.QueryString[\"vista\"] == \"lotes\"", carga);
            Assert.Contains("EditarLoteReglas.TextoAviso(Request.QueryString[\"msg\"])", carga);
            // ?aviso= es de Site.Master (ErrorAplicacionHelper): los avisos de vuelta usan ?msg=.
            Assert.DoesNotContain("EditarLoteReglas.TextoAviso(Request.QueryString[\"aviso\"])", carga);
        }

        [Fact]
        public void WebConfigYCsproj()
        {
            Assert.DoesNotContain("<location path=\"Views/EditarLote.aspx\">", Leer("Web.config"));
            string csproj = Leer("WebSGV.csproj");
            foreach (string entrada in new[]
            {
                "<Content Include=\"Views\\EditarLote.aspx\" />",
                "<Compile Include=\"Views\\EditarLote.aspx.cs\">",
                "<Compile Include=\"Views\\EditarLote.aspx.designer.cs\">",
                "<Content Include=\"Content\\paginas\\EditarLote.css\" />",
                "<Content Include=\"Scripts\\paginas\\EditarLote.js\" />",
                "<Compile Include=\"Services\\Despachos\\EditarLoteReglas.cs\" />",
            })
                Assert.Contains(entrada, csproj);
            Assert.Matches(@"<Compile Include=""Views\\EditarLote\.aspx\.cs"">\s*<DependentUpon>EditarLote\.aspx</DependentUpon>\s*<SubType>ASPXCodeBehind</SubType>", csproj);
        }

        // ── Topes de la tarea ───────────────────────────────────────────────────────────

        [Fact]
        public void Topes_DeLineas()
        {
            int cs = Lineas("Views", "ListaDespachos.aspx.cs"), aspx = Lineas("Views", "ListaDespachos.aspx");
            Assert.True(cs <= 1100, "ListaDespachos.aspx.cs tiene " + cs + " líneas (tope de E6-28: 1 100).");
            Assert.True(aspx <= 500, "ListaDespachos.aspx tiene " + aspx + " líneas (tope de E6-28: 500).");
        }

        [Fact]
        public void NingunMetodoDeMasDeSesenta()
        {
            var archivos = new[]
            {
                new[] { "Views", "ListaDespachos.aspx.cs" },
                new[] { "Views", "EditarLote.aspx.cs" },
                new[] { "Services", "Despachos", "EditarLoteReglas.cs" },
            };
            var largos = archivos.SelectMany(a => TamanoCodigoTests.Metodos(Leer(a))
                    .Where(m => m.Value > 60).Select(m => string.Join("/", a) + "::" + m.Key + " (" + m.Value + ")"))
                .ToList();
            Assert.True(largos.Count == 0, "Métodos de más de 60 líneas:\n" + string.Join("\n", largos));
        }
    }
}
