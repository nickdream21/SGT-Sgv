using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.Liquidaciones;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Reglas de las firmas de la liquidación sin BD: código de verificación, huella de lo que se
    /// firma, motivos obligatorios de los ajustes y textos que acepta cada firmante.
    /// </summary>
    public class FirmaLiquidacionTests
    {
        // ---------------- Código de verificación ----------------

        [Fact]
        public void CodigoGenerado_TieneFormatoYEsValido()
        {
            string codigo = CodigoVerificacion.Generar();

            Assert.Matches("^[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}$", codigo);
            Assert.Equal(codigo, CodigoVerificacion.Normalizar(codigo));
        }

        [Fact]
        public void CodigosGenerados_NoSeRepiten()
        {
            var codigos = Enumerable.Range(0, 2000).Select(_ => CodigoVerificacion.Generar()).ToList();
            Assert.Equal(codigos.Count, codigos.Distinct().Count());
        }

        [Theory]
        [InlineData("abcd efgh 2345", "ABCD-EFGH-2345")]
        [InlineData(" ABCD-EFGH-2345 ", "ABCD-EFGH-2345")]
        [InlineData("abcdefgh2345", "ABCD-EFGH-2345")]
        public void Normalizar_AceptaMinusculasEspaciosYGuiones(string ingresado, string esperado) =>
            Assert.Equal(esperado, CodigoVerificacion.Normalizar(ingresado));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("ABCD-EFGH")]               // corto
        [InlineData("ABCD-EFGH-23450")]         // largo
        [InlineData("ABCD-EFGH-0001")]          // 0 y 1 no existen en el alfabeto
        [InlineData("ABCD-EFGH-IO23")]          // I y O tampoco
        [InlineData("'; DROP TABLE x;--")]
        public void Normalizar_RechazaCodigosInvalidos(string ingresado) =>
            Assert.Null(CodigoVerificacion.Normalizar(ingresado));

        // ---------------- Huella de la liquidación ----------------

        private static DetalleLiquidacion Liquidacion() => new DetalleLiquidacion
        {
            NumeroOrdenViaje = "OV-2026-000123",
            DespachoSoles = 800m, DespachoDolares = 60m,
            GastosPeajesSoles = 150m, GastosCombustibleDolares = 50m,
            DescuentoSoles = 40m, DescuentoMotivo = "Alimentación sin comprobante",
            ReintegroSoles = 15m, ReintegroMotivo = "Peaje no declarado",
            NotaAprobacion = "Revisado",
            DetallesIngresosAdicionales = new List<ItemAdicional>
            {
                new ItemAdicional { Nombre = "Bono", Soles = 20m },
                new ItemAdicional { Nombre = "Apoyo", Soles = 10m }
            },
            DetallesGastosAdicionales = new List<ItemAdicional> { new ItemAdicional { Nombre = "Lavado", Soles = 30m } }
        };

        [Fact]
        public void Huella_EsEstable_YNoDependeDelOrdenDeLosAdicionales()
        {
            var a = Liquidacion();
            var b = Liquidacion();
            b.DetallesIngresosAdicionales.Reverse();

            Assert.Equal(64, HuellaLiquidacion.Calcular(a).Length);
            Assert.Equal(HuellaLiquidacion.Calcular(a), HuellaLiquidacion.Calcular(b));
        }

        [Fact]
        public void Huella_CambiaSiCambiaUnMontoUnMotivoOLaNota()
        {
            string original = HuellaLiquidacion.Calcular(Liquidacion());

            var monto = Liquidacion(); monto.DescuentoSoles = 41m;
            var motivo = Liquidacion(); motivo.DescuentoMotivo = "Otro motivo";
            var entregado = Liquidacion(); entregado.DespachoSoles = 900m;
            var nota = Liquidacion(); nota.NotaAprobacion = "Otra nota";
            var gasto = Liquidacion(); gasto.DetallesGastosAdicionales[0].Soles = 31m;

            foreach (var cambiada in new[] { monto, motivo, entregado, nota, gasto })
                Assert.NotEqual(original, HuellaLiquidacion.Calcular(cambiada));
        }

        [Fact]
        public void Huella_IgnoraEspaciosAlrededorDeLosTextos()
        {
            var a = Liquidacion();
            var b = Liquidacion(); b.DescuentoMotivo = "  Alimentación sin comprobante ";
            Assert.Equal(HuellaLiquidacion.Calcular(a), HuellaLiquidacion.Calcular(b));
        }

        // ---------------- Motivos de los ajustes ----------------

        [Fact]
        public void Ajustes_SinMontos_NoPidenMotivo() =>
            Assert.Null(ReglasFirma.ValidarMotivosAjuste(0m, 0m, null, 0m, 0m, null));

        [Theory]
        [InlineData(40, 0, null)]
        [InlineData(0, 5, "corto")]
        [InlineData(40, 0, "         ")]
        public void Descuento_SinMotivoSuficiente_SeRechaza(decimal soles, decimal dolares, string motivo)
        {
            string error = ReglasFirma.ValidarMotivosAjuste(soles, dolares, motivo, 0m, 0m, null);
            Assert.Contains("motivo del descuento", error);
        }

        [Fact]
        public void Reintegro_SinMotivo_SeRechaza() =>
            Assert.Contains("motivo del reintegro",
                ReglasFirma.ValidarMotivosAjuste(0m, 0m, null, 15m, 0m, ""));

        [Fact]
        public void Ajustes_ConMotivos_SeAceptan() =>
            Assert.Null(ReglasFirma.ValidarMotivosAjuste(40m, 0m, "Alimentación sin comprobante", 15m, 0m, "Peaje no declarado"));

        [Fact]
        public void Motivo_DemasiadoLargo_SeRechaza() =>
            Assert.NotNull(ReglasFirma.ValidarMotivosAjuste(40m, 0m, new string('x', 501), 0m, 0m, null));

        // ---------------- Textos y dispositivo ----------------

        [Fact]
        public void Consentimiento_LlevaElNumeroDeOrdenYElTextoAceptado()
        {
            string c = TextosFirma.Consentimiento(EtapaFirma.Conformidad, "OV-2026-000123");

            Assert.StartsWith("Orden de viaje N.° OV-2026-000123. ", c);
            Assert.EndsWith(TextosFirma.Conformidad, c);
            Assert.True(c.Length <= 1000);
        }

        [Fact]
        public void TextoDeConformidad_AutorizaLosDescuentos() =>
            Assert.Contains("autorizo que los descuentos indicados se apliquen", TextosFirma.Conformidad);

        [Theory]
        [InlineData("Mozilla/5.0 (Linux; Android 13; SM-A135M) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Mobile Safari/537.36", "Chrome en Android")]
        [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36 Edg/130.0", "Edge en Windows")]
        [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1", "Safari en iPhone")]
        [InlineData(null, "No identificado")]
        public void DescribirDispositivo_ResumeElNavegador(string ua, string esperado) =>
            Assert.Equal(esperado, ReglasFirma.DescribirDispositivo(ua));

        [Fact]
        public void NombreDeEtapa_CubreTodasLasEtapas()
        {
            Assert.Contains("Declaración", EtapaFirma.Nombre(EtapaFirma.Declaracion));
            Assert.Contains("Revisión", EtapaFirma.Nombre(EtapaFirma.Revision));
            Assert.Contains("Conformidad", EtapaFirma.Nombre(EtapaFirma.Conformidad));
            Assert.Contains("sistema anterior", EtapaFirma.Nombre(null));
        }
    }
}
