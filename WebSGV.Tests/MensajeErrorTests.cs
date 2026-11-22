using System;
using WebSGV.Services.Common;
using Xunit;

namespace WebSGV.Tests
{
    public class MensajeErrorTests
    {
        [Fact]
        public void ErrorDeNegocio_SeMuestraTalCual()
        {
            var ex = new ErrorNegocioException("El número de pedido ya está asociado a otra factura.");
            Assert.Equal("El número de pedido ya está asociado a otra factura.", MensajeError.ExtraerMensajeDeNegocio(ex));
        }

        [Fact]
        public void ErrorDeNegocio_EnvueltoEnErrorTecnico_SeEncuentraEnLasInternas()
        {
            var negocio = new ErrorNegocioException("La hoja del archivo Excel está vacía.");
            var envoltorio = new Exception("Error al procesar los datos: " + negocio.Message, negocio);

            Assert.Equal("La hoja del archivo Excel está vacía.", MensajeError.ExtraerMensajeDeNegocio(envoltorio));
        }

        [Fact]
        public void ErrorTecnico_NoSeMuestra()
        {
            var ex = new Exception("Invalid column name 'idPlantaX'. Cannot open database \"sgvTransporte\"");
            Assert.Null(MensajeError.ExtraerMensajeDeNegocio(ex));
        }

        [Fact]
        public void InvalidOperationExceptionComun_NoSeConsideraDeNegocio()
        {
            // Solo ErrorNegocioException (que hereda de ella) marca un mensaje como mostrable.
            Assert.Null(MensajeError.ExtraerMensajeDeNegocio(new InvalidOperationException("Connection is not open.")));
        }

        [Fact]
        public void ErrorDeNegocio_SigueSiendoInvalidOperationException()
        {
            // Para no romper los catch (InvalidOperationException) existentes.
            Assert.IsAssignableFrom<InvalidOperationException>(new ErrorNegocioException("x"));
        }

        [Fact]
        public void Nulo_DevuelveNull()
        {
            Assert.Null(MensajeError.ExtraerMensajeDeNegocio(null));
        }

        // ===== E4-8: sin texto técnico en el resumen del GPS ni en el reporte personalizado =====

        [Fact]
        public void GpsAutomatico_ResumenDeUnErrorTecnico_SinSuTexto()
        {
            var ex = new System.IO.IOException(@"No se pudo abrir C:\inetpub\sgv\App_Data\x.json: acceso denegado");
            var textos = WebSGV.Services.Exportacion.ActualizacionGpsAutomatica.TextosTrasError(ex);
            Assert.Equal("Error: " + MensajeError.Generico, textos.Resumen);
            Assert.Equal("No se pudo consultar el GPS. " + MensajeError.Generico, textos.Mensaje);
            Assert.DoesNotContain("inetpub", textos.Resumen + textos.Mensaje);
        }

        [Fact]
        public void GpsAutomatico_ResumenDeUnErrorDeNegocio_ConSuTexto()
        {
            var textos = WebSGV.Services.Exportacion.ActualizacionGpsAutomatica.TextosTrasError(
                new ErrorNegocioException("No hay viajes abiertos con tracto."));
            Assert.Equal("Error: No hay viajes abiertos con tracto.", textos.Resumen);
            Assert.Equal("No se pudo consultar el GPS. No hay viajes abiertos con tracto.", textos.Mensaje);
        }

        [Fact]
        public void ReportePersonalizado_FiltrosInvalidosSonDeNegocio_YNingunArgumentExceptionLlegaAlUsuario()
        {
            string cs = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Integracion.BaseDePruebas.RaizRepo, "WebSGV", "Views", "ReportesOrdenesViaje.aspx.cs"));
            Assert.DoesNotContain("catch (ArgumentException", cs);
            Assert.DoesNotContain("throw new ArgumentException(", cs);
            Assert.DoesNotContain("ResponderSolicitudInvalida(ex.Message)", cs);
            Assert.Contains("throw new ErrorNegocioException(\"El formato de exportación personalizado no es válido.\")", cs);
        }
    }
}
