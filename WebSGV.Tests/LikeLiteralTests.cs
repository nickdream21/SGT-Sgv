using System.IO;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Reportes;
using WebSGV.Tests.Integracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Bloque 6, B6-4: los LIKE con texto del usuario buscan el texto literal (patrón armado en C# con
    /// <see cref="DbHelper.PatronContiene"/> / <see cref="DbHelper.PatronEmpieza"/>).
    /// </summary>
    public class LikeLiteralTests
    {
        [Theory]
        [InlineData("4400", "4400%")]
        [InlineData("a_b", "a[_]b%")]
        [InlineData("100%", "100[%]%")]
        [InlineData("[x]", "[[]x]%")]
        [InlineData("%_[", "[%][_][[]%")]
        [InlineData("", "%")]
        [InlineData(null, "%")]
        public void PatronEmpieza_escapa_los_comodines(string texto, string esperado)
        {
            Assert.Equal(esperado, DbHelper.PatronEmpieza(texto));
        }

        private static object Valor(System.Data.SqlClient.SqlParameter[] ps, string nombre) =>
            ps.Single(p => p.ParameterName == nombre).Value;

        [Fact]
        public void ReporteCargas_arma_los_patrones_en_CSharp()
        {
            var ps = ReporteCargasService.Parametros(new FiltroCargas { Pedido = " 44_0 ", Texto = " ABC-1_2 " });
            Assert.Equal("44[_]0%", Valor(ps, "@pedido"));
            Assert.Equal("%ABC-1[_]2%", Valor(ps, "@texto"));
            Assert.Equal("%ABC1[_]2%", Valor(ps, "@placa"));      // la placa se compara sin guiones, como antes
        }

        [Fact]
        public void ReporteCargas_sin_texto_ni_pedido_no_filtra()
        {
            var ps = ReporteCargasService.Parametros(new FiltroCargas { Pedido = "  ", Texto = null });
            Assert.Equal(System.DBNull.Value, Valor(ps, "@pedido"));
            Assert.Equal(System.DBNull.Value, Valor(ps, "@texto"));
            Assert.Equal(System.DBNull.Value, Valor(ps, "@placa"));
        }

        private static string Fuente(params string[] ruta) =>
            File.ReadAllText(Path.Combine(BaseDePruebas.RaizRepo, Path.Combine(ruta)));

        /// <summary>Código que no se puede llamar sin la página (code-behind): se revisa el fuente.</summary>
        [Theory]
        [InlineData("Services", "Reportes/ReportesOrdenesViajeService.cs", "DbHelper.PatronContiene(buscarConductor)")]
        [InlineData("Services", "Exportacion/SeguimientoViajesService.cs", "DbHelper.PatronContiene(texto)")]
        [InlineData("Services", "Liquidaciones/LiquidacionesContabilidadService.cs", "DbHelper.PatronContiene(nombreConductor)")]
        public void Busquedas_usan_el_patron_escapado(string carpeta, string archivo, string esperado)
        {
            string codigo = Fuente("WebSGV", carpeta, archivo);
            Assert.Contains(esperado, codigo);
            Assert.DoesNotContain("$\"%{", codigo);
            Assert.DoesNotContain("\"%\" + conductor", codigo);
            Assert.DoesNotContain("\"%\" + texto", codigo);
            Assert.DoesNotContain("\"%\" + numeroOrden", codigo);
            Assert.DoesNotContain("\"%\" + nombreConductor", codigo);
        }

        [Fact]
        public void Auditoria_filtra_el_usuario_literal()
        {
            var parametros = new System.Collections.Generic.List<System.Data.SqlClient.SqlParameter>();
            WebSGV.Services.Auditoria.ConsultaAuditoriaService.ConstruirConsultaPagina(
                new WebSGV.Services.Auditoria.FiltroAuditoria { Usuario = "a_b%" }, 0, parametros);
            Assert.Equal("%a[_]b[%]%", Valor(parametros.ToArray(), "@Usuario"));
        }

        [Fact]
        public void ControlDeAprobadas_busca_literal_y_con_RTRIM_por_columna()
        {
            string codigo = Fuente("WebSGV", "Services", "Liquidaciones", "LiquidacionesPendientesService.cs");
            Assert.Contains("DbHelper.PatronContiene(term.Trim())", codigo);
            Assert.Contains("DbHelper.PatronContiene(numeroOrden)", codigo);
            Assert.Contains("(RTRIM(c.nombre) + ' ' + RTRIM(c.apPaterno) + ' ' + RTRIM(ISNULL(c.apMaterno, ''))) LIKE @term", codigo);
            Assert.DoesNotContain("'%' + @numeroOrden", codigo);
            Assert.DoesNotContain("\"%\" + term", codigo);
        }

        [Fact]
        public void ReporteCargas_no_arma_el_patron_en_el_SQL()
        {
            string codigo = Fuente("WebSGV", "Services", "Reportes", "ReporteCargasService.cs");
            Assert.DoesNotContain("'%' + @texto", codigo);
            Assert.DoesNotContain("@pedido + '%'", codigo);
            Assert.DoesNotContain("REPLACE(@texto", codigo);
        }
    }
}
