using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    public class RolesHelperTests
    {
        // ─── Tarea 17: una sola fuente para la lista de roles en SQL y para OPERADOR ───

        private static readonly string[] TodosLosRolesConocidos =
        {
            RolesHelper.ROL_ADMIN, "ADMINISTRADOR", RolesHelper.ROL_ADMIN_TRANSPORTE, RolesHelper.ROL_ADMIN_SISTEMA,
            RolesHelper.ROL_CONDUCTOR, "CHOFER", RolesHelper.ROL_SUPERVISOR,
            RolesHelper.ROL_ADMIN_MAQUINARIA, RolesHelper.ROL_OPERADOR, RolesHelper.ROL_CONTABILIDAD
        };

        [Fact]
        public void RolesAdmin_CoincideConEsRolAdmin()
        {
            var segunEsRolAdmin = TodosLosRolesConocidos.Where(RolesHelper.EsRolAdmin).OrderBy(r => r);
            Assert.Equal(segunEsRolAdmin, RolesHelper.RolesAdmin.OrderBy(r => r));
            Assert.All(RolesHelper.RolesAdmin, r => Assert.Equal(r.Trim().ToUpper(), r)); // la consulta compara UPPER(TRIM(rol))
        }

        [Fact]
        public void RolesAvisosOperacion_SonAdminsMasSupervisor()
        {
            var esperado = TodosLosRolesConocidos
                .Where(r => RolesHelper.EsRolAdmin(r) || r == RolesHelper.ROL_SUPERVISOR)
                .OrderBy(r => r);
            Assert.Equal(esperado, RolesHelper.RolesAvisosOperacion().OrderBy(r => r));
            Assert.DoesNotContain(RolesHelper.ROL_ADMIN_SISTEMA, RolesHelper.RolesAvisosOperacion());
        }

        [Fact]
        public void ParametrosRoles_UnParametroPorRolYSoloNombresEnLaLista()
        {
            var roles = RolesHelper.RolesAvisosOperacion();
            var ps = WebSGV.Services.NotificacionService.ParametrosRoles(roles, out string lista);

            Assert.Equal(roles.Count, ps.Length);
            Assert.Equal(roles, ps.Select(p => (string)p.Value));
            Assert.Equal(string.Join(", ", ps.Select(p => p.ParameterName)), lista);
            Assert.DoesNotContain("'", lista);
        }

        [Fact]
        public void NotificacionService_NoTieneRolesEscritosEnSql()
        {
            string fuente = File.ReadAllText(Path.Combine(RaizRepo(), "WebSGV", "Services", "NotificacionService.cs"));
            Assert.DoesNotContain("'ADMINISTRADOR DE TRANSPORTE'", fuente);
            Assert.DoesNotContain("'SUPERVISOR'", fuente);
        }

        private static string RaizRepo()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        [Theory]
        [InlineData("OPERADOR", true)]
        [InlineData("OPERADOR ", true)]                             // espacio de más en la BD
        [InlineData(" operador", true)]
        [InlineData("CONDUCTOR", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void EsRolOperador_SinImportarMayusculasNiEspacios(string rol, bool esperado)
        {
            Assert.Equal(esperado, RolesHelper.EsRolOperador(rol));
        }

        [Theory]
        [InlineData("CONDUCTOR")]
        [InlineData("CHOFER")]
        [InlineData(" chofer ")]                                    // sin distinguir mayúsculas/espacios
        [InlineData("Conductor")]
        public void EsRolConductor_ConductorOChofer_True(string rol)
        {
            Assert.True(RolesHelper.EsRolConductor(rol));
        }

        [Theory]
        [InlineData("OPERADOR")]
        [InlineData("ADMIN")]
        [InlineData("")]
        [InlineData(null)]
        public void EsRolConductor_OtrosRoles_False(string rol)
        {
            Assert.False(RolesHelper.EsRolConductor(rol));
        }

        [Theory]
        [InlineData("CONDUCTOR", "~/Views/DashboardConductor.aspx")]
        [InlineData("chofer", "~/Views/DashboardConductor.aspx")]
        [InlineData("OPERADOR", "~/Views/DashboardOperador.aspx")]
        [InlineData("ADMINISTRADOR DE SISTEMA", "~/Views/DashboardAdminSistema.aspx")]
        [InlineData("ADMINISTRADOR DE MAQUINARIA", "~/Views/AsignacionesMaquinaria.aspx")] // Inicio.aspx lo rechaza
        [InlineData("CONTABILIDAD", "~/Views/LiquidacionesAprobadasContabilidad.aspx")]
        [InlineData("ADMIN", "~/Views/Inicio.aspx")]
        [InlineData("ADMINISTRADOR", "~/Views/Inicio.aspx")]
        [InlineData("Administrador de Transporte ", "~/Views/Inicio.aspx")]
        public void UrlInicioSegunRol_RolConocido_SuPagina(string rol, string esperado)
        {
            Assert.Equal(esperado, RolesHelper.UrlInicioSegunRol(rol));
        }

        [Theory]
        [InlineData("INVITADO")]
        [InlineData("ADMINISTRADOR DE GRIFO")]                     // rol retirado (2026-11): usuarios viejos van al login
        [InlineData("")]
        [InlineData(null)]
        public void UrlInicioSegunRol_RolDesconocido_Null(string rol)
        {
            Assert.Null(RolesHelper.UrlInicioSegunRol(rol));
        }

        [Theory]
        [InlineData("ADMIN", true)]
        [InlineData("ADMINISTRADOR", true)]
        [InlineData("ADMINISTRADOR DE TRANSPORTE", true)]
        [InlineData(" administrador de transporte ", true)]          // sin distinguir mayúsculas/espacios
        [InlineData("ADMINISTRADOR DE SISTEMA", false)]              // separación de funciones: solo usuarios y auditoría
        [InlineData("Administrador de Sistema", false)]
        [InlineData("SUPERVISOR", false)]
        [InlineData("ADMINISTRADOR DE GRIFO", false)]
        [InlineData("ADMINISTRADOR DE MAQUINARIA", false)]
        [InlineData("CONTABILIDAD", false)]
        [InlineData("OPERADOR", false)]
        [InlineData("CONDUCTOR", false)]
        [InlineData("CHOFER", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void EsRolAdmin_SoloAdministradoresDeLaOperacion(string rol, bool esperado)
        {
            Assert.Equal(esperado, RolesHelper.EsRolAdmin(rol));
        }

        [Fact]
        public void AdminSistema_NoEsAdmin_YSuInicioEsSuPanel()
        {
            Assert.False(RolesHelper.EsRolAdmin(RolesHelper.ROL_ADMIN_SISTEMA));
            Assert.Equal("~/Views/DashboardAdminSistema.aspx", RolesHelper.UrlInicioSegunRol(RolesHelper.ROL_ADMIN_SISTEMA));
        }

        // ===== Matriz de permisos por sección (TienePermiso(seccion, rol)) =====
        // Tabla literal: si cambia un permiso, debe cambiar aquí a propósito. Documentada en
        // docs/FLUJO_DE_TRABAJO_SGV.md (sección "Matriz de permisos").
        private static readonly string[] AdminOperacion = { "ADMIN", "ADMINISTRADOR", "ADMINISTRADOR DE TRANSPORTE" };

        private static string[] Con(params string[] otros) => AdminOperacion.Concat(otros).ToArray();

        private static readonly Dictionary<string, string[]> MatrizEsperada = new Dictionary<string, string[]>
        {
            { "DESPACHO",                   Con("SUPERVISOR") },
            { "FACTURA",                    Con("SUPERVISOR") },
            { "CPIC",                       Con("SUPERVISOR") },
            { "ORDEN_VIAJE",                Con("SUPERVISOR") },
            { "REGISTRO",                   Con("SUPERVISOR") },
            { "CONSULTAS",                  Con("SUPERVISOR") },
            { "INDICADORES",                Con("SUPERVISOR") },
            { "REGISTRO_CONDUCTORES",       Con("SUPERVISOR") },
            { "AUDITORIA",                  new[] { "ADMINISTRADOR DE SISTEMA" } },
            { "SEGUIMIENTO_EXPORTACION",    Con("SUPERVISOR") },
            { "DASHBOARD_EXPORTACION",      Con("SUPERVISOR") },
            { "DASHBOARD_CONDUCTOR",        Con("CONDUCTOR", "CHOFER") },
            { "MIS_VIAJES",                 Con("CONDUCTOR", "CHOFER") },
            { "MI_PERFIL",                  Con("CONDUCTOR", "CHOFER") },
            { "DASHBOARD_OPERADOR",         Con("OPERADOR", "ADMINISTRADOR DE MAQUINARIA") },
            { "PARTE_DIARIO",               Con("OPERADOR", "ADMINISTRADOR DE MAQUINARIA") },
            { "LIQUIDACIONES_CONTABILIDAD", Con("CONTABILIDAD", "SUPERVISOR") },
        };

        // "ADMINISTRADOR DE GRIFO" ya no es un rol (retirado 2026-11), pero puede quedar en Usuarios.rol:
        // se prueba para fijar que no tiene ningún permiso.
        private static readonly string[] RolesProbados =
        {
            "ADMIN", "ADMINISTRADOR", "ADMINISTRADOR DE TRANSPORTE", "ADMINISTRADOR DE SISTEMA", "SUPERVISOR",
            "CONDUCTOR", "CHOFER", "ADMINISTRADOR DE GRIFO", "ADMINISTRADOR DE MAQUINARIA", "OPERADOR",
            "CONTABILIDAD", " contabilidad ", "", null
        };

        public static IEnumerable<object[]> CasosMatriz()
        {
            foreach (string seccion in MatrizEsperada.Keys.Concat(new[] { "SECCION_DESCONOCIDA", "", null }))
            {
                string[] permitidos = seccion != null && MatrizEsperada.TryGetValue(seccion, out var p) ? p : new string[0];
                foreach (string rol in RolesProbados)
                {
                    string normalizado = (rol ?? "").Trim().ToUpperInvariant();
                    yield return new object[] { seccion, rol, permitidos.Contains(normalizado) };
                }
            }
        }

        [Theory]
        [MemberData(nameof(CasosMatriz))]
        public void TienePermiso_SigueLaMatrizDeSecciones(string seccion, string rol, bool esperado)
        {
            Assert.Equal(esperado, RolesHelper.TienePermiso(seccion, rol));
        }

        private static List<string> ConstantesDeRol() =>
            typeof(RolesHelper).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name.StartsWith("ROL_"))
                .Select(f => (string)f.GetRawConstantValue())
                .ToList();

        /// <summary>Un rol nuevo (constante ROL_*) debe entrar a la prueba de matriz: si no, sus permisos no se verifican.</summary>
        [Fact]
        public void ConstantesDeRol_EstanEnRolesProbados()
        {
            var constantes = ConstantesDeRol();
            Assert.True(constantes.Count >= 8, "Se esperaban al menos 8 constantes ROL_*; hay " + constantes.Count);
            var faltan = constantes.Where(c => !RolesProbados.Contains(c)).ToList();
            Assert.Empty(faltan);
        }

        /// <summary>Cada rol de la matriz es una constante de RolesHelper o un alias aceptado (ADMINISTRADOR, CHOFER).</summary>
        [Fact]
        public void RolesDeLaMatriz_SonConstantesOAlias()
        {
            var validos = new HashSet<string>(ConstantesDeRol()) { "ADMINISTRADOR", "CHOFER" };
            var desconocidos = MatrizEsperada.SelectMany(p => p.Value).Distinct().Where(r => !validos.Contains(r)).ToList();
            Assert.Empty(desconocidos);
        }

        [Fact]
        public void TienePermiso_SeccionSinDistinguirMayusculasNiEspacios()
        {
            Assert.True(RolesHelper.TienePermiso(" seguimiento_exportacion ", "Supervisor"));
        }

        /// <summary>Toda sección usada en el código existe en la matriz (una sección mal escrita negaría el acceso a todos).</summary>
        [Fact]
        public void SeccionesUsadasEnElCodigo_EstanEnLaMatriz()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            string raiz = Path.Combine(dir.FullName, "WebSGV");

            var patron = new Regex(@"(?:ValidarAccesoSeccion\(|TienePermiso\()\s*""([^""]+)""");
            var usadas = Directory.GetFiles(raiz, "*.cs", SearchOption.AllDirectories)
                .Where(f => f.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0)
                .SelectMany(f => patron.Matches(File.ReadAllText(f)).Cast<Match>()
                    .Select(m => new { Archivo = f.Substring(raiz.Length + 1), Seccion = m.Groups[1].Value }))
                .ToList();

            Assert.NotEmpty(usadas);
            var desconocidas = usadas.Where(u => !MatrizEsperada.ContainsKey(u.Seccion))
                .Select(u => u.Archivo + ": " + u.Seccion).ToList();
            Assert.Empty(desconocidas);
        }

        // ===== GpsAutomatico.ashx (estado e iniciar del tablero de seguimiento) =====

        [Theory]
        [InlineData(true, "ADMIN", true)]
        [InlineData(true, "ADMINISTRADOR DE TRANSPORTE", true)]
        [InlineData(true, "SUPERVISOR", true)]
        [InlineData(true, "CONDUCTOR", false)]
        [InlineData(true, "CONTABILIDAD", false)]
        [InlineData(true, "ADMINISTRADOR DE SISTEMA", false)]
        [InlineData(true, "ADMINISTRADOR DE GRIFO", false)]
        [InlineData(false, "ADMIN", false)]                         // sin sesión
        [InlineData(false, null, false)]
        public void GpsAutomatico_TableroSoloConSesionYPermisoDeSeguimiento(bool conSesion, string rol, bool esperado)
        {
            Assert.Equal(esperado, WebSGV.Views.Exportacion.GpsAutomatico.PuedeUsarTablero(conSesion, rol));
        }
    }
}
