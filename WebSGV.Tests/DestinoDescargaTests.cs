using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;
using Xunit;
using static WebSGV.Services.Despachos.DestinoDescargaService;

namespace WebSGV.Tests
{
    // La lista de ejemplo reproduce la forma de la que manda el cliente a transporte
    // (pedido, "tracto - carreta", lugar de recepción); las placas y el pedido son inventados.
    public class DestinoDescargaTests
    {
        private const string Encabezado = "Fecha Recepción Bodega\tPEDIDO\tUnidad\tPlaca Camión\tEmpresa de transporte\tLugar de recepción";

        private const string ListaPegada = Encabezado + "\n" +
            "25/9/2026\t9900000001\t1\tAAA111 - TXA-001\tVIVIANA\tMILAGRO\n" +
            "25/9/2026\t9900000001\t1\tBBB222 - TXB-002\tVIVIANA\tJAVE\n" +
            "25/9/2026\t9900000001\t1\tCCC333 - TXC-003\tVIVIANA\tEL MILAGRO\n";

        private static readonly List<Planta> Plantas = new List<Planta>
        {
            new Planta { Id = 5, Nombre = "Guayaquil" },
            new Planta { Id = 9, Nombre = "Milagro" },
            new Planta { Id = 10, Nombre = "Jave" }
        };

        private static VistaPrevia Vista(IEnumerable<LectorDestinosDescarga.FilaDestino> filas,
            IDictionary<string, DestinoVigente> vigentes = null, ISet<string> carretas = null) =>
            Preparar(filas, Plantas, vigentes ?? new Dictionary<string, DestinoVigente>(), null, carretas);

        // ── Placas ───────────────────────────────────────────────────────

        [Theory]
        [InlineData("BSP816 - TBE-995", "BSP816", "TBE995")]
        [InlineData("BSP-816 / TBE 995", "BSP816", "TBE995")]
        [InlineData("bsp816-tbe995", "BSP816", "TBE995")]
        [InlineData("  CBU928   -   TMB-988 ", "CBU928", "TMB988")]
        public void SepararPlacas_PartePorCaracteresSinImportarLosGuiones(string texto, string tracto, string carreta)
        {
            Assert.True(LectorDestinosDescarga.SepararPlacas(texto, out var t, out var c));
            Assert.Equal(tracto, t);
            Assert.Equal(carreta, c);
        }

        [Fact]
        public void SepararPlacas_UnaSolaPlaca_DejaLaCarretaVacia()
        {
            Assert.True(LectorDestinosDescarga.SepararPlacas("BSP816", out var t, out var c));
            Assert.Equal("BSP816", t);
            Assert.Null(c);
        }

        [Theory]
        [InlineData("")]
        [InlineData("BSP81")]
        [InlineData("BSP816-TBE-99")]
        public void SepararPlacas_FormatoRaro_NoSeAdivina(string texto)
        {
            Assert.False(LectorDestinosDescarga.SepararPlacas(texto, out _, out _));
        }

        // ── Lectura ──────────────────────────────────────────────────────

        [Fact]
        public void LeerTexto_ListaPegadaConEncabezado()
        {
            var filas = LectorDestinosDescarga.LeerTexto(ListaPegada);

            Assert.Equal(3, filas.Count);
            Assert.Equal(new DateTime(2026, 9, 25), filas[0].Fecha);
            Assert.Equal("9900000001", filas[0].Pedido);
            Assert.Equal("AAA111", filas[0].PlacaTracto);
            Assert.Equal("TXA001", filas[0].PlacaCarreta);
            Assert.Equal("MILAGRO", filas[0].Lugar);   // y no "VIVIANA", la empresa
            Assert.Equal("JAVE", filas[1].Lugar);
            Assert.Equal(2, filas[0].Fila);   // la primera fila de datos es la 2 (la 1 es el título)
        }

        [Fact]
        public void LeerTexto_SinEncabezado_ReconoceLasColumnasPorSuContenido()
        {
            var filas = LectorDestinosDescarga.LeerTexto(
                "25/9/2026\t9900000001\t1\tAAA111 - TXA-001\tVIVIANA\tJAVE");

            var f = Assert.Single(filas);
            Assert.Equal("9900000001", f.Pedido);
            Assert.Equal("TXA001", f.PlacaCarreta);
            Assert.Equal("JAVE", f.Lugar);
        }

