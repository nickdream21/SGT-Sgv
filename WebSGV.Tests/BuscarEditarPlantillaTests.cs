using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static WebSGV.Tests.AccesibilidadMarcadoTests;

namespace WebSGV.Tests
{
    /// <summary>
    /// Pantallas de buscar y editar con la plantilla común (E5-17, fase 3 de M-073): sgv-panel, sgv-tabla,
    /// sgv-paginador, filtros en details.sgv-filtros, cifras en una línea (sgv-resumen) en vez de tarjetas, sellos
    /// sgv-estado y el giratorio sgv-cargando. La lista de páginas crece con cada parte de E5-17 (a: GestionUsuarios,
    /// Auditoria y DashboardAdminSistema; b: ReporteCargas, ReportesOrdenesViaje, ReporteGps y
    /// LiquidacionesAprobadasContabilidad). Leen las páginas como texto sin comentarios, igual que
    /// <see cref="CatalogosPlantillaTests"/>.
    /// </summary>
    public class BuscarEditarPlantillaTests
    {
        /// <summary>Páginas con la plantilla (crece con E5-17c).</summary>
        public static readonly string[] Paginas =
        {
            "GestionUsuarios", "Auditoria", "DashboardAdminSistema",
            "ReporteCargas", "ReportesOrdenesViaje", "ReporteGps", "LiquidacionesAprobadasContabilidad"
        };

        /// <summary>
        /// Páginas con filtros (van en details.sgv-filtros). ReporteGps no: sus botones "Traer tractos" y "Activas de
        /// Vitapro" están entre los campos y hacen postback, así que plegarlo en el celular escondería su resultado.
        /// </summary>
        public static readonly string[] PaginasConFiltros =
        {
            "GestionUsuarios", "Auditoria", "ReporteCargas", "ReportesOrdenesViaje", "LiquidacionesAprobadasContabilidad"
        };

        /// <summary>
        /// style= permitidos por archivo (.aspx y .aspx.cs), en igualdad exacta: el ancho de una barra de progreso
        /// es un dato y se queda (llegan con E5-17b/c). Las páginas que no están aquí van con 0.
        /// </summary>
        public static readonly Dictionary<string, int> StylePermitidos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Views/ReporteCargas.aspx.cs"] = 1,   // Barra(): width:% del peso de cada cliente o producto
        };

        /// <summary>Clases retiradas en E5-17a y E5-17b: no vuelven en el marcado, el código, el script ni la hoja de la página.</summary>
        private static readonly string[] ClasesRetiradas =
        {
            "aud-stat-", "aud-section-", "aud-badge-", "aud-pager", "aud-form-label", "aud-table",
            "stat-card", "admin-kpi-", "quick-link", "fa-table", "fa-circle", "badge-info", "badge-danger",
            // E5-17b
            "rc-tarjeta", "rc-doc-", "rc-faltante", "badge-secondary", "summary-card", "summary-icon", "section-card",
            "section-header", "tab-btn", "btn-export", "btn-info-action", "alert-info-light", "badge-count", "table-reportes",
            "modal-header-info", "page-item", "page-link", "\"pagination", "fa-spinner", "fa-info-circle"
        };

        public static IEnumerable<object[]> Casos() => Paginas.Select(p => new object[] { p });

        public static IEnumerable<object[]> CasosConFiltros() => PaginasConFiltros.Select(p => new object[] { p });

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Aspx(string pagina) => PaginaDe("Views/" + pagina + ".aspx").Texto;

        private static string Cs(string pagina) => File.ReadAllText(Path.Combine(RaizWeb(), "Views", pagina + ".aspx.cs"));

        private static string SiHay(params string[] ruta)
        {
            string f = Path.Combine(RaizWeb(), Path.Combine(ruta));
            return File.Exists(f) ? File.ReadAllText(f) : string.Empty;
        }

        private static string Js(string pagina) => SiHay("Scripts", "paginas", pagina + ".js");

        private static string Css(string pagina) => SiHay("Content", "paginas", pagina + ".css");

