using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests
{
    // Títulos tal como aparecen en los Word de programación de enero–octubre 2026, erratas incluidas.
    public class RutaProgramacionTests
    {
        [Theory]
        [InlineData("CARTAVIO-AZUCAR", "CRISAR", true, "CARTAVIO", false)]
        [InlineData("AZÚCAR-CARTAVIO-CARGAN 15 DE SEPTIEMBRE", "CRISAR", true, "CARTAVIO", false)]
        [InlineData("GUAYAQUIL-AZÚCAR", "CRISAR", false, "GUAYAQUIL", true)]
        [InlineData("GUAYAQUIL-AZUCAR", "CRISAR", false, "GUAYAQUIL", true)]
        [InlineData("GUAYAQUIL-CARTAVIO", "CRISAR", false, "GUAYAQUIL", true)]
        [InlineData("NOVOPAN-QUITO", "NOVOPAN", true, "QUITO", true)]
        [InlineData("NOVOPAN-PIURA", "NOVOPAN", false, "PIURA", false)]
        [InlineData("NOVOPAN-TRUJILO", "NOVOPAN", false, "TRUJILLO", false)]
        [InlineData("TRUJILLO-NOVOPAN", "NOVOPAN", false, "TRUJILLO", false)]
        [InlineData("POLINPLAST-CHAPAIRA", "POLINPLAST", true, "CHAPAIRA", false)]
        [InlineData("POLINPLAST-RAPEL", "POLINPLAST", true, "CHAPAIRA", false)]
        [InlineData("CARTON-RAPEL", "POLINPLAST", true, "CHAPAIRA", false)]
        [InlineData("POLINPLAST-VIRU", "POLINPLAST", false, "VIRU", false)]
        [InlineData("POLINPLAST-OLMOS PROYECTO", "POLINPLAST", false, "OLMOS", false)]
        [InlineData("POLINPLAST-OLMOS OROYECTO", "POLINPLAST", false, "OLMOS", false)]
        [InlineData("POLINPAST-ACP", "POLINPLAST", false, "ACP", false)]
        [InlineData("AFRECHO-PAITA", "AFRECHO", true, "PAITA", false)]
        [InlineData("AFRECHO", "AFRECHO", true, "PAITA", false)]
        public void Reconocer_ClienteOperacionYLugar(string titulo, string cliente, bool esCarga, string lugar, bool internacional)
        {
            var r = RutaProgramacion.Reconocer(titulo);

            Assert.NotNull(r);
            Assert.Equal(cliente, r.Cliente);
            Assert.Equal(esCarga, r.EsCarga);
            Assert.Equal(lugar, r.Lugar);
            Assert.Equal(internacional, r.EsInternacional);
        }

        [Fact]
        public void Reconocer_Rapel_CargaEnChapairaParaRapel()
        {
            var r = RutaProgramacion.Reconocer("POLINPLAST-RAPEL");
            Assert.Equal("CHAPAIRA", r.Lugar);
            Assert.Equal("RAPEL", r.Destino);
            Assert.Null(RutaProgramacion.Reconocer("POLINPLAST-CHAPAIRA").Destino);
        }

        [Theory]
        [InlineData("TRUJILLO-VITAPRO-CARGAN 22 DE OCTUBRE")]
        [InlineData("GUAYAQUIL-VITAPRO")]
        [InlineData("EMBARQUE-VITAPRO")]
        [InlineData("CARRETAS VACIAS-TUMBES")]
        [InlineData("")]
        [InlineData(null)]
        public void Reconocer_VitaproYOtros_Null(string titulo) => Assert.Null(RutaProgramacion.Reconocer(titulo));

        private static SeccionProgramacion Seccion(string titulo, string horario, string pedido = null)
        {
            var s = new SeccionProgramacion { Titulo = titulo, NumeroPedido = pedido };
            s.Filas.Add(new FilaProgramacion { Chofer = "ANA TORRES", Tracto = "CCC-333", Carreta = "TXC-001", Horario = horario });
            return s;
        }

        [Fact]
        public void EsSeccionDeCarga_LaDiceElTituloDelTercero()
        {
            // Sin hora pero es la carga en Quito; con hora pero son las descargas.
            Assert.True(ProgramacionCarga.EsSeccionDeCarga(Seccion("NOVOPAN-QUITO", null)));
            Assert.False(ProgramacionCarga.EsSeccionDeCarga(Seccion("NOVOPAN-PIURA", "07:00AM")));
            Assert.False(ProgramacionCarga.EsSeccionDeCarga(Seccion("POLINPLAST-VIRU", "08:00AM")));
            Assert.True(ProgramacionCarga.EsSeccionDeCarga(Seccion("CARTAVIO-AZUCAR", "06:00AM")));
            Assert.False(ProgramacionCarga.EsSeccionDeCarga(Seccion("GUAYAQUIL-AZÚCAR", null)));

            // Vitapro sigue con la regla de siempre.
            Assert.True(ProgramacionCarga.EsSeccionDeCarga(Seccion("TRUJILLO-VITAPRO-CARGAN 22 DE OCTUBRE", "05:30AM")));
            Assert.False(ProgramacionCarga.EsSeccionDeCarga(Seccion("GUAYAQUIL-VITAPRO", null, "4400133950")));
        }
    }
}
