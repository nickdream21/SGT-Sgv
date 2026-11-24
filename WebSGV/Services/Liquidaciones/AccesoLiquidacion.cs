using System;
using WebSGV.Helpers;

namespace WebSGV.Services.Liquidaciones
{
    /// <summary>
    /// Regla única de quién puede ver el detalle y el PDF de una liquidación pedida por su id
    /// (PageMethods de LiquidacionesPendientes y DescargarPdfOrdenViaje):
    /// <list type="bullet">
    ///   <item>Administración (<see cref="RolesHelper.EsRolAdmin"/>): cualquier orden.</item>
    ///   <item>CONTABILIDAD: solo órdenes <see cref="EstadosOrden.Aprobado"/> (decisión D3, B6-6), lo mismo que lista su pantalla.</item>
    ///   <item>CONDUCTOR / CHOFER: solo las suyas.</item>
    ///   <item>Cualquier otro rol: ninguna.</item>
    /// </list>
    /// Una orden que no existe se trata igual que una no permitida: no se revela cuál de las dos es.
    /// </summary>
    public static class AccesoLiquidacion
    {
        /// <summary>Roles que ven cualquier liquidación sin mirar su estado ni su conductor.</summary>
        public static bool VeTodasLasOrdenes(string rol)
        {
            string r = Normalizar(rol);
            return RolesHelper.EsRolAdmin(r);
        }

        /// <summary>
        /// Regla pura (sin BD ni sesión). <paramref name="estadoAprobacion"/> null = la orden no existe.
        /// </summary>
        public static bool Permite(string rol, string estadoAprobacion, bool ordenEsDelUsuario)
        {
            string r = Normalizar(rol);
            if (VeTodasLasOrdenes(r))
                return true;
            if (r == RolesHelper.ROL_CONTABILIDAD)
                return string.Equals((estadoAprobacion ?? "").Trim(), EstadosOrden.Aprobado, StringComparison.OrdinalIgnoreCase);
            if (RolesHelper.EsRolConductor(r))
                return ordenEsDelUsuario;
            return false;
        }

        /// <summary>
        /// Aplica <see cref="Permite"/> a una orden de la BD. Solo consulta lo que el rol necesita:
        /// el estado para CONTABILIDAD y la pertenencia para el conductor.
        /// </summary>
        public static bool PuedeConsultar(int idOrdenViaje, int idUsuario, string rol)
        {
            if (idOrdenViaje <= 0)
                return false;

            string r = Normalizar(rol);
            if (VeTodasLasOrdenes(r))
                return true;

            if (r == RolesHelper.ROL_CONTABILIDAD)
                return Permite(r, ObtenerEstadoAprobacion(idOrdenViaje), false);

            if (RolesHelper.EsRolConductor(r))
                return idUsuario > 0 &&
                       Permite(r, null, LiquidacionesPendientesService.OrdenPerteneceAUsuarioConductor(idOrdenViaje, idUsuario));

            return false;
        }

        /// <summary>Estado de aprobación de la orden, o null si no existe.</summary>
        private static string ObtenerEstadoAprobacion(int idOrdenViaje)
        {
            object v = DbHelper.EjecutarEscalar(
                "SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @idOrdenViaje",
                DbHelper.Param("@idOrdenViaje", idOrdenViaje));
            return v == null || v == DBNull.Value ? null : v.ToString();
        }

        private static string Normalizar(string rol) => (rol ?? "").Trim().ToUpperInvariant();
    }
}