        private static readonly Regex Grilla = new Regex(@"<asp:GridView\b(?<a>[^>]*)>(?<c>.*?)</asp:GridView\s*>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // ── (a) Sin estilos en línea ni anchos en el marcado ────────────────────────────────

        [Theory]
        [MemberData(nameof(Casos))]
        public void A_StyleSoloLosPermitidosYSinBloqueStyle(string pagina)
        {
            string aspx = Aspx(pagina), cs = Cs(pagina);
            foreach (var (archivo, texto) in new[] { ("Views/" + pagina + ".aspx", aspx), ("Views/" + pagina + ".aspx.cs", cs) })
            {
                int esperado = StylePermitidos.TryGetValue(archivo, out int n) ? n : 0;
                var hallados = Regex.Matches(texto, @"\bstyle\s*=", RegexOptions.IgnoreCase).Cast<Match>().ToList();
                Assert.True(hallados.Count == esperado, $"{archivo}: {hallados.Count} style= (se esperaban {esperado}).");
            }
            Assert.DoesNotMatch(new Regex(@"<style\b", RegexOptions.IgnoreCase), aspx);
            Assert.DoesNotMatch(new Regex(@"<style\b", RegexOptions.IgnoreCase), cs);
        }

        [Theory]
        [MemberData(nameof(Casos))]
        public void A2_SinAnchosNiAlineacionesEnLasColumnas(string pagina)
        {
            string texto = Aspx(pagina) + "\n" + Cs(pagina);
            Assert.Empty(Regex.Matches(texto, @"\b(Item|Header|Footer|Pager)Style-(Width|Height|Wrap|HorizontalAlign)\b").Cast<Match>().Select(m => m.Value));
            Assert.Empty(Regex.Matches(texto, @"\bHorizontalAlign\s*=").Cast<Match>().Select(m => m.Value));
        }

        [Theory]
        [MemberData(nameof(Casos))]
        public void A3_TodaGrillaConCellSpacingMenosUno(string pagina)
        {
            // Sin CellSpacing="-1" el GridView escribe style="border-collapse:collapse" en la tabla.
            foreach (Match g in Grilla.Matches(Aspx(pagina)))
                Assert.Contains("CellSpacing=\"-1\"", g.Groups["a"].Value);
        }

        // ── (b) Plantilla visual ─────────────────────────────────────────────────────────────

        [Theory]
        [MemberData(nameof(Casos))]
        public void B_PanelesSinTarjetasNiContenedorFluido(string pagina)
        {
            string aspx = Aspx(pagina);
            Assert.Matches(@"class=""sgv-panel[\s""]", aspx);
            Assert.DoesNotContain("card shadow-sm", aspx);
            Assert.DoesNotContain("card-header", aspx);
            Assert.DoesNotContain("container-fluid", aspx);
            Assert.False(InterfazFormalTests.TieneTarjetasKpi(aspx), "La página volvió a tener tarjetas KPI");
        }

        [Theory]
        [MemberData(nameof(CasosConFiltros))]
        public void B2_FiltrosPlegablesQueArrancanPlegadosEnElCelular(string pagina)
        {
            string aspx = Aspx(pagina);
            Match d = Regex.Match(aspx, @"<details\b[^>]*\bclass=""sgv-filtros""[^>]*>", RegexOptions.Singleline);
            Assert.True(d.Success, "Falta <details class=\"sgv-filtros\">");
            Assert.Contains(" open", d.Value);
            Assert.Contains("data-plegar-movil", d.Value);
            Assert.Contains("<span class=\"sgv-filtros-cuenta\"></span>", aspx);
            // El botón de buscar va dentro (btnBuscar, o btnBuscarX en páginas con varias búsquedas).
            Assert.Matches(@"\b(?:ID|id)=""btnBuscar\w*""", aspx.Substring(d.Index));
        }

        [Theory]
        [MemberData(nameof(Casos))]
        public void B3_ClasesRetiradasAusentes(string pagina)
        {
            string todo = Aspx(pagina) + "\n" + Cs(pagina) + "\n" + Js(pagina) + "\n" + Css(pagina);
            var halladas = ClasesRetiradas.Where(c => todo.IndexOf(c, StringComparison.Ordinal) >= 0).ToList();
            Assert.True(halladas.Count == 0, pagina + " todavía usa: " + string.Join(", ", halladas));
        }

        [Theory]
        [MemberData(nameof(Casos))]
        public void B4_TextUppercaseSoloEnCajasDeTexto(string pagina)
        {
            // Rótulos y encabezados en estilo oración (DN-28); text-uppercase solo pasa a mayúsculas lo que se escribe.
            string aspx = Aspx(pagina);
            foreach (Match m in Regex.Matches(aspx, @"\btext-uppercase\b"))
            {
                int abre = aspx.LastIndexOf('<', m.Index);
                Assert.True(abre >= 0 && aspx.Substring(abre).StartsWith("<asp:TextBox", StringComparison.OrdinalIgnoreCase),
                    pagina + ": text-uppercase fuera de un asp:TextBox (línea " + (aspx.Take(m.Index).Count(c => c == '\n') + 1) + ")");
            }
            Assert.DoesNotContain("text-uppercase", Cs(pagina));
        }

        [Theory]
        [MemberData(nameof(Casos))]
        public void B5_CargandoConLaClaseComun(string pagina)
        {
            // Donde la página pide datos sin postback (fetch o $.ajax), el aviso de espera es sgv-cargando.
            string js = Js(pagina);
            if (!Regex.IsMatch(js, @"\bfetch\(|\$\.ajax\(")) return;
            Assert.True((Aspx(pagina) + js).Contains("sgv-cargando"), pagina + " pide datos y no usa sgv-cargando");
            Assert.DoesNotContain("spinner-border", Aspx(pagina));
        }

        // ── (c) Base común ───────────────────────────────────────────────────────────────────

        [Fact]
        public void C_BaseComun_resumenCargandoYPlegadoEnElCelular()
        {
            string css = SiHay("Content", "sgv-base.css");
            Assert.Contains(".sgv-resumen {", css);
            Assert.Contains(".sgv-resumen > span + span::before", css);
            Assert.Contains(".sgv-cargando {", css);
            Assert.Contains("@keyframes sgv-girar", css);
            Assert.Contains("prefers-reduced-motion: reduce", css);
            // El nombre de la línea de resumen no cuenta como tarjeta KPI.
            Assert.False(InterfazFormalTests.EsClaseKpi("sgv-resumen"));

            string js = SiHay("Scripts", "paginas", "SiteMaster.js");
            Assert.Contains("details.sgv-filtros[data-plegar-movil], details.sgv-plegable[data-plegar-movil]", js);
            Assert.Contains("SGV.plegarFiltros = function", js);
            Assert.Contains("SGV.htmlCargando = function", js);
            // El plegado al cargar no corre tras un parcial (no cierra lo que el usuario abrió).
            Assert.DoesNotContain("add_endRequest(plegarAlCargar)", js);
            Assert.DoesNotContain("add_endRequest(alCargar)", js);
        }

        [Fact]
        public void D0_GestionUsuarios_cadaAccionDeFilaPasaPorSuMetodoConGuardas()
        {
            // El argumento de fila lo elige el cliente: cada acción tiene que entrar por el método que
            // revalida fila propia, reautenticación y último administrador (no directo al que escribe).
            string cs = Cs("GestionUsuarios");
            Assert.Matches(@"case AccionFilaUsuarioTipo\.Editar:\s*CargarUsuarioEnModal\(", cs);
            Assert.Matches(@"case AccionFilaUsuarioTipo\.Estado:\s*(//[^\n]*\n\s*)*CambiarEstadoUsuario\(", cs);
            Assert.Matches(@"case AccionFilaUsuarioTipo\.QuitarSegundoFactor:\s*QuitarSegundoFactor\(", cs);
            Assert.Matches(@"case AccionFilaUsuarioTipo\.Contrasena:\s*RestablecerContrasenaDeFila\(", cs);
            Assert.DoesNotMatch(@"case AccionFilaUsuarioTipo\.\w+:\s*PrepararResetPassword\(", cs);
        }

        // ── (d) GestionUsuarios: grilla sin ViewState y acciones por el botón oculto ──────────

        [Fact]
        public void D_GestionUsuarios_grillaPaginadaSinViewStateNiControlesDeServidor()
        {
            string aspx = Aspx("GestionUsuarios");
            var grillas = Grilla.Matches(aspx).Cast<Match>().ToList();
            Assert.Single(grillas);
            string a = grillas[0].Groups["a"].Value, c = grillas[0].Groups["c"].Value;
            Assert.Contains("EnableViewState=\"false\"", a);
            Assert.Contains("AllowPaging=\"true\"", a);
            Assert.Contains("PageSize=\"25\"", a);
            Assert.Contains("OnPageIndexChanging=", a);
            Assert.DoesNotContain("OnRowCommand", a);
            Assert.Contains("CssClass=\"sgv-paginador\"", c);
            Assert.Contains("sgv-tabla", a);
            Assert.DoesNotMatch(new Regex(@"<asp:(LinkButton|Button|ImageButton|TextBox|DropDownList|CheckBox)\b", RegexOptions.IgnoreCase), c);

            string cs = Cs("GestionUsuarios");
            Assert.Contains("override void OnPreRender(", cs);
            Assert.Contains("!_grillaEnlazada && !Response.IsRequestBeingRedirected", cs);
            Assert.Contains("ListaCatalogo.IndiceValido(", cs);
            Assert.Contains("AdminUsuariosReglas.TryLeerAccionFila(argumento,", cs);
            Assert.DoesNotContain("_RowCommand", cs);
            Assert.DoesNotContain("CommandName", aspx);
        }

        [Fact]
        public void D2_GestionUsuarios_cuatroAccionesPorElBotonOcultoConSuConfirmacion()
        {
            string aspx = Aspx("GestionUsuarios");
            string c = Grilla.Match(aspx).Groups["c"].Value;
            var botones = Regex.Matches(c, @"<button\b(?:(?><%.*?%>)|[^<>])*?\bdata-accion=""enviarFila""(?:(?><%.*?%>)|[^<>])*>", RegexOptions.Singleline)
                .Cast<Match>().Select(m => m.Value).ToList();
            Assert.Equal(4, botones.Count);
            foreach (string tipo in new[] { "Editar", "Estado", "Contrasena", "QuitarSegundoFactor" })
                Assert.Single(botones, b => b.Contains("data-argumento='<%#: ArgumentoFila(AccionFilaUsuarioTipo." + tipo + ","));
            Assert.Contains("data-confirmar='<%#: ConfirmarCambioEstado(", botones.Single(b => b.Contains(".Estado,")));
            Assert.Contains("data-confirmar='<%#: ConfirmarQuitarSegundoFactor(", botones.Single(b => b.Contains(".QuitarSegundoFactor,")));
            Assert.Contains("data-confirmar-peligro=\"true\"", botones.Single(b => b.Contains(".QuitarSegundoFactor,")));

            Match boton = Regex.Match(aspx, @"<asp:Button\b[^>]*\bID=""btnAccionFila""[^>]*>", RegexOptions.Singleline);
            Assert.True(boton.Success, "Falta <asp:Button ID=\"btnAccionFila\">");
            Assert.Contains("CausesValidation=\"false\"", boton.Value);
            Assert.Contains("OnClick=\"btnAccionFila_Click\"", boton.Value);
            Assert.True(boton.Index < Grilla.Match(aspx).Index, "btnAccionFila va fuera de la grilla");
            Assert.Contains("hfArgumentoFila: '<%= hfArgumentoFila.ClientID %>'", aspx);
            Assert.Contains("btnAccionFila: '<%= btnAccionFila.ClientID %>'", aspx);
        }

        [Fact]
        public void D3_GestionUsuarios_cadaAccionRevalidaEnElServidor()
        {
            string cs = Cs("GestionUsuarios");
            int i = cs.IndexOf("protected void btnAccionFila_Click(", StringComparison.Ordinal);
            Assert.True(i > 0);
            string cuerpo = cs.Substring(i, cs.IndexOf("\n        }", i, StringComparison.Ordinal) - i);
            // El usuario tiene que existir y el argumento se borra tras leerlo.
            Assert.Contains("hfArgumentoFila.Value = \"\";", cuerpo);
            Assert.Contains("SELECT 1 FROM Usuarios WHERE idUsuario = @id", cuerpo);
            Assert.Contains("CargarUsuarioEnModal(idUsuario)", cuerpo);
            Assert.Contains("CambiarEstadoUsuario(", cuerpo);
            Assert.Contains("QuitarSegundoFactor(idUsuario)", cuerpo);
            Assert.Contains("RestablecerContrasenaDeFila(idUsuario)", cuerpo);

            // Contraseña: fila propia y reautenticación, como antes en el RowCommand.
            int r = cs.IndexOf("private void RestablecerContrasenaDeFila(", StringComparison.Ordinal);
            string reset = cs.Substring(r, cs.IndexOf("\n        }", r, StringComparison.Ordinal) - r);
            Assert.Contains("AdminUsuariosReglas.EsFilaPropia(", reset);
            Assert.Contains("ReautenticacionVigente()", reset);
            // Estado: estado destino explícito, reautenticación al activar cuentas de administración y la guarda del servicio.
            Assert.Contains("EstadoActivoService.TryLeerArgumento(argumento", cs);
            Assert.Contains("AdminUsuariosReglas.RequiereReautenticacionAlActivar(", cs);
            Assert.Contains("UsuarioEstadoService.CambiarEstado(idUsuario, nuevoActivo, SesionHelper.IdUsuarioActual)", cs);
            // Quitar el segundo factor: fila propia y reautenticación.
            int q = cs.IndexOf("private void QuitarSegundoFactor(", StringComparison.Ordinal);
            string quitar = cs.Substring(q, cs.IndexOf("SegundoFactorService.Quitar(", q, StringComparison.Ordinal) - q);
            Assert.Contains("AdminUsuariosReglas.EsFilaPropia(", quitar);
            Assert.Contains("ReautenticacionVigente()", quitar);
        }

        [Fact]
        public void D4_GestionUsuarios_filtrosAplicadosSoloDeBuscarYLimpiar()
        {
            string aspx = Aspx("GestionUsuarios");
            string cs = Cs("GestionUsuarios");
            foreach (string campo in new[] { "hfFiltroTexto", "hfFiltroRol", "hfFiltroEstado" })
            {
                Assert.Contains("ID=\"" + campo + "\"", aspx);
                var escrituras = Regex.Matches(cs, campo + @"\.Value\s*=(?!=)").Cast<Match>().ToList();
                Assert.Equal(2, escrituras.Count);
                foreach (Match m in escrituras)
                {
                    bool dentro = new[] { "void btnBuscar_Click(", "void btnLimpiar_Click(" }.Any(n =>
                    {
                        int inicio = cs.IndexOf(n, StringComparison.Ordinal);
                        int siguiente = cs.IndexOf("\n        }", inicio, StringComparison.Ordinal);
                        return inicio > 0 && m.Index > inicio && m.Index < siguiente;
                    });
                    Assert.True(dentro, campo + " se escribe fuera de btnBuscar_Click y btnLimpiar_Click");
                }
            }
            // Se revalidan al leerlos: texto recortado, rol de la lista y estado "", "0" o "1".
            Assert.Contains("ListaCatalogo.RecortarBusqueda(hfFiltroTexto.Value)", cs);
            Assert.Contains("ddlFiltroRol.Items.FindByValue(hfFiltroRol.Value", cs);
            Assert.Contains("AdminUsuariosReglas.FiltroEstadoValido(hfFiltroEstado.Value)", cs);
            Assert.Contains("DbHelper.PatronContiene(buscar)", cs);
        }

        [Fact]
        public void D5_GestionUsuarios_conductoresSinViewStateLlenadosAntesDeLeerElEnvio()
        {
            string aspx = Aspx("GestionUsuarios");
            Match ddl = Regex.Match(aspx, @"<asp:DropDownList\b[^>]*\bID=""ddlConductor""[^>]*>");
            Assert.True(ddl.Success);
            Assert.Contains("EnableViewState=\"false\"", ddl.Value);

            string cs = Cs("GestionUsuarios");
            int init = cs.IndexOf("protected override void OnInit(", StringComparison.Ordinal);
            Assert.True(init > 0, "ddlConductor se llena en OnInit (antes de LoadPostData)");
            string cuerpo = cs.Substring(init, cs.IndexOf("\n        }", init, StringComparison.Ordinal) - init);
            Assert.Contains("SecurityHelper.EsAdminSistema()", cuerpo);
            Assert.Contains("CargarConductores();", cuerpo);
            int load = cs.IndexOf("protected void Page_Load(", StringComparison.Ordinal);
            string pageLoad = cs.Substring(load, cs.IndexOf("\n        }", load, StringComparison.Ordinal) - load);
            Assert.DoesNotContain("CargarConductores();", pageLoad);
        }

        // ── (e) Auditoria ───────────────────────────────────────────────────────────────────

        [Fact]
        public void E_Auditoria_filtroDeAccionesDesdeLaBdYSellosSinIcono()
        {
            string aspx = Aspx("Auditoria");
            string cs = Cs("Auditoria");
            Match ddl = Regex.Match(aspx, @"<asp:DropDownList\b[^>]*\bID=""ddlAccion""[^>]*/>");
            Assert.True(ddl.Success, "ddlAccion sin opciones fijas en el marcado");
            Assert.Contains("ConsultaAuditoriaService.ObtenerAcciones()", cs);
            Assert.Contains("ConsultaAuditoriaService.NombreAccion(accion), accion", cs);
            Assert.Contains("ddlAccion.SelectedItem?.Text", cs);   // el Excel nombra la acción como el filtro

            string c = Grilla.Match(aspx).Groups["c"].Value;
            Assert.Contains("<%# SelloAccion(Eval(\"Accion\")) %>", c);
            Assert.Contains("EstadoUiHelper.Sello(ConsultaAuditoriaService.NombreAccion(accion), EstadoUiHelper.TonoAccionAuditoria(accion))", cs);
            Assert.DoesNotContain("ObtenerIconoAccion", aspx + cs);
            Assert.Contains("CssClass=\"sgv-paginador\"", c);

            Match boton = Regex.Match(aspx, @"<button\b(?:(?><%.*?%>)|[^<>])*?\baud-btn-detalle\b(?:(?><%.*?%>)|[^<>])*>", RegexOptions.Singleline);
            Assert.True(boton.Success);
            Assert.Contains("sgv-btn-icono", boton.Value);
            Assert.Matches(@"\baria-label\s*=\s*['""][^'""]+", boton.Value);

            // Modal sin style=: las secciones se muestran con la clase d-none.
            Assert.Matches(@"class=""mb-3 d-none"" id=""seccionAnteriores""", aspx);
            Assert.Matches(@"class=""mb-3 d-none"" id=""seccionNuevos""", aspx);
            Assert.Contains("toggleClass('d-none', !texto)", Js("Auditoria"));
            Assert.Contains("id=\"detalleCargando\" class=\"sgv-cargando d-none\" role=\"status\"", aspx);
            Assert.Contains("litResumen.Text = ListaCatalogo.Resumen(pagina.IndicePagina, pagina.Total, pagina.Total, \"\")", cs);
        }

        [Fact]
        public void E2_Auditoria_cifrasEnUnaLinea()
        {
            string html = WebSGV.Views.Auditoria.HtmlCifras(6523, 1, 8, 21);
            Assert.Equal("<p class=\"sgv-resumen\"><span><strong>6,523</strong> registros</span><span><strong>1</strong> hoy</span>" +
                         "<span><strong>8</strong> usuarios en los últimos 7 días</span><span><strong>21</strong> tablas</span></p>",
                html.Replace("6.523", "6,523"));
            Assert.Contains("<strong>1</strong> usuario en los últimos 7 días", WebSGV.Views.Auditoria.HtmlCifras(1, 0, 1, 1));
            Assert.Contains("<strong>1</strong> registro<", WebSGV.Views.Auditoria.HtmlCifras(1, 0, 1, 1));
        }

        // ── (f) DashboardAdminSistema ───────────────────────────────────────────────────────

        [Fact]
        public void F_Panel_resumenEnUnaLineaYSellosDeAccion()
        {
            string aspx = Aspx("DashboardAdminSistema");
            Assert.Contains("<asp:Literal ID=\"litResumen\" runat=\"server\" />", aspx);
            Assert.Contains("<%# SelloAccion(Eval(\"Accion\")) %>", aspx);
            Assert.DoesNotContain("badge", aspx);
            Assert.Equal(3, Regex.Matches(aspx, @"<section class=""sgv-panel h-100"">").Count);

            string conTodo = WebSGV.Views.DashboardAdminSistema.HtmlResumen(95, 89, 6, 12);
            Assert.Equal("<p class=\"sgv-resumen\"><span><strong>95</strong> usuarios: <strong>89</strong> activos y " +
                         "<strong>6</strong> inactivos</span><span><strong>12</strong> eventos de auditoría hoy</span></p>", conTodo);
            string sinAuditoria = WebSGV.Views.DashboardAdminSistema.HtmlResumen(1, 1, 0, null);
            Assert.Equal("<p class=\"sgv-resumen\"><span><strong>1</strong> usuario: <strong>1</strong> activo y " +
                         "<strong>0</strong> inactivos</span></p>", sinAuditoria);
        }

        // ── (g) ReporteCargas (E5-17b) ──────────────────────────────────────────────────────

        /// <summary>El &lt;details&gt; abierto de clase <paramref name="clase"/> que contiene <paramref name="marca"/>.</summary>
        private static Match Detalle(string aspx, string clase, string marca)
        {
            foreach (Match d in Regex.Matches(aspx, @"<details\b[^>]*\bclass=""" + clase + @"""[^>]*>.*?</details>", RegexOptions.Singleline))
                if (d.Value.Contains(marca)) return d;
            return Match.Empty;
        }

        [Fact]
        public void G_ReporteCargas_descargasFueraDeLosFiltrosYGraficosPlegablesQueSeDibujanAlAbrir()
        {
            string aspx = Aspx("ReporteCargas");
            Match filtros = Detalle(aspx, "sgv-filtros", "ID=\"btnBuscar\"");
            Assert.True(filtros.Success);
            // Excel y PDF en el encabezado: se ven con los filtros plegados y siguen pasando por LeerFiltros.
            Assert.DoesNotContain("btnExcel", filtros.Value);
            Assert.DoesNotContain("btnPdf", filtros.Value);
            int acciones = aspx.IndexOf("sgv-encabezado-acciones", StringComparison.Ordinal);
            Assert.True(acciones > 0 && aspx.IndexOf("ID=\"btnExcel\"", StringComparison.Ordinal) > acciones
                        && aspx.IndexOf("ID=\"btnPdf\"", StringComparison.Ordinal) < filtros.Index);
            string cs = Cs("ReporteCargas");
            Assert.Matches(@"void Exportar\(bool pdf\)\s*\{\s*FiltroCargas filtro;\s*if \(!LeerFiltros\(out filtro\)\) return;", cs);
            // La búsqueda automática al abrir sigue.
            Assert.Matches(@"if \(!IsPostBack\)\s*\{\s*CargarClientes\(\);\s*PonerMes\(FechaHelper\.Hoy\(\)\);\s*Buscar\(\);", cs);

            // Gráficos y documentación: plegables que arrancan cerrados en el celular.
            Match graficos = Regex.Match(aspx, @"<details\b[^>]*\bid=""rcGraficos""[^>]*>");
            Assert.True(graficos.Success);
            foreach (string a in new[] { "class=\"sgv-plegable\"", " open", "data-plegar-movil" })
                Assert.Contains(a, graficos.Value);
            Assert.True(Detalle(aspx, "sgv-plegable", "litDocumentacion").Success);
            Assert.Contains("data-plegar-movil", Detalle(aspx, "sgv-plegable", "litDocumentacion").Value);
            Assert.Contains("rcChartPeriodo", Detalle(aspx, "sgv-plegable", "rcChartClientes").Value);
            // Por cliente y Por producto quedan a la vista (sin plegar).
            Assert.False(Detalle(aspx, "sgv-plegable", "litClientes").Success);
            Assert.False(Detalle(aspx, "sgv-plegable", "litProductos").Success);

            // Un canvas oculto mide 0: se dibuja al abrir el bloque y al mostrar la pestaña, si se ve.
            string js = Js("ReporteCargas");
            Assert.Contains("graficos.addEventListener('toggle'", js);
            Assert.Contains("if (resumenVisible && (!graficos || graficos.open)) dibujar();", js);

            Assert.Contains("EstadoUiHelper.Sello(\"Anulado\", TonoEstado.Neutro)", cs);
            Assert.DoesNotContain("HtmlTarjetas", cs);
        }

        [Fact]
        public void G2_ReporteCargas_cifrasEnUnaLineaConVariacion()
        {
            var tot = new WebSGV.Services.Reportes.ReporteCargasService.Totales
            {
                Despachos = 12, Pedidos = 1, Clientes = 2, Bolsas = 1500m, KgBruto = 30000m, FacturadoUsd = 1000m, FacturadoPen = 0m
            };
            var ant = new WebSGV.Services.Reportes.ReporteCargasService.Totales
            {
                Despachos = 10, Pedidos = 1, Clientes = 2, Bolsas = 2000m, KgBruto = 30000m
            };
            var docs = new WebSGV.Services.Reportes.ReporteCargasService.ResumenDocumentos
            {
                Estado = new WebSGV.Services.Despachos.EstadoDocumental { Despachos = 12 }, Pendientes = 2, Atrasados = 1
            };
            string html = WebSGV.Views.ReporteCargas.HtmlResumen(tot, ant, docs);
            Assert.StartsWith("<p class=\"sgv-resumen\"><span><strong>12</strong> viajes <span class=\"rc-var rc-var-sube\"", html);
            Assert.Contains("&#x25B2; 20 %</span></span>", html);                       // 12 frente a 10
            Assert.Contains("<span><strong>1</strong> pedido <span class=\"rc-var\" title=\"Igual", html);
            Assert.Contains("<span><strong>2</strong> clientes</span>", html);
            Assert.Contains("bolsas <span class=\"rc-var rc-var-baja\"", html);         // 1 500 frente a 2 000
            Assert.Contains("&#x25BC; 25 %</span>", html);
            Assert.Contains("<span>Facturado <strong>US$ 1", html);
            Assert.Contains("<strong>2</strong> con documentos pendientes <span class=\"rc-var rc-var-baja\">(1 con m&#xE1;s de 3 d&#xED;as)</span>", html);
            Assert.EndsWith("</span></p>", html);
            Assert.False(InterfazFormalTests.TieneTarjetasKpi(html));

            // Sin comparación (período anterior chico): sin flechas; sin facturas ni pendientes, dicho en texto.
            string sinComparar = WebSGV.Views.ReporteCargas.HtmlResumen(
                new WebSGV.Services.Reportes.ReporteCargasService.Totales { Despachos = 1 }, null,
                new WebSGV.Services.Reportes.ReporteCargasService.ResumenDocumentos { Estado = new WebSGV.Services.Despachos.EstadoDocumental { Despachos = 1 } });
            Assert.DoesNotContain("rc-var", sinComparar);
            Assert.Contains("<span><strong>1</strong> viaje</span>", sinComparar);
            Assert.Contains("<span>Sin facturar</span>", sinComparar);
            Assert.Contains("<span>Documentaci&#xF3;n completa</span>", sinComparar);
        }

        // ── (h) ReportesOrdenesViaje (E5-17b) ───────────────────────────────────────────────

        [Fact]
        public void H_ReportesOrdenesViaje_pestanasComunesConLosMismosIdsYSeccionesConDNone()
        {
            string aspx = Aspx("ReportesOrdenesViaje");
            foreach (var (id, tab) in new[] { ("tabLiquidaciones", "liquidaciones"), ("tabViajesActivos", "viajesActivos"), ("tabPersonalizado", "personalizado") })
            {
                Match b = Regex.Match(aspx, @"<button\b[^>]*\bid=""" + id + @"""[^>]*>");
                Assert.True(b.Success, "Falta la pestaña " + id);
                Assert.Matches(@"class=""sgv-tab( activo)?""", b.Value);
                Assert.Contains("data-tab=\"" + tab + "\"", b.Value);
                Assert.Contains("data-accion=\"cambiarTab\"", b.Value);
            }
            Assert.Contains("<div class=\"sgv-tabs\" role=\"tablist\"", aspx);
            Assert.Contains("<div id=\"seccionViajesActivos\" class=\"d-none\">", aspx);
            Assert.Contains("<div id=\"seccionPersonalizado\" class=\"d-none\">", aspx);
            Assert.Contains("<asp:HiddenField ID=\"hdnPestana\" runat=\"server\"", aspx);

            string js = Js("ReportesOrdenesViaje");
            Assert.Contains("$(p.seccion).toggleClass('d-none', !activa);", js);
            Assert.Contains("$(p.tab).toggleClass('activo', activa)", js);
            Assert.DoesNotContain(".show()", js);
            Assert.DoesNotContain(".hide()", js);

            // Totales en una línea y en estilo oración; sin el método de estado que nadie usaba.
            Assert.Contains("<p class=\"sgv-resumen mb-1\">", aspx);
            Assert.DoesNotContain("TOTAL", aspx);
            Assert.DoesNotContain("ObtenerClaseEstado", Cs("ReportesOrdenesViaje"));
            Assert.Contains("lblResumenFactor.Text = factorConversion > 0", Cs("ReportesOrdenesViaje"));
        }

        [Fact]
        public void H2_ReportesOrdenesViaje_estadoDelViajeFijoYCombosPersonalizadosSinViewState()
        {
            string aspx = Aspx("ReportesOrdenesViaje");
            string cs = Cs("ReportesOrdenesViaje");
            string js = Js("ReportesOrdenesViaje");

            // "Viajes activos": texto fijo; el servidor usa la constante y el script manda ABIERTO.
            Assert.DoesNotContain("ddlEstadoViaje", aspx + cs + js);
            Assert.Contains("Abiertos (sin liquidar)", aspx);
            Assert.Contains("private const string EstadoViajeAbierto = \"ABIERTO\";", cs);
            Assert.Contains("&estadoViaje=ABIERTO", js);

            // Los cuatro combos Pers* sin ViewState: solo los lee el script (GET ?action=reportePersonalizado).
            foreach (string id in new[] { "ddlPersEstado", "ddlPersConductor", "ddlPersCliente", "ddlPersOrden" })
            {
                Match ddl = Regex.Match(aspx, @"<asp:DropDownList\b[^>]*\bID=""" + id + @"""[^>]*>");
                Assert.True(ddl.Success, "Falta " + id);
                Assert.Contains("EnableViewState=\"false\"", ddl.Value);
                Assert.Contains("SGV." + id + ")", js);
            }
            Assert.Empty(Regex.Matches(cs, @"\bddlPers\w+\.(?:SelectedValue|SelectedItem|SelectedIndex|Text)\b(?!\s*=[^=])")
                .Cast<Match>().Select(m => m.Value));

            // Se llenan en OnInit (antes de leer el envío) con sesión de admin o supervisor, y no en las exportaciones.
            int init = cs.IndexOf("protected override void OnInit(", StringComparison.Ordinal);
            Assert.True(init > 0);
            string cuerpo = cs.Substring(init, cs.IndexOf("\n        }", init, StringComparison.Ordinal) - init);
            Assert.Contains("SecurityHelper.TieneSesionActiva() && SecurityHelper.EsAdminOSupervisor()", cuerpo);
            Assert.Contains("string.IsNullOrEmpty(Request.QueryString[\"action\"])", cuerpo);
            foreach (string metodo in new[] { "CargarEstadosPersonalizado();", "CargarConductoresPersonalizado();", "CargarClientesPersonalizado();" })
            {
                Assert.Contains(metodo, cuerpo);
                Assert.Single(Regex.Matches(cs, Regex.Escape(metodo)).Cast<Match>());
            }
        }

        // ── (i) ReporteGps (E5-17b) ─────────────────────────────────────────────────────────

        [Fact]
        public void I_ReporteGps_panelesSinFiltrosPlegablesYAvisoConLaClaseComun()
        {
            string aspx = Aspx("ReporteGps");
            Assert.DoesNotContain("sgv-filtros", aspx);   // sus postbacks están entre los campos
            Assert.Equal(3, Regex.Matches(aspx, @"<section class=""sgv-panel mb-4"">").Count);
            Assert.Contains("<div id=\"avisoGenerando\" class=\"sgv-cargando d-none\" role=\"status\">", aspx);
            Assert.DoesNotContain("spinner-border", aspx);
            Assert.DoesNotContain("thead-light", aspx);
            Assert.DoesNotContain("font-weight-bold mb-0", aspx);
            Assert.Contains("<label class=\"sgv-etiqueta mb-0\" for=\"<%= txtPlacas.ClientID %>\">", aspx);
            // La descarga de cada fila sigue por el RowCommand (EnvioUnicoTests).
            Match g = Grilla.Match(aspx);
            Assert.Contains("OnRowCommand=\"gvResultados_RowCommand\"", g.Groups["a"].Value);
            Assert.Contains("CommandName=\"Descargar\"", g.Groups["c"].Value);
            Assert.Contains("$('#avisoGenerando').removeClass('d-none');", Js("ReporteGps"));
        }

        // ── (j) LiquidacionesAprobadasContabilidad (E5-17b) ─────────────────────────────────

        [Fact]
        public void J_Contabilidad_paginadorComunYFiltrosQueSePlieganTrasBuscarConUnBoton()
        {
            string aspx = Aspx("LiquidacionesAprobadasContabilidad");
            string js = Js("LiquidacionesAprobadasContabilidad");
            Assert.Contains("<div class=\"sgv-paginador\" id=\"paginadorLiquidaciones\"></div>", aspx);
            Assert.DoesNotContain("pagination", aspx + js);
            Assert.Contains("'<span aria-current=\"page\"><span aria-hidden=", js);
            Assert.Contains("<span class=\"sr-only\">", js);
            Assert.Contains("'<button type=\"button\" data-accion=\"irPagina\" data-pagina=\"' + pagina + '\"'", js);
            Assert.Contains("div.sgv-paginador {", SiHay("Content", "sgv-base.css"));
            Assert.Contains(".sgv-paginador button,", SiHay("Content", "sgv-base.css"));

            // Solo la búsqueda de un botón pliega los filtros (no la que corre mientras se escribe).
            Assert.Contains("if (esManual) SGV.plegarFiltros(document.getElementById('btnBuscarLiquidaciones'));", js);
            Assert.Single(Regex.Matches(js, @"SGV\.plegarFiltros\(").Cast<Match>());
            Assert.Contains("<div class=\"sgv-cargando\" role=\"status\" aria-live=\"polite\">Cargando el documento…</div>", aspx);

            // Mismos WebMethods de siempre.
            foreach (string metodo in new[] { "LiquidacionesAprobadasContabilidad.aspx/ObtenerLiquidacionesAprobadasContabilidad",
                                              "LiquidacionesAprobadasContabilidad.aspx/BuscarConductores",
                                              "LiquidacionesPendientes.aspx/ObtenerDetalleLiquidacion",
                                              "LiquidacionesPendientes.aspx/ObtenerUrlPdfOrdenViaje" })
                Assert.Contains("'" + metodo + "'", js);
        }
    }
}