        [Fact]
        public void LeerTexto_ColumnasEnOtroOrden_SeUbicanPorSuTitulo()
        {
            var filas = LectorDestinosDescarga.LeerTexto(
                "LUGAR DE RECEPCIÓN\tPLACA CAMIÓN\tPEDIDO\n" +
                "JAVE\tAAA111 - TXA-001\t9900000001");

            var f = Assert.Single(filas);
            Assert.Equal("JAVE", f.Lugar);
            Assert.Equal("9900000001", f.Pedido);
            Assert.Equal("AAA111", f.PlacaTracto);
        }

        [Fact]
        public void LeerTexto_PedidoVacio_TomaElDeLaFilaAnteriorYAvisa()
        {
            var filas = LectorDestinosDescarga.LeerTexto(
                Encabezado + "\n" +
                "25/9/2026\t9900000001\t1\tAAA111 - TXA-001\tVIVIANA\tJAVE\n" +
                "25/9/2026\t\t1\tBBB222 - TXB-002\tVIVIANA\tMILAGRO");

            Assert.Equal("9900000001", filas[1].Pedido);
            Assert.Contains(filas[1].Avisos, a => a.Contains("fila anterior"));
        }

        [Fact]
        public void LeerTexto_IgnoraRenglonesSueltosYVacios()
        {
            var filas = LectorDestinosDescarga.LeerTexto(
                Encabezado + "\n\n" +
                "TOTAL\t\t\t\t\t\n" +
                "25/9/2026\t9900000001\t1\tAAA111 - TXA-001\tVIVIANA\tJAVE\n");

            Assert.Single(filas);
        }

        [Fact]
        public void LeerExcel_LeeFechasComoFechaYElPedidoComoNumero()
        {
            using (var libro = new XLWorkbook())
            {
                var h = libro.AddWorksheet("Hoja1");
                string[] titulos = { "Fecha Recepción Bodega", "PEDIDO", "Unidad", "Placa Camión", "Empresa de transporte", "Lugar de recepción" };
                for (int i = 0; i < titulos.Length; i++) h.Cell(1, i + 1).Value = titulos[i];
                h.Cell(2, 1).Value = new DateTime(2026, 9, 25);
                h.Cell(2, 2).Value = 9900000001;
                h.Cell(2, 3).Value = 1;
                h.Cell(2, 4).Value = "AAA111 - TXA-001";
                h.Cell(2, 5).Value = "VIVIANA";
                h.Cell(2, 6).Value = "JAVE";

                using (var ms = new MemoryStream())
                {
                    libro.SaveAs(ms);
                    ms.Position = 0;
                    var f = Assert.Single(LectorDestinosDescarga.LeerExcel(ms));

                    Assert.Equal(2, f.Fila);
                    Assert.Equal(new DateTime(2026, 9, 25), f.Fecha);
                    Assert.Equal("9900000001", f.Pedido);
                    Assert.Equal("TXA001", f.PlacaCarreta);
                    Assert.Equal("JAVE", f.Lugar);
                }
            }
        }

        // ── Plantas ──────────────────────────────────────────────────────

        [Theory]
        [InlineData("MILAGRO", 9)]
        [InlineData("Milagro", 9)]
        [InlineData("EL MILAGRO", 9)]
        [InlineData("CD VITAPRO MILAGRO - Ecuador", 9)]
        [InlineData("INBALNOR", 9)]
        [InlineData("JAVE", 10)]
        [InlineData("  jave ", 10)]
        public void ResolverPlanta_ReconoceLasFormasDeEscribirlas(string lugar, int esperada)
        {
            Assert.Equal(esperada, ResolverPlanta(lugar, Plantas)?.Id);
        }

        [Theory]
        [InlineData("")]
        [InlineData("QUITO NORTE")]
        [InlineData("MILAGRO O JAVE")]   // dos plantas: no se adivina
        public void ResolverPlanta_SinCoincidenciaClara_DevuelveNull(string lugar)
        {
            Assert.Null(ResolverPlanta(lugar, Plantas));
        }

        // ── Vista previa ─────────────────────────────────────────────────

        [Fact]
        public void Preparar_FilasNuevas_QuedanComoNuevasConSuPlanta()
        {
            var v = Vista(LectorDestinosDescarga.LeerTexto(ListaPegada));

            Assert.Equal(3, v.Nuevos);
            Assert.Equal(0, v.Errores);
            Assert.Equal(new[] { "Milagro", "Jave", "Milagro" }, v.Filas.Select(f => f.Planta));
            Assert.All(v.Filas, f => Assert.True(f.SeAplica));
        }

