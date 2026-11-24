using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Menú de Site.Master desde una sola fuente (E5-19c: M-124, M-076, M-164). <see cref="MenuSitio"/> arma
    /// los menús por rol; estas pruebas fijan lo que ve cada rol (tabla literal: dar o quitar una opción
    /// cambia esta prueba a propósito), que quien ve una opción entra a su página (según
    /// <see cref="GuardasAccesoTests.AccesoEsperado"/>), que no hay opciones repetidas y qué páginas no
    /// tienen opción en el menú, con su motivo.
    /// </summary>
    public class MenuSitioTests
    {
        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        /// <summary>"~/Views/X.aspx" → "Views/X.aspx" (la clave de las tablas de GuardasAccesoTests).</summary>
        private static string Pagina(string url) => url.Substring(2);

        /// <summary>Roles vigentes (con sus alias) y el grupo de GuardasAccesoTests al que pertenecen.</summary>
        private static IEnumerable<(string Grupo, string Rol)> RolesVigentes() =>
            GuardasAccesoTests.RolesDelGrupo.SelectMany(g => g.Value.Select(r => (g.Key, r)));

        private static List<string> Lineas(IEnumerable<GrupoMenu> menu) =>
            menu.SelectMany(g => g.Entradas.Select((e, i) => g.Texto + " > " + (g.LlevaSeparador(i) ? "--- " : "") + e.Texto)).ToList();

        // ── Lo que ve cada rol (hoy, sin la Auditoría repetida de Configuración, M-164) ─────────────

        private static readonly string[] MenuAdministracion =
        {
            "Despacho > Registro de despachos",
            "Despacho > Bandeja de documentos",
            "Despacho > Lista de despachos",
            "Despacho > Destino de descarga",
            "Seguimiento > Tablero de seguimiento",
            "Seguimiento > Dashboard de exportación",
            "Liquidaciones > Liquidaciones pendientes",
            "Liquidaciones > Descuentos y reintegros (quincena)",
            "Liquidaciones > Dinero y pagos del viaje",        // solo con MovimientosViaje.Habilitado
            "Reportes > Reporte de cargas (pedidos, guías y carga)",
            "Reportes > Reporte de liquidaciones y viajes",
            "Reportes > Facturas de flete (conciliación)",
            "Reportes > --- Reporte GPS por placas",
            "Catálogos > Conductores",
            "Catálogos > Tractos",
            "Catálogos > Semirremolques",
            "Catálogos > Clientes",
            "Catálogos > Catálogo del cliente",
            "Catálogos > --- Plantas",
            "Catálogos > Peajes",
        };

        private static readonly Dictionary<string, string[]> MenuEsperado = new Dictionary<string, string[]>
        {
            ["ADMIN"] = MenuAdministracion,
            ["ADMINISTRADOR"] = MenuAdministracion,
            ["ADMINISTRADOR DE TRANSPORTE"] = MenuAdministracion,
            ["ADMINISTRADOR DE SISTEMA"] = new[]
            {
                "Administración > Panel del sistema",
                "Administración > Gestión de usuarios",
                "Administración > --- Auditoría del sistema",
            },
            ["CONDUCTOR"] = new[] { "Mi viaje > Mi viaje" },
            ["CHOFER"] = new[] { "Mi viaje > Mi viaje" },
            ["CONTABILIDAD"] = new[] { "Liquidaciones aprobadas > Liquidaciones aprobadas" },
        };

        [Fact]
        public void Menu_PorRol_EsElEsperado()
        {
            foreach (var par in MenuEsperado)
            {
                Assert.Equal(par.Value, Lineas(MenuSitio.Para(par.Key, movimientosHabilitado: true)));
                Assert.Equal(par.Value.Where(l => l != "Liquidaciones > Dinero y pagos del viaje"),
                             Lineas(MenuSitio.Para(par.Key, movimientosHabilitado: false)));
            }
            // Sin importar mayúsculas ni espacios, como la sesión.
            Assert.Equal(MenuAdministracion, Lineas(MenuSitio.Para(" administrador de transporte ", true)));
            Assert.Equal(MenuEsperado.Keys.OrderBy(k => k), RolesVigentes().Select(r => r.Rol).OrderBy(k => k));
        }

        [Fact]
        public void Menu_PorRol_DesplegablesYEnlacesDirectos()
        {
            Assert.Equal(new[] { "Despacho", "Seguimiento", "Liquidaciones", "Reportes", "Catálogos" },
                MenuSitio.Para("ADMIN", true).Select(g => g.Texto));
            Assert.All(MenuSitio.Para("ADMIN", true), g => Assert.False(g.EsEnlaceDirecto));
            Assert.True(MenuSitio.Para("CONDUCTOR", true).Single().EsEnlaceDirecto);
            Assert.True(MenuSitio.Para("CONTABILIDAD", true).Single().EsEnlaceDirecto);
            Assert.False(MenuSitio.Para("ADMINISTRADOR DE SISTEMA", true).Single().EsEnlaceDirecto);

            // Los desplegables conservan sus id (aria-labelledby) y no se repiten.
            var ids = MenuSitio.Completo.Where(g => !g.EsEnlaceDirecto).Select(g => g.Id).ToList();
            Assert.All(ids, id => Assert.StartsWith("navbarDropdown", id));
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.All(MenuSitio.Completo.Where(g => g.EsEnlaceDirecto), g => Assert.Single(g.Entradas));
        }

        [Theory]
        [InlineData("ADMINISTRADOR DE GRIFO")]
        [InlineData("ADMINISTRADOR DE MAQUINARIA")]
        [InlineData("OPERADOR")]
        [InlineData("SUPERVISOR")]
        [InlineData("")]
        [InlineData(null)]
        public void Menu_RolRetiradoODesconocido_NoVeNada(string rol)
        {
            Assert.Empty(MenuSitio.Para(rol, true));
        }

        [Fact]
        public void Menu_QuienVeLaEntrada_Entra()
        {
            var fallas = new List<string>();
            foreach (var (grupo, rol) in RolesVigentes())
                foreach (var e in MenuSitio.Para(rol, movimientosHabilitado: true).SelectMany(g => g.Entradas))
                {
                    if (!GuardasAccesoTests.AccesoEsperado.TryGetValue(Pagina(e.Url), out var entran))
                        fallas.Add($"{e.Texto} ({e.Url}): la página no está en GuardasAccesoTests.AccesoEsperado.");
                    else if (!entran.Contains(grupo))
                        fallas.Add($"{rol} ve \"{e.Texto}\" pero su guarda no lo deja entrar a {e.Url}.");
                }
            Assert.True(fallas.Count == 0, "Opciones del menú que llevan a una página sin acceso:\n" + string.Join("\n", fallas));
        }

        [Fact]
        public void Menu_SeccionesYCondiciones_SonLasDeLaMatriz()
        {
            foreach (var e in MenuSitio.Completo.SelectMany(g => g.Entradas))
            {
                Assert.True((e.Seccion == null) != (e.Condicion == null), e.Texto + ": debe tener sección o condición, no ambas.");
                if (e.Seccion != null)
                    Assert.Contains(e.Seccion, RolesHelper.Secciones);
            }
        }

        [Fact]
        public void Menu_SinUrlRepetidasPorRol()
        {
            foreach (var (_, rol) in RolesVigentes())
                foreach (bool mov in new[] { true, false })
                {
                    var urls = MenuSitio.Para(rol, mov).SelectMany(g => g.Entradas).Select(e => MenuSitio.NormalizarRuta(e.Url)).ToList();
                    var repetidas = urls.GroupBy(u => u).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                    Assert.True(repetidas.Count == 0, rol + ": opciones repetidas " + string.Join(", ", repetidas));
                }
            // Configuración (marcado de Site.Master) no repite ninguna opción de MenuSitio: la Auditoría salía
            // dos veces al administrador de sistema (M-164).
            Assert.DoesNotContain(MenuSitio.Completo.SelectMany(g => g.Entradas),
                e => MenuSitio.EsActiva(e, SiteMaster.UrlMiSeguridad));
            string master = File.ReadAllText(Path.Combine(RaizWeb(), "Site.Master"));
            Assert.DoesNotMatch(@"~/Views/(?!Login\.aspx)[\w/]+\.aspx", master);
        }

        /// <summary>Páginas de Views sin opción en el menú de ningún rol, y por qué (lista cerrada).</summary>
        private static readonly Dictionary<string, string> PaginasSinEntrada = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Views/Inicio.aspx"] = "portada de la administración: se llega con el logo (RolesHelper.UrlInicioSegunRol)",
            ["Views/MiSeguridad.aspx"] = "en Configuración, en el marcado de Site.Master (solo con segundo factor)",
            ["Views/DetalleOrdenViaje.aspx"] = "detalle de una liquidación: se abre desde las listas",
            ["Views/FirmarLiquidacion.aspx"] = "firma del conductor: se abre desde su panel",
            ["Views/ConformarLiquidacion.aspx"] = "conformidad del conductor: se abre desde su panel",
            ["Views/Exportacion/SeguimientoViaje.aspx"] = "detalle de un viaje: se abre desde el tablero de seguimiento",
            ["Views/Exportacion/ImportarSeguimiento.aspx"] = "importación: se abre desde el tablero de seguimiento",
            ["Views/DescargarDocumento.aspx"] = "descarga con clave de sesión (DocumentoHelper.UrlDescarga)",
            ["Views/DescargarPdfOrdenViaje.aspx"] = "descarga del PDF de una orden",
            ["Views/VerificarDocumento.aspx"] = "verificación pública del PDF firmado (código del pie)",
            ["Views/Login.aspx"] = "inicio de sesión, sin menú",
            ["Views/RecuperarContrasena.aspx"] = "cuenta, sin menú",
            ["Views/RestablecerContrasena.aspx"] = "cuenta (enlace del correo), sin menú",
            ["Views/SegundoFactor.aspx"] = "cuenta (código al entrar), sin menú",
            ["Views/Error.aspx"] = "página de error",
            ["Views/Error404.aspx"] = "página 404",
        };

        [Fact]
        public void Menu_PaginasSinEntrada_SonLasDeLaLista()
        {
            string raiz = RaizWeb();
            var todas = Directory.GetFiles(Path.Combine(raiz, "Views"), "*.aspx", SearchOption.AllDirectories)
                .Select(f => f.Substring(raiz.Length + 1).Replace('\\', '/'))
                .ToList();
            var enMenu = new HashSet<string>(MenuSitio.Completo.SelectMany(g => g.Entradas).Select(e => Pagina(e.Url)), StringComparer.OrdinalIgnoreCase);

            foreach (string p in enMenu)
                Assert.True(todas.Contains(p, StringComparer.OrdinalIgnoreCase), "El menú enlaza a una página que no existe: " + p);

            var sinEntrada = todas.Where(p => !enMenu.Contains(p)).ToList();
            var noListadas = sinEntrada.Where(p => !PaginasSinEntrada.ContainsKey(p)).ToList();
            var sobran = PaginasSinEntrada.Keys.Where(p => !sinEntrada.Contains(p, StringComparer.OrdinalIgnoreCase)).ToList();
            Assert.True(noListadas.Count == 0 && sobran.Count == 0,
                "Páginas sin opción en el menú y fuera de la lista (agréguela a MenuSitio o a la lista con su motivo): "
                + string.Join(", ", noListadas) + "\nEn la lista pero ya no existen o ya tienen opción: " + string.Join(", ", sobran));
        }

        // ── Página activa (M-076) ─────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("~/Views/ListaDespachos.aspx", "~/Views/ListaDespachos.aspx", true)]
        [InlineData("~/Views/ListaDespachos.aspx", "~/views/listadespachos.ASPX", true)]
        [InlineData("~/Views/ListaDespachos.aspx", "/Views/ListaDespachos.aspx?id=3#x", true)]
        [InlineData("~/Views/ListaDespachos.aspx", "Views/ListaDespachos.aspx", true)]
        [InlineData("~/Views/Exportacion/RegistroSeguimiento.aspx", "~/Views/Exportacion/RegistroSeguimiento.aspx", true)]
        [InlineData("~/Views/ListaDespachos.aspx", "~/Views/RegistroDespacho.aspx", false)]
        [InlineData("~/Views/RegistroSeguimiento.aspx", "~/Views/Exportacion/RegistroSeguimiento.aspx", false)]
        [InlineData("~/Views/ListaDespachos.aspx", "", false)]
        [InlineData("~/Views/ListaDespachos.aspx", null, false)]
        public void EsActiva_ComparaLaRutaSinQueryNiMayusculas(string url, string rutaActual, bool esperado)
        {
            Assert.Equal(esperado, MenuSitio.EsActiva(url, rutaActual));
        }

        [Fact]
        public void EsActivo_ElMenuQueContieneLaPagina()
        {
            var menu = MenuSitio.Para("ADMIN", true);
            Assert.Equal(new[] { "Reportes" }, menu.Where(g => MenuSitio.EsActivo(g, "~/Views/ReporteGps.aspx")).Select(g => g.Texto));
            Assert.Empty(menu.Where(g => MenuSitio.EsActivo(g, "~/Views/Inicio.aspx")));
            Assert.False(MenuSitio.EsActiva((EntradaMenu)null, "~/Views/Inicio.aspx"));
        }

        // ── Site.Master pinta MenuSitio ───────────────────────────────────────────────────────

        [Fact]
        public void SiteMaster_PintaElMenuDeMenuSitio()
        {
            string raiz = RaizWeb();
            string master = File.ReadAllText(Path.Combine(raiz, "Site.Master"));
            string cs = File.ReadAllText(Path.Combine(raiz, "Site.Master.cs"));

            Assert.Contains("foreach (var grupo in Menu)", master);
            Assert.Contains("<%: opcion.Texto %>", master);
            Assert.Contains("<%: directa.Texto %>", master);
            Assert.Contains("<%: grupo.Texto %>", master);
            Assert.Contains("MenuSitio.Para(", cs);
            // Ya no quedan los bloques por rol del marcado ni sus propiedades.
            foreach (string viejo in new[] { "EsAdmin", "EsConductor", "EsAdminSistema", "EsContabilidad" })
            {
                Assert.DoesNotMatch(@"\b" + viejo + @"\b", master);
                Assert.DoesNotMatch(@"public bool " + viejo + @"\b", cs);
            }
            // aria-current solo con un texto fijo.
            Assert.Equal(2, Regex.Matches(cs, @"aria-current=\\""page\\""").Count);
        }

        [Fact]
        public void Css_OpcionActivaYBarraSinPartirse()
        {
            string css = File.ReadAllText(Path.Combine(RaizWeb(), "Content", "paginas", "SiteMaster.css"));
            Assert.Matches(@"\.navbar \.dropdown-menu \.dropdown-item\.active\s*\{[^}]*var\(--sgv-primario", css);
            Assert.Matches(@"@media \(min-width: 992px\) and \(max-width: 1199\.98px\)\s*\{[^}]*\.user-nombre\s*\{\s*display:\s*none", css);
        }
    }
}
