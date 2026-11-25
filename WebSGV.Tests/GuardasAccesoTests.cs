using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E4-2: las guardas de acceso sostenidas por pruebas (de fuente, sin BD; corren en CI).
    ///
    /// - Cada punto de entrada (.aspx, .ashx, .asmx, .svc) está en una de tres tablas: guarda
    ///   estándar esperada, pública o manual justificada. Uno nuevo que no esté falla: quien lo agrega
    ///   decide su guarda a propósito (como en <c>RolesHelperTests</c>, cambiar un permiso es cambiar
    ///   esta prueba).
    /// - La guarda estándar es la PRIMERA sentencia de <c>Page_Load</c> (por eso nunca dentro de un
    ///   <c>try</c>, que se tragaría el ThreadAbortException del Redirect y seguiría ejecutando).
    /// - Cada <c>[WebMethod]</c> (que no pasa por Page_Load) tiene <c>EnableSession = true</c> y
    ///   empieza por sus comprobaciones de sesión y rol en su forma negada (<c>if (!X() || ...) return</c>);
    ///   antes de la última solo hay otras comprobaciones o preparaciones de una lista cerrada
    ///   (<see cref="FallasDeComprobaciones"/>).
    /// - Sin caché (no-store): Site.Master en su Page_Init, las páginas sin master en Page_Load.
    /// </summary>
    public class GuardasAccesoTests
    {
        // ── Tablas (rutas relativas a WebSGV, con '/') ─────────────────────

        /// <summary>Página → primera sentencia esperada de su Page_Load.</summary>
        private static readonly Dictionary<string, string> GuardaEsperada =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Views/Auditoria.aspx",                         "SecurityHelper.ExigirRolAdminSistema()" },
                // Ayuda (E5-21): con sesión; el tema y sus imágenes se filtran por rol con AyudaCatalogo.PuedeVer.
                { "Views/Ayuda.aspx",                             "SecurityHelper.ExigirSesion()" },
                { "Views/AyudaImagen.aspx",                       "SecurityHelper.ExigirSesion()" },
                { "Views/BandejaDocumentos.aspx",                 "SecurityHelper.ExigirRolAdmin()" },
                { "Views/CatalogoCliente.aspx",                   "RolesHelper.ValidarAccesoSeccion(\"REGISTRO\")" },
                // Conductor (su conformidad) o quien consulta la orden: el rol se decide después, en el GET.
                { "Views/ConformarLiquidacion.aspx",              "SecurityHelper.ExigirSesion()" },
                { "Views/DashboardAdminSistema.aspx",             "SecurityHelper.ExigirRolAdminSistema()" },
                { "Views/DashboardConductor.aspx",                "SecurityHelper.ExigirRolConductor()" },
                // La clave de descarga es por sesión (DocumentoHelper.ObtenerRutaPermitida).
                { "Views/DescargarDocumento.aspx",                "SecurityHelper.ExigirSesion()" },
                // El permiso por orden lo decide AccesoLiquidacion.PuedeConsultar (403 si no).
                { "Views/DescargarPdfOrdenViaje.aspx",            "SecurityHelper.ExigirSesion()" },
                { "Views/DestinoDescarga.aspx",                   "SecurityHelper.ExigirRolAdmin()" },
                // Conductor (la suya) o quien consulta la orden: el rol se decide después, en el GET.
                { "Views/DetalleOrdenViaje.aspx",                 "SecurityHelper.ExigirSesion()" },
                { "Views/Exportacion/DashboardExportacion.aspx",  "RolesHelper.ValidarAccesoSeccion(\"SEGUIMIENTO_EXPORTACION\")" },
                { "Views/Exportacion/ImportarSeguimiento.aspx",   "RolesHelper.ValidarAccesoSeccion(\"SEGUIMIENTO_EXPORTACION\")" },
                { "Views/Exportacion/RegistroSeguimiento.aspx",   "RolesHelper.ValidarAccesoSeccion(\"SEGUIMIENTO_EXPORTACION\")" },
                { "Views/Exportacion/SeguimientoViaje.aspx",      "RolesHelper.ValidarAccesoSeccion(\"SEGUIMIENTO_EXPORTACION\")" },
                { "Views/FacturasFlete.aspx",                     "RolesHelper.ValidarAccesoSeccion(\"FACTURAS_FLETE\")" },
                { "Views/FirmarLiquidacion.aspx",                 "SecurityHelper.ExigirRolConductor()" },
                { "Views/GestionUsuarios.aspx",                   "SecurityHelper.ExigirRolAdminSistema()" },
                { "Views/Inicio.aspx",                            "SecurityHelper.ExigirRolAdmin()" },
                { "Views/LiquidacionesAprobadasContabilidad.aspx","RolesHelper.ValidarAccesoSeccion(\"LIQUIDACIONES_CONTABILIDAD\")" },
                { "Views/LiquidacionesPendientes.aspx",           "RolesHelper.ValidarAccesoSeccion(\"ORDEN_VIAJE\")" },
                { "Views/ListaDespachos.aspx",                    "SecurityHelper.ExigirRolAdmin()" },
                // Los roles de RolesHelper.ExigeSegundoFactor; otro rol (conductor) vuelve a su inicio después (E4-14).
                { "Views/MiSeguridad.aspx",                       "SecurityHelper.ExigirSesion()" },
                { "Views/MovimientosViaje.aspx",                  "SecurityHelper.ExigirRolAdmin()" },
                { "Views/RegistroChoferes.aspx",                  "RolesHelper.ValidarAccesoSeccion(\"REGISTRO_CONDUCTORES\")" },
                { "Views/RegistroClientes.aspx",                  "RolesHelper.ValidarAccesoSeccion(\"REGISTRO\")" },
                { "Views/RegistroDespacho.aspx",                  "SecurityHelper.ExigirRolAdmin()" },
                { "Views/RegistroPeajes.aspx",                    "RolesHelper.ValidarAccesoSeccion(\"REGISTRO\")" },
                { "Views/RegistroPlantas.aspx",                   "RolesHelper.ValidarAccesoSeccion(\"REGISTRO\")" },
                { "Views/RegistroSemiremolques.aspx",             "RolesHelper.ValidarAccesoSeccion(\"REGISTRO\")" },
                { "Views/RegistroTractos.aspx",                   "RolesHelper.ValidarAccesoSeccion(\"REGISTRO\")" },
                { "Views/ReporteCargas.aspx",                     "SecurityHelper.ExigirRolAdmin()" },
                { "Views/ReporteGps.aspx",                        "SecurityHelper.ExigirRolAdmin()" },
                { "Views/ReportesOrdenesViaje.aspx",              "SecurityHelper.ExigirRolAdmin()" },
                { "Views/SaldosConductores.aspx",                 "SecurityHelper.ExigirRolAdmin()" },
            };

        /// <summary>Puntos de entrada sin sesión, a propósito, y por qué.</summary>
        private static readonly Dictionary<string, string> Publicas =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Views/Login.aspx",                "inicio de sesión" },
                { "Views/RecuperarContrasena.aspx",  "pedir el correo de recuperación" },
                { "Views/RestablecerContrasena.aspx","enlace con token del correo de recuperación" },
                { "Views/SegundoFactor.aspx",        "código del segundo factor (E4-14): aún no hay sesión, solo el estado pendiente de 5 min sin rol" },
                { "Views/VerificarDocumento.aspx",   "verificación pública del PDF firmado (código del pie)" },
                { "Views/Error.aspx",                "página de error: debe mostrarse siempre" },
                { "Views/Error404.aspx",             "página 404: debe mostrarse siempre" },
                { "Salud.ashx",                      "prueba de salud del monitoreo, sin datos" },
                { "CspInforme.ashx",                 "informes CSP del navegador (E4-5): solo POST, 8 KB, con límite; no lee ni escribe la BD" },
            };

        /// <summary>
        /// Guardas manuales justificadas (lo mínimo posible): motivo y lo que su punto de entrada debe
        /// seguir conteniendo.
        /// </summary>
        private static readonly Dictionary<string, (string Motivo, string[] Marcadores)> Manuales =
            new Dictionary<string, (string, string[])>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "Views/Exportacion/GpsAutomatico.ashx",
                    ("lo llama un cron con la clave X-SGV-Clave, sin sesión; el tablero usa sesión y permiso. " +
                     "ProcessRequest pasa la clave, la sesión y el rol a Atender, que decide (sus ramas: GpsAutomaticoTests, E6-9)",
                     new[] { "ConfigurationManager.AppSettings[\"GpsAutomaticoClave\"]", "Headers[\"X-SGV-Clave\"]",
                             "SesionHelper.HaySesion(ctx.Session)", "SesionHelper.Rol(ctx.Session)", "Atender(solicitud," })
                },
            };

        /// <summary>Lista cerrada de primeras sentencias válidas (terminan la respuesta si no pasa).</summary>
        private static readonly Regex GuardaEstandar = new Regex(
            @"^(SecurityHelper\.Exigir(Sesion|RolAdmin|RolAdminSistema|RolConductor)\(\)" +
            @"|RolesHelper\.ValidarAccesoSeccion\(""[A-Z_]+""\))$");

        /// <summary>
        /// Comprobaciones válidas al inicio de un [WebMethod] (prefijo del texto de la condición) →
        /// inicializador que debe tener la variable de la condición, o null si es una llamada directa.
        /// </summary>
        private static readonly Dictionary<string, string> ComprobacionesConocidas =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // Sesión
                { "SecurityHelper.TieneSesionActiva()",      null },
                { "SesionHelper.HaySesionActual",            null },
                { "contexto.IdUsuario==0",                   "ContextoFirmaActual(" },
                { "idUsuario==0",                            "SesionHelper.ObtenerUsuarioId(" },
                { "idConductorSesion==null",                 "SesionHelper.IdConductor(" },
                // Sesión y rol (y la orden) en una
                { "error!=null",                             "ValidarConductor(" },
                { "PuedeGestionarLiquidaciones()",           null },
                { "PuedeConsultarOrden(",                    null },
                // Rol
                { "RolesHelper.TienePermiso(",               null },
                { "RolesHelper.EsConductor()",               null },
                { "SecurityHelper.EsConductor()",            null },
                { "SecurityHelper.EsAdmin()",                null },
                { "SecurityHelper.EsAdminSistema()",         null },
                { "SecurityHelper.PuedeConsultarLiquidacionesGlobales()", null },
                { "AccesoLiquidacion.VeTodasLasOrdenes(",    null },
            };

        /// <summary>
        /// "archivo.cs:Metodo" → comprobaciones que deben estar en un <c>if (...) return/throw</c> al inicio
        /// (sin espacios). Un WebMethod nuevo que no esté aquí hace fallar la prueba.
        /// </summary>
        private static readonly Dictionary<string, string[]> ComprobacionesWebMethod =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Views/ConformarLiquidacion.aspx.cs:ObtenerLiquidacion",   new[] { "error!=null" } },
                { "Views/ConformarLiquidacion.aspx.cs:RegistrarConformidad", new[] { "error!=null" } },
                { "Views/ConformarLiquidacion.aspx.cs:ObservarLiquidacion",  new[] { "error!=null" } },

                { "Views/DashboardConductor.aspx.cs:MantenerSesionActiva",
                    new[] { "SecurityHelper.TieneSesionActiva()", "RolesHelper.EsConductor()" } },
                { "Views/DashboardConductor.aspx.cs:RetirarLiquidacion",
                    new[] { "idConductorSesion==null", "RolesHelper.EsConductor()" } },

                { "Views/Exportacion/SeguimientoViaje.aspx.cs:ObtenerRuta",
                    new[] { "SecurityHelper.TieneSesionActiva()", "RolesHelper.TienePermiso(\"SEGUIMIENTO_EXPORTACION\")" } },

                { "Views/LiquidacionesAprobadasContabilidad.aspx.cs:ObtenerLiquidacionesAprobadasContabilidad",
                    new[] { "SesionHelper.HaySesionActual", "RolesHelper.TienePermiso(\"LIQUIDACIONES_CONTABILIDAD\")" } },
                { "Views/LiquidacionesAprobadasContabilidad.aspx.cs:BuscarConductores",
                    new[] { "SesionHelper.HaySesionActual", "RolesHelper.TienePermiso(\"LIQUIDACIONES_CONTABILIDAD\")" } },

                { "Views/LiquidacionesPendientes.aspx.cs:BuscarConductores",              new[] { "PuedeGestionarLiquidaciones()" } },
                { "Views/LiquidacionesPendientes.aspx.cs:ObtenerDetalleLiquidacion",      new[] { "PuedeConsultarOrden(idOrdenViaje)" } },
                { "Views/LiquidacionesPendientes.aspx.cs:ObtenerLiquidacionesAprobadas",
                    new[] { "SecurityHelper.TieneSesionActiva()", "SecurityHelper.PuedeConsultarLiquidacionesGlobales()" } },
                // Las dos delegan en DevolverARevisionAdmin (la prueba sigue la llamada).
                { "Views/LiquidacionesPendientes.aspx.cs:RevertirAprobacion",             new[] { "PuedeGestionarLiquidaciones()" } },
                { "Views/LiquidacionesPendientes.aspx.cs:RetirarEnvioConformidad",        new[] { "PuedeGestionarLiquidaciones()" } },
                { "Views/LiquidacionesPendientes.aspx.cs:ObtenerLiquidacionesPorConformar",
                    new[] { "SecurityHelper.TieneSesionActiva()", "AccesoLiquidacion.VeTodasLasOrdenes(SecurityHelper.ObtenerRol())" } },
                { "Views/LiquidacionesPendientes.aspx.cs:CorregirSalidaPendiente",        new[] { "PuedeGestionarLiquidaciones()" } },
                { "Views/LiquidacionesPendientes.aspx.cs:ConsultarHoraLlegadaGps",        new[] { "PuedeGestionarLiquidaciones()" } },
                { "Views/LiquidacionesPendientes.aspx.cs:RegistrarFirmaConductor",
                    new[] { "contexto.IdUsuario==0", "SecurityHelper.EsConductor()" } },
                { "Views/LiquidacionesPendientes.aspx.cs:EnviarAConformidad",             new[] { "PuedeGestionarLiquidaciones()" } },
                { "Views/LiquidacionesPendientes.aspx.cs:ObtenerMovimientosParaRevision", new[] { "PuedeGestionarLiquidaciones()" } },
                { "Views/LiquidacionesPendientes.aspx.cs:ObtenerUrlPdfOrdenViaje",
                    new[] { "idUsuario==0", "PuedeConsultarOrden(idOrdenViaje)" } },

                { "Views/RegistroChoferes.aspx.cs:BuscarPorDNI",
                    new[] { "SecurityHelper.TieneSesionActiva()", "RolesHelper.TienePermiso(\"REGISTRO_CONDUCTORES\")" } },
                { "Views/RegistroClientes.aspx.cs:BuscarPorRuc",
                    new[] { "SecurityHelper.TieneSesionActiva()", "RolesHelper.TienePermiso(\"REGISTRO\")" } },
                // Mismo rol que la guarda de la página (SecurityHelper.ExigirRolAdminSistema, E5-8).
                { "Views/Auditoria.aspx.cs:ObtenerDetalle",
                    new[] { "SecurityHelper.TieneSesionActiva()", "SecurityHelper.EsAdminSistema()" } },
                { "Views/ReportesOrdenesViaje.aspx.cs:ObtenerDetalleOrden",
                    new[] { "SecurityHelper.TieneSesionActiva()", "SecurityHelper.EsAdmin()" } },
            };

        /// <summary>Páginas sin master que no llaman a AgregarHeadersSeguridad, y por qué.</summary>
        private static readonly Dictionary<string, string> SinNoStore =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Views/Error.aspx",                 "página de error sin datos; debe mostrarse aunque falle todo lo demás" },
                { "Views/DescargarPdfOrdenViaje.aspx","descarga del PDF con su propia caché (Cache-Control: private)" },
            };

        /// <summary>
        /// Además de los inicializadores de <see cref="ComprobacionesConocidas"/>, las únicas
        /// declaraciones que un WebMethod puede tener antes de su última comprobación (sin espacios;
        /// no llaman a servicios ni a la BD). Lista cerrada: agregar una es una decisión a propósito.
        /// </summary>
        private static readonly Regex PreparacionPermitida = new Regex(
            @"^((System\.Web\.)?HttpContext\.Current(\.Session)?" +   // var ctx = HttpContext.Current; var session = ...Session
            @"|SesionHelper\.ObtenerNombre\(ctx\.Session\)" +         // nombre del firmante (RegistrarFirmaConductor)
            @"|newList<\w+>\(\))$");                                  // la lista vacía que devuelve sin sesión

        // ── Pruebas ────────────────────────────────────────────────────────

        [Fact]
        public void PuntosDeEntrada_TodosTienenGuardaDecidida()
        {
            string raiz = RaizWeb();
            var actuales = PuntosDeEntrada(raiz);
            var decididos = GuardaEsperada.Keys.Concat(Publicas.Keys).Concat(Manuales.Keys).ToList();

            var repetidos = decididos.GroupBy(k => k, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(repetidos.Count == 0, "Punto de entrada en más de una tabla de GuardasAccesoTests: " + string.Join(", ", repetidos));

            var nuevos = actuales.Except(decididos, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
            Assert.True(nuevos.Count == 0,
                "Punto de entrada sin guarda decidida (agrégalo a GuardaEsperada con su SecurityHelper.Exigir* o " +
                "RolesHelper.ValidarAccesoSeccion como primera sentencia de Page_Load, o a Publicas/Manuales con el motivo):" +
                Environment.NewLine + string.Join(Environment.NewLine, nuevos));

            var sobran = decididos.Except(actuales, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
            Assert.True(sobran.Count == 0,
                "Entradas de GuardasAccesoTests que ya no existen (quítalas):" + Environment.NewLine + string.Join(Environment.NewLine, sobran));
        }

        [Fact]
        public void Paginas_PrimeraSentenciaDePageLoadEsSuGuarda()
        {
            string raiz = RaizWeb();
            var fallas = new List<string>();

            foreach (var par in GuardaEsperada.OrderBy(p => p.Key))
            {
                if (!GuardaEstandar.IsMatch(par.Value))
                {
                    fallas.Add($"{par.Key}: la guarda de la tabla '{par.Value}' no es de la lista cerrada (SecurityHelper.Exigir*, RolesHelper.ValidarAccesoSeccion(\"SECCION\")).");
                    continue;
                }

                string archivo = par.Key + ".cs";
                string codigo = LeerSinComentarios(raiz, archivo);
                if (codigo == null) { fallas.Add($"{archivo}: no existe el code-behind."); continue; }

                string cuerpo = CuerpoDeMetodo(codigo, "Page_Load");
                if (cuerpo == null) { fallas.Add($"{archivo}: no tiene Page_Load; se esperaba que empezara por {par.Value};."); continue; }

                string primera = PrimeraSentencia(cuerpo);
                if (Normalizar(primera) != Normalizar(par.Value))
                    fallas.Add($"{archivo} Page_Load: la primera sentencia es '{Recortar(primera)}'; se esperaba '{par.Value};' " +
                               "(fuera de cualquier try, antes de todo lo demás).");
            }

            Assert.True(fallas.Count == 0, "Guardas de página:" + Environment.NewLine + string.Join(Environment.NewLine, fallas));
        }

        // ── Acceso real por página (E4-3) ──────────────────────────────────
        // Quién entra a cada página según su guarda (GuardaEsperada), evaluada con la matriz de
        // RolesHelper.TienePermiso para las de sección. Tabla literal: dar o quitar una página a un rol
        // (en la guarda o en la matriz) debe cambiarla a propósito. U-081, opción recomendada
        // (2026-11-22): SUPERVISOR no entra a FacturasFlete, y la administración no tiene
        // DashboardConductor (como hoy y como el menú). RegistroProductos, RegistroRutas y CargarExcel
        // se retiraron en E5-19a (U-094): productos y rutas se gestionan en CatalogoCliente. E5-19b (U-090,
        // 2026-11-24): el rol SUPERVISOR se retiró; las páginas que compartía con la administración quedan
        // solo para ella, y SUPERVISOR pasa a RolesRetirados (no entra a ninguna página con guarda de rol).

        internal const string A = "ADMIN", C = "CONDUCTOR", K = "CONTABILIDAD", Y = "SISTEMA";
        private static readonly string[] Todos = { A, C, K, Y };

        internal static readonly Dictionary<string, string[]> AccesoEsperado =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Views/Auditoria.aspx",                         new[] { Y } },
                // Solo la sesión: qué tema ve cada rol lo decide AyudaCatalogo (AyudaTests).
                { "Views/Ayuda.aspx",                             Todos },
                { "Views/AyudaImagen.aspx",                       Todos },
                { "Views/BandejaDocumentos.aspx",                 new[] { A } },
                { "Views/CatalogoCliente.aspx",                   new[] { A } },
                // Solo la sesión: el permiso por orden y rol se decide después (ver GuardaEsperada).
                { "Views/ConformarLiquidacion.aspx",              Todos },
                { "Views/DashboardAdminSistema.aspx",             new[] { Y } },
                { "Views/DashboardConductor.aspx",                new[] { C } },
                { "Views/DescargarDocumento.aspx",                Todos },
                { "Views/DescargarPdfOrdenViaje.aspx",            Todos },
                { "Views/DestinoDescarga.aspx",                   new[] { A } },
                { "Views/DetalleOrdenViaje.aspx",                 Todos },
                { "Views/Exportacion/DashboardExportacion.aspx",  new[] { A } },
                { "Views/Exportacion/ImportarSeguimiento.aspx",   new[] { A } },
                { "Views/Exportacion/RegistroSeguimiento.aspx",   new[] { A } },
                { "Views/Exportacion/SeguimientoViaje.aspx",      new[] { A } },
                { "Views/FacturasFlete.aspx",                     new[] { A } },
                { "Views/FirmarLiquidacion.aspx",                 new[] { C } },
                { "Views/GestionUsuarios.aspx",                   new[] { Y } },
                { "Views/Inicio.aspx",                            new[] { A } },
                { "Views/LiquidacionesAprobadasContabilidad.aspx",new[] { A, K } },
                { "Views/LiquidacionesPendientes.aspx",           new[] { A } },
                { "Views/ListaDespachos.aspx",                    new[] { A } },
                // Solo la sesión: el conductor vuelve a su inicio después (RolesHelper.ExigeSegundoFactor, E4-14).
                { "Views/MiSeguridad.aspx",                       Todos },
                { "Views/MovimientosViaje.aspx",                  new[] { A } },
                { "Views/RegistroChoferes.aspx",                  new[] { A } },
                { "Views/RegistroClientes.aspx",                  new[] { A } },
                { "Views/RegistroDespacho.aspx",                  new[] { A } },
                { "Views/RegistroPeajes.aspx",                    new[] { A } },
                { "Views/RegistroPlantas.aspx",                   new[] { A } },
                { "Views/RegistroSemiremolques.aspx",             new[] { A } },
                { "Views/RegistroTractos.aspx",                   new[] { A } },
                { "Views/ReporteCargas.aspx",                     new[] { A } },
                { "Views/ReporteGps.aspx",                        new[] { A } },
                { "Views/ReportesOrdenesViaje.aspx",              new[] { A } },
                { "Views/SaldosConductores.aspx",                 new[] { A } },
            };

        /// <summary>Valores reales de Usuarios.rol de cada grupo de la tabla (con sus alias).</summary>
        internal static readonly Dictionary<string, string[]> RolesDelGrupo = new Dictionary<string, string[]>
        {
            { A, new[] { "ADMIN", "ADMINISTRADOR", "ADMINISTRADOR DE TRANSPORTE" } },
            { C, new[] { "CONDUCTOR", "CHOFER" } },
            { K, new[] { "CONTABILIDAD" } },
            { Y, new[] { "ADMINISTRADOR DE SISTEMA" } },
        };

        // Retirados (2026-11): ninguna página con guarda de rol los deja pasar.
        internal static readonly string[] RolesRetirados = { "ADMINISTRADOR DE GRIFO", "ADMINISTRADOR DE MAQUINARIA", "OPERADOR", "SUPERVISOR" };

        /// <summary>
        /// Lo que deja pasar la guarda con sesión del rol indicado (en mayúsculas). Repite las reglas de
        /// SecurityHelper.Exigir* (que leen la sesión) y, para las de sección, consulta la matriz misma.
        /// </summary>
        private static bool GuardaDejaPasar(string guarda, string rol)
        {
            Match seccion = Regex.Match(guarda, @"^RolesHelper\.ValidarAccesoSeccion\(""([A-Z_]+)""\)$");
            if (seccion.Success) return WebSGV.Helpers.RolesHelper.TienePermiso(seccion.Groups[1].Value, rol);
            switch (guarda)
            {
                case "SecurityHelper.ExigirSesion()":              return true;
                case "SecurityHelper.ExigirRolAdmin()":            return WebSGV.Helpers.RolesHelper.EsRolAdmin(rol);
                case "SecurityHelper.ExigirRolAdminSistema()":     return rol == "ADMINISTRADOR DE SISTEMA";
                case "SecurityHelper.ExigirRolConductor()":        return WebSGV.Helpers.RolesHelper.EsRolConductor(rol);
                default: throw new InvalidOperationException("Guarda sin regla en GuardaDejaPasar: " + guarda);
            }
        }

        [Fact]
        public void Paginas_GuardaYMatrizDanElAccesoEsperado()
        {
            var sinTabla = GuardaEsperada.Keys.Except(AccesoEsperado.Keys, StringComparer.OrdinalIgnoreCase)
                .Concat(AccesoEsperado.Keys.Except(GuardaEsperada.Keys, StringComparer.OrdinalIgnoreCase)).ToList();
            Assert.True(sinTabla.Count == 0, "GuardaEsperada y AccesoEsperado deben tener las mismas páginas: " + string.Join(", ", sinTabla));

            var fallas = new List<string>();
            foreach (var par in GuardaEsperada.OrderBy(p => p.Key))
            {
                string[] grupos = AccesoEsperado[par.Key];
                foreach (var g in RolesDelGrupo)
                    foreach (string rol in g.Value)
                    {
                        bool esperado = grupos.Contains(g.Key);
                        bool real = GuardaDejaPasar(par.Value, rol);
                        if (real != esperado)
                            fallas.Add($"{par.Key} ({par.Value}): {rol} {(real ? "entra" : "no entra")}; la tabla dice que {(esperado ? "sí" : "no")}.");
                    }
                foreach (string rol in RolesRetirados)
                    if (par.Value != "SecurityHelper.ExigirSesion()" && GuardaDejaPasar(par.Value, rol))
                        fallas.Add($"{par.Key}: el rol retirado {rol} entra.");
            }
            Assert.True(fallas.Count == 0, "Acceso por página:" + Environment.NewLine + string.Join(Environment.NewLine, fallas));
        }

        /// <summary>
        /// Toda sección de la matriz la consulta algún punto de entrada (guarda de página, WebMethod o
        /// guarda manual): una sección sin página es un permiso que no protege nada (M-064).
        /// </summary>
        [Fact]
        public void Matriz_CadaSeccionLaConsultaAlgunPuntoDeEntrada()
        {
            string raiz = RaizWeb();
            var patron = new Regex(@"(?:ValidarAccesoSeccion|TienePermiso)\(\s*""([A-Z_]+)""");
            var usadas = new HashSet<string>(
                GuardaEsperada.Values.Concat(ComprobacionesWebMethod.Values.SelectMany(v => v))
                    .Concat(Manuales.Keys.Select(k => LeerSinComentarios(raiz, k + ".cs") ?? ""))
                    .SelectMany(t => patron.Matches(t).Cast<Match>().Select(m => m.Groups[1].Value)),
                StringComparer.OrdinalIgnoreCase);

            var sinUso = WebSGV.Helpers.RolesHelper.Secciones.Where(s => !usadas.Contains(s)).OrderBy(s => s).ToList();
            Assert.True(sinUso.Count == 0, "Secciones de RolesHelper que ninguna página consulta (quítalas o úsalas): " + string.Join(", ", sinUso));
        }

        [Fact]
        public void Manuales_ConservanSuComprobacion()
        {
            string raiz = RaizWeb();
            var fallas = new List<string>();
            foreach (var par in Manuales)
            {
                string archivo = par.Key + ".cs";
                string codigo = LeerSinComentarios(raiz, archivo);
                string cuerpo = codigo == null ? null : CuerpoDeMetodo(codigo, par.Key.EndsWith(".ashx", StringComparison.OrdinalIgnoreCase) ? "ProcessRequest" : "Page_Load");
                if (cuerpo == null) { fallas.Add($"{archivo}: no se encontró el punto de entrada."); continue; }
                foreach (string m in par.Value.Marcadores)
                    if (!Normalizar(cuerpo).Contains(Normalizar(m)))
                        fallas.Add($"{archivo}: la guarda manual ({par.Value.Motivo}) ya no contiene '{m}'.");
            }
            Assert.True(fallas.Count == 0, "Guardas manuales:" + Environment.NewLine + string.Join(Environment.NewLine, fallas));
        }

        [Fact]
        public void WebMethods_TodosListadosConSesionYComprobacionAlInicio()
        {
            string raiz = RaizWeb();
            var fallas = new List<string>();
            var encontrados = new List<string>();

            foreach (var t in ComprobacionesWebMethod)
                foreach (string c in t.Value)
                    if (!ComprobacionesConocidas.Keys.Any(k => c.StartsWith(k, StringComparison.Ordinal)))
                        fallas.Add($"{t.Key}: la comprobación '{c}' no está en la lista cerrada ComprobacionesConocidas.");

            foreach (string ruta in ArchivosCs(raiz))
            {
                string relativo = Relativo(raiz, ruta);
                string codigo = SinComentarios(File.ReadAllText(ruta));
                foreach (Match m in Regex.Matches(codigo, @"\[\s*(System\.Web\.Services\.)?WebMethod\b"))
                {
                    int finAtributo = Cierre(codigo, codigo.IndexOf('[', m.Index), '[', ']');
                    string atributo = codigo.Substring(m.Index, finAtributo - m.Index + 1);
                    Match firma = new Regex(@"\G(\s*\[[^\]]*\])*\s*public\s+static\s+[^(=;{]+?\s(\w+)\s*\(").Match(codigo, finAtributo + 1);
                    if (!firma.Success) { fallas.Add($"{relativo}: [WebMethod] en la posición {m.Index} sin método public static reconocible."); continue; }

                    string nombre = firma.Groups[2].Value;
                    string clave = relativo + ":" + nombre;
                    encontrados.Add(clave);

                    if (!Regex.IsMatch(atributo, @"EnableSession\s*=\s*true"))
                        fallas.Add($"{clave}: falta EnableSession = true en [WebMethod] (sin él no ve la sesión).");

                    if (!ComprobacionesWebMethod.TryGetValue(clave, out string[] esperadas))
                    {
                        fallas.Add($"{clave}: WebMethod nuevo; agrégalo a ComprobacionesWebMethod con sus comprobaciones de sesión y rol.");
                        continue;
                    }

                    string cuerpo = CuerpoTrasParametros(codigo, firma.Index + firma.Length - 1);
                    if (cuerpo == null) { fallas.Add($"{clave}: no se pudo leer el cuerpo."); continue; }

                    foreach (string f in FallasDeComprobaciones(cuerpo, esperadas))
                        fallas.Add($"{clave}: {f}");
                }
            }

            foreach (string sobra in ComprobacionesWebMethod.Keys.Except(encontrados, StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
                fallas.Add($"{sobra}: está en ComprobacionesWebMethod pero ya no es un [WebMethod] (quítalo).");

            Assert.True(fallas.Count == 0, "WebMethods:" + Environment.NewLine + string.Join(Environment.NewLine, fallas));
        }

        [Fact]
        public void NoStore_MasterYPaginasSinMaster()
        {
            string raiz = RaizWeb();
            var fallas = new List<string>();

            string master = LeerSinComentarios(raiz, "Site.Master.cs");
            string init = master == null ? null : CuerpoDeMetodo(master, "Page_Init");
            if (init == null || Normalizar(PrimeraSentencia(init)) != Normalizar("SecurityHelper.AgregarHeadersSeguridad()"))
                fallas.Add("Site.Master.cs: Page_Init debe empezar por SecurityHelper.AgregarHeadersSeguridad(); (no-store en toda página con master).");

            var sinMaster = new List<string>();
            foreach (string pagina in PuntosDeEntrada(raiz))
            {
                string archivo = pagina + ".cs";
                if (pagina.EndsWith(".ashx", StringComparison.OrdinalIgnoreCase))
                {
                    string cuerpoAshx = CuerpoDeMetodo(LeerSinComentarios(raiz, archivo) ?? "", "ProcessRequest");
                    if (cuerpoAshx == null || !Normalizar(cuerpoAshx).Contains("Cache.SetNoStore()"))
                        fallas.Add($"{archivo}: ProcessRequest debe llamar a Response.Cache.SetNoStore().");
                    continue;
                }
                if (!pagina.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)) continue;

                string directiva = Regex.Match(File.ReadAllText(Path.Combine(raiz, pagina)), @"<%@\s*Page\b[^>]*%>", RegexOptions.IgnoreCase).Value;
                if (Regex.IsMatch(directiva, @"\bMasterPageFile\s*=", RegexOptions.IgnoreCase)) continue;

                sinMaster.Add(pagina);
                if (SinNoStore.ContainsKey(pagina)) continue;
                string cuerpo = CuerpoDeMetodo(LeerSinComentarios(raiz, archivo) ?? "", "Page_Load");
                if (cuerpo == null || !Regex.IsMatch(cuerpo, @"\bAgregarHeadersSeguridad\s*\(\s*\)"))
                    fallas.Add($"{archivo}: página sin master; su Page_Load debe llamar a SecurityHelper.AgregarHeadersSeguridad() (o va a SinNoStore con el motivo).");
            }

            foreach (string sobra in SinNoStore.Keys.Except(sinMaster, StringComparer.OrdinalIgnoreCase))
                fallas.Add($"{sobra}: está en SinNoStore pero no es una página sin master (quítala).");

            Assert.True(fallas.Count == 0, "Sin caché (no-store):" + Environment.NewLine + string.Join(Environment.NewLine, fallas));
        }

        /// <summary>El lector de C# de esta prueba: comentarios, literales y llaves.</summary>
        [Fact]
        public void LectorDeFuente_ComentariosLiteralesYLlaves()
        {
            Assert.Equal("a  b", SinComentarios("a /* x */ b"));
            Assert.Equal("x = \"//no\"; ", SinComentarios("x = \"//no\"; // si"));
            Assert.Equal("x = @\"a\"\"//\"; ", SinComentarios("x = @\"a\"\"//\"; // si"));
            Assert.Equal("c = '\\''; ", SinComentarios("c = '\\''; // si"));

            string codigo = "void Page_Load(object s, EventArgs e) { try { SecurityHelper.ExigirSesion(); } catch { } string t = \"}\"; }";
            string cuerpo = CuerpoDeMetodo(codigo, "Page_Load");
            Assert.EndsWith("string t = \"}\";", cuerpo.Trim());
            Assert.Equal("try{SecurityHelper.ExigirSesion()", Normalizar(PrimeraSentencia(cuerpo)));

            Assert.Equal(new[] { "if (!a()) { LogSGV.X(\"a;b\"); return null; }", "var d = S.X(1, \";\");", "catch { }" },
                Sentencias("try { if (!a()) { LogSGV.X(\"a;b\"); return null; } var d = S.X(1, \";\"); } catch { }"));
            Assert.Equal(new[] { "!a()", "(b&&c)", "x<=0" }, Terminos(" (!a() || (b && c) || x <= 0) "));
            Assert.Equal(new[] { "a&&!b" }, Terminos("a && !b"));
        }

        private static readonly string[] SesionYConductor = { "SecurityHelper.TieneSesionActiva()", "RolesHelper.EsConductor()" };

        /// <summary>Casos en memoria de la regla de los WebMethod: lo que pasa y lo que no.</summary>
        [Theory]
        [InlineData("if (!SecurityHelper.TieneSesionActiva() || !RolesHelper.EsConductor()) return null; var d = MiService.X();")]
        [InlineData("try { if (!SecurityHelper.TieneSesionActiva()) { LogSGV.Advertencia(\"x\"); return null; } if (id <= 0) return null; if (!RolesHelper.EsConductor()) throw new Exception(); MiService.X(); } catch { }")]
        [InlineData("var lista = new List<Item>(); if (!SecurityHelper.TieneSesionActiva() || !RolesHelper.EsConductor() || id < 0) return lista;")]
        public void ReglaWebMethod_Aceptada(string cuerpo)
        {
            Assert.Empty(FallasDeComprobaciones(cuerpo, SesionYConductor));
        }

        [Theory]
        // Polaridad: sin '!' la rama sale justo cuando SÍ hay sesión.
        [InlineData("if (SecurityHelper.TieneSesionActiva()) return null; if (!RolesHelper.EsConductor()) return null;")]
        // Un && de nivel superior: sin sesión no sale si x es falso.
        [InlineData("if (x && !SecurityHelper.TieneSesionActiva()) return null; if (!RolesHelper.EsConductor()) return null;")]
        [InlineData("if ((x || !SecurityHelper.TieneSesionActiva()) && y) return null; if (!RolesHelper.EsConductor()) return null;")]
        // Trabajo antes de la última comprobación.
        [InlineData("var d = MiService.X(); if (!SecurityHelper.TieneSesionActiva() || !RolesHelper.EsConductor()) return null;")]
        [InlineData("if (!SecurityHelper.TieneSesionActiva()) return null; Hacer(); if (!RolesHelper.EsConductor()) return null;")]
        // Un if previo que llama a algo fuera de la lista cerrada.
        [InlineData("if (!MiService.Existe(id)) return null; if (!SecurityHelper.TieneSesionActiva() || !RolesHelper.EsConductor()) return null;")]
        // La rama no sale.
        [InlineData("if (!SecurityHelper.TieneSesionActiva() || !RolesHelper.EsConductor()) x = 1;")]
        // Falta una de las dos.
        [InlineData("if (!SecurityHelper.TieneSesionActiva()) return null; var d = MiService.X();")]
        public void ReglaWebMethod_Rechazada(string cuerpo)
        {
            Assert.NotEmpty(FallasDeComprobaciones(cuerpo, SesionYConductor));
        }

        [Theory]
        [InlineData("var ctx = HttpContext.Current; int idUsuario = SesionHelper.ObtenerUsuarioId(ctx.Session); if (idUsuario == 0) return null;", true)]
        [InlineData("int idUsuario = 5; if (idUsuario == 0) return null;", false)]            // inicializador no permitido
        [InlineData("if (idUsuario == 0) return null;", false)]                               // variable sin declarar aquí
        [InlineData("int idUsuario = SesionHelper.ObtenerUsuarioId(s); if (idUsuario != 0) return null;", false)] // polaridad
        public void ReglaWebMethod_ComprobacionConVariable(string cuerpo, bool aceptada)
        {
            Assert.Equal(aceptada, FallasDeComprobaciones(cuerpo, new[] { "idUsuario==0" }).Count == 0);
        }

        // ── Lectura del fuente ─────────────────────────────────────────────

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static readonly string[] CarpetasExcluidas = { "obj", "bin", "packages", "Database" };

        private static bool Excluido(string raiz, string archivo)
        {
            string primera = Relativo(raiz, archivo).Split('/')[0];
            return CarpetasExcluidas.Contains(primera, StringComparer.OrdinalIgnoreCase);
        }

        private static string Relativo(string raiz, string archivo) =>
            archivo.Substring(raiz.Length).TrimStart('\\', '/').Replace('\\', '/');

        private static List<string> PuntosDeEntrada(string raiz) =>
            new[] { "*.aspx", "*.ashx", "*.asmx", "*.svc" }
                .SelectMany(ext => Directory.GetFiles(raiz, ext, SearchOption.AllDirectories))
                .Where(f => !Excluido(raiz, f))
                .Select(f => Relativo(raiz, f))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static IEnumerable<string> ArchivosCs(string raiz) =>
            Directory.GetFiles(raiz, "*.cs", SearchOption.AllDirectories).Where(f => !Excluido(raiz, f));

        private static string LeerSinComentarios(string raiz, string relativo)
        {
            string ruta = Path.Combine(raiz, relativo.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(ruta) ? SinComentarios(File.ReadAllText(ruta)) : null;
        }

        /// <summary>Sin espacios: para comparar sentencias sin depender del formato.</summary>
        private static string Normalizar(string s) => Regex.Replace(s ?? "", @"\s+", "");

        private static string Recortar(string s)
        {
            s = Regex.Replace(s ?? "", @"\s+", " ").Trim();
            return s.Length > 120 ? s.Substring(0, 120) + "..." : s;
        }

        /// <summary>
        /// Si en <paramref name="i"/> empieza un literal (texto, verbatim, interpolado o carácter),
        /// devuelve la posición siguiente a su cierre; si no, -1.
        /// </summary>
        private static int FinDeLiteral(string c, int i)
        {
            int j = i;
            bool verbatim = false;
            while (j < c.Length && (c[j] == '$' || c[j] == '@'))
            {
                if (c[j] == '@') verbatim = true;
                j++;
            }
            if (j >= c.Length) return -1;
            if (c[j] == '\'' && j == i)
            {
                for (int k = j + 1; k < c.Length; k++)
                {
                    if (c[k] == '\\') { k++; continue; }
                    if (c[k] == '\'') return k + 1;
                    if (c[k] == '\n') return k;
                }
                return c.Length;
            }
            if (c[j] != '"') return -1;
            if (j > i && i > 0 && (char.IsLetterOrDigit(c[i - 1]) || c[i - 1] == '_')) return -1;
            for (int k = j + 1; k < c.Length; k++)
            {
                if (verbatim)
                {
                    if (c[k] == '"')
                    {
                        if (k + 1 < c.Length && c[k + 1] == '"') { k++; continue; }
                        return k + 1;
                    }
                }
                else
                {
                    if (c[k] == '\\') { k++; continue; }
                    if (c[k] == '"') return k + 1;
                    if (c[k] == '\n') return k;
                }
            }
            return c.Length;
        }

        /// <summary>Quita comentarios // y /* */ respetando los literales.</summary>
        private static string SinComentarios(string c)
        {
            var sb = new StringBuilder(c.Length);
            for (int i = 0; i < c.Length;)
            {
                int fin = FinDeLiteral(c, i);
                if (fin > i) { sb.Append(c, i, fin - i); i = fin; continue; }
                if (c[i] == '/' && i + 1 < c.Length && c[i + 1] == '/')
                {
                    while (i < c.Length && c[i] != '\n') i++;
                    continue;
                }
                if (c[i] == '/' && i + 1 < c.Length && c[i + 1] == '*')
                {
                    int cierre = c.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = cierre < 0 ? c.Length : cierre + 2;
                    continue;
                }
                sb.Append(c[i]);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>Posición del cierre que corresponde a la apertura en <paramref name="abre"/>.</summary>
        private static int Cierre(string c, int abre, char apertura, char cierre)
        {
            int nivel = 0;
            for (int i = abre; i < c.Length;)
            {
                int fin = FinDeLiteral(c, i);
                if (fin > i) { i = fin; continue; }
                if (c[i] == apertura) nivel++;
                else if (c[i] == cierre && --nivel == 0) return i;
                i++;
            }
            return c.Length - 1;
        }

        /// <summary>Cuerpo (sin las llaves) del primer método con ese nombre que tenga cuerpo.</summary>
        private static string CuerpoDeMetodo(string codigo, string nombre)
        {
            foreach (Match m in Regex.Matches(codigo, @"\b" + Regex.Escape(nombre) + @"\s*\("))
            {
                string antes = codigo.Substring(0, m.Index).TrimEnd();
                if (!Regex.IsMatch(antes, @"\b(void|[\w<>\[\],]+)$") || Regex.IsMatch(antes, @"(\.|\bnew|=|return)$")) continue;
                string cuerpo = CuerpoTrasParametros(codigo, m.Index + m.Length - 1);
                if (cuerpo != null) return cuerpo;
            }
            return null;
        }

        /// <summary>
        /// Dado el '(' de los parámetros, el cuerpo del método: el bloque { } o, si es "=> X(...)" y X
        /// es un método del mismo archivo, el cuerpo de X.
        /// </summary>
        private static string CuerpoTrasParametros(string codigo, int parentesis)
        {
            int finParam = Cierre(codigo, parentesis, '(', ')');
            int i = finParam + 1;
            while (i < codigo.Length && char.IsWhiteSpace(codigo[i])) i++;
            if (i >= codigo.Length) return null;
            if (codigo[i] == '{')
            {
                int fin = Cierre(codigo, i, '{', '}');
                return codigo.Substring(i + 1, fin - i - 1);
            }
            if (codigo[i] == '=' && i + 1 < codigo.Length && codigo[i + 1] == '>')
            {
                Match llamada = new Regex(@"\G\s*(\w+)\s*\(").Match(codigo, i + 2);
                if (llamada.Success)
                {
                    string delegado = CuerpoDeMetodoDeclarado(codigo, llamada.Groups[1].Value);
                    if (delegado != null) return delegado;
                }
                int puntoComa = codigo.IndexOf(';', i);
                return codigo.Substring(i + 2, puntoComa - i - 2);
            }
            return null; // declaración sin cuerpo (abstracta, partial, interfaz)
        }

        private static string CuerpoDeMetodoDeclarado(string codigo, string nombre)
        {
            Match decl = Regex.Match(codigo, @"\b(private|internal|protected|public)\s+static\s+[^(=;{]+?\s" + Regex.Escape(nombre) + @"\s*\(");
            if (!decl.Success) return null;
            int abre = decl.Index + decl.Length - 1;
            int finParam = Cierre(codigo, abre, '(', ')');
            int i = finParam + 1;
            while (i < codigo.Length && char.IsWhiteSpace(codigo[i])) i++;
            if (i >= codigo.Length || codigo[i] != '{') return null;
            return codigo.Substring(i + 1, Cierre(codigo, i, '{', '}') - i - 1);
        }

        /// <summary>Texto hasta el primer ';' de nivel superior (sin el ';').</summary>
        private static string PrimeraSentencia(string cuerpo)
        {
            for (int i = 0; i < cuerpo.Length;)
            {
                int fin = FinDeLiteral(cuerpo, i);
                if (fin > i) { i = fin; continue; }
                if (cuerpo[i] == ';') return cuerpo.Substring(0, i).Trim();
                i++;
            }
            return cuerpo.Trim();
        }

        /// <summary>
        /// Regla de un WebMethod: hasta su última comprobación esperada, cada sentencia es
        /// (a) un <c>if (t1 || t2 || ...) return/throw</c> cuyos términos son comprobaciones conocidas
        /// en su forma negada (<c>!X(...)</c>, o la forma de la tabla para las de variable, como
        /// <c>idUsuario == 0</c>) o comparaciones sin llamadas (<c>id &lt;= 0</c>); o (b) una declaración
        /// con un inicializador de la lista cerrada. Un <c>&amp;&amp;</c> de nivel superior no vale.
        /// Devuelve las fallas (vacía si cumple).
        /// </summary>
        private static List<string> FallasDeComprobaciones(string cuerpo, string[] esperadas)
        {
            var fallas = new List<string>();
            var pendientes = esperadas.Select(Normalizar).ToList();
            var declaradas = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string sentencia in Sentencias(cuerpo))
            {
                if (pendientes.Count == 0) break;

                if (EsIfConSalida(sentencia, out string condicion))
                {
                    var terminos = Terminos(condicion);
                    foreach (string t in terminos)
                        if (!EsComprobacionNegada(t) && Regex.IsMatch(t, @"\w\("))
                            fallas.Add($"antes de sus comprobaciones tiene 'if ({Recortar(condicion)})' con el término '{t}', que no es una " +
                                       "comprobación conocida negada (!X(...)) ni una comparación sin llamadas (en un || de nivel superior).");

                    foreach (string c in pendientes.ToList())
                    {
                        if (!terminos.Any(t => FormaNegada(c) == t)) continue;
                        pendientes.Remove(c);
                        string inicializador = ComprobacionConocida(c).Value;
                        if (inicializador == null) continue;
                        string variable = Regex.Match(c, @"^\w+").Value;
                        if (!declaradas.TryGetValue(variable, out string init) || !init.StartsWith(Normalizar(inicializador), StringComparison.Ordinal))
                            fallas.Add($"la comprobación '{c}' requiere declarar antes '{variable} = {inicializador}...'.");
                    }
                    if (fallas.Count > 0) return fallas;
                    continue;
                }

                Match decl = Regex.Match(sentencia, @"^(?:var|[\w<>\[\],.?]+)\s+(\w+)\s*=\s*([\s\S]+?)\s*;$");
                if (decl.Success)
                {
                    string init = Normalizar(decl.Groups[2].Value);
                    bool conocido = ComprobacionesConocidas.Values.Any(v => v != null && init.StartsWith(Normalizar(v), StringComparison.Ordinal));
                    if (conocido || PreparacionPermitida.IsMatch(init))
                    {
                        declaradas[decl.Groups[1].Value] = init;
                        continue;
                    }
                }

                fallas.Add($"antes de comprobar '{string.Join("', '", pendientes)}' ejecuta '{Recortar(sentencia)}', que no es una " +
                           "comprobación (if ... return/throw) ni una preparación de la lista cerrada.");
                return fallas;
            }

            foreach (string c in pendientes)
                fallas.Add($"no empieza por un 'if (... || {FormaNegada(c)} || ...) return/throw' (forma negada, en un || de nivel superior).");
            return fallas;
        }

        /// <summary>La comprobación conocida de la que la de la tabla es un caso (por prefijo).</summary>
        private static KeyValuePair<string, string> ComprobacionConocida(string c) =>
            ComprobacionesConocidas.FirstOrDefault(k => c.StartsWith(k.Key, StringComparison.Ordinal));

        /// <summary>
        /// Cómo debe aparecer la comprobación en la condición para que la rama salga cuando NO pasa:
        /// las de variable tal cual (<c>idUsuario==0</c>), las llamadas negadas (<c>!X(...)</c>).
        /// </summary>
        private static string FormaNegada(string c)
        {
            c = Normalizar(c);
            return ComprobacionConocida(c).Value != null ? c : "!" + c;
        }

        /// <summary>El término es alguna comprobación conocida en su forma negada (completa).</summary>
        private static bool EsComprobacionNegada(string termino)
        {
            foreach (var k in ComprobacionesConocidas)
            {
                if (k.Value != null) { if (termino == k.Key) return true; continue; }
                string negada = "!" + k.Key;
                if (!termino.StartsWith(negada, StringComparison.Ordinal)) continue;
                if (termino == negada) return true;
                // Clave que termina en '(' (TienePermiso(, PuedeConsultarOrden(...): el paréntesis cierra al final.
                if (k.Key.EndsWith("(") && Cierre(termino, negada.Length - 1, '(', ')') == termino.Length - 1) return true;
            }
            return false;
        }

        /// <summary>Si la sentencia es un if cuya rama sale con return o throw, su condición.</summary>
        private static bool EsIfConSalida(string sentencia, out string condicion)
        {
            condicion = null;
            Match m = Regex.Match(sentencia, @"^if\s*\(");
            if (!m.Success) return false;
            int abre = m.Length - 1;
            int cierra = Cierre(sentencia, abre, '(', ')');
            if (cierra >= sentencia.Length - 1) return false;
            string rama = sentencia.Substring(cierra + 1).Trim();
            if (rama.StartsWith("{")) rama = rama.Substring(1).TrimStart();
            // La rama empieza por return/throw, o es un bloque con un return/throw como sentencia.
            if (!Regex.IsMatch(rama, @"^(return|throw)\b|;\s*(return|throw)\b")) return false;
            condicion = sentencia.Substring(abre + 1, cierra - abre - 1);
            return true;
        }

        /// <summary>
        /// Términos (sin espacios) del || de nivel superior de una condición; un paréntesis que
        /// envuelve toda la condición se quita.
        /// </summary>
        private static List<string> Terminos(string condicion)
        {
            string c = condicion.Trim();
            while (c.StartsWith("(") && Cierre(c, 0, '(', ')') == c.Length - 1)
                c = c.Substring(1, c.Length - 2).Trim();

            var terminos = new List<string>();
            int nivel = 0, inicio = 0;
            for (int i = 0; i < c.Length;)
            {
                int fin = FinDeLiteral(c, i);
                if (fin > i) { i = fin; continue; }
                char ch = c[i];
                if (ch == '(' || ch == '[' || ch == '{') nivel++;
                else if (ch == ')' || ch == ']' || ch == '}') nivel--;
                else if (nivel == 0 && ch == '|' && i + 1 < c.Length && c[i + 1] == '|')
                {
                    terminos.Add(Normalizar(c.Substring(inicio, i - inicio)));
                    inicio = i + 2;
                    i += 2;
                    continue;
                }
                i++;
            }
            terminos.Add(Normalizar(c.Substring(inicio)));
            return terminos;
        }

        /// <summary>
        /// Sentencias del cuerpo en orden, aplanando try y bloques: un if con su rama es una
        /// sentencia; el resto llega hasta su ';' de nivel superior.
        /// </summary>
        private static List<string> Sentencias(string cuerpo)
        {
            var sentencias = new List<string>();
            int i = 0;
            while (i < cuerpo.Length)
            {
                char ch = cuerpo[i];
                if (char.IsWhiteSpace(ch) || ch == '{' || ch == '}') { i++; continue; }
                if (Regex.IsMatch(cuerpo.Substring(i), @"^try\b")) { i += 3; continue; }

                int fin;
                Match si = new Regex(@"\Gif\s*\(").Match(cuerpo, i);
                if (si.Success)
                {
                    int cierra = Cierre(cuerpo, si.Index + si.Length - 1, '(', ')');
                    int j = cierra + 1;
                    while (j < cuerpo.Length && char.IsWhiteSpace(cuerpo[j])) j++;
                    fin = j < cuerpo.Length && cuerpo[j] == '{' ? Cierre(cuerpo, j, '{', '}') : FinDeSentencia(cuerpo, j);
                }
                else
                {
                    fin = FinDeSentencia(cuerpo, i);
                }
                sentencias.Add(cuerpo.Substring(i, Math.Min(fin, cuerpo.Length - 1) - i + 1).Trim());
                i = fin + 1;
            }
            return sentencias;
        }

        /// <summary>Posición del ';' que cierra la sentencia que empieza en <paramref name="i"/>.</summary>
        private static int FinDeSentencia(string c, int i)
        {
            int nivel = 0;
            for (; i < c.Length;)
            {
                int fin = FinDeLiteral(c, i);
                if (fin > i) { i = fin; continue; }
                char ch = c[i];
                if (ch == '(' || ch == '[' || ch == '{') nivel++;
                else if (ch == ')' || ch == ']' || ch == '}') nivel--;
                else if (ch == ';' && nivel == 0) return i;
                i++;
            }
            return c.Length - 1;
        }
    }
}
