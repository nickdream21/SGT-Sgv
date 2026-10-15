using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using WebSGV.Models.Despachos;
using WebSGV.Services.Common;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;
using Xunit;
using B = WebSGV.Services.Despachos.Importacion.LectorProgramacionWord.BloqueWord;

namespace WebSGV.Tests
{
    // Datos inventados: la forma es la del Word de programación que reparte transporte.
    public class ProgramacionDescargaTests
    {
        private static List<List<string>> Tabla(params string[] filas) =>
            filas.Select(f => f.Split('|').Select(c => c.Trim()).ToList()).ToList();

        private static List<B> ProgramacionDeEjemplo() => new List<B>
        {
            B.Parrafo("DESPACHO 11/10/2026"),
            B.Parrafo("AZÚCAR-CARTAVIO-CARGAN 12 DE OCTUBRE"),
            B.DeTabla(Tabla(
                "CHOFER | TRACTO | CARRETA | HORARIO",
                "LUIS PRADO | AAA-111 | TXA-001 | 06:00AM")),
            B.Parrafo("GUAYAQUIL-VITAPRO"),
            B.Parrafo("   9900000001"),
            B.DeTabla(Tabla(
                "CHOFER | TRACTO | CARRETA | CAMBIO",
                "MARIO QUISPE | BBB-222 | TXB-001 | PEDRO RAMOS",
                "| | |",
                "ANA TORRES | CCC-333 | TXB-002 | "))
        };

        [Fact]
        public void Interpretar_ArmaLasSeccionesConSuPedidoYColumnas()
        {
            var p = LectorProgramacionWord.Interpretar(ProgramacionDeEjemplo());

            Assert.Equal(new DateTime(2026, 10, 11), p.Fecha);
            Assert.Equal(2, p.Secciones.Count);

            var carga = p.Secciones[0];
            Assert.Equal("AZÚCAR-CARTAVIO-CARGAN 12 DE OCTUBRE", carga.Titulo);
            Assert.Null(carga.NumeroPedido);
            Assert.Equal("06:00AM", carga.Filas[0].Horario);
            Assert.Null(carga.Filas[0].Cambio);

            var descarga = p.Secciones[1];
            Assert.Equal("GUAYAQUIL-VITAPRO", descarga.Titulo);
            Assert.Equal("9900000001", descarga.NumeroPedido);
            Assert.Equal(2, descarga.Filas.Count);   // la fila vacía se ignora
            Assert.Equal("MARIO QUISPE", descarga.Filas[0].Chofer);
            Assert.Equal("BBB-222", descarga.Filas[0].Tracto);
            Assert.Equal("TXB-001", descarga.Filas[0].Carreta);
            Assert.Equal("PEDRO RAMOS", descarga.Filas[0].Cambio);
            Assert.Null(descarga.Filas[1].Cambio);
        }

        [Fact]
        public void Interpretar_PedidoEnLaMismaLineaDelTitulo()
        {
            var p = LectorProgramacionWord.Interpretar(new List<B>
            {
                B.Parrafo("GUAYAQUIL-VITAPRO 9900000002"),
                B.DeTabla(Tabla("CARRETA | CONDUCTOR | TRACTO", "TXB-001 | MARIO QUISPE | BBB-222"))
            });

            var s = Assert.Single(p.Secciones);
            Assert.Equal("GUAYAQUIL-VITAPRO", s.Titulo);
            Assert.Equal("9900000002", s.NumeroPedido);
            Assert.Equal("MARIO QUISPE", s.Filas[0].Chofer);
            Assert.Equal("TXB-001", s.Filas[0].Carreta);
        }

        [Fact]
        public void Leer_DocxReal_ExtraeParrafosYTablas()
        {
            const string xml =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>" +
                "<w:p><w:r><w:t>DESPACHO </w:t></w:r><w:r><w:t>11/10/2026</w:t></w:r></w:p>" +
                "<w:p><w:r><w:t>GUAYAQUIL-VITAPRO</w:t></w:r></w:p>" +
                "<w:p><w:r><w:tab/><w:t>9900000001</w:t></w:r></w:p>" +
                "<w:tbl>" +
                "<w:tr><w:tc><w:p><w:r><w:t>CHOFER</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>TRACTO</w:t></w:r></w:p></w:tc>" +
                "<w:tc><w:p><w:r><w:t>CARRETA</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>CAMBIO</w:t></w:r></w:p></w:tc></w:tr>" +
                "<w:tr><w:tc><w:p><w:r><w:t>MARIO QUISPE</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>BBB-222</w:t></w:r></w:p></w:tc>" +
                "<w:tc><w:p><w:r><w:t>TXB-001</w:t></w:r></w:p></w:tc><w:tc><w:p/></w:tc></w:tr>" +
                "</w:tbl></w:body></w:document>";

            byte[] docx;
            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                using (var w = new StreamWriter(zip.CreateEntry("word/document.xml").Open(), new UTF8Encoding(false)))
                    w.Write(xml);
                docx = ms.ToArray();
            }

