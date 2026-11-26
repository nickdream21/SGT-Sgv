using System;
using System.Globalization;
using System.Threading;
using WebSGV.Services.Common;
using Xunit;

namespace WebSGV.Tests
{
    public class MontoHelperTests
    {
        // ---- ValidarMonto ----

        [Fact]
        public void ValidarMonto_Nulo_RetornaCero()
        {
            Assert.Equal(0m, MontoHelper.ValidarMonto((decimal?)null, "campo"));
        }

        [Fact]
        public void ValidarMonto_Valido_RetornaElValor()
        {
            Assert.Equal(1500m, MontoHelper.ValidarMonto(1500m, "peajes"));
        }

        [Fact]
        public void ValidarMonto_EnElLimite_RetornaElValor()
        {
            Assert.Equal(MontoHelper.MontoMaximo, MontoHelper.ValidarMonto(MontoHelper.MontoMaximo, "campo"));
        }

        [Fact]
        public void ValidarMonto_Negativo_LanzaErrorDeNegocioConElCampo()
        {
            var ex = Assert.Throws<ErrorNegocioException>(() => MontoHelper.ValidarMonto(-1m, "descuento"));
            Assert.Contains("descuento", ex.Message);
            // Mensaje pensado para el usuario: MensajeErrorHelper lo muestra tal cual.
            Assert.Equal(ex.Message, MensajeError.ExtraerMensajeDeNegocio(ex));
        }

        [Fact]
        public void ValidarMonto_ExcedeMaximo_LanzaErrorDeNegocioConElCampo()
        {
            var ex = Assert.Throws<ErrorNegocioException>(
                () => MontoHelper.ValidarMonto(MontoHelper.MontoMaximo + 1m, "reintegro"));
            Assert.Contains("reintegro", ex.Message);
            Assert.Contains("máximo", ex.Message);
        }

        [Fact]
        public void ValidarMonto_SigueSiendoInvalidOperation_ParaLosCatchExistentes()
        {
            Assert.IsAssignableFrom<InvalidOperationException>(
                Assert.ThrowsAny<Exception>(() => MontoHelper.ValidarMonto(-1m, "x")));
        }

        // ---- LeerMonto (campos del formulario con tope) ----

        [Theory]
        [InlineData(null, "0")]
        [InlineData("", "0")]
        [InlineData("  ", "0")]
        [InlineData("10.50", "10.50")]
        [InlineData(" 250 ", "250")]
        [InlineData("9999999", "9999999")]
        public void LeerMonto_Validos(string entrada, string esperado)
        {
            Assert.Equal(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture),
                MontoHelper.LeerMonto(entrada, "Alimentación (S/)"));
        }

        [Theory]
        [InlineData("2000000000")]
        [InlineData("10000000")]
        [InlineData("10000000,00")]
        [InlineData("99999999999999999999999999999999")] // no cabe en un decimal: también es "sobre el tope"
        public void LeerMonto_SobreElTope_LanzaConElNombreDelCampo(string entrada)
        {
            var ex = Assert.Throws<ErrorNegocioException>(() => MontoHelper.LeerMonto(entrada, "Combustible (S/)"));
            Assert.Contains("Combustible (S/)", ex.Message);
            Assert.Contains("máximo", ex.Message);
        }

        // Las columnas de destino son de 2 decimales (DECIMAL(12,2) desde la migración 51): un tercer
        // decimal lo redondearía SQL Server sin avisar. Con la coma o el punto como decimal (E5-1), un
        // texto con 3 decimales es además ambiguo ("1,250": ¿mil doscientos cincuenta?): se rechaza con
        // el mensaje de formato, que dice cómo escribirlo.
        [Theory]
        [InlineData("12.345")]
        [InlineData("0.001")]
        [InlineData("9999998.999")]
        [InlineData("12.500")]
        public void LeerMonto_ConMasDeDosDecimales_Lanza(string entrada)
        {
            var ex = Assert.Throws<ErrorNegocioException>(() => MontoHelper.LeerMonto(entrada, "Peajes nacionales (S/)"));
            Assert.Contains(MontoHelper.MensajeFormatoMonto, ex.Message);
            Assert.Contains("Peajes nacionales (S/)", ex.Message);
        }

