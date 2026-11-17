using System;
using System.Collections.Generic;
using System.IO;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Partes puras de "Finalizar lote" todo o nada (tarea 10 del bloque 4).</summary>
    public class FinalizarLoteServiceTests
    {
        private static LoteDespachos Lote(bool internacional = false)
        {
            var lote = new LoteDespachos
            {
                NombreCliente = "VITAPRO", TipoOperacion = "CARGA", EsInternacional = internacional,
                PlantaOperacion = "Planta Trujillo", NumeroPedido = "4400123"
            };
            lote.Documentacion.NumeroFactura = "F001-1";
            lote.Documentacion.NumeroCPIC = "123456";
            return lote;
        }

        [Fact]
        public void Archivos_declarados_que_no_estan_en_disco_bloquean_el_lote()
        {
            var lote = Lote(internacional: true);
            lote.Documentacion.FacturaArchivoRutaTemp = @"C:\Uploads\Factura\_temp\a.pdf";
            lote.Documentacion.CPICArchivoRutaTemp = @"C:\Uploads\CPIC\_temp\b.pdf";
            lote.Conductores.Add(new ConductorLote
            {
                NombreConductor = "JUAN", ManifiestoCruceRutaTemp = @"C:\Uploads\Manifiesto\_temp\c.pdf",
                ManifiestoRegresoRutaTemp = @"C:\Uploads\Manifiesto\_temp\d.pdf"
            });

            var errores = FinalizarLoteService.ArchivosFaltantes(lote, ruta => ruta.EndsWith("b.pdf"));

            Assert.Equal(3, errores.Count);
            Assert.Contains(errores, e => e.Contains("El documento de la factura F001-1 que adjuntó al iniciar el lote ya no está en el servidor. Cancele el lote"));
            Assert.Contains(errores, e => e.StartsWith("JUAN") && e.Contains("manifiesto de cruce"));
            Assert.Contains(errores, e => e.StartsWith("JUAN") && e.Contains("manifiesto de retorno"));
            Assert.DoesNotContain(errores, e => e.Contains("CPIC"));
        }

        [Fact]
        public void Sin_archivos_declarados_no_hay_faltantes()
        {
            var lote = Lote();
            lote.Conductores.Add(new ConductorLote { NombreConductor = "JUAN" });
            Assert.Empty(FinalizarLoteService.ArchivosFaltantes(lote, _ => false));
        }

        [Fact]
        public void Guia_repetida_dentro_del_lote_bloquea()
        {
            var lote = Lote();
            lote.Conductores.Add(new ConductorLote { NombreConductor = "JUAN", GuiaRemitente = "T001-100" });
            lote.Conductores.Add(new ConductorLote { NombreConductor = "PEDRO", GuiaRemitente = " t001-100 " });
            lote.Conductores.Add(new ConductorLote { NombreConductor = "LUIS", GuiaRemitente = "T001-101" });
            lote.Conductores.Add(new ConductorLote { NombreConductor = "ANA" });
            lote.Conductores.Add(new ConductorLote { NombreConductor = "EVA", GuiaRemitente = "  " });

            var errores = FinalizarLoteService.GuiasRepetidasEnLote(lote);

            var error = Assert.Single(errores);
            Assert.Contains("T001-100", error);
            Assert.Contains("JUAN", error);
            Assert.Contains("PEDRO", error);
        }

        [Fact]
        public void Agregar_conductor_rechaza_guia_ya_usada_en_otro_del_lote()
        {
            var otros = new List<ConductorLote>
            {
                new ConductorLote { NombreConductor = "JUAN", GuiaRemitente = "T001-100" },
                new ConductorLote { NombreConductor = "ANA" },
            };

            string error = FinalizarLoteService.GuiaRepetidaEnOtros(otros, "  t001-100 ");

            Assert.NotNull(error);
            Assert.Contains("t001-100", error);
            Assert.Contains("JUAN", error);
        }

        [Theory]
        [InlineData("T001-101")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Agregar_conductor_acepta_guia_nueva_o_vacia(string guia)
        {
            var otros = new List<ConductorLote>
            {
                new ConductorLote { NombreConductor = "JUAN", GuiaRemitente = "T001-100" },
                new ConductorLote { NombreConductor = "ANA", GuiaRemitente = "  " },
                new ConductorLote { NombreConductor = "EVA" },
            };

            Assert.Null(FinalizarLoteService.GuiaRepetidaEnOtros(otros, guia));
        }

        private static List<ConductorLote> TresConductores() => new List<ConductorLote>
        {
            new ConductorLote { NombreConductor = "JUAN", GuiaRemitente = "T001-100" },
            new ConductorLote { NombreConductor = "PEDRO", GuiaRemitente = "T001-200" },
            new ConductorLote { NombreConductor = "LUIS", GuiaRemitente = "T001-300" },
        };

        [Fact]
        public void Editar_conductor_sin_cambiar_su_guia_no_choca_consigo_mismo()
        {
            var otros = FinalizarLoteService.OtrosConductores(TresConductores(), 1);   // se edita PEDRO

            Assert.Equal(new[] { "JUAN", "LUIS" }, otros.ConvertAll(c => c.NombreConductor));
            Assert.Null(FinalizarLoteService.GuiaRepetidaEnOtros(otros, "T001-200"));
        }

        [Fact]
        public void Editar_conductor_con_la_guia_de_otro_se_rechaza()
        {
            var otros = FinalizarLoteService.OtrosConductores(TresConductores(), 1);

            string error = FinalizarLoteService.GuiaRepetidaEnOtros(otros, "t001-300");

            Assert.NotNull(error);
            Assert.Contains("LUIS", error);
        }

        [Fact]
        public void Agregar_conductor_valida_contra_todos_los_del_lote()
        {
            var otros = FinalizarLoteService.OtrosConductores(TresConductores(), -1);

            Assert.Equal(3, otros.Count);
            Assert.NotNull(FinalizarLoteService.GuiaRepetidaEnOtros(otros, "T001-200"));
        }

        [Fact]
        public void Lote_con_commit_en_duda_no_se_vuelve_a_finalizar()
        {
            var lote = Lote();
            lote.Conductores.Add(new ConductorLote { NombreConductor = "JUAN" });
            Assert.Null(FinalizarLoteService.MotivoNoReintentar(lote));

            lote.CommitEnDuda = true;
            Assert.Equal(FinalizarLoteService.MensajeNoReintentar, FinalizarLoteService.MotivoNoReintentar(lote));

            // El servicio corta antes de revalidar o escribir (no toca la BD)
            var r = FinalizarLoteService.Finalizar(lote, Path.GetTempPath());
            Assert.False(r.Ok);
            Assert.True(r.CommitEnDuda);
            Assert.Equal(FinalizarLoteService.MensajeNoReintentar, Assert.Single(r.Errores));
            Assert.Empty(r.IdsPorConductor);
        }

        [Fact]
        public void Celda_carga_codifica_el_resumen_y_los_lotes()
        {
            string html = WebSGV.Views.RegistroDespacho.CeldaCarga("<script>alert(1)</script> \"x\"", "L1\" onmouseover=\"y\", <b>L2</b>");

            Assert.DoesNotContain("<script>", html);
            Assert.DoesNotContain("<b>", html);
            Assert.DoesNotContain("\" onmouseover", html);
            Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt; &quot;x&quot;", html);
            Assert.Contains("title=\"L1&quot; onmouseover=&quot;y&quot;, &lt;b&gt;L2&lt;/b&gt;\"", html);
            Assert.StartsWith("<span title=\"", html);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Celda_carga_sin_carga_muestra_el_guion_atenuado(string resumen)
        {
            Assert.Equal("<span class=\"rd-sin-datos\">—</span>", WebSGV.Views.RegistroDespacho.CeldaCarga(resumen, "L1"));
        }

        [Fact]
        public void Celda_carga_sin_lotes_no_pone_titulo()
        {
            Assert.Equal("1,188 bolsas", WebSGV.Views.RegistroDespacho.CeldaCarga("1,188 bolsas", ""));
        }

        [Fact]
        public void Commit_en_duda_avisa_que_revise_la_lista_antes_de_reintentar()
        {
            string mensaje = FinalizarLoteService.MensajeFallo(
                new System.Transactions.TransactionInDoubtException("se cortó"), "Lote");

            Assert.Equal(FinalizarLoteService.MensajeCommitEnDuda, mensaje);
            Assert.DoesNotContain("No se guardó nada", mensaje);
            Assert.Contains("Lista de despachos", mensaje);
        }

        [Fact]
        public void Fallo_comun_dice_que_no_se_guardo_nada_y_el_paso()
        {
            string mensaje = FinalizarLoteService.MensajeFallo(
                new WebSGV.Services.Common.ErrorNegocioException("La placa no existe."), "JUAN");

            Assert.Equal("No se guardó nada. JUAN: La placa no existe.", mensaje);
        }

        [Fact]
        public void La_auditoria_lleva_todos_los_ids()
        {
            var lote = Lote();
            string texto = FinalizarLoteService.DescripcionAuditoria(lote, new List<int> { 501, 502 }, new List<int> { 400 }, 77, 88);

            Assert.Contains("Lote de 3 despacho(s)", texto);
            Assert.Contains("Cliente: VITAPRO", texto);
            Assert.Contains("Planta: Planta Trujillo", texto);
            Assert.Contains("Pedido: 4400123", texto);
            Assert.Contains("Factura: F001-1 (id 77)", texto);
            Assert.Contains("CPIC: 123456 (id 88)", texto);
            Assert.Contains("Despachos creados: 501, 502", texto);
            Assert.Contains("despachos programados 400", texto);
        }

        [Fact]
        public void La_ruta_destino_queda_dentro_de_la_raiz_con_el_nombre_de_siempre()
        {
            string raiz = Path.Combine(Path.GetTempPath(), "sgv_raiz") + Path.DirectorySeparatorChar;
            var a = FinalizarLoteService.RutaDestino(raiz, "Factura", "FACTURA_15", ".pdf", new DateTime(2026, 3, 4, 5, 6, 7));

            Assert.Equal("FACTURA_15_20260304_050607.pdf", a.NombreArchivo);
            Assert.Equal("~/Uploads/Factura/2026/03/FACTURA_15_20260304_050607.pdf", a.RutaVirtual);
            Assert.Equal(Path.Combine(raiz, "Factura", "2026", "03", a.NombreArchivo), a.RutaFisica);
            Assert.StartsWith(raiz, Path.GetFullPath(a.RutaFisica), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void La_ruta_destino_fuera_de_la_raiz_se_rechaza()
        {
            string raiz = Path.Combine(Path.GetTempPath(), "sgv_raiz") + Path.DirectorySeparatorChar;
            Assert.Throws<InvalidOperationException>(() =>
                FinalizarLoteService.RutaDestino(raiz, @"..\..\otra", "FACTURA_15", ".pdf", DateTime.Now));
            Assert.Throws<InvalidOperationException>(() =>
                FinalizarLoteService.RutaDestino(null, "Factura", "FACTURA_15", ".pdf", DateTime.Now));
        }
    }
}
