using System;
using System.Collections.Generic;
using System.Data;
using WebSGV.Helpers;
using WebSGV.Services.Common;

namespace WebSGV.Services.Clientes
{
    /// <summary>Los cuatro catálogos que cuelgan de un cliente.</summary>
    public enum TipoCatalogo
    {
        Producto,
        PlantaCarga,
        PlantaDescarga,
        Ruta
    }

    /// <summary>
    /// Acceso a datos de los catálogos por cliente (<c>CatalogoCliente.aspx</c>).
    ///
    /// Las cuatro tablas tienen la misma forma —id, nombre, un campo secundario,
    /// baja lógica y FK al cliente— pero nombres de columna distintos por razones
    /// históricas: <c>Producto</c> y <c>Ruta</c> usan <c>activo</c>, las plantas usan
    /// <c>activa</c>; el campo secundario es una descripción en unas y una dirección en
    /// otras. En vez de repetir cuatro CRUD casi idénticos, esas diferencias viven en
    /// un mapa de metadatos y las consultas se arman a partir de él.
    ///
    /// Sobre la seguridad de armar SQL con nombres de tabla: los identificadores salen
    /// exclusivamente de <see cref="Metadatos"/>, que es una constante del programa. El
    /// único punto de entrada es un valor de <see cref="TipoCatalogo"/>, y un enum
    /// desconocido lanza en <see cref="Meta"/> antes de tocar la base. Todo lo que
    /// viene del usuario —nombres, detalles, ids— sigue pasando por parámetros.
    /// </summary>
    public static class CatalogoClienteService
    {
        private sealed class MetaCatalogo
        {
            public string Tabla;
            public string ColumnaId;
            public string ColumnaActivo;
            public string ColumnaDetalle;
            public string ColumnaFecha;
            public string Etiqueta;
            public string EtiquetaDetalle;
            /// <summary>Género de <see cref="Etiqueta"/> para los mensajes ("la ruta", "el producto").</summary>
            public bool Femenino;
            /// <summary>Rutas admiten idCliente nulo: valen para todos los clientes.</summary>
            public bool PermiteSinCliente;
            /// <summary>Largo de la columna <c>nombre</c> (lo comprueba una prueba contra la BD).</summary>
            public int LargoNombre;
            /// <summary>Largo de la columna del detalle (descripción o dirección).</summary>
            public int LargoDetalle;
        }

        private static readonly Dictionary<TipoCatalogo, MetaCatalogo> Metadatos =
            new Dictionary<TipoCatalogo, MetaCatalogo>
            {
                [TipoCatalogo.Producto] = new MetaCatalogo
                {
                    Tabla = "Producto",
                    ColumnaId = "idProducto",
                    ColumnaActivo = "activo",
                    ColumnaDetalle = "descripcion",
                    ColumnaFecha = "fechaRegistro",
                    Etiqueta = "producto",
                    Femenino = false,
                    EtiquetaDetalle = "Descripción",
                    LargoNombre = 50,
                    LargoDetalle = 300
                },
                [TipoCatalogo.PlantaCarga] = new MetaCatalogo
                {
                    Tabla = "PlantaCarga",
                    ColumnaId = "idPlantaCarga",
                    ColumnaActivo = "activa",
                    ColumnaDetalle = "direccion",
                    ColumnaFecha = "fechaCreacion",
                    Etiqueta = "planta de carga",
                    Femenino = true,
                    EtiquetaDetalle = "Dirección",
                    LargoNombre = 100,
                    LargoDetalle = 200
                },
                [TipoCatalogo.PlantaDescarga] = new MetaCatalogo
                {
                    Tabla = "PlantaDescarga",
                    ColumnaId = "idPlanta",
                    ColumnaActivo = "activa",
                    ColumnaDetalle = "direccion",
                    ColumnaFecha = "fechaCreacion",
                    Etiqueta = "planta de descarga",
                    Femenino = true,
                    EtiquetaDetalle = "Dirección",
                    LargoNombre = 100,
                    LargoDetalle = 200
                },
                [TipoCatalogo.Ruta] = new MetaCatalogo
                {
                    Tabla = "Ruta",
                    ColumnaId = "idRuta",
                    ColumnaActivo = "activo",
                    ColumnaDetalle = "descripcion",
                    ColumnaFecha = "fechaRegistro",
                    Etiqueta = "ruta",
                    Femenino = true,
                    EtiquetaDetalle = "Descripción",
                    LargoNombre = 100,
                    LargoDetalle = 200,
                    PermiteSinCliente = true
                }
            };

