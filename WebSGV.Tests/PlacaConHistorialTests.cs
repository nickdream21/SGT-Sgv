using System.Collections.Generic;
using WebSGV.Services.Vehiculos;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Regla B5-4 (decisión D2): la placa de un vehículo con despachos u otro historial no se puede cambiar;
    /// solo su formato (misma <see cref="PlacaVehiculo.Clave"/>). Lógica pura de <see cref="VehiculoService"/>.
    /// </summary>
    public class PlacaConHistorialTests
    {
        private static List<VehiculoService.ReferenciaContada> Historial(int despachos, int seguimiento = 0, int destinos = 0) =>
            new List<VehiculoService.ReferenciaContada>
            {
                new VehiculoService.ReferenciaContada { Descripcion = "despacho(s)", Cantidad = despachos, EsDespacho = true },
                new VehiculoService.ReferenciaContada { Descripcion = "viaje(s) del seguimiento de exportación", Cantidad = seguimiento },
                new VehiculoService.ReferenciaContada { Descripcion = "destino(s) de descarga", Cantidad = destinos }
            };

        [Fact]
        public void Con_despachos_cambiar_la_placa_se_rechaza_con_el_numero_de_despachos()
        {
            string motivo = VehiculoService.MotivoPlacaBloqueada("ABC-123", "ABD-123", "tracto", Historial(3, seguimiento: 5));

            Assert.Equal("La placa de ABC-123 no se puede cambiar: tiene 3 despacho(s) registrados. " +
                         "Si la placa está mal, registre el vehículo correcto y desactive este.", motivo);
        }

        [Fact]
        public void Sin_despachos_pero_con_otro_historial_se_rechaza_y_lista_lo_que_hay()
        {
            string motivo = VehiculoService.MotivoPlacaBloqueada("ABC-123", "ABD-123", "semirremolque",
                Historial(0, seguimiento: 2, destinos: 1));

            Assert.StartsWith("La placa de ABC-123 no se puede cambiar: el semirremolque tiene historial registrado", motivo);
            Assert.Contains("2 viaje(s) del seguimiento de exportación, 1 destino(s) de descarga", motivo);
            Assert.DoesNotContain("despacho(s)", motivo);
            Assert.EndsWith("registre el vehículo correcto y desactive este.", motivo);
        }

        [Fact]
        public void Sin_historial_se_permite_cambiar_la_placa()
        {
            Assert.Null(VehiculoService.MotivoPlacaBloqueada("ABC-123", "ABD-123", "tracto", Historial(0)));
            Assert.Null(VehiculoService.MotivoPlacaBloqueada("ABC-123", "ABD-123", "tracto", null));
        }

        [Theory]
        [InlineData("T4I-868 ", "T4I-868")]
        [InlineData("ABC123", "ABC-123")]
        [InlineData("abc 123", "ABC-123")]
        public void Solo_cambiar_el_formato_se_permite_aunque_tenga_despachos(string guardada, string nueva)
        {
            Assert.True(VehiculoService.MismaPlaca(guardada, nueva));
            Assert.Null(VehiculoService.MotivoPlacaBloqueada(guardada, nueva, "tracto", Historial(10, seguimiento: 4)));
        }

        [Fact]
        public void El_mensaje_muestra_la_placa_guardada_sin_espacios_sobrantes()
        {
            string motivo = VehiculoService.MotivoPlacaBloqueada("T4I-868 ", "T4I-869", "tracto", Historial(1));
            Assert.StartsWith("La placa de T4I-868 no se puede cambiar: tiene 1 despacho(s) registrados.", motivo);
        }

        [Theory]
        [InlineData(TipoVehiculo.Tracto, "v.idTracto", "tracto2")]
        [InlineData(TipoVehiculo.Carreta, "v.idCarreta", "carreta")]
        public void La_consulta_de_la_grilla_trae_conHistorial(TipoVehiculo tipo, string columnaId, string columnaTexto)
        {
            string sql = VehiculoService.ConsultaGrilla(tipo);
            Assert.Contains("AS conHistorial", sql);
            Assert.Contains(columnaId, sql);
            Assert.Contains("FROM Despachos", sql);
            Assert.Contains("FROM DestinoDescarga", sql);
            Assert.Contains(columnaTexto, sql);
        }
    }
}
