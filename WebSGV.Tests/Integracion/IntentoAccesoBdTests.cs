using System;
using System.Collections.Generic;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E4-4: contadores anti-fuerza-bruta en la tabla IntentoAcceso (migración 61, SP sp_US_*IntentoAcceso)
    /// y auditoría de acceso, contra la BD de pruebas y dentro de una <see cref="TransaccionDePrueba"/>
    /// (nada queda guardado). Cada almacén lleva su propio respaldo vacío: si un caso pasa, es por la BD.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class IntentoAccesoBdTests
    {
        public IntentoAccesoBdTests() => BaseDePruebas.Preparar();

        private static AlmacenBdIntentos AlmacenNuevo() => new AlmacenBdIntentos(new RespaldoVacio(), () => DateTime.UtcNow);

        // Sin [FactBD] de reservas en paralelo: varias conexiones a la vez no caben en una TransaccionDePrueba
        // (pasaría a MSDTC) y fuera de ella quedarían filas. La atomicidad la da sp_US_ReservarIntentoAcceso
        // (UPDLOCK + HOLDLOCK); el probador la comprueba con logins en paralelo en el sitio local.

        /// <summary>IP de documentación (RFC 5737) y usuario inventado, distintos en cada caso.</summary>
        private static string IpDePrueba() => "198.51.100." + new Random(Guid.NewGuid().GetHashCode()).Next(1, 254);
        private static string UsuarioDePrueba() => "zz_intento_" + Guid.NewGuid().ToString("N").Substring(0, 12);

        private static int Filas(string where, params System.Data.SqlClient.SqlParameter[] p) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT COUNT(*) FROM dbo.IntentoAcceso WHERE " + where, p));

        [FactBD]
        public void CincoFallos_Bloquean_YUnaInstanciaNuevaSigueBloqueando()
        {
            using (new TransaccionDePrueba())
            {
                string ip = IpDePrueba(), usuario = UsuarioDePrueba();
                var almacen = AlmacenNuevo();
                var control = ControlIntentosLogin.CrearLogin(almacen, () => DateTime.UtcNow);

                for (int i = 1; i <= 4; i++)
                    Assert.False(control.ReservarLogin(ip, usuario).BloqueoCuenta);
                var quinto = control.ReservarLogin(ip, usuario);
                Assert.True(quinto.BloqueoCuenta);
                Assert.False(almacen.UsandoRespaldo);   // fue a la BD

                // Reciclaje del grupo de aplicaciones: control y almacén nuevos, caché vacía.
                var reciclado = ControlIntentosLogin.CrearLogin(AlmacenNuevo(), () => DateTime.UtcNow);
                var sexto = reciclado.ReservarLogin(ip, usuario);
                Assert.True(sexto.Rechazado);
                Assert.InRange(sexto.SegundosRestantes, 14 * 60, 15 * 60 + 1);

                // La misma cuenta desde otra IP no está bloqueada (lleva 5 de 30 en la cuenta global).
                Assert.False(reciclado.ReservarLogin("203.0.113.7", usuario).Rechazado);

                // En la tabla: el hash, nunca el texto escrito.
                Assert.Equal(0, Filas("clave LIKE @u", DbHelper.Param("@u", "%" + usuario + "%")));
                Assert.Equal(1, Filas("clave = @c AND bloqueadoHastaUtc > SYSUTCDATETIME()",
                    DbHelper.Param("@c", ControlIntentosLogin.ClaveCuentaIp(usuario, ip))));
                Assert.Equal(1, Filas("clave = @c AND fallidos = 6",
                    DbHelper.Param("@c", ControlIntentosLogin.ClaveCuenta(usuario))));
            }
        }

        [FactBD]
        public void LoginCorrecto_QuitaLasFilasDeLaCuenta()
        {
            using (new TransaccionDePrueba())
            {
                string ip = IpDePrueba(), usuario = UsuarioDePrueba();
                var control = ControlIntentosLogin.CrearLogin(AlmacenNuevo(), () => DateTime.UtcNow);
                control.ReservarLogin(ip, usuario);
                control.LoginCorrecto(control.ReservarLogin(ip, usuario));

                Assert.Equal(0, Filas("clave IN (@a, @b)",
                    DbHelper.Param("@a", ControlIntentosLogin.ClaveCuenta(usuario)),
                    DbHelper.Param("@b", ControlIntentosLogin.ClaveCuentaIp(usuario, ip))));
            }
        }

        [FactBD]
        public void Vencidos_NoCuentan_YSeBorranAlReservar()
        {
            using (new TransaccionDePrueba())
            {
                var limite = new ControlIntentosLogin.Limite(5, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));
                string vencida = "ZZ_PRUEBA_" + Guid.NewGuid().ToString("N");
                DbHelper.EjecutarNonQuery(@"
                    INSERT INTO dbo.IntentoAcceso (clave, fallidos, primerFalloUtc, bloqueadoHastaUtc, venceUtc)
                    VALUES (@c, 4, DATEADD(HOUR, -2, SYSUTCDATETIME()), NULL, DATEADD(HOUR, -1, SYSUTCDATETIME()))",
                    DbHelper.Param("@c", vencida));

                // La fila vencida no suma: el intento empieza una ventana nueva (no bloquea en el 5.º).
                var control = new ControlIntentosLogin(AlmacenNuevo(), () => DateTime.UtcNow, null, limite);
                Assert.False(control.Reservar(null, vencida).BloqueoCuenta);
                Assert.Equal(1, Filas("clave = @c AND fallidos = 1 AND venceUtc > SYSUTCDATETIME()", DbHelper.Param("@c", vencida)));

                // Reservar borra vencidos de a 200: se repite hasta que no quede ninguno o se agote el margen.
                string otraVencida = "ZZ_PRUEBA_" + Guid.NewGuid().ToString("N");
                DbHelper.EjecutarNonQuery(@"
                    INSERT INTO dbo.IntentoAcceso (clave, fallidos, primerFalloUtc, bloqueadoHastaUtc, venceUtc)
                    VALUES (@c, 2, DATEADD(HOUR, -2, SYSUTCDATETIME()), NULL, DATEADD(HOUR, -1, SYSUTCDATETIME()))",
                    DbHelper.Param("@c", otraVencida));
                for (int i = 0; i < 20 && Filas("venceUtc < SYSUTCDATETIME()") > 0; i++)
                    control.ErrorAlValidar(control.Reservar(null, "ZZ_PRUEBA_" + Guid.NewGuid().ToString("N")));
                Assert.Equal(0, Filas("clave = @c", DbHelper.Param("@c", otraVencida)));
            }
        }

        [FactBD]
        public void ErrorAlValidar_LevantaElBloqueoQueCausoLaReserva()
        {
            using (new TransaccionDePrueba())
            {
                var limite = new ControlIntentosLogin.Limite(5, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));
                string clave = "ZZ_PRUEBA_" + Guid.NewGuid().ToString("N");
                var control = new ControlIntentosLogin(AlmacenNuevo(), () => DateTime.UtcNow, null, limite);
                for (int i = 0; i < 4; i++) control.Reservar(null, clave);
                var quinto = control.Reservar(null, clave);
                Assert.True(quinto.BloqueoCuenta);

                control.ErrorAlValidar(quinto);
                Assert.Equal(1, Filas("clave = @c AND fallidos = 4 AND bloqueadoHastaUtc IS NULL", DbHelper.Param("@c", clave)));

                // Como antes de la reserva: el siguiente bloquea; devolver un fallo normal resta uno.
                Assert.True(control.Reservar(null, clave).BloqueoCuenta);
                Assert.True(control.Reservar(null, clave).Rechazado);

                string otra = "ZZ_PRUEBA_" + Guid.NewGuid().ToString("N");
                control.Reservar(null, otra);
                control.ErrorAlValidar(control.Reservar(null, otra));
                Assert.Equal(1, Filas("clave = @c AND fallidos = 1", DbHelper.Param("@c", otra)));
                control.ErrorAlValidar(control.Reservar(null, otra));
                control.LoginCorrecto(control.Reservar(null, otra));
                Assert.Equal(0, Filas("clave = @c", DbHelper.Param("@c", otra)));
            }
        }

        [FactBD]
        public void UsuarioConFormatoInvalido_CuentaEnLaClaveFija()
        {
            using (new TransaccionDePrueba())
            {
                string ip = IpDePrueba();
                var control = ControlIntentosLogin.CrearLogin(AlmacenNuevo(), () => DateTime.UtcNow);
                int antes = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT ISNULL(MAX(fallidos), 0) FROM dbo.IntentoAcceso WHERE clave = @c AND venceUtc > SYSUTCDATETIME()",
                    DbHelper.Param("@c", ControlIntentosLogin.ClaveCuentaInvalida)));
                var r = control.ReservarLogin(ip, "zz\uFEFF");
                if (r.Rechazado) return;   // la clave fija ya estaba bloqueada en la BD de pruebas
                Assert.Equal(1, Filas("clave = @c AND (fallidos = @f OR bloqueadoHastaUtc IS NOT NULL)",
                    DbHelper.Param("@c", ControlIntentosLogin.ClaveCuentaInvalida), DbHelper.Param("@f", antes + 1)));
                Assert.Equal(1, Filas("clave = @c", DbHelper.Param("@c", ControlIntentosLogin.ClaveIp(ip))));
            }
        }

        [FactBD]
        public void RegistrarAcceso_DejaLaFilaSinSesion()
        {
            using (new TransaccionDePrueba())
            {
                string marca = "zz_acceso_" + Guid.NewGuid().ToString("N");
                AuditoriaHelper.RegistrarAcceso(AuditoriaAccesoLogin.AccionFallido, -987, "zz_cuenta", "Prueba " + marca);
                AuditoriaHelper.RegistrarAcceso(AuditoriaAccesoLogin.AccionBloqueado, null, null, "Prueba " + marca);

                var dt = DbHelper.ConsultarTabla(@"
                    SELECT Accion, IdUsuario, NombreUsuario, TablaAfectada, IdRegistroAfectado
                    FROM AuditoriaLog WHERE Descripcion = @d ORDER BY IdAuditoria",
                    DbHelper.Param("@d", "Prueba " + marca));
                Assert.Equal(2, dt.Rows.Count);
                Assert.Equal("LOGIN_FALLIDO", dt.Rows[0]["Accion"]);
                Assert.Equal(-987, Convert.ToInt32(dt.Rows[0]["IdUsuario"]));
                Assert.Equal("zz_cuenta", dt.Rows[0]["NombreUsuario"]);
                Assert.Equal("Usuarios", dt.Rows[0]["TablaAfectada"]);
                Assert.Equal("-987", dt.Rows[0]["IdRegistroAfectado"]);
                Assert.Equal("LOGIN_BLOQUEADO", dt.Rows[1]["Accion"]);
                Assert.Equal(DBNull.Value, dt.Rows[1]["IdUsuario"]);
                Assert.Equal(AuditoriaHelper.NombreAccesoDesconocido, dt.Rows[1]["NombreUsuario"]);
            }
        }

        /// <summary>Respaldo que nunca tiene nada: obliga a leer de la BD.</summary>
        private sealed class RespaldoVacio : IAlmacenIntentosLogin
        {
            public IntentosLogin Leer(string clave) => null;
            public void Guardar(string clave, IntentosLogin intentos, DateTime venceUtc) { }
            public void Quitar(string clave) { }
        }
    }
}
