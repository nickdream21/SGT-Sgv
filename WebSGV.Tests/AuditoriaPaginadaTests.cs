using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Services.Auditoria;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E5-8 (M-103): la grilla de Auditoría se pagina en SQL y no lleva ValoresAnteriores/ValoresNuevos;
    /// el detalle se pide al abrirlo ([WebMethod] ObtenerDetalle) y se pinta con .text().
    /// </summary>
    public class AuditoriaPaginadaTests
    {
        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Leer(params string[] partes) =>
            File.ReadAllText(Path.Combine(new[] { RaizWeb() }.Concat(partes).ToArray()));

        private static string SinComentariosJs(string js) =>
            Regex.Replace(Regex.Replace(js, @"/\*.*?\*/", "", RegexOptions.Singleline), @"(?m)^\s*//.*$", "");

        [Fact]
        public void ConsultaDePagina_LlevaOffsetFetchYNoSeleccionaLosValores()
        {
            var parametros = new List<SqlParameter>();
            string sql = ConsultaAuditoriaService.ConstruirConsultaPagina(new FiltroAuditoria(), 1, parametros);

            Assert.Contains("OFFSET @salto ROWS FETCH NEXT @tamano ROWS ONLY", sql);
            Assert.Contains("COUNT(*) OVER() AS TotalFilas", sql);
            Assert.Contains("LEFT(Descripcion, @largoDesc)", sql);
            Assert.Contains("ORDER BY FechaHora DESC, IdAuditoria DESC OFFSET", sql);
            Assert.DoesNotContain("ValoresAnteriores", sql);
            Assert.DoesNotContain("ValoresNuevos", sql);
            Assert.DoesNotContain("SELECT *", sql);
            Assert.Equal(ConsultaAuditoriaService.TamanoPagina, parametros.Single(p => p.ParameterName == "@salto").Value);
            Assert.Equal(25, parametros.Single(p => p.ParameterName == "@tamano").Value);
            Assert.Equal(200, parametros.Single(p => p.ParameterName == "@largoDesc").Value);
        }

        [Fact]
        public void ConsultaDePagina_UsaLosMismosFiltrosParametrizadosQueElExcel()
        {
            var filtro = new FiltroAuditoria
            {
                Desde = new DateTime(2026, 11, 1), Hasta = new DateTime(2026, 11, 16),
                Accion = "UPDATE", Tabla = "Usuarios", Usuario = "ana'; DROP TABLE x--"
            };
            var pPagina = new List<SqlParameter>();
            var pExcel = new List<SqlParameter>();
            string sqlPagina = ConsultaAuditoriaService.ConstruirConsultaPagina(filtro, 0, pPagina);
            string sqlExcel = ConsultaAuditoriaService.ConstruirConsultaExportacion(filtro, 10000, pExcel);

            string[] filtros = { "@FechaDesde", "@FechaHasta", "@Accion", "@Tabla", "@Usuario" };
            foreach (string f in filtros)
            {
                Assert.Contains(f, sqlPagina);
                Assert.Equal(pExcel.Single(p => p.ParameterName == f).Value, pPagina.Single(p => p.ParameterName == f).Value);
            }
            Assert.DoesNotContain("DROP", sqlPagina);
            Assert.Equal(0, pPagina.Single(p => p.ParameterName == "@salto").Value);
            // El Excel conserva su consulta: todo lo filtrado (con su tope), sin paginar.
            Assert.DoesNotContain("OFFSET", sqlExcel);
            Assert.Contains("TOP (@max)", sqlExcel);
        }

        [Fact]
        public void PaginaNegativa_SeTomaComoLaPrimera()
        {
            var parametros = new List<SqlParameter>();
            ConsultaAuditoriaService.ConstruirConsultaPagina(new FiltroAuditoria(), -3, parametros);
            Assert.Equal(0, parametros.Single(p => p.ParameterName == "@salto").Value);
        }

        [Fact]
        public void Marcado_SinValoresEnAtributosYGrillaConPaginadoPersonalizadoSinViewState()
        {
            string aspx = Leer("Views", "Auditoria.aspx");
            string js = Leer("Scripts", "paginas", "Auditoria.js");

            foreach (string texto in new[] { aspx, js })
            {
                Assert.DoesNotContain("data-anterior", texto);
                Assert.DoesNotContain("data-nuevo", texto);
                Assert.DoesNotMatch(@"data-(desc|ip|nav)\b", texto);
                Assert.DoesNotContain("Eval(\"ValoresAnteriores\")", texto);
                Assert.DoesNotContain("Eval(\"ValoresNuevos\")", texto);
            }

            Match grilla = Regex.Match(aspx, @"<asp:GridView ID=""gvAuditoria""[^>]*>", RegexOptions.Singleline);
            Assert.True(grilla.Success);
            Assert.Contains("AllowCustomPaging=\"true\"", grilla.Value);
            Assert.Contains("EnableViewState=\"false\"", grilla.Value);
            Assert.Contains("PageSize=\"25\"", grilla.Value);

            // El botón de detalle lleva solo data-accion y data-id.
            Match boton = Regex.Match(aspx, @"<button[^>]*aud-btn-detalle[^>]*>", RegexOptions.Singleline);
            Assert.True(boton.Success);
            var datos = Regex.Matches(boton.Value, @"\bdata-[\w-]+").Cast<Match>().Select(m => m.Value).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            Assert.Equal(new[] { "data-accion", "data-id" }, datos);
        }

        [Fact]
        public void Js_PideElDetalleAlWebMethodYPintaConText()
        {
            string js = SinComentariosJs(Leer("Scripts", "paginas", "Auditoria.js"));

            Assert.Contains("'Auditoria.aspx/ObtenerDetalle'", js);
            Assert.Contains("JSON.stringify({ id: id })", js);
            Assert.DoesNotContain(".html(", js);
            Assert.DoesNotContain("innerHTML", js);
            Assert.DoesNotContain("alert(", js);
            Assert.Contains("errorDetalle(", js);   // el error se muestra dentro del modal
            Assert.Contains("JSON.stringify(JSON.parse(texto), null, 2)", js);   // el JSON bonito se conserva
            Assert.Contains("#detalleCargando", js);
        }

        [Fact]
        public void CodeBehind_CargaLaGrillaPorElServicioYElExcelPorSuConsulta()
        {
            string cs = Leer("Views", "Auditoria.aspx.cs");

            Assert.Contains("ConsultaAuditoriaService.ObtenerPagina(", cs);
            Assert.Contains("gvAuditoria.VirtualItemCount = pagina.Total", cs);
            Assert.Contains("ConsultaAuditoriaService.ObtenerParaExportar(filtro, MaximoAlExportar)", cs);
            Assert.DoesNotContain("ex.Message", cs);
            Assert.Contains("MensajeErrorHelper.ParaUsuario(ex)", cs);
            Assert.DoesNotContain("SqlCommand", cs);
        }

        // ── Detalle (puro) ───────────────────────────────────────────

        private static DataRow FilaDetalle(string anteriores, long largoAnteriores, string nuevos, long largoNuevos)
        {
            var dt = new DataTable();
            dt.Columns.Add("IdAuditoria", typeof(int));
            dt.Columns.Add("Descripcion", typeof(string));
            dt.Columns.Add("LargoDescripcion", typeof(long));
            dt.Columns.Add("DireccionIP", typeof(string));
            dt.Columns.Add("Navegador", typeof(string));
            dt.Columns.Add("ValoresAnteriores", typeof(string));
            dt.Columns.Add("LargoAnteriores", typeof(long));
            dt.Columns.Add("ValoresNuevos", typeof(string));
            dt.Columns.Add("LargoNuevos", typeof(long));
            dt.Rows.Add(7, "Cambio", 6L, "10.0.0.1", "Edge",
                (object)anteriores ?? DBNull.Value, anteriores == null ? (object)DBNull.Value : largoAnteriores,
                (object)nuevos ?? DBNull.Value, nuevos == null ? (object)DBNull.Value : largoNuevos);
            return dt.Rows[0];
        }

        [Fact]
        public void Detalle_CompletoSinAviso()
        {
            var d = ConsultaAuditoriaService.ArmarDetalle(FilaDetalle("{\"a\":1}", 7, null, 0), 500000);

            Assert.True(d.Ok);
            Assert.Equal(7, d.Id);
            Assert.Equal("{\"a\":1}", d.ValoresAnteriores);
            Assert.Null(d.ValoresNuevos);
            Assert.False(d.AnterioresRecortados);
            Assert.False(d.NuevosRecortados);
            Assert.Null(d.AvisoRecorte);
        }

        [Fact]
        public void Detalle_RecortadoAvisaQueParte()
        {
            var d = ConsultaAuditoriaService.ArmarDetalle(FilaDetalle(new string('x', 10), 900000, "{}", 2), 10);

            Assert.True(d.AnterioresRecortados);
            Assert.False(d.NuevosRecortados);
            Assert.Equal("Se muestran los primeros 10 caracteres de los valores anteriores; el registro completo se conserva en la base de datos.", d.AvisoRecorte);
        }

        [Fact]
        public void Detalle_TopeDeDoscientosMilYRecorteEnSql()
        {
            Assert.Equal(200000, ConsultaAuditoriaService.MaximoCaracteresDetalle);
            string sql = ConsultaAuditoriaService.SqlDetalle;
            Assert.Contains("LEFT(ValoresAnteriores, @max)", sql);
            Assert.Contains("LEFT(ValoresNuevos, @max)", sql);
            Assert.Contains("LEFT(Descripcion, @max)", sql);
            Assert.Contains("WHERE IdAuditoria = @id", sql);
        }

        [Fact]
        public void Detalle_DeErrorNoTraeDatos()
        {
            var d = DetalleAuditoria.Error("sin acceso");
            Assert.False(d.Ok);
            Assert.Equal("sin acceso", d.Mensaje);
            Assert.Null(d.ValoresAnteriores);
        }
    }
}
