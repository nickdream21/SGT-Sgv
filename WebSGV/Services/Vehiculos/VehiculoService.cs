using System;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using WebSGV.Services.Common;

namespace WebSGV.Services.Vehiculos
{
    /// <summary>Tabla de vehículos propios que se registran en el sistema.</summary>
    public enum TipoVehiculo
    {
        Tracto,
        Carreta
    }

    /// <summary>
    /// Alta y edición de tractos (<c>RegistroTractos.aspx</c>) y semirremolques (<c>RegistroSemiremolques.aspx</c>)
    /// (bloque 5, B5-1). Una placa es única por su <see cref="PlacaVehiculo.Clave"/>: "ABC123" y "ABC-123" son la
    /// misma. Se guarda en su forma <see cref="PlacaVehiculo.Canonica"/>, así los índices únicos de la BD
    /// (<c>UC_PlacaTracto</c>, <c>UX_Carreta_placaCarreta</c>, comparación exacta) también la protegen ante dos
    /// usuarios a la vez. Los errores para el usuario salen como <see cref="ErrorNegocioException"/>.
    /// </summary>
    public static class VehiculoService
    {
        /// <summary>Largo de <c>placaTracto</c> / <c>placaCarreta</c> (varchar(10)).</summary>
        public const int LargoPlaca = 10;

        /// <summary>Largo de <c>marca</c> y <c>modelo</c> (varchar(30)).</summary>
        public const int LargoMarcaModelo = 30;

        /// <summary>Mínimo de letras/números de una placa (las peruanas tienen 6).</summary>
        private const int MinimoAlfanumericos = 6;

        private static readonly Regex RxMarcaModelo =
            new Regex(@"^[A-ZÁÉÍÓÚÑÜ0-9\s\-.]{2," + LargoMarcaModelo + "}$", RegexOptions.Compiled);

        /// <summary>Nombres fijos de tabla y columnas (nunca vienen del usuario).</summary>
        private sealed class Tabla
        {
            public string Nombre, Id, Placa, Descripcion;
        }

        private static Tabla De(TipoVehiculo tipo) =>
            tipo == TipoVehiculo.Tracto
                ? new Tabla { Nombre = "Tracto", Id = "idTracto", Placa = "placaTracto", Descripcion = "tracto" }
                : new Tabla { Nombre = "Carreta", Id = "idCarreta", Placa = "placaCarreta", Descripcion = "semirremolque" };

        /// <summary>Datos ya validados y normalizados (placa canónica, marca y modelo en mayúsculas).</summary>
        public sealed class DatosVehiculo
        {
            public string Placa { get; set; }
            public string Marca { get; set; }
            public string Modelo { get; set; }
        }

        /// <summary>
        /// Valida y normaliza lo que escribió el usuario. Lógica pura: lanza <see cref="ErrorNegocioException"/>
        /// con el primer problema.
        /// En la edición (<paramref name="guardado"/> con los valores actuales), la marca y el modelo solo se
        /// validan si cambian: un vehículo antiguo con modelo vacío o de 1 carácter se puede editar sin tocarlo,
        /// y ese valor se conserva tal cual. La placa se valida siempre.
        /// </summary>
        public static DatosVehiculo Validar(string placa, string marca, string modelo, DatosVehiculo guardado = null)
        {
            string placaCanonica = PlacaVehiculo.Canonica(placa);
            string marcaN = Normalizar(marca);
            string modeloN = Normalizar(modelo);
            bool marcaIgual = guardado != null && marcaN == Normalizar(guardado.Marca);
            bool modeloIgual = guardado != null && modeloN == Normalizar(guardado.Modelo);

            if (placaCanonica == null || (marcaN.Length == 0 && !marcaIgual) || (modeloN.Length == 0 && !modeloIgual))
                throw new ErrorNegocioException("Debe completar la placa, la marca y el modelo.");

            if (!ValidacionHelper.EsPlaca(placaCanonica) || PlacaVehiculo.Clave(placaCanonica).Length < MinimoAlfanumericos)
                throw new ErrorNegocioException(
                    $"Placa inválida: use de 6 a {LargoPlaca} letras, números o guion (por ejemplo ABC-123).");

            if (!marcaIgual && !RxMarcaModelo.IsMatch(marcaN))
                throw new ErrorNegocioException($"Marca inválida: use entre 2 y {LargoMarcaModelo} letras, números, espacios, guion o punto.");

            if (!modeloIgual && !RxMarcaModelo.IsMatch(modeloN))
                throw new ErrorNegocioException($"Modelo inválido: use entre 2 y {LargoMarcaModelo} letras, números, espacios, guion o punto.");

            return new DatosVehiculo
            {
                Placa = placaCanonica,
                Marca = marcaIgual ? guardado.Marca : marcaN,
                Modelo = modeloIgual ? guardado.Modelo : modeloN
            };
        }

        private static string Normalizar(string texto) => (texto ?? "").Trim().ToUpper(CultureInfo.InvariantCulture);

