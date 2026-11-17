using System.Collections.Generic;
using System.Data;
using WebSGV.Services.Common;
using WebSGV.Services.Despachos.Importacion;
using WebSGV.Services.Vehiculos;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Placas únicas (bloque 5, B5-1): forma canónica y clave de comparación, validación del alta/edición de
    /// tractos y semirremolques, y asociación de la importación cuando una placa tiene varios vehículos.
    /// </summary>
    public class PlacaVehiculoTests
    {
        // ── Canonica ─────────────────────────────────────────────────────────

        [Theory]
        [InlineData("ABC-123", "ABC-123")]
        [InlineData("ABC123", "ABC-123")]
        [InlineData("abc123", "ABC-123")]
        [InlineData("  t4i-868 ", "T4I-868")]
        [InlineData("ABC 123", "ABC-123")]
        [InlineData("AB-C123", "ABC-123")]      // guion en otra posición
        [InlineData("A-B-C-1-2-3", "ABC-123")]
        [InlineData("ABC-1234", "ABC-1234")]     // 3 letras + 4 dígitos
        [InlineData("abc 1234", "ABC-1234")]
        [InlineData("ABC1234", "ABC-1234")]
        [InlineData("AB1-2345", "AB1-2345")]     // otros 7 caracteres: no se reacomodan
        [InlineData("1234 ABC", "1234ABC")]
        [InlineData("AB.123C", "AB.123C")]       // otros signos: se deja para que la validación lo rechace
        public void Canonica_lleva_seis_caracteres_a_AAA_999(string entrada, string esperado)
        {
            Assert.Equal(esperado, PlacaVehiculo.Canonica(entrada));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Canonica_y_Clave_de_vacio_son_null(string entrada)
        {
            Assert.Null(PlacaVehiculo.Canonica(entrada));
            Assert.Null(PlacaVehiculo.Clave(entrada));
        }

        // ── Clave ────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("ABC-123", "ABC123")]
        [InlineData("abc123", "ABC123")]
        [InlineData("T4I-868 ", "T4I868")]
        [InlineData(" a b-c 1 2 3 ", "ABC123")]
        [InlineData("ABC-1234", "ABC1234")]
        public void Clave_quita_guion_y_espacios_en_mayusculas(string entrada, string esperado)
        {
            Assert.Equal(esperado, PlacaVehiculo.Clave(entrada));
        }

        [Fact]
        public void Distintas_escrituras_de_la_misma_placa_tienen_la_misma_clave_y_canonica()
        {
            var escrituras = new[] { "ZZ9-999", "ZZ9999", "zz9 999", "ZZ9-999 " };
            foreach (var e in escrituras)
            {
                Assert.Equal("ZZ9999", PlacaVehiculo.Clave(e));
                Assert.Equal("ZZ9-999", PlacaVehiculo.Canonica(e));
            }
        }

        [Fact]
        public void NormalizarPlaca_de_la_importacion_es_la_clave()
        {
            foreach (var e in new[] { "C6K-799", "c6k799", " C6K 799 ", null, "" })
                Assert.Equal(PlacaVehiculo.Clave(e), LectorDocumentosPedido.NormalizarPlaca(e));
        }

        // ── Validar (alta y edición) ─────────────────────────────────────────

        [Fact]
        public void Validar_normaliza_placa_marca_y_modelo()
        {
            var d = VehiculoService.Validar("abc123", " volvo ", "fh 460");
            Assert.Equal("ABC-123", d.Placa);
            Assert.Equal("VOLVO", d.Marca);
            Assert.Equal("FH 460", d.Modelo);
        }

        [Theory]
        [InlineData("", "VOLVO", "FH")]
        [InlineData("ABC123", "", "FH")]
        [InlineData("ABC123", "VOLVO", " ")]
        [InlineData("ABC12", "VOLVO", "FH")]          // 5 caracteres
        [InlineData("------", "VOLVO", "FH")]         // sin letras ni números
        [InlineData("AB-12-3", "VOLVO", "FH")]        // solo 5 alfanuméricos
        [InlineData("ABC-12345678", "VOLVO", "FH")]   // más de 10
        [InlineData("AB.123C", "VOLVO", "FH")]        // signo no permitido
        [InlineData("ABC123", "V", "FH")]             // marca de 1 carácter
        [InlineData("ABC123", "VOLVO", "M<script>")]  // modelo con signos
        public void Validar_rechaza_datos_invalidos(string placa, string marca, string modelo)
        {
            Assert.Throws<ErrorNegocioException>(() => VehiculoService.Validar(placa, marca, modelo));
        }

        [Fact]
        public void Validar_marca_y_modelo_hasta_30_caracteres()
        {
            string treinta = new string('A', 30);
            Assert.Equal(treinta, VehiculoService.Validar("ABC123", treinta, treinta).Marca);

            string treintaYUno = new string('A', 31);
            var ex = Assert.Throws<ErrorNegocioException>(() => VehiculoService.Validar("ABC123", treintaYUno, "FH"));
            Assert.Contains("30", ex.Message);
            Assert.Throws<ErrorNegocioException>(() => VehiculoService.Validar("ABC123", "VOLVO", treintaYUno));
        }

        [Fact]
        public void Edicion_acepta_marca_y_modelo_antiguos_si_no_cambian()
        {
            var guardado = new VehiculoService.DatosVehiculo { Placa = "ABC-123", Marca = "VOLVO", Modelo = "1" };

            var d = VehiculoService.Validar("ABC124", "VOLVO", "1", guardado);
            Assert.Equal("ABC-124", d.Placa);
            Assert.Equal("1", d.Modelo);

            // Modelo NULL sin tocar (el cuadro llega vacío): se conserva NULL
            var sinModelo = new VehiculoService.DatosVehiculo { Placa = "ABC-123", Marca = "VOLVO", Modelo = null };
            Assert.Null(VehiculoService.Validar("ABC-123", "volvo", "", sinModelo).Modelo);

            // Cambiarlo por otro inválido sí falla, y en el alta sigue siendo obligatorio 2-30
            Assert.Throws<ErrorNegocioException>(() => VehiculoService.Validar("ABC-123", "VOLVO", "X", guardado));
            Assert.Throws<ErrorNegocioException>(() => VehiculoService.Validar("ABC-123", "VOLVO", "1"));
            Assert.Throws<ErrorNegocioException>(() => VehiculoService.Validar("ABC-123", "VOLVO", ""));
        }

        // ── Importación: varias unidades con la misma placa ──────────────────

        private static DataTable Tractos(params (int id, string placa, bool activo)[] filas)
        {
            var dt = new DataTable();
            dt.Columns.Add("idTracto", typeof(int));
            dt.Columns.Add("placaTracto", typeof(string));
            dt.Columns.Add("activo", typeof(bool));
            foreach (var f in filas) dt.Rows.Add(f.id, f.placa, f.activo);
            return dt;
        }

        [Fact]
        public void Importacion_usa_el_unico_activo_cuando_la_placa_se_repite()
        {
            var avisos = new List<string>();
            var indice = ImportacionPedidoService.IndicePorPlaca(
                Tractos((1, "ABC-123", false), (2, "ABC123", true), (3, "XYZ-999", true)),
                "placaTracto", "tractos", "Registro de tractos", avisos);

            Assert.Equal(2, (int)indice["ABC123"]["idTracto"]);
            Assert.Equal(3, (int)indice["XYZ999"]["idTracto"]);
            Assert.Empty(avisos);
        }

        [Fact]
        public void Importacion_no_elige_en_silencio_si_hay_varios_activos_y_avisa_una_vez()
        {
            var avisos = new List<string>();
            var datos = Tractos((1, "ABC-123", true), (2, "ABC123 ", true));

            var indice = ImportacionPedidoService.IndicePorPlaca(datos, "placaTracto", "tractos", "Registro de tractos", avisos);
            ImportacionPedidoService.IndicePorPlaca(datos, "placaTracto", "tractos", "Registro de tractos", avisos);   // se vuelve a asociar

            Assert.False(indice.ContainsKey("ABC123"));
            var aviso = Assert.Single(avisos);
            Assert.Contains("ABC123", aviso);
            Assert.Contains("2 tractos", aviso);
            Assert.Contains("Registro de tractos", aviso);
        }

        [Fact]
        public void Importacion_ignora_placas_solo_inactivas_y_vacias()
        {
            var avisos = new List<string>();
            var dt = Tractos((1, "ABC-123", false), (2, "ABC123", false));
            dt.Rows.Add(4, null, true);

            var indice = ImportacionPedidoService.IndicePorPlaca(dt, "placaTracto", "tractos", "Registro de tractos", avisos);

            Assert.Empty(indice);
            Assert.Empty(avisos);
        }
    }
}
