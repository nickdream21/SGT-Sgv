namespace WebSGV.Services.Common
{
    /// <summary>
    /// Largos de columnas de maestros que validan las pantallas de registro (bloque 5, B5-2), para que la
    /// pantalla no acepte más de lo que cabe y la BD no corte con un error técnico. Una prueba
    /// (<c>LargoColumnasBdTests</c>) los compara con <c>INFORMATION_SCHEMA.COLUMNS</c>.
    /// Los del conductor están en <c>RegistroConductorService</c> / <c>DocumentoConductor</c>, y los de los
    /// catálogos por cliente en <c>CatalogoClienteService</c>.
    /// </summary>
    public static class LargoColumnas
    {
        /// <summary><c>Cliente.nombre</c> (varchar(100)).</summary>
        public const int ClienteNombre = 100;

        /// <summary><c>EstacionesPeaje.nombre</c> (varchar(100)).</summary>
        public const int EstacionPeajeNombre = 100;
    }
}
