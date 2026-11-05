using System;
using System.Collections.Generic;
using WebSGV.Services.Despachos.Importacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// CPIC y MCI: las líneas imitan lo que <see cref="ExtractorTextoPdf"/> saca de los PDF reales, con datos
    /// inventados (los documentos no se versionan).
    /// </summary>
    public class LectorDocumentosAduanaTests
    {
        private static IList<IList<string>> Cpic() => new List<IList<string>>
        {
            new List<string>
            {
                "Carta de Porte Internacional por Carretera ( CPIC)",
                "N° 0001234",
                "COMUNIDAD ANDINA",
                "6 Lugar, país y fecha en que el transportista recibe las mercancías",
                "TRUJILLO - PERÚ / 21 DE OCTUBRE DEL 2026",
                "10 Cantidad y clase 11 Marcas y números 12 Descripción corriente de la naturaleza de las mercancías 13 PESO EN KILOGRAMOS",
                "de los bultos de los bultos (indicar si son peligrosas) neto bruto",
                "1,188 BOLSAS PRODUCTO DEMO 35% 0.8",
                "178,200.00 179,000.50",
                "Valor de flete",
                "$ 11,048.40 USD USD",
                "TOTAL $ 11,048.40 USD USD",
                "FACTURA COMERCIAL No. F222 - 00001111",
                "TRUJILLO - PERÚ / 23 DE OCTUBRE DEL 2026 Hoja separada, el transportista autorizado"
            }
        };

        private static IList<IList<string>> Mci(string descarga = "TRUJILLO - PERU GUAYAQUIL - ECUADOR") => new List<IList<string>>
        {
            new List<string>
            {
                "Manifiesto de Carga Internacional (MCI)",
                "11 46",
                "N° 0005678",
                "IDENTIFICACION DEL VEHICULO HABILITADO (CAMION O TRACTO CAMION)",
                "4 Marca 5 Año de fabricación 6 Placa y país 7 Número o serie del chasis",
                "INTERNATIONAL 2016 ABC-123 PERU 3HSCEAPR0HN000000",
                "IDENTIFICACION DE LA UNIDAD DE CARGA (REMOLQUE O SEMI-REMOLQUE)",
                "9 Marca 10 Año de fabricación 11 Placa y país 12 Otro",
                "FAMECA 2017 T1X-456 PERU 8T9SP3D12GTE00000",
                "13 CONDUCTOR PRINCIPAL: Nombres y apellidos 18 CONDUCTOR AUXILIAR: Nombres y apellidos",
                "JUAN PEREZ DEMO *****",
                "16 Licencia de Conducir N° 17 Libreta de Tripulante Terrestre N° 21 Licencia de Conducir Nº",
                "B-12345678 PE. 13219 XXX",
                "24 Lugar y país de descarga",
                descarga,
                "28 Carta de Porte N° 29 Descripción de las mercancías 32 Peso en kilogramos m3 u otra unidad",
                "0001234 PRODUCTO DEMO 35% 2.0 T 994 BOLSAS 24,949.40 24,850.00",
                "40 Fecha de emisión",
                "29 DE SETIEMBRE DEL 2026"
            }
        };

        [Fact]
        public void Clasifica_cpic_y_mci()
        {
            Assert.Equal(TipoDocumentoAduana.Cpic, LectorDocumentosAduana.Clasificar(Cpic()));
            Assert.Equal(TipoDocumentoAduana.Mci, LectorDocumentosAduana.Clasificar(Mci()));
            Assert.Equal(TipoDocumentoAduana.Ninguno, LectorDocumentosAduana.Clasificar(new List<IList<string>> { new List<string> { "FACTURA ELECTRONICA" } }));
        }

        [Fact]
        public void Lee_la_cpic()
        {
            var c = LectorDocumentosAduana.LeerCpic(Cpic());
            Assert.Equal("0001234", c.Numero);
            Assert.Equal("F222-00001111", c.FacturaComercial);
            Assert.Equal(11048.40m, c.ValorFlete);
            Assert.Equal(178200.00m, c.PesoNetoKg);
            Assert.Equal(179000.50m, c.PesoBrutoKg);
            Assert.Equal(new DateTime(2026, 10, 23), c.FechaEmision);   // la de la casilla 19, la última
        }

        [Fact]
        public void Lee_el_mci_de_ida()
        {
            var m = LectorDocumentosAduana.LeerMci(Mci());
            Assert.Equal("0005678", m.Numero);
            Assert.Equal("0001234", m.CartaPorte);
            Assert.Equal("ABC-123", m.PlacaTracto);
            Assert.Equal("T1X-456", m.PlacaCarreta);
            Assert.Equal("JUAN PEREZ DEMO", m.Conductor);
            Assert.Equal("B12345678", m.Licencia);
            Assert.False(m.EsRetorno);
            Assert.Equal("GUAYAQUIL - ECUADOR", m.LugarDescarga);
            Assert.Equal(new DateTime(2026, 9, 29), m.FechaEmision);
        }

        [Fact]
        public void El_mci_que_descarga_en_el_peru_es_de_retorno()
        {
            var m = LectorDocumentosAduana.LeerMci(Mci("GUAYAQUIL - ECUADOR TRUJILLO - PERÚ"));
            Assert.True(m.EsRetorno);
        }

        [Theory]
        [InlineData("C.P.I 0003943 VITAPRO CF.pdf", 0)]
        [InlineData("C.P.I 0003943 VITAPRO.pdf", 1)]
        [InlineData("C.P.I 0003943 VITAPRO SF.pdf", 2)]
        [InlineData("BORRADOR CPI VITAPRO 0003943.pdf", 3)]
        public void Prefiere_la_version_firmada(string archivo, int prioridad) =>
            Assert.Equal(prioridad, LectorDocumentosAduana.PrioridadVersion(archivo));
    }
}
