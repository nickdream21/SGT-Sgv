using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using WebSGV.Views.Controles.Despachos;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-25: "Leer documentos" con el ZIP o los PDF sale de RegistroDespacho a <c>Views/Controles/Despachos/ImportacionDocumentos.ascx</c>,
    /// conectado por eventos (la página aplica la <c>PropuestaConfiguracionBase</c> ampliada con factura y pedido); los tres
    /// repetidores van sin ViewState (M-169, parte 3) con una marca sacada de la importación de la sesión; el recuadro aplicado
    /// se oculta al incorporarlo (M-201) y lo aplicado (importación o descarga) lleva su marca hasta "Iniciar lote" (M-206).
    /// </summary>
    public class ImportacionDocumentosTests
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
        private static string Control() => Leer("Views", "Controles", "Despachos", "ImportacionDocumentos.ascx");
        private static string ControlCs() => Leer("Views", "Controles", "Despachos", "ImportacionDocumentos.ascx.cs");
        private static string Descarga() => Leer("Views", "Controles", "Despachos", "DescargaPedido.ascx");
        private static string DescargaCs() => Leer("Views", "Controles", "Despachos", "DescargaPedido.ascx.cs");
        private static string ConfiguracionBase() => Leer("Views", "Controles", "Despachos", "ConfiguracionBaseLote.ascx");
        private static string ConfiguracionBaseCs() => Leer("Views", "Controles", "Despachos", "ConfiguracionBaseLote.ascx.cs");

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

        private static int Pos(string texto, string buscado)
        {
            int i = texto.IndexOf(buscado, StringComparison.Ordinal);
            Assert.True(i >= 0, "No está: " + buscado);
            return i;
        }

        private static ImportacionPedido Importacion() => new ImportacionPedido
        {
            NumeroPedido = "4400000001",
            IdCliente = 3,
            FacturaArchivoRutaTemp = @"C:\temp\Factura\_temp\a.pdf",
            Tractos =
            {
                new TractoImportado { NumeroTransporte = "T1", NumeroGuia = "G1", IdConductor = 5, IdTracto = 6, IdCarreta = 7,
                    Lineas = { new LineaCarga { CodigoProducto = "P1", Descripcion = "Alimento", Cantidad = 10 } } },
                new TractoImportado { NumeroTransporte = "T2", NumeroGuia = "G2", YaRegistrado = true },
            },
            Programados = { new DespachoProgramado { IdDespacho = 90 } },
        };

        // ── El control en la página ─────────────────────────────────────────────────────

        [Fact]
        public void LaPaginaRegistraElControl_YLaVistaPreviaYaNoEstaEnElla()
        {
            Assert.Matches(@"<%@ Register Src=""~/Views/Controles/Despachos/ImportacionDocumentos\.ascx"" TagPrefix=""sgv"" TagName=""ImportacionDocumentos"" %>", Pagina());
            Assert.Contains("<sgv:ImportacionDocumentos ID=\"ucImportacionDocumentos\" runat=\"server\" />", Pagina());
            foreach (string id in new[] { "pnlVistaPreviaImportacion", "ddlClienteImp", "txtFechaProgramacionImp", "ddlPlantaImp", "rptTractosImportados",
                                          "rptProductosNuevos", "rptConductoresNuevos", "chkAprobarAltas", "btnAplicarImportacion", "btnCorregirImportacion",
                                          "btnDescartarImportacion", "pnlImportacionAplicada", "btnQuitarImportacion" })
            {
                Assert.DoesNotContain("ID=\"" + id + "\"", Pagina());
                Assert.Contains("ID=\"" + id + "\"", Control());
            }
            // El código de la página no toca los controles de la vista previa: los maneja el control.
            foreach (string campo in new[] { "pnlImportacionAplicada", "pnlVistaPreviaImportacion", "rptTractosImportados", "ddlClienteImp",
                                             "ddlPlantaImp", "txtFechaProgramacionImp", "chkAprobarAltas", "ImportacionActual", "ConductoresImportados" })
                Assert.DoesNotContain(campo, PaginaCs());
            Assert.False(Regex.IsMatch(ControlCs(), @"\bSession\s*\["), "ImportacionDocumentos.ascx.cs usa Session[...]: use EstadoRegistroDespacho.");

            // El FileUpload y "Leer documentos" siguen en la página (PostBackTrigger): le pasa los archivos al control.
            Assert.DoesNotContain("<asp:FileUpload", Control());
            string leer = Cuerpo(PaginaCs(), "protected void btnLeerDocumentos_Click(");
            Assert.Contains("fileDocumentosPedido.PostedFiles", leer);
            Assert.True(Pos(leer, "DescartarImportacion();") < leer.LastIndexOf("ucImportacionDocumentos.Leer(archivos, words);", StringComparison.Ordinal));
            Assert.Contains("ucProgramacionWord.Leer(words[0]);", leer);
        }

        [Fact]
        public void ElControl_NoPasaDe650Lineas_NiTieneMetodosDeMasDe60()
        {
            int lineas = File.ReadAllLines(Path.Combine(RaizWeb(), "Views", "Controles", "Despachos", "ImportacionDocumentos.ascx.cs")).Length;
            Assert.True(lineas <= 650, "ImportacionDocumentos.ascx.cs tiene " + lineas + " líneas (tope de E6-25: 650).");
            var largos = TamanoCodigoTests.Metodos(ControlCs()).Where(m => m.Value > 60).Select(m => m.Key + " (" + m.Value + ")").ToList();
            Assert.True(largos.Count == 0, "Métodos de más de 60 líneas: " + string.Join(", ", largos));
        }

        [Fact]
        public void LaPaginaLoConectaEnOnInit_YRegistraSusBotonesComoAsincronos()
        {
            string cs = PaginaCs();
            Assert.Contains("ConectarImportacionDocumentos();", Cuerpo(cs, "protected override void OnInit(EventArgs e)"));
            string conectar = Cuerpo(cs, "private void ConectarImportacionDocumentos()");
            foreach (string linea in new[]
            {
                "ucImportacionDocumentos.Aviso += (s, a) => MostrarMensajeImportacion(a.Mensaje, a.Tipo);",
                "ucImportacionDocumentos.Cerrada += (s, a) => { pnlConfiguracionBase.Visible = !TieneLoteActivo; pnlSubirDocumentos.Visible = true; };",
                "ucImportacionDocumentos.ConfiguracionPropuesta += (s, a) => AplicarPropuestaConfiguracionBase(a.Propuesta);",
                "ucImportacionDocumentos.LoteCreado += (s, a) => IniciarLoteImportado(a.Lote, a.Tractos);",
                "ucImportacionDocumentos.Descartada += (s, a) => DescartarImportacion();",
            })
                Assert.Contains(linea, conectar);

            string disparadores = Cuerpo(cs, "private void RegistrarDisparadores()");
            string asincronos = disparadores.Substring(disparadores.IndexOf("var clic", StringComparison.Ordinal));
            Assert.Contains("ucImportacionDocumentos.DisparadoresAsincronos", asincronos);
            var botones = Regex.Match(ControlCs(), @"DisparadoresAsincronos => new Control\[\] \{(?<b>[^}]*)\};").Groups["b"].Value;
            Assert.Equal(new[] { "btnAplicarImportacion", "btnCorregirImportacion", "btnDescartarImportacion", "btnQuitarImportacion" },
                botones.Split(',').Select(b => b.Trim()));
            // "Leer documentos" sigue siendo PostBackTrigger, en la página ("Iniciar lote" llega del control de la configuración base).
            Assert.Contains("ucConfiguracionBase.DisparadoresCompletos.Concat(new Control[] { btnLeerDocumentos })", disparadores);
        }

        // ── La propuesta ampliada (factura y pedido) ────────────────────────────────────

        [Fact]
        public void Corregir_ProponeLaConfiguracionBase_YLaPaginaLaAplicaASus11Controles()
        {
            string corregir = Cuerpo(ControlCs(), "protected void btnCorregirImportacion_Click(");
            Assert.Contains("PropuestaConfiguracionBase.DeImportacion(imp, ddlPlantaImp.SelectedValue, txtFechaProgramacionImp.Text)", corregir);
            Assert.True(Pos(corregir, "if (!Disponible(imp) || ClienteCambiado(imp)) return;") < Pos(corregir, "Preparar(imp,"));

            // E6-26: la página pasa la propuesta al control de la configuración base y abre su panel.
            string pagina = Cuerpo(PaginaCs(), "private void AplicarPropuestaConfiguracionBase(PropuestaConfiguracionBase p)");
            Assert.True(Pos(pagina, "ucConfiguracionBase.Aplicar(p);") < Pos(pagina, "pnlConfiguracionBase.Visible = true;"));
            string aplicar = Cuerpo(ConfiguracionBaseCs(), "public void Aplicar(PropuestaConfiguracionBase p)");
            foreach (string linea in new[]
            {
                "RestaurarSeleccion(ddlClienteBase, p.IdCliente?.ToString());",
                "ddlTipoOperacionBase.SelectedValue = p.TipoOperacion;",
                "rblAmbitoOperacionBase.SelectedValue = p.EsInternacional ? \"1\" : \"0\";",
                "RestaurarSeleccion(ddlLugarOperacionBase, p.IdPlanta);",
                "txtFechaDespachoBase.Text = p.Fecha;",
                "RestaurarSeleccion(ddlMonedaFacturaBase, p.MonedaFactura);",
                "txtNumeroPedidoBase.Text = p.NumeroPedido;",
                "txtNumeroFacturaBase.Text = p.NumeroFactura;",
                "txtFechaEmisionFacturaBase.Text = p.FechaEmisionFactura;",
                "txtValorTotalFacturaBase.Text = p.ValorTotalFactura;",
            })
                Assert.Contains(linea, aplicar);
            // La planta se elige después de recargar las del ámbito, y la factura después de mostrar su panel.
            Assert.True(Pos(aplicar, "ActualizarPlantasPorAmbito();") < Pos(aplicar, "RestaurarSeleccion(ddlLugarOperacionBase"));
            Assert.True(Pos(aplicar, "MostrarOcultarPanelesBase();") < Pos(aplicar, "txtNumeroFacturaBase"));
        }

        [Fact]
        public void DeImportacion_CargaNacionalConElPedidoYLaFacturaLeidos()
        {
            var imp = Importacion();
            imp.Factura = new FacturaImportada { NumeroFactura = "F222-1", FechaEmision = new DateTime(2026, 11, 9), Moneda = "USD", ValorTotal = 12345.5m };
            var p = PropuestaConfiguracionBase.DeImportacion(imp, "4", "2026-11-10");
            Assert.Equal(3, p.IdCliente);
            Assert.Equal("CARGA", p.TipoOperacion);
            Assert.False(p.EsInternacional);
            Assert.Equal("4", p.IdPlanta);
            Assert.Equal("2026-11-10", p.Fecha);
            Assert.Equal("USD", p.MonedaFactura);
            Assert.Equal("4400000001", p.NumeroPedido);
            Assert.Equal("F222-1", p.NumeroFactura);
            Assert.Equal("2026-11-09", p.FechaEmisionFactura);
            Assert.Equal("12345.50", p.ValorTotalFactura);   // punto decimal, como lo dejaba la página

            // Sin factura: no se toca nada de la factura. Factura sin número: el campo queda vacío.
            imp.Factura = null;
            p = PropuestaConfiguracionBase.DeImportacion(imp, "4", "");
            Assert.Null(p.NumeroFactura);
            Assert.Null(p.FechaEmisionFactura);
            Assert.Null(p.ValorTotalFactura);
            Assert.Null(p.MonedaFactura);
            imp.Factura = new FacturaImportada();
            Assert.Equal("", PropuestaConfiguracionBase.DeImportacion(imp, "4", "").NumeroFactura);
        }

        // ── M-201: el recuadro aplicado no queda a la vista ─────────────────────────────

        [Fact]
        public void IncorporarAlLote_OcultaElRecuadro_YLaSubidaVuelveALaVista()
        {
            string cuerpo = Cuerpo(ControlCs(), "public void IncorporarAlLote(LoteDespachos lote)");
            int incorpora = Pos(cuerpo, "ArmadoLoteImportado.IncorporarAlLote(lote, imp, conductores);");
            Assert.True(Pos(cuerpo, "LimpiarImportacion(borrarArchivos: true);") > incorpora);
            Assert.True(Pos(cuerpo, "pnlImportacionAplicada.Visible = false;") > incorpora);
            Assert.Contains("ucImportacionDocumentos.IncorporarAlLote(lote);", Cuerpo(PaginaCs(), "private void IniciarLote()"));
            // Al volver a la configuración base (lote finalizado o cancelado) se pueden subir archivos otra vez.
            Assert.Contains("pnlSubirDocumentos.Visible = !ucImportacionDocumentos.Aplicada;", Cuerpo(PaginaCs(), "private void MostrarFaseConfiguracionBase()"));
            Assert.Contains("ucImportacionDocumentos.Aplicada", Cuerpo(PaginaCs(), "protected override void OnPreRender(EventArgs e)"));
        }

        // ── Sin ViewState ───────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("rptTractosImportados")]
        [InlineData("rptProductosNuevos")]
        [InlineData("rptConductoresNuevos")]
        public void Repetidores_SinViewState(string repetidor)
        {
            var m = Regex.Match(Control(), "<asp:Repeater ID=\"" + repetidor + "\"[^>]*>");
            Assert.True(m.Success, "Falta " + repetidor);
            Assert.Contains("EnableViewState=\"false\"", m.Value);
        }

        [Fact]
        public void SinViewState_SeVuelvenAEnlazarDesdeLaSesionEnCadaPostback()
        {
            string carga = Cuerpo(ControlCs(), "protected void Page_Load(object sender, EventArgs e)");
            Assert.Contains("if (!IsPostBack || !pnlVistaPreviaImportacion.Visible) return;", carga);
            Assert.Contains("if (_vigente) Enlazar(imp);", carga);
            // Enlazar arma las tres tablas (tractos y altas) desde la importación; los catálogos, una vez por petición.
            string enlazar = Cuerpo(ControlCs(), "private void Enlazar(ImportacionPedido imp)");
            foreach (string r in new[] { "rptTractosImportados.DataBind();", "rptProductosNuevos.DataBind();", "rptConductoresNuevos.DataBind();" })
                Assert.Contains(r, enlazar);
            Assert.Contains("_catalogos.Conductores", ControlCs());
            Assert.Contains("_catalogos.Dispose();", ControlCs());
            Assert.DoesNotContain("ObtenerConductoresActivos", ControlCs());
        }

        // ── Dos pestañas: la marca de la vista previa ───────────────────────────────────

        [Fact]
        public void MarcaImportacion_SaleDeLaImportacionDeLaSesion()
        {
            string marca = RegistroDespachoReglas.MarcaImportacion(Importacion(), aplicada: false);
            Assert.Matches("^[0-9a-f]{32}$", marca);
            Assert.Equal(marca, RegistroDespachoReglas.MarcaImportacion(Importacion(), aplicada: false));
            Assert.Null(RegistroDespachoReglas.MarcaImportacion(null, false));

            // Otro cliente, otro pedido, otra lectura (otro PDF temporal), otra fila, otro programado o ya aplicada: otra marca.
            var cambios = new List<Action<ImportacionPedido>>
            {
                i => i.IdCliente = 4,
                i => i.NumeroPedido = "4400000002",
                i => i.FacturaArchivoRutaTemp = @"C:\temp\Factura\_temp\b.pdf",
                i => i.Tractos.RemoveAt(1),
                i => i.Tractos[0].IdConductor = 8,
                i => i.Programados.Clear(),
                // Otra pestaña registró el producto nuevo (las altas de este formulario ya no son las de la sesión).
                i => i.Tractos[0].Lineas[0].IdProducto = 11,
            };
            foreach (var cambio in cambios)
            {
                var imp = Importacion();
                cambio(imp);
                Assert.NotEqual(marca, RegistroDespachoReglas.MarcaImportacion(imp, aplicada: false));
            }
            Assert.NotEqual(marca, RegistroDespachoReglas.MarcaImportacion(Importacion(), aplicada: true));
        }

        [Fact]
        public void ImportacionVigente_SoloConLaMarcaDeLaSesion()
        {
            var enSesion = Importacion();
            string marca = RegistroDespachoReglas.MarcaImportacion(enSesion, false);
            Assert.True(RegistroDespachoReglas.ImportacionVigente(marca, enSesion, false));
            Assert.False(RegistroDespachoReglas.ImportacionVigente(marca, enSesion, true));   // otra pestaña la aplicó
            var otra = Importacion(); otra.NumeroPedido = "4400000009";
            Assert.False(RegistroDespachoReglas.ImportacionVigente(RegistroDespachoReglas.MarcaImportacion(otra, false), enSesion, false));
            Assert.False(RegistroDespachoReglas.ImportacionVigente(null, enSesion, false));
            Assert.False(RegistroDespachoReglas.ImportacionVigente("", enSesion, false));
            Assert.False(RegistroDespachoReglas.ImportacionVigente(marca, null, false));
            Assert.False(RegistroDespachoReglas.ImportacionVigente(marca.ToUpperInvariant(), enSesion, false));
        }

        [Fact]
        public void MarcaDistinta_NoSeEnlazaNiSeUsa()
        {
            string ascx = Control(), cs = ControlCs();
            int panel = Pos(ascx, "<asp:Panel ID=\"pnlVistaPreviaImportacion\"");
            int campo = Pos(ascx, "<asp:HiddenField ID=\"hfMarcaImp\" runat=\"server\" EnableViewState=\"false\" />");
            Assert.True(campo > panel && campo < Pos(ascx, "<asp:Repeater ID=\"rptTractosImportados\""));
            Assert.Contains("hfMarcaImp.Value = RegistroDespachoReglas.MarcaImportacion(imp, ConductoresImportados != null);", Cuerpo(cs, "private void Mostrar(ImportacionPedido imp)"));
            Assert.Contains("Mostrar(imp);", Cuerpo(cs, "public void Restaurar()"));

            string carga = Cuerpo(cs, "protected void Page_Load(object sender, EventArgs e)");
            int vigente = Pos(carga, "_vigente = RegistroDespachoReglas.ImportacionVigente(Request.Form[hfMarcaImp.UniqueID], imp, ConductoresImportados != null);");
            Assert.True(Pos(carga, "Enlazar(imp)") > vigente);

            // "Crear lote" y "Corregir" se cortan antes de validar, registrar altas o armar el lote.
            foreach (var (evento, servicio) in new[] { ("btnAplicarImportacion_Click", "ArmadoLoteImportado."), ("btnCorregirImportacion_Click", "Preparar(") })
            {
                string cuerpo = Cuerpo(cs, "protected void " + evento + "(");
                Assert.True(Pos(cuerpo, "if (!Disponible(imp) || ClienteCambiado(imp)) return;") < Pos(cuerpo, servicio), evento);
            }
            string disponible = Cuerpo(cs, "private bool Disponible(ImportacionPedido imp)");
            Assert.Contains("Vencida();", disponible);
            Assert.Contains("\"La importación cambió en otra pestaña; revísela.\"", disponible);
            Assert.Contains("\"La importación expiró. Vuelva a leer los documentos.\"", Cuerpo(cs, "private void Vencida()"));
            Assert.Contains("Disponible(Actual)", Cuerpo(cs, "protected override void OnPreRender(EventArgs e)"));

            // Defensa extra: una fila que el formulario no trajo (la lista del conductor siempre viaja) no se incluye.
            Assert.Contains("Incluida = Request.Form[conductor.UniqueID] != null && ((CheckBox)item.FindControl(\"chkIncluir\")).Checked",
                Cuerpo(cs, "private ArmadoLoteImportado.FilaVistaPrevia LeerFila(RepeaterItem item)"));
            Assert.Contains("Filas = rptTractosImportados.Items.Cast<RepeaterItem>().Select(LeerFila).ToList()", cs);
        }

        // ── M-206: lo aplicado al lote, con su marca hasta "Iniciar lote" ───────────────

        private static List<ConductorLote> Conductores(long ticks) => new List<ConductorLote>
        {
            new ConductorLote { IdConductor = 1, IdTracto = 2, IdCarreta = 3, NumeroTransporte = "T1", FechaAgregado = new DateTime(ticks) },
        };

        [Fact]
        public void MarcaAplicada_CambiaConLaVistaYConCadaVezQueSeAplica()
        {
            string vista = RegistroDespachoReglas.MarcaImportacion(Importacion(), true);
            long t = new DateTime(2026, 11, 25, 10, 0, 0).Ticks;
            string marca = RegistroDespachoReglas.MarcaAplicada(vista, Conductores(t));
            Assert.Matches("^[0-9a-f]{32}$", marca);
            Assert.Equal(marca, RegistroDespachoReglas.MarcaAplicada(vista, Conductores(t)));
            // Otra pestaña volvió a aplicar lo mismo (otro momento), otro pedido u otro conductor: otra marca.
            Assert.NotEqual(marca, RegistroDespachoReglas.MarcaAplicada(vista, Conductores(t + 1)));
            Assert.NotEqual(marca, RegistroDespachoReglas.MarcaAplicada(RegistroDespachoReglas.MarcaDescarga(new[] { new DescargaPedidoService.CargaPendiente { IdDespacho = 1 } }), Conductores(t)));
            var otro = Conductores(t); otro[0].IdConductor = 9;
            Assert.NotEqual(marca, RegistroDespachoReglas.MarcaAplicada(vista, otro));
            Assert.Null(RegistroDespachoReglas.MarcaAplicada(null, Conductores(t)));
            Assert.Null(RegistroDespachoReglas.MarcaAplicada(vista, null));
        }

        [Fact]
        public void AlIniciarLote_SeCortaSiLaMarcaEnviadaNoEsLaDeLaSesion()
        {
            const string aviso = "La importación cambió en otra pestaña; revísela.";
            string enSesion = RegistroDespachoReglas.MarcaAplicada(RegistroDespachoReglas.MarcaImportacion(Importacion(), true), Conductores(1));
            // Misma marca: se sigue y se incorpora.
            Assert.Null(RegistroDespachoReglas.AvisoAplicadaAlIniciar(enSesion, enSesion, aviso));
            Assert.True(RegistroDespachoReglas.AplicadaVigente(enSesion, enSesion));
            // Otra pestaña aplicó otra cosa, la volvió a aplicar o la quitó (sesión sin nada): se corta, sin crear el lote.
            Assert.Equal(aviso, RegistroDespachoReglas.AvisoAplicadaAlIniciar(enSesion, RegistroDespachoReglas.MarcaAplicada("x", Conductores(1)), aviso));
            Assert.Equal(aviso, RegistroDespachoReglas.AvisoAplicadaAlIniciar(enSesion, null, aviso));
            Assert.Equal(aviso, RegistroDespachoReglas.AvisoAplicadaAlIniciar("", enSesion, aviso));
            // El recuadro no estaba a la vista en esa pestaña (el campo no llegó): se sigue, pero no se incorpora lo de la sesión.
            Assert.Null(RegistroDespachoReglas.AvisoAplicadaAlIniciar(null, enSesion, aviso));
            Assert.False(RegistroDespachoReglas.AplicadaVigente(null, enSesion));
            Assert.False(RegistroDespachoReglas.AplicadaVigente("", enSesion));
            Assert.False(RegistroDespachoReglas.AplicadaVigente(enSesion, null));
        }

        [Fact]
        public void IniciarLote_ComparaLaMarcaDeLoAplicado_AntesDeCrearElLote()
        {
            // Los dos recuadros llevan su marca, en un campo que solo viaja si el recuadro está a la vista.
            foreach (var (ascx, panel, campo) in new[]
            {
                (Control(), "pnlImportacionAplicada", "hfMarcaImpAplicada"),
                (Descarga(), "pnlDescargaAplicada", "hfMarcaDescAplicada"),
            })
            {
                int p = Pos(ascx, "<asp:Panel ID=\"" + panel + "\"");
                int c = Pos(ascx, "<asp:HiddenField ID=\"" + campo + "\" runat=\"server\" EnableViewState=\"false\" />");
                Assert.True(c > p && c < ascx.IndexOf("</asp:Panel>", p, StringComparison.Ordinal), campo + " debe ir dentro de " + panel);
            }
            Assert.Contains("hfMarcaImpAplicada.Value = MarcaAplicadaEnSesion();", Cuerpo(ControlCs(), "private void MostrarAplicada("));
            Assert.Contains("hfMarcaDescAplicada.Value = MarcaAplicadaEnSesion();", Cuerpo(DescargaCs(), "protected void btnAplicarDescarga_Click("));

            // "Iniciar lote" pregunta a los dos controles (los dos, para que cada uno oculte su recuadro viejo) antes de validar
            // y de crear el lote.
            string iniciar = Cuerpo(PaginaCs(), "private void IniciarLote()");
            int pregunta = Pos(iniciar, "string avisoImportacion = ucImportacionDocumentos.AvisoAlIniciarLote(), avisoDescarga = ucDescargaPedido.AvisoAlIniciarLote();");
            Assert.True(pregunta < Pos(iniciar, "ucConfiguracionBase.Validar()") && pregunta < Pos(iniciar, "ucConfiguracionBase.CrearLote("));
            Assert.True(Pos(iniciar, "MostrarMensaje(avisoImportacion ?? avisoDescarga, \"warning\"); return; }") < Pos(iniciar, "ucConfiguracionBase.CrearLote("));

            // Ronda 2: con el aviso el recuadro viejo se OCULTA (sin tocar la sesión) y nunca recibe la marca vigente: su
            // configuración base es de otro pedido, y un segundo "Iniciar lote" incorporaría los tractos de la otra pestaña
            // bajo el pedido, la factura, el cliente y la planta de esta.
            foreach (var (cs, campo, aviso, panel, vuelve) in new[]
            {
                (ControlCs(), "hfMarcaImpAplicada", "RegistroDespachoReglas.AvisoImportacionEnOtraPestana", "pnlImportacionAplicada", "Cerrada?.Invoke(this, EventArgs.Empty);"),
                (DescargaCs(), "hfMarcaDescAplicada", "RegistroDespachoReglas.AvisoDescargaEnOtraPestana", "pnlDescargaAplicada", "pnlElegirPedidoDescarga.Visible = true;"),
            })
            {
                string cuerpo = Cuerpo(cs, "public string AvisoAlIniciarLote()");
                Assert.Matches(@"RegistroDespachoReglas\.AvisoAplicadaAlIniciar\(Request\.Form\[" + campo + @"\.UniqueID\], MarcaAplicadaEnSesion\(\),\s+" + Regex.Escape(aviso) + @"\);", cuerpo);
                Assert.Contains(panel + ".Visible = false;", cuerpo);
                Assert.Contains(vuelve, cuerpo);
                Assert.DoesNotContain("MostrarAplicada", cuerpo);
                Assert.DoesNotContain(campo + ".Value", cuerpo);
                Assert.DoesNotContain("Limpiar", cuerpo);
            }
            Assert.Contains("Cerrada?.Invoke(this, EventArgs.Empty);", Cuerpo(DescargaCs(), "public string AvisoAlIniciarLote()"));
            // La marca aplicada solo se escribe al aplicar (Corregir y "Usar en el lote", o al restaurar con el GET).
            Assert.Single(Regex.Matches(ControlCs(), @"hfMarcaImpAplicada\.Value = "));
            Assert.Single(Regex.Matches(DescargaCs(), @"hfMarcaDescAplicada\.Value = "));

            // Y cada uno incorpora solo lo que esa pestaña tenía a la vista con la marca de la sesión.
            foreach (var (cs, campo, servicio) in new[]
            {
                (ControlCs(), "hfMarcaImpAplicada", "ArmadoLoteImportado.IncorporarAlLote("),
                (DescargaCs(), "hfMarcaDescAplicada", "ArmadoDescarga.IncorporarAlLote("),
            })
            {
                string cuerpo = Cuerpo(cs, "public void IncorporarAlLote(LoteDespachos lote)");
                int vigente = Pos(cuerpo, "if (!RegistroDespachoReglas.AplicadaVigente(Request.Form[" + campo + ".UniqueID], MarcaAplicadaEnSesion())) return;");
                Assert.True(vigente < Pos(cuerpo, servicio), campo);
            }
        }

        [Fact]
        public void Quitar_DesdeUnRecuadroViejo_AvisaSinBorrarLoDeLaOtraPestana()
        {
            foreach (var (cs, evento, boton, aviso, borra) in new[]
            {
                (ControlCs(), "protected void btnDescartarImportacion_Click(", "btnQuitarImportacion", "Avisar(aviso, \"warning\"); return; }", "Descartada?.Invoke"),
                (DescargaCs(), "protected void btnDescartarDescarga_Click(", "btnQuitarDescarga", "MostrarMensajeDescarga(aviso, \"warning\"); return; }", "Limpiar();"),
            })
            {
                string cuerpo = Cuerpo(cs, evento);
                int compara = Pos(cuerpo, "if (sender == " + boton + " && AvisoAlIniciarLote() is string aviso) { " + aviso);
                Assert.True(compara < Pos(cuerpo, borra), boton);
            }
        }

        // ── Ronda 1 de E6-25 ────────────────────────────────────────────────────────────

        [Fact]
        public void ElFormulario_SiempreSubeArchivos_AunqueElFileUploadNoSeDibuje()
        {
            // Con la importación aplicada (F5 u otra pestaña) la zona de subida no se dibuja y el FileUpload no pone el
            // multipart; "Quitar importación" la vuelve a mostrar por AJAX y "Leer documentos" llegaría sin archivo.
            Assert.Contains("pnlSubirDocumentos.Visible = !ucImportacionDocumentos.Aplicada;", PaginaCs());
            Assert.Contains("Page.Form.Enctype = \"multipart/form-data\";", Cuerpo(PaginaCs(), "protected override void OnPreRender(EventArgs e)"));
            // Los cuatro FileUpload de la página y de sus controles dependen de él.
            foreach (var (marcado, id) in new[] { (Pagina(), "fileDocumentosPedido"), (ConfiguracionBase(), "fileFacturaBase"), (ConfiguracionBase(), "fileCPICBase"), (Descarga(), "fileProgramacionDescarga") })
                Assert.Contains("<asp:FileUpload ID=\"" + id + "\"", marcado);
        }

        [Fact]
        public void TrasUnError_LaVistaPreviaQuedaConLaMarcaDeLaSesion()
        {
            // Aplicar/Preparar pudo registrar altas (cambia la importación de la sesión) antes de fallar: el reintento no debe
            // dar un falso "cambió en otra pestaña".
            foreach (string evento in new[] { "protected void btnAplicarImportacion_Click(", "protected void btnCorregirImportacion_Click(" })
            {
                string cuerpo = Cuerpo(ControlCs(), evento);
                string captura = cuerpo.Substring(Pos(cuerpo, "catch (Exception ex)"));
                Assert.True(Pos(captura, "TrasError();") < Pos(captura, "Avisar("), evento);
            }
            string tras = Cuerpo(ControlCs(), "private void TrasError()");
            Assert.Contains("ConductoresImportados = null;", tras);
            // Siempre se vuelve a mostrar desde la sesión: las filas enlazadas antes de Aplicar ya no son las de la importación.
            Assert.Contains("if (Actual != null) Mostrar(Actual);", tras);
            Assert.DoesNotContain("hfMarcaImp.Value", tras);
        }

        [Fact]
        public void LasFilasReenlazadas_MarcanEnRojoLoQueSigueSinElegir_SegunLoEnviado()
        {
            string pintar = Cuerpo(ControlCs(), "protected override void OnPreRender(EventArgs e)");
            Assert.Contains("else if (_vigente && !_mostrada && pnlVistaPreviaImportacion.Visible) MarcarPorElegir();", pintar);
            string marcar = Cuerpo(ControlCs(), "private void MarcarPorElegir()");
            Assert.Contains("new[] { \"ddlConductorImp\", \"ddlTractoImp\", \"ddlProgramadoImp\" }", marcar);
            Assert.Contains("ddl.CssClass.Replace(\" is-invalid\", \"\") + (ddl.SelectedValue == (id == \"ddlProgramadoImp\" ? \"\" : \"0\") ? \" is-invalid\" : \"\")", marcar);
        }

        [Fact]
        public void AvisoSinGuias_ListaLosPrimerosCincoIgnorados()
        {
            const string sinGuias = "No se encontraron guías de remisión ni packing list legibles.";
            Assert.Equal(sinGuias, RegistroDespachoReglas.AvisoSinGuias(new List<string>()));
            Assert.Equal(sinGuias + " a · b", RegistroDespachoReglas.AvisoSinGuias(new List<string> { "a", "b" }));
            Assert.Equal(sinGuias + " 1 · 2 · 3 · 4 · 5 … y 2 más.", RegistroDespachoReglas.AvisoSinGuias(new List<string> { "1", "2", "3", "4", "5", "6", "7" }));
            Assert.Equal(sinGuias + " 1 · 2 · 3 · 4 · 5", RegistroDespachoReglas.AvisoSinGuias(new List<string> { "1", "2", "3", "4", "5" }));
        }

        [Fact]
        public void LosAvisosDeOtraPestana_DicenQueHacer()
        {
            Assert.Equal("La importación cambió en otra pestaña: continúe en esa pestaña, o recargue esta página para retomarla (la configuración base se reiniciará).",
                RegistroDespachoReglas.AvisoImportacionEnOtraPestana);
            Assert.Equal("La descarga cambió en otra pestaña: continúe en esa pestaña.", RegistroDespachoReglas.AvisoDescargaEnOtraPestana);
        }

        [Fact]
        public void ElAvisoDelLoteIniciado_DiceSiEsDescarga()
        {
            Assert.Contains("tracto(s) {(LoteActual.TipoOperacion == \"DESCARGA\" ? \"con su descarga\" : \"importados\")}", Cuerpo(PaginaCs(), "private void IniciarLote()"));
        }
    }
}
