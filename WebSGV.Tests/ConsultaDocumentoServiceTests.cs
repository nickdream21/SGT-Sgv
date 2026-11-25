using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Consultas;
using WebSGV.Tests.Integracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// B5-6: la consulta de DNI/RUC (apisperu) la hace el servidor con el token de la configuración. Todas las
    /// pruebas usan un HTTP falso: ninguna llama a la API real.
    /// </summary>
    public class ConsultaDocumentoServiceTests
    {
        private const string Token = "token-de-prueba/+=";

        private sealed class HttpFalso
        {
            public readonly List<string> Urls = new List<string>();
            private readonly Func<string, RespuestaHttp> _responder;

            public HttpFalso(Func<string, RespuestaHttp> responder) { _responder = responder; }

            public RespuestaHttp Obtener(string url)
            {
                Urls.Add(url);
                return _responder(url);
            }
        }

        private static HttpFalso Responde(int codigo, string cuerpo) => new HttpFalso(_ => new RespuestaHttp(codigo, cuerpo));

        private static ConsultaDocumentoService Servicio(HttpFalso http, string token = Token) =>
            new ConsultaDocumentoService(token, http.Obtener);

        private static string MensajeDe(Action accion) => Assert.Throws<ErrorNegocioException>(accion).Message;

        // ── DNI ─────────────────────────────────────────────────────────────

        [Fact]
        public void Dni_encontrado_se_mapea_y_la_url_lleva_el_token_codificado()
        {
            var http = Responde(200,
                "{\"success\":true,\"dni\":\"12345678\",\"nombres\":\"JOSÉ  MARÍA\",\"apellidoPaterno\":\"PEÑA\"," +
                "\"apellidoMaterno\":\"QUISPE \",\"codVerifica\":3}");

            DatosDni d = Servicio(http).ConsultarDni(" 12345678 ");

            Assert.Equal("12345678", d.Dni);
            Assert.Equal("JOSÉ MARÍA", d.Nombres);
            Assert.Equal("PEÑA", d.ApellidoPaterno);
            Assert.Equal("QUISPE", d.ApellidoMaterno);
            Assert.Equal(ConsultaDocumentoService.UrlBase + "dni/12345678?token=" + Uri.EscapeDataString(Token), Assert.Single(http.Urls));
        }

        [Fact]
        public void Dni_sin_apellido_materno_lo_deja_vacio()
        {
            var http = Responde(200, "{\"success\":true,\"nombres\":\"ANA\",\"apellidoPaterno\":\"RUIZ\",\"apellidoMaterno\":null}");
            Assert.Equal("", Servicio(http).ConsultarDni("87654321").ApellidoMaterno);
        }

        [Fact]
        public void Dni_que_no_existe_success_false_avisa_que_no_se_encontro()
        {
            var http = Responde(200, "{\"success\":false,\"message\":\"No se encontraron resultados.\"}");
            Assert.Equal(ConsultaDocumentoService.MensajeDniNoEncontrado, MensajeDe(() => Servicio(http).ConsultarDni("12345678")));
        }

        [Fact]
        public void Dni_con_404_avisa_que_no_se_encontro()
        {
            var http = Responde(404, "{\"message\":\"Not found\"}");
            Assert.Equal(ConsultaDocumentoService.MensajeDniNoEncontrado, MensajeDe(() => Servicio(http).ConsultarDni("12345678")));
        }

        [Fact]
        public void Dni_sin_nombres_avisa_que_no_se_encontro()
        {
            var http = Responde(200, "{\"dni\":\"12345678\",\"nombres\":\"  \"}");
            Assert.Equal(ConsultaDocumentoService.MensajeDniNoEncontrado, MensajeDe(() => Servicio(http).ConsultarDni("12345678")));
        }

        [Theory]
        [InlineData(401)]
        [InlineData(403)]
        [InlineData(429)]
        [InlineData(500)]
        [InlineData(503)]
        public void Dni_con_error_del_proveedor_pide_completar_a_mano(int codigo)
        {
            var http = Responde(codigo, "{\"message\":\"x\"}");
            Assert.Equal(ConsultaDocumentoService.MensajeFalloDni, MensajeDe(() => Servicio(http).ConsultarDni("12345678")));
        }

        [Fact]
        public void Dni_con_timeout_pide_completar_a_mano()
        {
            var http = new HttpFalso(_ => throw new WebException("The operation has timed out", WebExceptionStatus.Timeout));
            Assert.Equal(ConsultaDocumentoService.MensajeFalloDni, MensajeDe(() => Servicio(http).ConsultarDni("12345678")));
        }

        [Fact]
        public void Dni_sin_red_pide_completar_a_mano()
        {
            var http = new HttpFalso(_ => throw new InvalidOperationException("sin red"));
            Assert.Equal(ConsultaDocumentoService.MensajeFalloDni, MensajeDe(() => Servicio(http).ConsultarDni("12345678")));
        }

        [Theory]
        [InlineData("<html>Bad gateway</html>")]
        [InlineData("[]")]
        [InlineData("")]
        [InlineData(null)]
        public void Dni_con_cuerpo_ilegible_pide_completar_a_mano(string cuerpo)
        {
            var http = Responde(200, cuerpo);
            Assert.Equal(ConsultaDocumentoService.MensajeFalloDni, MensajeDe(() => Servicio(http).ConsultarDni("12345678")));
        }

        [Fact]
        public void Dni_con_respuesta_nula_pide_completar_a_mano()
        {
            var http = new HttpFalso(_ => null);
            Assert.Equal(ConsultaDocumentoService.MensajeFalloDni, MensajeDe(() => Servicio(http).ConsultarDni("12345678")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Sin_token_avisa_que_no_esta_configurada_y_no_llama_a_la_api(string token)
        {
            var http = Responde(200, "{\"nombres\":\"ANA\"}");
            Assert.Equal(ConsultaDocumentoService.MensajeSinConfigurar, MensajeDe(() => Servicio(http, token).ConsultarDni("12345678")));
            Assert.Equal(ConsultaDocumentoService.MensajeSinConfigurar, MensajeDe(() => Servicio(http, token).ConsultarRuc("20123456789")));
            Assert.Empty(http.Urls);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1234567")]
        [InlineData("123456789")]
        [InlineData("1234567a")]
        [InlineData("1234 678")]
        [InlineData("12345678?x=1")]
        [InlineData("../ruc/1")]
        [InlineData("１２３４５６７８")]   // dígitos de ancho completo: \d los aceptaría, [0-9] no
        public void Dni_invalido_no_llama_a_la_api(string dni)
        {
            var http = Responde(200, "{\"nombres\":\"ANA\"}");
            Assert.Equal(ConsultaDocumentoService.MensajeDniInvalido, MensajeDe(() => Servicio(http).ConsultarDni(dni)));
            Assert.Empty(http.Urls);
        }

        // ── RUC ─────────────────────────────────────────────────────────────

        [Fact]
        public void Ruc_encontrado_trae_razon_social_y_direccion()
        {
            var http = Responde(200,
                "{\"ruc\":\"20123456789\",\"razonSocial\":\"VITAPRO S.A.\",\"direccion\":\"AV. LOS ÁLAMOS 123\"," +
                "\"estado\":\"ACTIVO\",\"condicion\":\"HABIDO\"}");

            DatosRuc d = Servicio(http).ConsultarRuc("20123456789");

            Assert.Equal("20123456789", d.Ruc);
            Assert.Equal("VITAPRO S.A.", d.RazonSocial);
            Assert.Equal("AV. LOS ÁLAMOS 123", d.Direccion);
            Assert.StartsWith(ConsultaDocumentoService.UrlBase + "ruc/20123456789?token=", Assert.Single(http.Urls));
        }

        [Fact]
        public void Ruc_que_no_existe_avisa_que_no_se_encontro()
        {
            var http = Responde(200, "{\"success\":false,\"message\":\"No se encontraron resultados.\"}");
            Assert.Equal(ConsultaDocumentoService.MensajeRucNoEncontrado, MensajeDe(() => Servicio(http).ConsultarRuc("20123456789")));
        }

        [Fact]
        public void Ruc_con_timeout_pide_completar_a_mano()
        {
            var http = new HttpFalso(_ => throw new WebException("timeout", WebExceptionStatus.Timeout));
            Assert.Equal(ConsultaDocumentoService.MensajeFalloRuc, MensajeDe(() => Servicio(http).ConsultarRuc("20123456789")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("2012345678")]
        [InlineData("201234567890")]
        [InlineData("2012345678x")]
        [InlineData("12345678")]
        public void Ruc_invalido_no_llama_a_la_api(string ruc)
        {
            var http = Responde(200, "{\"razonSocial\":\"X\"}");
            Assert.Equal(ConsultaDocumentoService.MensajeRucInvalido, MensajeDe(() => Servicio(http).ConsultarRuc(ruc)));
            Assert.Empty(http.Urls);
        }

        [Fact]
        public void Ningun_mensaje_de_error_contiene_el_token()
        {
            var casos = new[]
            {
                Responde(500, "error " + Token),
                new HttpFalso(url => throw new WebException("fallo en " + url, WebExceptionStatus.ConnectFailure)),
                Responde(200, "no es json " + Token)
            };
            foreach (var http in casos)
                Assert.DoesNotContain(Token, MensajeDe(() => Servicio(http).ConsultarDni("12345678")));
        }

        [Fact]
        public void El_constructor_exige_la_funcion_http()
        {
            Assert.Throws<ArgumentNullException>(() => new ConsultaDocumentoService(Token, null));
        }

        // ── Respuesta al navegador ──────────────────────────────────────────

        [Fact]
        public void Respuesta_al_navegador_solo_lleva_los_datos_del_formulario()
        {
            var dni = RespuestaConsultaDocumento.DeDni(new DatosDni { Dni = "1", Nombres = "A", ApellidoPaterno = "B", ApellidoMaterno = "C" });
            Assert.True(dni.exito);
            Assert.Equal("A", dni.nombres);
            Assert.Null(dni.mensaje);

            var ruc = RespuestaConsultaDocumento.DeRuc(new DatosRuc { Ruc = "2", RazonSocial = "R", Direccion = "D" });
            Assert.True(ruc.exito);
            Assert.Equal("R", ruc.razonSocial);
            Assert.Equal("D", ruc.direccion);

            var error = RespuestaConsultaDocumento.Error("m");
            Assert.False(error.exito);
            Assert.Equal("m", error.mensaje);
        }

        // ── Código fuente ───────────────────────────────────────────────────

        private static readonly Regex Jwt = new Regex(@"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.", RegexOptions.Compiled);

        [Fact]
        public void Ningun_script_ni_pagina_lleva_un_token_ni_llama_a_apisperu()
        {
            string web = Path.Combine(BaseDePruebas.RaizRepo, "WebSGV");
            var archivos = Directory.GetFiles(Path.Combine(web, "Scripts"), "*.js", SearchOption.AllDirectories)
                .Concat(FuentesWeb.Marcado(Path.Combine(web, "Views")))   // páginas y controles .ascx
                .Concat(Directory.GetFiles(web, "*.Master", SearchOption.TopDirectoryOnly));

            var malos = new List<string>();
            foreach (string archivo in archivos)
            {
                string texto = File.ReadAllText(archivo);
                if (Jwt.IsMatch(texto)) malos.Add(archivo.Substring(web.Length + 1) + " (JWT)");
                if (texto.IndexOf("apisperu.com", StringComparison.OrdinalIgnoreCase) >= 0)
                    malos.Add(archivo.Substring(web.Length + 1) + " (apisperu.com)");
            }
            Assert.True(malos.Count == 0, "El token de la API va solo en el servidor (B5-6): " + string.Join(", ", malos));
        }

        [Fact]
        public void Los_webmethods_de_consulta_verifican_sesion_y_permiso()
        {
            string choferes = File.ReadAllText(Path.Combine(BaseDePruebas.RaizRepo, "WebSGV", "Views", "RegistroChoferes.aspx.cs"));
            string clientes = File.ReadAllText(Path.Combine(BaseDePruebas.RaizRepo, "WebSGV", "Views", "RegistroClientes.aspx.cs"));

            Assert.Contains("[WebMethod(EnableSession = true)]", choferes);
            Assert.Contains("RolesHelper.TienePermiso(\"REGISTRO_CONDUCTORES\")", choferes);
            Assert.Contains("ConsultaDocumentoService.DesdeConfiguracion().BuscarDni(numero,", choferes);
            Assert.Contains("RegistroConductorService.BuscarDuplicado(DocumentoConductor.NormalizarDni(dni), null", choferes);

            Assert.Contains("[WebMethod(EnableSession = true)]", clientes);
            Assert.Contains("RolesHelper.TienePermiso(\"REGISTRO\")", clientes);
            Assert.Contains("ConsultaDocumentoService.DesdeConfiguracion().BuscarRuc(numero, ClienteConRuc,", clientes);
            // Misma comparación que la validación de duplicado del alta
            Assert.Contains("SELECT COUNT(*) FROM Cliente WHERE ruc = @ruc", clientes);
            Assert.Contains("FROM Cliente WHERE ruc = @ruc ORDER BY idCliente", clientes);

            foreach (string codigo in new[] { choferes, clientes })
            {
                Assert.Contains("SecurityHelper.TieneSesionActiva()", codigo);
                Assert.Contains("LimiteConsultasSesion.DeSesionActual()", codigo);
                Assert.Contains("() => limite != null && limite.Permitir()", codigo);
            }
        }

        // ── Flujo del botón: duplicado y límite antes de gastar cuota ───────

        private const string DniOk = "{\"success\":true,\"nombres\":\"ANA\",\"apellidoPaterno\":\"RUIZ\",\"apellidoMaterno\":\"SOTO\"}";

        private sealed class Contador
        {
            public int Llamadas;
            public bool Respuesta = true;
            public bool Permitir() { Llamadas++; return Respuesta; }
        }

        [Fact]
        public void BuscarDni_ok_cuenta_una_consulta_y_devuelve_los_datos()
        {
            var http = Responde(200, DniOk);
            var limite = new Contador();
            string consultadoDuplicado = null;

            var r = Servicio(http).BuscarDni(" 12345678 ", d => { consultadoDuplicado = d; return null; }, limite.Permitir);

            Assert.True(r.exito);
            Assert.Equal("ANA", r.nombres);
            Assert.Equal("12345678", consultadoDuplicado);
            Assert.Equal(1, limite.Llamadas);
            Assert.Single(http.Urls);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("1234567")]
        [InlineData("12345678?x")]
        public void BuscarDni_invalido_no_cuenta_ni_busca_duplicado_ni_llama_a_la_api(string numero)
        {
            var http = Responde(200, DniOk);
            var limite = new Contador();
            bool buscoDuplicado = false;

            var r = Servicio(http).BuscarDni(numero, d => { buscoDuplicado = true; return null; }, limite.Permitir);

            Assert.False(r.exito);
            Assert.Equal(ConsultaDocumentoService.MensajeDniInvalido, r.mensaje);
            Assert.False(buscoDuplicado);
            Assert.Equal(0, limite.Llamadas);
            Assert.Empty(http.Urls);
        }

        [Fact]
        public void BuscarDni_ya_registrado_avisa_sin_contar_ni_llamar_a_la_api()
        {
            var http = Responde(200, DniOk);
            var limite = new Contador();

            var r = Servicio(http).BuscarDni("12345678", d => "Ya existe un conductor con el DNI 12345678: ANA RUIZ.", limite.Permitir);

            Assert.False(r.exito);
            Assert.Equal("Ya existe un conductor con el DNI 12345678: ANA RUIZ.", r.mensaje);
            Assert.Equal(0, limite.Llamadas);
            Assert.Empty(http.Urls);
        }

        [Fact]
        public void BuscarRuc_ya_registrado_avisa_sin_contar_ni_llamar_a_la_api()
        {
            var http = Responde(200, "{\"razonSocial\":\"X\"}");
            var limite = new Contador();

            var r = Servicio(http).BuscarRuc("20123456789",
                ruc => WebSGV.Views.RegistroClientes.MensajeRucRegistrado(" VITAPRO S.A. "), limite.Permitir);

            Assert.False(r.exito);
            Assert.Equal("Ya existe un cliente registrado con ese RUC: VITAPRO S.A.", r.mensaje);
            Assert.Equal(0, limite.Llamadas);
            Assert.Empty(http.Urls);
        }

        [Theory]
        [InlineData("VITAPRO S.A.", "Ya existe un cliente registrado con ese RUC: VITAPRO S.A.")]
        [InlineData(" INBALNOR  ", "Ya existe un cliente registrado con ese RUC: INBALNOR.")]
        [InlineData("", "Ya existe un cliente registrado con ese RUC: (sin nombre).")]
        [InlineData(null, "Ya existe un cliente registrado con ese RUC: (sin nombre).")]
        public void Mensaje_de_ruc_registrado(string nombre, string esperado)
        {
            Assert.Equal(esperado, WebSGV.Views.RegistroClientes.MensajeRucRegistrado(nombre));
        }

        [Fact]
        public void BuscarRuc_ok_devuelve_razon_social()
        {
            var http = Responde(200, "{\"razonSocial\":\"VITAPRO S.A.\",\"direccion\":\"AV. X\"}");
            var r = Servicio(http).BuscarRuc("20123456789", _ => null, new Contador().Permitir);
            Assert.True(r.exito);
            Assert.Equal("VITAPRO S.A.", r.razonSocial);
        }

        [Fact]
        public void Limite_superado_responde_el_aviso_sin_llamar_a_la_api()
        {
            var http = Responde(200, DniOk);
            var limite = new Contador { Respuesta = false };

            var r = Servicio(http).BuscarDni("12345678", _ => null, limite.Permitir);

            Assert.False(r.exito);
            Assert.Equal(LimiteConsultasSesion.MensajeExcedido, r.mensaje);
            Assert.Equal("Demasiadas consultas seguidas; espere unos minutos o complete los datos a mano.", r.mensaje);
            Assert.Empty(http.Urls);
        }

        [Fact]
        public void Sin_token_el_flujo_avisa_y_no_gasta_del_limite()
        {
            var http = Responde(200, DniOk);
            var limite = new Contador();

            var r = Servicio(http, "").BuscarRuc("20123456789", _ => null, limite.Permitir);

            Assert.Equal(ConsultaDocumentoService.MensajeSinConfigurar, r.mensaje);
            Assert.Equal(0, limite.Llamadas);
            Assert.Empty(http.Urls);
        }

        [Fact]
        public void Error_de_la_api_en_el_flujo_vuelve_como_mensaje()
        {
            var http = Responde(500, "x");
            var r = Servicio(http).BuscarDni("12345678", _ => null, new Contador().Permitir);
            Assert.False(r.exito);
            Assert.Equal(ConsultaDocumentoService.MensajeFalloDni, r.mensaje);
        }

        // ── Límite por sesión ───────────────────────────────────────────────

        private sealed class SesionFalsa
        {
            public object Valor;
            public DateTime Ahora = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
            public LimiteConsultasSesion Limite() => new LimiteConsultasSesion(() => Valor, v => Valor = v, () => Ahora);
        }

        [Fact]
        public void Limite_por_sesion_treinta_pasan_la_treinta_y_uno_no()
        {
            var s = new SesionFalsa();
            for (int i = 0; i < LimiteConsultasSesion.Maximo; i++)
            {
                Assert.True(s.Limite().Permitir(), "consulta " + (i + 1));
                s.Ahora = s.Ahora.AddSeconds(1);
            }
            Assert.False(s.Limite().Permitir());
            Assert.Equal(30, ((DateTime[])s.Valor).Length);   // la rechazada no se registra
        }

        [Fact]
        public void Limite_por_sesion_se_libera_al_pasar_la_ventana()
        {
            var s = new SesionFalsa();
            for (int i = 0; i < 30; i++) Assert.True(s.Limite().Permitir());
            Assert.False(s.Limite().Permitir());

            s.Ahora = s.Ahora.AddMinutes(10);
            Assert.True(s.Limite().Permitir());
            Assert.Single((DateTime[])s.Valor);   // las vencidas se descartan
        }

        [Fact]
        public void Limite_por_sesion_es_ventana_deslizante()
        {
            var s = new SesionFalsa();
            for (int i = 0; i < 15; i++) Assert.True(s.Limite().Permitir());
            s.Ahora = s.Ahora.AddMinutes(5);
            for (int i = 0; i < 15; i++) Assert.True(s.Limite().Permitir());
            Assert.False(s.Limite().Permitir());

            s.Ahora = s.Ahora.AddMinutes(5).AddSeconds(1);   // vencen solo las 15 primeras
            for (int i = 0; i < 15; i++) Assert.True(s.Limite().Permitir());
            Assert.False(s.Limite().Permitir());
        }

        [Fact]
        public void Limite_por_sesion_tolera_un_valor_ajeno_en_la_sesion()
        {
            var s = new SesionFalsa { Valor = "basura" };
            Assert.True(s.Limite().Permitir());
            Assert.Single((DateTime[])s.Valor);
        }

        [Fact]
        public void Limite_por_sesion_rechaza_parametros_invalidos()
        {
            Assert.Throws<ArgumentNullException>(() => new LimiteConsultasSesion(null, v => { }, () => DateTime.UtcNow));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LimiteConsultasSesion(() => null, v => { }, () => DateTime.UtcNow, 0));
        }
    }
}
