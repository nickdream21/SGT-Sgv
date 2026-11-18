using System;
using System.Data;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Aplica <see cref="PoliticaContrasena"/> con los datos de la persona tomados de la BD
    /// (usuario, nombre y, si es conductor, sus nombres, apellidos y DNI o carné de extranjería).
    /// Todo sitio que guarda una contraseña nueva pasa por aquí (lo comprueba
    /// <c>PoliticaContrasenaTests.CadaSitioQueHasheaUnaContrasenaNueva_AplicaLaPolitica</c>).
    /// Los errores de BD se propagan: quien llama decide.
    /// </summary>
    public static class ContrasenaNuevaHelper
    {
        /// <summary>Contraseña nueva de una cuenta que ya existe. Un idUsuario inexistente valida sin datos personales.</summary>
        public static string ValidarParaUsuario(string nueva, int idUsuario)
        {
            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT u.nombreUsuario, u.nombre,
                       c.nombre AS nombreConductor, c.apPaterno, c.apMaterno,
                       COALESCE(NULLIF(LTRIM(RTRIM(c.DNI)), ''), NULLIF(LTRIM(RTRIM(c.carnetExtranjeria)), '')) AS documento
                FROM Usuarios u
                LEFT JOIN Conductor c ON c.idConductor = u.idConductor
                WHERE u.idUsuario = @id",
                DbHelper.Param("@id", idUsuario));

            if (dt.Rows.Count == 0)
                return PoliticaContrasena.Validar(nueva, null, null);

            DataRow r = dt.Rows[0];
            return PoliticaContrasena.Validar(nueva,
                Texto(r["nombreUsuario"]),
                NombreCompleto(Texto(r["nombre"]), r),
                Texto(r["documento"]));
        }

        /// <summary>Contraseña de un usuario que se está creando (todavía sin idUsuario).</summary>
        public static string ValidarParaNuevoUsuario(string nueva, string nombreUsuario, string nombre, int? idConductor)
        {
            if (!idConductor.HasValue)
                return PoliticaContrasena.Validar(nueva, nombreUsuario, nombre);

            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT c.nombre AS nombreConductor, c.apPaterno, c.apMaterno,
                       COALESCE(NULLIF(LTRIM(RTRIM(c.DNI)), ''), NULLIF(LTRIM(RTRIM(c.carnetExtranjeria)), '')) AS documento
                FROM Conductor c
                WHERE c.idConductor = @id",
                DbHelper.Param("@id", idConductor.Value));

            if (dt.Rows.Count == 0)
                return PoliticaContrasena.Validar(nueva, nombreUsuario, nombre);

            DataRow r = dt.Rows[0];
            return PoliticaContrasena.Validar(nueva, nombreUsuario, NombreCompleto(nombre, r), Texto(r["documento"]));
        }

        private static string NombreCompleto(string nombre, DataRow conductor)
        {
            return string.Join(" ", nombre,
                Texto(conductor["nombreConductor"]), Texto(conductor["apPaterno"]), Texto(conductor["apMaterno"]));
        }

        private static string Texto(object valor)
        {
            return valor == null || valor == DBNull.Value ? null : Convert.ToString(valor).Trim();
        }
    }
}
