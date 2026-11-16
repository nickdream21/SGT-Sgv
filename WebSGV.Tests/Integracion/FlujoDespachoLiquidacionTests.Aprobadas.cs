using System;
using System.Data;
using System.Linq;
using WebSGV.Services.Conductor;
using WebSGV.Services.Liquidaciones;
using WebSGV.Services.Reportes;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// "Aprobada" = con la conformidad del conductor firmada (auditoría de seguridad, tarea 8B): una
    /// liquidación POR_CONFORMAR no aparece en Control de aprobadas, Contabilidad, Reportes ni Saldos;
    /// después de la conformidad aparece en todas. Todo dentro de <see cref="TransaccionDePrueba"/>.
    /// </summary>
    public partial class FlujoDespachoLiquidacionTests
    {
        [FactBD]
        public void LiquidacionAprobada_SoloConLaConformidadFirmada_EnTodasLasPantallas()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(NuevaLiquidacion(datos, idViaje));
                string numero = Convert.ToString(Fila("SELECT numeroOrdenViaje FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["numeroOrdenViaje"]);
                FirmarComoConductor(idOrden, datos.IdUsuarioConductor);

                var envio = LiquidacionesPendientesService.EnviarAConformidad(idOrden, AjustesDePrueba(), null, datos.IdUsuarioAdmin);
                Assert.True(envio.Exito, envio.Mensaje);
                string huella = HuellaLiquidacion.Calcular(LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrden));
                FirmarRevision(idOrden, datos.IdUsuarioAdmin, huella);
                Assert.Equal(EstadosOrden.PorConformar,
                    Fila("SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["estadoAprobacion"]);

                // Revisada pero sin la conformidad del conductor: todavía no es "aprobada" en ninguna pantalla.
                Assert.False(EnControlDeAprobadas(datos.IdConductor, numero, idOrden), "POR_CONFORMAR en Control de aprobadas");
                Assert.False(EnContabilidad(datos.IdConductor, numero, idOrden), "POR_CONFORMAR en Contabilidad");
                Assert.False(EnReporteDeLiquidaciones(idOrden), "POR_CONFORMAR en Reportes");
                Assert.False(EnSaldos(datos.IdConductor, idOrden), "POR_CONFORMAR en Saldos de conductores");

                Assert.Equal(1, FirmarConformidad(idOrden, datos.IdUsuarioConductor, huella, out string mensaje));
                Assert.Equal(EstadosOrden.Aprobado,
                    Fila("SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["estadoAprobacion"]);

                Assert.True(EnControlDeAprobadas(datos.IdConductor, numero, idOrden), "APROBADO falta en Control de aprobadas: " + mensaje);
                Assert.True(EnContabilidad(datos.IdConductor, numero, idOrden), "APROBADO falta en Contabilidad");
                Assert.True(EnReporteDeLiquidaciones(idOrden), "APROBADO falta en Reportes");
                Assert.True(EnSaldos(datos.IdConductor, idOrden), "APROBADO falta en Saldos de conductores");

                // Autocompletado de Contabilidad: encuentra al conductor por "nombre apellido" aunque las
                // columnas char traigan espacios de relleno, y lo sugiere sin espacios de más.
                DataRow c = Fila("SELECT RTRIM(nombre) AS nombre, RTRIM(apPaterno) AS apPaterno FROM Conductor WHERE idConductor = @id", datos.IdConductor);
                string ultimoNombre = Convert.ToString(c["nombre"]).Split(' ').Last();
                string termino = ultimoNombre + " " + Convert.ToString(c["apPaterno"]);
                DataRow sugerido = LiquidacionesContabilidadService.BuscarConductoresConAprobadas(termino).Rows.Cast<DataRow>()
                    .FirstOrDefault(r => Convert.ToInt32(r["idConductor"]) == datos.IdConductor);
                Assert.True(sugerido != null, "El autocompletado no encontró al conductor con '" + termino + "'");
                string nombreCompleto = Convert.ToString(sugerido["nombreCompleto"]);
                Assert.Equal(nombreCompleto.Trim(), nombreCompleto);
                Assert.DoesNotContain("  ", nombreCompleto);
            }
        }

        private static bool Contiene(DataTable dt, string columna, int idOrden) =>
            dt.Rows.Cast<DataRow>().Any(r => Convert.ToInt32(r[columna]) == idOrden);

        private static bool EnControlDeAprobadas(int idConductor, string numero, int idOrden) =>
            Contiene(LiquidacionesPendientesService.ObtenerAprobadas(idConductor, null, null, numero, null, null), "idOrdenViaje", idOrden);

        private static bool EnContabilidad(int idConductor, string numero, int idOrden) =>
            Contiene(LiquidacionesContabilidadService.ObtenerAprobadas(idConductor, numero, ""), "idOrdenViaje", idOrden);

        private static bool EnReporteDeLiquidaciones(int idOrden) =>
            Contiene(ReportesOrdenesViajeService.ObtenerLiquidaciones(new DateTime(2000, 1, 1), new DateTime(2100, 1, 1)), "IdOrdenViaje", idOrden);

        private static bool EnSaldos(int idConductor, int idOrden) =>
            Contiene(SaldosConductorService.Listar(new DateTime(2000, 1, 1), new DateTime(2100, 1, 1), idConductor, false, false, new[] { idOrden }),
                "idOrdenViaje", idOrden);
    }
}
