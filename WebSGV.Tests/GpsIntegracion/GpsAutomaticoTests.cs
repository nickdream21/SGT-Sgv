using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSGV.Services.Exportacion;
using Xunit;
using Manejador = WebSGV.Views.Exportacion.GpsAutomatico;

namespace WebSGV.Tests.GpsIntegracion
{
    /// <summary>
    /// E6-9: la decisión de GpsAutomatico.ashx sin HttpContext (<see cref="Manejador.Atender"/>), con un motor de
    /// prueba (<see cref="EscenarioMotorGps"/>). La clave es ficticia: nunca se lee la de appSettings.Secrets.config.
    /// </summary>
    public class GpsAutomaticoTests
    {
        private const string ClaveFicticia = "clave-de-prueba-ficticia-0001";

        private static Manejador.SolicitudGps DelCron(string claveRecibida, string claveConfigurada = ClaveFicticia) =>
            new Manejador.SolicitudGps { Metodo = "GET", ClaveConfigurada = claveConfigurada, ClaveRecibida = claveRecibida };

        /// <summary>El JSON que escribiría ProcessRequest, leído sin convertir las fechas (se comparan como texto).</summary>
        private static JObject Cuerpo(Manejador.RespuestaGps r) =>
            JsonConvert.DeserializeObject<JObject>(JsonConvert.SerializeObject(r.Cuerpo),
                new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });

        private static Func<DateTime?> SinConsulta => () => throw new InvalidOperationException("no debía consultar la BD");

        // ===== El programador externo (clave) =====

        [Theory]
        [InlineData(null, ClaveFicticia)]                         // sin encabezado
        [InlineData("", ClaveFicticia)]
        [InlineData("otra-clave", ClaveFicticia)]
        [InlineData("CLAVE-DE-PRUEBA-FICTICIA-0001", ClaveFicticia)] // distingue mayúsculas
        [InlineData(ClaveFicticia + " ", ClaveFicticia)]
        [InlineData("clave-de-prueba-ficticia-000", ClaveFicticia)]  // un carácter menos
        [InlineData(ClaveFicticia, null)]                          // el sitio sin clave configurada
        [InlineData(ClaveFicticia, "")]
        [InlineData("   ", "   ")]
        public void SinClaveValida_403_YNoIniciaNada(string recibida, string configurada)
        {
            var e = new EscenarioMotorGps();
            var r = Manejador.Atender(DelCron(recibida, configurada), e.Motor, SinConsulta);

            Assert.Equal(403, r.Codigo);
            Assert.True(r.ClaveInvalida); // ProcessRequest registra la IP
            Assert.Equal("Clave inválida", (string)Cuerpo(r)["error"]);
            Assert.Empty(e.Cola);
            Assert.False(e.Motor.EnCurso);
            Assert.Null(e.Motor.UltimoInicio);
            Assert.Null(e.Motor.UltimoInicioProgramado);
        }

        [Fact]
        public void ConLaClave_Inicia_202()
        {
            var e = new EscenarioMotorGps();
            var r = Manejador.Atender(DelCron(ClaveFicticia), e.Motor, SinConsulta);

            Assert.Equal(202, r.Codigo);
            Assert.False(r.ClaveInvalida);
            Assert.Null(r.Allow);
            var c = Cuerpo(r);
            Assert.True((bool)c["iniciada"]);
            Assert.Equal(JTokenType.Null, c["motivo"].Type);
            Assert.Equal(e.Motor.UltimoInicio.Value.ToString("yyyy-MM-ddTHH:mm:ss"), (string)c["ultimoInicio"]);
            Assert.Single(e.Cola);
            Assert.Equal(e.Reloj.AhoraUtc, e.Motor.UltimoInicioProgramado); // cuenta como corrida del cron
        }

        [Fact]
        public void ConLaClave_EnCurso_200_ConSuMotivo()
        {
            var e = new EscenarioMotorGps();
            Manejador.Atender(DelCron(ClaveFicticia), e.Motor, SinConsulta);
            e.Reloj.Esperar(TimeSpan.FromMinutes(6));

            var r = Manejador.Atender(DelCron(ClaveFicticia), e.Motor, SinConsulta);
            Assert.Equal(200, r.Codigo);
            Assert.False((bool)Cuerpo(r)["iniciada"]);
            Assert.Equal("Ya hay una actualización en curso", (string)Cuerpo(r)["motivo"]);
            Assert.Single(e.Cola);
        }

        [Fact]
        public void ConLaClave_HacePoco_200_ConSuMotivoYElResumenAnterior()
        {
            var e = new EscenarioMotorGps();
            Manejador.Atender(DelCron(ClaveFicticia), e.Motor, SinConsulta);
            e.CorrerCola();
            e.Reloj.Esperar(TimeSpan.FromMinutes(2));

            var r = Manejador.Atender(DelCron(ClaveFicticia), e.Motor, SinConsulta);
            var c = Cuerpo(r);
            Assert.Equal(200, r.Codigo);
            Assert.False((bool)c["iniciada"]);
            Assert.Equal("La anterior fue hace menos de 5 minutos", (string)c["motivo"]);
            Assert.Equal(e.Motor.UltimoResumen, (string)c["ultimoResumen"]);
            Assert.Equal(e.Motor.UltimoFin.Value.ToString("yyyy-MM-ddTHH:mm:ss"), (string)c["ultimoFin"]);
            Assert.Empty(e.Cola);
        }

