using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-29 (M-138): las reglas de documentos de un lote (perfil del cliente, qué se espera de cada carga y qué falta)
    /// están escritas dos veces, en <c>sp_LD_ObtenerLotesRegistrados</c> (la lista) y en <c>sp_LD_ContarLotesActivos</c>
    /// (los contadores). Van entre marcadores <c>-- [reglas-documentos:x]</c> … <c>-- [/reglas-documentos:x]</c> y el
    /// texto de adentro tiene que ser idéntico, letra por letra y espacio por espacio (solo se ignora CRLF/LF): si una
    /// regla cambia en un SP y no en el otro, la pestaña y la lista dirían números distintos. Prueba de fuente, sin BD.
    /// </summary>
    public class ReglasDocumentosSpTests
    {
        private static readonly string[] Spes = { "sp_LD_ObtenerLotesRegistrados", "sp_LD_ContarLotesActivos" };

        private static string Sp(string nombre)
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Database", "StoredProcedures")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return File.ReadAllText(Path.Combine(dir.FullName, "WebSGV", "Database", "StoredProcedures", nombre + ".sql"))
                .Replace("\r\n", "\n");
        }

        /// <summary>Lo que hay entre el fin de la línea del marcador de apertura y el comienzo de la línea del de cierre.</summary>
        private static string Region(string sql, string sp, string region)
        {
            string abre = "-- [reglas-documentos:" + region + "]", cierra = "-- [/reglas-documentos:" + region + "]";
            Assert.True(Regex.Matches(sql, Regex.Escape(abre)).Count == 1, $"{sp}: el marcador «{abre}» debe estar una sola vez.");
            Assert.True(Regex.Matches(sql, Regex.Escape(cierra)).Count == 1, $"{sp}: el marcador «{cierra}» debe estar una sola vez.");
            int i = sql.IndexOf(abre, StringComparison.Ordinal), j = sql.IndexOf(cierra, StringComparison.Ordinal);
            Assert.True(i < j, $"{sp}: «{abre}» va antes de «{cierra}».");
            int desde = sql.IndexOf('\n', i) + 1;
            int hasta = sql.LastIndexOf('\n', j) + 1;
            Assert.True(desde > 0 && hasta >= desde, $"{sp}: los marcadores de «{region}» van cada uno en su línea.");
            return sql.Substring(desde, hasta - desde);
        }

        [Theory]
        [InlineData("perfil-cliente", "@perfilCliente")]
        [InlineData("documentos-por-tipo", "AS perfil")]
        [InlineData("faltantes", "AS SinManifiesto")]
        [InlineData("faltantes", "AS EsperaManifiesto")]
        public void Region_IdenticaEnLosDosSp(string region, string debeContener)
        {
            string lista = Region(Sp(Spes[0]), Spes[0], region);
            string conteo = Region(Sp(Spes[1]), Spes[1], region);
            Assert.Contains(debeContener, lista);
            Assert.True(lista == conteo,
                $"La región «{region}» difiere entre {Spes[0]} y {Spes[1]}: copie el texto idéntico en los dos (editar los dos).");
        }

        [Fact]
        public void LosDosSp_AvisanQueSeEditanJuntos()
        {
            foreach (string sp in Spes)
            {
                string sql = Sp(sp);
                Assert.Contains("editar las dos", sql);
                Assert.Contains("ReglasDocumentosSpTests", sql);
            }
        }

        /// <summary>
        /// M-242: el manifiesto está completo con un MCI de cualquier tipo en cada despacho que lo espera (DN-21); ningún
        /// SP vuelve a exigir el de cruce y el de retorno a la vez, y el contador compara contra los que lo esperan.
        /// </summary>
        [Fact]
        public void Manifiesto_UnMciPorDespachoQueLoEspera_NoCruceYRetorno()
        {
            foreach (string sp in Spes)
            {
                string sql = Sp(sp);
                Assert.DoesNotMatch(@"(?is)TieneCruce\s*=\s*1\s+AND\s+\S*TieneRetorno", sql);
                Assert.DoesNotContain("TieneCruce", sql);
                Assert.DoesNotContain("TieneRetorno", sql);
                Assert.Matches(@"(?i)SUM\(d\.EsperaManifiesto\)\s+AS\s+DespachosEsperanManifiesto", sql);
            }
            string conteo = Sp("sp_LD_ContarLotesActivos");
            Assert.Matches(@"(?i)l\.DespachosEsperanManifiesto\s*>\s*0", conteo);
            Assert.Matches(@"(?i)l\.DespachosManifiestoCompleto\s*<\s*l\.DespachosEsperanManifiesto", conteo);
            Assert.DoesNotMatch(@"(?i)l\.DespachosManifiestoCompleto\s*<\s*l\.CantidadDespachos", conteo);
        }

        [Fact]
        public void Conteo_SinGetdate_ConHoyYTolerancia()
        {
            string sql = Sp("sp_LD_ContarLotesActivos");
            Assert.DoesNotMatch(@"(?i)\b(getdate|sysdatetime|current_timestamp|getutcdate)\b", sql);
            Assert.Matches(@"(?i)@hoy\s+DATE\b", sql);
            Assert.Matches(@"(?i)@diasTolerancia\s+INT\s*=\s*3\b", sql);
            Assert.Matches(@"(?i)@idCliente\s+INT\s*=\s*NULL\b", sql);
            Assert.Contains("CREATE OR ALTER PROCEDURE sp_LD_ContarLotesActivos", sql);
        }

        [Fact]
        public void Lista_ConTopeOpcionalTotalYOrdenEstable()
        {
            string sql = Sp("sp_LD_ObtenerLotesRegistrados");
            Assert.Matches(@"(?i)@maxFilas\s+INT\s*=\s*NULL", sql);
            Assert.Matches(@"(?i)SELECT\s+TOP\s*\(\s*ISNULL\(@maxFilas,\s*2147483647\)\s*\)\s+la\.\*,\s*COUNT\(\*\)\s+OVER\s*\(\s*\)\s+AS\s+TotalLotes", sql);
            Assert.Matches(@"(?is)ORDER BY CASE WHEN @documentos = 'PENDIENTES' THEN la\.FechaProgramacion END,\s*"
                + @"CASE WHEN @documentos = 'PENDIENTES' THEN la\.NombreCliente END,\s*la\.FechaCreacion DESC,\s*la\.IdLoteVirtual\s", sql);
        }
    }
}
