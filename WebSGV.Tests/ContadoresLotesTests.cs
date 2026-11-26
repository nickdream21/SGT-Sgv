using System;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-29 (M-138): el cálculo de los contadores de lotes de antes (<see cref="ContadoresLotes.DesdeLotes"/>, el oráculo
    /// de <c>sp_LD_ContarLotesActivos</c>) y el aviso de la lista con tope. Sin BD.
    /// </summary>
    public class ContadoresLotesTests
    {
        private static readonly DateTime Hoy = new DateTime(2026, 11, 25);

        private static LoteRegistrado Lote(DateTime fecha, int despachos = 2, bool internacional = false, int idPlanta = 1,
            int manifiestoCompleto = 0, int sinGuia = 0, string estado = "ACTIVO") => new LoteRegistrado
        {
            FechaProgramacion = fecha,
            CantidadDespachos = despachos,
            DespachosVigentes = estado == "ANULADO" ? 0 : despachos,
            EsInternacional = internacional,
            IdPlanta = idPlanta,
            DespachosManifiestoCompleto = manifiestoCompleto,
            DespachosSinGuia = sinGuia,
            EstadoLote = estado
        };

        [Fact]
        public void DiasTolerancia_EsTres()
        {
            Assert.Equal(3, EstadoDocumental.DiasTolerancia);
        }

        [Fact]
        public void SinLotes_TodoEnCero()
        {
            Assert.Equal(new ContadoresLotes(), ContadoresLotes.DesdeLotes(null, Hoy));
            Assert.Equal(new ContadoresLotes(), ContadoresLotes.DesdeLotes(new LoteRegistrado[0], Hoy));
        }

        [Fact]
        public void Anulado_NoCuentaEnNada()
        {
            var c = ContadoresLotes.DesdeLotes(new[]
            {
                Lote(Hoy.AddDays(-20), internacional: true, sinGuia: 2, estado: "ANULADO"),
                Lote(Hoy)
            }, Hoy);

            Assert.Equal(1, c.LotesActivos);
            Assert.Equal(0, c.ConDocumentosPendientes);
            Assert.Equal(0, c.Atrasados);
            Assert.Equal(0, c.ConManifiestosPendientes);
        }

        [Fact]
        public void Manifiesto_PendienteYParcialCuentan_CompletoYNacionalNo()
        {
            var c = ContadoresLotes.DesdeLotes(new[]
            {
                Lote(Hoy, despachos: 2, internacional: true, manifiestoCompleto: 0), // Pendiente
                Lote(Hoy, despachos: 2, internacional: true, manifiestoCompleto: 1), // Parcial 1/2
                Lote(Hoy, despachos: 2, internacional: true, manifiestoCompleto: 2), // Completo
                Lote(Hoy, despachos: 2, internacional: false),                       // N/A: nacional
                Lote(Hoy, despachos: 2, internacional: true, idPlanta: 0)            // N/A: sin planta
            }, Hoy);

            Assert.Equal("Parcial 1/2", ListaDespachosService.EstadoManifiesto(Lote(Hoy, internacional: true, manifiestoCompleto: 1)));
            Assert.Equal(5, c.LotesActivos);
            Assert.Equal(2, c.ConManifiestosPendientes);
        }

        [Fact]
        public void Atrasado_EsMasDeTresDias_ElBordeNoCuenta()
        {
            var c = ContadoresLotes.DesdeLotes(new[]
            {
                Lote(Hoy.AddDays(-3), sinGuia: 1), // pendiente, justo en el borde: no atrasado
                Lote(Hoy.AddDays(-4), sinGuia: 1), // pendiente y atrasado
                Lote(Hoy.AddDays(-30)),            // completo: nunca atrasado
                Lote(Hoy.AddDays(5), sinGuia: 2)   // futuro: pendiente, no atrasado
            }, Hoy);

            Assert.Equal(4, c.LotesActivos);
            Assert.Equal(3, c.ConDocumentosPendientes);
            Assert.Equal(1, c.Atrasados);
        }

        [Fact]
        public void Igualdad_CampoACampo()
        {
            var a = new ContadoresLotes { LotesActivos = 108, ConDocumentosPendientes = 6, Atrasados = 6, ConManifiestosPendientes = 12 };
            var b = new ContadoresLotes { LotesActivos = 108, ConDocumentosPendientes = 6, Atrasados = 6, ConManifiestosPendientes = 12 };
            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            b.ConManifiestosPendientes = 11;
            Assert.NotEqual(a, b);
            Assert.Equal("activos 108, pendientes 6, atrasados 6, manifiestos 12", a.ToString());
        }

        [Fact]
        public void AvisoTopeLotes_SinCorte_EsNull()
        {
            Assert.Null(ListaDespachosReglas.AvisoTopeLotes(0, 0, false));
            Assert.Null(ListaDespachosReglas.AvisoTopeLotes(108, 108, false));
            Assert.Null(ListaDespachosReglas.AvisoTopeLotes(500, 500, true));
        }

        [Fact]
        public void AvisoTopeLotes_ConCorte_DiceCualesSeMuestran()
        {
            string n = 1234.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-PE"));
            Assert.Equal("Se muestran los 500 lotes más recientes de " + n + "; acote las fechas o los filtros para ver el resto.",
                ListaDespachosReglas.AvisoTopeLotes(500, 1234, false));
            Assert.Equal("Se muestran los 500 lotes con documentos pendientes más antiguos de " + n
                + "; complete esos o filtre por cliente o fechas para ver el resto.",
                ListaDespachosReglas.AvisoTopeLotes(500, 1234, true));
            Assert.Equal(500, ListaDespachosReglas.TopeLotesLista);
        }

        [Fact]
        public void TextoTotalLotes_SinCorteIgualQueAntes_ConCorteConElTotalYElAviso()
        {
            Assert.Equal("Sin lotes con los filtros elegidos", ListaDespachosReglas.TextoTotalLotes(0, 0, 0, false));
            Assert.Equal("Mostrando 1–25 de 108 con los filtros elegidos", ListaDespachosReglas.TextoTotalLotes(0, 108, 108, false));
            Assert.Equal("Mostrando 1–6 de 6 con los filtros elegidos", ListaDespachosReglas.TextoTotalLotes(0, 6, 6, true));
            string n = 1234.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-PE"));
            Assert.Equal("Mostrando 26–50 de " + n + " con los filtros elegidos. " + ListaDespachosReglas.AvisoTopeLotes(500, 1234, false),
                ListaDespachosReglas.TextoTotalLotes(1, 500, 1234, false));
        }

        [Fact]
        public void ListadoLotes_CortadoSoloSiElTotalSuperaLoTraido()
        {
            var lotes = new System.Collections.Generic.List<LoteRegistrado> { Lote(Hoy), Lote(Hoy) };
            Assert.False(new ListadoLotes(lotes, 2).Cortado);
            Assert.True(new ListadoLotes(lotes, 3).Cortado);
            // Sin TotalLotes (SP anterior) el total no baja de lo traído.
            Assert.Equal(2, new ListadoLotes(lotes, 0).Total);
            Assert.Empty(new ListadoLotes(null, 0).Lotes);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void ConTotal_TopeMenorQueUno_SeRechazaSinIrALaBd(int maximo)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ListaDespachosService.ObtenerLotesRegistradosConTotal(
                null, null, null, null, null, null, "ACTIVO", null, null, null, null, maximo));
        }
    }
}