        [Theory]
        [InlineData("12.50", "12.50")]
        [InlineData("12.5", "12.5")]
        [InlineData("0.30", "0.30")]
        [InlineData("9999999.00", "9999999")]
        public void LeerMonto_ConHastaDosDecimales_Acepta(string entrada, string esperado)
        {
            Assert.Equal(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture),
                MontoHelper.LeerMonto(entrada, "Peajes nacionales (S/)"));
        }

        [Fact]
        public void ValidarMonto_ConMasDeDosDecimales_LanzaErrorDeNegocio()
        {
            // Montos que llegan por JSON (otros gastos, peajes) pasan por ValidarMonto sin LeerMonto.
            var ex = Assert.Throws<ErrorNegocioException>(() => MontoHelper.ValidarMonto(12.345m, "soles de gasto adicional"));
            Assert.Contains("máximo 2 decimales", ex.Message);
            Assert.Equal(ex.Message, MensajeError.ExtraerMensajeDeNegocio(ex));
        }

        // ---- «Factura y CPIC por despacho» (RegistroManualLote): monto opcional con las mismas reglas ----

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void LeerMontoOpcional_Vacio_EsSinValorYSinError(string entrada)
        {
            var errores = new System.Collections.Generic.List<string>();
            Assert.Null(WebSGV.Services.Common.MontoHelper.LeerMontoOpcional(entrada, "Valor del flete", errores));
            Assert.Empty(errores);
        }

        [Fact]
        public void LeerMontoOpcional_Valido_DevuelveElMonto()
        {
            var errores = new System.Collections.Generic.List<string>();
            Assert.Equal(1200.50m, WebSGV.Services.Common.MontoHelper.LeerMontoOpcional("1200.50", "Valor del flete", errores));
            Assert.Empty(errores);
        }

        [Theory]
        [InlineData("12.345", "sin separador de miles")]
        [InlineData("1.250,00", "sin separador de miles")]
        [InlineData("10000000", "máximo permitido")]
        [InlineData("-5", "negativo")]
        [InlineData("abc", "no es válido")]
        public void LeerMontoOpcional_Invalido_AgregaElErrorDelCampo(string entrada, string fragmento)
        {
            var errores = new System.Collections.Generic.List<string>();
            Assert.Null(WebSGV.Services.Common.MontoHelper.LeerMontoOpcional(entrada, "Valor total de la factura", errores));
            string error = Assert.Single(errores);
            Assert.Contains("Valor total de la factura", error);
            Assert.Contains(fragmento, error);
        }

        [Fact]
        public void LeerMonto_Negativo_Lanza()
        {
            var ex = Assert.Throws<ErrorNegocioException>(() => MontoHelper.LeerMonto("-5", "Movilidad (S/)"));
            Assert.Contains("negativo", ex.Message);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("1e400")]
        public void LeerMonto_NoNumerico_Lanza(string entrada)
        {
            var ex = Assert.Throws<ErrorNegocioException>(() => MontoHelper.LeerMonto(entrada, "Hospedaje ($)"));
            Assert.Contains("Hospedaje ($)", ex.Message);
        }

        // ---- E5-1: una sola regla, coma o punto decimal, sin miles ni exponente ----
        // La misma regla está en SGV.leerMonto (Scripts/paginas/SiteMaster.js); MarcadoMontosTests
        // comprueba que la expresión de los dos lados es la misma y la corre con estos casos.

        [Theory]
        [InlineData("12,50", "12.50")]
        [InlineData("12.5", "12.5")]
        [InlineData("1250", "1250")]
        [InlineData(" 7,05 ", "7.05")]
        [InlineData("0,5", "0.5")]
        [InlineData("12,", "12")]
        [InlineData("12.", "12")]
        [InlineData(",5", "0.5")]
        [InlineData("+3,10", "3.10")]
        [InlineData("1250,50", "1250.50")]   // el ejemplo del mensaje
        public void LeerMonto_ComaOPuntoDecimal_Acepta(string entrada, string esperado)
        {
            Assert.Equal(decimal.Parse(esperado, CultureInfo.InvariantCulture), MontoHelper.LeerMonto(entrada, "Combustible (S/)"));
        }

        [Theory]
        [InlineData("1,250")]
        [InlineData("1.250")]
        [InlineData("1.250,00")]
        [InlineData("1,250.00")]
        [InlineData("1e3")]
        [InlineData("12,345")]
        [InlineData("abc")]
        [InlineData("1 250")]
        [InlineData("12,5,0")]
        [InlineData("- 5")]
        [InlineData("5-")]
        [InlineData("(5)")]
        [InlineData("S/ 5")]
        [InlineData("\u0661\u0662")]   // dígitos arábigos orientales: el \d de .NET los aceptaría
        [InlineData(",")]
        public void LeerMonto_FueraDeLaRegla_LanzaConElMensajeDeFormato(string entrada)
        {
            var ex = Assert.Throws<ErrorNegocioException>(() => MontoHelper.LeerMonto(entrada, "Combustible (S/)"));
            Assert.Equal("El monto de «Combustible (S/)» no es válido. " + MontoHelper.MensajeFormatoMonto, ex.Message);
            Assert.Equal(ex.Message, MensajeError.ExtraerMensajeDeNegocio(ex));
        }

        [Fact]
        public void IntentarLeerMonto_Vacio_NoEsUnMonto()
        {
            Assert.False(MontoHelper.IntentarLeerMonto("", out _));
            Assert.False(MontoHelper.IntentarLeerMonto(null, out _));
            Assert.False(MontoHelper.IntentarLeerMonto("   ", out _));
            // LeerMonto, en cambio, toma el campo vacío como 0.
            Assert.Equal(0m, MontoHelper.LeerMonto("", "x"));
        }

        [Fact]
        public void LeerMonto_Negativo_ConComa_SigueRechazandoseComoNegativo()
        {
            var ex = Assert.Throws<ErrorNegocioException>(() => MontoHelper.LeerMonto("-12,50", "Movilidad (S/)"));
            Assert.Contains("negativo", ex.Message);
        }

        [Theory]
        [InlineData("es-PE")]
        [InlineData("en-US")]
        [InlineData("es-ES")]
        [InlineData("de-DE")]
        public void LeerMonto_NoDependeDeLaCulturaDelHilo(string cultura)
        {
            var antes = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(cultura);
                Assert.Equal(12.50m, MontoHelper.LeerMonto("12,50", "x"));
                Assert.Equal(12.5m, MontoHelper.LeerMonto("12.5", "x"));
                Assert.Equal(1250m, MontoHelper.LeerMonto("1250", "x"));
                Assert.Equal(7.05m, MontoHelper.LeerMonto(" 7,05 ", "x"));
                Assert.Equal(0.5m, MontoHelper.LeerMonto("0,5", "x"));
                foreach (string rechazado in new[] { "1,250", "1.250", "1.250,00", "1,250.00", "1e3", "12,345", "abc", "1 250" })
                    Assert.Throws<ErrorNegocioException>(() => MontoHelper.LeerMonto(rechazado, "x"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = antes;
            }
        }
    }
}
