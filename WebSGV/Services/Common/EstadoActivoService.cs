using System;
using System.Data;
using System.Globalization;
using WebSGV.Helpers;

namespace WebSGV.Services.Common
{
    /// <summary>
    /// Tabla de un maestro con baja lógica (columna bit activo/activa). Los nombres de tabla y columnas son
    /// constantes del programa (el constructor es interno): nunca vienen del usuario.
    /// </summary>
    public sealed class TablaActivable
    {
        internal TablaActivable(string tabla, string columnaId, string columnaActivo, string expresionNombre,
            string sustantivo, bool femenino)
        {
            Tabla = tabla;
            ColumnaId = columnaId;
            ColumnaActivo = columnaActivo;
            ExpresionNombre = expresionNombre;
            Sustantivo = sustantivo;
            Femenino = femenino;
        }

        public string Tabla { get; }
        internal string ColumnaId { get; }
        internal string ColumnaActivo { get; }
        /// <summary>Expresión SQL que identifica la fila para el usuario (placa, nombre...).</summary>
        internal string ExpresionNombre { get; }
        /// <summary>Sustantivo en minúsculas y singular ("tracto", "estación de peaje").</summary>
        public string Sustantivo { get; }
        public bool Femenino { get; }
    }

    /// <summary>Resultado de <see cref="EstadoActivoService.Cambiar"/>.</summary>
    public sealed class ResultadoCambioEstado
    {
        /// <summary>false si la fila ya tenía el estado pedido (no se tocó ni se auditó).</summary>
        public bool Cambiado { get; set; }
        public string Mensaje { get; set; }
    }

    /// <summary>
    /// Activar / desactivar un maestro con estado destino explícito (bloque 5, B5-3). Antes el botón invertía el
    /// estado "a ciegas" (<c>CASE WHEN activo = 1 THEN 0 ELSE 1 END</c>): con dos pestañas o un F5 que reenvía el
    /// postback se reactivaba lo que se quería desactivar. Ahora el botón manda el estado que quiere dejar
    /// (<see cref="ArgumentoComando"/>), el UPDATE solo toca la fila si tiene el otro estado y, si no cambia nada,
    /// se avisa que "ya estaba" sin auditar. La auditoría lleva el estado anterior y el nuevo.
    /// </summary>
    public static class EstadoActivoService
    {
        // ── Lógica pura ──────────────────────────────────────────────────

        /// <summary>
        /// CommandArgument del botón Activar/Desactivar de una fila: "id|estadoDestino" (0 = desactivar si hoy
        /// está activa, 1 = activar si hoy está inactiva).
        /// </summary>
        public static string ArgumentoComando(object id, object activoActual) =>
            Convert.ToInt32(id, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) + "|" +
            (EsActivo(activoActual) ? "0" : "1");

        /// <summary>Lee "id|0" o "id|1". false si el formato no es ese (p. ej. una página vieja en caché).</summary>
        public static bool TryLeerArgumento(object argumento, out int id, out bool nuevoActivo)
        {
            id = 0;
            nuevoActivo = false;
            string[] partes = (argumento?.ToString() ?? "").Split('|');
            if (partes.Length != 2) return false;
            if (!int.TryParse(partes[0], NumberStyles.None, CultureInfo.InvariantCulture, out id) || id <= 0) return false;
            if (partes[1] == "1") nuevoActivo = true;
            else if (partes[1] != "0") return false;
            return true;
        }

        /// <summary>"El tracto ya estaba inactivo; se actualizó la lista." (con género).</summary>
        public static string MensajeYaEstaba(string sustantivo, bool femenino, bool activo) =>
            $"{Articulo(femenino)} {sustantivo} ya estaba {Adjetivo(femenino, activo)}; se actualizó la lista.";

        /// <summary>"Tracto ABC-123 desactivado correctamente." (con género).</summary>
        public static string MensajeCambiado(string sustantivo, bool femenino, bool activo, string nombre) =>
            $"{Capital(sustantivo)} {nombre} {(activo ? "activad" : "desactivad")}{(femenino ? "a" : "o")} correctamente.";

        /// <summary>Texto de los valores anterior/nuevo de la auditoría.</summary>
        public static string DescribirEstado(bool activo) => activo ? "Estado: Activo" : "Estado: Inactivo";

        private static bool EsActivo(object activo) =>
            activo != null && activo != DBNull.Value && Convert.ToBoolean(activo, CultureInfo.InvariantCulture);

        private static string Articulo(bool femenino) => femenino ? "La" : "El";

        private static string Adjetivo(bool femenino, bool activo) =>
            (activo ? "activ" : "inactiv") + (femenino ? "a" : "o");

        private static string Capital(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], CultureInfo.InvariantCulture) + s.Substring(1);

        // ── Base de datos ────────────────────────────────────────────────

        /// <summary>
        /// Deja la fila en <paramref name="nuevoActivo"/>. Si ya lo estaba no la toca ni audita y devuelve
        /// <see cref="ResultadoCambioEstado.Cambiado"/> = false con el mensaje "ya estaba". Si la fila no existe,
        /// lanza <see cref="ErrorNegocioException"/>.
        /// </summary>
        public static ResultadoCambioEstado Cambiar(TablaActivable t, int id, bool nuevoActivo)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));

            DataTable actual = DbHelper.ConsultarTabla(
                $"SELECT {t.ExpresionNombre} AS nombre FROM {t.Tabla} WHERE {t.ColumnaId} = @id",
                DbHelper.Param("@id", id));
            if (actual.Rows.Count == 0)
                throw new ErrorNegocioException($"{Articulo(t.Femenino)} {t.Sustantivo} ya no existe; recargue la página.");
            string nombre = (actual.Rows[0]["nombre"] as string ?? "").Trim();
            if (nombre.Length == 0) nombre = "#" + id.ToString(CultureInfo.InvariantCulture);

            // OUTPUT ... INTO (y no OUTPUT a secas ni el conteo de ExecuteNonQuery): Tracto, Carreta y Conductor
            // tienen trigger de auditoría, que rechaza el OUTPUT sin INTO y suma sus propias filas al conteo.
            int filas = Convert.ToInt32(DbHelper.EjecutarEscalar(
                $@"DECLARE @cambiadas TABLE (id INT);
                   UPDATE {t.Tabla}
                      SET {t.ColumnaActivo} = @nuevo
                   OUTPUT inserted.{t.ColumnaId} INTO @cambiadas (id)
                    WHERE {t.ColumnaId} = @id AND {t.ColumnaActivo} <> @nuevo;
                   SELECT COUNT(*) FROM @cambiadas;",
                DbHelper.Param("@nuevo", nuevoActivo),
                DbHelper.Param("@id", id)), CultureInfo.InvariantCulture);

            if (filas == 0)
                return new ResultadoCambioEstado
                {
                    Cambiado = false,
                    Mensaje = MensajeYaEstaba(t.Sustantivo, t.Femenino, nuevoActivo)
                };

            AuditoriaHelper.Registrar("UPDATE", t.Tabla, id,
                $"{Capital(t.Sustantivo)} {nombre} {(nuevoActivo ? "activad" : "desactivad")}{(t.Femenino ? "a" : "o")}",
                valoresAnteriores: DescribirEstado(!nuevoActivo),
                valoresNuevos: DescribirEstado(nuevoActivo));

            return new ResultadoCambioEstado
            {
                Cambiado = true,
                Mensaje = MensajeCambiado(t.Sustantivo, t.Femenino, nuevoActivo, nombre)
            };
        }
    }
}
