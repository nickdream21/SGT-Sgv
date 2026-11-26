using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Services.Despachos;
using Xunit;
using Datos = WebSGV.Services.Despachos.ArmadoLoteManual.DatosConfiguracionBase;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-26: "Registrar un lote a mano" sin BD ni controles (<see cref="ArmadoLoteManual"/>): los documentos que pide cada
    /// operación, la validación de la configuración base (cada mensaje y su orden), el armado del lote y el aviso de
    /// "Finalizar lote" (<see cref="RegistroDespachoReglas.TextoLoteFinalizado"/>). Los textos son los que mostraba
    /// RegistroDespacho.aspx.cs (ValidarConfiguracionBase, CrearNuevoLote, MostrarOcultarPanelesBase y btnFinalizarLote_Click).
    /// </summary>
    public class ArmadoLoteManualTests
    {
        private static readonly DateTime Hoy = new DateTime(2026, 11, 25);

        /// <summary>Una carga nacional bien llenada (con factura).</summary>
        private static Datos CargaNacional() => new Datos
        {
            FechaDespacho = "2026-11-25",
            IdCliente = "3",
            NombreCliente = "Cliente 3",
            PedidoVisible = true,
            NumeroPedido = "4400000001",
            TipoOperacion = "CARGA",
            EsInternacional = false,
            IdPlanta = "4",
            NombrePlanta = "Planta 4",
            FacturaVisible = true,
            NumeroFactura = " F222-1 ",
            FechaEmisionFactura = "2026-11-24",
            ValorTotalFactura = "1234,50",
            MonedaFactura = "USD",
        };

        /// <summary>Una carga internacional bien llenada (factura y CPIC).</summary>
        private static Datos CargaInternacional()
        {
            var d = CargaNacional();
            d.EsInternacional = true;
            d.PedidoVisible = false;
            d.CpicVisible = true;
            d.NumeroCpic = " 1234567 ";
            d.FechaEmisionCpic = "2026-11-23";
            d.ValorFlete = "800.00";
            return d;
        }

        private static List<string> Errores(string aviso)
        {
            Assert.StartsWith("Errores de validación: ", aviso);
            Assert.EndsWith(".", aviso);
            return aviso.Substring("Errores de validación: ".Length).TrimEnd('.').Split(new[] { ". " }, StringSplitOptions.None).ToList();
        }

        // ── Documentos por operación (MostrarOcultarPanelesBase) ─────────────────────────────

        [Theory]
        [InlineData("", false, false, false, false, "Seleccione el tipo de operación y ámbito para ver los documentos requeridos.")]
        [InlineData("CARGA", true, true, true, false, "Internacional CARGA: Requiere N° Factura + CPIC (se reutilizarán para todos los conductores)")]
        [InlineData("DESCARGA", true, false, true, true, "Internacional DESCARGA: CPIC opcional (se reutilizará para todos los conductores). Si aún no lo tiene, agréguelo después al lote en Lista de despachos.")]
        [InlineData("CARGA", false, true, false, false, "Nacional CARGA: Requiere N° Factura (se reutilizará para todos los conductores)")]
        [InlineData("DESCARGA", false, false, false, false, "Nacional DESCARGA: Solo se requieren guías específicas por conductor")]
        public void Documentos_SegunOperacionYAmbito(string tipo, bool internacional, bool factura, bool cpic, bool cpicOpcional, string ayuda)
        {
            var d = ArmadoLoteManual.Documentos(tipo, internacional);
            Assert.Equal(factura, d.Factura);
            Assert.Equal(cpic, d.Cpic);
            Assert.Equal(cpicOpcional, d.CpicOpcional);
            Assert.Equal(ayuda, d.Ayuda);
        }

        [Fact]
        public void Documentos_SinTipoInternacional_NiPideNiApagaLaCpic()
        {
            var d = ArmadoLoteManual.Documentos(null, true);
            Assert.False(d.Factura);
            Assert.False(d.Cpic);
            Assert.False(d.CpicOpcional);
            Assert.Equal("Seleccione el tipo de operación y ámbito para ver los documentos requeridos.", d.Ayuda);
        }

        // ── Validar (ValidarConfiguracionBase) ──────────────────────────────────────────────

        [Fact]
        public void Validar_BienLlenada_NoDaErrores()
        {
            Assert.Null(ArmadoLoteManual.Validar(CargaNacional(), Hoy, null));
            Assert.Null(ArmadoLoteManual.Validar(CargaInternacional(), Hoy, null));
        }

        [Fact]
        public void Validar_TodosLosErrores_EnElOrdenDeAntes()
        {
            var d = CargaInternacional();
            d.FechaDespacho = "25/11/2026";
            d.PedidoVisible = true;
            d.NumeroPedido = "123";
            d.FechaEmisionFactura = "2026-11-26";
            d.FechaEmisionCpic = "ayer";
            d.ErrorArchivoFactura = "el archivo pesa más de 10 MB";
            d.ErrorArchivoCpic = "la extensión no corresponde";
            d.NumeroFactura = "";
            d.NumeroCpic = "";

            Assert.Equal(new[]
            {
                "La fecha de programación no tiene un formato válido",
                "El número de pedido debe tener exactamente 10 dígitos",
                "La fecha de emisión de factura no puede ser futura",
                "La fecha de emisión de CPIC no tiene un formato válido",
                "Documento de factura: el archivo pesa más de 10 MB",
                "Documento de CPIC: la extensión no corresponde",
                "La factura repetida",
                "Número de factura requerido para carga internacional",
                "Número de CPIC requerido para carga internacional",
            }, Errores(ArmadoLoteManual.Validar(d, Hoy, "La factura repetida")));
        }

        [Fact]
        public void Validar_FechasDeEmision_SoloConSuPanelYConValor()
        {
            var d = CargaInternacional();
            d.FechaEmisionFactura = "x";
            d.FechaEmisionCpic = "2026-11-26";
            Assert.Equal(new[] { "La fecha de emisión de factura no tiene un formato válido", "La fecha de emisión de CPIC no puede ser futura" },
                Errores(ArmadoLoteManual.Validar(d, Hoy, null)));

            // Paneles ocultos: ni las fechas ni los adjuntos cuentan. Vacías: tampoco (las exige su validador).
            d.FacturaVisible = d.CpicVisible = false;
            d.ErrorArchivoFactura = d.ErrorArchivoCpic = "malo";
            d.NumeroFactura = "F1";
            d.NumeroCpic = "C1";
            Assert.Null(ArmadoLoteManual.Validar(d, Hoy, null));
            d.FacturaVisible = d.CpicVisible = true;
            d.FechaEmisionFactura = d.FechaEmisionCpic = "  ";
            d.ErrorArchivoFactura = d.ErrorArchivoCpic = "";
            Assert.Null(ArmadoLoteManual.Validar(d, Hoy, null));
        }

        [Fact]
        public void Validar_PedidoOculto_NoSeRevisa()
        {
            var d = CargaNacional();
            d.NumeroPedido = "12";
            Assert.Equal(new[] { "El número de pedido debe tener exactamente 10 dígitos" }, Errores(ArmadoLoteManual.Validar(d, Hoy, null)));
            d.PedidoVisible = false;
            Assert.Null(ArmadoLoteManual.Validar(d, Hoy, null));
            d.PedidoVisible = true;
            d.NumeroPedido = "";
            Assert.Null(ArmadoLoteManual.Validar(d, Hoy, null));
        }

        [Fact]
        public void Validar_CargaNacional_PideLaFactura()
        {
            var d = CargaNacional();
            d.NumeroFactura = "";
            Assert.Equal(new[] { "Número de factura requerido para carga nacional" }, Errores(ArmadoLoteManual.Validar(d, Hoy, null)));
            // Un espacio cuenta como escrito (IsNullOrEmpty, como antes).
            d.NumeroFactura = " ";
            Assert.Null(ArmadoLoteManual.Validar(d, Hoy, null));
        }

        [Fact]
        public void Validar_DescargaInternacional_CpicOpcional_PeroConNumeroPideFechaYFlete()
        {
            var d = CargaInternacional();
            d.TipoOperacion = "DESCARGA";
            d.FacturaVisible = false;
            d.NumeroFactura = "";
            d.NumeroCpic = "";
            d.FechaEmisionCpic = "";
            d.ValorFlete = "";
            Assert.Null(ArmadoLoteManual.Validar(d, Hoy, null));

            d.NumeroCpic = "1234567";
            Assert.Equal(new[] { "Con N° de CPIC, indique su fecha de emisión", "Con N° de CPIC, indique el valor del flete" },
                Errores(ArmadoLoteManual.Validar(d, Hoy, null)));
            // Un número en blanco no cuenta (IsNullOrWhiteSpace).
            d.NumeroCpic = "  ";
            Assert.Null(ArmadoLoteManual.Validar(d, Hoy, null));
        }

        [Fact]
        public void Validar_DescargaNacional_NoPideDocumentos()
        {
            var d = CargaNacional();
            d.TipoOperacion = "DESCARGA";
            d.PedidoVisible = d.FacturaVisible = false;
            d.NumeroFactura = "";
            Assert.Null(ArmadoLoteManual.Validar(d, Hoy, null));
        }

        // ── Duplicados (ValidarDocumentosDuplicados) ────────────────────────────────────────

        [Fact]
        public void NumerosAConsultar_SoloLosDeLosPanelesVisiblesConNumero()
        {
            var c = ArmadoLoteManual.NumerosAConsultar(CargaInternacional());
            Assert.Equal("F222-1", c.NumeroFactura);
            Assert.Equal("1234567", c.NumeroCpic);
            Assert.Equal(3, c.IdCliente);

            var d = CargaInternacional();
            d.CpicVisible = false;
            d.NumeroFactura = "";
            d.IdCliente = "0";
            c = ArmadoLoteManual.NumerosAConsultar(d);
            Assert.Null(c.NumeroFactura);
            Assert.Null(c.NumeroCpic);
            Assert.Null(c.IdCliente);
            d.IdCliente = "x";
            Assert.Null(ArmadoLoteManual.NumerosAConsultar(d).IdCliente);
        }

        [Fact]
        public void TextoDuplicados_LaFacturaPorCliente_YLaCpic()
        {
            var c = ArmadoLoteManual.NumerosAConsultar(CargaInternacional());
            Assert.Null(ArmadoLoteManual.TextoDuplicados(c, false, false));
            Assert.Equal("El número de factura 'F222-1' ya está registrado en el sistema para este cliente. " +
                         "El número de CPIC '1234567' ya está registrado en el sistema", ArmadoLoteManual.TextoDuplicados(c, true, true));
            Assert.Equal("El número de CPIC '1234567' ya está registrado en el sistema", ArmadoLoteManual.TextoDuplicados(c, false, true));
            // Sin número consultado no se avisa aunque la BD diga que existe.
            c.NumeroFactura = null;
            Assert.Null(ArmadoLoteManual.TextoDuplicados(c, true, false));
        }

        // ── Armar (CrearNuevoLote) ──────────────────────────────────────────────────────────

        [Fact]
        public void Armar_CargaInternacional_ConFacturaYCpic()
        {
            var lote = ArmadoLoteManual.Armar(CargaInternacional(), "Usuario 1", out string error);
            Assert.Null(error);
            Assert.Equal("2026-11-25", lote.FechaProgramacion);
            Assert.Equal(3, lote.IdCliente);
            Assert.Equal("Cliente 3", lote.NombreCliente);
            Assert.Equal("", lote.NumeroPedido);   // el panel del pedido no se ve en la internacional
            Assert.Equal("CARGA", lote.TipoOperacion);
            Assert.True(lote.EsInternacional);
            Assert.Equal(4, lote.IdPlanta);
            Assert.Equal("Planta 4", lote.PlantaOperacion);
            Assert.Equal("Usuario 1", lote.UsuarioCreacion);
            Assert.Equal("F222-1", lote.Documentacion.NumeroFactura);
            Assert.Equal(new DateTime(2026, 11, 24), lote.Documentacion.FechaEmisionFactura);
            Assert.Equal(1234.50m, lote.Documentacion.ValorTotalFactura);
            Assert.Equal("USD", lote.Documentacion.MonedaFactura);
            Assert.Equal("1234567", lote.Documentacion.NumeroCPIC);
            Assert.Equal(new DateTime(2026, 11, 23), lote.Documentacion.FechaEmisionCPIC);
            Assert.Equal(800m, lote.Documentacion.ValorFlete);
            // Los adjuntos los guarda el control después de armar.
            Assert.Null(lote.Documentacion.FacturaArchivoRutaTemp);
            Assert.Null(lote.Documentacion.CPICArchivoRutaTemp);
        }

        [Fact]
        public void Armar_CargaNacional_ConPedido_YMonedaPorDefecto()
        {
            var d = CargaNacional();
            d.MonedaFactura = "EUR";
            var lote = ArmadoLoteManual.Armar(d, "U", out string error);
            Assert.Null(error);
            Assert.Equal("4400000001", lote.NumeroPedido);
            Assert.Equal("PEN", lote.Documentacion.MonedaFactura);
            Assert.Null(lote.Documentacion.NumeroCPIC);

            // Sin número de factura (o con el panel oculto) el lote va sin factura.
            d.NumeroFactura = "";
            Assert.Null(ArmadoLoteManual.Armar(d, "U", out _).Documentacion.NumeroFactura);
            d.NumeroFactura = "F1";
            d.FacturaVisible = false;
            Assert.Null(ArmadoLoteManual.Armar(d, "U", out _).Documentacion.NumeroFactura);
        }

        [Theory]
        [InlineData("FechaDespacho", "2026/11/25", "La fecha de programación no tiene un formato válido.")]
        [InlineData("IdPlanta", "", "Debe seleccionar una planta de operación válida.")]
        [InlineData("IdPlanta", "0", "Debe seleccionar una planta de operación válida.")]
        [InlineData("ValorTotalFactura", "0", "El valor total de la factura debe ser mayor a 0.")]
        [InlineData("ValorTotalFactura", "", "El valor total de la factura debe ser mayor a 0.")]
        [InlineData("FechaEmisionFactura", "", "La fecha de emisión de factura no tiene un formato válido.")]
        [InlineData("ValorFlete", "0,00", "El valor del flete debe ser mayor a 0.")]
        [InlineData("FechaEmisionCpic", "x", "La fecha de emisión de CPIC no tiene un formato válido.")]
        public void Armar_CadaError_ConSuMensaje(string campo, string valor, string mensaje)
        {
            var d = CargaInternacional();
            typeof(Datos).GetProperty(campo).SetValue(d, valor);
            Assert.Null(ArmadoLoteManual.Armar(d, "U", out string error));
            Assert.Equal(mensaje, error);
        }

        [Fact]
        public void Armar_MontoIlegible_DaElAvisoDeMontoHelper()
        {
            var d = CargaInternacional();
            d.ValorTotalFactura = "1.234,50";
            Assert.Null(ArmadoLoteManual.Armar(d, "U", out string error));
            Assert.StartsWith("El monto de «Valor total de la factura» no es válido.", error);

            d = CargaInternacional();
            d.ValorFlete = "-5";
            Assert.Null(ArmadoLoteManual.Armar(d, "U", out error));
            Assert.Equal("El monto de «Valor del flete» no puede ser negativo.", error);
        }

        [Fact]
        public void Armar_LaFacturaSeRevisaAntesQueLaCpic()
        {
            var d = CargaInternacional();
            d.ValorTotalFactura = "0";
            d.ValorFlete = "0";
            ArmadoLoteManual.Armar(d, "U", out string error);
            Assert.Equal("El valor total de la factura debe ser mayor a 0.", error);
        }

        [Fact]
        public void Armar_ClienteQueNoEsNumero_Lanza_ComoAntes()
        {
            // "Iniciar lote" lo atrapa y muestra "Error al iniciar el lote. Intente nuevamente." (Convert.ToInt32 de antes).
            var d = CargaNacional();
            d.IdCliente = "abc";
            Assert.Throws<FormatException>(() => ArmadoLoteManual.Armar(d, "U", out _));
        }

        [Fact]
        public void LlevaFacturaYCpic_PanelVisibleYConNumero()
        {
            var d = CargaInternacional();
            Assert.True(ArmadoLoteManual.LlevaFactura(d));
            Assert.True(ArmadoLoteManual.LlevaCpic(d));
            d.NumeroFactura = "";
            d.CpicVisible = false;
            Assert.False(ArmadoLoteManual.LlevaFactura(d));
            Assert.False(ArmadoLoteManual.LlevaCpic(d));
        }

        // ── Aviso de "Finalizar lote" ───────────────────────────────────────────────────────

        [Theory]
        [InlineData(3, 0, "Lote finalizado: se crearon 3 despachos.")]
        [InlineData(0, 2, "Lote finalizado: se completaron 2 despacho(s) programado(s) con sus documentos.")]
        [InlineData(1, 2, "Lote finalizado: se completaron 2 despacho(s) programado(s) con sus documentos y se crearon 1 nuevo(s).")]
        public void TextoLoteFinalizado_LosTresTextosDeAntes(int creados, int completados, string texto)
        {
            Assert.Equal(texto, RegistroDespachoReglas.TextoLoteFinalizado(creados, completados));
        }
    }
}
