using System;
using System.Collections.Generic;
using WebSGV.Models.Liquidaciones;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Reglas del dinero del viaje (entregas al conductor y pagos directos), sin BD.</summary>
    public class MovimientosViajeTests
    {
        private static readonly DateTime Ahora = new DateTime(2026, 11, 11, 18, 0, 0);

        private static MovimientoViaje Entrega(string concepto = "ADELANTO", string medio = "EFECTIVO", decimal soles = 300m) => new MovimientoViaje
        {
            Tipo = MovimientosViajeService.Entrega, IdConductor = 10, Fecha = Ahora.AddHours(-5),
            Concepto = concepto, Medio = medio, MontoSoles = soles
        };

        [Fact]
        public void EntregaEnEfectivo_EsValida() =>
            Assert.Null(MovimientosViajeService.Validar(Entrega(), Ahora));

        [Fact]
        public void Sobre_ExigeOtroConductorQueLoLleve()
        {
            var m = Entrega("SEGUNDO_DESPACHO", "SOBRE");
            Assert.Contains("lleva el sobre", MovimientosViajeService.Validar(m, Ahora));
            m.IdConductorPortador = 10;
            Assert.Contains("debe ser otro", MovimientosViajeService.Validar(m, Ahora));
            m.IdConductorPortador = 20;
            Assert.Null(MovimientosViajeService.Validar(m, Ahora));
        }

        [Fact]
        public void YapeYTransferencia_ExigenNumeroDeOperacion()
        {
            var m = Entrega(medio: "YAPE");
            Assert.Contains("número de operación", MovimientosViajeService.Validar(m, Ahora));
            m.NumeroOperacion = "12345678";
            Assert.Null(MovimientosViajeService.Validar(m, Ahora));
        }

        [Fact]
        public void PagoDirecto_ExigeProveedorYNoVaEnSobre()
        {
            var m = new MovimientoViaje
            {
                Tipo = MovimientosViajeService.PagoDirecto, IdConductor = 10, Fecha = Ahora.AddHours(-1),
                Concepto = "REPARACION", Medio = "YAPE", NumeroOperacion = "998877", MontoSoles = 40m
            };
            Assert.Contains("proveedor", MovimientosViajeService.Validar(m, Ahora));
            m.NombreProveedor = "Llantería El Rápido";
            m.RucProveedor = "12345";
            Assert.Contains("11 dígitos", MovimientosViajeService.Validar(m, Ahora));
            m.RucProveedor = "20123456789";
            Assert.Null(MovimientosViajeService.Validar(m, Ahora));
            m.Medio = "SOBRE"; m.IdConductorPortador = 20;
            Assert.Contains("no va en sobre", MovimientosViajeService.Validar(m, Ahora));
        }

        [Theory]
        [InlineData(0, 0, "Indique el monto")]
        [InlineData(-5, 0, "negativos")]
        public void Montos_Invalidos(decimal soles, decimal dolares, string error)
        {
            var m = Entrega(soles: soles);
            m.MontoDolares = dolares;
            Assert.Contains(error, MovimientosViajeService.Validar(m, Ahora));
        }

        [Fact]
        public void FechaFutura_ConceptoDesconocido_SeRechazan()
        {
            var futura = Entrega(); futura.Fecha = Ahora.AddDays(1);
            Assert.Contains("futuro", MovimientosViajeService.Validar(futura, Ahora));
            var concepto = Entrega("LLANTAS"); // concepto de pago directo, no de entrega
            Assert.Contains("concepto", MovimientosViajeService.Validar(concepto, Ahora));
        }

        [Fact]
        public void SumarEntregas_AdelantoYSegundoDespachoVanAlAdelanto_YLosPagosDirectosNoSuman()
        {
            var lista = new List<MovimientoViaje>
            {
                Entrega("ADELANTO", "EFECTIVO", 300m),
                new MovimientoViaje { Tipo = MovimientosViajeService.Entrega, Concepto = "SEGUNDO_DESPACHO", Medio = "SOBRE",
                                      NombrePortador = "JUAN PEREZ", MontoSoles = 200m, Fecha = Ahora },
                new MovimientoViaje { Tipo = MovimientosViajeService.Entrega, Concepto = "PRESTAMO", Medio = "YAPE", MontoSoles = 50m, Fecha = Ahora },
                new MovimientoViaje { Tipo = MovimientosViajeService.Entrega, Concepto = "OTROS", Medio = "EFECTIVO", MontoDolares = 20m, Fecha = Ahora },
                new MovimientoViaje { Tipo = MovimientosViajeService.PagoDirecto, Concepto = "REPARACION", Medio = "YAPE", MontoSoles = 40m, Fecha = Ahora },
                new MovimientoViaje { Tipo = MovimientosViajeService.Entrega, Concepto = "ADELANTO", Medio = "EFECTIVO", MontoSoles = 999m, Activo = false, Fecha = Ahora }
            };

            var r = MovimientosViajeService.SumarEntregas(lista);

            Assert.Equal(500m, r.DespachoSoles);
            Assert.Equal(50m, r.PrestamoSoles);
            Assert.Equal(20m, r.OtrosDolares);
            Assert.Equal(0m, r.MensualidadSoles);
            Assert.Contains("JUAN PEREZ", r.DescDespacho);
        }

        [Fact]
        public void Huella_CambiaSiCambiaUnaEntregaOQuienLlevoElSobre()
        {
            DetalleLiquidacion Detalle(int portador) => new DetalleLiquidacion
            {
                NumeroOrdenViaje = "OV-1",
                Entregas = new List<MovimientoViaje>
                {
                    new MovimientoViaje { IdMovimiento = 1, Tipo = MovimientosViajeService.Entrega, Concepto = "SEGUNDO_DESPACHO",
                                          Medio = "SOBRE", IdConductorPortador = portador, MontoSoles = 200m, Fecha = Ahora }
                }
            };
            Assert.NotEqual(HuellaLiquidacion.Calcular(Detalle(20)), HuellaLiquidacion.Calcular(Detalle(21)));
            Assert.Equal(HuellaLiquidacion.Calcular(Detalle(20)), HuellaLiquidacion.Calcular(Detalle(20)));
        }
    }
}
