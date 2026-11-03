using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using WebSGV.Helpers;
using WebSGV.Models.Liquidaciones;
using WebSGV.Services;
using WebSGV.Services.Conductor;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Medición de rendimiento del flujo de escritura del conductor (no es una prueba de
    /// corrección). Solo corre con SGV_RENDIMIENTO=1; cada iteración va dentro de una
    /// <see cref="TransaccionDePrueba"/> y no deja datos. El informe se guarda en
    /// SGV_RENDIMIENTO_SALIDA (por defecto %TEMP%\sgv-rendimiento.txt).
    /// </summary>
    public partial class FlujoDespachoLiquidacionTests
    {
        public sealed class FactRendimientoAttribute : FactAttribute
        {
            public FactRendimientoAttribute()
            {
                if (!BaseDePruebas.Disponible)
                    Skip = BaseDePruebas.MotivoNoDisponible;
                else if (Environment.GetEnvironmentVariable("SGV_RENDIMIENTO") != "1")
                    Skip = "Medición de rendimiento: definir SGV_RENDIMIENTO=1 para ejecutarla.";
            }
        }

        private static readonly EmpresaInfo EmpresaRend = new EmpresaInfo
        {
            RazonSocial = "SERVICIOS GENERALES VIVIANA E.I.R.L.", Rubro = "Transporte y Construcción",
            Ruc = "20483851171", DomicilioFiscal = "Lima", Web = "sgvtransporte.somee.com"
        };
        private static readonly FormatoControladoInfo FormatoRend = new FormatoControladoInfo
        {
            CodigoFormato = "SGV-CDF-F-05", Version = "01", FechaVigencia = new DateTime(2025, 1, 1)
        };

        private static string Resumen(string paso, List<double> ms)
        {
            var s = ms.OrderBy(x => x).ToList();
            double p95 = s[Math.Min(s.Count - 1, (int)Math.Floor(s.Count * 0.95))];
            return string.Format("{0,-34} n={1,-3} prom={2,7:N0} ms  mediana={3,7:N0} ms  p95={4,7:N0} ms  max={5,7:N0} ms",
                paso, s.Count, s.Average(), s[s.Count / 2], p95, s.Max());
        }

        [FactRendimiento]
        public void Rendimiento_FlujoDeEscrituraDelConductorYGeneracionDePdf()
        {
            const int iteraciones = 12;
            var t = new Dictionary<string, List<double>>
            {
                ["1 Registrar despacho (programar)"] = new List<double>(),
                ["2 Enviar liquidación (conductor)"] = new List<double>(),
                ["3 Firma digital (SP)"] = new List<double>(),
                ["4 Leer detalle para el PDF"] = new List<double>(),
                ["5 Generar PDF (QuestPDF)"] = new List<double>(),
                ["Total del flujo del conductor 2-5"] = new List<double>()
            };
            var tamanos = new List<int>();
            DetalleLiquidacion detalleParaParalelo = null;
            double pdfFrio = 0;
            string logo = BaseDePruebas.RaizRepo == null ? null : Path.Combine(BaseDePruebas.RaizRepo, "WebSGV", "Content", "favicon.png");
            var sw = new Stopwatch();

            for (int i = 0; i < iteraciones; i++)
            {
                using (new TransaccionDePrueba())
                {
                    var datos = DatosBase.Cargar();

                    sw.Restart();
                    int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                    t["1 Registrar despacho (programar)"].Add(sw.Elapsed.TotalMilliseconds);

                    var input = NuevaLiquidacion(datos, idViaje);
                    var total = Stopwatch.StartNew();

                    sw.Restart();
                    int idOrden = LiquidacionConductorService.EnviarLiquidacion(input);
                    t["2 Enviar liquidación (conductor)"].Add(sw.Elapsed.TotalMilliseconds);

                    sw.Restart();
                    FirmarComoConductor(idOrden, datos.IdUsuarioConductor);
                    t["3 Firma digital (SP)"].Add(sw.Elapsed.TotalMilliseconds);

                    sw.Restart();
                    DetalleLiquidacion detalle = LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrden);
                    t["4 Leer detalle para el PDF"].Add(sw.Elapsed.TotalMilliseconds);

                    sw.Restart();
                    byte[] pdf = PdfOrdenViajeService.CrearDocumento(detalle, EmpresaRend, FormatoRend,
                        new FirmasOrdenViaje { TrazoConductor = null }, logo).GeneratePdf();
                    double ms = sw.Elapsed.TotalMilliseconds;
                    t["5 Generar PDF (QuestPDF)"].Add(ms);
                    if (i == 0) pdfFrio = ms;
                    tamanos.Add(pdf.Length);

                    t["Total del flujo del conductor 2-5"].Add(total.Elapsed.TotalMilliseconds);
                    detalleParaParalelo = detalle;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("RENDIMIENTO DEL FLUJO DEL CONDUCTOR - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " (BD de pruebas, con rollback)");
            sb.AppendLine("Iteraciones secuenciales: " + iteraciones + "   Máquina: " + Environment.ProcessorCount + " núcleos lógicos");
            sb.AppendLine();
            foreach (var kv in t) sb.AppendLine(Resumen(kv.Key, kv.Value));
            sb.AppendLine(string.Format("Primer PDF (en frío, carga de Skia): {0:N0} ms   Tamaño medio del PDF: {1:N0} KB", pdfFrio, tamanos.Average() / 1024.0));

            // Capacidad de generación de PDF en paralelo (lo más costoso de CPU en el hosting).
            sb.AppendLine();
            sb.AppendLine("PDF en paralelo (24 PDF por escenario, mismo documento):");
            foreach (int hilos in new[] { 1, 2, 4, 8 })
            {
                var lat = new System.Collections.Concurrent.ConcurrentBag<double>();
                var total = Stopwatch.StartNew();
                Parallel.For(0, 24, new ParallelOptions { MaxDegreeOfParallelism = hilos }, _ =>
                {
                    var one = Stopwatch.StartNew();
                    PdfOrdenViajeService.CrearDocumento(detalleParaParalelo, EmpresaRend, FormatoRend, new FirmasOrdenViaje(), logo).GeneratePdf();
                    lat.Add(one.Elapsed.TotalMilliseconds);
                });
                total.Stop();
                var l = lat.OrderBy(x => x).ToList();
                sb.AppendLine(string.Format("  {0} hilo(s): {1,5:N1} PDF/s   latencia prom={2,6:N0} ms  p95={3,6:N0} ms   (total {4:N1} s)",
                    hilos, 24 / total.Elapsed.TotalSeconds, l.Average(), l[(int)Math.Floor(l.Count * 0.95)], total.Elapsed.TotalSeconds));
            }

            // Latencia pura a la BD y lecturas fuera de transacción (sin el costo del scope de prueba).
            sb.AppendLine();
            DbHelper.EjecutarEscalar("SELECT 1");
            var rtt = new List<double>();
            for (int i = 0; i < 30; i++) { sw.Restart(); DbHelper.EjecutarEscalar("SELECT 1"); rtt.Add(sw.Elapsed.TotalMilliseconds); }
            sb.AppendLine(Resumen("Ida y vuelta a la BD (SELECT 1)", rtt));

            object idExistente = DbHelper.EjecutarEscalar("SELECT TOP 1 idOrdenViaje FROM OrdenViaje ORDER BY idOrdenViaje DESC");
            if (idExistente != null && idExistente != DBNull.Value)
            {
                int id = Convert.ToInt32(idExistente);
                LiquidacionesPendientesService.ObtenerDetalleLiquidacion(id);
                var det = new List<double>();
                for (int i = 0; i < 10; i++) { sw.Restart(); LiquidacionesPendientesService.ObtenerDetalleLiquidacion(id); det.Add(sw.Elapsed.TotalMilliseconds); }
                sb.AppendLine(Resumen("Leer detalle SIN transacción de prueba", det));
                var pen = new List<double>();
                for (int i = 0; i < 10; i++) { sw.Restart(); LiquidacionesPendientesService.ObtenerPendientes(null, null, null); pen.Add(sw.Elapsed.TotalMilliseconds); }
                sb.AppendLine(Resumen("Bandeja del admin (pendientes)", pen));
            }

            string salida = Environment.GetEnvironmentVariable("SGV_RENDIMIENTO_SALIDA")
                            ?? Path.Combine(Path.GetTempPath(), "sgv-rendimiento.txt");
            File.WriteAllText(salida, sb.ToString(), Encoding.UTF8);
            Assert.True(t["5 Generar PDF (QuestPDF)"].Count == iteraciones);
        }
    }
}
