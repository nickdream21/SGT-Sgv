using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using WebSGV.Services.Common;

namespace WebSGV.Services.Conductor
{
    /// <summary>
    /// Alta y edición manual de conductores (<c>RegistroChoferes.aspx</c>; bloque 5, B5-2).
    /// El documento se guarda en la forma de <see cref="DocumentoConductor"/> (la misma que usa la Lista de
    /// choferes) y el duplicado se busca contra los documentos ya guardados también normalizados: un carnet
    /// "2719446" es el mismo que "002719446". Los largos son los de las columnas de <c>Conductor</c>.
    /// Los errores para el usuario salen como <see cref="ErrorNegocioException"/>.
    /// </summary>
    public static class RegistroConductorService
    {
        /// <summary>Largo de <c>Conductor.nombre</c> (varchar(50)).</summary>
        public const int LargoNombre = 50;

        /// <summary>Largo de <c>Conductor.apPaterno</c> y <c>apMaterno</c> (varchar(20)).</summary>
        public const int LargoApellido = 20;

        /// <summary>Largo de <c>Conductor.telefono</c> (varchar(12)); la pantalla pide de 7 a 9 dígitos.</summary>
        public const int LargoTelefono = 12;

        /// <summary>Valores del desplegable "Tipo documento" (el pasaporte se quitó: no hay dónde guardarlo).</summary>
        public const string TipoDni = "DNI";
        public const string TipoCarnet = "Carnet de Extranjería";

        private const string Letras = "A-Za-zÁÉÍÓÚÑÜáéíóúñü";
        private static readonly Regex RxNombre = new Regex($@"^[{Letras}\s]{{2,{LargoNombre}}}$", RegexOptions.Compiled);
        private static readonly Regex RxApellido = new Regex($@"^[{Letras}\s]{{2,{LargoApellido}}}$", RegexOptions.Compiled);
        private static readonly Regex RxTelefono = new Regex(@"^\d{7,9}$", RegexOptions.Compiled);
        private static readonly Regex RxDni = new Regex(@"^\d{" + DocumentoConductor.LargoDni + "}$", RegexOptions.Compiled);

        /// <summary>Datos ya validados. Dni y Carnet en forma normalizada (a lo más uno de los dos).</summary>
        public sealed class DatosConductor
        {
            public string Dni { get; set; }
            public string Carnet { get; set; }
            public string Nombre { get; set; }
            public string ApPaterno { get; set; }
            public string ApMaterno { get; set; }
            public string Telefono { get; set; }
        }

        /// <summary>Documento de un conductor ya guardado, tal como está en la BD.</summary>
        public sealed class DocumentoGuardado
        {
            public int IdConductor { get; set; }
            public string Dni { get; set; }
            public string Carnet { get; set; }
            public string NombreCompleto { get; set; }
        }

        // ── Validación (lógica pura) ─────────────────────────────────────

        /// <summary>
        /// Valida el tipo y número de documento del alta y lo devuelve normalizado en
        /// <paramref name="dni"/> o <paramref name="carnet"/>.
        /// </summary>
        public static void ValidarDocumento(string tipoDocumento, string numero, out string dni, out string carnet)
        {
            dni = null;
            carnet = null;
            string texto = Regex.Replace(numero ?? "", @"\s+", "");

            if (tipoDocumento == TipoDni)
            {
                if (!RxDni.IsMatch(texto))
                    throw new ErrorNegocioException($"DNI inválido: debe tener exactamente {DocumentoConductor.LargoDni} dígitos.");
                dni = DocumentoConductor.NormalizarDni(texto);
            }
            else if (tipoDocumento == TipoCarnet)
            {
                if (!DocumentoConductor.EsCarnetValido(texto))
                    throw new ErrorNegocioException(
                        $"Carnet de extranjería inválido: use de {DocumentoConductor.MinimoCarnet} a {DocumentoConductor.LargoCarnet} letras, dígitos o guion.");
                carnet = DocumentoConductor.NormalizarCarnet(texto);
            }
            else
                throw new ErrorNegocioException("Tipo de documento inválido: elija DNI o carnet de extranjería.");
        }

