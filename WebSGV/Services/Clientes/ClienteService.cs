using System;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using WebSGV.Services.Common;

namespace WebSGV.Services.Clientes
{
    /// <summary>
    /// Alta, edición y grilla del catálogo de clientes (<c>RegistroClientes.aspx</c>, E6-19). Valida, escribe y
    /// audita aquí; los errores para el usuario salen como <see cref="ErrorNegocioException"/> con los mismos textos
    /// que tenía la página. El RUC repetido lo detecta el índice único filtrado <c>UQ_Cliente_Ruc</c>
    /// (<c>ruc IS NOT NULL</c>, comparación exacta del RUC ya recortado): así también protege ante dos usuarios a
    /// la vez. Un campo opcional vacío se guarda como NULL, nunca como cadena vacía (varias "" chocarían en el índice).
    /// </summary>
    public static class ClienteService
    {
        public const int LargoNombre = LargoColumnas.ClienteNombre;

        /// <summary>Índice único filtrado del RUC (migraciones 14 y 64).</summary>
        public const string IndiceRuc = "UQ_Cliente_Ruc";

        private static readonly Regex RxNombre =
            new Regex("^[A-Za-zÁÉÍÓÚÑáéíóú0-9\\s\\-\\.&]{3," + LargoNombre + "}$", RegexOptions.Compiled);

        public static readonly string MensajeNombreInvalido =
            $"Nombre/Razón Social inválido: use entre 3 y {LargoNombre} letras, números, espacios, guion, punto o &.";

        public const string MensajeNombreObligatorio = "El nombre del cliente es obligatorio.";
        public const string MensajeRucInvalido = "El RUC debe contener 11 dígitos numéricos.";
        public const string MensajeRucDuplicadoAlta = "Ya existe un cliente registrado con ese RUC.";
        public const string MensajeRucDuplicadoEdicion = "Ya existe otro cliente registrado con ese RUC.";

        /// <summary>Lo que se escribe en el formulario (alta o edición), tal cual; el servicio recorta.</summary>
        public sealed class DatosCliente
        {
            public string Ruc { get; set; }
            public string Nombre { get; set; }
            public string Direccion { get; set; }
            public string Contacto { get; set; }
            public string Telefono { get; set; }
            public string Correo { get; set; }
            public string Moneda { get; set; }
            public bool EsExportador { get; set; }
            public string Observaciones { get; set; }
        }

        /// <summary>
        /// Grilla de la página: todos los clientes (activos primero) con los opcionales en "" para mostrarlos.
        /// Catálogo: no crece con el tiempo (la página filtra y pagina en memoria).
        /// </summary>
        public static DataTable ObtenerGrilla() =>
            DbHelper.ConsultarTabla(
                @"SELECT idCliente,
                         ISNULL(ruc, '')               AS ruc,
                         nombre,
                         ISNULL(direccion, '')         AS direccion,
                         ISNULL(contacto, '')          AS contacto,
                         ISNULL(telefono, '')          AS telefono,
                         ISNULL(correo, '')            AS correo,
                         ISNULL(observaciones, '')     AS observaciones,
                         monedaFacturacion,
                         esExportador,
                         activo
                    FROM Cliente
                ORDER BY activo DESC, nombre");

        /// <summary>
        /// Nombre y RUC obligatorio/opcional (lógica pura). El RUC, si viene, son 11 dígitos. En la edición
        /// (<paramref name="nombreGuardado"/> = el nombre guardado) un nombre antiguo que no cambia se conserva aunque no
        /// cumpla el formato; en el alta el formato se exige siempre.
        /// </summary>
        public static void Validar(string ruc, string nombre, string nombreGuardado = null)
        {
            string r = (ruc ?? "").Trim();
            string n = (nombre ?? "").Trim();
            if (n.Length == 0)
                throw new ErrorNegocioException(MensajeNombreObligatorio);
            if (r.Length > 0 && (r.Length != 11 || !SoloDigitos(r)))
                throw new ErrorNegocioException(MensajeRucInvalido);
            bool nombreIgual = nombreGuardado != null && n == nombreGuardado.Trim();
            if (!nombreIgual && !RxNombre.IsMatch(n))
                throw new ErrorNegocioException(MensajeNombreInvalido);
        }

        /// <summary>
        /// Registra un cliente activo y lo audita. <paramref name="usuarioRegistro"/> es el nombre de quien lo
        /// registra ("SISTEMA" si no hay). Devuelve su id.
        /// </summary>
        public static int Registrar(DatosCliente d, string usuarioRegistro)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            Validar(d.Ruc, d.Nombre);
            string ruc = (d.Ruc ?? "").Trim();
            string nombre = d.Nombre.Trim();

            int idCliente;
            try
            {
                idCliente = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    // SCOPE_IDENTITY y no OUTPUT: la tabla puede tener trigger de auditoría
                    @"INSERT INTO Cliente
                          (ruc, nombre, direccion, contacto, telefono, correo,
                           monedaFacturacion, esExportador, observaciones,
                           usuarioRegistro, activo)
                      VALUES
                          (@ruc, @nombre, @direccion, @contacto, @telefono, @correo,
                           @moneda, @esExportador, @observaciones,
                           @usuarioRegistro, 1);
                      SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    DbHelper.Param("@ruc", TextoONull(ruc)),
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@direccion", TextoONull(d.Direccion)),
                    DbHelper.Param("@contacto", TextoONull(d.Contacto)),
                    DbHelper.Param("@telefono", TextoONull(d.Telefono)),
                    DbHelper.Param("@correo", TextoONull(d.Correo)),
                    DbHelper.Param("@moneda", d.Moneda),
                    DbHelper.Param("@esExportador", d.EsExportador),
                    DbHelper.Param("@observaciones", TextoONull(d.Observaciones)),
                    DbHelper.Param("@usuarioRegistro", usuarioRegistro)));
            }
            catch (SqlException ex) when (EsClaveDuplicada(ex, IndiceRuc))
            {
                throw new ErrorNegocioException(MensajeRucDuplicadoAlta, ex);
            }