        [Fact]
        public void Preparar_ConLoVigente_DistingueSinCambioYCambioDePlanta()
        {
            var vigentes = new Dictionary<string, DestinoVigente>
            {
                [Clave("9900000001", "TXA001")] = new DestinoVigente { IdPlanta = 9, Planta = "Milagro" },   // igual
                [Clave("9900000001", "TXB002")] = new DestinoVigente { IdPlanta = 9, Planta = "Milagro" }    // ahora va a Jave
            };

            var v = Vista(LectorDestinosDescarga.LeerTexto(ListaPegada), vigentes);

            Assert.Equal(EstadoDestino.SinCambio, v.Filas[0].Estado);
            Assert.Equal(EstadoDestino.Cambia, v.Filas[1].Estado);
            Assert.Equal("Milagro", v.Filas[1].PlantaAnterior);
            Assert.Equal(EstadoDestino.Nuevo, v.Filas[2].Estado);
            Assert.Equal(2, v.Aplicables);
        }

        [Theory]
        [InlineData("25/9/2026\t\t1\tAAA111 - TXA-001\tVIVIANA\tJAVE", "pedido")]
        [InlineData("25/9/2026\tABC\t1\tAAA111 - TXA-001\tVIVIANA\tJAVE", "pedido")]
        [InlineData("25/9/2026\t9900000001\t1\tAAA111\tVIVIANA\tJAVE", "carreta")]
        [InlineData("25/9/2026\t9900000001\t1\tAAA111 - TXA-001\tVIVIANA\tQUITO NORTE", "planta")]
        [InlineData("25/9/2026\t9900000001\t1\tAAA111 - TXA-001\tVIVIANA\t", "lugar")]
        public void Preparar_FilasInvalidas_SeRechazanConSuMotivo(string fila, string palabraDelMotivo)
        {
            var v = Vista(LectorDestinosDescarga.LeerTexto(Encabezado + "\n" + fila));

            var f = Assert.Single(v.Filas);
            Assert.Equal(EstadoDestino.Error, f.Estado);
            Assert.False(f.SeAplica);
            Assert.Contains(palabraDelMotivo, f.Avisos[0], StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Preparar_CarretaRepetidaConLaMismaPlanta_SeCargaUnaVez()
        {
            var v = Vista(LectorDestinosDescarga.LeerTexto(Encabezado + "\n" +
                "25/9/2026\t9900000001\t1\tAAA111 - TXA-001\tVIVIANA\tJAVE\n" +
                "25/9/2026\t9900000001\t1\tAAA111 - TXA-001\tVIVIANA\tJAVE"));

            Assert.Equal(1, v.Aplicables);
            Assert.Equal(1, v.SinCambio);
            Assert.Equal(0, v.Errores);
        }

        [Fact]
        public void Preparar_CarretaRepetidaConPlantasDistintas_NoSeCargaNinguna()
        {
            var v = Vista(LectorDestinosDescarga.LeerTexto(Encabezado + "\n" +
                "25/9/2026\t9900000001\t1\tAAA111 - TXA-001\tVIVIANA\tJAVE\n" +
                "25/9/2026\t9900000001\t1\tAAA111 - TXA-001\tVIVIANA\tMILAGRO"));

            Assert.Equal(2, v.Errores);
            Assert.Equal(0, v.Aplicables);
        }

        [Fact]
        public void Preparar_LaMismaCarretaEnOtroPedido_NoEsRepetida()
        {
            var v = Vista(LectorDestinosDescarga.LeerTexto(Encabezado + "\n" +
                "25/9/2026\t9900000001\t1\tAAA111 - TXA-001\tVIVIANA\tJAVE\n" +
                "26/9/2026\t9900000002\t1\tAAA111 - TXA-001\tVIVIANA\tMILAGRO"));

            Assert.Equal(2, v.Nuevos);
        }

        [Fact]
        public void Preparar_PlacaFueraDeLaFlota_AvisaPeroSeCarga()
        {
            var v = Vista(LectorDestinosDescarga.LeerTexto(ListaPegada),
                carretas: new HashSet<string> { "TXA001", "TXB002" });

            Assert.Equal(3, v.Aplicables);
            Assert.Empty(v.Filas[0].Avisos);
            Assert.Contains(v.Filas[2].Avisos, a => a.Contains("TXC003"));
        }
    }
}
