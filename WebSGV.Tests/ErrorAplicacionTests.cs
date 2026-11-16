using System;
using System.Web;
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
        public void EsNoEncontrado_SoloHttp404()
        {
            Assert.True(ErrorAplicacionHelper.EsNoEncontrado(new HttpException(404, "No existe")));
            Assert.False(ErrorAplicacionHelper.EsNoEncontrado(new HttpException(500, "Falla")));
            Assert.False(ErrorAplicacionHelper.EsNoEncontrado(new InvalidOperationException("404")));
            Assert.False(ErrorAplicacionHelper.EsNoEncontrado(null));
        }
    }
}
