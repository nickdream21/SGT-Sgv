namespace WebSGV.Services.Common
{
    /// <summary>Maestros sin servicio propio que se activan/desactivan desde su pantalla de registro.</summary>
    public enum MaestroSimple
    {
        /// <summary><c>RegistroClientes.aspx</c>.</summary>
        Cliente,
        /// <summary><c>RegistroPeajes.aspx</c>.</summary>
        EstacionPeaje,
        /// <summary><c>RegistroPlantas.aspx</c>.</summary>
        Planta
    }

    /// <summary>
    /// Activar/desactivar clientes, estaciones de peaje y plantas con estado destino explícito (bloque 5, B5-3).
    /// Tractos, semirremolques, conductores y catálogos por cliente lo hacen desde su propio servicio.
    /// </summary>
    public static class MaestrosService
    {
        private static readonly TablaActivable Cliente =
            new TablaActivable("Cliente", "idCliente", "activo", "nombre", "cliente", femenino: false);

        private static readonly TablaActivable EstacionPeaje =
            new TablaActivable("EstacionesPeaje", "idEstacion", "activo", "nombre", "estación de peaje", femenino: true);

        private static readonly TablaActivable Planta =
            new TablaActivable("Planta", "idPlanta", "activo", "nombre", "planta", femenino: true);

        /// <summary>Tabla y textos del maestro (para pruebas y auditoría).</summary>
        public static TablaActivable Tabla(MaestroSimple maestro)
        {
            switch (maestro)
            {
                case MaestroSimple.Cliente: return Cliente;
                case MaestroSimple.EstacionPeaje: return EstacionPeaje;
                case MaestroSimple.Planta: return Planta;
                default: throw new System.ArgumentOutOfRangeException(nameof(maestro), maestro, "Maestro desconocido.");
            }
        }

        /// <summary>Deja el maestro en <paramref name="nuevoActivo"/>; si ya lo estaba no lo toca ni audita.</summary>
        public static ResultadoCambioEstado CambiarEstado(MaestroSimple maestro, int id, bool nuevoActivo) =>
            EstadoActivoService.Cambiar(Tabla(maestro), id, nuevoActivo);
    }
}
