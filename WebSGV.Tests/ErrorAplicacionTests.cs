using System;
using System.IO;
using System.Web;
using WebSGV.Services.Common;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Clasificación de errores no controlados (auditoría de seguridad, tarea 6): Global.asax
    /// registra cada error una sola vez y manda los de ViewState al Login.
    /// </summary>
    public class ErrorAplicacionTests
    {
        [Fact]
        public void Desenvolver_QuitaCapasDeHttpUnhandledException()
        {
            var real = new InvalidOperationException("real");
            var envuelta = new HttpUnhandledException("capa 1", new HttpUnhandledException("capa 2", real));
            Assert.Same(real, ErrorAplicacionHelper.Desenvolver(envuelta));
        }

        [Fact]
        public void Desenvolver_SinInterna_DevuelveLaMisma()
        {
            var sola = new HttpUnhandledException("sin interna");
            Assert.Same(sola, ErrorAplicacionHelper.Desenvolver(sola));
        }

        [Fact]
        public void Desenvolver_NoQuitaOtrasExcepciones()
        {
            var interna = new ArgumentException("interna");
            var externa = new InvalidOperationException("externa", interna);
            Assert.Same(externa, ErrorAplicacionHelper.Desenvolver(externa));
            Assert.Null(ErrorAplicacionHelper.Desenvolver(null));
        }

        [Theory]
        [InlineData("Validation of viewstate MAC failed.", true)]
        [InlineData("The state information is invalid for this page and might be corrupted. ViewState", true)]
        [InlineData("Otro error HTTP", false)]
        public void EsErrorViewState_SoloHttpExceptionConViewState(string mensaje, bool esperado)
        {
            Assert.Equal(esperado, ErrorAplicacionHelper.EsErrorViewState(new HttpException(mensaje)));
        }

        [Fact]
        public void EsErrorViewState_ServidorEnEspanol_ConInternaViewStateException()
        {
            // Así llega con .NET en español: el mensaje no dice "ViewState".
            // ViewStateException no tiene constructor público con mensaje: se crea sin inicializar.
            var interna = (Exception)System.Runtime.Serialization.FormatterServices
                .GetUninitializedObject(typeof(System.Web.UI.ViewStateException));
            var ex = new HttpException("Error en la validación del MAC.", interna);
            Assert.True(ErrorAplicacionHelper.EsErrorViewState(ex));

            // ViewStateException más abajo en la cadena.
            var profunda = new HttpException("Error", new InvalidOperationException("capa", interna));
            Assert.True(ErrorAplicacionHelper.EsErrorViewState(profunda));
        }

        [Fact]
        public void EsErrorViewState_TextoEnEspanol_SinInterna()
        {
            var ex = new HttpException("Error en la validación del estado de vista MAC. Si esta aplicación...");
            Assert.True(ErrorAplicacionHelper.EsErrorViewState(ex));
        }

        [Fact]
        public void EsErrorViewState_OtrosTipos_NoCuentan()
        {
            Assert.False(ErrorAplicacionHelper.EsErrorViewState(new InvalidOperationException("ViewState")));
            Assert.False(ErrorAplicacionHelper.EsErrorViewState(null));
        }

        [Fact]
        public void EsValidacionDePeticion_SoloHttpRequestValidationException()
        {
            Assert.True(ErrorAplicacionHelper.EsValidacionDePeticion(new HttpRequestValidationException("valor <script>")));
            Assert.False(ErrorAplicacionHelper.EsValidacionDePeticion(new HttpException(500, "Falla")));
            Assert.False(ErrorAplicacionHelper.EsValidacionDePeticion(null));
        }

        [Fact]
        public void EsNoEncontrado_SoloHttp404()
        {
            Assert.True(ErrorAplicacionHelper.EsNoEncontrado(new HttpException(404, "No existe")));
            Assert.False(ErrorAplicacionHelper.EsNoEncontrado(new HttpException(500, "Falla")));
            Assert.False(ErrorAplicacionHelper.EsNoEncontrado(new InvalidOperationException("404")));
            Assert.False(ErrorAplicacionHelper.EsNoEncontrado(null));
        }

        // --- Postbacks parciales (UpdatePanel): Site.Master.ScriptManager1_AsyncPostBackError ---

        private static HttpException ErrorMacViewState()
        {
            var interna = (Exception)System.Runtime.Serialization.FormatterServices
                .GetUninitializedObject(typeof(System.Web.UI.ViewStateException));
            return new HttpException("Error en la validación del MAC.", interna);
        }

        [Fact]
        public void MensajePostbackParcial_ViewStateSinSesion_SesionVencida()
        {
            Assert.Equal("SGV:SESION_VENCIDA", ErrorAplicacionHelper.MensajePostbackParcial(ErrorMacViewState(), false));
        }

        [Fact]
        public void MensajePostbackParcial_ViewStateConSesion_PaginaVencida()
        {
            Assert.Equal("SGV:PAGINA_VENCIDA", ErrorAplicacionHelper.MensajePostbackParcial(ErrorMacViewState(), true));
            // También envuelto por la página en HttpUnhandledException.
            Assert.Equal("SGV:PAGINA_VENCIDA", ErrorAplicacionHelper.MensajePostbackParcial(
                new HttpUnhandledException("envoltura", ErrorMacViewState()), true));
        }

        [Fact]
        public void MensajePostbackParcial_ErrorDeNegocio_SuTexto()
        {
            var ex = new HttpUnhandledException("envoltura", new ErrorNegocioException("El lote ya está anulado."));
            Assert.Equal("El lote ya está anulado.", ErrorAplicacionHelper.MensajePostbackParcial(ex, true));
        }

        [Fact]
        public void MensajePostbackParcial_ErrorTecnico_GenericoNuncaSuMessage()
        {
            var ex = new InvalidOperationException("Invalid column name 'secreto' en una ruta del servidor");
            string mensaje = ErrorAplicacionHelper.MensajePostbackParcial(ex, true);
            Assert.Equal(MensajeError.Generico, mensaje);
            Assert.DoesNotContain("secreto", mensaje);
        }

        // --- Postback completo con ViewState vencido: Global.asax Application_Error ---

        [Fact]
        public void DestinoTrasErrorViewState_SinSesion_Login()
        {
            Assert.Equal("~/Views/Login.aspx?error=sesion",
                ErrorAplicacionHelper.DestinoTrasErrorViewState("POST", false, "~/Views/ListaDespachos.aspx", "?id=5"));
            Assert.Equal("~/Views/Login.aspx?error=sesion",
                ErrorAplicacionHelper.DestinoTrasErrorViewState("GET", false, "~/Views/ListaDespachos.aspx", ""));
        }

        [Theory]
        [InlineData("", "~/Views/ListaDespachos.aspx?aviso=pagina_vencida")]
        [InlineData("?id=5", "~/Views/ListaDespachos.aspx?id=5&aviso=pagina_vencida")]
        [InlineData("?id=5&aviso=pagina_vencida", "~/Views/ListaDespachos.aspx?id=5&aviso=pagina_vencida")]
        [InlineData("?aviso=otro&b=2", "~/Views/ListaDespachos.aspx?b=2&aviso=pagina_vencida")]
        public void DestinoTrasErrorViewState_PostConSesion_MismaPaginaConAvisoSinDuplicar(string query, string esperado)
        {
            Assert.Equal(esperado, ErrorAplicacionHelper.DestinoTrasErrorViewState("POST", true, "~/Views/ListaDespachos.aspx", query));
        }

        [Fact]
        public void DestinoTrasErrorViewState_GetConSesion_RutaSinQuery_SinBucle()
        {
            // Un GET con __VIEWSTATE en la query cuenta como postback: volver a la misma URL
            // repetiría el error para siempre (ERR_TOO_MANY_REDIRECTS).
            string destino = ErrorAplicacionHelper.DestinoTrasErrorViewState(
                "GET", true, "~/Views/ListaDespachos.aspx", "?__VIEWSTATE=abc&__EVENTTARGET=x");
            Assert.Equal("~/Views/ListaDespachos.aspx", destino);
        }

        [Fact]
        public void DestinoTrasErrorViewState_RutaRara_Login()
        {
            Assert.Equal("~/Views/Login.aspx?error=sesion", ErrorAplicacionHelper.DestinoTrasErrorViewState("POST", true, "//otro.sitio/x", ""));
            Assert.Equal("~/Views/Login.aspx?error=sesion", ErrorAplicacionHelper.DestinoTrasErrorViewState("POST", true, null, ""));
            Assert.Equal("~/Views/Login.aspx?error=sesion", ErrorAplicacionHelper.DestinoTrasErrorViewState("POST", true, "~//otro.sitio/x", ""));
        }

        // --- Envío más grande que maxRequestLength (tarea 14) ---

        /// <summary>HttpException con el WebEventCode que pone ASP.NET (su setter es interno).</summary>
        private static HttpException ConCodigoDeEvento(string mensaje, int codigo)
        {
            var ex = new HttpException(mensaje);
            var propiedad = typeof(HttpException).GetProperty("WebEventCode");
            var setter = propiedad?.GetSetMethod(true);
            if (setter != null)
                setter.Invoke(ex, new object[] { codigo });
            else
                typeof(HttpException).GetField("_webEventCode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(ex, codigo);
            Assert.Equal(codigo, ex.WebEventCode);
            return ex;
        }

        [Fact]
        public void EsEnvioDemasiadoGrande_PorCodigo3004_DirectaOEnvuelta()
        {
            HttpException grande = ConCodigoDeEvento("texto cualquiera", 3004);
            Assert.True(ErrorAplicacionHelper.EsEnvioDemasiadoGrande(grande));
            Assert.True(ErrorAplicacionHelper.EsEnvioDemasiadoGrande(new HttpUnhandledException("página", grande)));
            Assert.True(ErrorAplicacionHelper.EsEnvioDemasiadoGrande(new HttpException("capa", grande)));
            Assert.True(ErrorAplicacionHelper.EsEnvioDemasiadoGrande(
                ErrorAplicacionHelper.Desenvolver(new HttpUnhandledException("página", grande))));
        }

        [Fact]
        public void EsEnvioDemasiadoGrande_PorMensaje_EnInglesYEspanol()
        {
            Assert.True(ErrorAplicacionHelper.EsEnvioDemasiadoGrande(new HttpException("Maximum request length exceeded.")));
            Assert.True(ErrorAplicacionHelper.EsEnvioDemasiadoGrande(new HttpException("Se superó la longitud máxima de la solicitud.")));
        }

        [Fact]
        public void EsEnvioDemasiadoGrande_OtrosErrores_No()
        {
            Assert.False(ErrorAplicacionHelper.EsEnvioDemasiadoGrande(null));
            Assert.False(ErrorAplicacionHelper.EsEnvioDemasiadoGrande(new HttpException(404, "No existe")));
            Assert.False(ErrorAplicacionHelper.EsEnvioDemasiadoGrande(ConCodigoDeEvento("otro", 3005)));
            // El texto solo cuenta en una HttpException: una excepción de negocio con esas palabras no.
            Assert.False(ErrorAplicacionHelper.EsEnvioDemasiadoGrande(new InvalidOperationException("Maximum request length exceeded")));
        }

        [Theory]
        [InlineData("", "~/Views/RegistroDespacho.aspx?aviso=archivo_grande")]
        [InlineData("?id=5", "~/Views/RegistroDespacho.aspx?id=5&aviso=archivo_grande")]
        [InlineData("?id=5&aviso=pagina_vencida", "~/Views/RegistroDespacho.aspx?id=5&aviso=archivo_grande")]
        public void DestinoConAviso_ArchivoGrande_MismaPaginaPorGet(string query, string esperado)
        {
            Assert.Equal(esperado, ErrorAplicacionHelper.DestinoConAviso(
                "POST", true, "~/Views/RegistroDespacho.aspx", query, ErrorAplicacionHelper.AvisoArchivoGrande));
        }

        [Fact]
        public void DestinoConAviso_ArchivoGrande_SinSesionAlLogin_YGetSinBucle()
        {
            Assert.Equal("~/Views/Login.aspx?error=sesion", ErrorAplicacionHelper.DestinoConAviso(
                "POST", false, "~/Views/ListaDespachos.aspx", "", ErrorAplicacionHelper.AvisoArchivoGrande));
            Assert.Equal("~/Views/ListaDespachos.aspx", ErrorAplicacionHelper.DestinoConAviso(
                "GET", true, "~/Views/ListaDespachos.aspx", "?a=1", ErrorAplicacionHelper.AvisoArchivoGrande));
        }

        [Fact]
        public void TextoAviso_SoloValoresConocidos()
        {
            Assert.Equal("La página estaba desactualizada; su última acción no se guardó. Repítala.",
                ErrorAplicacionHelper.TextoAviso("pagina_vencida"));
            Assert.Equal("El envío supera el tamaño permitido. Suba los archivos por separado o más livianos.",
                ErrorAplicacionHelper.TextoAviso("archivo_grande"));
            Assert.Null(ErrorAplicacionHelper.TextoAviso("ARCHIVO_GRANDE"));
            Assert.Null(ErrorAplicacionHelper.TextoAviso("<script>alert(1)</script>"));
            Assert.Null(ErrorAplicacionHelper.TextoAviso("PAGINA_VENCIDA"));
            Assert.Null(ErrorAplicacionHelper.TextoAviso(""));
            Assert.Null(ErrorAplicacionHelper.TextoAviso(null));
        }

        [Fact]
        public void ErrorPostbackParcialYaManejado_SoloConLaMarcaDeSiteMaster()
        {
            var items = new System.Collections.Hashtable();
            Assert.False(ErrorAplicacionHelper.ErrorPostbackParcialYaManejado(items));
            Assert.False(ErrorAplicacionHelper.ErrorPostbackParcialYaManejado(null));

            items[ErrorAplicacionHelper.ClaveErrorPostbackParcialManejado] = "true";   // no es bool
            Assert.False(ErrorAplicacionHelper.ErrorPostbackParcialYaManejado(items));

            items[ErrorAplicacionHelper.ClaveErrorPostbackParcialManejado] = true;
            Assert.True(ErrorAplicacionHelper.ErrorPostbackParcialYaManejado(items));
        }

        [Fact]
        public void GlobalYSiteMaster_UsanLaMarcaDeErrorParcialManejado()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            string raiz = Path.Combine(dir.FullName, "WebSGV");

            string master = File.ReadAllText(Path.Combine(raiz, "Site.Master.cs"));
            Assert.Contains("Context.Items[ErrorAplicacionHelper.ClaveErrorPostbackParcialManejado] = true", master);
            string global = File.ReadAllText(Path.Combine(raiz, "Global.asax.cs"));
            Assert.Contains("ErrorAplicacionHelper.ErrorPostbackParcialYaManejado(Context.Items)", global);
        }

        [Theory]
        [InlineData("MicrosoftAjax.js")]
        [InlineData("MicrosoftAjaxWebForms.js")]
        public void MsAjax_SeSirveDesdeElEnsambladoConSusTextos(string script)
        {
            // Los archivos estáticos del paquete MSAjax no traen Sys.Res / Sys.WebForms.Res: un error de
            // postback parcial reventaba dentro de MS Ajax y la capa "Procesando..." se quedaba fija.
            BundleConfig.RegisterJQueryScriptManager();
            Assert.True(BundleConfig.MsAjaxSeSirveDesdeEnsamblado(script));
        }

        [Fact]
        public void SiteMaster_ScriptManagerManejaErroresDePostbackParcial()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            string raiz = Path.Combine(dir.FullName, "WebSGV");

            string master = File.ReadAllText(Path.Combine(raiz, "Site.Master"));
            Assert.Contains("AllowCustomErrorsRedirect=\"false\"", master);
            Assert.Contains("OnAsyncPostBackError=\"ScriptManager1_AsyncPostBackError\"", master);
            Assert.Contains("avisoInicial: '<%= System.Web.HttpUtility.JavaScriptStringEncode(AvisoInicial) %>'", master);
            Assert.Contains("ErrorAplicacionHelper.TextoPaginaVencida", master);

            string js = File.ReadAllText(Path.Combine(raiz, "Scripts", "paginas", "SiteMaster-2.js"));
            Assert.Contains("set_errorHandled(true)", js);
            Assert.Contains(ErrorAplicacionHelper.CodigoSesionVencida, js);
            Assert.Contains(ErrorAplicacionHelper.CodigoPaginaVencida, js);
            Assert.Contains("SGV.avisoInicial", js);
            Assert.Contains("SGV.textoPaginaVencida", js);
        }
    }
}
