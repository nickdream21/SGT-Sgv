using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using WebSGV.Helpers;

namespace WebSGV.Services.Auditoria
{
    /// <summary>Filtros de la pantalla Auditoría (los mismos para la grilla y el Excel).</summary>
    public sealed class FiltroAuditoria
    {
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public string Accion { get; set; }
        public string Tabla { get; set; }
        public string Usuario { get; set; }
    }

    /// <summary>Una página de la grilla: sus filas (sin los Valores*) y el total con los filtros.</summary>
    public sealed class PaginaAuditoria
    {
        public DataTable Filas { get; set; }
        public int Total { get; set; }
        /// <summary>Página que se devolvió (0 si la pedida ya no existe con los filtros actuales).</summary>
        public int IndicePagina { get; set; }
    }

    /// <summary>Cifras de la pantalla Auditoría (<see cref="ConsultaAuditoriaService.ObtenerResumen"/>).</summary>
    public sealed class ResumenAuditoria
    {
        public int Total { get; set; }
        public int Hoy { get; set; }
        public int Usuarios7Dias { get; set; }
        public int Tablas { get; set; }
    }

    /// <summary>Detalle de un registro, que la página pide al abrirlo ([WebMethod] ObtenerDetalle).</summary>
    public sealed class DetalleAuditoria
    {
        public bool Ok { get; set; }
        public string Mensaje { get; set; }
        public int Id { get; set; }
        public string Descripcion { get; set; }
        public string DireccionIP { get; set; }
        public string Navegador { get; set; }
        public string ValoresAnteriores { get; set; }
        public string ValoresNuevos { get; set; }
        public bool AnterioresRecortados { get; set; }
        public bool NuevosRecortados { get; set; }
        /// <summary>Aviso para el usuario si algún valor se recortó; null si llegó completo.</summary>
        public string AvisoRecorte { get; set; }

        public static DetalleAuditoria Error(string mensaje) => new DetalleAuditoria { Ok = false, Mensaje = mensaje };
    }

    /// <summary>
    /// Consultas de AuditoriaLog para la pantalla Auditoría (E5-8, M-103). La grilla se pagina en SQL
    /// (OFFSET/FETCH de <see cref="TamanoPagina"/>) y no trae ValoresAnteriores/ValoresNuevos: el detalle
    /// los pide al abrirse. El Excel conserva su consulta propia (<see cref="ConstruirConsultaExportacion"/>).
    /// </summary>
    public static class ConsultaAuditoriaService
    {
        public const int TamanoPagina = 25;
        /// <summary>Largo de la descripción en la grilla (el detalle la trae entera).</summary>
        public const int LargoDescripcionGrilla = 200;
        /// <summary>Tope de caracteres de cada valor (anteriores, nuevos, descripción) en el detalle.</summary>
        public const int MaximoCaracteresDetalle = 200000;   // 3 textos con escapes de 6 caracteres caben en maxJsonLength (4 194 304)

