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
    /// Alta, edición y grilla del catálogo de estaciones de peaje (<c>RegistroPeajes.aspx</c>, E6-19). Valida,
    /// escribe y audita aquí; los errores para el usuario salen como <see cref="ErrorNegocioException"/> con los
    /// mismos textos que tenía la página. El nombre repetido lo rechaza el índice único
    /// <c>UK_EstacionesPeaje_Nombre</c> (collation CI: "Peaje x" = "PEAJE X"), que también protege ante dos
    /// usuarios a la vez. El nombre se guarda en mayúsculas y sin espacios a los lados.
    /// </summary>
    public static class EstacionPeajeService
    {
        public const int LargoNombre = LargoColumnas.EstacionPeajeNombre;

        /// <summary>Restricción única del nombre (migración 64 la crea si falta).</summary>
        public const string IndiceNombre = "UK_EstacionesPeaje_Nombre";

        private static readonly Regex RxNombre =
            new Regex("^[A-ZÁÉÍÓÚÑ0-9\\s\\-\\.,]{3," + LargoNombre + "}$", RegexOptions.Compiled);

        public static readonly string MensajeNombreInvalido =
            $"Nombre de estación inválido: use entre 3 y {LargoNombre} letras, números, espacios, guion, punto o coma.";

        public const string MensajeNombreObligatorioAlta = "Debe ingresar el nombre de la estación de peaje.";
        public const string MensajeNombreObligatorioEdicion = "Debe ingresar el nombre de la estación.";
        public const string MensajeDuplicadoAlta = "Ya existe una estación de peaje con ese nombre.";
        public const string MensajeDuplicadoEdicion = "Ya existe otra estación de peaje con ese nombre.";
        public const string MensajeNoExiste = "La estación ya no existe; recargue la página.";

        /// <summary>Nombre como se guarda: sin espacios a los lados y en mayúsculas.</summary>
        public static string Normalizar(string nombre) => (nombre ?? "").Trim().ToUpper(CultureInfo.CurrentCulture);

        /// <summary>Grilla de la página: todas las estaciones por nombre. Catálogo (no crece con el tiempo).</summary>
        public static DataTable ObtenerGrilla() =>
            DbHelper.ConsultarTabla("SELECT idEstacion, nombre, activo FROM EstacionesPeaje ORDER BY nombre");

        /// <summary>Registra una estación activa y la audita. Devuelve su id.</summary>
        public static int Registrar(string nombre)
        {
            string n = Normalizar(nombre);
            if (n.Length == 0)
                throw new ErrorNegocioException(MensajeNombreObligatorioAlta);
            if (!RxNombre.IsMatch(n))
                throw new ErrorNegocioException(MensajeNombreInvalido);

            int idEstacion;
            try
            {
                idEstacion = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "INSERT INTO EstacionesPeaje (nombre, activo) VALUES (@nombre, 1); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    DbHelper.Param("@nombre", n)));
            }
            catch (SqlException ex) when (EsClaveDuplicada(ex, IndiceNombre))
            {
                throw new ErrorNegocioException(MensajeDuplicadoAlta, ex);
            }

            AuditoriaHelper.Registrar("INSERT", "EstacionesPeaje", idEstacion,
                $"Estacion de peaje registrada - Nombre: {n}");
            return idEstacion;
        }

        /// <summary>
        /// Cambia el nombre y lo audita. Mismas reglas que el alta, salvo que un nombre antiguo que no cambia
        /// (p. ej. con "+") se conserva aunque no las cumpla.
        /// </summary>
        public static void Actualizar(int idEstacion, string nombre)
        {
            string n = Normalizar(nombre);
            if (n.Length == 0)
                throw new ErrorNegocioException(MensajeNombreObligatorioEdicion);

            string anterior = DbHelper.EjecutarEscalar(
                "SELECT nombre FROM EstacionesPeaje WHERE idEstacion = @id", DbHelper.Param("@id", idEstacion)) as string;
            if (anterior == null)
                throw new ErrorNegocioException(MensajeNoExiste);
            if (n != Normalizar(anterior) && !RxNombre.IsMatch(n))
                throw new ErrorNegocioException(MensajeNombreInvalido);

            try
            {
                DbHelper.EjecutarNonQuery(
                    "UPDATE EstacionesPeaje SET nombre=@nombre WHERE idEstacion=@id",
                    DbHelper.Param("@nombre", n),
                    DbHelper.Param("@id", idEstacion));
            }
            catch (SqlException ex) when (EsClaveDuplicada(ex, IndiceNombre))
            {
                throw new ErrorNegocioException(MensajeDuplicadoEdicion, ex);
            }

            AuditoriaHelper.Registrar("UPDATE", "EstacionesPeaje", idEstacion,
                $"Estación de peaje editada — Nombre:{n}");
        }

        /// <summary>
        /// Violación de clave única (2601/2627) del índice <paramref name="indice"/>: solo esa es "nombre repetido";
        /// la de otro índice sigue como error técnico (se relanza tal cual).
        /// </summary>
        internal static bool EsClaveDuplicada(SqlException ex, string indice) =>
            (ex.Number == 2601 || ex.Number == 2627) && ex.Message.IndexOf(indice, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
