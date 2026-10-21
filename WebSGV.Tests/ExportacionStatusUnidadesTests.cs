using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Excel liviano de STATUS de unidades y la hora de la ubicación.</summary>
    public class ExportacionStatusUnidadesTests
    {
        private static ViajeResumen Viaje(string grupo, DateTime? posicion)
        {
            var t = new DataTable();
            t.Columns.Add("idSeguimiento", typeof(int));
            var fila = t.NewRow();
            fila["idSeguimiento"] = 7381;
            t.Rows.Add(fila);
            return new ViajeResumen
            {
                Pedido = "4400133879", Carreta = "TDX-994", Tracto1 = "TBM-898", Chofer1 = "JAIME ROMERO", Tracto2 = "CBV-854", Chofer2 = "ROBERTO MACALUPU",
                FhProgramacion = new DateTime(2026, 10, 21), Status = "UNIDAD EN PROCESO DE DESCARGA - JAVE",
                UltimaPosicion = posicion, Fase = new FaseCalculada { Grupo = grupo }, Fila = fila,
            };
        }

        [Fact]
        public void UbicacionHora_SoloMientrasElViajeSigueAbierto()
        {
            Assert.Equal("21/10 12:47", Viaje("ecuador", new DateTime(2026, 10, 21, 12, 47, 33)).UbicacionHora);
            Assert.Null(Viaje("cerrado", new DateTime(2026, 10, 21, 12, 47, 33)).UbicacionHora);
            Assert.Null(Viaje("ecuador", null).UbicacionHora);
        }

        [Fact]
        public void Excel_TraeLasColumnasDelCuadroDeLaAdministradora()
        {
            var v = Viaje("ecuador", new DateTime(2026, 10, 21, 12, 47, 0));
            var r = ExportacionStatusUnidades.Construir(new[] { v }, new[] { "Búsqueda: 4400133879" }, "prueba");

            Assert.Equal(new[]
            {
                "FECHA", "NRO DE PEDIDO", "CARRETA", "CONDUCTOR DE CARGA", "CONDUCTOR DE DESCARGA",
                "PLACA TRACTO PARA LA CARGA", "PLACA TRACTO PARA EL CRUCE EN FRONTERA", "STATUS", "UBICACIÓN AL",
            }, r.Columnas.Select(c => c.Titulo).ToArray());
            var fila = r.Filas[0];
            Assert.Equal(new object[]
            {
                "21/10/26", "4400133879", "TDX-994", "JAIME ROMERO", "ROBERTO MACALUPU", "TBM-898", "CBV-854",
                "UNIDAD EN PROCESO DE DESCARGA - JAVE", "21/10 12:47",
            }, r.Columnas.Select(c => c.Valor(fila)).ToArray());
        }
    }
}
