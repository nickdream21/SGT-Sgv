using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Services.Exportacion;
using WebSGV.Services.Reportes;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Fase, avisos y avance del tablero de seguimiento, y la exportación al STATUS GENERAL.</summary>
    public class SeguimientoViajesTests
    {
        private static Dictionary<string, DateTime?> Horas(params (string Columna, string Hora)[] valores)
        {
            var h = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in valores) h[v.Columna] = DateTime.Parse(v.Hora);
            return h;
        }

        [Theory]
        [InlineData(null, "Programado", "carga")]
        [InlineData("fhSalidaBase1", "Rumbo a Trujillo", "carga")]
        [InlineData("fhIngresoPlanta", "En planta", "carga")]
        [InlineData("fhSalidaPlanta", "Retorno a base", "carga")]
        [InlineData("fhLlegadaBase2", "En base, cargado", "base")]
        [InlineData("fhSalidaBase2", "Rumbo a Ecuador", "ecuador")]
        [InlineData("fhIngresoBodegaNacional", "En frontera", "ecuador")]
        [InlineData("fhCruceEcuador", "En Ecuador", "ecuador")]
        [InlineData("fhLlegadaPlantaEcuador", "Descargando", "ecuador")]
        [InlineData("fhSalida", "Retorno de Ecuador", "retorno")]
        [InlineData("fhLlegadaBaseFinal", "Cerrado", "cerrado")]
        public void Fase_SegunElUltimoTramoConHora(string columna, string fase, string grupo)
        {
            var h = columna == null ? Horas() : Horas((columna, "2026-10-02 10:00"));
            var f = FaseViaje.Calcular(h, "EN_CURSO");
            Assert.Equal(fase, f.Nombre);
            Assert.Equal(grupo, f.Grupo);
        }

        [Fact]
        public void Fase_FinalizadoOAnulado_VanACerrados()
        {
            Assert.Equal("Cerrado", FaseViaje.Calcular(Horas(("fhSalidaPlanta", "2026-10-02 10:00")), "FINALIZADO").Nombre);
            Assert.Equal("Anulado", FaseViaje.Calcular(Horas(), "CANCELADO").Nombre);
            Assert.Equal("cerrado", FaseViaje.Calcular(Horas(), "cancelado").Grupo);
        }

        [Fact]
        public void Avisos_CasoBsd716_PasoDirectoColaDelComplexYFaltaDescarga()
        {
            // Horas reales de BSD-716 (29-30/09/2026).
            var h = Horas(("fhSalidaPlanta", "2026-09-28 15:46:46"), ("fhInicioCarga", "2026-09-28 12:00"), ("fhTerminoCarga", "2026-09-28 15:00"),
                ("fhLlegadaBodegaNacional", "2026-09-29 17:48:41"), ("fhIngresoBodegaNacional", "2026-09-29 20:11:25"),
                ("fhLlegadaTCI", "2026-09-29 22:23:50"), ("fhSalidaTCI", "2026-09-29 22:26:47"),
                ("fhSalida", "2026-09-30 17:31:10"));
            var avisos = FaseViaje.Avisos(h, "AMO-905", "BSD-716", pasoDirectoTci: true).Select(a => a.Tipo + ":" + a.Texto).ToList();

            Assert.Equal(new[]
            {
                "Info:Pasó directo por TCI",
                "Alerta:2 h 22 min en cola del Complex",
                "Manual:Falta inicio y término de descarga",
            }, avisos);
        }

        [Fact]
        public void Avisos_SinTractoDeDescargaDespuesDeCargar()
        {
            var avisos = FaseViaje.Avisos(Horas(("fhLlegadaBase2", "2026-10-07 23:51:10")), "BPT-788", null, false);
            Assert.Contains(avisos, a => a.Tipo == TipoAviso.Alerta && a.Texto == "Descarga sin tracto asignado");
        }

        [Fact]
        public void Avisos_SigueEnLaColaDeTrujillo_SeMarcaConLaHoraActual()
        {
            // CBV-761 del 21/10/2026: en la cola desde las 00:51, todavía sin entrar a las 11:05.
            var h = Horas(("fhSalidaBase1", "2026-10-20 13:29:39"), ("fhLlegadaTrujillo", "2026-10-21 00:51:57"));
            var avisos = FaseViaje.Avisos(h, "CBV-761", null, false, ahora: DateTime.Parse("2026-10-21 11:05"));
            Assert.Contains(avisos, a => a.Tipo == TipoAviso.Alerta && a.Texto == "10 h 13 min en cola de Trujillo (sigue esperando)");
            Assert.Equal("En Trujillo", FaseViaje.Calcular(h, "EN_CURSO").Nombre);

            // Sin la hora actual (o con un hito posterior) no se avisa la espera en curso.
            Assert.DoesNotContain(FaseViaje.Avisos(h, "CBV-761", null, false), a => a.Texto.Contains("cola"));
            h["fhSalidaPlanta"] = DateTime.Parse("2026-10-21 14:00");
            Assert.DoesNotContain(FaseViaje.Avisos(h, "CBV-761", null, false, ahora: DateTime.Parse("2026-10-21 15:00")), a => a.Texto.Contains("cola"));
        }

        [Fact]
        public void ActualizarGps_PrimeroLasCargasDeHoyLuegoLasDescargasYAlFinalLosAtrasados()
        {
            ViajeResumen V(int id, string fase, string prog, string consulta) => new ViajeResumen
            {
                Id = id,
                Fase = new FaseCalculada { Grupo = fase },
                FhProgramacion = DateTime.Parse(prog),
                UltimaConsultaGps = consulta == null ? (DateTime?)null : DateTime.Parse(consulta),
            };
            var viajes = new[]
            {
                V(1, "retorno", "2026-10-19", null),
                V(2, "carga", "2026-10-03", null),          // atrasado: al final aunque nunca se consultó
                V(3, "ecuador", "2026-10-17", "2026-10-21 08:00"),
                V(4, "carga", "2026-10-20", "2026-10-21 07:00"),
                V(5, "carga", "2026-10-21", "2026-10-21 11:00"),
                V(6, "base", "2026-10-20", "2026-10-21 07:30"),
            };
            var orden = SeguimientoViajesService.OrdenarParaGps(viajes, DateTime.Parse("2026-10-21")).Select(v => v.Id);
            Assert.Equal(new[] { 5, 4, 6, 3, 1, 2 }, orden);
        }

        [Fact]
        public void Avisos_EsperasNormales_NoSeMarcan()
        {
            // BFI-922 en el Complex: 45 min de cola, 11 min en TCI.
            var h = Horas(("fhLlegadaBodegaNacional", "2026-10-02 16:56:18"), ("fhIngresoBodegaNacional", "2026-10-02 17:41:53"),
                ("fhLlegadaTCI", "2026-10-02 19:38:13"), ("fhSalidaTCI", "2026-10-02 19:50:13"));
            Assert.Empty(FaseViaje.Avisos(h, "ANU-794", "BFI-922", false));
        }

        [Fact]
        public void Avance_YUltimoHito()
        {
            var h = Horas(("fhSalidaBase1", "2026-09-29 13:15:03"), ("fhLlegadaTrujillo", "2026-09-30 00:17:12"),
                ("fhSalidaBase2", "2026-10-02 07:36:00"), ("fhLlegadaAlmacen", "2026-10-03 09:58:24"));
            Assert.Equal(2, FaseViaje.Avance(h, TramoViaje.Carga));
            Assert.Equal(1, FaseViaje.Avance(h, TramoViaje.Descarga)); // el almacén va junto con la llegada a planta
            Assert.Equal("Salida de base Sullana", FaseViaje.UltimoHito(h).Item1.Nombre);
            Assert.Equal(new DateTime(2026, 10, 2, 7, 36, 0), FaseViaje.UltimoHito(h).Item2);
        }

        [Theory]
        [InlineData(45, "45 min")]
        [InlineData(143, "2 h 23 min")]
        [InlineData(120, "2 h")]
        [InlineData(27 * 60, "1 d 3 h")]
        public void Duracion_Legible(int minutos, string esperado) =>
            Assert.Equal(esperado, FaseViaje.Duracion(TimeSpan.FromMinutes(minutos)));

        [Fact]
        public void Hitos_SonLos17PasosDeLos18CamposDelGps()
        {
            Assert.Equal(5, HitosSeguimiento.Carga.Count);
            Assert.Equal(12, HitosSeguimiento.Descarga.Count);
            var columnas = HitosSeguimiento.Todos.SelectMany(h => new[] { h.Columna }.Concat(h.ColumnasIguales)).ToList();
            Assert.Equal(18, columnas.Distinct().Count());
        }

        [Fact]
        public void ExportacionStatusGeneral_CadaHitoConSuColumnaHora()
        {
            var t = new DataTable();
            foreach (var c in new[] { "cliente", "tracto1", "carreta", "bodegaDescarga" }) t.Columns.Add(c, typeof(string));
            foreach (var c in new[] { "fhIngresoPlanta", "fhProgramacion" }) t.Columns.Add(c, typeof(DateTime));
            t.Rows.Add("4400133136", "ANU-794", "AUK-976", "JAVE", new DateTime(2026, 9, 30, 6, 1, 28), new DateTime(2026, 9, 30, 7, 45, 0));

            var r = ExportacionStatusGeneral.Construir(t.Rows.Cast<DataRow>().ToList(), new[] { "Pedido: 4400133136" }, "prueba");
            var titulos = r.Columnas.Select(c => c.Titulo).ToList();

            // Encabezados que reconoce la importación, cada uno seguido de HORA (como el STATUS GENERAL).
            int i = titulos.IndexOf("F.H.I PLANTA");
            Assert.True(i > 0);
            Assert.Equal("HORA", titulos[i + 1]);
            Assert.Equal("F.H.LL BASE", titulos[titulos.LastIndexOf("HORA") - 1]);
            Assert.Contains("CLIENTE", titulos);
            Assert.Contains("BODEGA DESCARGA", titulos);

            var fila = t.Rows[0];
            Assert.Equal(new DateTime(2026, 9, 30, 6, 1, 28), r.Columnas[i].Valor(fila));
            Assert.Equal("06:01:28", r.Columnas[i + 1].Valor(fila));
            Assert.Null(r.Columnas[titulos.IndexOf("F.H.S PLANTA")].Valor(fila)); // columna que no está en la fila
            Assert.False(r.IncluirTotales);

            // El Excel real: la importación busca los encabezados en las primeras 15 filas
            // (ImportarSeguimiento.DetectarFilaHeaders) y toma la hora de la columna "HORA" siguiente.
            var empresa = new Helpers.EmpresaInfo { RazonSocial = "SERVICIOS GENERALES VIVIANA E.I.R.L.", Ruc = "20483851171", DomicilioFiscal = "Lima" };
            var ws = new ClosedXML.Excel.XLWorkbook(new System.IO.MemoryStream(ReporteTabularExportador.GenerarExcel(r, empresa))).Worksheet(1);
            var cab = ws.RowsUsed().First(f => f.CellsUsed().Any(c => c.GetString() == "CLIENTE"));
            Assert.True(cab.RowNumber() <= 15);
            var celdaIngreso = cab.CellsUsed().Single(c => c.GetString() == "F.H.I PLANTA");
            Assert.Equal("HORA", cab.Cell(celdaIngreso.Address.ColumnNumber + 1).GetString());
            var datos = ws.Row(cab.RowNumber() + 1);
            Assert.Equal(new DateTime(2026, 9, 30), datos.Cell(celdaIngreso.Address.ColumnNumber).GetDateTime().Date);
            Assert.Equal("06:01:28", datos.Cell(celdaIngreso.Address.ColumnNumber + 1).GetString());
        }
    }
}
