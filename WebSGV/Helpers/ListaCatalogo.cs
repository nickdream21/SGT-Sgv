using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Lista de un catálogo (E5-16): búsqueda en el servidor, página válida y la línea de resumen
    /// ("Mostrando 26–50 de 134"). Lógica pura, sin ASP.NET: la usan las páginas Registro* con su GridView
    /// paginado de <see cref="FilasPorPagina"/> filas y sin ViewState.
    /// </summary>
    public static class ListaCatalogo
    {
        /// <summary>Filas por página de las grillas de catálogo (PageSize="25" en el marcado).</summary>
        public const int FilasPorPagina = 25;

        /// <summary>Largo máximo del texto de búsqueda (el mismo MaxLength de txtBuscar), también en el servidor.</summary>
        public const int LargoBusqueda = 50;

        /// <summary>Texto de búsqueda sin espacios a los lados y recortado a <see cref="LargoBusqueda"/>.</summary>
        public static string RecortarBusqueda(string texto)
        {
            string t = (texto ?? "").Trim();
            return t.Length > LargoBusqueda ? t.Substring(0, LargoBusqueda) : t;
        }

        /// <summary>
        /// Clave de búsqueda: sin tildes, en mayúsculas y sin espacios ni guiones, para que "abc123" encuentre
        /// "ABC-123" y "garcia" encuentre "García".
        /// </summary>
        public static string Normalizar(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            string descompuesto = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(descompuesto.Length);
            foreach (char c in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsWhiteSpace(c) || c == '-' || c == '‐' || c == '‑' || c == '–') continue;
                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>
        /// Copia de <paramref name="tabla"/> con las filas en las que alguna de <paramref name="columnas"/>
        /// contiene el texto (comparado con <see cref="Normalizar"/>). Texto vacío (o solo espacios y guiones):
        /// todas las filas. Una columna que no existe en la tabla se ignora.
        /// </summary>
        public static DataTable Filtrar(DataTable tabla, string texto, params string[] columnas)
        {
            if (tabla == null) throw new ArgumentNullException(nameof(tabla));
            string clave = Normalizar(texto);
            if (clave.Length == 0) return tabla.Copy();

            var cols = (columnas ?? new string[0]).Where(c => c != null && tabla.Columns.Contains(c)).ToArray();
            DataTable resultado = tabla.Clone();
            foreach (DataRow fila in tabla.Rows)
            {
                foreach (string c in cols)
                {
                    object v = fila[c];
                    if (v == null || v == DBNull.Value) continue;
                    if (Normalizar(Convert.ToString(v, CultureInfo.InvariantCulture)).Contains(clave))
                    {
                        resultado.ImportRow(fila);
                        break;
                    }
                }
            }
            return resultado;
        }

        /// <summary>Número de páginas para <paramref name="filas"/> filas (al menos 1).</summary>
        public static int Paginas(int filas) => filas <= 0 ? 1 : (filas + FilasPorPagina - 1) / FilasPorPagina;

        /// <summary>
        /// Índice de página (base 0) dentro del rango para <paramref name="filas"/> filas: tras buscar, borrar o
        /// desactivar, la página guardada puede quedar fuera (o venir negativa del paginador).
        /// </summary>
        public static int IndiceValido(int indice, int filas)
        {
            if (indice < 0) return 0;
            int ultima = Paginas(filas) - 1;
            return indice > ultima ? ultima : indice;
        }

        /// <summary>
        /// Línea de resumen de la lista: "Mostrando 26–50 de 134"; con búsqueda, "3 de 134 coinciden con «x»"
        /// (si caben en una página), "Mostrando 1–25 de 40 que coinciden con «x»" (si no) o
        /// "Ninguno coincide con «x»". Sin filas ni búsqueda, vacía (la grilla muestra su texto de vacío).
        /// El texto va sin codificar: la página lo pinta con un Literal Mode="Encode".
        /// </summary>
        public static string Resumen(int indice, int filtradas, int total, string texto)
        {
            string buscado = (texto ?? "").Trim();
            bool conFiltro = Normalizar(buscado).Length > 0;
            var es = CultureInfo.GetCultureInfo("es-PE");

            if (!conFiltro)
            {
                if (total <= 0) return string.Empty;
                return "Mostrando " + Rango(indice, total, es) + " de " + total.ToString("N0", es);
            }

            string cita = "«" + buscado + "»";
            if (filtradas <= 0) return "Ninguno coincide con " + cita;
            if (filtradas <= FilasPorPagina)
                return filtradas.ToString("N0", es) + " de " + total.ToString("N0", es) +
                       (filtradas == 1 ? " coincide con " : " coinciden con ") + cita;
            return "Mostrando " + Rango(indice, filtradas, es) + " de " + filtradas.ToString("N0", es) +
                   " que coinciden con " + cita;
        }

        private static string Rango(int indice, int filas, CultureInfo es)
        {
            int i = IndiceValido(indice, filas);
            int desde = i * FilasPorPagina + 1;
            int hasta = Math.Min(filas, (i + 1) * FilasPorPagina);
            if (desde == hasta) return desde.ToString("N0", es);   // "Mostrando 101 de 101"
            return desde.ToString("N0", es) + "\u2013" + hasta.ToString("N0", es);
        }
    }
}
