using System;
using System.IO;
using System.Linq;
using WebSGV.Services.Clientes;
using WebSGV.Services.Common;
using WebSGV.Services.Usuarios;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Bloque 5, B5-3: el botón Activar/Desactivar de los maestros manda el estado destino ("id|0" / "id|1") en
    /// vez de invertir a ciegas. Lógica pura y una comprobación del código fuente de las pantallas.
    /// </summary>
    public class EstadoActivoTests
    {
        [Theory]
        [InlineData(true, "15|0")]
        [InlineData(false, "15|1")]
        public void Argumento_lleva_el_estado_contrario_al_que_muestra_la_fila(bool activo, string esperado)
        {
            Assert.Equal(esperado, EstadoActivoService.ArgumentoComando(15, activo));
        }

        [Fact]
        public void Argumento_con_estado_nulo_pide_activar()
        {
            Assert.Equal("7|1", EstadoActivoService.ArgumentoComando(7, DBNull.Value));
            Assert.Equal("7|1", EstadoActivoService.ArgumentoComando(7, null));
        }

        [Theory]
        [InlineData("15|0", 15, false)]
        [InlineData("15|1", 15, true)]
        public void Lee_el_argumento(string argumento, int id, bool nuevo)
        {
            Assert.True(EstadoActivoService.TryLeerArgumento(argumento, out int leido, out bool activo));
            Assert.Equal(id, leido);
            Assert.Equal(nuevo, activo);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("15")]          // formato anterior: una página vieja en caché no debe invertir el estado
        [InlineData("15|2")]
        [InlineData("15|")]
        [InlineData("x|1")]
        [InlineData("0|1")]
        [InlineData("-3|1")]
        [InlineData("15|1|0")]
        [InlineData(" 15|1")]
        public void Rechaza_un_argumento_mal_formado(string argumento)
        {
            Assert.False(EstadoActivoService.TryLeerArgumento(argumento, out _, out _));
        }

        [Fact]
        public void Mensaje_ya_estaba_con_genero()
        {
            Assert.Equal("El tracto ya estaba inactivo; se actualizó la lista.",
                EstadoActivoService.MensajeYaEstaba("tracto", false, false));
            Assert.Equal("La estación de peaje ya estaba activa; se actualizó la lista.",
                EstadoActivoService.MensajeYaEstaba("estación de peaje", true, true));
        }

        [Fact]
        public void Mensaje_cambiado_con_genero()
        {
            Assert.Equal("Tracto ABC-123 desactivado correctamente.",
                EstadoActivoService.MensajeCambiado("tracto", false, false, "ABC-123"));
            Assert.Equal("Ruta PAITA activada correctamente.",
                EstadoActivoService.MensajeCambiado("ruta", true, true, "PAITA"));
        }

        [Fact]
        public void Estado_de_la_auditoria()
        {
            Assert.Equal("Estado: Activo", EstadoActivoService.DescribirEstado(true));
            Assert.Equal("Estado: Inactivo", EstadoActivoService.DescribirEstado(false));
        }

        [Theory]
        [InlineData(MaestroSimple.Cliente, "Cliente", "cliente", false)]
        [InlineData(MaestroSimple.EstacionPeaje, "EstacionesPeaje", "estación de peaje", true)]
        [InlineData(MaestroSimple.Planta, "Planta", "planta", true)]
        public void Maestros_simples_apuntan_a_su_tabla(MaestroSimple maestro, string tabla, string sustantivo, bool femenino)
        {
            TablaActivable t = MaestrosService.Tabla(maestro);
            Assert.Equal(tabla, t.Tabla);
            Assert.Equal(sustantivo, t.Sustantivo);
            Assert.Equal(femenino, t.Femenino);
        }

        [Theory]
        [InlineData(TipoCatalogo.Producto, false)]
        [InlineData(TipoCatalogo.PlantaCarga, true)]
        [InlineData(TipoCatalogo.PlantaDescarga, true)]
        [InlineData(TipoCatalogo.Ruta, true)]
        public void Genero_de_los_catalogos_para_los_mensajes(TipoCatalogo tipo, bool femenino)
        {
            // "ya tiene otro producto" / "ya tiene otra ruta"
            Assert.Equal(femenino, CatalogoClienteService.EsFemenino(tipo));
        }

        [Fact]
        public void Usuarios_renuevan_el_sello_de_sesion_en_el_mismo_update()
        {
            // B5-8: el sello solo se renueva junto con el cambio real de estado (WHERE activo <> @nuevo)
            TablaActivable t = UsuarioEstadoService.TablaUsuarios;
            Assert.Equal("Usuarios", t.Tabla);
            Assert.Equal("usuario", t.Sustantivo);
            Assert.False(t.Femenino);
            Assert.Equal("selloSesion = NEWID()", t.SetAdicional);
            Assert.Null(MaestrosService.Tabla(MaestroSimple.Planta).SetAdicional);
        }

        [Fact]
        public void Nadie_se_desactiva_a_si_mismo()
        {
            // Se rechaza antes de tocar la BD
            var ex = Assert.Throws<ErrorNegocioException>(() => UsuarioEstadoService.CambiarEstado(42, false, 42));
            Assert.Equal("No puedes desactivar tu propia cuenta.", ex.Message);
        }

        // ── Código fuente de las pantallas ───────────────────────────────

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        public static readonly TheoryData<string> Pantallas = new TheoryData<string>
        {
            "RegistroTractos", "RegistroSemiremolques", "RegistroChoferes", "RegistroClientes",
            "RegistroPeajes", "RegistroPlantas", "CatalogoCliente"
        };

        [Theory]
        [MemberData(nameof(Pantallas))]
        public void Pantalla_no_invierte_el_estado_a_ciegas_y_muestra_el_error_de_edicion_en_el_modal(string pagina)
        {
            string raiz = RaizWeb();
            string cs = File.ReadAllText(Path.Combine(raiz, "Views", pagina + ".aspx.cs"));
            string aspx = File.ReadAllText(Path.Combine(raiz, "Views", pagina + ".aspx"));
            string js = File.ReadAllText(Path.Combine(raiz, "Scripts", "paginas", pagina + ".js"));

            // B5-3
            Assert.DoesNotContain("THEN 0 ELSE 1 END", cs);
            Assert.Contains("EstadoActivoService.TryLeerArgumento(", cs);
            // E5-16: los catálogos con la plantilla común (sin ViewState en la grilla) llevan el argumento en
            // data-argumento y lo envían por hfArgumentoFila. Las pestañas de CatalogoCliente conservan su
            // CommandArgument fijo (B5-9); ninguno va enlazado a la fila.
            Assert.Contains(pagina, CatalogosPlantillaTests.PaginasConEstado);
            Assert.Contains("data-argumento='<%#: ArgumentoEstado(", aspx);
            Assert.DoesNotMatch(@"CommandArgument\s*=\s*['""]<%#", aspx);
            Assert.DoesNotContain("ToggleActivo", aspx + cs);
            Assert.Contains("EstadoActivoService.TryLeerArgumento(hfArgumentoFila.Value", cs);

            // B5-7: el error de la edición va dentro del modal, sin ViewState, y se limpia al abrir otra edición
            Assert.Contains("ID=\"pnlErrorModal\"", aspx);
            Assert.Matches("<asp:Panel ID=\"pnlErrorModal\"[^>]*EnableViewState=\"false\"", aspx);
            Assert.True(aspx.IndexOf("ID=\"pnlErrorModal\"", StringComparison.Ordinal) >
                        aspx.IndexOf("<div class=\"modal-body\">", StringComparison.Ordinal),
                "El panel de error debe estar dentro del cuerpo del modal");
            Assert.Contains("pnlErrorModal: '<%= pnlErrorModal.ClientID %>'", aspx);
            Assert.Contains("MensajeHtml.Texto(mensaje)", cs);
            Assert.Contains("SGV.pnlErrorModal", js);
            Assert.DoesNotContain("msgPanel.querySelector('.alert-danger')", js);
        }

        [Fact]
        public void GestionUsuarios_no_invierte_el_estado_a_ciegas()
        {
            // B5-8: con F5 o dos pestañas se reactivaba al usuario recién desactivado
            string raiz = RaizWeb();
            string cs = File.ReadAllText(Path.Combine(raiz, "Views", "GestionUsuarios.aspx.cs"));
            string aspx = File.ReadAllText(Path.Combine(raiz, "Views", "GestionUsuarios.aspx"));

            Assert.DoesNotContain("THEN 0 ELSE 1 END", cs);
            Assert.Contains("EstadoActivoService.TryLeerArgumento(", cs);
            Assert.Contains("UsuarioEstadoService.CambiarEstado(", cs);
            Assert.Contains("CommandName=\"ToggleActivo\" CommandArgument='<%# ArgumentoEstado(", aspx);
        }
    }
}
