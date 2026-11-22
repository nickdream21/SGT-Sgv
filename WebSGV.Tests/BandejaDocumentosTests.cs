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
    /// Bandeja de documentos: unión de cada guía del correo con su despacho programado (sin BD) y la
    /// elección del usuario en la vista previa.
    /// </summary>
    public class BandejaDocumentosTests
    {
        private static readonly DateTime Dia = new DateTime(2026, 11, 3);

        private static GuiaBandeja Guia(string numero, int? idTracto, int? idCarreta, DateTime? traslado = null, int cliente = 1,
                                        EstadoGuiaBandeja estado = EstadoGuiaBandeja.SinProgramado) =>
            new GuiaBandeja
            {
                IdCliente = cliente, Estado = estado,
                Tracto = new TractoImportado { NumeroGuia = numero, IdTracto = idTracto, IdCarreta = idCarreta, FechaTraslado = traslado ?? Dia, Placa = "PL" + idTracto }
            };

        private static DespachoProgramado Prog(int id, int idTracto, int idCarreta, int dias = 0, int cliente = 1, int hora = 6) =>
            new DespachoProgramado
            {
                IdDespacho = id, IdCliente = cliente, IdTracto = idTracto, IdCarreta = idCarreta, IdConductor = 100 + id,
                FechaDespacho = Dia.AddDays(dias), Hora = new TimeSpan(hora, 0, 0),
                PlacaTracto = "T" + idTracto, PlacaCarreta = "C" + idCarreta, NombreConductor = "CHOFER " + id
            };

        [Fact]
        public void Une_por_mismo_tracto_aunque_cargue_al_dia_siguiente()
        {
            var g = Guia("T660-1", 10, 20);
            BandejaDocumentosService.Unir(new[] { g }, new[] { Prog(1, 10, 99, dias: 1) });

            Assert.Equal(EstadoGuiaBandeja.Unida, g.Estado);
            Assert.Equal(1, g.IdProgramado);
            Assert.Equal("Mismo tracto", g.Regla);
        }

        [Fact]
        public void Sin_el_tracto_une_por_la_carreta_y_avisa_el_cambio()
        {
            var g = Guia("T660-1", 10, 20);
            g.Tracto.IdConductor = 555;
            BandejaDocumentosService.Unir(new[] { g }, new[] { Prog(1, 11, 21), Prog(2, 12, 20) });

            Assert.Equal(2, g.IdProgramado);
            Assert.StartsWith("Misma carreta", g.Regla);
            Assert.Contains(g.Avisos, a => a.Contains("queda el tracto programado T12"));
            Assert.Contains(g.Avisos, a => a.Contains("queda CHOFER 2"));
        }

        [Fact]
        public void Una_guia_y_un_programado_libres_sin_coincidencia_no_se_unen_por_descarte()
        {
            var a = Guia("T660-1", 10, 20);
            var b = Guia("T660-2", 30, 40);   // ni tracto ni carreta coinciden
            BandejaDocumentosService.Unir(new[] { a, b }, new[] { Prog(1, 10, 20), Prog(2, 77, 78) });

            Assert.Equal(1, a.IdProgramado);
            Assert.False(a.PorElegir);
            // Aunque sea el único libre, no se preselecciona: el usuario elige el camión (H8).
            Assert.Null(b.IdProgramado);
            Assert.Null(b.Regla);
            Assert.Equal(EstadoGuiaBandeja.SinProgramado, b.Estado);
            Assert.True(b.PorElegir);
            Assert.Equal(new List<int> { 1, 2 }, b.Candidatos);
        }

        [Fact]
        public void Con_dos_libres_no_adivina()
        {
            var a = Guia("T660-1", 30, 40);
            BandejaDocumentosService.Unir(new[] { a }, new[] { Prog(1, 77, 78), Prog(2, 88, 89) });

            Assert.Equal(EstadoGuiaBandeja.SinProgramado, a.Estado);
            Assert.Null(a.IdProgramado);
            Assert.True(a.PorElegir);
            Assert.Equal(new List<int> { 1, 2 }, a.Candidatos);   // el usuario elige
        }

        [Fact]
        public void Fuera_de_la_ventana_o_de_otro_cliente_no_se_une()
        {
            var antes = Guia("T660-1", 10, 20);
            var otroCliente = Guia("T660-2", 11, 21, cliente: 2);
            BandejaDocumentosService.Unir(new[] { antes, otroCliente },
                new[] { Prog(1, 10, 20, dias: -1), Prog(2, 10, 20, dias: BandejaDocumentosService.DiasDespuesDeLaGuia + 1), Prog(3, 11, 21, cliente: 3) });

            Assert.Null(antes.IdProgramado);
            Assert.Empty(antes.Candidatos);
            Assert.Null(otroCliente.IdProgramado);
        }

        [Fact]
        public void Cada_programado_se_usa_una_sola_vez()
        {
            var a = Guia("T660-1", 10, 20);
            var b = Guia("T660-2", 10, 21, traslado: Dia);
            BandejaDocumentosService.Unir(new[] { a, b }, new[] { Prog(1, 10, 20) });

            Assert.Equal(1, a.IdProgramado);
            Assert.Null(b.IdProgramado);
        }

        [Fact]
        public void Las_guias_ya_registradas_no_se_tocan()
        {
            var ya = Guia("T660-1", 10, 20, estado: EstadoGuiaBandeja.YaRegistrada);
            BandejaDocumentosService.Unir(new[] { ya }, new[] { Prog(1, 10, 20) });

            Assert.Equal(EstadoGuiaBandeja.YaRegistrada, ya.Estado);
            Assert.Null(ya.IdProgramado);
        }

        private static LecturaBandeja Lectura(params GuiaBandeja[] guias)
        {
            var l = new LecturaBandeja();
            var p = new PedidoBandeja { Pedido = new ImportacionPedido { NumeroPedido = "4400000001" } };
            p.Guias.AddRange(guias);
            l.Pedidos.Add(p);
            foreach (var x in new[] { Prog(1, 10, 20), Prog(2, 77, 78) }) l.Programados[x.IdDespacho] = x;
            BandejaDocumentosService.Unir(guias, l.Programados.Values.ToList());
            return l;
        }

        [Fact]
        public void El_usuario_puede_cambiar_o_quitar_el_despacho()
        {
            var a = Guia("T660-1", 10, 20);
            var b = Guia("T660-2", 30, 40);
            var l = Lectura(a, b);

            var errores = BandejaDocumentosService.AplicarEleccion(l, E(("T660-1", null), ("T660-2", 1)));

            Assert.Empty(errores);
            Assert.Equal(EstadoGuiaBandeja.SinProgramado, a.Estado);
            Assert.Equal(1, b.IdProgramado);
            Assert.Equal(EstadoGuiaBandeja.Unida, b.Estado);
        }

        [Fact]
        public void No_deja_elegir_el_mismo_despacho_para_dos_guias()
        {
            var l = Lectura(Guia("T660-1", 10, 20), Guia("T660-2", 30, 40));
            var errores = BandejaDocumentosService.AplicarEleccion(l, E(("T660-1", 1), ("T660-2", 1)));
            Assert.Contains(errores, e => e.Contains("más de una guía"));
        }

        [Fact]
        public void No_deja_elegir_un_despacho_que_no_es_candidato()
        {
            var l = Lectura(Guia("T660-1", 10, 20));
            var errores = BandejaDocumentosService.AplicarEleccion(l, E(("T660-1", 999)));
            Assert.Contains(errores, e => e.Contains("no corresponde"));
        }

        private static Dictionary<string, EleccionGuia> E(params (string guia, int? id)[] elecciones) =>
            elecciones.ToDictionary(x => x.guia, x => EleccionGuia.Programado(x.id));

        // ── Despacho nuevo (guía sin programado) ────────────────────────────

        [Fact]
        public void Sin_programado_propone_un_despacho_nuevo_con_lo_que_dice_la_guia()
        {
            var g = Guia("T660-1", 10, 20);
            g.Tracto.IdConductor = 5;
            BandejaDocumentosService.Unir(new[] { g }, new DespachoProgramado[0]);

            Assert.Equal(EstadoGuiaBandeja.Nueva, g.Estado);
            Assert.Equal(5, g.NuevoIdConductor);
            Assert.Equal(10, g.NuevoIdTracto);
            Assert.Equal(20, g.NuevoIdCarreta);
            Assert.Equal(Dia, g.NuevaFecha);
        }

        [Fact]
        public void Sin_chofer_reconocido_no_lo_propone_solo()
        {
            var g = Guia("T660-1", 10, 20);   // IdConductor null
            BandejaDocumentosService.Unir(new[] { g }, new DespachoProgramado[0]);
            Assert.Equal(EstadoGuiaBandeja.SinProgramado, g.Estado);
        }

        [Fact]
        public void Con_un_programado_posible_no_propone_despacho_nuevo()
        {
            var g = Guia("T660-1", 30, 40);
            var otra = Guia("T660-2", 50, 60);
            g.Tracto.IdConductor = 5; otra.Tracto.IdConductor = 6;
            BandejaDocumentosService.Unir(new[] { g, otra }, new[] { Prog(1, 77, 78), Prog(2, 88, 89) });

            Assert.Equal(EstadoGuiaBandeja.SinProgramado, g.Estado);   // tiene candidatos: el usuario decide
        }

        [Fact]
        public void El_despacho_nuevo_pide_todos_sus_datos_y_no_repite_tracto()
        {
            var a = Guia("T660-1", 10, 20);
            var b = Guia("T660-2", 30, 40);
            var l = Lectura(a, b);
            var errores = BandejaDocumentosService.AplicarEleccion(l, new Dictionary<string, EleccionGuia>
            {
                ["T660-1"] = new EleccionGuia { Nuevo = true, IdConductor = 1, IdTracto = 10, IdCarreta = 20, Fecha = Dia },
                ["T660-2"] = new EleccionGuia { Nuevo = true, IdConductor = 2, IdTracto = 10, IdCarreta = null, Fecha = Dia }
            });

            Assert.Contains(errores, e => e.Contains("T660-2") && e.Contains("elija chofer"));
            Assert.Contains(errores, e => e.Contains("mismo tracto"));
            Assert.Equal(EstadoGuiaBandeja.Nueva, a.Estado);
            Assert.Equal(20, a.NuevoIdCarreta);
        }

        [Fact]
        public void El_usuario_puede_elegir_el_despacho_de_un_mci()
        {
            var l = new LecturaBandeja();
            l.Mcis.Add(new MciBandeja
            {
                Mci = new WebSGV.Services.Despachos.Importacion.MciLeido { Numero = "0019349" },
                Estado = EstadoAduanaBandeja.Elegir,
                Candidatos = new Dictionary<int, string> { [7] = "despacho 7", [8] = "despacho 8" }
            });
            Assert.Empty(BandejaDocumentosService.AplicarEleccion(l, new Dictionary<string, EleccionGuia>(), new Dictionary<int, int?> { [0] = 8 }));
            Assert.Equal(8, l.Mcis[0].IdDespacho);
            Assert.Equal(EstadoAduanaBandeja.Registrar, l.Mcis[0].Estado);

            Assert.NotEmpty(BandejaDocumentosService.AplicarEleccion(l, new Dictionary<string, EleccionGuia>(), new Dictionary<int, int?> { [0] = 99 }));
        }

        // ── Certeza de la unión ─────────────────────────────────────────────

        [Fact]
        public void La_guia_sin_coincidencia_queda_por_elegir_y_por_revisar_y_la_del_tracto_no()
        {
            var porTracto = Guia("T660-1", 10, 20);
            var sinCoincidencia = Guia("T660-2", 30, 40);
            BandejaDocumentosService.Unir(new[] { porTracto, sinCoincidencia }, new[] { Prog(1, 10, 20), Prog(2, 77, 78) });

            Assert.False(porTracto.PorElegir);
            Assert.False(porTracto.PorRevisar);
            Assert.True(sinCoincidencia.PorElegir);
            Assert.True(sinCoincidencia.PorRevisar);
        }

        [Fact]
        public void Con_coincidencia_por_tracto_o_carreta_sigue_preseleccionada()
        {
            var porTracto = Guia("T660-1", 10, 20);
            var porCarreta = Guia("T660-2", 30, 78);
            BandejaDocumentosService.Unir(new[] { porTracto, porCarreta }, new[] { Prog(1, 10, 20), Prog(2, 77, 78) });

            Assert.Equal(1, porTracto.IdProgramado);
            Assert.Equal("Mismo tracto", porTracto.Regla);
            Assert.Equal(2, porCarreta.IdProgramado);
            Assert.StartsWith("Misma carreta", porCarreta.Regla);
            Assert.False(porTracto.PorElegir);
            Assert.False(porCarreta.PorElegir);
        }

        [Fact]
        public void Al_elegir_un_despacho_la_guia_va_ahi()
        {
            var a = Guia("T660-1", 10, 20);
            var b = Guia("T660-2", 30, 40);
            var l = Lectura(a, b);

            Assert.Empty(BandejaDocumentosService.AplicarEleccion(l, E(("T660-1", 1), ("T660-2", 2))));

            Assert.Equal(EstadoGuiaBandeja.Unida, b.Estado);
            Assert.Equal(2, b.IdProgramado);
            Assert.Equal("Elegido por el usuario", b.Regla);
            Assert.False(b.PorElegir);
            Assert.False(b.PorRevisar);
            Assert.Empty(BandejaDocumentosService.QuedoSinRegistrar(l));
        }

        [Fact]
        public void Sin_elegir_no_se_registra_y_dice_por_que()
        {
            var a = Guia("T660-1", 10, 20);
            var b = Guia("T660-2", 30, 40);
            var l = Lectura(a, b);

            // Dejó "— Elija qué hacer —": no bloquea lo demás, pero esa guía no va a ningún despacho.
            var errores = BandejaDocumentosService.AplicarEleccion(l, new Dictionary<string, EleccionGuia>
            {
                ["T660-1"] = EleccionGuia.Programado(1),
                ["T660-2"] = EleccionGuia.SinElegir()
            });

            Assert.Empty(errores);
            Assert.Equal(EstadoGuiaBandeja.Unida, a.Estado);
            Assert.Equal(EstadoGuiaBandeja.SinProgramado, b.Estado);
            Assert.Null(b.IdProgramado);
            Assert.True(b.PorElegir);
            var quedo = Assert.Single(BandejaDocumentosService.QuedoSinRegistrar(l));
            Assert.StartsWith("Guía T660-2: elija a qué camión va o márquela como no registrar.", quedo);
        }

        [Fact]
        public void Si_el_selector_no_llega_la_guia_sigue_por_elegir()
        {
            var b = Guia("T660-2", 30, 40);
            var l = Lectura(b);
            Assert.Empty(BandejaDocumentosService.AplicarEleccion(l, new Dictionary<string, EleccionGuia>()));
            Assert.Null(b.IdProgramado);
            Assert.True(b.PorElegir);
        }

        [Fact]
        public void Marcarla_como_no_registrar_es_una_eleccion()
        {
            var b = Guia("T660-2", 30, 40);
            var l = Lectura(b);
            Assert.Empty(BandejaDocumentosService.AplicarEleccion(l, new Dictionary<string, EleccionGuia>
            {
                ["T660-2"] = BandejaDocumentosService.EleccionDesdeSelector(BandejaDocumentosService.NoRegistrar)
            }));
            Assert.False(b.PorElegir);
            Assert.Equal(EstadoGuiaBandeja.SinProgramado, b.Estado);
            Assert.Contains(BandejaDocumentosService.QuedoSinRegistrar(l), x => x.Contains("T660-2") && x.Contains("no se eligió su despacho"));
        }

        [Fact]
        public void Sin_candidatos_el_valor_vacio_es_no_registrar_y_no_por_elegir()
        {
            var nueva = Guia("T660-1", 10, 20, cliente: 9);   // de otro cliente: sin candidatos
            nueva.Tracto.IdConductor = 5;
            var l = Lectura(nueva);
            Assert.Equal(EstadoGuiaBandeja.Nueva, nueva.Estado);

            Assert.Empty(BandejaDocumentosService.AplicarEleccion(l, new Dictionary<string, EleccionGuia>
            {
                ["T660-1"] = BandejaDocumentosService.EleccionDesdeSelector("")   // valor vacío o manipulado
            }));

            Assert.Equal(EstadoGuiaBandeja.SinProgramado, nueva.Estado);
            Assert.False(nueva.PorElegir);
            Assert.Contains(BandejaDocumentosService.QuedoSinRegistrar(l), x => x.Contains("no tiene despacho programado"));
        }

        [Fact]
        public void La_cpic_que_esperaba_el_camion_dice_por_que_no_se_registro()
        {
            var l = new LecturaBandeja();
            l.Cpics.Add(new CpicBandeja
            {
                Archivo = "c.pdf", Estado = EstadoAduanaBandeja.NoSeRegistra, EsperaSuPedido = true, Pedido = "4400000001",
                Detalle = BandejaDocumentosService.DetalleCpicEsperaCamion,
                Cpic = new WebSGV.Services.Despachos.Importacion.CpicLeido { Numero = "0003900" }
            });
            var quedo = Assert.Single(BandejaDocumentosService.QuedoSinRegistrar(l));
            Assert.StartsWith("CPIC 0003900: no se registró porque no se eligió el camión de las guías de su pedido 4400000001.", quedo);
        }

        [Fact]
        public void Un_despacho_invalido_da_error_y_la_guia_sigue_por_elegir()
        {
            var b = Guia("T660-2", 30, 40);
            var l = Lectura(b);
            var errores = BandejaDocumentosService.AplicarEleccion(l, E(("T660-2", 999)));
            Assert.Contains(errores, e => e.Contains("no corresponde"));
            Assert.True(b.PorElegir);
        }

        [Theory]
        [InlineData("", null, true)]
        [InlineData(null, null, true)]
        [InlineData("basura", null, true)]
        [InlineData("no", null, false)]
        [InlineData("2", 2, false)]
        public void El_valor_del_selector_se_lee_asi(string valor, int? idProgramado, bool porElegir)
        {
            var e = BandejaDocumentosService.EleccionDesdeSelector(valor);
            Assert.Equal(idProgramado, e.IdProgramado);
            Assert.Equal(porElegir, e.PorElegir);
            Assert.False(e.Nuevo);
        }

        // ── Lo que queda sin registrar ──────────────────────────────────────

        private static MciBandeja Mci(string numero, EstadoAduanaBandeja estado, string detalle = null) => new MciBandeja
        {
            Archivo = numero + ".pdf", Estado = estado, Detalle = detalle,
            Mci = new WebSGV.Services.Despachos.Importacion.MciLeido { Numero = numero, CartaPorte = "0003900" }
        };

        [Fact]
        public void Dice_lo_que_quedo_sin_registrar_con_su_motivo()
        {
            var sinElegir = Guia("T660-1", 30, 40);
            sinElegir.Candidatos.Add(1);
            var sinDespacho = Guia("T660-2", 30, 40);
            var l = new LecturaBandeja();
            var p = new PedidoBandeja { Pedido = new ImportacionPedido { NumeroPedido = "4400000001" } };
            p.Guias.AddRange(new[] { sinElegir, sinDespacho });
            l.Pedidos.Add(p);
            l.Cpics.Add(new CpicBandeja
            {
                Archivo = "c.pdf", Estado = EstadoAduanaBandeja.NoSeRegistra, Detalle = "Suba primero el ZIP.",
                Cpic = new WebSGV.Services.Despachos.Importacion.CpicLeido { Numero = "0003900" }
            });
            l.Mcis.Add(Mci("0001", EstadoAduanaBandeja.Elegir));
            l.Mcis.Add(Mci("0002", EstadoAduanaBandeja.TrasSuCpic));
            l.Mcis.Add(Mci("0003", EstadoAduanaBandeja.NoSeRegistra, "No se leyó la carta de porte."));
            l.NoReconocidos.Add("foto.pdf: no tiene guías ni factura del cliente.");

            var quedo = BandejaDocumentosService.QuedoSinRegistrar(l);

            Assert.Equal(7, quedo.Count);
            Assert.Contains(quedo, x => x.Contains("T660-1") && x.Contains("no se eligió"));
            Assert.Contains(quedo, x => x.Contains("T660-2") && x.Contains("no tiene despacho programado"));
            Assert.Contains("CPIC 0003900: Suba primero el ZIP.", quedo);
            Assert.Contains(quedo, x => x.StartsWith("MCI 0001") && x.Contains("falta indicar su despacho"));
            Assert.Contains(quedo, x => x.StartsWith("MCI 0002") && x.Contains("no se registró"));
            Assert.Contains("MCI 0003: No se leyó la carta de porte.", quedo);
            Assert.Contains("foto.pdf: no tiene guías ni factura del cliente.", quedo);
        }

        [Fact]
        public void Las_guias_de_un_pedido_con_el_mismo_motivo_van_en_una_linea()
        {
            var l = new LecturaBandeja();
            var p = new PedidoBandeja { Pedido = new ImportacionPedido { NumeroPedido = "4400000001" } };
            p.Guias.AddRange(new[] { Guia("T660-1", 30, 40), Guia("T660-2", 31, 41) });
            l.Pedidos.Add(p);

            var quedo = Assert.Single(BandejaDocumentosService.QuedoSinRegistrar(l));

            Assert.StartsWith("Guías T660-1, T660-2 (pedido 4400000001): no tienen despacho programado.", quedo);
        }

        [Fact]
        public void Lo_que_se_registra_o_ya_estaba_no_aparece_como_pendiente()
        {
            var a = Guia("T660-1", 10, 20);
            var ya = Guia("T660-2", 30, 40, estado: EstadoGuiaBandeja.YaRegistrada);
            var l = Lectura(a, ya);
            l.Mcis.Add(Mci("0001", EstadoAduanaBandeja.Registrar));
            l.Mcis.Add(Mci("0002", EstadoAduanaBandeja.YaRegistrado));
            l.Mcis.Add(Mci("0003", EstadoAduanaBandeja.OtraVersion));

            Assert.Empty(BandejaDocumentosService.QuedoSinRegistrar(l));
        }

        // ── PDF de la lectura en disco, no en la sesión (tarea 14 C) ──

        private static LecturaBandeja LecturaConPdfs()
        {
            var pedido = new PedidoBandeja { Archivo = "pedido.zip", FacturaPdf = new byte[] { 1, 1 } };
            pedido.PdfGuias["T660-1"] = new PdfGuia { Nombre = "guia.pdf", Pdf = new byte[] { 2, 2 } };
            var l = new LecturaBandeja();
            l.Pedidos.Add(pedido);
            l.Pedidos.Add(new PedidoBandeja { Archivo = "sin-factura.zip" });
            l.Cpics.Add(new CpicBandeja { Archivo = "cpic.pdf", Pdf = new byte[] { 3 } });
            l.Mcis.Add(new MciBandeja { Archivo = "mci.pdf", Pdf = new byte[] { 4 } });
            l.Gres.Add(new GreBandeja { Archivo = "gre.pdf", Pdf = new byte[] { 5 } });
            l.FacturasFlete.Add(new FacturaFleteBandeja { Archivo = "e001.pdf", Pdf = new byte[] { 6 } });
            return l;
        }

        private static Func<byte[], string> GuardarEn(string carpeta) => b =>
        {
            string ruta = System.IO.Path.Combine(carpeta, Guid.NewGuid().ToString("N") + ".pdf");
            System.IO.File.WriteAllBytes(ruta, b);
            return ruta;
        };

        [Fact]
        public void PasarPdfsADisco_deja_la_memoria_vacia_y_Pdf_relee_los_mismos_bytes()
        {
            string carpeta = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sgv-bandeja-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(carpeta);
            try
            {
                var l = LecturaConPdfs();
                l.PasarPdfsADisco(GuardarEn(carpeta));

                // Cada PDF quedó en su archivo (6) y en memoria solo la ruta.
                Assert.Equal(6, l.RutasTemporales().Count);
                Assert.All(l.RutasTemporales(), r => Assert.True(System.IO.File.Exists(r)));
                var campo = typeof(ConPdfTemporal).GetField("_pdf", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var conPdf = new ConPdfTemporal[] { l.Pedidos[0].FacturaPdfTemporal, l.Pedidos[0].PdfGuias["T660-1"], l.Cpics[0], l.Mcis[0], l.Gres[0], l.FacturasFlete[0] };
                Assert.All(conPdf, x => Assert.Null(campo.GetValue(x)));
                Assert.Null(l.Pedidos[1].RutaFacturaPdfTemp);

                Assert.Equal(new byte[] { 1, 1 }, l.Pedidos[0].FacturaPdf);
                Assert.Equal(new byte[] { 2, 2 }, l.Pedidos[0].PdfGuias["T660-1"].Pdf);
                Assert.Equal(new byte[] { 3 }, l.Cpics[0].Pdf);
                Assert.Equal(new byte[] { 4 }, l.Mcis[0].Pdf);
                Assert.Equal(new byte[] { 5 }, l.Gres[0].Pdf);
                Assert.Equal(new byte[] { 6 }, l.FacturasFlete[0].Pdf);
                Assert.Null(l.Pedidos[1].FacturaPdf);
                Assert.True(l.Pedidos[0].HayFacturaPdf);
                Assert.False(l.Pedidos[1].HayFacturaPdf);
                Assert.False(l.FaltanTemporales());
            }
            finally { System.IO.Directory.Delete(carpeta, true); }
        }

        [Fact]
        public void FaltanTemporales_detecta_un_pdf_borrado_y_su_Pdf_queda_null()
        {
            string carpeta = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sgv-bandeja-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(carpeta);
            try
            {
                var l = LecturaConPdfs();
                l.PasarPdfsADisco(GuardarEn(carpeta));
                System.IO.File.Delete(l.Mcis[0].RutaPdfTemp);

                Assert.True(l.FaltanTemporales());
                Assert.Null(l.Mcis[0].Pdf);
                Assert.False(l.Mcis[0].HayPdf);
                Assert.Equal(new byte[] { 3 }, l.Cpics[0].Pdf);
            }
            finally { System.IO.Directory.Delete(carpeta, true); }
        }

        [Fact]
        public void Registrar_con_un_temporal_borrado_no_registra_nada()
        {
            string carpeta = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sgv-bandeja-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(carpeta);
            try
            {
                var l = LecturaConPdfs();
                l.PasarPdfsADisco(GuardarEn(carpeta));
                System.IO.File.Delete(l.FacturasFlete[0].RutaPdfTemp);

                // Corta antes de tocar la BD: un solo error, nada registrado.
                var r = BandejaDocumentosService.Registrar(l, "prueba", carpeta, "20000000001");

                Assert.Equal(BandejaDocumentosService.MensajeLecturaVencida, Assert.Single(r.Errores));
                Assert.Equal(0, r.DespachosCompletados + r.DespachosCreados + r.CpicsRegistradas + r.ManifiestosAdjuntados
                                + r.FacturasCreadas + r.PdfAdjuntados + r.FacturasFleteRegistradas + r.GresRegistradas + r.PdfGuiasGuardados);
            }
            finally { System.IO.Directory.Delete(carpeta, true); }
        }

        [Fact]
        public void Sin_pasar_a_disco_el_Pdf_sigue_en_memoria()
        {
            var l = LecturaConPdfs();
            Assert.Empty(l.RutasTemporales());
            Assert.False(l.FaltanTemporales());
            Assert.Equal(new byte[] { 3 }, l.Cpics[0].Pdf);
            Assert.True(l.Pedidos[0].HayFacturaPdf);
        }

        /// <summary>ZIP en memoria con entradas de texto (nombre, contenido).</summary>
        private static byte[] ZipConTextos(params (string Nombre, string Texto)[] entradas)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
                    foreach (var e in entradas)
                        using (var s = zip.CreateEntry(e.Nombre).Open())
                        {
                            byte[] b = System.Text.Encoding.UTF8.GetBytes(e.Texto);
                            s.Write(b, 0, b.Length);
                        }
                return ms.ToArray();
            }
        }

        [Fact]
        public void En_un_zip_lo_que_no_es_un_pdf_de_verdad_va_a_no_reconocidos_con_su_motivo()
        {
            // M-067: un .pdf que es otra cosa (firma, no extensión) y un archivo que no es PDF.
            var zip = new ImportacionPedidoService.ArchivoSubido
            {
                Nombre = "pedido.zip",
                Contenido = ZipConTextos(("cpic.pdf", "MZ esto no es un PDF"), ("notas.txt", "hola"))
            };
            var presupuesto = new ImportacionPedidoService.PresupuestoLectura();

            var l = BandejaDocumentosService.LeerArchivos(new[] { zip }, null, presupuesto);

            Assert.Contains("pedido.zip: cpic.pdf: no es un PDF (está dañado o le cambiaron la extensión)", l.NoReconocidos);
            Assert.Contains("pedido.zip: notas.txt: no es un PDF (dentro de un ZIP solo se leen PDF)", l.NoReconocidos);
            Assert.Empty(l.Cpics);
            Assert.Empty(l.Pedidos);
            Assert.Equal(ImportacionPedidoService.PresupuestoLectura.PdfPorEnvio, presupuesto.PdfRestantes);   // no contó como PDF
        }
    }
}
