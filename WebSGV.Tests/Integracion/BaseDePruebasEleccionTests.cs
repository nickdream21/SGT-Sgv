using System;
using Xunit;
using Xunit.Abstractions;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Qué conexión eligen los tests de integración (U-098), sin conectarse: las fuentes se inyectan.
    /// Por defecto la copia local sgvEnsayo_pruebas; sgvActualizada solo con SGV_TEST_BD=actualizada.
    /// </summary>
    public class BaseDePruebasEleccionTests
    {
        private const string Actualizada = @"Data Source=sgvActualizada.mssql.somee.com;Initial Catalog=sgvActualizada;User ID=x;Password=clave-secreta";
        private static readonly string CopiaLocal = BaseDePruebas.CadenaCopiaLocal(@"(localdb)\MSSQLLocalDB", 15);

        private readonly ITestOutputHelper _salida;
        public BaseDePruebasEleccionTests(ITestOutputHelper salida) { _salida = salida; }

        private static Func<string> NoDebeLlamarse(string fuente) =>
            () => throw new InvalidOperationException("No debía leerse " + fuente);

        [Fact]
        public void PorDefecto_UsaLaCopiaLocal_YNoLeeConnectionStringsConfig()
        {
            var e = BaseDePruebas.Elegir(null, null, NoDebeLlamarse("connectionStrings.config"), () => CopiaLocal);

            Assert.Equal(CopiaLocal, e.Cadena);
            Assert.Null(e.Motivo);
            Assert.Contains(BaseDePruebas.BdCopiaLocal, e.Origen);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SinCopiaLocal_SeOmiteConElMotivo_AunqueHayaConnectionStringsConfig(string varBd)
        {
            var e = BaseDePruebas.Elegir("", varBd, () => Actualizada, () => null);

            Assert.Null(e.Cadena);
            Assert.Equal(BaseDePruebas.MotivoSinCopiaLocal, e.Motivo);
            Assert.Contains("preparar-bd-pruebas.ps1", e.Motivo);
            Assert.Contains("SGV_TEST_BD=actualizada", e.Motivo);
        }

        [Theory]
        [InlineData("actualizada")]
        [InlineData("ACTUALIZADA")]
        [InlineData(" Actualizada ")]
        public void ConSgvTestBdActualizada_UsaConnectionStringsConfig_YNoBuscaLaCopia(string varBd)
        {
            var e = BaseDePruebas.Elegir(null, varBd, () => Actualizada, NoDebeLlamarse("la copia local"));

            Assert.Equal(Actualizada, e.Cadena);
            Assert.Contains("connectionStrings.config", e.Origen);
        }

        [Fact]
        public void ConSgvTestBdActualizada_SinConfig_SeOmite()
        {
            var e = BaseDePruebas.Elegir(null, "actualizada", () => null, NoDebeLlamarse("la copia local"));

            Assert.Null(e.Cadena);
            Assert.Contains("connectionStrings.config", e.Motivo);
        }

        [Fact]
        public void SgvTestConnStr_TienePrioridadSobreTodo()
        {
            string ensayo = @"Data Source=.;Initial Catalog=sgvEnsayo_20261125_1000;Integrated Security=True";
            var e = BaseDePruebas.Elegir(ensayo, "actualizada", NoDebeLlamarse("connectionStrings.config"), NoDebeLlamarse("la copia local"));

            Assert.Equal(ensayo, e.Cadena);
            Assert.Equal("SGV_TEST_CONNSTR", e.Origen);
        }

        [Fact]
        public void SgvTestBdDesconocido_SeOmite_SinTocarNingunaFuente()
        {
            var e = BaseDePruebas.Elegir(null, "sgvTransporte", NoDebeLlamarse("connectionStrings.config"), NoDebeLlamarse("la copia local"));

            Assert.Null(e.Cadena);
            Assert.Contains("no se reconoce", e.Motivo);
        }

        [Theory]   // las protecciones de destino siguen valiendo para cada fuente
        [InlineData(@"Data Source=sgvTransporte.mssql.somee.com;Initial Catalog=sgvTransporte;User ID=x;Password=y", null)]
        [InlineData(null, "actualizada")]
        [InlineData(null, null)]
        public void UnDestinoNoPermitido_SeRechaza_VengaDeDondeVenga(string varConnStr, string varBd)
        {
            string prohibida = @"Data Source=sgvTransporte.mssql.somee.com;Initial Catalog=sgvTransporte;User ID=x;Password=y";
            string remotaConNombreDeCopia = @"Data Source=10.0.0.5;Initial Catalog=sgvEnsayo_pruebas;Integrated Security=True";
            var e = BaseDePruebas.Elegir(varConnStr, varBd, () => prohibida, () => remotaConNombreDeCopia);

            Assert.Null(e.Cadena);
            Assert.NotNull(e.Motivo);
            Assert.DoesNotContain("Password", e.Motivo);
        }

        [Theory]
        [InlineData(@"(localdb)\MSSQLLocalDB")]
        [InlineData(".")]
        public void LaCadenaDeLaCopia_EsLocal_ConWindows_YPasaLaProteccion(string servidor)
        {
            string cadena = BaseDePruebas.CadenaCopiaLocal(servidor, 3);

            Assert.Null(BaseDePruebas.MotivoDestinoNoPermitido(cadena));
            Assert.Contains("Integrated Security=True", cadena);
            Assert.DoesNotContain("Password", cadena);
            Assert.True(BaseDePruebas.EsNombreBdEnsayo(BaseDePruebas.BdCopiaLocal));
        }

        [Fact]
        public void LosServidoresDeLaCopia_SonLocales_YLocalDbVaPrimero()
        {
            Assert.All(BaseDePruebas.ServidoresCopiaLocal, s => Assert.True(BaseDePruebas.EsServidorLocal(s)));
            Assert.StartsWith("(localdb)", BaseDePruebas.ServidoresCopiaLocal[0]);
        }

        [Fact]
        public void ElDestino_SeDescribeSinUsuarioNiClave()
        {
            var e = BaseDePruebas.Elegir(null, "actualizada", () => Actualizada, () => null);
            string d = BaseDePruebas.DescribirDestino(e);

            Assert.Contains("sgvActualizada", d);
            Assert.DoesNotContain("clave-secreta", d);
            Assert.DoesNotContain("User ID", d);
        }

        [Fact]
        public void ElScriptDePreparacion_UsaElMismoNombreDeCopia()
        {
            string script = System.IO.File.ReadAllText(System.IO.Path.Combine(
                BaseDePruebas.RaizRepo, "herramientas", "respaldo", "preparar-bd-pruebas.ps1"));
            Assert.Contains("$NombreBd = '" + BaseDePruebas.BdCopiaLocal + "'", script);
        }

        /// <summary>Deja en la salida de la prueba contra qué BD corrió la suite (sin clave).</summary>
        [Fact]
        public void InformaElDestinoElegido()
        {
            string texto = BaseDePruebas.Disponible
                ? "Destino de las [FactBD]: " + BaseDePruebas.Destino
                : "[FactBD] omitidas: " + BaseDePruebas.MotivoNoDisponible;
            _salida.WriteLine(texto);
            Assert.DoesNotContain("Password", texto, StringComparison.OrdinalIgnoreCase);
        }
    }
}