            AuditoriaHelper.Registrar("INSERT", "Cliente", idCliente,
                $"Cliente registrado - Nombre: {nombre}, " +
                $"RUC: {(ruc.Length == 0 ? "Sin RUC" : ruc)}, " +
                $"Moneda: {d.Moneda}, " +
                $"Exportador: {(d.EsExportador ? "Si" : "No")}");
            return idCliente;
        }

        /// <summary>Cambia los datos del cliente <paramref name="idCliente"/> y lo audita.</summary>
        public static void Actualizar(int idCliente, DatosCliente d)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            // Mismas reglas que el alta; un nombre antiguo que no cambia se conserva aunque no las cumpla.
            string nombreAnterior = DbHelper.EjecutarEscalar(
                "SELECT nombre FROM Cliente WHERE idCliente = @id", DbHelper.Param("@id", idCliente)) as string;
            Validar(d.Ruc, d.Nombre, nombreAnterior);
            string ruc = (d.Ruc ?? "").Trim();
            string nombre = d.Nombre.Trim();

            try
            {
                DbHelper.EjecutarNonQuery(
                    @"UPDATE Cliente SET
                          ruc               = @ruc,
                          nombre            = @nombre,
                          direccion         = @direccion,
                          contacto          = @contacto,
                          telefono          = @telefono,
                          correo            = @correo,
                          monedaFacturacion = @moneda,
                          esExportador      = @esExportador,
                          observaciones     = @observaciones
                      WHERE idCliente = @id",
                    DbHelper.Param("@ruc", TextoONull(ruc)),
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@direccion", TextoONull(d.Direccion)),
                    DbHelper.Param("@contacto", TextoONull(d.Contacto)),
                    DbHelper.Param("@telefono", TextoONull(d.Telefono)),
                    DbHelper.Param("@correo", TextoONull(d.Correo)),
                    DbHelper.Param("@moneda", d.Moneda),
                    DbHelper.Param("@esExportador", d.EsExportador),
                    DbHelper.Param("@observaciones", TextoONull(d.Observaciones)),
                    DbHelper.Param("@id", idCliente));
            }
            catch (SqlException ex) when (EsClaveDuplicada(ex, IndiceRuc))
            {
                throw new ErrorNegocioException(MensajeRucDuplicadoEdicion, ex);
            }

            AuditoriaHelper.Registrar("UPDATE", "Cliente", idCliente,
                $"Cliente editado — Nombre:{nombre}, RUC:{(ruc.Length == 0 ? "Sin RUC" : ruc)}, " +
                $"Moneda:{d.Moneda}, Exportador:{(d.EsExportador ? "Si" : "No")}");
        }

        /// <summary>
        /// Aviso del botón "Verificar" (B5-6) si ya hay un cliente, activo o inactivo, con ese RUC: misma
        /// comparación que el índice único del alta (<c>ruc = @ruc</c> con el RUC recortado); null si está libre.
        /// </summary>
        public static string AvisoRucRegistrado(string ruc)
        {
            object nombre = DbHelper.EjecutarEscalar(
                "SELECT TOP 1 LTRIM(RTRIM(ISNULL(nombre, ''))) FROM Cliente WHERE ruc = @ruc ORDER BY idCliente",
                DbHelper.Param("@ruc", ruc));
            if (nombre == null || nombre == DBNull.Value) return null;
            return MensajeRucRegistrado(Convert.ToString(nombre));
        }

        public static string MensajeRucRegistrado(string nombreCliente)
        {
            string nombre = string.IsNullOrWhiteSpace(nombreCliente) ? "(sin nombre)" : nombreCliente.Trim();
            // "VITAPRO S.A." ya termina en punto: no se agrega otro
            return "Ya existe un cliente registrado con ese RUC: " + nombre + (nombre.EndsWith(".") ? "" : ".");
        }

        private static bool SoloDigitos(string texto)
        {
            foreach (char c in texto)
                if (!char.IsDigit(c)) return false;
            return true;
        }

        /// <summary>
        /// <c>DBNull</c> para un campo opcional vacío, en lugar de una cadena vacía: el índice único del RUC es
        /// filtrado por <c>ruc IS NOT NULL</c>, así que varias "" chocarían entre sí y varios NULL conviven.
        /// </summary>
        private static object TextoONull(string valor) =>
            string.IsNullOrWhiteSpace(valor) ? (object)DBNull.Value : valor.Trim();

        /// <summary>
        /// Violación de clave única (2601/2627) del índice <paramref name="indice"/>: solo esa es "nombre repetido";
        /// la de otro índice sigue como error técnico (se relanza tal cual).
        /// </summary>
        internal static bool EsClaveDuplicada(SqlException ex, string indice) =>
            (ex.Number == 2601 || ex.Number == 2627) && ex.Message.IndexOf(indice, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
