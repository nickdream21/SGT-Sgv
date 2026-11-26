using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Views.Controles.Despachos;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-26: los viajes en progreso del conductor salen de RegistroDespacho a <c>Views/Controles/Despachos/ViajesConductor.ascx</c>
    /// (con "Crear nuevo viaje", envío único E5-3) y el modal del historial a <c>HistorialViajes.ascx</c>, fuera de
    /// UpdatePanelMain. La página les pasa su selector de conductor y pinta sus avisos.
    /// </summary>
    public class ViajesConductorTests
    {
        private static string Leer(params string[] ruta) => File.ReadAllText(Path.Combine(FuentesWeb.RaizWeb(), Path.Combine(ruta)));
        private static string Pagina() => Leer("Views", "RegistroDespacho.aspx");
        private static string PaginaCs() => Leer("Views", "RegistroDespacho.aspx.cs");
        private static string Viajes() => Leer("Views", "Controles", "Despachos", "ViajesConductor.ascx");
        private static string ViajesCs() => Leer("Views", "Controles", "Despachos", "ViajesConductor.ascx.cs");
        private static string Historial() => Leer("Views", "Controles", "Despachos", "HistorialViajes.ascx");
        private static string HistorialCs() => Leer("Views", "Controles", "Despachos", "HistorialViajes.ascx.cs");

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

        [Fact]
        public void LaPaginaRegistraLosDosControles_YSuMarcadoYaNoEstaEnElla()
        {
            string aspx = Pagina();
            Assert.Contains("<%@ Register Src=\"~/Views/Controles/Despachos/ViajesConductor.ascx\" TagPrefix=\"sgv\" TagName=\"ViajesConductor\" %>", aspx);
            Assert.Contains("<%@ Register Src=\"~/Views/Controles/Despachos/HistorialViajes.ascx\" TagPrefix=\"sgv\" TagName=\"HistorialViajes\" %>", aspx);
            foreach (string id in new[] { "UpdatePanelViajes", "hfEnvioUnicoViaje", "pnlViajesProgreso", "ddlViajesActivos", "btnCrearNuevoViaje", "btnVerHistorialViajes" })
            {
                Assert.DoesNotContain("ID=\"" + id + "\"", aspx);
                Assert.Contains("ID=\"" + id + "\"", Viajes());
            }
            foreach (string id in new[] { "modalHistorialViajes", "UpdatePanelTituloModal", "spanConductorModal", "UpdatePanelModal", "gvHistorialViajes" })
            {
                Assert.DoesNotContain("id=\"" + id + "\"", aspx, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("=\"" + id + "\"", Historial());
            }
            // El código de la página no toca los controles de los viajes ni del historial.
            foreach (string campo in new[] { "pnlViajesProgreso", "ddlViajesActivos", "hfEnvioUnicoViaje", "UpdatePanelViajes", "gvHistorialViajes", "spanConductorModal" })
                Assert.DoesNotContain(campo, PaginaCs());
        }

        [Fact]
        public void LosViajesVanDentroDeLaTarjetaDelConductor_YElHistorialFueraDeUpdatePanelMain()
        {
            string aspx = Pagina();
            int tarjeta = Pos(aspx, "<asp:Panel ID=\"pnlAdicionConductores\"");
            int viajes = Pos(aspx, "<sgv:ViajesConductor ID=\"ucViajesConductor\" runat=\"server\" />");
            Assert.True(viajes > tarjeta && viajes < Pos(aspx, "<asp:DropDownList ID=\"ddlConductor\""));
            // "Reabrir" acaba en MostrarMensaje → UpdatePanelMain.Update(): dentro, el panel rompería el modal abierto.
            int finMain = aspx.LastIndexOf("</asp:UpdatePanel>", StringComparison.Ordinal);   // el último cierre es el de UpdatePanelMain
            Assert.True(Pos(aspx, "<sgv:HistorialViajes ID=\"ucHistorialViajes\" runat=\"server\" />") > finMain);
            Assert.Contains("id=\"modalHistorialViajes\"", Historial());
            Assert.Contains("$('#modalHistorialViajes').modal('show');", Leer("Scripts", "paginas", "RegistroDespacho.js"));
            Assert.Contains("<asp:AsyncPostBackTrigger ControlID=\"gvHistorialViajes\" EventName=\"RowCommand\" />", Historial());
        }

        [Fact]
        public void LaClaveDelEnvioUnico_EsLaDeSiempre()
        {
            // Un token emitido por la versión anterior sigue sirviendo tras publicar.
            Assert.Equal("RegistroDespacho.CrearNuevoViaje", ViajesConductor.ClaveEnvioUnicoViaje);
            Assert.Matches(@"if \(!IsPostBack\)\s+EnvioUnicoHelper\.Emitir\(ClaveEnvioUnicoViaje, hfEnvioUnicoViaje\);", Cuerpo(ViajesCs(), "protected void Page_Load("));
        }

        [Fact]
        public void CrearNuevoViaje_TokenNuevo_LuegoConsumir_LuegoCrear_LuegoAuditar()
        {
            string c = Cuerpo(ViajesCs(), "protected void btnCrearNuevoViaje_Click(");
            int token = Pos(c, "string token = hfEnvioUnicoViaje.Value;");
            int emitir = Pos(c, "EnvioUnicoHelper.Emitir(ClaveEnvioUnicoViaje, hfEnvioUnicoViaje);");
            int consumir = Pos(c, "EnvioUnicoHelper.Consumir(ClaveEnvioUnicoViaje, token)");
            int crear = Pos(c, "CrearNuevoViajeProgreso(idConductor, \"Viaje creado manualmente por usuario\")");
            int auditar = Pos(c, "AuditoriaHelper.Registrar(\"INSERT\", \"ViajesEnProgreso\"");
            Assert.True(token < emitir && emitir < consumir && consumir < crear && crear < auditar);
            Assert.Contains("Avisar(EnvioUnicoHelper.MensajeYaRegistrado, \"warning\");", c);
            Assert.Contains("MostrarErrorOperacion(\"Error al crear nuevo viaje.\", \"btnCrearNuevoViaje_Click\", ex);", c);
        }

        [Fact]
        public void LaPagina_LesPasaElSelector_YRegistraSusDisparadores()
        {
            string cs = PaginaCs();
            Assert.Contains("ConectarViajes();", Cuerpo(cs, "protected override void OnInit(EventArgs e)"));
            string conectar = Cuerpo(cs, "private void ConectarViajes()");
            foreach (string linea in new[]
            {
                "ucViajesConductor.SelectorConductor = ddlConductor;",
                "ucHistorialViajes.SelectorConductor = ddlConductor;",
                "ucViajesConductor.Aviso += (s, a) => MostrarMensaje(a.Mensaje, a.Tipo);",
                "ucHistorialViajes.Aviso += (s, a) => MostrarMensaje(a.Mensaje, a.Tipo);",
                "ucViajesConductor.VerHistorial += (s, a) => ucHistorialViajes.Abrir();",
            })
                Assert.Contains(linea, conectar);

            string disparadores = Cuerpo(cs, "private void RegistrarDisparadores()");
            Assert.Contains("ucViajesConductor.DisparadoresAsincronos", disparadores.Substring(Pos(disparadores, "var clic")));
            Assert.Contains("ucViajesConductor.RefrescarCon(ddlConductor, \"SelectedIndexChanged\");", disparadores);
            Assert.Contains("DisparadoresAsincronos => new Control[] { btnCrearNuevoViaje, btnVerHistorialViajes };", ViajesCs());
            // El disparador del selector de la página ya no va en el marcado del panel (otro contenedor de nombres).
            Assert.DoesNotContain("ControlID=\"ddlConductor\"", Viajes());
            Assert.Contains("UpdatePanelViajes.Triggers.Add(new AsyncPostBackTrigger { ControlID = c.UniqueID, EventName = evento });", ViajesCs());
        }

        [Fact]
        public void ElViajeDelConductor_SeEligeConLoQueMuestraElControl()
        {
            string cs = PaginaCs();
            Assert.Matches(@"private int\? DeterminarViajeProgreso\([^)]*\) =>\s+RegistroDespachoReglas\.ElegirViajeProgreso\([^;]*ucViajesConductor\.ViajeElegido\);", cs);
            Assert.Contains("public string ViajeElegido => ddlViajesActivos.SelectedValue;", ViajesCs());
            string cambio = Cuerpo(cs, "protected void ddlConductor_SelectedIndexChanged(");
            Assert.True(Pos(cambio, "ucViajesConductor.Mostrar(idConductor);") < Pos(cambio, "ucViajesConductor.Ocultar();"));
            string editar = Cuerpo(cs, "private void CargarConductorParaEdicion(");
            Assert.True(Pos(editar, "ucViajesConductor.Mostrar(conductor.IdConductor);") < Pos(editar, "ucViajesConductor.Refrescar();"));
            // Al volver a la configuración base se oculta sin refrescar el panel (su tarjeta también se oculta).
            Assert.Contains("ucViajesConductor.Ocultar(refrescar: false);", Cuerpo(cs, "private void MostrarFaseConfiguracionBase()"));
            Assert.Contains("ucViajesConductor.Ocultar();", Cuerpo(cs, "private void LimpiarCamposConductor()"));
        }

        [Fact]
        public void Ocultar_RefrescaElPanelSoloEnUnPostbackParcial()
        {
            string ocultar = Cuerpo(ViajesCs(), "public void Ocultar(bool refrescar = true)");
            Assert.True(Pos(ocultar, "pnlViajesProgreso.Visible = false;") < Pos(ocultar, "if (!refrescar) return;"));
            Assert.Matches(@"if \(scriptManager != null && scriptManager\.IsInAsyncPostBack\)\s+UpdatePanelViajes\.Update\(\);", ocultar);
        }

        [Fact]
        public void Historial_AbreElModal_YReabrirAuditaConSuMotivoDeNegocio()
        {
            string abrir = Cuerpo(HistorialCs(), "public void Abrir()");
            Assert.True(Pos(abrir, "Avisar(\"Seleccione un conductor para ver su historial de viajes.\", \"warning\");") < Pos(abrir, "Cargar(idConductor);"));
            Assert.True(Pos(abrir, "UpdatePanelTituloModal.Update();") < Pos(abrir, "LlamarFuncionJsAlCargar(Page, \"showModal\", \"showHistorialModal\");"));

            string reabrir = Cuerpo(HistorialCs(), "protected void gvHistorialViajes_RowCommand(");
            Assert.True(Pos(reabrir, "RegistroDespachoService.ReabrirViajeProgreso(idViajeProgreso, UsuarioActual(Page));") <
                        Pos(reabrir, "AuditoriaHelper.Registrar(\"UPDATE\", \"ViajesEnProgreso\""));
            Assert.Contains("catch (SqlException ex) when (ex.Number == 50000)", reabrir);
            Assert.Contains("\"No se pudo reabrir el viaje: \" + MensajeErrorHelper.ParaUsuario(ex,", reabrir);
        }

        [Fact]
        public void Utilidades_EnControlesFormulario_SinCopiasEnLaPaginaNiEnProgramacionWord()
        {
            // M-205: RestaurarSeleccion, Html, RegistrarError y UsuarioActual existían en la página y en ProgramacionWord.
            string programacion = Leer("Views", "Controles", "Despachos", "ProgramacionWord.ascx.cs");
            foreach (var (nombre, cs) in new[] { ("RegistroDespacho.aspx.cs", PaginaCs()), ("ProgramacionWord.ascx.cs", programacion) })
            {
                Assert.Contains("using static WebSGV.Views.Controles.Despachos.ControlesFormulario;", cs);
                foreach (string firma in new[] { "void RestaurarSeleccion(", "string Html(", "void RegistrarError(", "string UsuarioActual(", "void LlamarFuncionJsAlCargar(" })
                    Assert.False(cs.Contains(firma), nombre + " vuelve a definir " + firma);
            }
            string utilidades = Leer("Views", "Controles", "Despachos", "ControlesFormulario.cs");
            Assert.Contains("public static void LlamarFuncionJsAlCargar(Page pagina, string clave, string funcion)", utilidades);
            Assert.Contains("public static string TextoErrorOperacion(string mensajeUsuario, string contexto, Exception ex)", utilidades);
            // La página conserva el envoltorio (lo usan ErroresQueNoSeTapanTests y sus avisos).
            Assert.Contains("ControlesFormulario.TextoErrorOperacion(mensajeUsuario, contexto, ex)", PaginaCs());
        }

        [Fact]
        public void LosAvisosDeLosControles_UsanElTipoDeProgramacionWord()
        {
            foreach (string cs in new[] { ViajesCs(), HistorialCs(), Leer("Views", "Controles", "Despachos", "ConfiguracionBaseLote.ascx.cs") })
                Assert.Contains("public event EventHandler<ProgramacionWord.AvisoEventArgs> Aviso;", cs);
            Assert.True(Regex.Matches(PaginaCs(), @"\.Aviso \+= \(s, a\) => MostrarMensaje\(a\.Mensaje, a\.Tipo\);").Count == 3);
        }
    }
}
