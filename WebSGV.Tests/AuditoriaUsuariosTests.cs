using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Newtonsoft.Json.Linq;
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
        [Theory]
        [InlineData(true, "{\"activo\":1}")]
        [InlineData(false, "{\"activo\":0}")]
        public void JsonEstadoActivo_EsUnoOCero(bool activo, string esperado)
        {
            Assert.Equal(esperado, GestionUsuarios.JsonEstadoActivo(activo));
        }

        [Fact]
        public void JsonUsuarioNuevo_EscapaComillasYConservaTildes()
        {
            string json = GestionUsuarios.JsonUsuarioNuevo("jperez", "José \"Pepe\" Muñoz", "CONDUCTOR", 15);

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
            var o = JObject.Parse(GestionUsuarios.JsonUsuarioNuevo("admin2", "Ana", "ADMIN", null));

            Assert.Equal(JTokenType.Null, o["idConductor"].Type);
            Assert.DoesNotContain(o.Properties(), p => p.Name.IndexOf("contrasena", StringComparison.OrdinalIgnoreCase) >= 0
                                                    && p.Name != "requiereCambioContrasena");
            Assert.DoesNotContain(o.Properties(), p => p.Name.IndexOf("pass", StringComparison.OrdinalIgnoreCase) >= 0
                                                    || p.Name.IndexOf("hash", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public void ConsultaDeAuditoria_EstaAcotadaConTopYOrdenadaPorFechaDescendente()
        {
            var parametros = new List<SqlParameter>();
            string sql = Auditoria.ConstruirConsulta(new DateTime(2026, 11, 1), new DateTime(2026, 11, 16),
                "UPDATE", "Usuarios", "ana", Auditoria.MaximoEnPantalla, parametros);

            Assert.Contains("SELECT TOP (@max)", sql);
            Assert.EndsWith("ORDER BY FechaHora DESC, IdAuditoria DESC", sql.Trim());
            Assert.Equal(Auditoria.MaximoEnPantalla, parametros.Single(p => p.ParameterName == "@max").Value);
            Assert.Equal("%ana%", parametros.Single(p => p.ParameterName == "@Usuario").Value);
            Assert.Equal(6, parametros.Count);
        }

        [Fact]
        public void ConsultaDeAuditoria_SinFiltrosSoloLlevaElTope()
        {
            var parametros = new List<SqlParameter>();
            string sql = Auditoria.ConstruirConsulta(null, null, "", "", "", Auditoria.MaximoAlExportar, parametros);

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
    }
}