            var p = LectorProgramacionWord.Leer(docx);

            Assert.Equal(new DateTime(2026, 10, 11), p.Fecha);
            var s = Assert.Single(p.Secciones);
            Assert.Equal("9900000001", s.NumeroPedido);
            Assert.Equal("MARIO QUISPE", s.Filas[0].Chofer);
            Assert.Null(s.Filas[0].Cambio);
        }

        [Fact]
        public void Leer_ArchivoQueNoEsWord_ErrorParaElUsuario()
        {
            Assert.Throws<ErrorNegocioException>(() => LectorProgramacionWord.Leer(Encoding.ASCII.GetBytes("no es un docx")));
        }

        [Theory]
        [InlineData("ROGER PAZ", "ROGEER DEL CARMEN PAZ LUNA - DNI: 12345678", true)]   // una letra de diferencia
        [InlineData("ANGELO CAMPOS", "EDWIN ANGELO CAMPOS VILCA", true)]                      // segundo nombre
        [InlineData("MARK TORRES", "MARCK TORRES ALVA", true)]
        [InlineData("ROBERTO ASUNCIÓN", "ASUNCION GARCIA ROBE", true)]                        // nombre cortado, sin tilde
        [InlineData("ROSA MUÑANTE", "ROSA MUÑANTE SALAS", true)]
        [InlineData("JULIO MENA", "VICENTE PAUL MENA VALDEZ", false)]
        [InlineData("JULIO MENA", "JULIO ARMANDO MENA RIOS", true)]
        [InlineData("JOSE LLANOS", "JOSE FAUSTINO", false)]
        [InlineData("", "JOSE FAUSTINO LLANOS", false)]
        public void NombreCoincide_ToleraNombresCortosYErrores(string corto, string completo, bool esperado)
        {
            Assert.Equal(esperado, ProgramacionDescarga.NombreCoincide(corto, completo));
        }

        private static DescargaPedidoService.CargaPendiente Carga(int id, string conductor, int idConductor, string carreta) =>
            new DescargaPedidoService.CargaPendiente
            {
                IdDespacho = id, NumeroPedido = "9900000001",
                NombreConductor = conductor, IdConductor = idConductor, PlacaCarreta = carreta
            };

        private static readonly List<KeyValuePair<int, string>> Conductores = new List<KeyValuePair<int, string>>
        {
            new KeyValuePair<int, string>(1, "PEDRO JOSE RAMOS LUNA - DNI: 1"),
            new KeyValuePair<int, string>(2, "MARIO QUISPE ROJAS - DNI: 2"),
            new KeyValuePair<int, string>(3, "ANA MARIA TORRES DIAZ - DNI: 3"),
            new KeyValuePair<int, string>(4, "LUIS PRADO VEGA - DNI: 4"),
            new KeyValuePair<int, string>(5, "LUIS ALBERTO PRADO CRUZ - DNI: 5"),
            new KeyValuePair<int, string>(6, "JULIO ARMANDO MENA RIOS - DNI: 6"),
            new KeyValuePair<int, string>(7, "TULIO MERA SOTO NAVAS - DNI: 7")
        };

        [Fact]
        public void Proponer_PrefiereLaCoincidenciaExactaAntesQueLaTolerante()
        {
            var seccion = new SeccionProgramacion
            {
                Filas = { new FilaProgramacion { Chofer = "JULIO MENA", Tracto = "BBB-222", Carreta = "TXB-001", Cambio = "PEDRO RAMOS" } }
            };
            var cargas = new List<DescargaPedidoService.CargaPendiente> { Carga(100, "PEDRO JOSE RAMOS LUNA", 1, "TXB-001") };

            var propuestas = ProgramacionDescarga.Proponer(cargas, seccion, Conductores, Tractos, out _);

            Assert.Equal(6, propuestas[100].IdConductor);   // no TULIO MERA (una letra de diferencia en cada palabra)
            Assert.Empty(propuestas[100].Avisos);
        }

        private static readonly List<KeyValuePair<int, string>> Tractos = new List<KeyValuePair<int, string>>
        {
            new KeyValuePair<int, string>(20, "BBB-222"),
            new KeyValuePair<int, string>(30, "CCC-333")
        };

