using System;
using System.Runtime.Serialization;
using System.Threading;
using System.Web;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Operacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Aviso por correo de los errores no controlados (E1-7): límite, exclusiones, cuerpo y remitente.</summary>
    public class AvisoErroresTests
    {
        private static readonly DateTime Ahora = new DateTime(2026, 11, 19, 15, 0, 0);

        /// <summary>Excepción lanzada de verdad (con pila) desde un método propio.</summary>
        private static Exception Lanzada(Func<Exception> crear)
        {
            try { throw crear(); }
            catch (Exception ex) { return ex; }
        }

        private static Exception OtroLugar(Func<Exception> crear)
        {
            try { throw crear(); }
            catch (Exception ex) { return ex; }
        }

        // ── Límite: por tipo ──

        [Fact]
        public void MismoTipo_UnoCada30Minutos()
        {
            var lim = new LimitadorAvisos();
            Assert.True(lim.Evaluar("A", Ahora).Enviar);
            Assert.False(lim.Evaluar("A", Ahora.AddMinutes(1)).Enviar);
            Assert.False(lim.Evaluar("A", Ahora.AddMinutes(29).AddSeconds(59)).Enviar);
            Assert.True(lim.Evaluar("A", Ahora.AddMinutes(30)).Enviar);
        }

        [Fact]
        public void TiposDistintos_NoSeLimitanEntreSi()
        {
            var lim = new LimitadorAvisos();
            Assert.True(lim.Evaluar("A", Ahora).Enviar);
            Assert.True(lim.Evaluar("B", Ahora.AddSeconds(1)).Enviar);
            Assert.False(lim.Evaluar("A", Ahora.AddSeconds(2)).Enviar);
        }

        // ── Límite: por día ──

        [Fact]
        public void Maximo10AlDia_YElDiaSiguienteVuelve()
        {
            var lim = new LimitadorAvisos();
            for (int i = 0; i < LimitadorAvisos.MaximoPorDia; i++)
                Assert.True(lim.Evaluar("T" + i, Ahora.AddMinutes(i)).Enviar);

            Assert.Equal(10, LimitadorAvisos.MaximoPorDia);
            Assert.False(lim.Evaluar("nuevo", Ahora.AddMinutes(20)).Enviar);
            Assert.False(lim.Evaluar("otro", Ahora.AddHours(3)).Enviar);

            var manana = lim.Evaluar("nuevo", Ahora.Date.AddDays(1).AddMinutes(1));
            Assert.True(manana.Enviar);
            Assert.Equal(2, manana.OmitidosAntes);
        }

        // ── Omitidos: se informan en el siguiente correo ──

        [Fact]
        public void Omitidos_SeCuentanYSeReinicianTrasEnviar()
        {
            var lim = new LimitadorAvisos();
            Assert.Equal(0, lim.Evaluar("A", Ahora).OmitidosAntes);
            lim.Evaluar("A", Ahora.AddMinutes(5));
            lim.Evaluar("A", Ahora.AddMinutes(6));
            lim.Evaluar("A", Ahora.AddMinutes(7));

            var siguiente = lim.Evaluar("B", Ahora.AddMinutes(8));
            Assert.True(siguiente.Enviar);
            Assert.Equal(3, siguiente.OmitidosAntes);
            Assert.Equal(Ahora.AddMinutes(5), siguiente.OmitidosDesde);

            var otro = lim.Evaluar("C", Ahora.AddMinutes(9));
            Assert.Equal(0, otro.OmitidosAntes);
            Assert.Null(otro.OmitidosDesde);
        }

        [Fact]
        public void Cuerpo_MencionaLosOmitidos()
        {
            var datos = Datos(new InvalidOperationException("x"));
            string conOmitidos = AvisoErrores.ArmarCuerpo(datos,
                new DecisionAviso { Enviar = true, OmitidosAntes = 4, OmitidosDesde = Ahora.AddMinutes(-40) });
            Assert.Contains("Y 4 errores más sin aviso desde el 19/11/2026 14:20", conOmitidos);

            string sin = AvisoErrores.ArmarCuerpo(datos, new DecisionAviso { Enviar = true });
            Assert.DoesNotContain("sin aviso desde", sin);
        }

        // ── Envío fallido: libera el cupo; 3 fallos seguidos pausan 15 min ──

        [Fact]
        public void EnvioFallido_LiberaElTipoYElCupoDelDia()
        {
            var lim = new LimitadorAvisos();
            var d = lim.Evaluar("A", Ahora);
            lim.RegistrarResultado(d, false, Ahora.AddSeconds(30));

            Assert.True(lim.Evaluar("A", Ahora.AddMinutes(1)).Enviar); // vuelve a intentar sin esperar 30 min

            // El fallido no cuenta en los 10 del día (la de arriba sí, porque no se registró como fallida).
            var lim2 = new LimitadorAvisos();
            lim2.RegistrarResultado(lim2.Evaluar("X", Ahora), false, Ahora);
            for (int i = 0; i < LimitadorAvisos.MaximoPorDia; i++)
                Assert.True(lim2.Evaluar("T" + i, Ahora.AddMinutes(1 + i)).Enviar);
            Assert.False(lim2.Evaluar("T-otro", Ahora.AddMinutes(20)).Enviar);
        }

        [Fact]
        public void EnvioFallido_LosOmitidosQueTraiaSeInformanEnElSiguiente()
        {
            var lim = new LimitadorAvisos();
            lim.Evaluar("A", Ahora);
            lim.Evaluar("A", Ahora.AddMinutes(1));       // omitido
            var conOmitido = lim.Evaluar("B", Ahora.AddMinutes(2));
            Assert.Equal(1, conOmitido.OmitidosAntes);
            lim.RegistrarResultado(conOmitido, false, Ahora.AddMinutes(2));

            var siguiente = lim.Evaluar("C", Ahora.AddMinutes(3));
            Assert.Equal(1, siguiente.OmitidosAntes);
            Assert.Equal(Ahora.AddMinutes(1), siguiente.OmitidosDesde);
        }

        [Fact]
        public void TresFallosSeguidos_PausanLosEnvios15Minutos()
        {
            var lim = new LimitadorAvisos();
            for (int i = 0; i < LimitadorAvisos.FallosParaPausa; i++)
                lim.RegistrarResultado(lim.Evaluar("F" + i, Ahora.AddMinutes(i)), false, Ahora.AddMinutes(i));

            var t = Ahora.AddMinutes(LimitadorAvisos.FallosParaPausa - 1); // hora del tercer fallo
            var enPausa = lim.Evaluar("nuevo", t.AddMinutes(14));
            Assert.False(enPausa.Enviar);
            Assert.True(enPausa.EnPausa);
            Assert.True(lim.Evaluar("nuevo", t.AddMinutes(15)).Enviar);
            Assert.Equal(TimeSpan.FromMinutes(15), LimitadorAvisos.Pausa);
            Assert.Equal(3, LimitadorAvisos.FallosParaPausa);
        }

        [Fact]
        public void UnEnvioBuenoReiniciaLosFallosSeguidos()
        {
            var lim = new LimitadorAvisos();
            lim.RegistrarResultado(lim.Evaluar("F1", Ahora), false, Ahora);
            lim.RegistrarResultado(lim.Evaluar("F2", Ahora), false, Ahora);
            lim.RegistrarResultado(lim.Evaluar("OK", Ahora), true, Ahora);
            lim.RegistrarResultado(lim.Evaluar("F3", Ahora), false, Ahora);
            Assert.True(lim.Evaluar("nuevo", Ahora.AddSeconds(1)).Enviar); // sin pausa
        }

        // ── Omisiones en el log: solo la primera de cada tipo en la ventana ──

        [Fact]
        public void Omision_SoloLaPrimeraDeCadaTipoSeRegistra()
        {
            var lim = new LimitadorAvisos();
            lim.Evaluar("A", Ahora);
            Assert.True(lim.Evaluar("A", Ahora.AddMinutes(1)).RegistrarOmision);
            Assert.False(lim.Evaluar("A", Ahora.AddMinutes(2)).RegistrarOmision);
            Assert.False(lim.Evaluar("A", Ahora.AddMinutes(29)).RegistrarOmision);
            Assert.True(lim.Evaluar("A", Ahora.AddMinutes(30)).Enviar);
            Assert.True(lim.Evaluar("A", Ahora.AddMinutes(32)).RegistrarOmision); // otra ventana
            Assert.False(lim.Evaluar("A", Ahora.AddMinutes(33)).RegistrarOmision);
        }

        // ── Origen: las PCs de desarrollo no avisan salvo AvisoErroresLocal=true ──

        [Theory]
        [InlineData(true, "sgvtransporte.somee.com", null, false)]
        [InlineData(false, "localhost", null, false)]
        [InlineData(false, "127.0.0.1", "", false)]
        [InlineData(false, "[::1]", "false", false)]
        [InlineData(false, "LOCALHOST", "true", true)]
        [InlineData(true, "localhost", "True", true)]
        [InlineData(false, "sgvtransporte.somee.com", null, true)]
        [InlineData(false, "sgvtransporte.somee.com", "false", true)]
        public void AvisarSegunOrigen(bool esLocal, string host, string valor, bool esperado)
        {
            Assert.Equal(esperado, AvisoErrores.AvisarSegunOrigen(esLocal, host, valor));
        }

        // ── Destinatario en el log ──

        [Theory]
        [InlineData("ana.perez@empresa.com", "***@empresa.com")]
        [InlineData("a@x.com, b@y.pe", "***@x.com, ***@y.pe")]
        [InlineData("sin-arroba", "***")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void OcultarDestinatario_SoloElDominio(string para, string esperado)
        {
            Assert.Equal(esperado, CorreoHelper.OcultarDestinatario(para));
        }

        // ── Tipo de error ──

        [Fact]
        public void ClaveTipo_TipoLugarYRutaNormalizada()
        {
            var a = Lanzada(() => new InvalidOperationException("uno"));
            var b = Lanzada(() => new InvalidOperationException("otro mensaje"));
            var c = OtroLugar(() => new InvalidOperationException("uno"));
            var d = Lanzada(() => new FormatException("uno"));

            Assert.Contains("AvisoErroresTests.Lanzada", AvisoErrores.Lugar(a));
            // Mismo tipo, lugar y ruta (con otra query, mayúsculas o número): mismo tipo, aunque cambie el mensaje.
            Assert.Equal(AvisoErrores.ClaveTipo(a, "/Views/Detalle.aspx"), AvisoErrores.ClaveTipo(b, "/views/detalle.aspx/?id=5"));
            Assert.Equal(AvisoErrores.NormalizarRuta("/Views/Lote123.aspx"), AvisoErrores.NormalizarRuta("/views/lote9.aspx"));
            // Cambia el lugar, la excepción o la ruta: otro tipo.
            Assert.NotEqual(AvisoErrores.ClaveTipo(a, "/Views/Detalle.aspx"), AvisoErrores.ClaveTipo(c, "/Views/Detalle.aspx"));
            Assert.NotEqual(AvisoErrores.ClaveTipo(a, "/Views/Detalle.aspx"), AvisoErrores.ClaveTipo(d, "/Views/Detalle.aspx"));
            Assert.NotEqual(AvisoErrores.ClaveTipo(a, "/Views/Detalle.aspx"), AvisoErrores.ClaveTipo(a, "/Views/Otra.aspx"));
        }

        [Fact]
        public void Lugar_SinLanzar_Vacio()
        {
            Assert.Equal("", AvisoErrores.Lugar(new InvalidOperationException("sin lanzar")));
            Assert.Equal("", AvisoErrores.Lugar(null));
        }

        // ── Exclusiones ──

        [Fact]
        public void ErrorDelSistema_SeAvisa()
        {
            Assert.True(AvisoErrores.DebeAvisar(Lanzada(() => new NullReferenceException()), "/Views/Inicio.aspx"));
            Assert.True(AvisoErrores.DebeAvisar(new HttpException(500, "fallo"), "/Views/Inicio.aspx"));
            Assert.True(AvisoErrores.DebeAvisar(new InvalidOperationException("x", new FormatException()), "/Views/Inicio.aspx"));
        }

        [Fact]
        public void NoSeAvisa_Nulo()
        {
            Assert.False(AvisoErrores.DebeAvisar(null, "/Views/Inicio.aspx"));
        }

        [Theory]
        [InlineData(404)]
        [InlineData(400)]
        [InlineData(405)]
        public void NoSeAvisa_PeticionInvalidaONoEncontrada(int codigo)
        {
            Assert.False(AvisoErrores.DebeAvisar(new HttpException(codigo, "x"), "/Views/X.aspx"));
            Assert.False(AvisoErrores.DebeAvisar(new InvalidOperationException("envuelta", new HttpException(codigo, "x")), "/Views/X.aspx"));
        }

        [Fact]
        public void NoSeAvisa_PeticionPeligrosa()
        {
            Assert.False(AvisoErrores.DebeAvisar(new HttpRequestValidationException("A potentially dangerous Request.Form value was detected"), "/Views/X.aspx"));
            Assert.False(AvisoErrores.DebeAvisar(new HttpException("A potentially dangerous Request.Path value was detected from the client (:)."), "/Views/X.aspx"));
            Assert.False(AvisoErrores.DebeAvisar(new HttpException("Se detectó un valor Request.Path potencialmente peligroso en el cliente (:)."), "/Views/X.aspx"));
        }

        [Fact]
        public void NoSeAvisa_ValidacionDeEventos()
        {
            var ingles = new ArgumentException("Invalid postback or callback argument.  Event validation is enabled using <pages enableEventValidation=\"true\"/>");
            var espanol = new ArgumentException("Argumento de devolución de datos o de devolución de llamada no válido. La validación de eventos se habilita...");
            Assert.False(AvisoErrores.DebeAvisar(ingles, "/Views/X.aspx"));
            Assert.False(AvisoErrores.DebeAvisar(espanol, "/Views/X.aspx"));
            Assert.False(AvisoErrores.DebeAvisar(new HttpUnhandledException("x", ingles), "/Views/X.aspx"));
            // Otro ArgumentException sí es un fallo del sistema.
            Assert.True(AvisoErrores.DebeAvisar(new ArgumentException("El valor no puede ser nulo."), "/Views/X.aspx"));
        }

        [Fact]
        public void NoSeAvisa_ViewState()
        {
            var interna = (Exception)FormatterServices.GetUninitializedObject(typeof(System.Web.UI.ViewStateException));
            Assert.False(AvisoErrores.DebeAvisar(new HttpException("Error en la validación del MAC.", interna), "/Views/X.aspx"));
            Assert.False(AvisoErrores.DebeAvisar(new HttpException("Validation of viewstate MAC failed."), "/Views/X.aspx"));
        }

        [Fact]
        public void NoSeAvisa_ThreadAbort()
        {
            // ThreadAbortException no tiene constructor público: se crea sin inicializar.
            var abort = (Exception)FormatterServices.GetUninitializedObject(typeof(ThreadAbortException));
            Assert.False(AvisoErrores.DebeAvisar(abort, "/Views/X.aspx"));
        }

        [Fact]
        public void NoSeAvisa_ErrorDeNegocio()
        {
            Assert.False(AvisoErrores.DebeAvisar(new ErrorNegocioException("La orden ya fue aprobada."), "/Views/X.aspx"));
            Assert.False(AvisoErrores.DebeAvisar(new HttpUnhandledException("x", new ErrorNegocioException("y")), "/Views/X.aspx"));
        }

        [Theory]
        [InlineData("/wp-login.php")]
        [InlineData("/wp-admin/setup-config.php")]
        [InlineData("/.env")]
        [InlineData("/.git/config")]
        [InlineData("/phpmyadmin/index.aspx")]
        [InlineData("/cgi-bin/luci")]
        public void NoSeAvisa_SondeoDeBots(string ruta)
        {
            Assert.True(AvisoErrores.EsRutaDeBot(ruta));
            Assert.False(AvisoErrores.DebeAvisar(new InvalidOperationException("x"), ruta));
        }

        [Theory]
        [InlineData("/Views/Inicio.aspx")]
        [InlineData("/Views/Exportacion/RegistroSeguimiento.aspx/ActualizarGps")]
        [InlineData("/Salud.ashx")]
        [InlineData("")]
        public void RutasPropias_NoSonDeBots(string ruta)
        {
            Assert.False(AvisoErrores.EsRutaDeBot(ruta));
        }

        // ── Cuerpo: sin querystring ni datos de formulario ──

        private static DatosErrorAviso Datos(Exception ex) => new DatosErrorAviso
        {
            FechaPeru = Ahora,
            Sitio = "sgvtransporte.somee.com",
            Ruta = "/Views/RestablecerContrasena.aspx",
            Query = "?token=SECRETO-123&usuario=pepe%40x.com&flag",
            Metodo = "POST",
            IdUsuario = 7,
            NombreUsuario = "Ana Pérez",
            Rol = "ADMIN",
            Ip = "190.1.2.3",
            Excepcion = ex,
        };

        [Fact]
        public void Cuerpo_TieneLosDatosYNoLosValoresDelQuerystring()
        {
            var ex = Lanzada(() => new InvalidOperationException("Falló el cálculo", new FormatException("interna")));
            string cuerpo = AvisoErrores.ArmarCuerpo(Datos(ex), new DecisionAviso { Enviar = true });

            Assert.Contains("Fecha (hora de Perú): 19/11/2026 15:00:00", cuerpo);
            Assert.Contains("Sitio: sgvtransporte.somee.com", cuerpo);
            Assert.Contains("URL: /Views/RestablecerContrasena.aspx?token=&usuario=&flag=", cuerpo);
            Assert.Contains("Método: POST", cuerpo);
            Assert.Contains("Usuario: 7 - Ana Pérez (ADMIN)", cuerpo);
            Assert.Contains("IP: 190.1.2.3", cuerpo);
            Assert.Contains("Tipo: System.InvalidOperationException", cuerpo);
            Assert.Contains("Mensaje: Falló el cálculo", cuerpo);
            Assert.Contains("AvisoErroresTests.Lanzada", cuerpo); // pila
            Assert.Contains("interna", cuerpo);                    // excepción interna

            Assert.DoesNotContain("SECRETO-123", cuerpo);
            Assert.DoesNotContain("pepe", cuerpo);
            Assert.DoesNotContain("<html", cuerpo); // texto plano
        }

        [Fact]
        public void DatosDelAviso_NoTienenDondeLlevarFormularioNiCookies()
        {
            // El cuerpo solo se arma con estas propiedades: ninguna lleva el formulario, las cookies ni los encabezados.
            var nombres = Array.ConvertAll(typeof(DatosErrorAviso).GetProperties(), p => p.Name);
            Assert.Equal(new[] { "FechaPeru", "Sitio", "Ruta", "Query", "Metodo", "IdUsuario", "NombreUsuario", "Rol", "Ip", "Excepcion" }, nombres);
        }

        [Fact]
        public void Cuerpo_SinSesion()
        {
            var d = Datos(new InvalidOperationException("x"));
            d.IdUsuario = null; d.NombreUsuario = ""; d.Rol = "";
            Assert.Contains("Usuario: sin sesión", AvisoErrores.ArmarCuerpo(d, null));
        }

        [Fact]
        public void QuerySinValores_SoloNombres()
        {
            Assert.Equal("", AvisoErrores.QuerySinValores(null));
            Assert.Equal("", AvisoErrores.QuerySinValores("?"));
            Assert.Equal("?id=&aviso=", AvisoErrores.QuerySinValores("?id=15&aviso=pagina_vencida"));
            Assert.Equal("?a=", AvisoErrores.QuerySinValores("a=1&&=solo-valor"));
        }

        [Fact]
        public void UrlSinValores_QuitaLosValoresDelQuerystring()
        {
            Assert.Equal("", AvisoErrores.UrlSinValores(null));
            Assert.Equal("/Views/Inicio.aspx", AvisoErrores.UrlSinValores("/Views/Inicio.aspx"));
            Assert.Equal("/Views/RestablecerContrasena.aspx?token=",
                AvisoErrores.UrlSinValores("/Views/RestablecerContrasena.aspx?token=abc123secreto"));
            Assert.Equal("~/Views/X.aspx?token=&aviso=",
                AvisoErrores.UrlSinValores("~/Views/X.aspx?token=abc&aviso=pagina_vencida"));
            Assert.Equal("/Views/X.aspx", AvisoErrores.UrlSinValores("/Views/X.aspx?"));
            Assert.DoesNotContain("abc123", AvisoErrores.UrlSinValores("https://sitio/Views/R.aspx?token=abc123&x=1"));
        }

        [Fact]
        public void Asunto_UnaLineaConTipoRutaYSitio()
        {
            var d = Datos(new InvalidOperationException("x"));
            Assert.Equal("[SGV] InvalidOperationException en /Views/RestablecerContrasena.aspx (sgvtransporte.somee.com)", AvisoErrores.ArmarAsunto(d));

            d.Ruta = "/Views/X.aspx\r\nBcc: otro@x.com";
            Assert.DoesNotContain("\n", AvisoErrores.ArmarAsunto(d));
        }

        // ── Remitente y configuración SMTP ──

        [Fact]
        public void Remitente_SmtpFromSiExiste_SiNoSmtpUser()
        {
            Assert.Equal("sgv-avisos@empresa.com", CorreoHelper.Remitente("sgv-avisos@empresa.com", "cuenta@empresa.com"));
            Assert.Equal("cuenta@empresa.com", CorreoHelper.Remitente(null, "cuenta@empresa.com"));
            Assert.Equal("cuenta@empresa.com", CorreoHelper.Remitente("  ", "cuenta@empresa.com"));
        }

        [Fact]
        public void LeerConfiguracion_ConYSinSmtpFrom()
        {
            var claves = new System.Collections.Generic.Dictionary<string, string>
            {
                ["SmtpHost"] = "smtp.ejemplo.com", ["SmtpPort"] = "587", ["SmtpSSL"] = "true",
                ["SmtpUser"] = "cuenta@empresa.com", ["SmtpPass"] = "x", ["SmtpFrom"] = "sgv-avisos@empresa.com",
            };
            Func<string, string> leer = k => claves.TryGetValue(k, out string v) ? v : null;

            var con = CorreoHelper.LeerConfiguracion(leer);
            Assert.Equal("sgv-avisos@empresa.com", con.Remitente);
            Assert.Equal("cuenta@empresa.com", con.Usuario);
            Assert.Equal(587, con.Puerto);
            Assert.True(con.Ssl);
            Assert.True(con.EstaCompleta);

            claves.Remove("SmtpFrom");
            Assert.Equal("cuenta@empresa.com", CorreoHelper.LeerConfiguracion(leer).Remitente);
        }

        [Fact]
        public void LeerConfiguracion_TlsPorDefecto_YPuertoInvalido587()
        {
            var vacia = CorreoHelper.LeerConfiguracion(k => null);
            Assert.True(vacia.Ssl);
            Assert.Equal(587, vacia.Puerto);
            Assert.False(vacia.EstaCompleta);

            var sinTls = CorreoHelper.LeerConfiguracion(k => k == "SmtpSSL" ? "false" : k == "SmtpPort" ? "abc" : "x");
            Assert.False(sinTls.Ssl);
            Assert.Equal(587, sinTls.Puerto);
        }

        [Fact]
        public void Enviar_SinConfiguracionOSinDestinatario_DevuelveFalseSinLanzar()
        {
            Assert.False(CorreoHelper.Enviar(null, "a@b.com", "asunto", "cuerpo", false));
            Assert.False(CorreoHelper.Enviar(CorreoHelper.LeerConfiguracion(k => null), "a@b.com", "asunto", "cuerpo", false));
            var completa = CorreoHelper.LeerConfiguracion(k => k == "SmtpHost" ? "smtp.invalido" : "x@y.com");
            Assert.False(CorreoHelper.Enviar(completa, "", "asunto", "cuerpo", false));
            Assert.Equal(30000, CorreoHelper.TimeoutMs);
        }
    }
}
