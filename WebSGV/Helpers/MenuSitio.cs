using System;
using System.Collections.Generic;
using System.Linq;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Una opción del menú de Site.Master: texto, página de destino (ruta virtual "~/Views/...") y quién
    /// la ve. El permiso es la sección de la matriz (<see cref="RolesHelper.TienePermiso(string, string)"/>)
    /// cuando la página se protege con esa sección, o la misma regla de rol que su guarda
    /// (<see cref="SecurityHelper"/>.Exigir*) cuando se protege por rol. Lo que se garantiza es que nadie ve
    /// una opción cuya guarda no pase; al revés hay una excepción: "Liquidaciones aprobadas" solo la ve
    /// contabilidad aunque el administrador también entra (como antes de E5-19c). Sin iconos (DN-28).
    /// </summary>
    public sealed class EntradaMenu
    {
        private readonly Func<string, bool> _permite;

        internal EntradaMenu(string texto, string url, string seccion, string condicion, Func<string, bool> permite,
                             bool separadorAntes = false, bool soloConMovimientos = false)
        {
            Texto = texto;
            Url = url;
            Seccion = seccion;
            Condicion = condicion;
            _permite = permite;
            SeparadorAntes = separadorAntes;
            SoloConMovimientos = soloConMovimientos;
        }

        /// <summary>Texto visible (se codifica al pintarlo).</summary>
        public string Texto { get; }

        /// <summary>Ruta virtual de la página ("~/Views/X.aspx").</summary>
        public string Url { get; }

        /// <summary>Sección de la matriz de RolesHelper, o null si la visibilidad es una regla de rol (<see cref="Condicion"/>).</summary>
        public string Seccion { get; }

        /// <summary>Nombre de la regla de rol cuando no hay sección (p. ej. "EsRolAdmin"), o null.</summary>
        public string Condicion { get; }

        /// <summary>Línea divisoria antes de la opción dentro de su desplegable.</summary>
        public bool SeparadorAntes { get; }

        /// <summary>Solo con el interruptor <c>MovimientosViaje.Habilitado</c> encendido.</summary>
        public bool SoloConMovimientos { get; }

        /// <summary>True si el rol (cualquier forma de mayúsculas o espacios) ve la opción.</summary>
        public bool PuedeVer(string rol)
        {
            string r = (rol ?? "").Trim().ToUpperInvariant();
            return Seccion != null ? RolesHelper.TienePermiso(Seccion, r) : _permite(r);
        }
    }

    /// <summary>
    /// Un menú de la barra: desplegable (con <see cref="Id"/> para Bootstrap) o enlace directo
    /// (<see cref="EsEnlaceDirecto"/>, una sola opción sin desplegable, como "Mi viaje").
    /// </summary>
    public sealed class GrupoMenu
    {
        internal GrupoMenu(string texto, string id, bool esEnlaceDirecto, IReadOnlyList<EntradaMenu> entradas)
        {
            Texto = texto;
            Id = id;
            EsEnlaceDirecto = esEnlaceDirecto;
            Entradas = entradas;
        }

        public string Texto { get; }

        /// <summary>id del enlace del desplegable (aria-labelledby del menú); null en un enlace directo.</summary>
        public string Id { get; }

        public bool EsEnlaceDirecto { get; }

        public IReadOnlyList<EntradaMenu> Entradas { get; }

        /// <summary>True si la opción de la posición indicada lleva la línea divisoria (nunca la primera).</summary>
        public bool LlevaSeparador(int indice) => indice > 0 && indice < Entradas.Count && Entradas[indice].SeparadorAntes;
    }

    /// <summary>
    /// Única fuente del menú de Site.Master (E5-19c, M-124): los menús y sus opciones en el orden en que
    /// se ven, y quién ve cada una. "Configuración" (Cambiar contraseña, Mi seguridad), el nombre del
    /// usuario y "Cerrar sesión" siguen en el marcado. Pura (no lee la sesión ni la configuración);
    /// fijada por <c>MenuSitioTests</c>, que además comprueba que quien ve una opción entra a su página.
    /// </summary>
    public static class MenuSitio
    {
        // Reglas de rol iguales a las guardas de las páginas que no usan sección (SecurityHelper.Exigir*).
        private const string CondAdmin = "EsRolAdmin";
        private const string CondConductor = "EsRolConductor";
        private const string CondAdminSistema = "EsRolAdminSistema";
        private const string CondContabilidad = "EsRolContabilidad";

        private static EntradaMenu PorRol(string texto, string url, string condicion, bool separadorAntes = false, bool soloConMovimientos = false)
        {
            Func<string, bool> permite;
            switch (condicion)
            {
                case CondAdmin:        permite = RolesHelper.EsRolAdmin; break;
                case CondConductor:    permite = RolesHelper.EsRolConductor; break;
                case CondAdminSistema: permite = r => r == RolesHelper.ROL_ADMIN_SISTEMA; break;
                // Solo su menú: la administración también entra a la página (LIQUIDACIONES_CONTABILIDAD),
                // pero llega desde Inicio, no desde el menú.
                case CondContabilidad: permite = r => r == RolesHelper.ROL_CONTABILIDAD; break;
                default: throw new ArgumentException("Condición de menú desconocida: " + condicion, nameof(condicion));
            }
            return new EntradaMenu(texto, url, null, condicion, permite, separadorAntes, soloConMovimientos);
        }

        private static EntradaMenu PorSeccion(string texto, string url, string seccion, bool separadorAntes = false) =>
            new EntradaMenu(texto, url, seccion, null, null, separadorAntes);

        private static GrupoMenu Desplegable(string texto, string id, params EntradaMenu[] entradas) =>
            new GrupoMenu(texto, id, false, entradas);

        private static GrupoMenu Directo(EntradaMenu entrada) =>
            new GrupoMenu(entrada.Texto, null, true, new[] { entrada });

        /// <summary>El menú completo, sin filtrar, en el orden de la barra.</summary>
        public static readonly IReadOnlyList<GrupoMenu> Completo = new[]
        {
            Desplegable("Despacho", "navbarDropdownDespacho",
                PorRol("Registro de despachos", "~/Views/RegistroDespacho.aspx", CondAdmin),
                PorRol("Bandeja de documentos", "~/Views/BandejaDocumentos.aspx", CondAdmin),
                PorRol("Lista de despachos", "~/Views/ListaDespachos.aspx", CondAdmin),
                PorRol("Destino de descarga", "~/Views/DestinoDescarga.aspx", CondAdmin)),

            Desplegable("Seguimiento", "navbarDropdownExportacion",
                PorSeccion("Tablero de seguimiento", "~/Views/Exportacion/RegistroSeguimiento.aspx", "SEGUIMIENTO_EXPORTACION"),
                PorSeccion("Dashboard de exportación", "~/Views/Exportacion/DashboardExportacion.aspx", "SEGUIMIENTO_EXPORTACION")),

            Desplegable("Liquidaciones", "navbarDropdownOrdenViaje",
                PorSeccion("Liquidaciones pendientes", "~/Views/LiquidacionesPendientes.aspx", "ORDEN_VIAJE"),
                PorRol("Descuentos y reintegros (quincena)", "~/Views/SaldosConductores.aspx", CondAdmin),
                PorRol("Dinero y pagos del viaje", "~/Views/MovimientosViaje.aspx", CondAdmin, soloConMovimientos: true)),

            Desplegable("Reportes", "navbarDropdownReportes",
                PorRol("Reporte de cargas (pedidos, guías y carga)", "~/Views/ReporteCargas.aspx", CondAdmin),
                PorRol("Reporte de liquidaciones y viajes", "~/Views/ReportesOrdenesViaje.aspx", CondAdmin),
                PorSeccion("Facturas de flete (conciliación)", "~/Views/FacturasFlete.aspx", "FACTURAS_FLETE"),
                PorRol("Reporte GPS por placas", "~/Views/ReporteGps.aspx", CondAdmin, separadorAntes: true)),

            Desplegable("Catálogos", "navbarDropdownRegistro",
                PorSeccion("Conductores", "~/Views/RegistroChoferes.aspx", "REGISTRO_CONDUCTORES"),
                PorSeccion("Tractos", "~/Views/RegistroTractos.aspx", "REGISTRO"),
                PorSeccion("Semirremolques", "~/Views/RegistroSemiremolques.aspx", "REGISTRO"),
                PorSeccion("Clientes", "~/Views/RegistroClientes.aspx", "REGISTRO"),
                PorSeccion("Catálogo del cliente", "~/Views/CatalogoCliente.aspx", "REGISTRO"),
                PorSeccion("Plantas", "~/Views/RegistroPlantas.aspx", "REGISTRO", separadorAntes: true),
                PorSeccion("Peajes", "~/Views/RegistroPeajes.aspx", "REGISTRO")),

            // La Auditoría va solo aquí: hasta E5-19c salía también en Configuración (M-164).
            Desplegable("Administración", "navbarDropdownAdminSistema",
                PorRol("Panel del sistema", "~/Views/DashboardAdminSistema.aspx", CondAdminSistema),
                PorRol("Gestión de usuarios", "~/Views/GestionUsuarios.aspx", CondAdminSistema),
                PorRol("Auditoría del sistema", "~/Views/Auditoria.aspx", CondAdminSistema, separadorAntes: true)),

            Directo(PorRol("Mi viaje", "~/Views/DashboardConductor.aspx", CondConductor)),

            Directo(PorRol("Liquidaciones aprobadas", "~/Views/LiquidacionesAprobadasContabilidad.aspx", CondContabilidad)),
        };

        /// <summary>
        /// Los menús que ve el rol, con solo las opciones que ve (un menú sin opciones no sale).
        /// <paramref name="movimientosHabilitado"/> es el interruptor <c>MovimientosViaje.Habilitado</c>.
        /// </summary>
        public static IReadOnlyList<GrupoMenu> Para(string rol, bool movimientosHabilitado)
        {
            var grupos = new List<GrupoMenu>();
            foreach (var g in Completo)
            {
                var visibles = g.Entradas
                    .Where(e => e.PuedeVer(rol) && (movimientosHabilitado || !e.SoloConMovimientos))
                    .ToList();
                if (visibles.Count > 0)
                    grupos.Add(new GrupoMenu(g.Texto, g.Id, g.EsEnlaceDirecto, visibles));
            }
            return grupos;
        }

        /// <summary>
        /// Ruta comparable: sin query ni fragmento, sin "~", con "/" inicial y en minúsculas
        /// ("~/Views/X.aspx?id=1" → "/views/x.aspx").
        /// </summary>
        public static string NormalizarRuta(string ruta)
        {
            string r = (ruta ?? "").Trim();
            int corte = r.IndexOfAny(new[] { '?', '#' });
            if (corte >= 0) r = r.Substring(0, corte);
            if (r.StartsWith("~", StringComparison.Ordinal)) r = r.Substring(1);
            r = r.Replace('\\', '/');
            if (!r.StartsWith("/", StringComparison.Ordinal)) r = "/" + r;
            return r.ToLowerInvariant();
        }

        /// <summary>True si la página de la URL es la actual (ruta virtual o relativa a la aplicación).</summary>
        public static bool EsActiva(string url, string rutaActual) =>
            !string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(rutaActual)
            && NormalizarRuta(url) == NormalizarRuta(rutaActual);

        /// <summary>True si la opción lleva a la página actual (sin query ni mayúsculas).</summary>
        public static bool EsActiva(EntradaMenu e, string rutaActual) => e != null && EsActiva(e.Url, rutaActual);

        /// <summary>True si alguna opción del menú lleva a la página actual.</summary>
        public static bool EsActivo(GrupoMenu g, string rutaActual) => g != null && g.Entradas.Any(e => EsActiva(e, rutaActual));
    }
}
