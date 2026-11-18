namespace WebSGV.Services.Liquidaciones
{
    /// <summary>
    /// Valores de <c>OrdenViaje.estadoAprobacion</c>. Una liquidación "aprobada" es solo la que tiene la
    /// conformidad del conductor firmada (<see cref="Aprobado"/>, la pone sp_DC_RegistrarConformidadConductor):
    /// es el único criterio para Control de aprobadas, Contabilidad, Reportes y Saldos de conductores.
    /// </summary>
    public static class EstadosOrden
    {
        public const string Pendiente = "PENDIENTE";
        /// <summary>Rechazada por la administración (sp_RechazarLiquidacion): vuelve al conductor para corregirla.</summary>
        public const string Reabierto = "REABIERTO";
        /// <summary>Revisada por el administrador; falta la conformidad firmada del conductor.</summary>
        public const string PorConformar = "POR_CONFORMAR";
        /// <summary>Con la conformidad del conductor firmada: liquidación cerrada.</summary>
        public const string Aprobado = "APROBADO";
    }
}