        private static MetaCatalogo Meta(TipoCatalogo tipo)
        {
            if (!Metadatos.TryGetValue(tipo, out MetaCatalogo meta))
                throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de catálogo desconocido.");
            return meta;
        }

        /// <summary>Nombre del catálogo en singular, para mensajes al usuario.</summary>
        public static string Etiqueta(TipoCatalogo tipo) => Meta(tipo).Etiqueta;

        /// <summary>Género de la etiqueta: "la ruta" / "el producto" (para los mensajes).</summary>
        public static bool EsFemenino(TipoCatalogo tipo) => Meta(tipo).Femenino;

        /// <summary>Rótulo del campo secundario: "Descripción" o "Dirección" según el catálogo.</summary>
        public static string EtiquetaDetalle(TipoCatalogo tipo) => Meta(tipo).EtiquetaDetalle;

        /// <summary>Indica si el catálogo admite filas sin cliente (sólo Ruta).</summary>
        public static bool PermiteSinCliente(TipoCatalogo tipo) => Meta(tipo).PermiteSinCliente;

        /// <summary>Largo máximo del nombre (el de la columna; para el MaxLength de la pantalla).</summary>
        public static int LargoNombre(TipoCatalogo tipo) => Meta(tipo).LargoNombre;

        /// <summary>Largo máximo de la descripción o dirección (el de la columna).</summary>
        public static int LargoDetalle(TipoCatalogo tipo) => Meta(tipo).LargoDetalle;

        /// <summary>Columna del detalle (descripción o dirección), para comparar los largos con la BD en las pruebas.</summary>
        public static string ColumnaDetalle(TipoCatalogo tipo) => Meta(tipo).ColumnaDetalle;

        /// <summary>
        /// Rechaza con <see cref="ErrorNegocioException"/> un nombre vacío o un nombre/detalle más largo que su
        /// columna (sin esto la BD cortaba con un error técnico). Lógica pura.
        /// </summary>
        public static void Validar(TipoCatalogo tipo, string nombre, string detalle)
        {
            MetaCatalogo m = Meta(tipo);
            string n = (nombre ?? "").Trim();
            string d = (detalle ?? "").Trim();

            if (n.Length == 0)
                throw new ErrorNegocioException("El nombre es obligatorio.");
            if (n.Length > m.LargoNombre)
                throw new ErrorNegocioException(
                    $"El nombre admite hasta {m.LargoNombre} caracteres (tiene {n.Length}).");
            if (d.Length > m.LargoDetalle)
                throw new ErrorNegocioException(
                    $"La {m.EtiquetaDetalle.ToLowerInvariant()} admite hasta {m.LargoDetalle} caracteres (tiene {d.Length}).");
        }

