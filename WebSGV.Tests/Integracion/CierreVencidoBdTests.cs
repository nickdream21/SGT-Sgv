using System;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Viajes vencidos del tablero contra la BD de pruebas, dentro de una <see cref="TransaccionDePrueba"/> (no deja datos):
    /// el tablero los marca con el viaje posterior de la carreta y sp_SE_CerrarVencido cierra solo los vencidos. Cada caso de
    /// error va en su propia prueba, al final: un THROW del SP (SET XACT_ABORT ON) deja abortada la transacción. Pedidos y
    /// placas inventados.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class CierreVencidoBdTests
    {
        public CierreVencidoBdTests() => BaseDePruebas.Preparar();

        private const string Pedido = "99000007";
        private const string Carreta = "ZZV-901";

        private static int Viaje(string pedido, int diasAtras) =>
            SeguimientoViajesService.GuardarDatosViaje(0, pedido, Carreta, "zzv-101", "Chofer Prueba", "zzv-102", null,
                null, FechaHelper.Hoy().AddDays(-diasAtras).AddHours(6), null);

        private static DataRow Fila(int id) =>
            DbHelper.ConsultarTabla("SELECT estado, motivoCierre, fhCierreAdministrativo, idUsuarioCierre FROM SeguimientoExportacion WHERE idSeguimiento = @id",
                DbHelper.Param("@id", id)).Rows[0];

        private static int? UnUsuario()
        {
            object id = DbHelper.EjecutarEscalar("SELECT MIN(idUsuario) FROM Usuarios");
            return id == null || id == DBNull.Value ? (int?)null : Convert.ToInt32(id);
        }

        [FactBD]
        public void Tablero_MarcaElVencidoConElViajePosteriorDeLaCarreta_YElGpsNoLoConsulta()
        {
            using (new TransaccionDePrueba())
            {
                int vencido = Viaje(Pedido + "01", 15);
                int posterior = Viaje(Pedido + "02", 2);
                int sinPosterior = SeguimientoViajesService.GuardarDatosViaje(0, Pedido + "03", "ZZV-902", "zzv-103", null, null, null,
                    null, FechaHelper.Hoy().AddDays(-12), null);

                var tablero = SeguimientoViajesService.ListarTablero(new FiltroTablero
                {
                    Desde = FechaHelper.Hoy().AddDays(-1), Hasta = FechaHelper.Hoy(), Texto = Pedido,
                });
                var v = tablero.Single(x => x.Id == vencido);
                Assert.True(v.Vencido);
                Assert.Equal(15, v.DiasAbierto);
                Assert.Equal(posterior, v.PosteriorId);
                Assert.Equal(Pedido + "02", v.PosteriorPedido);
                Assert.Equal(TipoAviso.Peligro, v.Avisos[0].Tipo);
                Assert.StartsWith("Vencido: abierto hace 15 días", v.Avisos[0].Texto);
                Assert.Contains($"La carreta {Carreta} ya salió en el viaje {Pedido}02", v.Avisos[0].Texto);

                var p = tablero.Single(x => x.Id == posterior);
                Assert.False(p.Vencido);
                Assert.Null(p.PosteriorId);
                var s = tablero.Single(x => x.Id == sinPosterior);
                Assert.True(s.Vencido);
                Assert.Null(s.PosteriorId);

                // Las columnas del viaje posterior no se cuelan en la fila (va al STATUS GENERAL).
                Assert.DoesNotContain(v.Fila.Table.Columns.Cast<DataColumn>(), c => c.ColumnName.StartsWith("sgv", StringComparison.OrdinalIgnoreCase));

                var gps = SeguimientoViajesService.AbiertosParaGps(tablero, FechaHelper.Hoy()).Select(x => x.Id).ToList();
                Assert.DoesNotContain(vencido, gps);
                Assert.Contains(posterior, gps);
                Assert.Contains(sinPosterior, gps);

                Assert.Equal(new[] { posterior }, PestanasSeguimiento.Filtrar(tablero, PestanasSeguimiento.EnCurso).Select(x => x.Id));
                Assert.Equal(new[] { sinPosterior, vencido }.OrderBy(x => x),
                    PestanasSeguimiento.Filtrar(tablero, PestanasSeguimiento.Vencidos).Select(x => x.Id).OrderBy(x => x));
            }
        }

        [FactBD]
        public void CerrarVencido_LoFinalizaConMotivoFechaYUsuario()
        {
            using (new TransaccionDePrueba())
            {
                int id = Viaje(Pedido + "11", 15);
                int? usuario = UnUsuario();
                SeguimientoViajesService.CerrarVencido(id, "  " + ViajeVencido.MotivoDescargaNoRegistrada + " ", usuario);

                var f = Fila(id);
                Assert.Equal("FINALIZADO", Convert.ToString(f["estado"]));
                Assert.Equal(ViajeVencido.MotivoDescargaNoRegistrada, Convert.ToString(f["motivoCierre"]));
                Assert.Equal(FechaHelper.Hoy(), Convert.ToDateTime(f["fhCierreAdministrativo"]).Date);
                Assert.Equal(usuario, f.IsNull("idUsuarioCierre") ? (int?)null : Convert.ToInt32(f["idUsuarioCierre"]));

                var d = SeguimientoViajesService.ObtenerDetalle(id).Resumen;
                Assert.Equal("cerrado", d.Fase.Grupo);
                Assert.False(d.Vencido);
                Assert.Equal($"Cerrado administrativamente el {FechaHelper.Hoy():dd/MM/yyyy}: {ViajeVencido.MotivoDescargaNoRegistrada}",
                    ViajeVencido.TextoCierre(d.Estado, d.MotivoCierre, d.FhCierreAdministrativo));

                // Varios a la vez: el cerrado ya no se cierra de nuevo y no frena al otro.
                int otro = Viaje(Pedido + "12", 20);
                var r = SeguimientoViajesService.CerrarVencidos(new[] { otro, otro }, ViajeVencido.MotivoCarretaEnOtroViaje, usuario);
                Assert.Equal(1, r.Item1);
                Assert.Empty(r.Item2);
                Assert.Equal("FINALIZADO", Convert.ToString(Fila(otro)["estado"]));
            }
        }

        [FactBD]
        public void Reabrir_BorraElCierreAdministrativo_YUnCierreNormalNoMuestraElMotivoViejo()
        {
            using (new TransaccionDePrueba())
            {
                int id = Viaje(Pedido + "13", 15);
                SeguimientoViajesService.CerrarVencido(id, ViajeVencido.MotivoCarretaEnOtroViaje, UnUsuario());
                SeguimientoViajesService.CambiarEstado(id, "EN_CURSO", null);
                var f = Fila(id);
                Assert.Equal("EN_CURSO", Convert.ToString(f["estado"]));
                Assert.True(f.IsNull("motivoCierre") && f.IsNull("fhCierreAdministrativo") && f.IsNull("idUsuarioCierre"));

                SeguimientoViajesService.CambiarEstado(id, "FINALIZADO", null);
                var d = SeguimientoViajesService.ObtenerDetalle(id).Resumen;
                Assert.Equal("FINALIZADO", d.Estado);
                Assert.Null(ViajeVencido.TextoCierre(d.Estado, d.MotivoCierre, d.FhCierreAdministrativo));
            }
        }

        [FactBD]
        public void Tablero_UnMismoPedidoYProgramacionSaleContiguo()
        {
            using (new TransaccionDePrueba())
            {
                // Creados intercalados (B, A, B, A) con la misma programación: el tablero los junta por pedido.
                DateTime prog = FechaHelper.Hoy().AddHours(5);
                foreach (string p in new[] { "B", "A", "B", "A" })
                    SeguimientoViajesService.GuardarDatosViaje(0, Pedido + "6" + p, Carreta, "zzv-104", null, null, null, null, prog, null);
                var pedidos = SeguimientoViajesService.ListarTablero(new FiltroTablero
                {
                    Desde = FechaHelper.Hoy(), Hasta = FechaHelper.Hoy(), Texto = Pedido + "6",
                }).Select(v => v.Pedido).ToList();
                Assert.Equal(new[] { Pedido + "6A", Pedido + "6A", Pedido + "6B", Pedido + "6B" }, pedidos);
            }
        }

        [FactBD]
        public void CerrarVencidos_SinIds_NoCierraNada()
        {
            var r = SeguimientoViajesService.CerrarVencidos(new int[0], ViajeVencido.MotivoCarretaEnOtroViaje, null);
            Assert.Equal(0, r.Item1);
            Assert.Empty(r.Item2);
        }

        [FactBD]
        public void CerrarVencido_MotivoVacio_SeRechazaAntesDelSp()
        {
            using (new TransaccionDePrueba())
            {
                int id = Viaje(Pedido + "21", 15);
                var ex = Assert.Throws<ErrorNegocioException>(() => SeguimientoViajesService.CerrarVencido(id, "   ", null));
                Assert.Equal("Escribe el motivo del cierre.", ex.Message);
                Assert.Throws<ErrorNegocioException>(() => SeguimientoViajesService.CerrarVencido(id, new string('x', 201), null));
                Assert.Equal("EN_CURSO", Convert.ToString(Fila(id)["estado"]));
            }
        }

        [FactBD]
        public void Sp_MotivoVacio_LoRechaza()
        {
            using (new TransaccionDePrueba())
            {
                int id = Viaje(Pedido + "22", 15);
                var ex = Assert.ThrowsAny<Exception>(() => DbHelper.EjecutarNonQuerySp("sp_SE_CerrarVencido",
                    DbHelper.Param("@idSeguimiento", id), DbHelper.Param("@motivo", "  "), DbHelper.Param("@idUsuario", null)));
                Assert.Equal("Escribe el motivo del cierre.", MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        [FactBD]
        public void Sp_ViajeDeTresDias_NoEstaVencido()
        {
            using (new TransaccionDePrueba())
            {
                int id = Viaje(Pedido + "31", 3);
                var ex = Assert.ThrowsAny<Exception>(() => SeguimientoViajesService.CerrarVencido(id, ViajeVencido.MotivoCarretaEnOtroViaje, null));
                Assert.StartsWith("Este viaje no está vencido", MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        [FactBD]
        public void Sp_ViajeDeDiezDias_TodaviaNoEstaVencido()
        {
            using (new TransaccionDePrueba())
            {
                int id = Viaje(Pedido + "32", 10);
                var ex = Assert.ThrowsAny<Exception>(() => SeguimientoViajesService.CerrarVencido(id, ViajeVencido.MotivoCarretaEnOtroViaje, null));
                Assert.StartsWith("Este viaje no está vencido", MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        [FactBD]
        public void Sp_ViajeCancelado_LoRechaza()
        {
            using (new TransaccionDePrueba())
            {
                int id = Viaje(Pedido + "41", 15);
                DbHelper.EjecutarNonQuery("UPDATE SeguimientoExportacion SET estado = 'CANCELADO' WHERE idSeguimiento = @id", DbHelper.Param("@id", id));
                var ex = Assert.ThrowsAny<Exception>(() => SeguimientoViajesService.CerrarVencido(id, ViajeVencido.MotivoCarretaEnOtroViaje, null));
                Assert.StartsWith("Este viaje está anulado", MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        [FactBD]
        public void Sp_ViajeImportadoDelExcel_LoRechaza()
        {
            using (new TransaccionDePrueba())
            {
                int id = Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                    INSERT INTO SeguimientoExportacion (cliente, carreta, fhProgramacion, estado, activo)
                    OUTPUT INSERTED.idSeguimiento
                    VALUES (@p, @c, DATEADD(DAY, -30, dbo.fn_AhoraPeru()), 'EN_CURSO', 1)",
                    DbHelper.Param("@p", Pedido + "71"), DbHelper.Param("@c", Carreta)));
                var ex = Assert.ThrowsAny<Exception>(() => SeguimientoViajesService.CerrarVencido(id, ViajeVencido.MotivoCarretaEnOtroViaje, null));
                Assert.StartsWith("Este viaje se importó del Excel", MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        [FactBD]
        public void Sp_ViajeYaCerrado_LoRechaza()
        {
            using (new TransaccionDePrueba())
            {
                int id = Viaje(Pedido + "51", 15);
                SeguimientoViajesService.CambiarEstado(id, "FINALIZADO", null);
                var ex = Assert.ThrowsAny<Exception>(() => SeguimientoViajesService.CerrarVencido(id, ViajeVencido.MotivoCarretaEnOtroViaje, null));
                Assert.Equal("Este viaje ya está cerrado.", MensajeErrorHelper.ParaUsuario(ex));
            }
        }
    }
}