        /// <summary>
        /// Consulta de una página de la grilla: sin los Valores*, la descripción recortada, el total con
        /// <c>COUNT(*) OVER()</c> y un orden estable (fecha e id descendentes). Agrega los parámetros.
        /// </summary>
        internal static string ConstruirConsultaPagina(FiltroAuditoria filtro, int indicePagina, List<SqlParameter> parametros)
        {
            var sb = new StringBuilder(@"
                    SELECT IdAuditoria, FechaHora, NombreUsuario, RolUsuario, Accion, TablaAfectada,
                           IdRegistroAfectado, LEFT(Descripcion, @largoDesc) AS Descripcion,
                           COUNT(*) OVER() AS TotalFilas
                    FROM AuditoriaLog
                    WHERE 1=1");
            parametros.Add(DbHelper.Param("@largoDesc", LargoDescripcionGrilla));
            AgregarFiltros(sb, filtro, parametros);
            sb.Append(" ORDER BY FechaHora DESC, IdAuditoria DESC OFFSET @salto ROWS FETCH NEXT @tamano ROWS ONLY");
            parametros.Add(DbHelper.Param("@salto", Math.Max(0, indicePagina) * TamanoPagina));
            parametros.Add(DbHelper.Param("@tamano", TamanoPagina));
            return sb.ToString();
        }

        /// <summary>
        /// Consulta del Excel (la de siempre): los <paramref name="maximo"/> registros más recientes con los
        /// filtros (TOP (@max), FechaHora descendente). Agrega los parámetros.
        /// </summary>
        internal static string ConstruirConsultaExportacion(FiltroAuditoria filtro, int maximo, List<SqlParameter> parametros)
        {
            var sb = new StringBuilder(@"
                    SELECT TOP (@max) IdAuditoria, FechaHora, IdUsuario, NombreUsuario, RolUsuario, Accion,
                           TablaAfectada, IdRegistroAfectado, Descripcion,
                           ValoresAnteriores, ValoresNuevos, DireccionIP, Navegador
                    FROM AuditoriaLog
                    WHERE 1=1");
            parametros.Add(DbHelper.Param("@max", maximo));
            AgregarFiltros(sb, filtro, parametros);
            sb.Append(" ORDER BY FechaHora DESC, IdAuditoria DESC");
            return sb.ToString();
        }

        private static void AgregarFiltros(StringBuilder sb, FiltroAuditoria f, List<SqlParameter> parametros)
        {
            if (f == null) return;
            if (f.Desde.HasValue)
            {
                sb.Append(" AND FechaHora >= @FechaDesde");
                parametros.Add(DbHelper.Param("@FechaDesde", f.Desde.Value));
            }
            if (f.Hasta.HasValue)
            {
                sb.Append(" AND FechaHora < DATEADD(DAY, 1, @FechaHasta)");
                parametros.Add(DbHelper.Param("@FechaHasta", f.Hasta.Value));
            }
            if (!string.IsNullOrEmpty(f.Accion))
            {
                sb.Append(" AND Accion = @Accion");
                parametros.Add(DbHelper.Param("@Accion", f.Accion));
            }
            if (!string.IsNullOrEmpty(f.Tabla))
            {
                sb.Append(" AND TablaAfectada = @Tabla");
                parametros.Add(DbHelper.Param("@Tabla", f.Tabla));
            }
            if (!string.IsNullOrEmpty(f.Usuario))
            {
                sb.Append(" AND NombreUsuario LIKE @Usuario");
                parametros.Add(DbHelper.Param("@Usuario", DbHelper.PatronContiene(f.Usuario)));
            }
        }

        /// <summary>
        /// Página <paramref name="indicePagina"/> (desde 0) de la grilla. Si con los filtros actuales esa
        /// página ya no existe (p. ej. el filtro achicó el resultado), devuelve la primera.
        /// </summary>
        public static PaginaAuditoria ObtenerPagina(FiltroAuditoria filtro, int indicePagina)
        {
            indicePagina = Math.Max(0, indicePagina);
            DataTable dt = ConsultarPagina(filtro, indicePagina);
            if (dt.Rows.Count == 0 && indicePagina > 0)
            {
                indicePagina = 0;
                dt = ConsultarPagina(filtro, 0);
            }
            int total = dt.Rows.Count == 0 ? 0 : Convert.ToInt32(dt.Rows[0]["TotalFilas"]);
            return new PaginaAuditoria { Filas = dt, Total = total, IndicePagina = indicePagina };
        }

        private static DataTable ConsultarPagina(FiltroAuditoria filtro, int indicePagina)
        {
            var parametros = new List<SqlParameter>();
            string sql = ConstruirConsultaPagina(filtro, indicePagina, parametros);
            return DbHelper.ConsultarTabla(sql, parametros.ToArray());
        }

        /// <summary>
        /// Códigos de acción distintos que hay en AuditoriaLog, en orden alfabético, para el filtro Acción (M-135:
        /// el filtro fijo de 11 opciones no dejaba buscar las otras, p. ej. FIRMAR o SEGUNDO_FACTOR_QUITADO).
        /// </summary>
        public static List<string> ObtenerAcciones()
        {
            DataTable dt = DbHelper.ConsultarTabla(
                "SELECT DISTINCT Accion FROM AuditoriaLog WHERE Accion IS NOT NULL AND Accion <> '' ORDER BY Accion");
            var acciones = new List<string>(dt.Rows.Count);
            foreach (DataRow fila in dt.Rows)
                acciones.Add(Convert.ToString(fila["Accion"]).Trim());
            return acciones;
        }

        /// <summary>
        /// Nombre en español de un código de acción para el filtro y la grilla. Las acciones genéricas (las del filtro
        /// antiguo) tienen nombre; las demás ya se leen por su código (FIRMAR, DESCARGAR_PDF) y salen tal cual.
        /// </summary>
        public static string NombreAccion(object codigo)
        {
            string c = codigo == null || codigo == DBNull.Value ? string.Empty : codigo.ToString().Trim();
            switch (c.ToUpperInvariant())
            {
                case "INSERT": return "Inserción";
                case "UPDATE": return "Actualización";
                case "DELETE": return "Eliminación";
                case "LOGIN": return "Inicio de sesión";
                case "LOGIN_FALLIDO": return "Inicio de sesión fallido";
                case "LOGIN_BLOQUEADO": return "Acceso bloqueado";
                case "LOGOUT": return "Cierre de sesión";
                case "APROBAR": return "Aprobación";
                case "RECHAZAR": return "Rechazo";
                case "LIQUIDAR": return "Liquidación";
                case "RETIRAR": return "Retiro";
                default: return c;
            }
        }

        /// <summary>Nombres distintos de tabla auditada, en orden alfabético, para el filtro Tabla (tal como están).</summary>
        public static List<string> ObtenerTablas()
        {
            DataTable dt = DbHelper.ConsultarTabla(
                "SELECT DISTINCT TablaAfectada FROM AuditoriaLog ORDER BY TablaAfectada");
            var tablas = new List<string>(dt.Rows.Count);
            foreach (DataRow fila in dt.Rows)
                tablas.Add(fila["TablaAfectada"].ToString());
            return tablas;
        }

        // ── Cifras (Auditoría y panel del administrador de sistema, E6-20) ────────────────────

        /// <summary>
        /// "Hoy" en AuditoriaLog (FechaHora va en hora de Perú): de las 00:00 de <paramref name="hoy"/> a las 00:00
        /// del día siguiente, sin incluirlo. Por rango y no con <c>CAST(FechaHora AS DATE)</c>, para que use el índice.
        /// </summary>
        internal static (DateTime Desde, DateTime Hasta) RangoDelDia(DateTime hoy) => (hoy.Date, hoy.Date.AddDays(1));

        /// <summary>El único "registros de hoy": lo usan <see cref="ObtenerResumen"/> y <see cref="ContarRegistrosDeHoy"/>.</summary>
        internal const string SqlRegistrosDeHoy = "SELECT COUNT(*) FROM AuditoriaLog WHERE FechaHora >= @hoyDesde AND FechaHora < @hoyHasta";

        private static SqlParameter[] ParametrosDeHoy()
        {
            var rango = RangoDelDia(FechaHelper.Hoy());
            return new[] { DbHelper.Param("@hoyDesde", rango.Desde), DbHelper.Param("@hoyHasta", rango.Hasta) };
        }

        /// <summary>Registros de auditoría de hoy (hora de Perú), para el panel del administrador de sistema.</summary>
        public static int ContarRegistrosDeHoy()
        {
            object resultado = DbHelper.EjecutarEscalar(SqlRegistrosDeHoy, ParametrosDeHoy());
            return resultado == null || resultado == DBNull.Value ? 0 : Convert.ToInt32(resultado);
        }

        /// <summary>
        /// Las cuatro cifras de la pantalla Auditoría en una consulta: total, de hoy, usuarios distintos de los
        /// últimos 7 días (desde esta hora de hace 7 días, hora de Perú) y tablas distintas.
        /// </summary>
        public static ResumenAuditoria ObtenerResumen()
        {
            var parametros = new List<SqlParameter>(ParametrosDeHoy())
            {
                DbHelper.Param("@hace7Dias", FechaHelper.Ahora().AddDays(-7))
            };
            DataTable dt = DbHelper.ConsultarTabla(@"
                    SELECT
                        (SELECT COUNT(*) FROM AuditoriaLog) AS TotalRegistros,
                        (" + SqlRegistrosDeHoy + @") AS RegistrosHoy,
                        (SELECT COUNT(DISTINCT NombreUsuario) FROM AuditoriaLog WHERE FechaHora >= @hace7Dias) AS UsuariosActivos,
                        (SELECT COUNT(DISTINCT TablaAfectada) FROM AuditoriaLog) AS TablasAfectadas",
                parametros.ToArray());
            if (dt.Rows.Count == 0) return null;
            DataRow r = dt.Rows[0];
            return new ResumenAuditoria
            {
                Total = Convert.ToInt32(r["TotalRegistros"]),
                Hoy = Convert.ToInt32(r["RegistrosHoy"]),
                Usuarios7Dias = Convert.ToInt32(r["UsuariosActivos"]),
                Tablas = Convert.ToInt32(r["TablasAfectadas"]),
            };
        }

        /// <summary>Cuántos eventos recientes muestra el panel del administrador de sistema.</summary>
        public const int UltimosEventosPanel = 8;

        /// <summary>Los <see cref="UltimosEventosPanel"/> eventos más recientes (usuario, acción, tabla y fecha) para el panel.</summary>
        public static DataTable ObtenerUltimosEventos() =>
            DbHelper.ConsultarTabla(
                "SELECT TOP (@n) NombreUsuario, Accion, TablaAfectada, FechaHora FROM AuditoriaLog ORDER BY FechaHora DESC",
                DbHelper.Param("@n", UltimosEventosPanel));

        /// <summary>Registros para el Excel (la consulta de siempre, con su tope).</summary>
        public static DataTable ObtenerParaExportar(FiltroAuditoria filtro, int maximo)
        {
            var parametros = new List<SqlParameter>();
            string sql = ConstruirConsultaExportacion(filtro, maximo, parametros);
            return DbHelper.ConsultarTabla(sql, parametros.ToArray());
        }

        /// <summary>Consulta del detalle: cada texto largo recortado en SQL a @max, con su largo real.</summary>
        internal const string SqlDetalle = @"
                    SELECT IdAuditoria,
                           LEFT(Descripcion, @max) AS Descripcion, LEN(Descripcion) AS LargoDescripcion,
                           DireccionIP, Navegador,
                           LEFT(ValoresAnteriores, @max) AS ValoresAnteriores, LEN(ValoresAnteriores) AS LargoAnteriores,
                           LEFT(ValoresNuevos, @max) AS ValoresNuevos, LEN(ValoresNuevos) AS LargoNuevos
                    FROM AuditoriaLog
                    WHERE IdAuditoria = @id";

        /// <summary>Detalle del registro <paramref name="id"/>; Ok = false con mensaje si no existe.</summary>
        public static DetalleAuditoria ObtenerDetalle(int id)
        {
            DataTable dt = DbHelper.ConsultarTabla(SqlDetalle,
                DbHelper.Param("@id", id), DbHelper.Param("@max", MaximoCaracteresDetalle));
            if (dt.Rows.Count == 0)
                return DetalleAuditoria.Error("No se encontró el registro de auditoría #" + id + ".");
            return ArmarDetalle(dt.Rows[0], MaximoCaracteresDetalle);
        }

        /// <summary>Arma el detalle desde la fila de <see cref="SqlDetalle"/>. Puro: sin BD.</summary>
        internal static DetalleAuditoria ArmarDetalle(DataRow fila, int maximo)
        {
            var d = new DetalleAuditoria
            {
                Ok = true,
                Id = Convert.ToInt32(fila["IdAuditoria"]),
                Descripcion = Texto(fila["Descripcion"]),
                DireccionIP = Texto(fila["DireccionIP"]),
                Navegador = Texto(fila["Navegador"]),
                ValoresAnteriores = Texto(fila["ValoresAnteriores"]),
                ValoresNuevos = Texto(fila["ValoresNuevos"]),
                AnterioresRecortados = Largo(fila["LargoAnteriores"]) > maximo,
                NuevosRecortados = Largo(fila["LargoNuevos"]) > maximo
            };
            bool descripcionRecortada = Largo(fila["LargoDescripcion"]) > maximo;

            var partes = new List<string>();
            if (descripcionRecortada) partes.Add("la descripción");
            if (d.AnterioresRecortados) partes.Add("los valores anteriores");
            if (d.NuevosRecortados) partes.Add("los valores nuevos");
            if (partes.Count > 0)
                d.AvisoRecorte = "Se muestran los primeros " + maximo.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-PE")) +
                    " caracteres de " + string.Join(" y ", partes) + "; el registro completo se conserva en la base de datos.";
            return d;
        }

        private static string Texto(object valor) => valor == null || valor == DBNull.Value ? null : valor.ToString();
        private static long Largo(object valor) => valor == null || valor == DBNull.Value ? 0 : Convert.ToInt64(valor);
    }
}