        /// <summary>Clientes activos para el selector de la pantalla.</summary>
        public static DataTable ObtenerClientes() =>
            DbHelper.ConsultarTabla(
                @"SELECT idCliente, nombre, esExportador
                    FROM Cliente
                   WHERE activo = 1
                ORDER BY nombre");

        /// <summary>
        /// Filas del catálogo de un cliente. Para <see cref="TipoCatalogo.Ruta"/> incluye
        /// además las rutas sin cliente, que son de uso general.
        /// </summary>
        public static DataTable Obtener(TipoCatalogo tipo, int idCliente)
        {
            MetaCatalogo m = Meta(tipo);

            string filtroCliente = m.PermiteSinCliente
                ? "(idCliente = @idCliente OR idCliente IS NULL)"
                : "idCliente = @idCliente";

            string sql =
                $@"SELECT {m.ColumnaId}            AS idItem,
                          nombre,
                          ISNULL({m.ColumnaDetalle}, '') AS detalle,
                          {m.ColumnaActivo}        AS activo,
                          CASE WHEN idCliente IS NULL THEN 1 ELSE 0 END AS esGeneral
                     FROM {m.Tabla}
                    WHERE {filtroCliente}
                 ORDER BY nombre";

            return DbHelper.ConsultarTabla(sql, DbHelper.Param("@idCliente", idCliente));
        }

        /// <summary>Cuenta las filas activas de cada catálogo, para las pestañas.</summary>
        public static Dictionary<TipoCatalogo, int> ContarActivos(int idCliente)
        {
            var conteos = new Dictionary<TipoCatalogo, int>();

            foreach (TipoCatalogo tipo in Metadatos.Keys)
            {
                MetaCatalogo m = Metadatos[tipo];
                string filtroCliente = m.PermiteSinCliente
                    ? "(idCliente = @idCliente OR idCliente IS NULL)"
                    : "idCliente = @idCliente";

                object valor = DbHelper.EjecutarEscalar(
                    $"SELECT COUNT(*) FROM {m.Tabla} WHERE {filtroCliente} AND {m.ColumnaActivo} = 1",
                    DbHelper.Param("@idCliente", idCliente));

                conteos[tipo] = Convert.ToInt32(valor);
            }

            return conteos;
        }

        /// <summary>
        /// ¿Ya existe una fila con ese nombre para el cliente? Se compara sin distinguir
        /// mayúsculas ni espacios: dos productos que sólo difieren en eso son el mismo
        /// producto escrito dos veces.
        /// </summary>
        public static bool ExisteNombre(TipoCatalogo tipo, int idCliente, string nombre, int idExcluir = 0)
        {
            MetaCatalogo m = Meta(tipo);

            object valor = DbHelper.EjecutarEscalar(
                $@"SELECT COUNT(*) FROM {m.Tabla}
                    WHERE idCliente = @idCliente
                      AND UPPER(LTRIM(RTRIM(nombre))) = UPPER(LTRIM(RTRIM(@nombre)))
                      AND {m.ColumnaId} <> @idExcluir",
                DbHelper.Param("@idCliente", idCliente),
                DbHelper.Param("@nombre", nombre),
                DbHelper.Param("@idExcluir", idExcluir));

            return Convert.ToInt32(valor) > 0;
        }

        /// <summary>Agrega una fila al catálogo del cliente. Devuelve su id (para la auditoría).</summary>
        public static int Crear(TipoCatalogo tipo, int idCliente, string nombre, string detalle)
        {
            MetaCatalogo m = Meta(tipo);
            Validar(tipo, nombre, detalle);

            // SCOPE_IDENTITY y no OUTPUT: si algún día la tabla tiene trigger, OUTPUT sin INTO falla
            return Convert.ToInt32(DbHelper.EjecutarEscalar(
                $@"INSERT INTO {m.Tabla} (nombre, {m.ColumnaDetalle}, idCliente, {m.ColumnaActivo}, {m.ColumnaFecha})
                   VALUES (@nombre, @detalle, @idCliente, 1, @fecha);
                   SELECT CAST(SCOPE_IDENTITY() AS INT);",
                DbHelper.Param("@nombre", nombre.Trim()),
                DbHelper.Param("@detalle", Opcional(detalle)),
                DbHelper.Param("@idCliente", idCliente),
                DbHelper.Param("@fecha", FechaHelper.Ahora())));
        }

        /// <summary>Actualiza nombre y detalle de una fila. Devuelve las filas afectadas.</summary>
        public static int Actualizar(TipoCatalogo tipo, int idItem, string nombre, string detalle)
        {
            MetaCatalogo m = Meta(tipo);
            Validar(tipo, nombre, detalle);

            return DbHelper.EjecutarNonQuery(
                $@"UPDATE {m.Tabla}
                      SET nombre = @nombre,
                          {m.ColumnaDetalle} = @detalle
                    WHERE {m.ColumnaId} = @idItem",
                DbHelper.Param("@nombre", nombre.Trim()),
                DbHelper.Param("@detalle", Opcional(detalle)),
                DbHelper.Param("@idItem", idItem));
        }

        /// <summary>
        /// Activa o desactiva una fila con estado destino explícito (B5-3): si ya lo estaba no la toca ni
        /// audita. Es baja lógica a propósito: un producto retirado sigue referenciado por las órdenes de
        /// viaje históricas, así que borrarlo rompería esos registros.
        /// </summary>
        public static ResultadoCambioEstado CambiarActivo(TipoCatalogo tipo, int idItem, bool nuevoActivo)
        {
            MetaCatalogo m = Meta(tipo);
            return EstadoActivoService.Cambiar(
                new TablaActivable(m.Tabla, m.ColumnaId, m.ColumnaActivo, "nombre", m.Etiqueta, m.Femenino),
                idItem, nuevoActivo);
        }

        /// <summary>Nombre de la tabla del catálogo, para registrar la auditoría.</summary>
        public static string NombreTabla(TipoCatalogo tipo) => Meta(tipo).Tabla;

        private static object Opcional(string valor) =>
            string.IsNullOrWhiteSpace(valor) ? (object)DBNull.Value : valor.Trim();
    }
}
