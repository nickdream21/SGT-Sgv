using System.Collections.Generic;
using WebSGV.Models.Conductor;
using WebSGV.Services.Common;
using WebSGV.Services.Conductor;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Reglas que el servidor aplica a la liquidación del conductor sin fiarse del navegador:
    /// viaje nacional (sin peajes ni dólares) y largo de los textos.
    /// </summary>
    public class ReglasLiquidacionConductorTests
    {
        // ---- Nacional o internacional ----

        [Fact]
        public void EsInternacional_NingunViajeInternacionalNiCruceGps_EsNacional()
        {
            Assert.False(ReglasLiquidacionConductor.EsInternacional(new[] { false, false }, false));
        }

        [Fact]
        public void EsInternacional_AlgunViajeInternacionalEnLaBd_EsInternacional()
        {
            Assert.True(ReglasLiquidacionConductor.EsInternacional(new[] { false, true }, false));
        }

        [Fact]
        public void EsInternacional_ViajeNacionalPeroElGpsCruzaAEcuador_EsInternacional()
        {
            // Crisar sin relevo: el viaje no está marcado internacional, pero el tracto pasó a Ecuador.
            Assert.True(ReglasLiquidacionConductor.EsInternacional(new[] { false }, true));
        }

        [Fact]
        public void EsInternacional_SinViajes_EsNacional()
        {
            Assert.False(ReglasLiquidacionConductor.EsInternacional(null, false));
        }

        // ---- DecidirAmbito: todas las combinaciones ----

        /// <summary>Si la BD o el GPS dicen internacional, se guarda todo, diga lo que diga lo demás (16 casos).</summary>
        [Theory]
        [InlineData(true, false, false, false, false)]
        [InlineData(true, false, false, false, true)]
        [InlineData(true, false, false, true, false)]
        [InlineData(true, false, false, true, true)]
        [InlineData(true, false, true, false, false)]
        [InlineData(true, false, true, false, true)]
        [InlineData(true, false, true, true, false)]
        [InlineData(true, false, true, true, true)]
        [InlineData(false, true, false, false, false)]
        [InlineData(false, true, false, false, true)]
        [InlineData(false, true, false, true, false)]
        [InlineData(false, true, false, true, true)]
        [InlineData(false, true, true, false, false)]
        [InlineData(false, true, true, false, true)]
        [InlineData(false, true, true, true, false)]
        [InlineData(false, true, true, true, true)]
        public void DecidirAmbito_ServidorDiceInternacional_SeGuarda(bool viajeInternacional, bool cruzaFrontera,
            bool gpsFallo, bool paginaInternacional, bool hayPeajesODolares)
        {
            var d = ReglasLiquidacionConductor.DecidirAmbito(new[] { false, viajeInternacional }, cruzaFrontera,
                gpsFallo, paginaInternacional, hayPeajesODolares);
            Assert.Equal(ResultadoAmbito.Internacional, d.Resultado);
            Assert.Null(d.Mensaje);
        }

        /// <summary>El servidor dice nacional (ni la BD ni el GPS ven Ecuador): los 8 casos restantes.</summary>
        [Theory]
        // GPS respondió, página nacional: se ponen en 0 (campos ocultos; si llegan con valor es manipulación).
        [InlineData(false, false, false, ResultadoAmbito.NacionalAplicarCeros, null)]
        [InlineData(false, false, true, ResultadoAmbito.NacionalAplicarCeros, null)]
        // GPS respondió, página internacional: el ámbito cambió entre la carga y el envío.
        [InlineData(false, true, false, ResultadoAmbito.Rechazar, ReglasLiquidacionConductor.MensajeAmbitoCambio)]
        [InlineData(false, true, true, ResultadoAmbito.Rechazar, ReglasLiquidacionConductor.MensajeAmbitoCambio)]
        // GPS falló, página nacional y nada que borrar: no hay nada que perder.
        [InlineData(true, false, false, ResultadoAmbito.NacionalAplicarCeros, null)]
        // GPS falló y hay peajes/dólares: podría ser internacional (el GPS era la única fuente): no se borra.
        [InlineData(true, false, true, ResultadoAmbito.Rechazar, ReglasLiquidacionConductor.MensajeGpsSinRespuesta)]
        // GPS falló y la página decía internacional: la causa es el GPS, no un cambio de ámbito.
        [InlineData(true, true, false, ResultadoAmbito.Rechazar, ReglasLiquidacionConductor.MensajeGpsSinRespuesta)]
        [InlineData(true, true, true, ResultadoAmbito.Rechazar, ReglasLiquidacionConductor.MensajeGpsSinRespuesta)]
        public void DecidirAmbito_ServidorDiceNacional(bool gpsFallo, bool paginaInternacional, bool hayPeajesODolares,
            ResultadoAmbito esperado, string mensaje)
        {
            var d = ReglasLiquidacionConductor.DecidirAmbito(new[] { false, false }, false,
                gpsFallo, paginaInternacional, hayPeajesODolares);
            Assert.Equal(esperado, d.Resultado);
            Assert.Equal(mensaje, d.Mensaje);
        }

        [Fact]
        public void TienePeajesODolares_DetectaCadaFuente()
        {
            Assert.False(ReglasLiquidacionConductor.TienePeajesODolares(new LiquidacionConductorInput { AlimentacionSoles = 50m }));
            Assert.True(ReglasLiquidacionConductor.TienePeajesODolares(new LiquidacionConductorInput { PeajesSoles = 1m }));
            Assert.True(ReglasLiquidacionConductor.TienePeajesODolares(new LiquidacionConductorInput { AlimentacionDolares = 25m }));
            Assert.True(ReglasLiquidacionConductor.TienePeajesODolares(new LiquidacionConductorInput
            {
                GastosAdicionales = new List<GastoAdicionalData> { new GastoAdicionalData { Categoria = "x", Soles = 1m, Dolares = 2m } }
            }));
            Assert.True(ReglasLiquidacionConductor.TienePeajesODolares(new LiquidacionConductorInput
            {
                GastosFinancieros = new List<GastoFinanciero> { new GastoFinanciero { Categoria = "Peajes", Soles = 10m } }
            }));
            Assert.False(ReglasLiquidacionConductor.TienePeajesODolares(new LiquidacionConductorInput
            {
                GastosFinancieros = new List<GastoFinanciero> { new GastoFinanciero { Categoria = "hospedaje", Soles = 10m } }
            }));
            Assert.False(ReglasLiquidacionConductor.TienePeajesODolares(null));
        }

        [Fact]
        public void AplicarViajeNacional_PoneEnCeroPeajesYDolares()
        {
            var input = InputConTodo();

            ReglasLiquidacionConductor.AplicarViajeNacional(input);

            Assert.Equal(0m, input.PeajesSoles);
            Assert.Equal(0m, input.PeajesDolares);
            Assert.Equal("", input.DescPeajes);
            Assert.Equal(0m, input.AlimentacionDolares);
            Assert.Equal(0m, input.ApoyoSeguridadDolares);
            Assert.Equal(0m, input.ReparacionesDolares);
            Assert.Equal(0m, input.MovilidadDolares);
            Assert.Equal(0m, input.EncapadaDolares);
            Assert.Equal(0m, input.HospedajeDolares);
            Assert.Equal(0m, input.CombustibleDolares);

            // Lo que es en soles queda igual.
            Assert.Equal(200m, input.AlimentacionSoles);
            Assert.Equal(300m, input.CombustibleSoles);
        }

        [Fact]
        public void AplicarViajeNacional_OtrosGastos_SinDolaresYSeDescartanLosQueQuedanSinMonto()
        {
            var input = InputConTodo();

            ReglasLiquidacionConductor.AplicarViajeNacional(input);

            // "Lavado" (S/ 30 + $ 10) queda solo en soles; "Ecuador" (solo $) se descarta, como hace el JS.
            var g = Assert.Single(input.GastosAdicionales);
            Assert.Equal("Lavado", g.Categoria);
            Assert.Equal(30m, g.Soles);
            Assert.Equal(0m, g.Dolares);
        }

        [Fact]
        public void AplicarViajeNacional_QuitaLosDetallesDePeajeYLosDolaresDeLosComprobantes()
        {
            var input = InputConTodo();

            ReglasLiquidacionConductor.AplicarViajeNacional(input);

            var f = Assert.Single(input.GastosFinancieros);
            Assert.Equal("hospedaje", f.Categoria);
            Assert.Equal(80m, f.Soles);
            Assert.Equal(0m, f.Dolares);
        }

        [Fact]
        public void AplicarViajeNacional_NoTocaLoDeLaAdministracion()
        {
            var input = InputConTodo();

            ReglasLiquidacionConductor.AplicarViajeNacional(input);

            Assert.Equal(100m, input.DespachoDolares);
            Assert.Equal(5m, input.ReintegroDolares);
            Assert.Equal("Peaje no declarado", input.MotivoReintegro);
        }

        // ---- Largo de los textos ----

        [Fact]
        public void ValidarTextos_EnElLimite_NoLanza()
        {
            var input = InputConTodo();
            input.GastosAdicionales[0].Categoria = new string('c', ReglasLiquidacionConductor.LargoConcepto);
            input.GastosAdicionales[0].Descripcion = new string('d', ReglasLiquidacionConductor.LargoDetalle);
            input.DescAlimentacion = new string('a', ReglasLiquidacionConductor.LargoDetalle);

            ReglasLiquidacionConductor.ValidarTextos(input);
        }

        [Fact]
        public void ValidarTextos_ConceptoDeOtroGastoMuyLargo_LanzaConElCampo()
        {
            var input = InputConTodo();
            input.GastosAdicionales[0].Categoria = new string('c', ReglasLiquidacionConductor.LargoConcepto + 1);

            var ex = Assert.Throws<ErrorNegocioException>(() => ReglasLiquidacionConductor.ValidarTextos(input));
            Assert.Contains("Concepto de otro gasto", ex.Message);
            Assert.Contains("101", ex.Message);
        }

        [Fact]
        public void ValidarTextos_DetalleDeUnGastoPrincipalMuyLargo_Lanza()
        {
            var input = InputConTodo();
            input.DescHospedaje = new string('h', ReglasLiquidacionConductor.LargoDetalle + 1);

            var ex = Assert.Throws<ErrorNegocioException>(() => ReglasLiquidacionConductor.ValidarTextos(input));
            Assert.Contains("hospedaje", ex.Message);
        }

        [Fact]
        public void ValidarTextos_ComprobanteMuyLargo_Lanza()
        {
            var input = InputConTodo();
            input.GastosFinancieros[0].Comprobante = new string('9', ReglasLiquidacionConductor.LargoComprobante + 1);

            var ex = Assert.Throws<ErrorNegocioException>(() => ReglasLiquidacionConductor.ValidarTextos(input));
            Assert.Contains("comprobante", ex.Message);
        }

        private static LiquidacionConductorInput InputConTodo() => new LiquidacionConductorInput
        {
            PeajesSoles = 150m, PeajesDolares = 20m, DescPeajes = "Nac: S/150.00 | Ext: $20.00",
            AlimentacionSoles = 200m, AlimentacionDolares = 15m,
            ApoyoSeguridadDolares = 1m, ReparacionesDolares = 2m, MovilidadDolares = 3m,
            EncapadaDolares = 4m, HospedajeDolares = 6m,
            CombustibleSoles = 300m, CombustibleDolares = 40m,
            DespachoDolares = 100m, ReintegroDolares = 5m, MotivoReintegro = "Peaje no declarado",
            GastosAdicionales = new List<GastoAdicionalData>
            {
                new GastoAdicionalData { Categoria = "Lavado", Soles = 30m, Dolares = 10m },
                new GastoAdicionalData { Categoria = "Ecuador", Soles = 0m, Dolares = 12m }
            },
            GastosFinancieros = new List<GastoFinanciero>
            {
                new GastoFinanciero { Categoria = "hospedaje", Comprobante = "H-1", Soles = 80m, Dolares = 9m },
                new GastoFinanciero { Categoria = "Peajes", Estacion = "Peajes Nacionales (Perú)", Soles = 150m }
            }
        };
    }
}
