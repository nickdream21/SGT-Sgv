using System;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Qué BD aceptan los tests de integración (no se conectan): solo sgvActualizada o un ensayo
    /// local sgvEnsayo_* (E3-1). Un ensayo en un servidor remoto, o cualquier otro nombre, se rechaza.
    /// </summary>
    public class BaseDePruebasDestinoTests
    {
        [Theory]
        [InlineData(@"Data Source=sgvActualizada.mssql.somee.com;Initial Catalog=sgvActualizada;User ID=x;Password=y")]
        [InlineData(@"Data Source=.;Initial Catalog=sgvEnsayo_20261121_1000;Integrated Security=True")]
        [InlineData(@"Data Source=localhost\SQLEXPRESS;Initial Catalog=sgvEnsayo_x;Integrated Security=True")]
        [InlineData(@"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=sgvEnsayo_20261121_1000;Integrated Security=True")]
        [InlineData(@"Data Source=tcp:127.0.0.1,1433;Initial Catalog=sgvEnsayo_a;Integrated Security=True")]
        public void AceptaPruebasYEnsayoLocal(string cadena)
        {
            Assert.Null(BaseDePruebas.MotivoDestinoNoPermitido(cadena));
        }

        [Fact]
        public void AceptaElNombreDeEstaMaquina()
        {
            string cadena = $@"Data Source={Environment.MachineName}\MSSQLSERVER;Initial Catalog=sgvEnsayo_1;Integrated Security=True";
            Assert.Null(BaseDePruebas.MotivoDestinoNoPermitido(cadena));
        }

        [Theory]
        [InlineData(@"Data Source=sgvTransporte.mssql.somee.com;Initial Catalog=sgvTransporte;User ID=x;Password=y")]
        [InlineData(@"Data Source=.;Initial Catalog=sgvTransporte;Integrated Security=True")]
        [InlineData(@"Data Source=.;Initial Catalog=sgvRestaurada_20261121_1000;Integrated Security=True")]
        [InlineData(@"Data Source=.;Initial Catalog=master;Integrated Security=True")]
        [InlineData(@"Data Source=.;Initial Catalog=sgvEnsayo_;Integrated Security=True")]
        [InlineData(@"Data Source=.;Initial Catalog='sgvEnsayo_x;y';Integrated Security=True")]
        [InlineData(@"Data Source=.;Initial Catalog=sgvEnsayo_x-y;Integrated Security=True")]
        [InlineData("Data Source=.;Initial Catalog='sgvEnsayo_x\n';Integrated Security=True")]
        [InlineData(@"Data Source=.;Initial Catalog=SGVENSAYO-x;Integrated Security=True")]
        public void RechazaOtrasBases(string cadena)
        {
            Assert.NotNull(BaseDePruebas.MotivoDestinoNoPermitido(cadena));
        }

        [Theory]
        [InlineData("sgvEnsayo_x\n")]
        [InlineData("sgvEnsayo_x\r\n")]
        [InlineData("sgvEnsayo_x;y")]
        [InlineData("sgvEnsayo_")]
        [InlineData("xsgvEnsayo_1")]
        [InlineData(null)]
        public void NombreDeEnsayo_RechazaLoQueNoEsExacto(string bd)
        {
            Assert.False(BaseDePruebas.EsNombreBdEnsayo(bd));
        }

        [Fact]
        public void NombreDeEnsayo_AceptaElQueGeneraElScript()
        {
            Assert.True(BaseDePruebas.EsNombreBdEnsayo("sgvEnsayo_20261121_1311"));
        }

        [Theory]
        [InlineData(@"Data Source=sgvActualizada.mssql.somee.com;Initial Catalog=sgvEnsayo_20261121_1000;User ID=x;Password=y")]
        [InlineData(@"Data Source=otro-servidor\SQLEXPRESS;Initial Catalog=sgvEnsayo_1;Integrated Security=True")]
        [InlineData(@"Data Source=10.0.0.5;Initial Catalog=sgvEnsayo_1;Integrated Security=True")]
        [InlineData(@"Data Source=localhost.somee.com;Initial Catalog=sgvEnsayo_1;Integrated Security=True")]
        public void RechazaUnEnsayoEnUnServidorRemoto(string cadena)
        {
            string motivo = BaseDePruebas.MotivoDestinoNoPermitido(cadena);
            Assert.NotNull(motivo);
            Assert.Contains("no es local", motivo);
            Assert.DoesNotContain("Password", motivo);
        }
    }
}