        /// <summary>Registra un tracto o semirremolque activo. Devuelve su id.</summary>
        public static int Registrar(TipoVehiculo tipo, string placa, string marca, string modelo)
        {
            var t = De(tipo);
            var d = Validar(placa, marca, modelo);
            ExigirPlacaLibre(t, d.Placa, null);

            int id;
            try
            {
                id = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    // SCOPE_IDENTITY y no OUTPUT: las tablas tienen trigger de auditoría (TR_*_Auditoria)
                    $"INSERT INTO {t.Nombre} ({t.Placa}, marca, modelo, activo) VALUES (@placa, @marca, @modelo, 1); " +
                    "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    DbHelper.Param("@placa", d.Placa),
                    DbHelper.Param("@marca", d.Marca),
                    DbHelper.Param("@modelo", d.Modelo)));
            }
            catch (SqlException ex) when (EsClaveDuplicada(ex))
            {
                throw new ErrorNegocioException($"Ya existe la placa {d.Placa}.", ex);
            }

            AuditoriaHelper.Registrar("INSERT", t.Nombre, id,
                $"{Capital(t.Descripcion)} registrado - Placa: {d.Placa}, Marca: {d.Marca}, Modelo: {d.Modelo}");
            return id;
        }

        /// <summary>Cambia placa, marca y modelo de un tracto o semirremolque.</summary>
        public static void Actualizar(TipoVehiculo tipo, int id, string placa, string marca, string modelo)
        {
            var t = De(tipo);

            var actual = DbHelper.ConsultarTabla(
                $"SELECT {t.Placa} AS placa, marca, modelo FROM {t.Nombre} WHERE {t.Id} = @id", DbHelper.Param("@id", id));
            if (actual.Rows.Count == 0)
                throw new ErrorNegocioException($"El {t.Descripcion} ya no existe; recargue la página.");

            var anterior = new DatosVehiculo
            {
                Placa = actual.Rows[0]["placa"] as string,
                Marca = actual.Rows[0]["marca"] as string,
                Modelo = actual.Rows[0]["modelo"] as string
            };
            var d = Validar(placa, marca, modelo, anterior);

            ExigirPlacaLibre(t, d.Placa, id);

            try
            {
                DbHelper.EjecutarNonQuery(
                    $"UPDATE {t.Nombre} SET {t.Placa} = @placa, marca = @marca, modelo = @modelo WHERE {t.Id} = @id",
                    DbHelper.Param("@placa", d.Placa),
                    DbHelper.Param("@marca", d.Marca),
                    DbHelper.Param("@modelo", d.Modelo),
                    DbHelper.Param("@id", id));
            }
            catch (SqlException ex) when (EsClaveDuplicada(ex))
            {
                throw new ErrorNegocioException($"Ya existe la placa {d.Placa}.", ex);
            }

            AuditoriaHelper.Registrar("UPDATE", t.Nombre, id,
                $"{Capital(t.Descripcion)} editado - Placa: {d.Placa}, Marca: {d.Marca}, Modelo: {d.Modelo}",
                valoresAnteriores: Describir(anterior),
                valoresNuevos: Describir(d));
        }

        private static readonly TablaActivable TablaActivableTracto =
            new TablaActivable("Tracto", "idTracto", "activo", "placaTracto", "tracto", femenino: false);

        private static readonly TablaActivable TablaActivableCarreta =
            new TablaActivable("Carreta", "idCarreta", "activo", "placaCarreta", "semirremolque", femenino: false);

        /// <summary>
        /// Activa o desactiva un tracto o semirremolque con estado destino explícito (B5-3): si ya lo estaba no
        /// lo toca ni audita.
        /// </summary>
        public static ResultadoCambioEstado CambiarEstado(TipoVehiculo tipo, int id, bool nuevoActivo) =>
            EstadoActivoService.Cambiar(tipo == TipoVehiculo.Tracto ? TablaActivableTracto : TablaActivableCarreta,
                id, nuevoActivo);

        /// <summary>
        /// Rechaza la placa si otro vehículo del mismo tipo tiene la misma clave (sin guion ni espacios),
        /// aunque esté inactivo o escrito de otra forma.
        /// </summary>
        private static void ExigirPlacaLibre(Tabla t, string placaCanonica, int? idPropio)
        {
            object existente = DbHelper.EjecutarEscalar(
                $@"SELECT TOP 1 {t.Placa} FROM {t.Nombre}
                   WHERE REPLACE(REPLACE(UPPER({t.Placa}), '-', ''), ' ', '') = @clave
                     AND (@id IS NULL OR {t.Id} <> @id)",
                DbHelper.Param("@clave", PlacaVehiculo.Clave(placaCanonica)),
                DbHelper.Param("@id", idPropio));

            if (existente != null && existente != DBNull.Value)
                throw new ErrorNegocioException(
                    $"Ya existe la placa {placaCanonica} (registrada como {existente.ToString().Trim()}).");
        }

        private static string Describir(DatosVehiculo v) =>
            $"Placa: {v.Placa ?? "(vacía)"}, Marca: {v.Marca ?? "(vacía)"}, Modelo: {v.Modelo ?? "(vacío)"}";

        private static bool EsClaveDuplicada(SqlException ex) => ex.Number == 2601 || ex.Number == 2627;

        private static string Capital(string s) => char.ToUpper(s[0], CultureInfo.InvariantCulture) + s.Substring(1);
    }
}
