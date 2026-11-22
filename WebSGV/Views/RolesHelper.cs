using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Clase para gestionar roles y permisos del sistema SGV
    /// </summary>
    public static class RolesHelper
    {
        public sealed class RolDisponible
        {
            public string Valor { get; set; }
            public string Texto { get; set; }
        }

        // Definición de roles
        public const string ROL_ADMIN = "ADMIN";
        public const string ROL_CONDUCTOR = "CONDUCTOR";
        public const string ROL_SUPERVISOR = "SUPERVISOR";
        public const string ROL_ADMIN_SISTEMA = "ADMINISTRADOR DE SISTEMA";
        public const string ROL_ADMIN_TRANSPORTE = "ADMINISTRADOR DE TRANSPORTE";
        public const string ROL_CONTABILIDAD = "CONTABILIDAD";

        private static readonly Dictionary<string, string> EtiquetasRol =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { ROL_ADMIN, "Administrador" },
                { ROL_SUPERVISOR, "Supervisor" },
                { ROL_CONDUCTOR, "Conductor" },
                { ROL_CONTABILIDAD, "Contabilidad" },
                { ROL_ADMIN_SISTEMA, "Administrador de Sistema" },
                { ROL_ADMIN_TRANSPORTE, "Administrador de Transporte" }
            };

        /// <summary>
        /// Obtiene el rol del usuario actual desde la sesión
        /// </summary>
        public static string ObtenerRolActual() => SesionHelper.RolActual;

        /// <summary>
        /// Verifica si el usuario actual es administrador de la operación (ver <see cref="EsRolAdmin"/>).
        /// </summary>
        public static bool EsAdmin()
        {
            return EsRolAdmin(ObtenerRolActual());
        }

        /// <summary>
        /// True si el rol es de administrador de la operación: ADMIN, ADMINISTRADOR (legado) o
        /// ADMINISTRADOR DE TRANSPORTE, sin importar mayúsculas ni espacios. No lee la sesión.
        /// ADMINISTRADOR DE SISTEMA no cuenta (separación de funciones, 2026-11): solo gestiona
        /// usuarios y consulta la auditoría; no opera despachos, liquidaciones, saldos ni reportes.
        /// </summary>
        public static bool EsRolAdmin(string rol)
        {
            string r = (rol ?? "").Trim().ToUpper();
            return RolesAdmin.Contains(r);
        }

        /// <summary>
        /// Valores de <c>Usuarios.rol</c> que <see cref="EsRolAdmin"/> acepta. Única fuente para las
        /// consultas SQL que buscan administradores (p. ej. a quién avisar por correo).
        /// </summary>
        public static readonly IReadOnlyList<string> RolesAdmin =
            new[] { ROL_ADMIN, "ADMINISTRADOR", ROL_ADMIN_TRANSPORTE };

        /// <summary>
        /// Roles que reciben los avisos por correo de la operación: los administradores
        /// (<see cref="RolesAdmin"/>) más SUPERVISOR.
        /// </summary>
        public static IReadOnlyList<string> RolesAvisosOperacion()
        {
            return RolesAdmin.Concat(new[] { ROL_SUPERVISOR }).ToList();
        }

        /// <summary>
        /// Roles que llevan segundo factor (código del celular al entrar, E4-14, U-079): administración
        /// (<see cref="EsRolAdmin"/>), SUPERVISOR, CONTABILIDAD y ADMINISTRADOR DE SISTEMA. Nunca el
        /// conductor (ya vuelve a escribir su contraseña para firmar). Sin el interruptor
        /// <c>Seguridad.ExigirSegundoFactor</c> el alta es voluntaria para estos roles; con él, obligatoria.
        /// Pura (no lee la sesión); fijada por <c>RolesHelperTests</c>.
        /// </summary>
        public static bool ExigeSegundoFactor(string rol)
        {
            string r = (rol ?? "").Trim().ToUpper();
            if (EsRolConductor(r)) return false;
            return EsRolAdmin(r) || r == ROL_SUPERVISOR || r == ROL_CONTABILIDAD || r == ROL_ADMIN_SISTEMA;
        }

        public static bool EsAdminTransporte()
        {
            return ObtenerRolActual() == ROL_ADMIN_TRANSPORTE;
        }

        /// <summary>
        /// Verifica si el usuario actual es Administrador de Sistema
        /// </summary>
        public static bool EsAdminSistema()
        {
            string rolActual = ObtenerRolActual();
            return rolActual == ROL_ADMIN_SISTEMA;
        }

        /// <summary>
        /// Verifica si el usuario actual es Conductor
        /// Acepta tanto "CONDUCTOR" como "CHOFER"
        /// </summary>
        public static bool EsConductor()
        {
            return EsRolConductor(ObtenerRolActual());
        }

        /// <summary>
        /// True si el rol indicado es de conductor ("CONDUCTOR" o "CHOFER"), sin importar
        /// mayúsculas ni espacios. No lee la sesión: sirve antes de guardarla (login).
        /// </summary>
        public static bool EsRolConductor(string rol)
        {
            string r = (rol ?? "").Trim().ToUpper();
            return r == ROL_CONDUCTOR || r == "CHOFER";
        }

        /// <summary>
        /// Página de inicio de cada rol (ruta virtual "~/Views/..."), o null si el rol no se
        /// reconoce. Única fuente para el login, RedirigirSegunRol y el menú del Site.Master.
        /// </summary>
        public static string UrlInicioSegunRol(string rol)
        {
            string r = (rol ?? "").Trim().ToUpper();
            if (EsRolConductor(r)) return "~/Views/DashboardConductor.aspx";

            switch (r)
            {
                case ROL_ADMIN_SISTEMA:    return "~/Views/DashboardAdminSistema.aspx";
                case ROL_CONTABILIDAD:     return "~/Views/LiquidacionesAprobadasContabilidad.aspx";
                case ROL_ADMIN:
                case "ADMINISTRADOR":
                case ROL_ADMIN_TRANSPORTE:
                case ROL_SUPERVISOR:       return "~/Views/Inicio.aspx";
                default:                   return null;
            }
        }

        /// <summary>
        /// Verifica si el usuario actual es del área de Contabilidad
        /// </summary>
        public static bool EsContabilidad()
        {
            string rolActual = ObtenerRolActual();
            return rolActual == ROL_CONTABILIDAD;
        }

        /// <summary>
        /// Verifica si el usuario tiene sesión activa
        /// </summary>
        public static bool TieneSesionActiva() => SesionHelper.HaySesionActual;

        /// <summary>
        /// Verifica si el usuario actual (rol de la sesión) tiene permiso para una sección.
        /// </summary>
        public static bool TienePermiso(string seccion) => TienePermiso(seccion, ObtenerRolActual());

        /// <summary>
        /// Matriz de permisos por sección: true si el rol indicado puede entrar a la sección.
        /// Pura (no lee la sesión), sin importar mayúsculas ni espacios; sección vacía o desconocida → false.
        /// La matriz está fijada en RolesHelperTests: cualquier cambio de permisos debe cambiar también la prueba.
        /// Solo tiene las secciones que alguna página consulta (<see cref="ValidarAccesoSeccion"/> o
        /// <see cref="TienePermiso(string)"/>); RolesHelperTests exige que cada una se use (E4-3, U-081).
        /// </summary>
        public static bool TienePermiso(string seccion, string rol)
        {
            if (string.IsNullOrWhiteSpace(seccion)) return false;
            string r = (rol ?? "").Trim().ToUpperInvariant();
            return Matriz.TryGetValue(seccion.Trim(), out Func<string, bool> permite) && permite(r);
        }

        /// <summary>Secciones de la matriz (<see cref="TienePermiso(string, string)"/>).</summary>
        public static IReadOnlyCollection<string> Secciones => Matriz.Keys;

        private static bool AdminOSupervisor(string r) => EsRolAdmin(r) || r == ROL_SUPERVISOR;

        /// <summary>Sección → roles que entran (el rol llega ya en mayúsculas y sin espacios).</summary>
        private static readonly Dictionary<string, Func<string, bool>> Matriz =
            new Dictionary<string, Func<string, bool>>(StringComparer.OrdinalIgnoreCase)
            {
                // Operación del transporte: administración y supervisión
                { "ORDEN_VIAJE",             AdminOSupervisor },   // LiquidacionesPendientes
                { "REGISTRO",                AdminOSupervisor },   // Tractos, Semirremolques, Clientes, Catálogo del cliente, Plantas, Peajes
                { "REGISTRO_CONDUCTORES",    AdminOSupervisor },   // RegistroChoferes
                { "SEGUIMIENTO_EXPORTACION", AdminOSupervisor },   // Exportacion/* y GpsAutomatico.ashx

                // Solo administración (U-081, 2026-11-22: SUPERVISOR no entra, como el menú)
                { "FACTURAS_FLETE",          EsRolAdmin },         // FacturasFlete
                { "REGISTRO_PRODUCTOS",      EsRolAdmin },         // RegistroProductos
                { "REGISTRO_RUTAS",          EsRolAdmin },         // RegistroRutas

                // Consulta para Contabilidad (solo lectura)
                { "LIQUIDACIONES_CONTABILIDAD", r => r == ROL_CONTABILIDAD || AdminOSupervisor(r) },
            };

        /// <summary>
        /// True si el sistema reconoce el rol (tiene página de inicio): los del catálogo y sus
        /// alias (ADMINISTRADOR, CHOFER). Un rol retirado (p. ej. ADMINISTRADOR DE GRIFO,
        /// OPERADOR, ADMINISTRADOR DE MAQUINARIA) no es vigente aunque siga en algún usuario.
        /// </summary>
        public static bool EsRolVigente(string rol) => UrlInicioSegunRol(rol) != null;

        /// <summary>
        /// True si GestionUsuarios puede guardar <paramref name="rolNuevo"/>: debe ser vigente, salvo
        /// en una edición que conserva el mismo rol que el usuario ya tenía (no se le obliga a
        /// cambiarlo). En un alta <paramref name="rolAnterior"/> es null. Ignora mayúsculas y espacios.
        /// </summary>
        public static bool PuedeAsignarseRol(string rolNuevo, string rolAnterior)
        {
            string nuevo = (rolNuevo ?? "").Trim().ToUpperInvariant();
            if (nuevo.Length == 0) return false;
            if (EsRolVigente(nuevo)) return true;
            return rolAnterior != null && nuevo == rolAnterior.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Obtiene los roles que se pueden asignar: catálogo base + roles vigentes que ya
        /// existan en la BD (alias como ADMINISTRADOR o CHOFER). Los roles retirados no se
        /// ofrecen aunque quede algún usuario antiguo con ellos.
        /// </summary>
        public static List<RolDisponible> ObtenerRolesDisponibles()
        {
            var rolesBd = new List<string>();
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    "SELECT DISTINCT LTRIM(RTRIM(rol)) AS rol FROM Usuarios WHERE rol IS NOT NULL AND LTRIM(RTRIM(rol)) <> ''");
                foreach (DataRow row in dt.Rows)
                    rolesBd.Add(row["rol"].ToString());
            }
            catch
            {
                // Si la BD no responde, se mantiene el catálogo base para no romper la UI.
            }

            return CombinarRolesDisponibles(rolesBd);
        }

        /// <summary>
        /// Parte pura de <see cref="ObtenerRolesDisponibles"/>: une el catálogo base con los
        /// roles leídos de la BD, descartando los que no son vigentes (<see cref="EsRolVigente"/>),
        /// sin duplicados (ignora mayúsculas y espacios) y ordenados.
        /// </summary>
        public static List<RolDisponible> CombinarRolesDisponibles(IEnumerable<string> rolesBd)
        {
            var roles = new List<string>(EtiquetasRol.Keys);
            if (rolesBd != null)
                roles.AddRange(rolesBd.Where(EsRolVigente));

            return roles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                .Select(r => new RolDisponible
                {
                    Valor = r,
                    Texto = EtiquetasRol.ContainsKey(r) ? EtiquetasRol[r] : r
                })
                .ToList();
        }

        /// <summary>
        /// Obtiene el nombre del usuario actual
        /// </summary>
        public static string ObtenerNombreUsuario()
        {
            string nombre = SesionHelper.NombreActual;
            return nombre.Length > 0 ? nombre : "Usuario";
        }

        /// <summary>
        /// Redirige al usuario a su página de inicio según su rol
        /// </summary>
        public static void RedirigirSegunRol()
        {
            if (!TieneSesionActiva())
            {
                HttpContext.Current.Response.Redirect("~/Views/Login.aspx");
                return;
            }

            // Si no tiene rol reconocido, redirigir a login
            HttpContext.Current.Response.Redirect(
                UrlInicioSegunRol(ObtenerRolActual()) ?? "~/Views/Login.aspx");
        }

        /// <summary>
        /// Valida que el usuario tenga acceso a la página actual
        /// Si no tiene acceso, redirige según su rol
        /// </summary>
        public static void ValidarAccesoSeccion(string seccion)
        {
            if (!TieneSesionActiva())
            {
                HttpContext.Current.Response.Redirect("~/Views/Login.aspx");
                return;
            }

            if (!TienePermiso(seccion))
            {
                RedirigirSegunRol();
            }
        }
    }
}
