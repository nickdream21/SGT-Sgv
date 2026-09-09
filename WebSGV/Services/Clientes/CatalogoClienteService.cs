using System;
using System.Collections.Generic;
using System.Data;
using WebSGV.Helpers;

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
            /// <summary>Rutas admiten idCliente nulo: valen para todos los clientes.</summary>
            public bool PermiteSinCliente;
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
                    EtiquetaDetalle = "Descripción"
                },
                [TipoCatalogo.PlantaCarga] = new MetaCatalogo
                {
                    Tabla = "PlantaCarga",
                    ColumnaId = "idPlantaCarga",
                    ColumnaActivo = "activa",
                    ColumnaDetalle = "direccion",
                    ColumnaFecha = "fechaCreacion",
                    Etiqueta = "planta de carga",
                    EtiquetaDetalle = "Dirección"
                },
                [TipoCatalogo.PlantaDescarga] = new MetaCatalogo
                {
                    Tabla = "PlantaDescarga",
                    ColumnaId = "idPlanta",
                    ColumnaActivo = "activa",
                    ColumnaDetalle = "direccion",
                    ColumnaFecha = "fechaCreacion",
                    Etiqueta = "planta de descarga",
                    EtiquetaDetalle = "Dirección"
                },
                [TipoCatalogo.Ruta] = new MetaCatalogo
                {
                    Tabla = "Ruta",
                    ColumnaId = "idRuta",
                    ColumnaActivo = "activo",
                    ColumnaDetalle = "descripcion",
                    ColumnaFecha = "fechaRegistro",
                    Etiqueta = "ruta",
                    EtiquetaDetalle = "Descripción",
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

        /// <summary>Rótulo del campo secundario: "Descripción" o "Dirección" según el catálogo.</summary>
        public static string EtiquetaDetalle(TipoCatalogo tipo) => Meta(tipo).EtiquetaDetalle;

        /// <summary>Indica si el catálogo admite filas sin cliente (sólo Ruta).</summary>
        public static bool PermiteSinCliente(TipoCatalogo tipo) => Meta(tipo).PermiteSinCliente;

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

        /// <summary>Agrega una fila al catálogo del cliente. Devuelve las filas afectadas.</summary>
        public static int Crear(TipoCatalogo tipo, int idCliente, string nombre, string detalle)
        {
            MetaCatalogo m = Meta(tipo);

            return DbHelper.EjecutarNonQuery(
                $@"INSERT INTO {m.Tabla} (nombre, {m.ColumnaDetalle}, idCliente, {m.ColumnaActivo}, {m.ColumnaFecha})
                   VALUES (@nombre, @detalle, @idCliente, 1, @fecha)",
                DbHelper.Param("@nombre", nombre.Trim()),
                DbHelper.Param("@detalle", Opcional(detalle)),
                DbHelper.Param("@idCliente", idCliente),
                DbHelper.Param("@fecha", FechaHelper.Ahora()));
        }

        /// <summary>Actualiza nombre y detalle de una fila. Devuelve las filas afectadas.</summary>
        public static int Actualizar(TipoCatalogo tipo, int idItem, string nombre, string detalle)
        {
            MetaCatalogo m = Meta(tipo);

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
        /// Activa o desactiva una fila. Es baja lógica a propósito: un producto retirado
        /// sigue referenciado por las órdenes de viaje históricas, así que borrarlo
        /// rompería esos registros.
        /// </summary>
        public static int AlternarActivo(TipoCatalogo tipo, int idItem)
        {
            MetaCatalogo m = Meta(tipo);

            return DbHelper.EjecutarNonQuery(
                $@"UPDATE {m.Tabla}
                      SET {m.ColumnaActivo} = CASE WHEN {m.ColumnaActivo} = 1 THEN 0 ELSE 1 END
                    WHERE {m.ColumnaId} = @idItem",
                DbHelper.Param("@idItem", idItem));
        }

        /// <summary>Nombre de una fila, para el detalle de auditoría.</summary>
        public static string ObtenerNombre(TipoCatalogo tipo, int idItem)
        {
            MetaCatalogo m = Meta(tipo);

            object valor = DbHelper.EjecutarEscalar(
                $"SELECT nombre FROM {m.Tabla} WHERE {m.ColumnaId} = @idItem",
                DbHelper.Param("@idItem", idItem));

            return valor?.ToString() ?? $"(#{idItem})";
        }

        /// <summary>Nombre de la tabla del catálogo, para registrar la auditoría.</summary>
        public static string NombreTabla(TipoCatalogo tipo) => Meta(tipo).Tabla;

        private static object Opcional(string valor) =>
            string.IsNullOrWhiteSpace(valor) ? (object)DBNull.Value : valor.Trim();
    }
}
