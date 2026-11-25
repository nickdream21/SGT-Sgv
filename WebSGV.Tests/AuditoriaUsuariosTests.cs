using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Newtonsoft.Json.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Auditoria;
using WebSGV.Services.Usuarios;
using WebSGV.Views;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Auditoría de seguridad, tarea 8A: valores antes/después que se auditan en Gestión de usuarios
    /// y la consulta acotada de la página Auditoría.
    /// </summary>
    public class AuditoriaUsuariosTests
    {
        [Fact]
        public void JsonUsuarioNuevo_EscapaComillasYConservaTildes()
        {
            string json = GestionUsuariosService.JsonUsuarioNuevo("jperez", "José \"Pepe\" Muñoz", "CONDUCTOR", 15);

            var o = JObject.Parse(json);
            Assert.Equal("jperez", (string)o["nombreUsuario"]);
            Assert.Equal("José \"Pepe\" Muñoz", (string)o["nombre"]);
            Assert.Equal("CONDUCTOR", (string)o["rol"]);
            Assert.Equal(15, (int)o["idConductor"]);
            Assert.Equal(1, (int)o["requiereCambioContrasena"]);
            Assert.Contains("\\\"Pepe\\\"", json);
            Assert.Contains("José", json);
        }

        [Fact]
        public void JsonUsuarioNuevo_NuncaIncluyeLaContrasena()
        {
            var o = JObject.Parse(GestionUsuariosService.JsonUsuarioNuevo("admin2", "Ana", "ADMIN", null));

            Assert.Equal(JTokenType.Null, o["idConductor"].Type);
            Assert.DoesNotContain(o.Properties(), p => p.Name.IndexOf("contrasena", StringComparison.OrdinalIgnoreCase) >= 0
                                                    && p.Name != "requiereCambioContrasena");
            Assert.DoesNotContain(o.Properties(), p => p.Name.IndexOf("pass", StringComparison.OrdinalIgnoreCase) >= 0
                                                    || p.Name.IndexOf("hash", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public void ConsultaDeExportacion_EstaAcotadaConTopYOrdenadaPorFechaDescendente()
        {
            var parametros = new List<SqlParameter>();
            var filtro = new FiltroAuditoria
            {
                Desde = new DateTime(2026, 11, 1), Hasta = new DateTime(2026, 11, 16),
                Accion = "UPDATE", Tabla = "Usuarios", Usuario = "ana"
            };
            string sql = ConsultaAuditoriaService.ConstruirConsultaExportacion(filtro, Auditoria.MaximoAlExportar, parametros);

            Assert.Contains("SELECT TOP (@max)", sql);
            Assert.EndsWith("ORDER BY FechaHora DESC, IdAuditoria DESC", sql.Trim());
            Assert.Equal(Auditoria.MaximoAlExportar, parametros.Single(p => p.ParameterName == "@max").Value);
            Assert.Equal("%ana%", parametros.Single(p => p.ParameterName == "@Usuario").Value);
            Assert.Equal(6, parametros.Count);
        }

        [Fact]
        public void ConsultaDeExportacion_SinFiltrosSoloLlevaElTope()
        {
            var parametros = new List<SqlParameter>();
            string sql = ConsultaAuditoriaService.ConstruirConsultaExportacion(new FiltroAuditoria(), Auditoria.MaximoAlExportar, parametros);

            Assert.Contains("TOP (@max)", sql);
            var max = Assert.Single(parametros);
            Assert.Equal(10000, max.Value);
        }

        [Fact]
        public void AvisoTope_SoloCuandoSeLlegaAlMaximo()
        {
            Assert.Null(Auditoria.AvisoTope(999, 1000));
            Assert.Equal("Se muestran los 1000 registros más recientes; acote las fechas.", Auditoria.AvisoTope(1000, 1000));
        }

        // ─── Tarea 17: exportación a .xlsx real con ReporteTabular ───

        private static System.Data.DataTable TablaAuditoria(int filas, string descripcion = "Cambio de prueba")
        {
            var dt = new System.Data.DataTable();
            dt.Columns.Add("IdAuditoria", typeof(int));
            dt.Columns.Add("FechaHora", typeof(DateTime));
            dt.Columns.Add("NombreUsuario", typeof(string));
            dt.Columns.Add("RolUsuario", typeof(string));
            dt.Columns.Add("Accion", typeof(string));
            dt.Columns.Add("TablaAfectada", typeof(string));
            dt.Columns.Add("IdRegistroAfectado", typeof(string));
            dt.Columns.Add("Descripcion", typeof(string));
            dt.Columns.Add("DireccionIP", typeof(string));
            for (int i = 0; i < filas; i++)
                dt.Rows.Add(i + 1, new DateTime(2026, 11, 16, 14, 5, 9), "Ana Pérez", "ADMIN", "UPDATE", "Usuarios",
                    "7", descripcion, "10.0.0.1");
            return dt;
        }

        [Fact]
        public void ExportacionAuditoria_EsXlsxConColumnasYFiltros()
        {
            var r = Auditoria.ConstruirReporte(TablaAuditoria(2), new DateTime(2026, 11, 1), new DateTime(2026, 11, 16),
                "UPDATE", "Actualizacion", "Usuarios", "ana", Auditoria.MaximoAlExportar, "Admin Sistema");
            byte[] xlsx = WebSGV.Services.Reportes.ReporteTabularExportador.GenerarExcel(r, new EmpresaInfo { RazonSocial = "EMPRESA" });

            Assert.Equal((byte)'P', xlsx[0]);                     // .xlsx = ZIP ("PK"), no HTML
            Assert.Equal((byte)'K', xlsx[1]);
            var ws = new ClosedXML.Excel.XLWorkbook(new System.IO.MemoryStream(xlsx)).Worksheet(1);
            var cabecera = ws.RowsUsed().First(f => f.Cell(1).GetString() == "ID");
            Assert.Equal(new[] { "ID", "Fecha / hora", "Usuario", "Rol", "Acción", "Tabla", "ID reg.", "Descripción", "IP" },
                Enumerable.Range(1, 9).Select(c => cabecera.Cell(c).GetString()));
            var fila = ws.Row(cabecera.RowNumber() + 1);
            Assert.Equal("16/11/2026 14:05:09", fila.Cell(2).GetString());
            Assert.Equal("Ana Pérez", fila.Cell(3).GetString());

            string todo = string.Join("|", ws.CellsUsed().Select(c => c.GetString()));
            Assert.Contains("Período: 01/11/2026 al 16/11/2026", todo);
            Assert.Contains("Acción: Actualizacion", todo);
            Assert.Contains("Usuario: ana", todo);
            Assert.Null(r.Nota);                                  // no llegó al tope
        }

        [Fact]
        public void ExportacionAuditoria_AvisaElTopeYCortaTextosEnormes()
        {
            var r = Auditoria.ConstruirReporte(TablaAuditoria(3, new string('x', 40000)), null, null,
                "", "-- Todas --", "", "", 3, null);

            Assert.Equal(Auditoria.AvisoTope(3, 3), r.Nota);
            Assert.Contains("Acción: todas", r.Filtros[1]);
            string desc = (string)r.Columnas.Single(c => c.Titulo == "Descripción").Valor(r.Filas[0]);
            Assert.Equal(Auditoria.MaximoCaracteresCelda + 1, desc.Length);
            WebSGV.Services.Reportes.ReporteTabularExportador.GenerarExcel(r, new EmpresaInfo()); // no revienta
        }
    }
}