        [Theory]
        [InlineData(null, null, true)]
        [InlineData(ClaveFicticia, ClaveFicticia, true)]
        [InlineData(ClaveFicticia, "otra", false)]
        [InlineData(ClaveFicticia, null, false)]
        [InlineData(null, ClaveFicticia, false)]
        [InlineData(" ", " ", false)]
        public void ClaveValida(string configurada, string recibida, bool esperadoSiConfigurada)
        {
            bool esperado = configurada != null && esperadoSiConfigurada;
            Assert.Equal(esperado, Manejador.ClaveValida(configurada, recibida));
        }

        // ===== ?estado=1 (el tablero abierto) =====

        [Fact]
        public void Estado_SinSesion_403_SinConsultarLaBd()
        {
            var e = new EscenarioMotorGps();
            var r = Manejador.Atender(new Manejador.SolicitudGps { Estado = "1", ClaveRecibida = ClaveFicticia, ClaveConfigurada = ClaveFicticia }, e.Motor, SinConsulta);
            Assert.Equal(403, r.Codigo);
            Assert.False(r.ClaveInvalida);
            Assert.Equal("Sesión vencida", (string)Cuerpo(r)["error"]);
            Assert.Empty(e.Cola); // con la clave tampoco inicia: ?estado=1 es solo lectura
        }

        [Fact]
        public void Estado_SinPermiso_403()
        {
            var e = new EscenarioMotorGps();
            var r = Manejador.Atender(new Manejador.SolicitudGps { Estado = "1", ConSesion = true, Rol = "CONDUCTOR" }, e.Motor, SinConsulta);
            Assert.Equal(403, r.Codigo);
            Assert.Equal("Sin permiso", (string)Cuerpo(r)["error"]);
        }

        [Fact]
        public void Estado_ConPermiso_DevuelveLaUltimaConsultaYElMotor()
        {
            var e = new EscenarioMotorGps();
            e.Motor.Iniciar(aPedido: false);
            e.CorrerCola();
            var ultima = new DateTime(2026, 11, 20, 9, 30, 15);

            var r = Manejador.Atender(new Manejador.SolicitudGps { Estado = "1", ConSesion = true, Rol = "ADMIN" }, e.Motor, () => ultima);
            var c = Cuerpo(r);
            Assert.Equal(200, r.Codigo);
            Assert.Equal("2026-11-20T09:30:15", (string)c["ultima"]);
            Assert.False((bool)c["enCurso"]);
            Assert.Equal(e.Motor.UltimoMensaje, (string)c["mensaje"]);
            Assert.False((bool)c["conFallas"]);
            Assert.Equal(e.Motor.UltimoFin.Value.ToString("yyyy-MM-ddTHH:mm:ss"), (string)c["ultimoFin"]);

            var sinConsultas = Cuerpo(Manejador.Atender(new Manejador.SolicitudGps { Estado = "1", ConSesion = true, Rol = "ADMIN" }, e.Motor, () => null));
            Assert.Equal(JTokenType.Null, sinConsultas["ultima"].Type);
        }

        // ===== ?iniciar=1 (botón "Actualizar GPS") =====

        private static Manejador.SolicitudGps DelBoton(string metodo = "POST", string contentType = "application/json; charset=utf-8",
            bool conSesion = true, string rol = "ADMINISTRADOR DE TRANSPORTE") =>
            new Manejador.SolicitudGps { Iniciar = "1", Metodo = metodo, ContentType = contentType, ConSesion = conSesion, Rol = rol };

        [Fact]
        public void Boton_NoPost_405_ConAllow()
        {
            var e = new EscenarioMotorGps();
            var r = Manejador.Atender(DelBoton(metodo: "GET"), e.Motor, SinConsulta);
            Assert.Equal(405, r.Codigo);
            Assert.Equal("POST", r.Allow);
            Assert.Empty(e.Cola);
        }

        [Theory]
        [InlineData(false, "ADMIN", "Sesión vencida")]
        [InlineData(true, "CONTABILIDAD", "Sin permiso")]
        public void Boton_SinSesionOSinPermiso_403(bool conSesion, string rol, string error)
        {
            var e = new EscenarioMotorGps();
            var r = Manejador.Atender(DelBoton(conSesion: conSesion, rol: rol), e.Motor, SinConsulta);
            Assert.Equal(403, r.Codigo);
            Assert.Equal(error, (string)Cuerpo(r)["error"]);
            Assert.Empty(e.Cola);
        }

        [Fact]
        public void Boton_SinJson_415()
        {
            var e = new EscenarioMotorGps();
            var r = Manejador.Atender(DelBoton(contentType: "text/plain"), e.Motor, SinConsulta);
            Assert.Equal(415, r.Codigo);
            Assert.Empty(e.Cola);
        }

        [Fact]
        public void Boton_Inicia_APedido_SinClave_YAunqueElCronCorrioHacePoco()
        {
            var e = new EscenarioMotorGps();
            Manejador.Atender(DelCron(ClaveFicticia), e.Motor, SinConsulta);
            e.CorrerCola();
            DateTime programada = e.Motor.UltimoInicioProgramado.Value;

            var r = Manejador.Atender(DelBoton(), e.Motor, SinConsulta);
            var c = Cuerpo(r);
            Assert.Equal(200, r.Codigo);
            Assert.True((bool)c["iniciada"]);
            Assert.True((bool)c["enCurso"]);
            Assert.Equal(programada, e.Motor.UltimoInicioProgramado); // no cuenta como corrida del cron

            var otra = Cuerpo(Manejador.Atender(DelBoton(), e.Motor, SinConsulta));
            Assert.False((bool)otra["iniciada"]); // ya hay una en curso
            Assert.Single(e.Cola);
        }
    }
}