        /// <summary>
        /// Valida nombres, apellidos y teléfono. En la edición (<paramref name="guardado"/> con los valores
        /// actuales) un campo que no cambia no se vuelve a validar: un conductor antiguo con un teléfono de
        /// otra forma se puede editar sin tocarlo, y ese valor se conserva tal cual.
        /// </summary>
        public static DatosConductor ValidarPersona(string nombre, string apPaterno, string apMaterno, string telefono,
            DatosConductor guardado = null)
        {
            string n = Limpiar(nombre), ap = Limpiar(apPaterno), am = Limpiar(apMaterno), tel = Limpiar(telefono);

            bool Igual(string nuevo, string anterior) => guardado != null && nuevo == Limpiar(anterior);

            if (n.Length == 0 || ap.Length == 0)
                throw new ErrorNegocioException("Los nombres y el apellido paterno son obligatorios.");

            if (!Igual(n, guardado?.Nombre) && !RxNombre.IsMatch(n))
                throw new ErrorNegocioException($"Nombres inválidos: solo letras y espacios (2 a {LargoNombre} caracteres).");
            if (!Igual(ap, guardado?.ApPaterno) && !RxApellido.IsMatch(ap))
                throw new ErrorNegocioException($"Apellido paterno inválido: solo letras y espacios (2 a {LargoApellido} caracteres).");
            if (am.Length > 0 && !Igual(am, guardado?.ApMaterno) && !RxApellido.IsMatch(am))
                throw new ErrorNegocioException($"Apellido materno inválido: solo letras y espacios (2 a {LargoApellido} caracteres).");
            if (tel.Length > 0 && !Igual(tel, guardado?.Telefono) && !RxTelefono.IsMatch(tel))
                throw new ErrorNegocioException("Teléfono inválido: ingrese entre 7 y 9 dígitos.");

            return new DatosConductor
            {
                Nombre = n,
                ApPaterno = ap,
                ApMaterno = am.Length > 0 ? am : null,
                Telefono = tel.Length > 0 ? tel : null
            };
        }

        /// <summary>
        /// Mensaje de duplicado si otro conductor (distinto de <paramref name="idPropio"/>, activo o no) tiene el
        /// mismo DNI o carnet una vez normalizados ambos; null si está libre.
        /// </summary>
        public static string BuscarDuplicado(string dni, string carnet, IEnumerable<DocumentoGuardado> guardados, int? idPropio = null)
        {
            foreach (var g in guardados.Where(x => idPropio == null || x.IdConductor != idPropio.Value))
            {
                if (dni != null && DocumentoConductor.NormalizarDni(g.Dni) == dni)
                    return $"Ya existe un conductor con el DNI {dni}: {Nombre(g)}.";
                if (carnet != null && DocumentoConductor.NormalizarCarnet(g.Carnet) == carnet)
                {
                    string comoEsta = g.Carnet.Trim().ToUpperInvariant();
                    return $"Ya existe un conductor con el carnet de extranjería {carnet}" +
                           (comoEsta != carnet ? $" (registrado como {comoEsta})" : "") + $": {Nombre(g)}.";
                }
            }
            return null;
        }

        private static string Nombre(DocumentoGuardado g) =>
            string.IsNullOrWhiteSpace(g.NombreCompleto) ? $"conductor #{g.IdConductor}" : g.NombreCompleto.Trim();

        private static string Limpiar(string texto) => Regex.Replace((texto ?? "").Trim(), @"\s{2,}", " ");

        // ── Base de datos ────────────────────────────────────────────────

