using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.SessionState;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-11: medición de peticiones por ruta (<see cref="MedicionPeticiones"/>), el aviso de petición lenta, el resumen
    /// del día y la línea ERR de un 500 de un <c>[WebMethod]</c> (<see cref="ErrorMetodoPagina"/>). Los relojes se
    /// inyectan; el log se captura en memoria con el id de petición propio (otras clases registran en paralelo).
    /// </summary>
    [Collection(LogSGVContextoTests.ColeccionLog)]
    public class MedicionPeticionesTests
    {
        private static readonly DateTime Dia1 = new DateTime(2026, 11, 24, 8, 30, 0);

        private sealed class Reloj
        {
            public DateTime Ahora = Dia1;
        }

        private static MedicionPeticiones Nueva(Reloj reloj, int umbral = 3000) => new MedicionPeticiones(() => reloj.Ahora, umbral);

        private static FilaMedicion Fila(ResumenDia r, string ruta) => r.Rutas.Single(f => f.Ruta == ruta);

        // ── Ruta y exclusiones ───────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("~/Views/Inicio.aspx", "", "~/views/inicio.aspx")]
        [InlineData("~/Views/Auditoria.aspx", "/ObtenerDetalle", "~/views/auditoria.aspx/obtenerdetalle")]
        [InlineData("~/Views/Auditoria.aspx", "/ObtenerDetalle/", "~/views/auditoria.aspx/obtenerdetalle")]
        [InlineData("~/Views/RestablecerContrasena.aspx?token=abc123", "", "~/views/restablecercontrasena.aspx")]
        [InlineData("~/Views/X.aspx", "/Metodo?id=5", "~/views/x.aspx/metodo")]
        [InlineData(null, null, "")]
        public void Ruta_EnMinusculasConElMetodoYSinQuerystring(string archivo, string pathInfo, string esperada)
        {
            Assert.Equal(esperada, MedicionPeticiones.Ruta(archivo, pathInfo));
        }

        [Theory]
        [InlineData("~/Content/paginas/Inicio.css")]
        [InlineData("~/Scripts/paginas/SiteMaster.js")]
        [InlineData("~/Content/img/logo.PNG")]
        [InlineData("~/Content/img/foto.jpg")]
        [InlineData("~/Content/img/foto.webp")]
        [InlineData("~/Content/img/icono.svg")]
        [InlineData("~/favicon.ico")]
        [InlineData("~/fonts/x.woff")]
        [InlineData("~/fonts/x.woff2")]
        [InlineData("~/Scripts/bootstrap.min.js.map")]
        [InlineData("~/ScriptResource.axd")]
        [InlineData("~/WebResource.axd")]
        [InlineData("~/Salud.ashx")]
        [InlineData("~/salud.ashx?gps=1")]
        [InlineData("~/CspInforme.ashx")]
        [InlineData("")]
        [InlineData(null)]
        public void SeMide_NoEstaticosNiSaludNiCsp(string ruta)
        {
            Assert.False(MedicionPeticiones.SeMide(ruta));
        }

        [Theory]
        [InlineData("~/Views/Inicio.aspx")]
        [InlineData("~/Views/Login.aspx")]
        [InlineData("~/GpsAutomatico.ashx")]
        [InlineData("~/Views/DescargarDocumento.aspx")]
        [InlineData("~/")]
        public void SeMide_PaginasYManejadores(string ruta)
        {
            Assert.True(MedicionPeticiones.SeMide(ruta));
        }

        // ── Acumulador ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Registrar_SumaCantidadTiempoMaximoSesionLentasY5xx()
        {
            var m = Nueva(new Reloj());
            m.Registrar("~/views/a.aspx", 100, 5, false);
            m.Registrar("~/views/a.aspx", 3500, 40, true);
            m.Registrar("~/views/a.aspx", 900, 0, false);
            m.Registrar("~/views/b.aspx", 10, 0, false);

            ResumenDia r = m.Instantanea();
            FilaMedicion a = Fila(r, "~/views/a.aspx");
            Assert.Equal(3, a.Cantidad);
            Assert.Equal(4500, a.MsTotal);
            Assert.Equal(3500, a.MsMaximo);
            Assert.Equal(45, a.MsSesion);
            Assert.Equal(1, a.Lentas);
            Assert.Equal(1, a.Errores5xx);
            Assert.Equal(4, r.Peticiones);
            Assert.Equal(1, r.Lentas);
            Assert.Equal(1, r.Errores5xx);
        }

        [Theory]
        [InlineData(3000, false)] // "por encima" del umbral: igual no es lenta
        [InlineData(3001, true)]
        [InlineData(2999, false)]
        public void Registrar_LentaSoloPorEncimaDelUmbral(long ms, bool lenta)
        {
            var m = Nueva(new Reloj(), 3000);
            Assert.Equal(lenta, m.Registrar("~/views/a.aspx", ms, 0, false).EsLenta);
        }

        [Theory]
        [InlineData(null, 3000)]
        [InlineData("", 3000)]
        [InlineData("abc", 3000)]
        [InlineData("0", 3000)]
        [InlineData("-5", 3000)]
        [InlineData("4500", 4500)]
        [InlineData(" 1500 ", 1500)]
        public void LeerUmbral_EnteroPositivoOTresMil(string valor, int esperado)
        {
            Assert.Equal(esperado, MedicionPeticiones.LeerUmbral(valor));
        }

        [Fact]
        public void Registrar_EnParalelo_NoPierdeNingunaPeticion()
        {
            var m = Nueva(new Reloj());
            string[] rutas = { "~/views/a.aspx", "~/views/b.aspx", "~/views/c.aspx/m", "~/views/d.aspx" };
            const int n = 20000;
            Parallel.For(0, n, i => m.Registrar(rutas[i % rutas.Length], i % 7000, 1, i % 100 == 0));

            ResumenDia r = m.Instantanea();
            Assert.Equal(n, r.Peticiones);
            Assert.Equal(n / 100, r.Errores5xx);
            Assert.Equal(Enumerable.Range(0, n).Sum(i => (long)(i % 7000)), r.Rutas.Sum(f => f.MsTotal));
            Assert.Equal(Enumerable.Range(0, n).Count(i => i % 7000 > 3000), r.Lentas);
            Assert.Equal(n, r.Rutas.Sum(f => f.MsSesion));
            Assert.Equal(6999, r.Rutas.Max(f => f.MsMaximo));
            Assert.Equal(4, r.Rutas.Count);
        }

        [Fact]
        public void Registrar_ConDemasiadasRutas_ElRestoVaAOtras()
        {
            var m = Nueva(new Reloj());
            for (int i = 0; i < MedicionPeticiones.MaxRutas + 100; i++)
                m.Registrar("~/wp-" + i + ".php", 1, 0, false);

            ResumenDia r = m.Instantanea();
            Assert.Equal(MedicionPeticiones.MaxRutas + 1, r.Rutas.Count);
            Assert.Equal(100, Fila(r, MedicionPeticiones.RutaOtras).Cantidad);
            Assert.Equal(MedicionPeticiones.MaxRutas + 100, r.Peticiones);
        }

        [Fact]
        public void CambioDeDia_DevuelveElResumenDelAnteriorYReinicia()
        {
            var reloj = new Reloj();
            var m = Nueva(reloj);
            Assert.Null(m.Registrar("~/views/a.aspx", 100, 0, false).ResumenDiaAnterior);
            reloj.Ahora = Dia1.Date.AddHours(23).AddMinutes(59);
            Assert.Null(m.Registrar("~/views/a.aspx", 4000, 0, true).ResumenDiaAnterior);

            reloj.Ahora = Dia1.Date.AddDays(1).AddMinutes(1); // 00:01 del día siguiente (hora de Perú)
            ResultadoMedicion r = m.Registrar("~/views/b.aspx", 50, 0, false);

            Assert.NotNull(r.ResumenDiaAnterior);
            Assert.Equal(Dia1.Date, r.ResumenDiaAnterior.Dia);
            Assert.Equal(2, r.ResumenDiaAnterior.Peticiones);
            Assert.Equal(1, r.ResumenDiaAnterior.Lentas);
            Assert.Equal(1, r.ResumenDiaAnterior.Errores5xx);
            Assert.DoesNotContain(r.ResumenDiaAnterior.Rutas, f => f.Ruta == "~/views/b.aspx");

            ResumenDia hoy = m.Instantanea();
            Assert.Equal(Dia1.Date.AddDays(1), hoy.Dia);
            Assert.Equal(1, Assert.Single(hoy.Rutas).Cantidad);
            Assert.Null(m.Registrar("~/views/b.aspx", 50, 0, false).ResumenDiaAnterior); // una sola vez
        }

        [Fact]
        public void CambioDeDia_EnParalelo_ElResumenSaleUnaSolaVez()
        {
            var reloj = new Reloj();
            var m = Nueva(reloj);
            m.Registrar("~/views/a.aspx", 1, 0, false);
            reloj.Ahora = Dia1.AddDays(1);
            var resumenes = new ConcurrentBag<ResumenDia>();
            Parallel.For(0, 2000, _ =>
            {
                var r = m.Registrar("~/views/a.aspx", 1, 0, false);
                if (r.ResumenDiaAnterior != null) resumenes.Add(r.ResumenDiaAnterior);
            });
            Assert.Equal(1, Assert.Single(resumenes).Peticiones);
            Assert.Equal(2000, m.Instantanea().Peticiones);
        }

        [Fact]
        public void Cerrar_DevuelveLoAcumuladoYEmpiezaVacio()
        {
            var m = Nueva(new Reloj());
            m.Registrar("~/views/a.aspx", 1, 0, false);
            Assert.Equal(1, m.Cerrar().Peticiones);
            Assert.Equal(0, m.Instantanea().Peticiones);
        }

        [Fact]
        public void Texto_TotalesYTopPorTiempoLentasY5xx()
        {
            var m = Nueva(new Reloj());
            m.Registrar("~/views/lenta.aspx", 5000, 120, false);
            m.Registrar("~/views/lenta.aspx", 4000, 0, false);
            m.Registrar("~/views/falla.aspx/metodo", 30, 2, true);
            m.Registrar("~/views/rapida.aspx", 20, 0, false);

            string texto = m.Instantanea().Texto();
            Assert.StartsWith("4 peticiones desde las 08:30, 2 lentas, 1 con 5xx; más tiempo: ~/views/lenta.aspx 2 pet. 9000 ms (máx 5000 ms, sesión 120 ms), ~/views/falla.aspx/metodo 1 pet. 30 ms", texto);
            Assert.Contains("; lentas: ~/views/lenta.aspx 2", texto);
            Assert.EndsWith("; 5xx: ~/views/falla.aspx/metodo 1", texto);
        }

        [Fact]
        public void Texto_SinLentasNi5xx_NoPoneEsasListas()
        {
            var m = Nueva(new Reloj());
            m.Registrar("~/views/a.aspx", 10, 0, false);
            string texto = m.Instantanea().Texto();
            Assert.DoesNotContain("lentas:", texto);
            Assert.DoesNotContain("5xx:", texto);
        }

        [Theory]
        [InlineData(200, false, false)]
        [InlineData(500, false, true)]
        [InlineData(503, false, true)]
        [InlineData(302, true, true)]   // error no controlado redirigido a Error.aspx (customErrors en Release)
        public void EsError5xx_PorCodigoOPorErrorNoControlado(int codigo, bool conError, bool esperado)
        {
            Assert.Equal(esperado, MedicionPeticiones.EsError5xx(codigo, conError ? new InvalidOperationException("x") : null));
        }

        [Fact]
        public void EsError5xx_UnNoEncontradoNoCuenta()
        {
            Assert.False(MedicionPeticiones.EsError5xx(404, new HttpException(404, "no está")));
            Assert.False(MedicionPeticiones.EsError5xx(302, new HttpUnhandledException("x", new HttpException(400, "mal"))));
            Assert.True(MedicionPeticiones.EsError5xx(302, new HttpUnhandledException("x", new InvalidOperationException("y"))));
        }

        // ── Log ─────────────────────────────────────────────────────────────────────────────

        private sealed class DestinoMemoria : ILogEventSink
        {
            public readonly ConcurrentQueue<LogEvent> Eventos = new ConcurrentQueue<LogEvent>();
            public void Emit(LogEvent logEvent) => Eventos.Enqueue(logEvent);
        }

        /// <summary>Contexto falso con id de petición y, si se pide, sesión con ese usuario.</summary>
        private static HttpContext Contexto(string id, int? idUsuario = null)
        {
            var ctx = new HttpContext(new HttpRequest("", "http://localhost/Views/Inicio.aspx", ""), new HttpResponse(new StringWriter()));
            ctx.Items[LogSGV.ClaveIdPeticion] = id;
            if (idUsuario.HasValue)
            {
                var contenedor = new HttpSessionStateContainer("prueba", new SessionStateItemCollection(), new HttpStaticObjectsCollection(),
                    30, true, HttpCookieMode.UseCookies, SessionStateMode.InProc, false);
                SessionStateUtility.AddHttpSessionStateToContext(ctx, contenedor);
                SesionHelper.Iniciar(ctx.Session, idUsuario.Value, "ADMIN", "Prueba");
            }
            return ctx;
        }

        private static List<string> Capturar(HttpContext ctx, Action accion)
        {
            string id = (string)ctx.Items[LogSGV.ClaveIdPeticion];
            var destino = new DestinoMemoria();
            ILogger anterior = Log.Logger;
            Log.Logger = LogSGV.ConfiguracionBase().WriteTo.Sink(destino).CreateLogger();
            HttpContext.Current = ctx;
            try { accion(); }
            finally
            {
                Log.Logger = anterior;
                HttpContext.Current = null;
            }
            var formato = new MessageTemplateTextFormatter(LogSGV.Plantilla, CultureInfo.InvariantCulture);
            return destino.Eventos
                .Where(e => e.Properties.TryGetValue(ContextoPeticionEnriquecedor.PropiedadPeticion, out var p)
                            && ((p as ScalarValue)?.Value as string) == id)
                .Select(e => { var w = new StringWriter(); formato.Format(e, w); return w.ToString(); })
                .ToList();
        }

        [Fact]
        public void Procesar_Lenta_DejaUnAvisoConRutaYMs()
        {
            var m = Nueva(new Reloj());
            var lineas = Capturar(Contexto(LogSGV.NuevoIdPeticion(), 7), () =>
            {
                m.Procesar("~/views/a.aspx", 4012, 35, false);
                m.Procesar("~/views/a.aspx", 200, 0, false);
            });
            string linea = Assert.Single(lineas);
            Assert.Matches(@"\[WRN\] \[[0-9a-f]{8}\] u7 Petición lenta ~/views/a\.aspx 4012 ms \(sesión 35 ms\)", linea);
        }

        [Fact]
        public void Procesar_PrimeraDelDiaNuevo_EscribeElResumenDelAnterior()
        {
            var reloj = new Reloj();
            var m = Nueva(reloj);
            m.Registrar("~/views/a.aspx", 100, 0, false);
            reloj.Ahora = Dia1.AddDays(1);
            var lineas = Capturar(Contexto(LogSGV.NuevoIdPeticion()), () => m.Procesar("~/views/b.aspx", 10, 0, false));
            string linea = Assert.Single(lineas);
            Assert.Contains("[INF]", linea);
            Assert.Contains("Resumen del día 2026-11-24: 1 peticiones desde las 08:30, 0 lentas, 0 con 5xx; más tiempo: ~/views/a.aspx 1 pet. 100 ms", linea);
        }

        [Fact]
        public void Enriquecedor_SinSesion_UsaElUsuarioGuardadoAlTenerla()
        {
            // En EndRequest el módulo de sesión ya la quitó del contexto: la línea sigue llevando el usuario.
            var ctx = Contexto(LogSGV.NuevoIdPeticion());
            ctx.Items[LogSGV.ClaveIdUsuario] = 12;
            var lineas = Capturar(ctx, () => LogSGV.Advertencia("Petición lenta {Ruta}", "~/views/a.aspx"));
            Assert.Contains("] u12 Petición lenta", Assert.Single(lineas));
        }

        [Fact]
        public void AlTenerSesion_GuardaElUsuarioNombreRolYLaMarca()
        {
            var ctx = Contexto(LogSGV.NuevoIdPeticion(), 9);
            MedicionPeticiones.AlTenerSesion(ctx);
            Assert.Equal(9, ctx.Items[LogSGV.ClaveIdUsuario]);
            Assert.Equal("Prueba", ctx.Items[MedicionPeticiones.ClaveNombreUsuario]);
            Assert.Equal("ADMIN", ctx.Items[MedicionPeticiones.ClaveRolUsuario]);
            Assert.IsType<long>(ctx.Items[MedicionPeticiones.ClaveFinSesion]);
        }

        [Fact]
        public void AvisoErrores_SinSesion_UsaElUsuarioGuardadoAlTenerla()
        {
            // El correo de un WebMethod sale en EndRequest, ya sin sesión: no debe decir "sin sesión".
            var ctx = Contexto(LogSGV.NuevoIdPeticion(), 9);
            MedicionPeticiones.AlTenerSesion(ctx);
            var datos = new WebSGV.Services.Operacion.DatosErrorAviso();
            WebSGV.Services.Operacion.AvisoErrores.CompletarUsuarioGuardado(datos, ctx.Items);
            Assert.Equal(9, datos.IdUsuario);
            Assert.Equal("Prueba", datos.NombreUsuario);
            Assert.Equal("ADMIN", datos.Rol);
            Assert.Contains("Usuario: 9 - Prueba (ADMIN)", WebSGV.Services.Operacion.AvisoErrores.ArmarCuerpo(datos, null));

            // Con el usuario de la sesión ya puesto (Application_Error) no se toca.
            var conSesion = new WebSGV.Services.Operacion.DatosErrorAviso { IdUsuario = 3, NombreUsuario = "Otro", Rol = "CONTABILIDAD" };
            WebSGV.Services.Operacion.AvisoErrores.CompletarUsuarioGuardado(conSesion, ctx.Items);
            Assert.Equal(3, conSesion.IdUsuario);
            Assert.Equal("Otro", conSesion.NombreUsuario);

            // Sin nada guardado sigue "sin sesión".
            var sinNada = new WebSGV.Services.Operacion.DatosErrorAviso();
            WebSGV.Services.Operacion.AvisoErrores.CompletarUsuarioGuardado(sinNada, new System.Collections.Hashtable());
            Assert.Null(sinNada.IdUsuario);
        }

        [Fact]
        public void LogSGVError_AnotaLaExcepcionRegistrada()
        {
            var ctx = Contexto(LogSGV.NuevoIdPeticion());
            Capturar(ctx, () => LogSGV.Info("nada"));
            Assert.Null(ctx.Items[LogSGV.ClaveExcepcionesRegistradas]);
            var ex = new InvalidOperationException("x");
            Capturar(ctx, () => LogSGV.Error(ex, "falla"));
            Assert.Same(ex, Assert.Single((List<Exception>)ctx.Items[LogSGV.ClaveExcepcionesRegistradas]));
        }

        [Fact]
        public void YaRegistrada_SoloLaMismaExcepcionEnLaCadena()
        {
            var propia = new InvalidOperationException("del método");
            var otra = new TimeoutException("la auditoría se tragó su error");
            var envuelta = new TargetInvocationException(new ErrorNegocioException("aviso", propia));
            Assert.True(ErrorMetodoPagina.YaRegistrada(new[] { propia }, new TargetInvocationException(propia))); // registró y relanzó
            Assert.True(ErrorMetodoPagina.YaRegistrada(new[] { propia }, envuelta));                              // registró y envolvió
            Assert.True(ErrorMetodoPagina.YaRegistrada(new Exception[] { new ApplicationException("x", propia) }, propia));
            Assert.False(ErrorMetodoPagina.YaRegistrada(new Exception[] { otra }, new TargetInvocationException(propia)));
            Assert.False(ErrorMetodoPagina.YaRegistrada(null, propia));
            Assert.False(ErrorMetodoPagina.YaRegistrada(new[] { propia }, null));
        }

        // ── Error de un [WebMethod] ─────────────────────────────────────────────────────────

        [Theory]
        [InlineData("/Views/Auditoria.aspx", "/ObtenerDetalle", true)]
        [InlineData("/Views/Auditoria.aspx", "", false)]
        [InlineData("/Views/Auditoria.aspx", "/", false)]
        [InlineData("/GpsAutomatico.ashx", "/x", false)]
        public void EsMetodoPagina_AspxConMetodo(string archivo, string pathInfo, bool esperado)
        {
            Assert.Equal(esperado, ErrorMetodoPagina.EsMetodoPagina(archivo, pathInfo));
        }

        [Fact]
        public void Decidir_SoloUn500ConJsonErrorDeUnMetodoSinRegistrarAntes()
        {
            var tecnico = new TargetInvocationException(new InvalidOperationException("x")); // como lo deja MethodInfo.Invoke
            Assert.Equal(ErrorMetodoPagina.Decision.Error, ErrorMetodoPagina.Decidir(true, 500, true, false, tecnico));
            Assert.Equal(ErrorMetodoPagina.Decision.Error, ErrorMetodoPagina.Decidir(true, 500, true, false, null));
            Assert.Equal(ErrorMetodoPagina.Decision.AvisoNegocio, ErrorMetodoPagina.Decidir(true, 500, true, false,
                new TargetInvocationException(new ErrorNegocioException("La orden ya está aprobada"))));
            Assert.Equal(ErrorMetodoPagina.Decision.Nada, ErrorMetodoPagina.Decidir(false, 500, true, false, tecnico)); // no es PageMethod
            Assert.Equal(ErrorMetodoPagina.Decision.Nada, ErrorMetodoPagina.Decidir(true, 200, true, false, tecnico));
            Assert.Equal(ErrorMetodoPagina.Decision.Nada, ErrorMetodoPagina.Decidir(true, 401, true, false, tecnico)); // sesión cerrada
            Assert.Equal(ErrorMetodoPagina.Decision.Nada, ErrorMetodoPagina.Decidir(true, 500, false, false, tecnico)); // 500 propio
            Assert.Equal(ErrorMetodoPagina.Decision.Nada, ErrorMetodoPagina.Decidir(true, 500, true, true, tecnico));  // ya registrado
        }

        [Fact]
        public void Decidir_FalloDelFrameworkAntesDelMetodo_EsLlamadaInvalidaSinCorreo()
        {
            // Un bot manda "{" o le falta un parámetro: ASP.NET lanza sin envolver y lo atrapa su propio RestHandler, así
            // que en la pila no hay código de WebSGV ni de una página ASP.*. (Sin lanzarlas aquí: la pila tendría el marco
            // de esta prueba, WebSGV.Tests.*.)
            Exception json = new ArgumentException("Invalid JSON primitive: {.");
            Exception falta = new InvalidOperationException("Llamada al servicio Web no válida. Falta un valor para el parámetro: 'id'.");
            Assert.Equal(ErrorMetodoPagina.Decision.LlamadaInvalida, ErrorMetodoPagina.Decidir(true, 500, true, false, json));
            Assert.Equal(ErrorMetodoPagina.Decision.LlamadaInvalida, ErrorMetodoPagina.Decidir(true, 500, true, false, falta));
            Assert.False(ErrorMetodoPagina.LlegoAEjecutarse(json));
        }

        [Fact]
        public void LlegoAEjecutarse_ConPilaPropiaSinEnvolver()
        {
            // Lanzada desde código de WebSGV (p. ej. un ayudante que el método llamó): llegó a ejecutarse.
            Exception propia = null;
            try { new MedicionPeticiones(null, 1).ToString(); }
            catch (ArgumentNullException e) { propia = e; }
            Assert.NotNull(propia);
            Assert.True(ErrorMetodoPagina.LlegoAEjecutarse(propia));
            Assert.Equal(ErrorMetodoPagina.Decision.Error, ErrorMetodoPagina.Decidir(true, 500, true, false, propia));
        }
        [Fact]
        public void Desenvolver_QuitaLaEnvolturaDeLaReflexion()
        {
            var real = new InvalidOperationException("real");
            Assert.Same(real, ErrorMetodoPagina.Desenvolver(new TargetInvocationException(new TargetInvocationException(real))));
            Assert.Same(real, ErrorMetodoPagina.Desenvolver(real));
            Assert.Null(ErrorMetodoPagina.Desenvolver(null));
        }

        [Fact]
        public void AlLanzarse_EnUnaPeticionDeMetodo_GuardaLaUltimaExcepcion()
        {
            ErrorMetodoPagina.Instalar();
            ErrorMetodoPagina.Instalar(); // dos veces no duplica
            var ctx = Contexto(LogSGV.NuevoIdPeticion());
            ctx.Items[ErrorMetodoPagina.ClaveEsMetodoPagina] = true;
            var otra = Contexto(LogSGV.NuevoIdPeticion()); // petición que no es PageMethod
            try
            {
                HttpContext.Current = ctx;
                try { throw new InvalidOperationException("del método"); }
                catch (InvalidOperationException) { /* lo atrapa el manejador de scripts */ }
                HttpContext.Current = otra;
                try { throw new InvalidOperationException("de otra petición"); }
                catch (InvalidOperationException) { }
            }
            finally { HttpContext.Current = null; }

            Assert.Equal("del método", Assert.IsType<InvalidOperationException>(ctx.Items[ErrorMetodoPagina.ClaveExcepcion]).Message);
            Assert.Null(otra.Items[ErrorMetodoPagina.ClaveExcepcion]);
        }

        [Fact]
        public void Escribir_ErrorDejaUnaLineaErrConLaExcepcion_NegocioUnaInfo()
        {
            Exception ex;
            try { throw new InvalidOperationException("falló el método"); }
            catch (InvalidOperationException e) { ex = e; }

            var lineas = Capturar(Contexto(LogSGV.NuevoIdPeticion(), 4), () =>
                ErrorMetodoPagina.Escribir(ErrorMetodoPagina.Decision.Error, "/Views/Auditoria.aspx/ObtenerDetalle", ex));
            string linea = Assert.Single(lineas);
            Assert.Matches(@"\[ERR\] \[[0-9a-f]{8}\] u4 Error no controlado en /Views/Auditoria\.aspx/ObtenerDetalle \(WebMethod\)", linea);
            Assert.Contains("falló el método", linea);

            lineas = Capturar(Contexto(LogSGV.NuevoIdPeticion(), 4), () =>
                ErrorMetodoPagina.Escribir(ErrorMetodoPagina.Decision.AvisoNegocio, "/Views/X.aspx/M", new ErrorNegocioException("Ya está aprobada")));
            linea = Assert.Single(lineas);
            Assert.Contains("[INF]", linea);
            Assert.Contains("WebMethod /Views/X.aspx/M respondió 500 con un aviso de negocio: Ya está aprobada", linea);

            // Llamada inválida: WRN con la ruta y el tipo, nunca el mensaje (puede traer un trozo del cuerpo) ni la pila.
            lineas = Capturar(Contexto(LogSGV.NuevoIdPeticion()), () =>
                ErrorMetodoPagina.Escribir(ErrorMetodoPagina.Decision.LlamadaInvalida, "/Views/Auditoria.aspx/ObtenerDetalle",
                    new ArgumentException("Invalid JSON primitive: {\"contrasena\":\"x")));
            linea = Assert.Single(lineas);
            Assert.Contains("[WRN] [", linea);
            Assert.Contains("u- Llamada inválida a /Views/Auditoria.aspx/ObtenerDetalle (WebMethod; ASP.NET la rechazó antes de ejecutar el método: ArgumentException)", linea);
            Assert.DoesNotContain("Invalid JSON", linea);
            Assert.DoesNotContain("contrasena", linea);
        }

        // ── Enganche en Global.asax.cs y Web.config ─────────────────────────────────────────

        private static string Cuerpo(string fuente, string metodo)
        {
            var m = Regex.Match(fuente, @"void " + metodo + @"\(object sender, EventArgs e\)\s*\{(?<c>.*?)\n        \}", RegexOptions.Singleline);
            Assert.True(m.Success, "Falta " + metodo + " en Global.asax.cs");
            return m.Groups["c"].Value;
        }

        [Fact]
        public void Global_EnganchaLaMedicionYElErrorDeWebMethod()
        {
            string global = File.ReadAllText(Path.Combine(FuentesWeb.RaizWeb(), "Global.asax.cs"));
            Assert.Contains("ErrorMetodoPagina.Instalar();", Cuerpo(global, "Application_Start"));
            string inicio = Cuerpo(global, "Application_BeginRequest");
            Assert.Contains("MedicionPeticiones.AlIniciar(Context);", inicio);
            Assert.Contains("ErrorMetodoPagina.AlIniciar(Context);", inicio);
            Assert.Contains("MedicionPeticiones.AlMapearManejador(Context);", Cuerpo(global, "Application_PostMapRequestHandler"));
            // Primera línea: la espera de la sesión no incluye el control de sesión vigente (consulta a la BD).
            Assert.StartsWith("MedicionPeticiones.AlTenerSesion(Context);", Cuerpo(global, "Application_AcquireRequestState").Trim());
            string fin = Cuerpo(global, "Application_EndRequest");
            Assert.Contains("ErrorMetodoPagina.RegistrarSiFallo(Context);", fin);
            Assert.Contains("MedicionPeticiones.AlTerminar(Context);", fin);
            string fin2 = Cuerpo(global, "Application_End");
            Assert.True(fin2.IndexOf("MedicionPeticiones.AlDetener();", StringComparison.Ordinal) >= 0
                        && fin2.IndexOf("MedicionPeticiones.AlDetener();", StringComparison.Ordinal) < fin2.IndexOf("LogSGV.Cerrar();", StringComparison.Ordinal),
                "El resumen parcial va antes de cerrar el log.");
        }

        [Fact]
        public void WebConfig_UmbralDeTresSegundos()
        {
            string config = File.ReadAllText(Path.Combine(FuentesWeb.RaizWeb(), "Web.config"));
            Assert.Contains("<add key=\"" + MedicionPeticiones.ClaveUmbral + "\" value=\"3000\" />", config);
        }
    }
}
