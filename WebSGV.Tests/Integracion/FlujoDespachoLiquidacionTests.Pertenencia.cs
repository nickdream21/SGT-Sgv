using System;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Conductor;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E6-15: los tres controles "esta orden es de este conductor" de las pantallas del conductor, ahora en
    /// <see cref="AccesoLiquidacion"/>, fijados en cada estado de la orden (sin firma, declarada,
    /// POR_CONFORMAR y APROBADO) y para otro conductor, que siempre se rechaza:
    /// <list type="bullet">
    ///   <item><see cref="AccesoLiquidacion.EsDelConductor"/> (DetalleOrdenViaje): la suya en cualquier estado.</item>
    ///   <item><see cref="AccesoLiquidacion.EsperaDeclaracionDelConductor"/> (FirmarLiquidacion): solo la suya sin firma.</item>
    ///   <item><see cref="AccesoLiquidacion.EsperaConformidadDelUsuario"/> (ConformarLiquidacion): solo la suya POR_CONFORMAR.</item>
    /// </list>
    /// Más las lecturas que salieron de MovimientosViaje y SaldosConductores. Todo dentro de una
    /// <see cref="TransaccionDePrueba"/>.
    /// </summary>
    public partial class FlujoDespachoLiquidacionTests
    {
        private struct Pertenencia
        {
            public bool Detalle, Firma, Conformidad;
            public override string ToString() => $"detalle={Detalle}, firma={Firma}, conformidad={Conformidad}";
        }

        private static Pertenencia Medir(int idOrden, int idConductor, int idUsuario) => new Pertenencia
        {
            Detalle = AccesoLiquidacion.EsDelConductor(idOrden, idConductor),
            Firma = AccesoLiquidacion.EsperaDeclaracionDelConductor(idOrden, idConductor),
            Conformidad = AccesoLiquidacion.EsperaConformidadDelUsuario(idOrden, idUsuario),
        };

        private static void Esperar(string estado, Pertenencia real, bool detalle, bool firma, bool conformidad)
        {
            var esperado = new Pertenencia { Detalle = detalle, Firma = firma, Conformidad = conformidad };
            Assert.True(esperado.Equals(real), estado + ": se esperaba " + esperado + " y salió " + real);
        }

        private static int UsuarioDelConductor(int idConductor)
        {
            object v = DbHelper.EjecutarEscalar(
                "SELECT TOP 1 idUsuario FROM Usuarios WHERE idConductor = @c AND activo = 1 ORDER BY idUsuario",
                DbHelper.Param("@c", idConductor));
            Assert.True(v != null && v != DBNull.Value, "El segundo conductor de prueba necesita un usuario activo.");
            return Convert.ToInt32(v);
        }

        [FactBD]
        public void Pertenencia_CadaPredicadoEnCadaEstado_YOtroConductorSeRechaza()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idUsuarioOtro = UsuarioDelConductor(datos.IdOtroConductor);

                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(NuevaLiquidacion(datos, idViaje));

                // Sin firma (PENDIENTE): el detalle y la firma, no la conformidad.
                Esperar("sin firma", Medir(idOrden, datos.IdConductor, datos.IdUsuarioConductor), true, true, false);
                Esperar("sin firma, otro conductor", Medir(idOrden, datos.IdOtroConductor, idUsuarioOtro), false, false, false);

                // Declarada (PENDIENTE con firma del conductor): ya no se firma otra vez.
                FirmarComoConductor(idOrden, datos.IdUsuarioConductor);
                Assert.Equal(EstadosOrden.Pendiente,
                    Fila("SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["estadoAprobacion"]);
                Esperar("declarada", Medir(idOrden, datos.IdConductor, datos.IdUsuarioConductor), true, false, false);
                Esperar("declarada, otro conductor", Medir(idOrden, datos.IdOtroConductor, idUsuarioOtro), false, false, false);

                // POR_CONFORMAR (revisada y firmada por la administración): solo la conformidad.
                var envio = LiquidacionesPendientesService.EnviarAConformidad(idOrden, AjustesDePrueba(), null, datos.IdUsuarioAdmin);
                Assert.True(envio.Exito, envio.Mensaje);
                string huella = HuellaLiquidacion.Calcular(LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrden));
                FirmarRevision(idOrden, datos.IdUsuarioAdmin, huella);
                Assert.Equal(EstadosOrden.PorConformar,
                    Fila("SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["estadoAprobacion"]);
                Esperar("POR_CONFORMAR", Medir(idOrden, datos.IdConductor, datos.IdUsuarioConductor), true, false, true);
                Esperar("POR_CONFORMAR, otro conductor", Medir(idOrden, datos.IdOtroConductor, idUsuarioOtro), false, false, false);
                // El usuario de la administración no es el conductor de la orden.
                Assert.False(AccesoLiquidacion.EsperaConformidadDelUsuario(idOrden, datos.IdUsuarioAdmin));

                // APROBADO (conformidad firmada): solo el detalle.
                Assert.Equal(1, FirmarConformidad(idOrden, datos.IdUsuarioConductor, huella, out string mensaje));
                Assert.Equal(EstadosOrden.Aprobado,
                    Fila("SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["estadoAprobacion"]);
                Esperar("APROBADO", Medir(idOrden, datos.IdConductor, datos.IdUsuarioConductor), true, false, false);
                Esperar("APROBADO, otro conductor", Medir(idOrden, datos.IdOtroConductor, idUsuarioOtro), false, false, false);

                // Sin conductor en la sesión u orden inexistente: nada.
                Assert.False(AccesoLiquidacion.EsDelConductor(idOrden, 0));
                int inexistente = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT ISNULL(MAX(idOrdenViaje), 0) + 1000 FROM OrdenViaje"));
                Esperar("orden inexistente", Medir(inexistente, datos.IdConductor, datos.IdUsuarioConductor), false, false, false);
            }
        }

        [FactBD]
        public void Pertenencia_UsuarioInactivo_NoConforma()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                var liquidacion = CrearLiquidacionDePrueba();
                int idOrden = liquidacion.IdOrden;
                FirmarComoConductor(idOrden, datos.IdUsuarioConductor);
                var envio = LiquidacionesPendientesService.EnviarAConformidad(idOrden, AjustesDePrueba(), null, datos.IdUsuarioAdmin);
                Assert.True(envio.Exito, envio.Mensaje);
                FirmarRevision(idOrden, datos.IdUsuarioAdmin,
                    HuellaLiquidacion.Calcular(LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrden)));
                Assert.True(AccesoLiquidacion.EsperaConformidadDelUsuario(idOrden, datos.IdUsuarioConductor));

                // Conformar exige el usuario activo (JOIN a Usuarios); el detalle y la firma miran solo el conductor.
                DbHelper.EjecutarNonQuery("UPDATE Usuarios SET activo = 0 WHERE idUsuario = @u",
                    DbHelper.Param("@u", datos.IdUsuarioConductor));
                Assert.False(AccesoLiquidacion.EsperaConformidadDelUsuario(idOrden, datos.IdUsuarioConductor));
                Assert.True(AccesoLiquidacion.EsDelConductor(idOrden, datos.IdConductor));
            }
        }

        [FactBD]
        public void MovimientosViaje_ConductoresYViajeAbierto()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                string numeroViaje = Convert.ToString(Fila(
                    "SELECT numeroViajeProgreso FROM ViajesEnProgreso WHERE idViajeProgreso = @id", idViaje)["numeroViajeProgreso"]);

                DataTable conductores = MovimientosViajeService.ConductoresParaRegistrar();
                Assert.Equal(new[] { "idConductor", "nombre", "activo" },
                    conductores.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray());
                Assert.Contains(conductores.Rows.Cast<DataRow>(), r => Convert.ToInt32(r["idConductor"]) == datos.IdConductor);

                // Dado de baja pero con el viaje abierto: sigue en la lista, marcado inactivo.
                DbHelper.EjecutarNonQuery("UPDATE Conductor SET activo = 0 WHERE idConductor = @c", DbHelper.Param("@c", datos.IdConductor));
                DataRow fila = MovimientosViajeService.ConductoresParaRegistrar().Rows.Cast<DataRow>()
                    .Single(r => Convert.ToInt32(r["idConductor"]) == datos.IdConductor);
                Assert.False(Convert.ToBoolean(fila["activo"]));

                // El viaje abierto más reciente es el recién creado.
                string descripcion = MovimientosViajeService.DescripcionViajeAbierto(datos.IdConductor);
                Assert.StartsWith(numeroViaje + " (desde ", descripcion);

                // Sin viaje abierto: null.
                DbHelper.EjecutarNonQuery("UPDATE ViajesEnProgreso SET estadoViaje = 'CERRADO' WHERE idConductor = @c AND estadoViaje = 'ABIERTO'",
                    DbHelper.Param("@c", datos.IdConductor));
                Assert.Null(MovimientosViajeService.DescripcionViajeAbierto(datos.IdConductor));
            }
        }

        [FactBD]
        public void SaldosConductores_FiltroConLosConductoresConLiquidacionAprobada()
        {
            using (new TransaccionDePrueba())
            {
                var liquidacion = CrearLiquidacionDePrueba(aprobada: true);
                DataTable conductores = SaldosConductorService.ConductoresConLiquidacionAprobada();
                Assert.Equal(new[] { "idConductor", "Nombre" },
                    conductores.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray());
                Assert.Single(conductores.Rows.Cast<DataRow>(), r => Convert.ToInt32(r["idConductor"]) == liquidacion.IdConductor);

                // Ninguno sin una orden APROBADO.
                foreach (DataRow r in conductores.Rows)
                    Assert.True(Convert.ToInt32(DbHelper.EjecutarEscalar(
                        "SELECT COUNT(*) FROM OrdenViaje WHERE idConductor = @c AND estadoAprobacion = @a",
                        DbHelper.Param("@c", Convert.ToInt32(r["idConductor"])), DbHelper.Param("@a", EstadosOrden.Aprobado))) > 0);
            }
        }
    }
}
