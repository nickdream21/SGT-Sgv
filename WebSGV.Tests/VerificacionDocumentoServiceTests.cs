using System;
using System.IO;
using System.Linq;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-20: VerificarDocumento es pública y sin sesión. Lo que el servicio le entrega es exactamente lo que la
    /// página muestra: agregar un dato (un id, el tipo de documento, el estado de la orden, el rol) debe ser una
    /// decisión a propósito, así que esta prueba fija la lista. Y la página ya no lee la BD por su cuenta.
    /// </summary>
    public class VerificacionDocumentoServiceTests
    {
        private static string[] Propiedades(Type t) =>
            t.GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        [Fact]
        public void DocumentoVerificado_SoloLoQueLaPaginaMuestra()
        {
            Assert.Equal(new[] { "Anulada", "Etapa", "FechaHoraFirmaUtc", "Firmas", "FirmoAdministracion", "HashDocumento",
                                 "NombreFirmante", "NumeroOrdenViaje", "Reemplazado" },
                Propiedades(typeof(DocumentoVerificado)));
            Assert.Equal(new[] { "Anulada", "CodigoVerificacion", "EsAnulacion", "Etapa", "FechaHoraFirmaUtc", "MotivoAnulacion",
                                 "NombreFirmante" },
                Propiedades(typeof(FirmaVerificada)));
        }

        [Fact]
        public void Pagina_UsaElServicio_ConSuLimiteYSinSql()
        {
            string cs = File.ReadAllText(Path.Combine(FuentesWeb.RaizWeb(), "Views", "VerificarDocumento.aspx.cs"));
            Assert.Contains("VerificacionDocumentoService.Consultar(codigo)", cs);
            Assert.DoesNotContain("sp_LD_VerificarDocumento", cs);
            Assert.DoesNotContain("SqlConnection", cs);
            // El límite por IP va antes de consultar, como siempre.
            int limite = cs.IndexOf("_limite.Reservar(", StringComparison.Ordinal);
            int consulta = cs.IndexOf("VerificacionDocumentoService.Consultar(", StringComparison.Ordinal);
            Assert.True(limite > 0 && limite < consulta, "El límite de consultas por IP debe ir antes de la consulta.");
        }
    }
}
