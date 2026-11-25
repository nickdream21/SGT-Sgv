using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.SessionState;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-10: cada línea del log lleva el id de la petición y el usuario de la sesión (<c>[3f9a0c1e] u12</c>), sin caerse
    /// fuera de una petición, y <see cref="MensajeErrorHelper.ParaUsuario(Exception,string,object[])"/> registra una sola
    /// vez: el error técnico como ERR y el de negocio como INF. Las pruebas cambian <c>Log.Logger</c> por un destino en
    /// memoria (y lo devuelven al terminar) y solo cuentan los eventos con su propio id de petición: otras clases de
    /// pruebas que corren en paralelo pueden registrar a la vez.
    /// </summary>
    [Collection(LogSGVContextoTests.ColeccionLog)]
    public class LogSGVContextoTests
    {
        /// <summary>Las clases que cambian <c>Log.Logger</c> (global) van en esta colección: no corren a la vez.</summary>
        internal const string ColeccionLog = "Log.Logger global";

        private sealed class DestinoMemoria : ILogEventSink
        {
            public readonly ConcurrentQueue<LogEvent> Eventos = new ConcurrentQueue<LogEvent>();
            public void Emit(LogEvent logEvent) => Eventos.Enqueue(logEvent);
        }

        /// <summary>Petición falsa con id en Items y, si se pide, sesión InProc con ese usuario.</summary>
        private sealed class PeticionFalsa : IDisposable
        {
            public string Id { get; } = LogSGV.NuevoIdPeticion();

            public PeticionFalsa(int? idUsuario, bool conId = true)
            {
                var ctx = new HttpContext(new HttpRequest("", "http://localhost/Views/Inicio.aspx", ""),
                    new HttpResponse(new StringWriter()));
                if (conId) ctx.Items[LogSGV.ClaveIdPeticion] = Id;
                if (idUsuario.HasValue)
                {
                    var contenedor = new HttpSessionStateContainer(
                        "prueba", new SessionStateItemCollection(), new HttpStaticObjectsCollection(),
                        30, true, HttpCookieMode.UseCookies, SessionStateMode.InProc, false);
                    SessionStateUtility.AddHttpSessionStateToContext(ctx, contenedor);
                    SesionHelper.Iniciar(ctx.Session, idUsuario.Value, "ADMIN", "Prueba");
                }
                HttpContext.Current = ctx;
            }

            public void Dispose() => HttpContext.Current = null;
        }

        /// <summary>Corre <paramref name="accion"/> con LogSGV escribiendo en memoria; devuelve las líneas de esa petición.</summary>
        private static List<string> Capturar(string idPeticion, Action accion)
        {
            var destino = new DestinoMemoria();
            ILogger anterior = Log.Logger;
            Log.Logger = LogSGV.ConfiguracionBase().WriteTo.Sink(destino).CreateLogger();
            try { accion(); }
            finally { Log.Logger = anterior; }

            var formato = new MessageTemplateTextFormatter(LogSGV.Plantilla, CultureInfo.InvariantCulture);
            return destino.Eventos
                .Where(e => e.Properties.TryGetValue(ContextoPeticionEnriquecedor.PropiedadPeticion, out var p)
                            && ((p as ScalarValue)?.Value as string) == idPeticion)
                .Select(e => { var w = new StringWriter(); formato.Format(e, w); return w.ToString(); })
                .ToList();
        }

        private static LogEvent EventoVacio() => new LogEvent(DateTimeOffset.Now, LogEventLevel.Information, null,
            new MessageTemplateParser().Parse("x"), Enumerable.Empty<LogEventProperty>());

        private static string Propiedad(LogEvent e, string nombre) => (string)((ScalarValue)e.Properties[nombre]).Value;

        private sealed class FabricaSimple : ILogEventPropertyFactory
        {
            public LogEventProperty CreateProperty(string name, object value, bool destructureObjects = false) =>
                new LogEventProperty(name, new ScalarValue(value));
        }

        [Fact]
        public void ErrorTecnico_DejaUnaSolaLineaErrConIdYUsuario()
        {
            string texto = null;
            List<string> lineas;
            using (var p = new PeticionFalsa(7))
                lineas = Capturar(p.Id, () =>
                    texto = MensajeErrorHelper.ParaUsuario(new InvalidOperationException("detalle técnico"), "Error al anular el lote {Lote}", 15));

            Assert.Equal(MensajeError.Generico, texto);
            string linea = Assert.Single(lineas);
            Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \[ERR\] \[[0-9a-f]{8}\] u7 Error al anular el lote 15", linea);
            Assert.Contains("detalle técnico", linea); // la excepción va en la línea siguiente
        }

        [Fact]
        public void ErrorDeNegocio_NoDejaLineasErr_SoloUnaInfoConElAviso()
        {
            string texto = null;
            List<string> lineas;
            using (var p = new PeticionFalsa(7))
                lineas = Capturar(p.Id, () =>
                    texto = MensajeErrorHelper.ParaUsuario(new ErrorNegocioException("El lote ya está anulado"), "Error al anular el lote {Lote}", 15));

            Assert.Equal("El lote ya está anulado", texto);
            Assert.DoesNotContain(lineas, l => l.Contains("[ERR]"));
            string linea = Assert.Single(lineas);
            Assert.Contains("[INF] [", linea);
            Assert.Contains("u7 Error al anular el lote 15 (aviso de negocio al usuario: El lote ya está anulado)", linea);
        }

        [Fact]
        public void ErrorDeNegocioConCausa_DejaUnaAdvertenciaConLaPila_NuncaErr()
        {
            Exception causa;
            try { throw new InvalidOperationException("Skia no cargó"); }
            catch (InvalidOperationException e) { causa = e; }
            string texto = null;
            List<string> lineas;
            using (var p = new PeticionFalsa(7))
                lineas = Capturar(p.Id, () => texto = MensajeErrorHelper.ParaUsuario(
                    new ErrorNegocioException("No se pudo generar el PDF.", causa), "Error al preparar el PDF de la orden {IdOrden}", 15));

            Assert.Equal("No se pudo generar el PDF.", texto);
            Assert.DoesNotContain(lineas, l => l.Contains("[ERR]"));
            string linea = Assert.Single(lineas);
            Assert.Contains("[WRN] [", linea);
            Assert.Contains("u7 Error al preparar el PDF de la orden 15 (aviso de negocio al usuario: No se pudo generar el PDF.)", linea);
            Assert.Contains("Skia no cargó", linea);              // la causa interna
            Assert.Contains(nameof(ErrorDeNegocioConCausa_DejaUnaAdvertenciaConLaPila_NuncaErr), linea); // y su pila
        }

        [Fact]
        public void ParaUsuario_SinValores_UsaLaPlantillaTalCual()
        {
            List<string> lineas;
            using (var p = new PeticionFalsa(3))
                lineas = Capturar(p.Id, () => MensajeErrorHelper.ParaUsuario(new Exception("x"), "Error al cargar clientes"));
            Assert.Contains("[ERR]", Assert.Single(lineas));
            Assert.Contains("u3 Error al cargar clientes", lineas[0]);
        }

        [Fact]
        public void Linea_SinSesion_LlevaElIdYUsuarioGuion()
        {
            List<string> lineas;
            using (var p = new PeticionFalsa(null))
                lineas = Capturar(p.Id, () => LogSGV.Info("Sin sesión {Ruta}", "/Salud.ashx"));
            Assert.Matches(@"\[INF\] \[[0-9a-f]{8}\] u- Sin sesión /Salud\.ashx",Assert.Single(lineas));
        }

        [Fact]
        public void Enriquecedor_SinHttpContext_NoLanzaYPoneGuiones()
        {
            HttpContext.Current = null;
            var e = EventoVacio();
            new ContextoPeticionEnriquecedor().Enrich(e, new FabricaSimple());
            Assert.Equal("--------", Propiedad(e, ContextoPeticionEnriquecedor.PropiedadPeticion));
            Assert.Equal("-", Propiedad(e, ContextoPeticionEnriquecedor.PropiedadUsuario));
        }

        [Fact]
        public void Enriquecedor_EnUnHiloDeFondo_NoLanzaYPoneGuiones()
        {
            LogEvent e = EventoVacio();
            Exception error = null;
            var hilo = new System.Threading.Thread(() =>
            {
                try { new ContextoPeticionEnriquecedor().Enrich(e, new FabricaSimple()); }
                catch (Exception ex) { error = ex; }
            });
            hilo.Start();
            hilo.Join();
            Assert.Null(error);
            Assert.Equal("--------", Propiedad(e, ContextoPeticionEnriquecedor.PropiedadPeticion));
            Assert.Equal("-", Propiedad(e, ContextoPeticionEnriquecedor.PropiedadUsuario));
        }

        [Fact]
        public void Enriquecedor_ContextoSinIdNiSesion_PoneGuiones()
        {
            using (new PeticionFalsa(null, conId: false))
            {
                var e = EventoVacio();
                new ContextoPeticionEnriquecedor().Enrich(e, new FabricaSimple());
                Assert.Equal("--------", Propiedad(e, ContextoPeticionEnriquecedor.PropiedadPeticion));
                Assert.Equal("-", Propiedad(e, ContextoPeticionEnriquecedor.PropiedadUsuario));
            }
        }

        [Fact]
        public void Enriquecedor_NoPisaUnIdUsuarioDelMensaje()
        {
            // "usuario {IdUsuario}" en un mensaje puede ser otro usuario (el afectado): la columna u{...} sigue siendo la sesión.
            List<string> lineas;
            using (var p = new PeticionFalsa(7))
                lineas = Capturar(p.Id, () => LogSGV.Info("Contraseña restablecida del usuario {IdUsuario}", 99));
            Assert.Contains("u7 Contraseña restablecida del usuario 99", Assert.Single(lineas));
        }

        [Fact]
        public void NuevoIdPeticion_OchoHexadecimales()
        {
            var ids = Enumerable.Range(0, 50).Select(_ => LogSGV.NuevoIdPeticion()).ToList();
            Assert.All(ids, id => Assert.Matches("^[0-9a-f]{8}$", id));
            Assert.True(ids.Distinct().Count() > 45);
        }

        [Fact]
        public void Plantilla_LlevaPeticionYUsuarioAntesDelMensaje()
        {
            Assert.Contains("[{Level:u3}] [{SgvPeticion}] u{SgvUsuario} {Message:lj}", LogSGV.Plantilla);
        }

        [Fact]
        public void Global_PoneElIdDePeticionEnBeginRequest()
        {
            string global = File.ReadAllText(Path.Combine(FuentesWeb.RaizWeb(), "Global.asax.cs"));
            var metodo = Regex.Match(global, @"void Application_BeginRequest\(object sender, EventArgs e\)\s*\{(?<c>.*?)\n        \}",
                RegexOptions.Singleline);
            Assert.True(metodo.Success, "Falta Application_BeginRequest en Global.asax.cs");
            Assert.Contains("Context.Items[LogSGV.ClaveIdPeticion] = LogSGV.NuevoIdPeticion();", metodo.Groups["c"].Value);
            // El encabezado va al enviar las cabeceras: sobrevive al Response.ClearHeaders() de las descargas.
            var envio = Regex.Match(global, @"void Application_PreSendRequestHeaders\(object sender, EventArgs e\)\s*\{(?<c>.*?)\n        \}",
                RegexOptions.Singleline);
            Assert.True(envio.Success, "Falta Application_PreSendRequestHeaders en Global.asax.cs");
            Assert.Contains("Response.Headers.Set(\"X-SGV-Peticion\", id);", envio.Groups["c"].Value);
        }
    }
}
