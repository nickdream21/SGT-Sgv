using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// GRE transportista de SUNAT en la bandeja: las líneas imitan lo que <see cref="ExtractorTextoPdf"/> saca del PDF real,
    /// con datos inventados (los documentos no se versionan).
    /// </summary>
    public class BandejaGreTransportistaTests
    {
        private static IList<IList<string>> Gre(string tipo = "TRANSPORTISTA", string remitentes = "Guía de Remisión Remitente N° T700 - 1643 - RUC N° 20111111111") =>
            new List<IList<string>>
            {
                new List<string>
                {
                    "EMPRESA DEMO E.I.R.L. RUC N°20999999999",
                    "GUÍA DE REMISIÓN ELECTRÓNICA",
                    tipo,
                    "N° EG03 - 00015116",
                    "Número de Registro MTC: 200200CNG",
                    "Fecha y hora de emisión: 09/09/2026 11:59 AM",
                    "Fecha de inicio de Traslado: 10/09/2026 Punto de Partida: CAR.PIURA A TAMBO GRANDE KM. 19",
                    "(COSTADO COMEDOR) - TAMBO GRANDE -",
                    "PIURA - PIURA",
                    "Punto de llegada: CAR.PANAMERICANA NORTE KM 495",
                    "(FUNDOS DEMO) - CHAO - VIRU - LA LIBERTAD",
                    "Datos del remitente: Cliente Demo SAC - REGISTRO ÚNICO DE CONTRIBUYENTES N° 20111111111",
                    "Datos del destinatario: DESTINO DEMO S.A.C. - REGISTRO ÚNICO DE CONTRIBUYENTES N° 20222222222",
                    "Documentos Relacionados:",
                    remitentes,
                    "Bienes por Transportar:",
                    "Unidad de Medida del Peso Bruto: KGM",
                    "Peso Bruto total de la carga: 1,590.144",
                    "Datos de los vehículos:",
                    "Principal: Número de placa: ABC123",
                    "Número de TUCE o Certificado de Habilitación Vehicular:20M22000095E",
                    "Secundario 1: Número de placa: T1X456",
                    "Número de TUCE o Certificado de Habilitación Vehicular:2018001150",
                    "Datos de los conductores:",
                    "Principal: PEREZ DEMO JUAN - DOCUMENTO NACIONAL DE IDENTIDAD N° 12345678",
                    "Número de licencia de conducir: B12345678",
                    "Datos de pagador de flete: Cliente Demo SAC - REGISTRO ÚNICO DE CONTRIBUYENTES N° 20111111111"
                }
            };

        [Fact]
        public void Clasifica_SoloLaDeTransportista()
        {
            Assert.True(LectorGreTransportista.EsGreTransportista(Gre()));
            Assert.False(LectorGreTransportista.EsGreTransportista(Gre("REMITENTE")));
        }

        [Fact]
        public void Lee_LoQueNecesitaElDespacho()
        {
            var g = LectorGreTransportista.Leer(Gre());

            Assert.Equal("EG03-00015116", g.Numero);
            Assert.Equal("20999999999", g.RucEmisor);
            Assert.Equal(new DateTime(2026, 9, 10), g.FechaTraslado);
            Assert.Equal(new DateTime(2026, 9, 9), g.FechaEmision);
            Assert.Equal("20111111111", g.RucRemitente);
            Assert.Equal("Cliente Demo SAC", g.NombreRemitente);
            Assert.Equal("DESTINO DEMO S.A.C.", g.NombreDestinatario);
            Assert.Equal(new[] { "T700-00001643" }, g.GuiasRemitente);
            Assert.Equal("ABC-123", g.PlacaTracto);
            Assert.Equal("T1X-456", g.PlacaCarreta);
            Assert.Equal("12345678", g.DniConductor);
            Assert.Equal("PEREZ DEMO JUAN", g.NombreConductor);
            Assert.Equal(1590.144m, g.PesoBrutoKg);
            Assert.Equal("CAR.PANAMERICANA NORTE KM 495 (FUNDOS DEMO) - CHAO - VIRU - LA LIBERTAD", g.PuntoLlegada);
            Assert.True(g.Completa);
            Assert.Equal("CHAO - VIRU - LA LIBERTAD", BandejaDocumentosService.DestinoCorto(g.PuntoLlegada));
        }

        [Fact]
        public void VariasGuiasDelRemitente_SeJuntan()
        {
            var g = LectorGreTransportista.Leer(Gre(remitentes:
                "Guía de Remisión Remitente N° T700 - 1643 - RUC N° 20111111111 Guía de Remisión Remitente N° T700 - 1644 - RUC N° 20111111111"));

            Assert.Equal("T700-00001643 / T700-00001644", g.GuiasRemitenteTexto);
        }

        // ── Unión con la carga ───────────────────────────────────────────────

        private static GreBandeja Leida(string numero, DateTime traslado, int tracto, int carreta, int conductor = 1, bool puedeCrear = true) => new GreBandeja
        {
            Archivo = numero + ".pdf",
            Gre = new GreTransportistaLeida { Numero = numero, FechaTraslado = traslado, PlacaTracto = "ABC-123", PlacaCarreta = "T1X-456", DniConductor = "1", RucRemitente = "2" },
            Estado = EstadoGreBandeja.Elegir, IdCliente = 7, Cliente = "CLIENTE DEMO",
            IdTracto = tracto, IdCarreta = carreta, IdConductor = conductor, IdPlanta = puedeCrear ? 12 : (int?)null
        };

        private static BandejaDocumentosService.DespachoGre Carga(int id, DateTime fecha, int tracto, int carreta, int cliente = 7, int conductor = 1) =>
            new BandejaDocumentosService.DespachoGre { IdDespacho = id, IdCliente = cliente, Fecha = fecha, IdTracto = tracto, IdCarreta = carreta, IdConductor = conductor, Texto = "Carga " + id };

        private static readonly DateTime Dia = new DateTime(2026, 10, 23);

        [Fact]
        public void MismoTractoYCarreta_SeUneAlDespachoDelWord()
        {
            var g = Leida("EG03-1", Dia, 10, 20);
            BandejaDocumentosService.UnirGres(new[] { g }, new[] { Carga(100, Dia, 11, 21), Carga(101, Dia, 10, 20) });

            Assert.Equal(EstadoGreBandeja.Unida, g.Estado);
            Assert.Equal(101, g.IdDespacho);
            Assert.Equal("Mismo tracto y carreta", g.Regla);
            Assert.Equal(2, g.Candidatos.Count);
        }

        [Fact]
        public void CambioDeTracto_SeUnePorLaCarreta_YCadaCargaUnaVez()
        {
            var a = Leida("EG03-1", Dia, 10, 20);
            var b = Leida("EG03-2", Dia, 99, 20);   // otra GRE con la misma carreta: la carga ya la tomó la primera
            var c = Leida("EG03-3", Dia, 98, 21);
            BandejaDocumentosService.UnirGres(new[] { a, b, c }, new[] { Carga(100, Dia, 10, 20), Carga(101, Dia.AddDays(1), 11, 21) });

            Assert.Equal(100, a.IdDespacho);
            Assert.Equal(101, c.IdDespacho);
            Assert.Equal("Misma carreta (cambio de tracto)", c.Regla);
            Assert.Equal(EstadoGreBandeja.Elegir, b.Estado);   // hay cargas en su ventana, pero ninguna coincide
            Assert.Null(b.IdDespacho);
        }

        [Fact]
        public void FueraDeLaVentana_OtroCliente_SinCarga_SeCreaElDespacho()
        {
            var g = Leida("EG03-1", Dia, 10, 20);
            BandejaDocumentosService.UnirGres(new[] { g }, new[] { Carga(100, Dia.AddDays(3), 10, 20), Carga(101, Dia, 10, 20, cliente: 8) });

            Assert.Equal(EstadoGreBandeja.Nueva, g.Estado);
            Assert.Empty(g.Candidatos);
        }

        [Fact]
        public void ClienteConPedido_NoCreaDespacho()
        {
            var g = Leida("EG03-1", Dia, 10, 20);
            g.ClienteConPedido = true;
            BandejaDocumentosService.UnirGres(new[] { g }, new BandejaDocumentosService.DespachoGre[0]);

            Assert.Equal(EstadoGreBandeja.NoSeRegistra, g.Estado);
            Assert.Contains("ZIP", g.Detalle);
        }

        [Fact]
        public void ChoferDistinto_SeAvisa_YQuedaElDelWord()
        {
            var g = Leida("EG03-1", Dia, 10, 20, conductor: 5);
            g.Conductor = "JUAN PEREZ";
            BandejaDocumentosService.UnirGres(new[] { g }, new[] { Carga(100, Dia.AddDays(-1), 10, 20, conductor: 6) });

            Assert.Equal(100, g.IdDespacho);
            Assert.Contains("queda el chofer del Word", g.Detalle);
        }

        [Fact]
        public void Eleccion_DelUsuario_SeValida()
        {
            var g = Leida("EG03-1", Dia, 10, 20);
            BandejaDocumentosService.UnirGres(new[] { g }, new[] { Carga(100, Dia, 11, 21) });
            var l = new LecturaBandeja { Gres = { g } };

            var errores = BandejaDocumentosService.AplicarEleccion(l, new Dictionary<string, EleccionGuia>(), null, new Dictionary<int, string> { [0] = "555" });
            Assert.Single(errores);

            errores = BandejaDocumentosService.AplicarEleccion(l, new Dictionary<string, EleccionGuia>(), null, new Dictionary<int, string> { [0] = "100" });
            Assert.Empty(errores);
            Assert.Equal(EstadoGreBandeja.Unida, g.Estado);
            Assert.Equal("Elegido por el usuario", g.Regla);

            errores = BandejaDocumentosService.AplicarEleccion(l, new Dictionary<string, EleccionGuia>(), null, new Dictionary<int, string> { [0] = BandejaDocumentosService.GreNuevo });
            Assert.Empty(errores);
            Assert.Equal(EstadoGreBandeja.Nueva, g.Estado);
            Assert.Empty(BandejaDocumentosService.QuedoSinRegistrar(l));   // nada pendiente
        }
    }
}
