using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Views;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E5-9: las listas de solo lectura del tablero de seguimiento, del panel del conductor y de
    /// ReportesOrdenesViaje no guardan ViewState, se vuelven a enlazar en todo postback, el tablero
    /// sincroniza los despachos solo al abrir y en "Actualizar GPS", y ReportesOrdenesViaje conserva la
    /// pestaña tras Buscar y ya no ofrece "Todos los estados" (M-077).
    /// </summary>
    public class ViewStateListasTests
    {
        private const string Tablero = "Views/Exportacion/RegistroSeguimiento.aspx";
        private const string Conductor = "Views/DashboardConductor.aspx";
        private const string Reportes = "Views/ReportesOrdenesViaje.aspx";

        /// <summary>Lista cerrada: cada lista que lleva EnableViewState="false" en estas páginas, ni una más ni una menos.</summary>
        private static readonly (string Pagina, string[] Controles)[] SinViewState =
        {
            (Tablero, new[] { "rptViajes" }),
            (Conductor, new[] { "rptPorConformar", "rptDineroRecibido", "rptDespachosActivos", "rptHistorial" }),
            (Reportes, new[] { "gvLiquidaciones", "gvViajesActivos" }),
        };

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Leer(string relativa) => File.ReadAllText(Path.Combine(RaizWeb(), relativa));

        private static string SinComentarios(string cs) =>
            Regex.Replace(Regex.Replace(cs, @"/\*.*?\*/", "", RegexOptions.Singleline), @"(?m)//.*$", "");

        /// <summary>El control (etiqueta de apertura + plantilla) hasta su cierre, contando los anidados del mismo tipo.</summary>
        private static (string Apertura, string Cuerpo) Control(string aspx, string id)
        {
            var m = Regex.Match(aspx, "<asp:(Repeater|GridView)\\s+ID=\"" + id + "\"[^>]*>");
            Assert.True(m.Success, "no está " + id);
            string tipo = m.Groups[1].Value;
            var etiquetas = new Regex("<asp:" + tipo + "\\b|</asp:" + tipo + ">");
            int nivel = 1, pos = m.Index + m.Length;
            foreach (Match t in etiquetas.Matches(aspx, pos))
            {
                nivel += t.Value.StartsWith("</") ? -1 : 1;
                if (nivel == 0) return (m.Value, aspx.Substring(pos, t.Index - pos));
            }
            throw new Exception("sin cierre: " + id);
        }

        /// <summary>El cuerpo { ... } del primer miembro cuya declaración contiene <paramref name="firma"/>.</summary>
        private static string Cuerpo(string cs, string firma)
        {
            int i = cs.IndexOf(firma, StringComparison.Ordinal);
            Assert.True(i >= 0, "no está " + firma);
            int abre = cs.IndexOf('{', i), nivel = 0;
            for (int j = abre; j < cs.Length; j++)
            {
                if (cs[j] == '{') nivel++;
                else if (cs[j] == '}' && --nivel == 0) return cs.Substring(abre, j - abre + 1);
            }
            throw new Exception("sin cierre: " + firma);
        }

        [Fact]
        public void ListasDeSoloLectura_LlevanEnableViewStateFalse_YNadaMas()
        {
            foreach (var (pagina, controles) in SinViewState)
            {
                string aspx = Leer(pagina);
                foreach (string id in controles)
                    Assert.Contains("EnableViewState=\"false\"", Control(aspx, id).Apertura);

                // Lista cerrada: ninguna otra Repeater/GridView de la página lo apagó sin pasar por esta prueba.
                var apagadas = Regex.Matches(aspx, "<asp:(?:Repeater|GridView)\\s+ID=\"(\\w+)\"[^>]*EnableViewState=\"false\"")
                    .Cast<Match>().Select(x => x.Groups[1].Value).OrderBy(x => x).ToArray();
                Assert.Equal(controles.OrderBy(x => x).ToArray(), apagadas);
            }
        }

        [Fact]
        public void ListasSinViewState_NoTienenComandosNiCamposDeServidorEnLasFilas()
        {
            foreach (var (pagina, controles) in SinViewState)
            {
                string aspx = Leer(pagina);
                foreach (string id in controles)
                {
                    var (apertura, cuerpo) = Control(aspx, id);
                    Assert.DoesNotMatch("On(Item|Row)Command|AllowPaging|AllowSorting|OnPageIndexChanging|OnSorting|OnSelectedIndexChanged", apertura);
                    // Un control de servidor que se lee o dispara un evento en el postback necesita el ViewState de la fila.
                    Assert.DoesNotMatch("<asp:(TextBox|CheckBox|RadioButton|DropDownList|LinkButton|Button|ImageButton|HiddenField|ButtonField|CommandField)\\b", cuerpo);
                }
            }
        }

        [Fact]
        public void PestanasDelTablero_ConservanViewState_PorqueTienenItemCommand()
        {
            var (apertura, _) = Control(Leer(Tablero), "rptFases");
            Assert.Contains("OnItemCommand", apertura);
            Assert.DoesNotContain("EnableViewState", apertura);
        }

        [Theory]
        [InlineData(Tablero)]
        [InlineData(Conductor)]
        [InlineData(Reportes)]
        public void PaginasConListasSinViewState_LasVuelvenAEnlazarEnTodoPostback(string pagina)
        {
            string cs = SinComentarios(Leer(pagina + ".cs"));
            string preRender = Cuerpo(cs, "protected override void OnPreRender(EventArgs e)");
            Assert.Contains("base.OnPreRender(e)", preRender);
            Assert.Contains("IsPostBack", preRender);
            Assert.Contains("IsRequestBeingRedirected", preRender);
            if (pagina == Tablero)
            {
                Assert.Contains("!_tableroCargado", preRender);
                Assert.Contains("SuppressContent", preRender);
                Assert.Matches(@"\bCargar\(\);", preRender);
                Assert.Contains("_tableroCargado = true;", Cuerpo(cs, "private void Cargar(bool sincronizar = false)"));
            }
            else if (pagina == Conductor)
            {
                string enlazar = Cuerpo(cs, "private void EnlazarListasEnPostback()");
                Assert.Contains("EnlazarListasEnPostback()", preRender);
                Assert.Contains("CargarHistorialLiquidaciones()", enlazar);
                Assert.Contains("CargarDineroRecibido()", enlazar);
                Assert.Contains("CargarDespachosViajesActivos(", enlazar);
                // Los viajes salen de la sesión; el campo oculto solo se compara (no se consulta con sus ids).
                Assert.Contains("ObtenerViajesActivos()", enlazar);
                Assert.Single(Regex.Matches(enlazar, "hfIdsViajesActivos"));
                Assert.Contains("MismosViajes(ids, hfIdsViajesActivos.Value)", enlazar);
                // InicializarDashboard pisaría las fechas que el conductor escribió.
                Assert.DoesNotContain("InicializarDashboard", enlazar);
                Assert.Contains("_historialCargado = true;", Cuerpo(cs, "private void CargarHistorialLiquidaciones()"));
            }
            else
            {
                Assert.Contains("CargarLiquidaciones(mostrarAvisos: false)", preRender);
                Assert.Contains("CargarViajesActivos(mostrarAvisos: false)", preRender);
                Assert.Contains("_liquidacionesCargadas = true;", Cuerpo(cs, "private void CargarLiquidaciones(bool mostrarAvisos = true)"));
                Assert.Contains("_viajesActivosCargados = true;", Cuerpo(cs, "private void CargarViajesActivos(bool mostrarAvisos = true)"));
            }
        }

        [Fact]
        public void Tablero_SincronizaLosDespachosSoloAlAbrirYEnActualizarGps()
        {
            string cs = SinComentarios(Leer(Tablero + ".cs"));

            // Una sola llamada al servicio (escribe en la BD), dentro de su método.
            Assert.Single(Regex.Matches(cs, @"SeguimientoViajesService\.SincronizarDespachos\("));
            Assert.Contains("SeguimientoViajesService.SincronizarDespachos(", Cuerpo(cs, "private void SincronizarDespachos()"));
            // Ese método solo se llama desde Cargar, cuando se lo piden.
            Assert.Single(Regex.Matches(cs, @"(?<!void )\bSincronizarDespachos\(\)"));
            Assert.Matches(@"if \(sincronizar\) SincronizarDespachos\(\);", Cuerpo(cs, "private void Cargar(bool sincronizar = false)"));

            // Lo piden solo la primera carga (!IsPostBack) y "Actualizar GPS".
            Assert.Equal(2, Regex.Matches(cs, @"Cargar\(sincronizar: true\)").Count);
            string pageLoad = Cuerpo(cs, "protected void Page_Load(");
            Assert.Contains("Cargar(sincronizar: true)", Cuerpo(pageLoad, "if (!IsPostBack)"));
            Assert.Contains("Cargar(sincronizar: true)", Cuerpo(cs, "protected void btnActualizarGps_Click("));
            // Las demás llamadas (Buscar, pestañas, exportaciones con aviso, OnPreRender) no sincronizan.
            Assert.Empty(Regex.Matches(cs, @"\bCargar\((?!\)|sincronizar: true\)|bool sincronizar)"));
        }

        [Theory]
        [InlineData("liquidaciones", "liquidaciones")]
        [InlineData("viajesActivos", "viajesActivos")]
        [InlineData("personalizado", "personalizado")]
        [InlineData("", "liquidaciones")]
        [InlineData(null, "liquidaciones")]
        [InlineData("<script>", "liquidaciones")]
        [InlineData("VIAJESACTIVOS", "liquidaciones")]
        public void Reportes_PestanaValida_SoloAceptaLasDeLaPagina(string enviada, string esperada)
        {
            Assert.Equal(esperada, ReportesOrdenesViaje.PestanaValida(enviada));
        }

        [Fact]
        public void Reportes_ConservaLaPestanaTrasBuscar()
        {
            string aspx = Leer(Reportes);
            string cs = SinComentarios(Leer(Reportes + ".cs"));
            string js = Leer("Scripts/paginas/ReportesOrdenesViaje.js");

            Assert.Matches("<asp:HiddenField ID=\"hdnPestana\" runat=\"server\"", aspx);
            Assert.Contains("hdnPestana: '<%= hdnPestana.ClientID %>'", aspx);
            // Cada data-tab del aspx es una pestaña que PestanaValida acepta.
            foreach (Match t in Regex.Matches(aspx, "data-tab=\"(\\w+)\""))
                Assert.Equal(t.Groups[1].Value, ReportesOrdenesViaje.PestanaValida(t.Groups[1].Value));

            Assert.Contains("hdnPestana.Value = PestanaLiquidaciones;", Cuerpo(cs, "protected void btnBuscarLiquidaciones_Click("));
            Assert.Contains("hdnPestana.Value = PestanaViajesActivos;", Cuerpo(cs, "protected void btnBuscarViajesActivos_Click("));
            Assert.Contains("hdnPestana.Value = PestanaValida(hdnPestana.Value);", Cuerpo(cs, "protected override void OnPreRender(EventArgs e)"));

            // El script guarda la pestaña al cambiarla y la vuelve a abrir al cargar.
            Assert.Contains("$('#' + SGV.hdnPestana).val(tab);", Cuerpo(js, "function cambiarTab(tab)"));
            Assert.Matches(@"var pestana = \$\('#' \+ SGV\.hdnPestana\)\.val\(\);\s+if \(pestana && pestana !== 'liquidaciones'\) cambiarTab\(pestana\);", js);
        }

        [Theory]
        [InlineData("TODOS", "ABIERTO")]
        [InlineData(" todos ", "ABIERTO")]
        [InlineData("ABIERTO", "ABIERTO")]
        [InlineData("abierto", "ABIERTO")]
        [InlineData("CERRADO", "CERRADO")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void Reportes_NormalizarEstadoViaje_TomaTodosComoAbierto(string enviado, string esperado)
        {
            Assert.Equal(esperado, ReportesOrdenesViaje.NormalizarEstadoViaje(enviado));
        }

        [Fact]
        public void Reportes_TextosTotales_EnCeroYConConversion()
        {
            var cultura = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("es-PE");
                Assert.Equal(("S/ 0.00", "$ 0.00", "S/ 0.00", "S/ 0.00"), ReportesOrdenesViaje.TextosTotales(0m, 0m, 0m));
                Assert.Equal(("S/ 10.00", "$ 2.00", "S/ 7.50", "S/ 17.50"), ReportesOrdenesViaje.TextosTotales(10m, 2m, 3.75m));
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = cultura; }
        }

        [Fact]
        public void Reportes_ConFiltrosInvalidos_VaciaGrillaYContadores_YSoloAvisaLaPestanaBuscada()
        {
            string cs = SinComentarios(Leer(Reportes + ".cs"));

            // La pestaña que no se buscó se enlaza en OnPreRender sin avisos: no pisa el mensaje de la búsqueda real.
            string preRender = Cuerpo(cs, "protected override void OnPreRender(EventArgs e)");
            Assert.Contains("CargarLiquidaciones(mostrarAvisos: false)", preRender);
            Assert.Contains("CargarViajesActivos(mostrarAvisos: false)", preRender);
            Assert.DoesNotContain(Regex.Matches(cs, @"CargarLiquidaciones\(mostrarAvisos: false\)|CargarViajesActivos\(mostrarAvisos: false\)")
                .Cast<Match>(), m => !preRender.Contains(m.Value));
            Assert.Equal(2, Regex.Matches(cs, @"mostrarAvisos: false").Count);

            foreach (var (metodo, vaciar) in new[] { ("private void CargarLiquidaciones(bool mostrarAvisos = true)", "VaciarLiquidaciones();"),
                                                     ("private void CargarViajesActivos(bool mostrarAvisos = true)", "VaciarViajesActivos();") })
            {
                string cuerpo = Cuerpo(cs, metodo);
                Assert.Matches(@"if \(mostrarAvisos\) MostrarMensaje\(mensajeValidacion, ""warning""\);\s+" + Regex.Escape(vaciar) + @"\s+return;", cuerpo);
                Assert.DoesNotMatch(@"(?<!if \(mostrarAvisos\) )MostrarMensaje\(mensajeValidacion", cuerpo);
            }

            string liq = Cuerpo(cs, "private void VaciarLiquidaciones()");
            Assert.Contains("gvLiquidaciones.DataBind();", liq);
            Assert.Contains("lblTotalRegistros.Text = \"0\";", liq);
            Assert.Contains("lblTotalRegistrosTabla.Text = \"0 registros\";", liq);
            Assert.Contains("CalcularTotalesLiquidaciones(vacia, 0m);", liq);
            Assert.Contains("TextosTotales(", Cuerpo(cs, "private void CalcularTotalesLiquidaciones("));

            string via = Cuerpo(cs, "private void VaciarViajesActivos()");
            Assert.Contains("gvViajesActivos.DataBind();", via);
            Assert.Contains("lblTotalViajesActivos.Text = \"0 conductores\";", via);
            Assert.Contains("lblCountViajesActivos.Text = \"0\";", via);
            Assert.Contains("lblCountViajesActivosTabla.Text = \"0 viajes\";", via);

            // El postback usa la constante (E5-17: sin combo) y "TODOS" de un enlace viejo se normaliza en la exportación GET.
            Assert.Contains("string estadoViaje = EstadoViajeAbierto;", Cuerpo(cs, "private void CargarViajesActivos(bool mostrarAvisos = true)"));
            Assert.Contains("NormalizarEstadoViaje(Request.QueryString[\"estadoViaje\"])", cs);
            Assert.Contains("NormalizarEstadoViaje(estadoViaje)", Cuerpo(cs, "private bool ValidarFiltrosViajesActivos("));
        }

        [Theory]
        [InlineData(new[] { 5, 7 }, "5,7", true)]
        [InlineData(new[] { 7, 5 }, "5, 7", true)]
        [InlineData(new[] { 5 }, "5,7", false)]
        [InlineData(new[] { 5, 8 }, "5,7", false)]
        [InlineData(new int[0], "", false)]
        [InlineData(new int[0], "5", false)]
        [InlineData(new[] { 5 }, "", false)]
        [InlineData(new[] { 5 }, null, false)]
        public void Conductor_MismosViajes_SoloConLosMismosIdsYNoVacios(int[] ids, string mostrados, bool esperado)
        {
            Assert.Equal(esperado, DashboardConductor.MismosViajes(ids, mostrados));
        }

        [Fact]
        public void Conductor_EnPostback_OcultaLosDespachosSiCambiaronLosViajes()
        {
            string enlazar = Cuerpo(SinComentarios(Leer(Conductor + ".cs")), "private void EnlazarListasEnPostback()");
            Assert.Matches(@"if \(!MismosViajes\(ids, hfIdsViajesActivos\.Value\)\)\s*\{\s*pnlDespachos\.Visible = false;\s*return;\s*\}\s*CargarDespachosViajesActivos\(ids\);", enlazar);
        }

        [Fact]
        public void Reportes_SinTodosLosEstados_QueDabaLoMismoQueAbierto()
        {
            // E5-17: ni combo; el estado es un texto fijo y el script exporta siempre ABIERTO.
            string aspx = Leer(Reportes);
            Assert.DoesNotContain("ddlEstadoViaje", aspx);
            Assert.Contains("Abiertos (sin liquidar)", aspx);

            string cs = SinComentarios(Leer(Reportes + ".cs"));
            Assert.Contains("EstadosViajePermitidos = { \"ABIERTO\" };", cs);
            string js = Leer("Scripts/paginas/ReportesOrdenesViaje.js");
            Assert.DoesNotContain("'TODOS'", js);
            Assert.Contains("&estadoViaje=ABIERTO", js);

            // La consulta ya filtra siempre los viajes ABIERTO: por eso "Todos" no sumaba nada.
            string servicio = Leer("Services/Reportes/ReportesOrdenesViajeService.cs");
            Assert.Contains("WHERE vp.estadoViaje = 'ABIERTO'", Cuerpo(servicio, "public static DataTable ObtenerViajesActivos("));
        }
    }
}
