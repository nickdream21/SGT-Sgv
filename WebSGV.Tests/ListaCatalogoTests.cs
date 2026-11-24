using System.Data;
using System.Linq;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>E5-16: búsqueda, página válida y resumen de las listas de catálogo (lógica pura).</summary>
    public class ListaCatalogoTests
    {
        [Fact]
        public void RecortarBusqueda_QuitaEspaciosYCortaAlLargoMaximo()
        {
            Assert.Equal("", ListaCatalogo.RecortarBusqueda(null));
            Assert.Equal("abc", ListaCatalogo.RecortarBusqueda("  abc  "));
            Assert.Equal(ListaCatalogo.LargoBusqueda, ListaCatalogo.RecortarBusqueda(new string('x', 5000)).Length);
        }

        private static DataTable Tabla(params string[] placas)
        {
            var t = new DataTable();
            t.Columns.Add("placa", typeof(string));
            t.Columns.Add("marca", typeof(string));
            t.Columns.Add("activo", typeof(bool));
            foreach (string p in placas) t.Rows.Add(p, "MARCA " + p, true);
            return t;
        }

        [Theory]
        [InlineData("abc123", "ABC123")]
        [InlineData("ABC-123", "ABC123")]
        [InlineData(" a b-c ", "ABC")]
        [InlineData("García", "GARCIA")]
        [InlineData("Peña", "PENA")]
        [InlineData("ÁÉÍÓÚ", "AEIOU")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void Normalizar_SinTildesMayusculasNiGuiones(string entrada, string esperado) =>
            Assert.Equal(esperado, ListaCatalogo.Normalizar(entrada));

        [Fact]
        public void Filtrar_EncuentraSinGuionNiMayusculas()
        {
            var t = Tabla("ABC-123", "XYZ-789", "ABD-555");
            var r = ListaCatalogo.Filtrar(t, "abc123", "placa", "marca");
            Assert.Single(r.Rows.Cast<DataRow>());
            Assert.Equal("ABC-123", r.Rows[0]["placa"]);
            Assert.Equal(3, t.Rows.Count);   // la original no cambia
        }

        [Fact]
        public void Filtrar_BuscaEnVariasColumnasYConTildes()
        {
            var t = Tabla("AAA-111");
            t.Rows.Add("BBB-222", "Mercedes Benz Línea", false);
            Assert.Equal(1, ListaCatalogo.Filtrar(t, "linea", "placa", "marca").Rows.Count);
            Assert.Equal(2, ListaCatalogo.Filtrar(t, "a", "placa", "marca").Rows.Count);
            Assert.Equal(0, ListaCatalogo.Filtrar(t, "linea", "placa").Rows.Count);   // solo en las columnas pedidas
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(" - ")]
        [InlineData(null)]
        public void Filtrar_VacioDevuelveTodas(string texto)
        {
            var t = Tabla("A-1", "B-2", "C-3");
            var r = ListaCatalogo.Filtrar(t, texto, "placa");
            Assert.Equal(3, r.Rows.Count);
            Assert.NotSame(t, r);
        }

        [Fact]
        public void Filtrar_IgnoraNulosYColumnasInexistentes()
        {
            var t = Tabla("A-1");
            t.Rows.Add(System.DBNull.Value, System.DBNull.Value, true);
            Assert.Equal(1, ListaCatalogo.Filtrar(t, "a1", "placa", "noExiste").Rows.Count);
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(3, 0, 0)]
        [InlineData(-1, 100, 0)]
        [InlineData(0, 25, 0)]
        [InlineData(1, 25, 0)]
        [InlineData(1, 26, 1)]
        [InlineData(5, 134, 5)]
        [InlineData(6, 134, 5)]
        [InlineData(99, 134, 5)]
        public void IndiceValido_EnLosBordes(int indice, int filas, int esperado) =>
            Assert.Equal(esperado, ListaCatalogo.IndiceValido(indice, filas));

        [Theory]
        [InlineData(0, 134, 134, "", "Mostrando 1–25 de 134")]
        [InlineData(1, 134, 134, null, "Mostrando 26–50 de 134")]
        [InlineData(5, 134, 134, "", "Mostrando 126–134 de 134")]
        [InlineData(0, 3, 3, "", "Mostrando 1–3 de 3")]
        [InlineData(0, 0, 0, "", "")]
        [InlineData(0, 3, 134, "x", "3 de 134 coinciden con «x»")]
        [InlineData(0, 1, 134, " abc ", "1 de 134 coincide con «abc»")]
        [InlineData(0, 0, 134, "zzz", "Ninguno coincide con «zzz»")]
        [InlineData(1, 40, 134, "a", "Mostrando 26–40 de 40 que coinciden con «a»")]
        [InlineData(4, 101, 101, "", "Mostrando 101 de 101")]
        [InlineData(9, 101, 101, "", "Mostrando 101 de 101")]
        [InlineData(1, 26, 134, "a", "Mostrando 26 de 26 que coinciden con «a»")]
        public void Resumen_Textos(int indice, int filtradas, int total, string texto, string esperado) =>
            Assert.Equal(esperado, ListaCatalogo.Resumen(indice, filtradas, total, texto));
    }
}
