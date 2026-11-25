using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;
using CargaPendiente = WebSGV.Services.Despachos.DescargaPedidoService.CargaPendiente;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-24: "Descarga por pedido" sale de RegistroDespacho a <c>Views/Controles/Despachos/DescargaPedido.ascx</c>, conectado
    /// por eventos (la página aplica la <c>PropuestaConfiguracionBase</c> a su configuración base); <c>rptDescargas</c> va sin
    /// ViewState (M-169, parte 2) y una marca sacada de las cargas corta lo que llega de otra pestaña.
    /// </summary>
    public class DescargaPedidoTests
    {
        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Leer(params string[] ruta) => File.ReadAllText(Path.Combine(RaizWeb(), Path.Combine(ruta)));
        private static string Pagina() => Leer("Views", "RegistroDespacho.aspx");
        private static string PaginaCs() => Leer("Views", "RegistroDespacho.aspx.cs");
        private static string Control() => Leer("Views", "Controles", "Despachos", "DescargaPedido.ascx");
        private static string ControlCs() => Leer("Views", "Controles", "Despachos", "DescargaPedido.ascx.cs");

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

        private static CargaPendiente Carga(int id) => new CargaPendiente { IdDespacho = id, NumeroPedido = "P1" };

        // ── El control en la página ─────────────────────────────────────────────────────

        [Fact]
        public void LaPaginaRegistraElControl_YLoDeLaDescargaYaNoEstaEnElla()
        {
            Assert.Matches(@"<%@ Register Src=""~/Views/Controles/Despachos/DescargaPedido\.ascx"" TagPrefix=""sgv"" TagName=""DescargaPedido"" %>", Pagina());
            Assert.Contains("<sgv:DescargaPedido ID=\"ucDescargaPedido\" runat=\"server\" />", Pagina());
            foreach (string id in new[] { "pnlDescargaPedido", "ddlPedidosPendientes", "btnVerCargasPendientes", "pnlVistaDescarga", "rptDescargas",
                                          "fileProgramacionDescarga", "btnLeerProgramacionDescarga", "btnAplicarDescarga", "pnlDescargaAplicada", "btnQuitarDescarga" })
            {
                Assert.DoesNotContain("ID=\"" + id + "\"", Pagina());
                Assert.Contains("ID=\"" + id + "\"", Control());
            }
            // El código de la página no toca los controles de la tarjeta: los maneja el control.
            foreach (string campo in new[] { "pnlDescargaAplicada", "pnlVistaDescarga", "ddlPlantaDescarga", "txtFechaDescarga", "rptDescargas" })
                Assert.DoesNotContain(campo, PaginaCs());
        }

        [Fact]
        public void ElControl_NoPasaDe400Lineas_NiTieneMetodosDeMasDe60()
        {
            string cs = ControlCs();
            int lineas = File.ReadAllLines(Path.Combine(RaizWeb(), "Views", "Controles", "Despachos", "DescargaPedido.ascx.cs")).Length;
            Assert.True(lineas <= 400, "DescargaPedido.ascx.cs tiene " + lineas + " líneas (tope de E6-24: 400).");
            var largos = TamanoCodigoTests.Metodos(cs).Where(m => m.Value > 60).Select(m => m.Key + " (" + m.Value + ")").ToList();
            Assert.True(largos.Count == 0, "Métodos de más de 60 líneas: " + string.Join(", ", largos));
        }

        [Fact]
        public void LaPagina_AplicaLaPropuestaASusControlesDeConfiguracionBase()
        {
            string cs = PaginaCs();
            Assert.Contains("ConectarDescargaPedido();", Cuerpo(cs, "protected override void OnInit(EventArgs e)"));
            string conectar = Cuerpo(cs, "private void ConectarDescargaPedido()");
            Assert.Contains("ucDescargaPedido.ConfiguracionPropuesta += (s, a) => AplicarPropuestaConfiguracionBase(a.Propuesta);", conectar);
            Assert.Contains("ucDescargaPedido.Abierta += (s, a) => pnlConfiguracionBase.Visible = false;", conectar);
            Assert.Contains("ucDescargaPedido.Cerrada += (s, a) => pnlConfiguracionBase.Visible = !TieneLoteActivo;", conectar);

            string aplicar = Cuerpo(cs, "private void AplicarPropuestaConfiguracionBase(PropuestaConfiguracionBase p)");
            foreach (string linea in new[]
            {
                "RestaurarSeleccion(ddlClienteBase, p.IdCliente?.ToString());",
                "ddlTipoOperacionBase.SelectedValue = p.TipoOperacion;",
                "rblAmbitoOperacionBase.SelectedValue = p.EsInternacional ? \"1\" : \"0\";",
                "ActualizarPlantasPorAmbito();",
                "RestaurarSeleccion(ddlLugarOperacionBase, p.IdPlanta);",
                "txtFechaDespachoBase.Text = p.Fecha;",
                "pnlConfiguracionBase.Visible = true;",
            })
                Assert.Contains(linea, aplicar);
            // Las plantas del ámbito se recargan antes de elegir la del lote.
            Assert.True(aplicar.IndexOf("ActualizarPlantasPorAmbito();", StringComparison.Ordinal) < aplicar.IndexOf("ddlLugarOperacionBase", StringComparison.Ordinal));

            // El control propone una DESCARGA con la planta del lote, y la página la incorpora al "Iniciar lote".
            string usar = Cuerpo(ControlCs(), "protected void btnAplicarDescarga_Click(");
            Assert.Contains("TipoOperacion = \"DESCARGA\"", usar);
            Assert.Contains("IdPlanta = ArmadoDescarga.PlantaDelLote(ddlPlantaDescarga.SelectedValue, conductores)", usar);
            Assert.Contains("ucDescargaPedido.IncorporarAlLote(lote);", cs);
            Assert.Contains("ucDescargaPedido.Aplicada", Cuerpo(cs, "protected override void OnPreRender(EventArgs e)"));
        }

        [Fact]
        public void IncorporarAlLote_NoDejaAVistaElRecuadroAplicada()
        {
            // M-201 (la parte de la descarga): ya están en el lote; el aviso "se agregarán al pulsar Iniciar lote" se oculta.
            string cuerpo = Cuerpo(ControlCs(), "public void IncorporarAlLote(LoteDespachos lote)");
            int incorpora = cuerpo.IndexOf("ArmadoDescarga.IncorporarAlLote(lote, conductores, cargas);", StringComparison.Ordinal);
            Assert.True(incorpora > 0);
            Assert.True(cuerpo.IndexOf("Limpiar();", StringComparison.Ordinal) > incorpora);
            Assert.True(cuerpo.IndexOf("pnlDescargaAplicada.Visible = false;", StringComparison.Ordinal) > incorpora);
        }

        // ── Sin ViewState ───────────────────────────────────────────────────────────────

        [Fact]
        public void RepetidorDeDescargas_SinViewState()
        {
            var m = Regex.Match(Control(), "<asp:Repeater ID=\"rptDescargas\"[^>]*>");
            Assert.True(m.Success);
            Assert.Contains("EnableViewState=\"false\"", m.Value);
        }

        [Fact]
        public void SinViewState_SeVuelveAEnlazarDesdeLaSesionEnCadaPostback()
        {
            string carga = Cuerpo(ControlCs(), "protected void Page_Load(object sender, EventArgs e)");
            Assert.Contains("if (!IsPostBack || !pnlVistaDescarga.Visible) return;", carga);
            Assert.Contains("var cargas = Cargas;", carga);
            Assert.Contains("if (_vigente) EnlazarDescargas(cargas);", carga);
            // Los catálogos de las filas: una consulta por petición aunque se enlace dos veces.
            string catalogos = Leer("Views", "Controles", "Despachos", "CatalogosDescarga.cs");
            Assert.Matches(@"Conductores => _conductores \?\? \(_conductores = RegistroDespachoService\.ObtenerConductoresActivos\(\)\);", catalogos);
            Assert.Matches(@"Tractos => _tractos \?\? \(_tractos = RegistroDespachoService\.ObtenerTractosActivos\(\)\);", catalogos);
            Assert.Matches(@"Carretas => _carretas \?\? \(_carretas = RegistroDespachoService\.ObtenerCarretasActivas\(\)\);", catalogos);
            Assert.Contains("_catalogos.Dispose();", ControlCs());
        }

        // ── Dos pestañas en la misma sesión: la marca de las cargas ─────────────────────

        [Fact]
        public void MarcaDescarga_SaleDeLasCargasEnOrden()
        {
            var a = new List<CargaPendiente> { Carga(10), Carga(11) };
            string marca = RegistroDespachoReglas.MarcaDescarga(a);
            Assert.Matches("^[0-9a-f]{32}$", marca);
            Assert.Equal(marca, RegistroDespachoReglas.MarcaDescarga(new List<CargaPendiente> { Carga(10), Carga(11) }));
            // Otro pedido, otro orden o un tracto menos (ya se descargó): otra marca.
            Assert.NotEqual(marca, RegistroDespachoReglas.MarcaDescarga(new List<CargaPendiente> { Carga(11), Carga(10) }));
            Assert.NotEqual(marca, RegistroDespachoReglas.MarcaDescarga(new List<CargaPendiente> { Carga(10) }));
            Assert.NotEqual(marca, RegistroDespachoReglas.MarcaDescarga(new List<CargaPendiente> { Carga(10), Carga(12) }));
            Assert.Null(RegistroDespachoReglas.MarcaDescarga(null));
        }

        [Fact]
        public void DescargaVigente_SoloConLaMarcaDeLasCargasDeLaSesion()
        {
            var enSesion = new List<CargaPendiente> { Carga(10), Carga(11) };
            string marca = RegistroDespachoReglas.MarcaDescarga(enSesion);
            Assert.True(RegistroDespachoReglas.DescargaVigente(marca, enSesion));
            // Otra pestaña vio otro pedido: el formulario trae la marca de antes.
            Assert.False(RegistroDespachoReglas.DescargaVigente(RegistroDespachoReglas.MarcaDescarga(new List<CargaPendiente> { Carga(20) }), enSesion));
            // Formulario sin marca (de antes de E6-24), sesión vencida o vacía, mayúsculas.
            Assert.False(RegistroDespachoReglas.DescargaVigente(null, enSesion));
            Assert.False(RegistroDespachoReglas.DescargaVigente("", enSesion));
            Assert.False(RegistroDespachoReglas.DescargaVigente(marca, null));
            Assert.False(RegistroDespachoReglas.DescargaVigente(RegistroDespachoReglas.MarcaDescarga(new List<CargaPendiente>()), new List<CargaPendiente>()));
            Assert.False(RegistroDespachoReglas.DescargaVigente(marca.ToUpperInvariant(), enSesion));
        }

        [Fact]
        public void MarcaDistinta_NoSeEnlazaNiSeUsa()
        {
            string ascx = Control(), cs = ControlCs();
            // El campo oculto va dentro de la vista previa, sin ViewState, con la marca de lo que se muestra.
            int panel = ascx.IndexOf("<asp:Panel ID=\"pnlVistaDescarga\"", StringComparison.Ordinal);
            int campo = ascx.IndexOf("<asp:HiddenField ID=\"hfMarcaDesc\" runat=\"server\" EnableViewState=\"false\" />", StringComparison.Ordinal);
            Assert.True(panel > 0 && campo > panel && campo < ascx.IndexOf("<asp:Repeater ID=\"rptDescargas\"", StringComparison.Ordinal));
            Assert.Contains("hfMarcaDesc.Value = RegistroDespachoReglas.MarcaDescarga(cargas);", Cuerpo(cs, "private void Mostrar(List<DescargaPedidoService.CargaPendiente> cargas)"));

            // Page_Load: se compara con lo enviado y, si no coincide, no se enlazan las filas con los datos de otro pedido.
            string carga = Cuerpo(cs, "protected void Page_Load(object sender, EventArgs e)");
            int vigente = carga.IndexOf("_vigente = RegistroDespachoReglas.DescargaVigente(Request.Form[hfMarcaDesc.UniqueID], cargas);", StringComparison.Ordinal);
            Assert.True(vigente > 0 && carga.IndexOf("EnlazarDescargas(cargas)", StringComparison.Ordinal) > vigente);

            // "Usar en el lote" y "Leer programación" se cortan antes de armar o de leer el Word.
            foreach (var (evento, servicio) in new[]
            {
                ("btnAplicarDescarga_Click", "ArmadoDescarga."),
                ("btnLeerProgramacionDescarga_Click", "LectorProgramacionWord."),
            })
            {
                string cuerpo = Cuerpo(cs, "protected void " + evento + "(");
                int corta = cuerpo.IndexOf("if (!Disponible(cargas)) return;", StringComparison.Ordinal);
                int usa = cuerpo.IndexOf(servicio, StringComparison.Ordinal);
                Assert.True(corta > 0 && usa > corta, evento + ": Disponible(...) debe ir antes de " + servicio);
            }
            string disponible = Cuerpo(cs, "private bool Disponible(List<DescargaPedidoService.CargaPendiente> cargas)");
            Assert.Contains("Vencida();", disponible);
            Assert.Contains("\"La descarga cambió en otra pestaña; revísela.\"", disponible);
            Assert.Contains("\"La selección expiró. Vuelva a elegir el pedido.\"", Cuerpo(cs, "private void Vencida()"));
            // Si ningún evento la mostró, se pinta la de la sesión con el aviso.
            Assert.Contains("Disponible(Cargas)", Cuerpo(cs, "protected override void OnPreRender(EventArgs e)"));

            // Defensa extra: una fila que el formulario no trajo (la lista del conductor siempre viaja; una casilla desmarcada
            // no) no se incluye, y queda en su sitio (fila i = carga i).
            string fila = Cuerpo(cs, "private ArmadoDescarga.FilaElegida LeerFila(RepeaterItem item)");
            Assert.Contains("Incluida = Request.Form[conductor.UniqueID] != null && ((CheckBox)item.FindControl(\"chkIncluirDesc\")).Checked", fila);
            Assert.Contains("Filas = rptDescargas.Items.Cast<RepeaterItem>().Select(LeerFila).ToList()", cs);
        }

        // ── Lo que pasó a funciones puras ───────────────────────────────────────────────

        [Fact]
        public void OpcionesPedidos_TextosDeSiempre()
        {
            using (var dt = new DataTable())
            {
                dt.Columns.Add("numeroPedido", typeof(string));
                dt.Columns.Add("cliente", typeof(string));
                dt.Columns.Add("tractos", typeof(int));
                dt.Columns.Add("fechaCarga", typeof(DateTime));
                var vacia = ArmadoDescarga.OpcionesPedidos(dt);
                Assert.Equal(new[] { new KeyValuePair<string, string>("-- No hay pedidos cargados pendientes de descarga --", "") }, vacia);

                dt.Rows.Add("4400000001", "Cliente A", 3, new DateTime(2026, 11, 10));
                var opciones = ArmadoDescarga.OpcionesPedidos(dt);
                Assert.Equal(2, opciones.Count);
                Assert.Equal(new KeyValuePair<string, string>("-- Seleccione el pedido a descargar --", ""), opciones[0]);
                Assert.Equal("4400000001 · Cliente A · 3 tracto(s) por descargar · cargado 10/11/2026", opciones[1].Key);
                Assert.Equal("4400000001", opciones[1].Value);
            }
        }

        [Fact]
        public void DetalleCargaHtml_CodificaYMarcaLosProductosNuevos()
        {
            Assert.Equal("<span class=\"rd-sin-datos\">—</span>", RegistroDespachoReglas.DetalleCargaHtml(null));
            Assert.Equal("<span class=\"rd-sin-datos\">—</span>", RegistroDespachoReglas.DetalleCargaHtml(new List<LineaCarga>()));
            string html = RegistroDespachoReglas.DetalleCargaHtml(new List<LineaCarga>
            {
                new LineaCarga { Descripcion = "A<b>", Lote = "L1", Cantidad = 10, PesoBrutoKg = 500, IdProducto = 1 },
                new LineaCarga { Descripcion = "Nuevo", Cantidad = 5 },
            });
            Assert.StartsWith("<strong>", html);
            Assert.Contains(" bolsas · ", html);
            Assert.Contains("A&lt;b&gt; · lote L1 · ", html);
            Assert.Contains("Nuevo · lote — · ", html);
            Assert.Single(Regex.Matches(html, ">nuevo<"));
            // La página (vista previa del ZIP) y el control usan la misma.
            Assert.Contains("RegistroDespachoReglas.DetalleCargaHtml(t.Lineas)", PaginaCs());
            Assert.Contains("RegistroDespachoReglas.DetalleCargaHtml(c.Carga)", ControlCs());
            Assert.DoesNotContain("private static string DetalleCargaHtml", PaginaCs());
        }
    }
}
