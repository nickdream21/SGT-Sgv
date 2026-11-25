using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using WebSGV.Services.Common;

namespace WebSGV.Services.Catalogos
{
    /// <summary>
    /// Alta, edición y grilla del catálogo de plantas (<c>RegistroPlantas.aspx</c>, E6-19). Valida, escribe y
    /// audita aquí; los errores para el usuario salen como <see cref="ErrorNegocioException"/> con los mismos textos
    /// que tenía la página. El nombre se admite una vez como nacional y una como internacional (DN-35, U-101): lo
    /// fija el índice único <c>UX_Planta_Nombre_Ambito</c> (migración 64; collation CI: "Planta X" = "PLANTA X"),
    /// que también protege ante dos usuarios a la vez. El nombre se guarda en mayúsculas y sin espacios a los lados.
    /// </summary>
    public static class PlantaService
    {
        /// <summary>Largo de <c>Planta.nombre</c> (varchar(200)).</summary>
        public const int LargoNombre = 200;

        /// <summary>Índice único de la migración 64.</summary>
        public const string IndiceNombreAmbito = "UX_Planta_Nombre_Ambito";

        private static readonly Regex RxNombre =
            new Regex("^[A-ZÁÉÍÓÚÑ0-9\\s\\-\\.,]{3," + LargoNombre + "}$", RegexOptions.Compiled);

        public const string MensajeNombreObligatorio = "Debe ingresar el nombre de la planta.";
        public const string MensajeNombreInvalido = "Nombre de planta inválido: use entre 3 y 200 caracteres válidos.";
        public const string MensajeDuplicadoAlta = "Ya existe una planta con ese nombre en el mismo ámbito.";
        public const string MensajeDuplicadoEdicion = "Ya existe otra planta con ese nombre en el mismo ámbito.";

        /// <summary>Nombre como se guarda: sin espacios a los lados y en mayúsculas (igual que la página antes de E6-19).</summary>
        public static string Normalizar(string nombre) => (nombre ?? "").Trim().ToUpper(CultureInfo.CurrentCulture);

        /// <summary>
        /// Reglas del alta (lógica pura): nombre obligatorio y con formato. La página la llama antes de consumir el
        /// token de envío único (E5-3), para que un nombre inválido diga lo que pasa y no "Ya se registró".
        /// </summary>
        public static string ValidarAlta(string nombre)
        {
            string n = Normalizar(nombre);
            if (n.Length == 0)
                throw new ErrorNegocioException(MensajeNombreObligatorio);
            if (!RxNombre.IsMatch(n))
                throw new ErrorNegocioException(MensajeNombreInvalido);
            return n;
        }

        /// <summary>Grilla de la página: todas las plantas, nacionales primero. Catálogo (no crece con el tiempo).</summary>
        public static DataTable ObtenerGrilla() =>
            DbHelper.ConsultarTabla(
                "SELECT idPlanta, nombre, esInternacional, activo FROM Planta ORDER BY esInternacional, nombre");

        /// <summary>Registra una planta activa y la audita. Devuelve su id.</summary>
        public static int Registrar(string nombre, bool esInternacional)
        {
            string n = ValidarAlta(nombre);

            int idPlanta;
            try
            {
                idPlanta = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "INSERT INTO Planta (nombre, esInternacional, activo) VALUES (@nombre, @esInternacional, 1); " +
                    "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    DbHelper.Param("@nombre", n),
                    DbHelper.Param("@esInternacional", esInternacional)));
            }
            catch (SqlException ex) when (EsClaveDuplicada(ex, IndiceNombreAmbito))
            {
                throw new ErrorNegocioException(MensajeDuplicadoAlta, ex);
            }

            AuditoriaHelper.Registrar("INSERT", "Planta", idPlanta,
                $"Planta registrada - Nombre: {n}, Ámbito: {Ambito(esInternacional)}");
            return idPlanta;
        }

        /// <summary>
        /// Cambia nombre y ámbito y lo audita. Como antes de E6-19, la edición solo exige el nombre (los
        /// validadores del modal ya limitan el formato); editar sin cambiar nada no choca consigo misma.
        /// </summary>
        public static void Actualizar(int idPlanta, string nombre, bool esInternacional)
        {
            string n = Normalizar(nombre);
            if (n.Length == 0)
                throw new ErrorNegocioException(MensajeNombreObligatorio);

            try
            {
                DbHelper.EjecutarNonQuery(
                    "UPDATE Planta SET nombre=@nombre, esInternacional=@esInt WHERE idPlanta=@id",
                    DbHelper.Param("@nombre", n),
                    DbHelper.Param("@esInt", esInternacional),
                    DbHelper.Param("@id", idPlanta));
            }
            catch (SqlException ex) when (EsClaveDuplicada(ex, IndiceNombreAmbito))
            {
                throw new ErrorNegocioException(MensajeDuplicadoEdicion, ex);
            }

            AuditoriaHelper.Registrar("UPDATE", "Planta", idPlanta,
                $"Planta editada — Nombre:{n}, Ámbito:{Ambito(esInternacional)}");
        }

        private static string Ambito(bool esInternacional) => esInternacional ? "Internacional" : "Nacional";

        /// <summary>
        /// Violación de clave única (2601/2627) del índice <paramref name="indice"/>: solo esa es "nombre repetido";
        /// la de otro índice sigue como error técnico (se relanza tal cual).
        /// </summary>
        internal static bool EsClaveDuplicada(SqlException ex, string indice) =>
            (ex.Number == 2601 || ex.Number == 2627) && ex.Message.IndexOf(indice, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
