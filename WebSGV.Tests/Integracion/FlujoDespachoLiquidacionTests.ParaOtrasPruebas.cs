using System;
using System.Collections.Generic;
using WebSGV.Services.Conductor;
using WebSGV.Services.Despachos;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Datos armados por los mismos servicios del flujo para las pruebas de otras clases que necesitan
    /// una liquidación (AnularLote, ReubicarDespacho, Saldos). Antes leían una OrdenViaje que ya existiera
    /// en la BD y fallaban cuando no había ninguna (se retiraron las de prueba en 873947a).
    /// Llamar siempre dentro de una <see cref="TransaccionDePrueba"/>.
    /// </summary>
    public partial class FlujoDespachoLiquidacionTests
    {
        internal sealed class LiquidacionDePrueba
        {
            public int IdOrden, IdConductor, IdOtroConductor;
            public string Numero;
            /// <summary>Viajes que cerró la liquidación; el primero es el principal (OrdenViaje.idViajeProgreso).</summary>
            public readonly List<int> Viajes = new List<int>();
            /// <summary>Un despacho activo por viaje, en el mismo orden que <see cref="Viajes"/>.</summary>
            public readonly List<int> Despachos = new List<int>();
        }

        internal sealed class DespachoDePrueba
        {
            public int IdDespacho, IdViaje, IdConductor, IdOtroConductor;
        }

        /// <summary>Un despacho nuevo en su propio viaje ABIERTO, sin liquidación.</summary>
        internal static DespachoDePrueba CrearDespachoDePrueba()
        {
            var datos = DatosBase.Cargar();
            int idViaje = CrearDespacho(datos, NuevoLote(datos), out int idDespacho);
            return new DespachoDePrueba
            {
                IdDespacho = idDespacho, IdViaje = idViaje,
                IdConductor = datos.IdConductor, IdOtroConductor = datos.IdOtroConductor
            };
        }

        /// <summary>
        /// Liquidación del conductor de prueba que cierra <paramref name="viajes"/> viajes, cada uno con un
        /// despacho (los demás viajes quedan con la misma fechaCierre que el principal). Con
        /// <paramref name="aprobada"/> recorre declaración, revisión y conformidad hasta APROBADO.
        /// </summary>
        internal static LiquidacionDePrueba CrearLiquidacionDePrueba(int viajes = 1, bool aprobada = false)
        {
            var datos = DatosBase.Cargar();
            var r = new LiquidacionDePrueba { IdConductor = datos.IdConductor, IdOtroConductor = datos.IdOtroConductor };

            var lote = NuevoLote(datos);
            r.Viajes.Add(CrearDespacho(datos, lote, out int primerDespacho));
            r.Despachos.Add(primerDespacho);
            for (int i = 1; i < viajes; i++)
            {
                int idViaje = RegistroDespachoService.CrearNuevoViajeProgreso(datos.IdConductor, "Viaje " + (i + 1) + " (test)", "test");
                lote.Conductores[0].IdViajeProgreso = idViaje;
                int idDespacho = RegistroDespachoService.CrearDespachoIndividual(lote, lote.Conductores[0], null, null);
                Assert.True(idDespacho > 0);
                r.Viajes.Add(idViaje);
                r.Despachos.Add(idDespacho);
            }

            var input = NuevaLiquidacion(datos, r.Viajes[0]);
            input.IdsViajesActivos = new List<int>(r.Viajes);
            r.IdOrden = LiquidacionConductorService.EnviarLiquidacion(input);
            Assert.True(r.IdOrden > 0);
            r.Numero = input.NumeroOrdenViaje;

            if (aprobada)
            {
                FirmarComoConductor(r.IdOrden, datos.IdUsuarioConductor);
                var envio = LiquidacionesPendientesService.EnviarAConformidad(r.IdOrden, AjustesDePrueba(), null, datos.IdUsuarioAdmin);
                Assert.True(envio.Exito, envio.Mensaje);
                string huella = HuellaLiquidacion.Calcular(LiquidacionesPendientesService.ObtenerDetalleLiquidacion(r.IdOrden));
                FirmarRevision(r.IdOrden, datos.IdUsuarioAdmin, huella);
                Assert.Equal(1, FirmarConformidad(r.IdOrden, datos.IdUsuarioConductor, huella, out string mensaje));
                Assert.Equal(EstadosOrden.Aprobado,
                    Fila("SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", r.IdOrden)["estadoAprobacion"]);
            }

            return r;
        }
    }
}