        [Fact]
        public void SeccionDelPedido_PorNumeroOPorCarretas()
        {
            var p = LectorProgramacionWord.Interpretar(ProgramacionDeEjemplo());

            Assert.Equal("GUAYAQUIL-VITAPRO", ProgramacionDescarga.SeccionDelPedido(p, "9900000001", new string[0]).Titulo);
            Assert.Equal("GUAYAQUIL-VITAPRO", ProgramacionDescarga.SeccionDelPedido(p, "1234", new[] { "TXB002" }).Titulo);
            Assert.Null(ProgramacionDescarga.SeccionDelPedido(p, "1234", new[] { "ZZZ-999" }));
        }

        [Fact]
        public void Proponer_UnePorCarretaYCompruebaElCambio()
        {
            var seccion = LectorProgramacionWord.Interpretar(ProgramacionDeEjemplo()).Secciones[1];
            var cargas = new List<DescargaPedidoService.CargaPendiente>
            {
                Carga(100, "PEDRO JOSE RAMOS LUNA", 1, "TXB001"),      // relevo: sale Pedro Ramos, descarga Mario Quispe
                Carga(101, "ANA MARIA TORRES DIAZ", 3, "TXB-002"),     // sin cambio: descarga la misma
                Carga(102, "LUIS PRADO VEGA", 4, "TXZ-009")            // no figura en la programación
            };

            var propuestas = ProgramacionDescarga.Proponer(cargas, seccion, Conductores, Tractos, out var generales);

            var relevo = propuestas[100];
            Assert.True(relevo.EnProgramacion);
            Assert.Equal(2, relevo.IdConductor);
            Assert.Equal(20, relevo.IdTracto);
            Assert.Empty(relevo.Avisos);

            var mismo = propuestas[101];
            Assert.Equal(3, mismo.IdConductor);
            Assert.Equal(30, mismo.IdTracto);
            Assert.Empty(mismo.Avisos);

            var fuera = propuestas[102];
            Assert.False(fuera.EnProgramacion);
            Assert.Null(fuera.IdConductor);
            Assert.Contains("no figura", Assert.Single(fuera.Avisos));

            Assert.Empty(generales);
        }

        [Fact]
        public void Proponer_AvisaLasDiferenciasConLaCarga()
        {
            var seccion = new SeccionProgramacion
            {
                Filas =
                {
                    new FilaProgramacion { Chofer = "MARIO QUISPE", Tracto = "BBB-222", Carreta = "TXB-001", Cambio = "JUAN SOTO" },
                    new FilaProgramacion { Chofer = "ANA TORRES", Tracto = "XXX-999", Carreta = "TXB-002" },
                    new FilaProgramacion { Chofer = "LUIS PRADO", Tracto = "CCC-333", Carreta = "TXB-003" },
                    new FilaProgramacion { Chofer = "RAUL NUÑEZ", Tracto = "CCC-333", Carreta = "TXB-004" },
                    new FilaProgramacion { Chofer = "OTRO CHOFER", Tracto = "BBB-222", Carreta = "TXB-999" }
                }
            };
            var cargas = new List<DescargaPedidoService.CargaPendiente>
            {
                Carga(100, "PEDRO JOSE RAMOS LUNA", 1, "TXB-001"),
                Carga(101, "PEDRO JOSE RAMOS LUNA", 1, "TXB-002"),
                Carga(102, "LUIS ALBERTO PRADO CRUZ", 5, "TXB-003"),
                Carga(103, "PEDRO JOSE RAMOS LUNA", 1, "TXB-004")
            };

            var propuestas = ProgramacionDescarga.Proponer(cargas, seccion, Conductores, Tractos, out var generales);

            // CAMBIO no es quien cargó
            Assert.Contains(propuestas[100].Avisos, a => a.Contains("saca a JUAN SOTO") && a.Contains("PEDRO JOSE RAMOS LUNA"));
            // Sin cambio, pero la carga la hizo otro; y el tracto no existe
            Assert.Contains(propuestas[101].Avisos, a => a.Contains("no indica cambio"));
            Assert.Contains(propuestas[101].Avisos, a => a.Contains("XXX-999"));
            Assert.Null(propuestas[101].IdTracto);
            // Dos "LUIS PRADO": se queda con el que cargó
            Assert.Equal(5, propuestas[102].IdConductor);
            Assert.Empty(propuestas[102].Avisos);
            // Chofer que no está registrado
            Assert.Null(propuestas[103].IdConductor);
            Assert.Contains(propuestas[103].Avisos, a => a.Contains("no está registrado como conductor"));
            // Fila de la programación sin carga pendiente
            Assert.Contains("TXB-999", Assert.Single(generales));
        }
    }
}
