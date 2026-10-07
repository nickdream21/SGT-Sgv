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
    }
}
