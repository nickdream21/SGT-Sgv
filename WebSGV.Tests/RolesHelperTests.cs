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
        // ─── Tarea 17: una sola fuente para la lista de roles en SQL ───

        private static readonly string[] TodosLosRolesConocidos =
        {
            RolesHelper.ROL_ADMIN, "ADMINISTRADOR", RolesHelper.ROL_ADMIN_TRANSPORTE, RolesHelper.ROL_ADMIN_SISTEMA,
            RolesHelper.ROL_CONDUCTOR, "CHOFER", RolesHelper.ROL_SUPERVISOR, RolesHelper.ROL_CONTABILIDAD
        };

        [Fact]
        public void RolesAdmin_CoincideConEsRolAdmin()
        {
            var segunEsRolAdmin = TodosLosRolesConocidos.Where(RolesHelper.EsRolAdmin).OrderBy(r => r);
            Assert.Equal(segunEsRolAdmin, RolesHelper.RolesAdmin.OrderBy(r => r));
            Assert.All(RolesHelper.RolesAdmin, r => Assert.Equal(r.Trim().ToUpper(), r)); // la consulta compara UPPER(TRIM(rol))
        }

        /// <summary>
        /// E4-14 (U-079, opción recomendada): llevan segundo factor administración, supervisor, contabilidad y
        /// administrador de sistema; nunca el conductor. Cambiar esta lista es una decisión a propósito.
        /// </summary>
        [Fact]
        public void ExigeSegundoFactor_TodosMenosElConductor()
        {
            var conFactor = TodosLosRolesConocidos.Where(RolesHelper.ExigeSegundoFactor).OrderBy(r => r);
            Assert.Equal(new[]
            {
                RolesHelper.ROL_ADMIN, "ADMINISTRADOR", RolesHelper.ROL_ADMIN_SISTEMA, RolesHelper.ROL_ADMIN_TRANSPORTE,
                RolesHelper.ROL_CONTABILIDAD, RolesHelper.ROL_SUPERVISOR
            }.OrderBy(r => r), conFactor);

            Assert.False(RolesHelper.ExigeSegundoFactor(RolesHelper.ROL_CONDUCTOR));
            Assert.False(RolesHelper.ExigeSegundoFactor(" chofer "));
            Assert.True(RolesHelper.ExigeSegundoFactor(" supervisor "));
            Assert.True(RolesHelper.ExigeSegundoFactor("administrador de sistema"));
            Assert.False(RolesHelper.ExigeSegundoFactor(null));
            Assert.False(RolesHelper.ExigeSegundoFactor(""));
            Assert.False(RolesHelper.ExigeSegundoFactor("OPERADOR"));
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
        [InlineData("ADMINISTRADOR DE SISTEMA", "~/Views/DashboardAdminSistema.aspx")]
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
        [InlineData("ADMINISTRADOR DE GRIFO")]                     // roles retirados (2026-11): usuarios viejos van al login
        [InlineData("OPERADOR")]
        [InlineData("ADMINISTRADOR DE MAQUINARIA")]
        [InlineData("")]
        [InlineData(null)]
        public void UrlInicioSegunRol_RolDesconocido_Null(string rol)
        {
            Assert.Null(RolesHelper.UrlInicioSegunRol(rol));
        }

        // ===== Rol que GestionUsuarios deja guardar (PuedeAsignarseRol) =====

        [Theory]
        [InlineData("ADMIN")]
        [InlineData("ADMINISTRADOR DE TRANSPORTE")]
        [InlineData("ADMINISTRADOR DE SISTEMA")]
        [InlineData("SUPERVISOR")]
        [InlineData("CONDUCTOR")]
        [InlineData("CONTABILIDAD")]
        [InlineData(" supervisor ")]
        [InlineData("ADMINISTRADOR")]                              // alias vigentes
        [InlineData("CHOFER")]
        public void PuedeAsignarseRol_AltaConRolVigente_Si(string rol)
        {
            Assert.True(RolesHelper.PuedeAsignarseRol(rol, null));
        }

        [Theory]
        [InlineData("OPERADOR")]
        [InlineData("ADMINISTRADOR DE GRIFO")]
        [InlineData("ADMINISTRADOR DE MAQUINARIA")]
        [InlineData(" operador ")]
        [InlineData("INVITADO")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void PuedeAsignarseRol_AltaConRolRetiradoODesconocido_No(string rol)
        {
            Assert.False(RolesHelper.PuedeAsignarseRol(rol, null));
        }

        [Theory]
        [InlineData("OPERADOR", "OPERADOR")]
        [InlineData("OPERADOR", " operador ")]                     // el guardado normaliza; la BD puede tenerlo distinto
        [InlineData("ADMINISTRADOR DE GRIFO", "Administrador de Grifo")]
        [InlineData(" administrador de maquinaria", "ADMINISTRADOR DE MAQUINARIA ")]
        public void PuedeAsignarseRol_EdicionQueConservaSuRolRetirado_Si(string rolNuevo, string rolAnterior)
        {
            Assert.True(RolesHelper.PuedeAsignarseRol(rolNuevo, rolAnterior));
        }

        [Theory]
        [InlineData("OPERADOR", "ADMINISTRADOR DE GRIFO")]          // de un retirado a otro retirado
        [InlineData("ADMINISTRADOR DE MAQUINARIA", "OPERADOR")]
        [InlineData("OPERADOR", "SUPERVISOR")]                      // de un vigente a un retirado
        [InlineData("ADMINISTRADOR DE GRIFO", "ADMIN")]
        [InlineData("", "OPERADOR")]
        [InlineData(null, "OPERADOR")]
        [InlineData("", "")]                                        // rol vacío nunca, aunque el anterior estuviera vacío
        public void PuedeAsignarseRol_EdicionHaciaRolRetiradoAjeno_No(string rolNuevo, string rolAnterior)
        {
            Assert.False(RolesHelper.PuedeAsignarseRol(rolNuevo, rolAnterior));
        }

        [Theory]
        [InlineData("SUPERVISOR", "OPERADOR")]                      // sacar a un usuario de un rol retirado
        [InlineData("ADMINISTRADOR", "ADMINISTRADOR DE GRIFO")]
        [InlineData("CHOFER", "ADMINISTRADOR DE MAQUINARIA")]
        [InlineData("CONDUCTOR", "SUPERVISOR")]
        [InlineData("ADMIN", "ADMIN")]
        public void PuedeAsignarseRol_EdicionHaciaRolVigente_Si(string rolNuevo, string rolAnterior)
        {
            Assert.True(RolesHelper.PuedeAsignarseRol(rolNuevo, rolAnterior));
        }

        // ===== Roles que GestionUsuarios ofrece (CombinarRolesDisponibles) =====

        [Theory]
        [InlineData("ADMINISTRADOR DE GRIFO")]                     // retirados: aunque quede un usuario con ese rol, no se ofrecen
        [InlineData("OPERADOR")]
        [InlineData(" operador ")]
        [InlineData("ADMINISTRADOR DE MAQUINARIA")]
        [InlineData("INVITADO")]
        public void CombinarRolesDisponibles_RolRetiradoDeLaBd_NoSeOfrece(string rolBd)
        {
            var valores = RolesHelper.CombinarRolesDisponibles(new[] { rolBd }).Select(r => r.Valor).ToList();
            Assert.DoesNotContain(valores, v => string.Equals(v, rolBd.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData("ADMINISTRADOR")]                              // alias vigentes: solo salen si hay usuarios con ellos
        [InlineData("CHOFER")]
        public void CombinarRolesDisponibles_AliasVigenteDeLaBd_SeOfrece(string rolBd)
        {
            Assert.DoesNotContain(RolesHelper.CombinarRolesDisponibles(new string[0]), r => r.Valor == rolBd);
            Assert.Contains(RolesHelper.CombinarRolesDisponibles(new[] { rolBd }), r => r.Valor == rolBd);
        }

        [Fact]
        public void CombinarRolesDisponibles_SinBd_EsElCatalogoSinDuplicados()
        {
            var esperados = new[]
            {
                RolesHelper.ROL_ADMIN, RolesHelper.ROL_ADMIN_SISTEMA, RolesHelper.ROL_ADMIN_TRANSPORTE,
                RolesHelper.ROL_CONDUCTOR, RolesHelper.ROL_CONTABILIDAD, RolesHelper.ROL_SUPERVISOR
            }.OrderBy(r => r, StringComparer.OrdinalIgnoreCase);

            Assert.Equal(esperados, RolesHelper.CombinarRolesDisponibles(null).Select(r => r.Valor));
            // Un rol del catálogo que también está en la BD (con otro formato) no se repite.
            Assert.Equal(esperados, RolesHelper.CombinarRolesDisponibles(new[] { " supervisor ", "CONDUCTOR" }).Select(r => r.Valor));
            Assert.Equal("Contabilidad",
                RolesHelper.CombinarRolesDisponibles(null).Single(r => r.Valor == RolesHelper.ROL_CONTABILIDAD).Texto);
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

        // E4-3 (U-081, opción recomendada, 2026-11-22: "dejarlo como funciona hoy"): solo las secciones
        // que alguna página consulta. SUPERVISOR no tiene FACTURAS_FLETE, REGISTRO_PRODUCTOS ni
        // REGISTRO_RUTAS (esas páginas y el menú ya eran solo de administración) y la administración ya
        // no tiene DASHBOARD_CONDUCTOR (la página la rebotaba). Se quitaron DESPACHO, FACTURA, CPIC,
        // CONSULTAS, INDICADORES, AUDITORIA, DASHBOARD_EXPORTACION, DASHBOARD_CONDUCTOR, MIS_VIAJES y
        // MI_PERFIL: ninguna página las consultaba (M-064). Quién entra a cada página: GuardasAccesoTests.
        private static readonly Dictionary<string, string[]> MatrizEsperada = new Dictionary<string, string[]>
        {
            { "ORDEN_VIAJE",                Con("SUPERVISOR") },
            { "REGISTRO",                   Con("SUPERVISOR") },
            { "REGISTRO_CONDUCTORES",       Con("SUPERVISOR") },
            { "SEGUIMIENTO_EXPORTACION",    Con("SUPERVISOR") },
            { "FACTURAS_FLETE",             AdminOperacion },
            { "REGISTRO_PRODUCTOS",         AdminOperacion },
            { "REGISTRO_RUTAS",             AdminOperacion },
            { "LIQUIDACIONES_CONTABILIDAD", Con("CONTABILIDAD", "SUPERVISOR") },
        };

        // "ADMINISTRADOR DE GRIFO", "ADMINISTRADOR DE MAQUINARIA" y "OPERADOR" ya no son roles (retirados
        // 2026-11), pero pueden quedar en Usuarios.rol: se prueban para fijar que no tienen ningún permiso.
        private static readonly string[] RolesProbados =
        {
            "ADMIN", "ADMINISTRADOR", "ADMINISTRADOR DE TRANSPORTE", "ADMINISTRADOR DE SISTEMA", "SUPERVISOR",
            "CONDUCTOR", "CHOFER", "ADMINISTRADOR DE GRIFO", "ADMINISTRADOR DE MAQUINARIA", "OPERADOR",
            "CONTABILIDAD", " contabilidad ", "", null
        };

        public static IEnumerable<object[]> CasosMatriz()
        {
            // Las quitadas en E4-3 se prueban como desconocidas: nadie entra.
            var quitadas = new[] { "DESPACHO", "FACTURA", "CPIC", "CONSULTAS", "INDICADORES", "AUDITORIA",
                                   "DASHBOARD_EXPORTACION", "DASHBOARD_CONDUCTOR", "MIS_VIAJES", "MI_PERFIL" };
            foreach (string seccion in MatrizEsperada.Keys.Concat(quitadas).Concat(new[] { "SECCION_DESCONOCIDA", "", null }))
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
            Assert.True(constantes.Count >= 6, "Se esperaban al menos 6 constantes ROL_*; hay " + constantes.Count);
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

        /// <summary>La matriz del código tiene exactamente las secciones de la tabla (ni una más sin probar).</summary>
        [Fact]
        public void Secciones_SonLasDeLaTabla()
        {
            Assert.Equal(MatrizEsperada.Keys.OrderBy(k => k), RolesHelper.Secciones.OrderBy(k => k));
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

        // E4-8: "Actualizar GPS" solo por POST con JSON; un formulario de otro sitio (text/plain, urlencoded,
        // multipart: sin preflight) no puede lanzarla con la cookie del usuario.
        [Theory]
        [InlineData("POST", true, "application/json", 0)]
        [InlineData("POST", true, "application/json; charset=utf-8", 0)]
        [InlineData("POST", true, "Application/JSON;charset=UTF-8", 0)]
        [InlineData("POST", true, "  application/json  ", 0)]
        [InlineData("POST", true, "text/plain", 415)]
        [InlineData("POST", true, "text/plain; charset=application/json", 415)]
        [InlineData("POST", true, "application/x-www-form-urlencoded", 415)]
        [InlineData("POST", true, "multipart/form-data; boundary=x", 415)]
        [InlineData("POST", true, "application/jsonx", 415)]
        [InlineData("POST", true, "", 415)]
        [InlineData("POST", true, null, 415)]
        [InlineData("POST", false, "text/plain", 403)]              // sin sesión o permiso: lo de siempre, antes que el 415
        [InlineData("POST", false, "application/json", 403)]
        [InlineData("GET", true, "application/json", 405)]
        [InlineData("GET", false, null, 405)]
        [InlineData("post", true, "application/json", 405)]
        [InlineData("PUT", true, "application/json", 405)]
        public void GpsAutomatico_IniciarSoloPostConJson(string metodo, bool puede, string contentType, int esperado)
        {
            Assert.Equal(esperado, WebSGV.Views.Exportacion.GpsAutomatico.RechazoIniciar(metodo, puede, contentType));
        }

        [Fact]
        public void GpsAutomatico_ElTableroMandaJsonAlIniciar()
        {
            string js = File.ReadAllText(Path.Combine(RaizRepo(), "WebSGV", "Scripts", "paginas", "Exportacion", "RegistroSeguimiento.js"));
            Assert.Contains("'Content-Type': 'application/json'", js);
            Assert.Contains("pedir(SGV.urlIniciarGps, 'POST')", js);
        }
    }
}
