using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// CPIC de un cliente tercero en la bandeja: sus MCI se unen a los despachos programados con el Word por la carreta
    /// (el tracto que cruza puede ser el del relevo en la base) y, si no, por el tracto. Caso real: CPIC 0003925 de
    /// Crisar (12/10/2026), dos de las cinco unidades cruzaron con otro tracto.
    /// </summary>
    public class BandejaTercerosTests
    {
        private static readonly DateTime Dia = new DateTime(2026, 10, 12);

        private static BandejaDocumentosService.DespachoTercero D(int id, string tracto, string carreta, int dia = 12, int cliente = 8) =>
            new BandejaDocumentosService.DespachoTercero
            {
                IdDespacho = id, IdCliente = cliente, Cliente = "CRISAR", Fecha = new DateTime(2026, 10, dia), Tracto = tracto, Carreta = carreta
            };

        private static MciLeido M(string numero, string tracto, string carreta) =>
            new MciLeido { Numero = numero, CartaPorte = "0003925", PlacaTracto = tracto, PlacaCarreta = carreta };

        [Fact]
        public void Cada_MCI_va_al_despacho_de_su_carreta_aunque_cruce_otro_tracto()
        {
            var programados = new[]
            {
                D(1, "BNI-736", "AUM-976"), D(2, "TBM-815", "AVA-979"), D(3, "CBU-928", "T5B-972"),
                D(4, "ANU-794", "TDN-978"), D(5, "C6K-799", "T0C-999")
            };
            var mcis = new[]
            {
                M("0019478", "BNI-736", "AUM-976"), M("0019479", "TBM-815", "AVA-979"), M("0019480", "CBU-928", "T5B-972"),
                M("0019481", "T8V-933", "TDN-978"), M("0019482", "CBU-945", "T0C-999")
            };

            var union = BandejaDocumentosService.UnirMcisTercero(mcis, programados, Dia);

            Assert.Equal(5, union.Count);
            Assert.Equal(4, union["0019481"]);
            Assert.Equal(5, union["0019482"]);
            Assert.Equal(5, union.Values.Distinct().Count());
        }

        [Fact]
        public void Entre_dos_viajes_de_la_misma_carreta_elige_el_mas_cercano_al_dia_de_la_CPIC()
        {
            var programados = new[] { D(10, "CBV-761", "T9U-991", dia: 8), D(11, "CBV-761", "T9U-991", dia: 11) };

            var union = BandejaDocumentosService.UnirMcisTercero(new[] { M("0019454", "CBV-761", "T9U-991") }, programados, Dia);

            Assert.Equal(11, union["0019454"]);
        }

        [Fact]
        public void Sin_su_carreta_se_une_por_el_tracto_y_cada_despacho_una_sola_vez()
        {
            var programados = new[] { D(20, "BFK-879", "TEA-983"), D(21, "ARN-913", "TAN-976") };
            var mcis = new[]
            {
                M("0019485", "BFK-879", "XXX-111"),      // cambió de carreta: va por el tracto
                M("0019486", "BFK-879", "TEA-983"),      // su carreta: se queda con el 20
                M("0019487", "ZZZ-999", "YYY-222")       // nada coincide
            };

            var union = BandejaDocumentosService.UnirMcisTercero(mcis, programados, Dia);

            Assert.Equal(20, union["0019486"]);
            Assert.False(union.ContainsKey("0019485"));  // el 20 ya lo tomó el de la carreta y el tracto no tiene otro
            Assert.False(union.ContainsKey("0019487"));
        }

        [Fact]
        public void La_CPIC_lee_la_recepcion_y_el_precio_de_las_mercancias()
        {
            var c = LectorDocumentosAduana.LeerCpic(new List<IList<string>> { new List<string>
            {
                "Carta de Porte Internacional por Carretera ( CPIC)",
                "COMUNIDAD ANDINA N° 0003911",
                "6 Lugar, país y fecha en que el transportista recibe las mercancías",
                "QUITO - ECUADOR / 01 DE OCTUBRE DEL 2026",
                "16 Precio de las mercancías",
                "(INCOTERMS 2020) y tipo de moneda",
                "VALOR CPT TUMBES USD",
                "$ 34,672.59",
                "18 Documentos recibidos del remitente",
                "FACTURA COMERCIAL No. 001-098-000331036",
                "19 Lugar, país y fecha de emisión",
                "QUITO - ECUADOR / 02 DE OCTUBRE DEL 2026"
            } });

            Assert.Equal("0003911", c.Numero);
            Assert.Equal("001-098-000331036", c.FacturaComercial);
            Assert.Equal(new DateTime(2026, 10, 1), c.FechaRecepcion);
            Assert.Equal(new DateTime(2026, 10, 2), c.FechaEmision);
            Assert.Equal(34672.59m, c.ValorMercancias);
        }
    }
}
