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
        public void Sobre_NoPideQuienLoLleva()
        {
            // Basta el monto: el conductor lo reconoce al firmar su conformidad.
            Assert.Null(MovimientosViajeService.Validar(Entrega("SEGUNDO_DESPACHO", "SOBRE"), Ahora));
            Assert.Equal("Sobre", MovimientosViajeService.NombreMedio("SOBRE"));
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
            m.Medio = "SOBRE";
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
                                      MontoSoles = 200m, Fecha = Ahora },
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
            Assert.Contains("sobre", r.DescDespacho);
        }

        [Fact]
        public void Huella_CambiaSiCambiaElMontoOElMedioDeUnaEntrega()
        {
            DetalleLiquidacion Detalle(decimal monto, string medio = "SOBRE") => new DetalleLiquidacion
            {
                NumeroOrdenViaje = "OV-1",
                Entregas = new List<MovimientoViaje>
                {
                    new MovimientoViaje { IdMovimiento = 1, Tipo = MovimientosViajeService.Entrega, Concepto = "SEGUNDO_DESPACHO",
                                          Medio = medio, MontoSoles = monto, Fecha = Ahora }
                }
            };
            Assert.NotEqual(HuellaLiquidacion.Calcular(Detalle(200m)), HuellaLiquidacion.Calcular(Detalle(201m)));
            Assert.NotEqual(HuellaLiquidacion.Calcular(Detalle(200m)), HuellaLiquidacion.Calcular(Detalle(200m, "EFECTIVO")));
            Assert.Equal(HuellaLiquidacion.Calcular(Detalle(200m)), HuellaLiquidacion.Calcular(Detalle(200m)));
        }
    }
}
