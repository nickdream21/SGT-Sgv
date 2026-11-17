using System;
using System.Collections.Generic;
using WebSGV.Models.Despachos;
using WebSGV.Services.Common;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Editar lote solo escribe lo que cambia (2026-11-17, bloque 4, tarea 12): antes guardar sin tocar nada pisaba el
    /// usuario y la fecha de modificación de todo el lote, y cambiar el conductor de un despacho reubicaba todos.
    /// También la lectura estricta de fechas y montos (antes un TryParse fallido guardaba 0001-01-01 o 0).
    /// </summary>
    public class CambiosEdicionLoteTests
    {
        private static readonly DateTime Fecha = new DateTime(2026, 11, 10);

        private static InstantaneaLote Lote()
        {
            var foto = new InstantaneaLote();
            foreach (var (id, conductor) in new[] { (101, 7), (102, 8) })
                foto.Despachos.Add(new InstantaneaDespacho
                {
                    IdDespacho = id, FechaDespacho = Fecha, NumeroPedido = "4400000001", IdPlanta = 3,
                    TipoOperacion = "CARGA", EsInternacional = true, IdConductor = conductor, IdViajeProgreso = 50,
                    IdFactura = 20, IdCPIC = 30
                });
            foto.Facturas.Add(new InstantaneaDocumento { Id = 20, Numero = "F001-100", Fecha = Fecha, Valor = 1500m, Moneda = "USD" });
            foto.Cpics.Add(new InstantaneaDocumento { Id = 30, Numero = "1234567", Fecha = Fecha, Valor = 800m });
            return foto;
        }

        /// <summary>Lo que manda el formulario sin tocar nada (documentos con número: GESTIONAR).</summary>
        private static GuardarCambiosLoteInput SinTocar() => new GuardarCambiosLoteInput
        {
            IdsDespachos = new List<int> { 101, 102 },
            FechaDespacho = Fecha, NumeroPedido = "4400000001", IdPlanta = 3, TipoOperacion = "CARGA", EsInternacional = true,
            CambiosConductores = new Dictionary<int, int> { [101] = 7, [102] = 8 },
            GestionarFactura = true, NumeroFactura = "F001-100", FechaEmisionFactura = Fecha, ValorTotalFactura = 1500m, MonedaFactura = "USD",
            GestionarCpic = true, NumeroCPIC = "1234567", FechaEmisionCPIC = Fecha, ValorFlete = 800m
        };

        [Fact]
        public void Sin_cambios_no_escribe_nada()
        {
            var c = EdicionDocumentosLote.CalcularCambios(Lote(), SinTocar());

            Assert.False(c.HayCambios);
            Assert.Empty(c.DespachosAActualizar);
            Assert.Empty(c.ConductoresCambiados);
            Assert.False(c.GestionarFactura);
            Assert.False(c.GestionarCpic);
        }

        [Fact]
        public void Pedido_vacio_y_null_son_iguales()
        {
            var foto = Lote();
            foto.Despachos.ForEach(d => d.NumeroPedido = "");
            var input = SinTocar();
            input.NumeroPedido = null;

            Assert.False(EdicionDocumentosLote.CalcularCambios(foto, input).HayCambios);
        }

        [Fact]
        public void Solo_un_conductor_cambiado_actualiza_y_reubica_solo_ese()
        {
            var input = SinTocar();
            input.CambiosConductores[102] = 9;

            var c = EdicionDocumentosLote.CalcularCambios(Lote(), input);

            Assert.Equal(new[] { 102 }, c.DespachosAActualizar);
            Assert.Equal(new Dictionary<int, int> { [102] = 9 }, c.ConductoresCambiados);
            Assert.False(c.GestionarFactura);
        }

        [Fact]
        public void Cambiar_la_fecha_actualiza_todos_sin_cambiar_conductores()
        {
            var input = SinTocar();
            input.FechaDespacho = Fecha.AddDays(1);

            var c = EdicionDocumentosLote.CalcularCambios(Lote(), input);

            Assert.Equal(new[] { 101, 102 }, c.DespachosAActualizar);
            Assert.Empty(c.ConductoresCambiados);
        }

        [Fact]
        public void Documento_igual_no_se_gestiona_y_valor_distinto_si()
        {
            var input = SinTocar();
            input.ValorFlete = 801m;

            var c = EdicionDocumentosLote.CalcularCambios(Lote(), input);

            Assert.False(c.GestionarFactura);
            Assert.True(c.GestionarCpic);
            Assert.Empty(c.DespachosAActualizar);
        }

        [Theory]
        [InlineData("numero")]
        [InlineData("fecha")]
        [InlineData("moneda")]
        [InlineData("despachoSinFactura")]
        public void Factura_se_gestiona_si_cambia_algo_o_falta_en_un_despacho(string que)
        {
            var foto = Lote();
            var input = SinTocar();
            if (que == "numero") input.NumeroFactura = "F001-101";
            if (que == "fecha") input.FechaEmisionFactura = Fecha.AddDays(-1);
            if (que == "moneda") input.MonedaFactura = "PEN";
            if (que == "despachoSinFactura") foto.Despachos[1].IdFactura = null;

            Assert.True(EdicionDocumentosLote.CalcularCambios(foto, input).GestionarFactura);
        }

        [Fact]
        public void Moneda_null_deja_la_que_tenia_y_factura_sin_moneda_es_PEN()
        {
            var input = SinTocar();
            input.MonedaFactura = null;
            Assert.False(EdicionDocumentosLote.CalcularCambios(Lote(), input).GestionarFactura);

            var foto = Lote();
            foto.Facturas[0].Moneda = null;
            input.MonedaFactura = "PEN";
            Assert.False(EdicionDocumentosLote.CalcularCambios(foto, input).GestionarFactura);
        }

        [Fact]
        public void Lote_sin_factura_con_numero_la_gestiona()
        {
            var foto = Lote();
            foto.Facturas.Clear();
            foto.Despachos.ForEach(d => d.IdFactura = null);

            Assert.True(EdicionDocumentosLote.CalcularCambios(foto, SinTocar()).GestionarFactura);
        }

        [Fact]
        public void Quitar_lista_los_documentos_del_lote_y_no_gestiona()
        {
            var input = SinTocar();
            input.QuitarCpic = true;
            input.GestionarCpic = false;

            var c = EdicionDocumentosLote.CalcularCambios(Lote(), input);

            Assert.Equal(new[] { 30 }, c.CpicsAQuitar);
            Assert.False(c.GestionarCpic);
            Assert.Empty(c.FacturasAQuitar);
            Assert.True(c.HayCambios);
        }

        [Fact]
        public void Quitar_cuando_el_lote_no_lo_tiene_no_es_un_cambio()
        {
            var foto = Lote();
            foto.Cpics.Clear();
            foto.Despachos.ForEach(d => d.IdCPIC = null);
            var input = SinTocar();
            input.GestionarCpic = false;
            input.QuitarCpic = true;

            Assert.False(EdicionDocumentosLote.CalcularCambios(foto, input).HayCambios);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Quitar_con_mas_de_un_documento_en_el_lote_avisa(bool factura)
        {
            // Otro usuario asignó una 2.ª factura/CPIC mientras el formulario estaba abierto.
            var foto = Lote();
            if (factura) foto.Facturas.Add(new InstantaneaDocumento { Id = 21, Numero = "F001-200" });
            else foto.Cpics.Add(new InstantaneaDocumento { Id = 31, Numero = "7654321" });
            var input = SinTocar();
            if (factura) { input.GestionarFactura = false; input.QuitarFactura = true; }
            else { input.GestionarCpic = false; input.QuitarCpic = true; }

            var ex = Assert.Throws<ErrorNegocioException>(() => EdicionDocumentosLote.CalcularCambios(foto, input));
            Assert.Contains(factura ? "más de una factura" : "más de una CPIC", ex.Message);
            Assert.Contains("Factura y CPIC por despacho", ex.Message);
        }

        // ── Lectura estricta de fechas y montos ─────────────────────────────

        [Fact]
        public void LeerFechaIso_lee_yyyy_MM_dd()
        {
            Assert.Equal(new DateTime(2026, 11, 17), DespachoValidaciones.LeerFechaIso(" 2026-11-17 ", "Fecha"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("17/11/2026")]
        [InlineData("2026-13-01")]
        [InlineData("mañana")]
        public void LeerFechaIso_texto_invalido_avisa_con_el_campo(string valor)
        {
            var ex = Assert.Throws<ErrorNegocioException>(() => DespachoValidaciones.LeerFechaIso(valor, "Fecha emisión de la CPIC"));
            Assert.Equal("La fecha de «Fecha emisión de la CPIC» no es válida.", ex.Message);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("12,50")]
        [InlineData("-5")]
        public void LeerMonto_texto_invalido_avisa_con_el_campo(string valor)
        {
            var ex = Assert.Throws<ErrorNegocioException>(() => MontoHelper.LeerMonto(valor, "Valor del flete"));
            Assert.Contains("«Valor del flete»", ex.Message);
        }
    }
}
