using System;
using System.IO;
using WebSGV.Services.Exportacion;
using WebSGV.Tests.Integracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// B6-1: rango del período del Dashboard de exportación (por F.H. Programación) y del período con que se compara, y
    /// los viajes CANCELADO fuera de lo que se cuenta (no se cierran ni se reabren, no entran en las duraciones en Ecuador).
    /// </summary>
    public class DashboardExportacionServiceTests
    {
        [Fact]
        public void Mes_VaDelPrimeroAlPrimeroDelSiguiente_YSeComparaConElMesAnterior()
        {
            var r = DashboardExportacionService.RangoDelPeriodo(2026, 11);
            Assert.Equal(new DateTime(2026, 11, 1), r.Desde);
            Assert.Equal(new DateTime(2026, 12, 1), r.Hasta);
            Assert.Equal(new DateTime(2026, 10, 1), r.DesdeAnterior);
        }

        [Fact]
        public void Enero_SeComparaConDiciembreDelAnioAnterior()
        {
            var r = DashboardExportacionService.RangoDelPeriodo(2026, 1);
            Assert.Equal(new DateTime(2026, 1, 1), r.Desde);
            Assert.Equal(new DateTime(2026, 2, 1), r.Hasta);
            Assert.Equal(new DateTime(2025, 12, 1), r.DesdeAnterior);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(13)]
        public void SinMesValido_EsElAnioEntero_YSeComparaConElAnterior(int mes)
        {
            var r = DashboardExportacionService.RangoDelPeriodo(2026, mes);
            Assert.Equal(new DateTime(2026, 1, 1), r.Desde);
            Assert.Equal(new DateTime(2027, 1, 1), r.Hasta);
            Assert.Equal(new DateTime(2025, 1, 1), r.DesdeAnterior);
        }

        [Theory]
        [InlineData("CANCELADO", true)]
        [InlineData(" cancelado ", true)]
        [InlineData("EN_CURSO", false)]
        [InlineData("FINALIZADO", false)]
        [InlineData(null, false)]
        public void EsAnulado_SoloElCancelado(string estado, bool esperado)
        {
            Assert.Equal(esperado, SeguimientoViajesService.EsAnulado(estado));
        }

        private static string Fuente(params string[] ruta) =>
            File.ReadAllText(Path.Combine(BaseDePruebas.RaizRepo, Path.Combine(ruta)));

        [Fact]
        public void SeguimientoViaje_OcultaElBotonDeEstado_YRechazaElCambioSiElViajeEstaAnuladoEnLaBd()
        {
            string codigo = Fuente("WebSGV", "Views", "Exportacion", "SeguimientoViaje.aspx.cs");
            Assert.Contains("btnEstado.Visible = !SeguimientoViajesService.EsAnulado(v.Estado);", codigo);

            // En el clic, el estado se lee de la BD y el anulado se rechaza antes de llamar a CambiarEstado.
            int clic = codigo.IndexOf("protected void btnEstado_Click", StringComparison.Ordinal);
            Assert.True(clic > 0);
            int detalle = codigo.IndexOf("SeguimientoViajesService.ObtenerDetalle(IdSeguimiento)", clic, StringComparison.Ordinal);
            int rechazo = codigo.IndexOf("SeguimientoViajesService.EsAnulado(d.Resumen.Estado)", clic, StringComparison.Ordinal);
            int mensaje = codigo.IndexOf("SeguimientoViajesService.MensajeViajeAnulado", clic, StringComparison.Ordinal);
            int cambio = codigo.IndexOf("SeguimientoViajesService.CambiarEstado(", clic, StringComparison.Ordinal);
            Assert.True(detalle > clic && rechazo > detalle && mensaje > rechazo && cambio > mensaje,
                "btnEstado_Click debe leer el viaje, rechazar el anulado y recién después cambiar el estado.");
        }

        [Fact]
        public void SpCambiarEstado_NoSacaDeCanceladoAUnViaje_ConElMismoMensajeQueLaPagina()
        {
            string sp = Fuente("WebSGV", "Database", "StoredProcedures", "sp_SE_CambiarEstado.sql");
            Assert.Contains("AND (ISNULL(estado, '') <> 'CANCELADO' OR @estado = 'CANCELADO')", sp);
            Assert.Contains("N'" + SeguimientoViajesService.MensajeViajeAnulado + "'", sp);
        }

        [Fact]
        public void DuracionesEnEcuador_NoUsaLosViajesCancelados()
        {
            string codigo = Fuente("WebSGV", "Services", "Exportacion", "SeguimientoViajesService.cs");
            int i = codigo.IndexOf("public static Dictionary<string, List<double>> DuracionesEnEcuador()", StringComparison.Ordinal);
            Assert.True(i > 0);
            int fin = codigo.IndexOf("var res = new Dictionary", i, StringComparison.Ordinal);
            Assert.Contains("ISNULL(se.estado, '') <> 'CANCELADO'", codigo.Substring(i, fin - i));
        }
    }
}
