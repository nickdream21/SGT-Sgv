using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
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
        public void Una_guia_y_un_programado_libres_se_unen_por_descarte()
        {
            var a = Guia("T660-1", 10, 20);
            var b = Guia("T660-2", 30, 40);   // ni tracto ni carreta coinciden
            BandejaDocumentosService.Unir(new[] { a, b }, new[] { Prog(1, 10, 20), Prog(2, 77, 78) });

            Assert.Equal(1, a.IdProgramado);
            Assert.Equal(2, b.IdProgramado);
            Assert.StartsWith("Única guía", b.Regla);
        }

        [Fact]
        public void Con_dos_libres_no_adivina()
        {
            var a = Guia("T660-1", 30, 40);
            BandejaDocumentosService.Unir(new[] { a }, new[] { Prog(1, 77, 78), Prog(2, 88, 89) });

            Assert.Equal(EstadoGuiaBandeja.SinProgramado, a.Estado);
            Assert.Null(a.IdProgramado);
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
    }
}
