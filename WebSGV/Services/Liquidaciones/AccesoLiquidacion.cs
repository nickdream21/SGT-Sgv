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
    /// También guarda los controles de pertenencia de las pantallas del conductor (detalle, firma de la
    /// declaración y conformidad).
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

        // =====================================================================
        // Pertenencia de la orden al conductor de la sesión (anti-IDOR, E6-15).
        // Cada pantalla del conductor tiene su predicado: no se juntan porque miran
        // cosas distintas (el conductor de la sesión o su usuario activo, y el estado).
        // =====================================================================

        /// <summary>
        /// La orden es del conductor (cualquier estado). Lo usa el detalle del conductor
        /// (<c>DetalleOrdenViaje</c>). Sin conductor (<paramref name="idConductor"/> &lt;= 0): false.
        /// </summary>
        public static bool EsDelConductor(int idOrdenViaje, int idConductor)
        {
            if (idConductor <= 0) return false;
            object n = DbHelper.EjecutarEscalar(
                "SELECT COUNT(1) FROM OrdenViaje WHERE idOrdenViaje = @id AND idConductor = @idConductor",
                DbHelper.Param("@id", idOrdenViaje),
                DbHelper.Param("@idConductor", idConductor));
            return n != null && n != DBNull.Value && Convert.ToInt32(n) > 0;
        }

        /// <summary>
        /// La orden es del conductor y espera la firma de su declaración: PENDIENTE y sin firma vigente
        /// (la misma condición que <c>sp_DC_RegistrarFirmaConductor</c>). Lo usa <c>FirmarLiquidacion</c>.
        /// </summary>
        public static bool EsperaDeclaracionDelConductor(int idOrdenViaje, int idConductor)
        {
            if (idConductor <= 0) return false;
            int count = Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                SELECT COUNT(1)
                FROM   OrdenViaje
                WHERE  idOrdenViaje     = @idOrdenViaje
                  AND  idConductor      = @idConductor
                  AND  estadoAprobacion = 'PENDIENTE'
                  AND  idFirmaConductor IS NULL
                  AND  idFirmaAdmin     IS NULL",
                DbHelper.Param("@idOrdenViaje", idOrdenViaje),
                DbHelper.Param("@idConductor", idConductor)));
            return count > 0;
        }

        /// <summary>
        /// La orden es del conductor asociado al usuario activo <paramref name="idUsuario"/> y espera su
        /// conformidad: POR_CONFORMAR con la firma de la revisión. Lo usa <c>ConformarLiquidacion</c>.
        /// </summary>
        public static bool EsperaConformidadDelUsuario(int idOrdenViaje, int idUsuario)
        {
            object n = DbHelper.EjecutarEscalar(@"
                SELECT COUNT(1)
                FROM OrdenViaje ov
                INNER JOIN Usuarios u ON u.idConductor = ov.idConductor AND u.activo = 1
                WHERE ov.idOrdenViaje = @id AND u.idUsuario = @idUsuario
                  AND ov.estadoAprobacion = 'POR_CONFORMAR' AND ov.idFirmaAdmin IS NOT NULL",
                DbHelper.Param("@id", idOrdenViaje),
                DbHelper.Param("@idUsuario", idUsuario));
            return n != null && n != DBNull.Value && Convert.ToInt32(n) > 0;
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