        /// <summary>Documentos de todos los conductores (activos e inactivos).</summary>
        public static List<DocumentoGuardado> ObtenerDocumentos()
        {
            DataTable dt = DbHelper.ConsultarTabla(
                @"SELECT idConductor, DNI, carnetExtranjeria,
                         LTRIM(RTRIM(ISNULL(nombre, '') + ' ' + ISNULL(apPaterno, '') + ' ' + ISNULL(apMaterno, ''))) AS nombreCompleto
                    FROM Conductor
                   WHERE DNI IS NOT NULL OR carnetExtranjeria IS NOT NULL");
            return dt.AsEnumerable().Select(r => new DocumentoGuardado
            {
                IdConductor = r.Field<int>("idConductor"),
                Dni = r["DNI"] as string,
                Carnet = r["carnetExtranjeria"] as string,
                NombreCompleto = r["nombreCompleto"] as string
            }).ToList();
        }

        /// <summary>Registra un conductor activo. Devuelve su id.</summary>
        public static int Registrar(string tipoDocumento, string numeroDocumento,
            string nombre, string apPaterno, string apMaterno, string telefono)
        {
            ValidarDocumento(tipoDocumento, numeroDocumento, out string dni, out string carnet);
            var d = ValidarPersona(nombre, apPaterno, apMaterno, telefono);
            d.Dni = dni;
            d.Carnet = carnet;

            string duplicado = BuscarDuplicado(dni, carnet, ObtenerDocumentos());
            if (duplicado != null)
                throw new ErrorNegocioException(duplicado);

            int id;
            try
            {
                // SCOPE_IDENTITY y no OUTPUT: la tabla tiene trigger de auditoría (TR_Conductor_Auditoria)
                id = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    @"INSERT INTO Conductor (DNI, nombre, apPaterno, apMaterno, telefono, carnetExtranjeria, activo)
                      VALUES (@DNI, @nombre, @apPaterno, @apMaterno, @telefono, @carnetExtranjeria, 1);
                      SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    DbHelper.Param("@DNI", d.Dni),
                    DbHelper.Param("@nombre", d.Nombre),
                    DbHelper.Param("@apPaterno", d.ApPaterno),
                    DbHelper.Param("@apMaterno", d.ApMaterno),
                    DbHelper.Param("@telefono", d.Telefono),
                    DbHelper.Param("@carnetExtranjeria", d.Carnet)));
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                // UC_DNI / UC_CarnetExtranjeria: otro usuario lo registró al mismo tiempo
                throw new ErrorNegocioException("Ya existe un conductor con ese documento.", ex);
            }

            AuditoriaHelper.Registrar("INSERT", "Conductor", id,
                $"Conductor registrado - Nombre: {d.Nombre} {d.ApPaterno}, Doc: {(d.Dni != null ? "DNI " + d.Dni : "CE " + d.Carnet)}");
            return id;
        }

        /// <summary>Cambia nombres, apellidos y teléfono (el documento no se edita en la pantalla).</summary>
        public static void Actualizar(int idConductor, string nombre, string apPaterno, string apMaterno, string telefono)
        {
            DataTable actual = DbHelper.ConsultarTabla(
                "SELECT nombre, apPaterno, apMaterno, telefono FROM Conductor WHERE idConductor = @id",
                DbHelper.Param("@id", idConductor));
            if (actual.Rows.Count == 0)
                throw new ErrorNegocioException("El conductor ya no existe; recargue la página.");

            var anterior = new DatosConductor
            {
                Nombre = actual.Rows[0]["nombre"] as string,
                ApPaterno = actual.Rows[0]["apPaterno"] as string,
                ApMaterno = actual.Rows[0]["apMaterno"] as string,
                Telefono = actual.Rows[0]["telefono"] as string
            };
            var d = ValidarPersona(nombre, apPaterno, apMaterno, telefono, anterior);

            DbHelper.EjecutarNonQuery(
                "UPDATE Conductor SET nombre = @nombre, apPaterno = @apPaterno, apMaterno = @apMaterno, telefono = @telefono WHERE idConductor = @id",
                DbHelper.Param("@nombre", d.Nombre),
                DbHelper.Param("@apPaterno", d.ApPaterno),
                DbHelper.Param("@apMaterno", d.ApMaterno),
                DbHelper.Param("@telefono", d.Telefono),
                DbHelper.Param("@id", idConductor));

            AuditoriaHelper.Registrar("UPDATE", "Conductor", idConductor,
                $"Conductor editado — {d.Nombre} {d.ApPaterno} {d.ApMaterno}".TrimEnd(),
                valoresAnteriores: Describir(anterior),
                valoresNuevos: Describir(d));
        }

        private static string Describir(DatosConductor c) =>
            $"Nombre: {c.Nombre ?? "(vacío)"}, Ap. paterno: {c.ApPaterno ?? "(vacío)"}, " +
            $"Ap. materno: {c.ApMaterno ?? "(vacío)"}, Teléfono: {c.Telefono ?? "(vacío)"}";
    }
}
