using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Common;

namespace WebSGV.Services.Usuarios
{
    /// <summary>Un usuario tal como lo lee Gestión de usuarios (<see cref="GestionUsuariosService.Leer"/>).</summary>
    public sealed class UsuarioGestion
    {
        public int IdUsuario { get; internal set; }
        /// <summary>Como está en la BD (sin recortar): la página recorta donde lo mostraba recortado.</summary>
        public string NombreUsuario { get; internal set; }
        /// <summary>"" si la columna es NULL.</summary>
        public string Nombre { get; internal set; }
        /// <summary>Como está en la BD (sin recortar ni pasar a mayúsculas).</summary>
        public string Rol { get; internal set; }
        public int? IdConductor { get; internal set; }
        public bool Activo { get; internal set; }
    }

    /// <summary>
    /// Lo que Gestión de usuarios (administrador de sistema) lee y crea (E6-17): la lista con sus filtros y totales,
    /// los conductores para vincular, los roles en uso, la lectura de un usuario por id (una sola, en lugar de las
    /// seis consultas que tenía la página) y el alta. Activar, desactivar y editar siguen en
    /// <see cref="UsuarioEstadoService"/> (guarda del último administrador de sistema y sello de sesión) y las
    /// contraseñas en <see cref="ContrasenaService"/>. La página conserva sus guardas (rol, fila propia,
    /// reautenticación del administrador, formato del nombre de usuario, rol vigente) y sus mensajes.
    /// </summary>
    public static class GestionUsuariosService
    {
        public const string MensajeConductorAsociado = "El conductor seleccionado ya está asociado a otro usuario.";
        public const string MensajeNombreRepetido = "El nombre de usuario ya existe. Elige otro.";

        // ── Lectura de un usuario ───────────────────────────────────────────────

        /// <summary>El usuario <paramref name="idUsuario"/>, o null si no existe. Los errores de BD se propagan.</summary>
        public static UsuarioGestion Leer(int idUsuario)
        {
            DataTable dt = DbHelper.ConsultarTabla(
                "SELECT idUsuario, nombreUsuario, nombre, rol, idConductor, activo FROM Usuarios WHERE idUsuario = @id",
                DbHelper.Param("@id", idUsuario));
            if (dt.Rows.Count == 0)
                return null;

            DataRow r = dt.Rows[0];
            return new UsuarioGestion
            {
                IdUsuario = Convert.ToInt32(r["idUsuario"], CultureInfo.InvariantCulture),
                NombreUsuario = Convert.ToString(r["nombreUsuario"], CultureInfo.InvariantCulture),
                Nombre = Convert.ToString(r["nombre"], CultureInfo.InvariantCulture),
                Rol = Convert.ToString(r["rol"], CultureInfo.InvariantCulture),
                IdConductor = r["idConductor"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["idConductor"], CultureInfo.InvariantCulture),
                Activo = r["activo"] != DBNull.Value && Convert.ToBoolean(r["activo"], CultureInfo.InvariantCulture)
            };
        }

        // ── Listados ────────────────────────────────────────────────────────────

        /// <summary>
        /// Usuarios con los filtros ya validados por la página: <paramref name="buscar"/> recortado (en nombre de
        /// usuario o nombre), <paramref name="rol"/> de la lista o "" y <paramref name="estado"/> "", "0" o "1".
        /// Columnas: idUsuario, nombreUsuario, nombre, rol, activo y segundoFactor (1 si lo tiene activo, 0 si tiene
        /// un alta a medias, NULL si no tiene nada; sin la migración 62 y con el interruptor apagado, NULL para todos).
        /// </summary>
        public static DataTable Listar(string buscar, string rol, string estado)
        {
            var parametros = new List<SqlParameter>();
            string query = " FROM Usuarios WHERE 1=1";

            if (!string.IsNullOrEmpty(buscar))
            {
                query += " AND (nombreUsuario LIKE @Buscar OR nombre LIKE @Buscar)";
                parametros.Add(DbHelper.Param("@Buscar", DbHelper.PatronContiene(buscar)));
            }
            if (!string.IsNullOrEmpty(rol))
            {
                query += " AND rol = @Rol";
                parametros.Add(DbHelper.Param("@Rol", rol));
            }
            if (!string.IsNullOrEmpty(estado))
            {
                query += " AND activo = @Estado";
                parametros.Add(DbHelper.Param("@Estado", Convert.ToInt32(estado, CultureInfo.InvariantCulture)));
            }
            query += " ORDER BY activo DESC, rol, nombre";

            // Parámetros nuevos en cada intento: un SqlParameter no puede estar en dos comandos.
            return InicioSesionHelper.ConsultarConSegundoFactor(conTabla => DbHelper.ConsultarTabla(
                @"SELECT idUsuario, nombreUsuario, nombre, rol, activo, "
                    + (conTabla
                        ? "(SELECT CAST(f.activo AS INT) FROM dbo.UsuarioSegundoFactor f WHERE f.idUsuario = Usuarios.idUsuario)"
                        : "CAST(NULL AS INT)")
                    + " AS segundoFactor" + query,
                parametros.Select(p => DbHelper.Param(p.ParameterName, p.Value)).ToArray()));
        }

