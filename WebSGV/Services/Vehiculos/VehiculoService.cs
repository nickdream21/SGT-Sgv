using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
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
    /// La placa de un vehículo con historial (despachos u otros registros, ver <c>ReferenciasTracto</c>) no se
    /// puede cambiar, salvo de formato (B5-4, decisión D2).
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
            public Referencia[] Referencias;
        }

        /// <summary>
        /// Un registro histórico que apunta al vehículo: por id (<see cref="Columnas"/> = columna de id, se
        /// compara con el id) o por el texto de la placa (se compara por <see cref="PlacaVehiculo.Clave"/>, en SQL
        /// <c>REPLACE(REPLACE(UPPER(col),'-',''),' ','')</c>). Varias columnas cuentan cada fila una vez.
        /// </summary>
        private sealed class Referencia
        {
            public string Tabla, Descripcion;
            public string[] Columnas;
            public bool PorTexto;
            public bool EsDespacho;
        }

        private static Referencia PorId(string tabla, string columna, string descripcion, bool esDespacho = false) =>
            new Referencia { Tabla = tabla, Columnas = new[] { columna }, Descripcion = descripcion, EsDespacho = esDespacho };

        private static Referencia PorTexto(string tabla, string descripcion, params string[] columnas) =>
            new Referencia { Tabla = tabla, Columnas = columnas, Descripcion = descripcion, PorTexto = true };

        // B5-4 (decisión D2): qué cuenta como "historial" de un vehículo. Si tiene alguno, su placa no se puede
        // cambiar (sí la marca y el modelo). Revisado en la BD el 2026-11-17 (INFORMATION_SCHEMA y sys.sql_modules):
        //   Por id (FK a Tracto/Carreta):
        //     Despachos.idTracto/idCarreta, OrdenViaje.idTracto/idCarreta.
        //     (E3-8, 2026-11-21: fuera las tablas de Grifo AbastecimientoCombustible, DespachoCombustibleObra e
        //     IngresoCombustibleEcuador, vacías y de un módulo retirado; borradas en la migración 60.)
        //   Por texto de la placa (se desengancharían si la placa cambia):
        //     SeguimientoExportacion.tracto1/tracto2/carreta (copia del despacho; sp_SE_SincronizarDespachos y el
        //       GPS emparejan por placa), DestinoDescarga.placaTracto/placaCarreta (lista de plantas por pedido y
        //       carreta, sin guion), Indicadores.tracto1/tracto2/carreta (histórico importado del Excel).
        //   No cuentan: SeguimientoExportacion.ultimaPlacaGps (siempre es uno de tracto1/tracto2/carreta del mismo
        //     viaje), camionetas/volquetes (otras flotas). Las guías, órdenes de viaje y PDFs
        //     firmados llevan la placa impresa, pero cuelgan de un despacho u orden de viaje, que ya cuenta.
        //   Se cuentan también los registros anulados o inactivos: siguen siendo historial.
        private static readonly Referencia[] ReferenciasTracto =
        {
            PorId("Despachos", "idTracto", "despacho(s)", esDespacho: true),
            PorId("OrdenViaje", "idTracto", "orden(es) de viaje"),
            PorTexto("SeguimientoExportacion", "viaje(s) del seguimiento de exportación", "tracto1", "tracto2"),
            PorTexto("DestinoDescarga", "destino(s) de descarga", "placaTracto"),
            PorTexto("Indicadores", "registro(s) de indicadores", "tracto1", "tracto2")
        };

        private static readonly Referencia[] ReferenciasCarreta =
        {
            PorId("Despachos", "idCarreta", "despacho(s)", esDespacho: true),
            PorId("OrdenViaje", "idCarreta", "orden(es) de viaje"),
            PorTexto("SeguimientoExportacion", "viaje(s) del seguimiento de exportación", "carreta"),
            PorTexto("DestinoDescarga", "destino(s) de descarga", "placaCarreta"),
            PorTexto("Indicadores", "registro(s) de indicadores", "carreta")
        };

        private static Tabla De(TipoVehiculo tipo) =>
            tipo == TipoVehiculo.Tracto
                ? new Tabla { Nombre = "Tracto", Id = "idTracto", Placa = "placaTracto", Descripcion = "tracto", Referencias = ReferenciasTracto }
                : new Tabla { Nombre = "Carreta", Id = "idCarreta", Placa = "placaCarreta", Descripcion = "semirremolque", Referencias = ReferenciasCarreta };

        /// <summary>Clave de una columna de placa en SQL (igual que <see cref="PlacaVehiculo.Clave"/> para placas válidas).</summary>
        private static string ClaveSql(string columna) => $"REPLACE(REPLACE(UPPER({columna}), '-', ''), ' ', '')";

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

        /// <summary>
        /// Cambio de placa rechazado por historial (B5-4). Lleva la placa guardada para que la página la vuelva a
        /// poner en el modal en lugar de la rechazada.
        /// </summary>
        public sealed class PlacaBloqueadaException : ErrorNegocioException
        {
            public PlacaBloqueadaException(string mensaje, string placaGuardada) : base(mensaje) => PlacaGuardada = placaGuardada;

            public string PlacaGuardada { get; }
        }

        /// <summary>Cuántos registros de una clase apuntan al vehículo (B5-4).</summary>
        public sealed class ReferenciaContada
        {
            public string Descripcion { get; set; }
            public int Cantidad { get; set; }
            /// <summary>Es la cuenta de despachos: el mensaje habla de ellos si hay alguno.</summary>
            public bool EsDespacho { get; set; }
        }

        /// <summary>Dos placas son la misma si tienen la misma <see cref="PlacaVehiculo.Clave"/> ("T4I-868 " = "T4I868").</summary>
        public static bool MismaPlaca(string a, string b) =>
            string.Equals(PlacaVehiculo.Clave(a), PlacaVehiculo.Clave(b), StringComparison.Ordinal);

        /// <summary>
        /// Regla B5-4 (decisión D2), lógica pura: null si la placa se puede cambiar; si no, el mensaje para el
        /// usuario. Cambiar solo el formato (misma clave) siempre se permite. Con despachos el mensaje los cuenta;
        /// sin despachos pero con otro historial, lista lo que hay.
        /// </summary>
        public static string MotivoPlacaBloqueada(string placaGuardada, string placaNueva, string descripcionVehiculo,
            IEnumerable<ReferenciaContada> historial)
        {
            if (MismaPlaca(placaGuardada, placaNueva)) return null;

            var conRegistros = (historial ?? Enumerable.Empty<ReferenciaContada>()).Where(r => r != null && r.Cantidad > 0).ToList();
            if (conRegistros.Count == 0) return null;

            string placa = (placaGuardada ?? "").Trim();
            const string Sugerencia = "Si la placa está mal, registre el vehículo correcto y desactive este.";

            int despachos = conRegistros.Where(r => r.EsDespacho).Sum(r => r.Cantidad);
            if (despachos > 0)
                return $"La placa de {placa} no se puede cambiar: tiene {despachos} despacho(s) registrados. {Sugerencia}";

            string detalle = string.Join(", ", conRegistros.Select(r => $"{r.Cantidad} {r.Descripcion}"));
            return $"La placa de {placa} no se puede cambiar: el {descripcionVehiculo} tiene historial registrado ({detalle}). {Sugerencia}";
        }

        /// <summary>
        /// Cuenta, por cada <see cref="Referencia"/> del tipo, los registros que apuntan al vehículo (por id o por la
        /// clave de su placa guardada). Una sola consulta. Si la placa guardada está vacía, las referencias por texto
        /// no se consultan (cuentan 0): una celda vacía de otra tabla no es historial de este vehículo.
        /// </summary>
        private static List<ReferenciaContada> ContarHistorial(Tabla t, int id, string placaGuardada)
        {
            string clave = PlacaVehiculo.Clave(placaGuardada);
            var columnas = new List<string>();
            for (int i = 0; i < t.Referencias.Length; i++)
            {
                var r = t.Referencias[i];
                if (r.PorTexto && clave == null)
                {
                    columnas.Add($"0 AS c{i}");
                    continue;
                }
                string condicion = r.PorTexto
                    ? string.Join(" OR ", r.Columnas.Select(c => $"{ClaveSql(c)} = @clave"))
                    : $"{r.Columnas[0]} = @id";
                columnas.Add($"(SELECT COUNT(*) FROM {r.Tabla} WHERE {condicion}) AS c{i}");
            }

            var fila = DbHelper.ConsultarTabla("SELECT " + string.Join(", ", columnas),
                DbHelper.Param("@id", id),
                DbHelper.Param("@clave", (object)clave ?? DBNull.Value));

            return t.Referencias.Select((r, i) => new ReferenciaContada
            {
                Descripcion = r.Descripcion,
                EsDespacho = r.EsDespacho,
                Cantidad = Convert.ToInt32(fila.Rows[0]["c" + i], CultureInfo.InvariantCulture)
            }).ToList();
        }

        /// <summary>
        /// Consulta de la grilla de <c>RegistroTractos</c> / <c>RegistroSemiremolques</c>: idX, placaX, marca,
        /// modelo, activo y <c>conHistorial</c> (bit, B5-4) para mostrar la placa como solo lectura en el modal.
        /// Las referencias por texto se juntan una vez (UNION de claves) y se cruzan con un hash join.
        /// </summary>
        public static string ConsultaGrilla(TipoVehiculo tipo)
        {
            var t = De(tipo);
            var porId = t.Referencias.Where(r => !r.PorTexto)
                .Select(r => $"EXISTS (SELECT 1 FROM {r.Tabla} r WHERE r.{r.Columnas[0]} = v.{t.Id})");
            var claves = t.Referencias.Where(r => r.PorTexto)
                .SelectMany(r => r.Columnas.Select(c => $"SELECT {ClaveSql(c)} AS clave FROM {r.Tabla}"));

            return $@"SELECT v.{t.Id}, v.{t.Placa}, v.marca, v.modelo, v.activo,
       CAST(CASE WHEN {string.Join(" OR ", porId)} OR h.clave IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS conHistorial
FROM {t.Nombre} v
LEFT JOIN ({string.Join(" UNION ", claves)}) h ON h.clave = {ClaveSql("v." + t.Placa)} AND {ClaveSql("v." + t.Placa)} <> ''
ORDER BY v.{t.Placa}";
        }

        /// <summary>Filas de la grilla de <c>RegistroTractos</c> / <c>RegistroSemiremolques</c> (<see cref="ConsultaGrilla"/>, E6-19).</summary>
        public static DataTable ObtenerGrilla(TipoVehiculo tipo) => DbHelper.ConsultarTabla(ConsultaGrilla(tipo));

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

            // B5-4: la placa de un vehículo con historial no cambia (solo su formato: misma clave).
            if (!MismaPlaca(anterior.Placa, d.Placa))
            {
                string motivo = MotivoPlacaBloqueada(anterior.Placa, d.Placa, t.Descripcion, ContarHistorial(t, id, anterior.Placa));
                if (motivo != null)
                    throw new PlacaBloqueadaException(motivo, (anterior.Placa ?? "").Trim());
            }

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