        /// <summary>Total de usuarios y cuántos están activos.</summary>
        public static (int Total, int Activos) Totales()
        {
            DataTable dt = DbHelper.ConsultarTabla(
                "SELECT COUNT(*) AS total, ISNULL(SUM(CASE WHEN activo = 1 THEN 1 ELSE 0 END), 0) AS activos FROM Usuarios");
            if (dt.Rows.Count == 0)
                return (0, 0);
            return (Convert.ToInt32(dt.Rows[0]["total"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(dt.Rows[0]["activos"], CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Conductores activos para vincular a un usuario: idConductor, descripcion ("documento - nombre apellidos")
        /// y nombreUsuario del usuario que ya lo tiene (NULL si ninguno).
        /// </summary>
        public static DataTable ConductoresParaVincular() =>
            DbHelper.ConsultarTabla(@"
                SELECT c.idConductor,
                       LTRIM(RTRIM(
                           COALESCE(NULLIF(c.DNI, ''), NULLIF(c.carnetExtranjeria, ''), 'Sin documento')
                           + ' - ' + ISNULL(c.nombre, '') + ' ' + ISNULL(c.apPaterno, '') + ' ' + ISNULL(c.apMaterno, '')
                       )) AS descripcion,
                       u.nombreUsuario
                FROM Conductor c
                LEFT JOIN Usuarios u ON u.idConductor = c.idConductor
                WHERE c.activo = 1
                ORDER BY c.apPaterno, c.apMaterno, c.nombre");

        /// <summary>Roles distintos (recortados, no vacíos) que tienen hoy los usuarios. Los errores de BD se propagan.</summary>
        public static List<string> RolesEnUso()
        {
            DataTable dt = DbHelper.ConsultarTabla(
                "SELECT DISTINCT LTRIM(RTRIM(rol)) AS rol FROM Usuarios WHERE rol IS NOT NULL AND LTRIM(RTRIM(rol)) <> ''");
            return dt.Rows.Cast<DataRow>().Select(r => r["rol"].ToString()).ToList();
        }

        // ── Alta ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Lanza <see cref="ErrorNegocioException"/> si el conductor ya está vinculado a un usuario distinto de
        /// <paramref name="idUsuario"/> (0 en un alta).
        /// </summary>
        public static void ValidarConductorLibre(int idConductor, int idUsuario)
        {
            int asociado = Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                SELECT COUNT(*)
                FROM Usuarios
                WHERE idConductor = @idConductor
                  AND idUsuario <> @idUsuario",
                DbHelper.Param("@idConductor", idConductor),
                DbHelper.Param("@idUsuario", idUsuario)), CultureInfo.InvariantCulture);
            if (asociado > 0)
                throw new ErrorNegocioException(MensajeConductorAsociado);
        }

        /// <summary>Lanza <see cref="ErrorNegocioException"/> si ya hay un usuario con ese nombre de usuario.</summary>
        public static void ValidarNombreLibre(string nombreUsuario)
        {
            int existe = Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM Usuarios WHERE nombreUsuario = @user",
                DbHelper.Param("@user", nombreUsuario)), CultureInfo.InvariantCulture);
            if (existe > 0)
                throw new ErrorNegocioException(MensajeNombreRepetido);
        }

        /// <summary>
        /// Crea un usuario activo con "debe cambiar contraseña" y audita el alta (sin la contraseña ni su hash).
        /// La página ya validó campos, formato del nombre de usuario, rol vigente y la reautenticación del
        /// administrador, y antes de pedirla mostró la política y los dos controles de abajo; aquí se repiten para
        /// que ningún camino guarde un usuario sin ellos: política de contraseña, conductor libre y nombre libre
        /// (<see cref="ErrorNegocioException"/> con el mensaje de cada uno). Devuelve el idUsuario nuevo.
        /// </summary>
        public static int Crear(string nombreUsuario, string nombre, string rol, string contrasena, int? idConductor)
        {
            string errorContrasena = ContrasenaNuevaHelper.ValidarParaNuevoUsuario(contrasena, nombreUsuario, nombre, idConductor);
            if (errorContrasena != null)
                throw new ErrorNegocioException(errorContrasena);
            if (idConductor.HasValue)
                ValidarConductorLibre(idConductor.Value, 0);
            ValidarNombreLibre(nombreUsuario);

            string hash = PasswordHelper.HashPassword(contrasena);
            int idNuevo = Convert.ToInt32(DbHelper.EjecutarEscalar(
                @"INSERT INTO Usuarios
                    (nombreUsuario, nombre, contrasena, rol, activo, idConductor, requiereCambioContrasena)
                  OUTPUT INSERTED.idUsuario
                  VALUES
                    (@user, @nombre, @pass, @rol, 1, @idConductor, 1)",
                DbHelper.Param("@user", nombreUsuario),
                DbHelper.Param("@nombre", nombre),
                DbHelper.Param("@pass", hash),
                DbHelper.Param("@rol", rol),
                DbHelper.Param("@idConductor", idConductor.HasValue ? (object)idConductor.Value : DBNull.Value)),
                CultureInfo.InvariantCulture);

            AuditoriaHelper.Registrar("INSERT", "Usuarios", idNuevo, $"Nuevo usuario creado: {nombreUsuario}, rol: {rol}",
                null, JsonUsuarioNuevo(nombreUsuario, nombre, rol, idConductor));
            return idNuevo;
        }

        /// <summary>
        /// Valores nuevos que se auditan al crear un usuario. Nunca incluye la contraseña ni su hash.
        /// </summary>
        internal static string JsonUsuarioNuevo(string nombreUsuario, string nombre, string rol, int? idConductor) =>
            Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                nombreUsuario,
                nombre,
                rol,
                idConductor,
                requiereCambioContrasena = 1
            });
    }
}
