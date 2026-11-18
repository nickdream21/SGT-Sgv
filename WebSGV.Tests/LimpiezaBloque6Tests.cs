using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Exportacion;
using WebSGV.Tests.Integracion;
using WebSGV.Views;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Bloque 6, B6-8 (H8–H10): el adjunto de MovimientosViaje se valida por su tipo real, los avisos del
    /// seguimiento de exportación salen de códigos fijos (no de un texto libre en ?msg=), las exportaciones
    /// GET de ReportesOrdenesViaje responden texto en vez de ir a Error.aspx, su aviso no queda pegado
    /// entre búsquedas y ReporteGps no guarda los Excel en la sesión.
    /// </summary>
    public class LimpiezaBloque6Tests
    {
        private static string Fuente(params string[] ruta) =>
            File.ReadAllText(Path.Combine(BaseDePruebas.RaizRepo, Path.Combine(ruta)));

        // ---------- Avisos del seguimiento: códigos fijos ----------

        [Theory]
        [InlineData(AvisoSeguimiento.Creado, "Viaje creado. Usa \"Actualizar GPS\" para traer sus horas.")]
        [InlineData(AvisoSeguimiento.DatosGuardados, "Datos del viaje guardados.")]
        [InlineData(AvisoSeguimiento.ManualesGuardados, "Datos guardados.")]
        [InlineData(AvisoSeguimiento.Cerrado, "Viaje cerrado.")]
        [InlineData(AvisoSeguimiento.Reabierto, "Viaje reabierto.")]
        public void Aviso_de_un_codigo_conocido_es_su_texto_fijo(string codigo, string esperado)
        {
            Assert.Equal(esperado, AvisoSeguimiento.Texto(codigo));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Viaje cerrado.")]
        [InlineData("Su sesión venció: ingrese su contraseña en www.otro-sitio.com")]
        [InlineData("CERRADO")]
        [InlineData("cerrado ")]
        [InlineData("<script>alert(1)</script>")]
        public void Aviso_de_un_codigo_desconocido_o_texto_libre_no_muestra_nada(string codigo)
        {
            Assert.Null(AvisoSeguimiento.Texto(codigo));
            Assert.Null(AvisoSeguimiento.Texto(codigo, "fhIngresoPlanta"));
        }

        [Fact]
        public void Aviso_de_hora_corregida_nombra_solo_un_hito_conocido()
        {
            var hito = HitosSeguimiento.Buscar("fhIngresoPlanta");
            Assert.NotNull(hito);
            Assert.Equal(hito.Nombre + ": hora corregida.", AvisoSeguimiento.Texto(AvisoSeguimiento.HoraCorregida, "fhIngresoPlanta"));
            Assert.Equal("Hito: hora corregida.", AvisoSeguimiento.Texto(AvisoSeguimiento.HoraCorregida, "Mensaje falso"));
            Assert.Equal("Hito: hora corregida.", AvisoSeguimiento.Texto(AvisoSeguimiento.HoraCorregida));
        }

        [Fact]
        public void Seguimiento_no_muestra_el_texto_del_querystring_ni_lo_envia()
        {
            foreach (string pagina in new[] { "SeguimientoViaje.aspx.cs", "RegistroSeguimiento.aspx.cs" })
            {
                string codigo = Fuente("WebSGV", "Views", "Exportacion", pagina);
                Assert.DoesNotContain("MostrarMensaje(Request.QueryString[\"msg\"]", codigo);
                Assert.Contains("AvisoSeguimiento.Texto(Request.QueryString[\"msg\"]", codigo);
                Assert.DoesNotContain("QueryString[\"tipo\"]", codigo);
            }

            // Cada vuelta a SeguimientoViaje pasa una constante de AvisoSeguimiento, nunca un texto entre comillas.
            string viaje = Fuente("WebSGV", "Views", "Exportacion", "SeguimientoViaje.aspx.cs");
            var llamadas = Regex.Matches(viaje, @"\bVolver\((?!int )[^;]*;").Cast<Match>().Select(m => m.Value).ToList();
            Assert.NotEmpty(llamadas);
            Assert.All(llamadas, l =>
            {
                Assert.DoesNotContain("\"", l);
                Assert.Contains("AvisoSeguimiento.", l);
            });
        }

        [Fact]
        public void Ninguna_pagina_redirige_al_seguimiento_con_un_texto_en_msg()
        {
            string raizWeb = Path.Combine(BaseDePruebas.RaizRepo, "WebSGV");
            var archivos = Directory.GetFiles(Path.Combine(raizWeb, "Views"), "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(Path.Combine(raizWeb, "Scripts", "paginas"), "*.js", SearchOption.AllDirectories));
            foreach (string archivo in archivos)
            {
                foreach (string linea in File.ReadAllLines(archivo))
                {
                    if (!Regex.IsMatch(linea, @"(RegistroSeguimiento|SeguimientoViaje)\.aspx\?[^""']*msg=")) continue;
                    // La única que lo arma es SeguimientoViaje.Volver, con el código ya validado.
                    Assert.Contains("HttpUtility.UrlEncode(codigoAviso)", linea);
                }
            }
        }

        // ---------- Adjunto de MovimientosViaje ----------

        private static readonly byte[] Pdf = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 };
        private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0 };
        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10 };
        private static readonly byte[] Exe = { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00 };

        [Fact]
        public void Adjunto_PDF_o_foto_verdaderos_se_aceptan()
        {
            Assert.Null(MovimientosViaje.ValidarAdjunto("yape.pdf", 1000, Pdf));
            Assert.Null(MovimientosViaje.ValidarAdjunto("yape.PNG", 1000, Png));
            Assert.Null(MovimientosViaje.ValidarAdjunto("yape.jpeg", 1000, Jpeg));
            // Celulares que guardan PNG como .jpg: se ve igual.
            Assert.Null(MovimientosViaje.ValidarAdjunto("yape.jpg", 1000, Png));
            Assert.Null(MovimientosViaje.ValidarAdjunto("yape.pdf", MovimientosViaje.MaxBytesAdjunto, Pdf));
        }

        [Fact]
        public void Adjunto_renombrado_se_rechaza_por_su_tipo_real()
        {
            Assert.Equal("Adjunto: el archivo no es un PDF (está dañado o le cambiaron la extensión).",
                MovimientosViaje.ValidarAdjunto("yape.pdf", 1000, Exe));
            Assert.Contains("no es un JPG", MovimientosViaje.ValidarAdjunto("yape.jpg", 1000, Exe));
            Assert.Contains("no es un PDF", MovimientosViaje.ValidarAdjunto("yape.pdf", 1000, Png));
        }

        [Fact]
        public void Adjunto_de_otro_tipo_grande_o_vacio_se_rechaza()
        {
            Assert.Equal("Adjunto: tipo de archivo no permitido. Use PDF, JPG o PNG.",
                MovimientosViaje.ValidarAdjunto("yape.exe", 1000, Exe));
            Assert.StartsWith("Adjunto: tipo de archivo no permitido", MovimientosViaje.ValidarAdjunto("yape.docx", 1000, new byte[] { 0x50, 0x4B, 0x03, 0x04 }));
            Assert.Equal("Adjunto: el archivo supera el tamaño máximo permitido (5 MB).",
                MovimientosViaje.ValidarAdjunto("yape.pdf", MovimientosViaje.MaxBytesAdjunto + 1L, Pdf));
            Assert.Equal("Adjunto: el archivo está vacío.", MovimientosViaje.ValidarAdjunto("yape.pdf", 0, new byte[0]));
        }

        [Fact]
        public void MovimientosViaje_valida_el_adjunto_antes_de_guardarlo_y_no_esta_en_un_UpdatePanel()
        {
            string codigo = Fuente("WebSGV", "Views", "MovimientosViaje.aspx.cs");
            int validar = codigo.IndexOf("ValidarAdjunto(fuAdjunto.PostedFile)", StringComparison.Ordinal);
            int guardar = codigo.IndexOf("GuardarAdjunto(fuAdjunto,", StringComparison.Ordinal);
            Assert.True(validar > 0 && guardar > validar, "El adjunto se valida por su tipo real antes de guardarlo.");

            // Sin UpdatePanel: el FileUpload sube con el postback normal (no hace falta PostBackTrigger).
            Assert.DoesNotContain("UpdatePanel", Fuente("WebSGV", "Views", "MovimientosViaje.aspx"));
        }

        // ---------- Exportaciones GET de ReportesOrdenesViaje ----------

        [Fact]
        public void Codigo_http_400_para_negocio_y_500_para_error_tecnico()
        {
            Assert.Equal(400, MensajeErrorHelper.CodigoHttp(new ErrorNegocioException("Sin datos.")));
            Assert.Equal(400, MensajeErrorHelper.CodigoHttp(new Exception("envoltorio", new ErrorNegocioException("Sin datos."))));
            Assert.Equal(500, MensajeErrorHelper.CodigoHttp(new InvalidOperationException("Connection is not open.")));
            Assert.Equal(500, MensajeErrorHelper.CodigoHttp(null));
        }

        [Fact]
        public void ParaUsuario_con_contexto_muestra_solo_mensajes_de_negocio()
        {
            Assert.Equal("Sin datos.", MensajeErrorHelper.ParaUsuario(new ErrorNegocioException("Sin datos."), "Exportando"));
            Assert.Equal(MensajeError.Generico,
                MensajeErrorHelper.ParaUsuario(new Exception("Invalid column name 'x'. Cannot open database"), "Exportando"));
        }

        [Fact]
        public void ReportesOrdenesViaje_no_relanza_los_errores_de_las_exportaciones()
        {
            string codigo = Fuente("WebSGV", "Views", "ReportesOrdenesViaje.aspx.cs");
            Assert.DoesNotContain("throw;", codigo);
            Assert.Equal(3, Regex.Matches(codigo, @"ResponderErrorExportacion\(ex, """).Count);
            Assert.Contains("MensajeErrorHelper.ParaUsuario(ex, contexto)", codigo);
            Assert.Contains("TrySkipIisCustomErrors = true", codigo);
        }

        [Fact]
        public void ReportesOrdenesViaje_el_aviso_no_queda_pegado_entre_busquedas()
        {
            string aspx = Fuente("WebSGV", "Views", "ReportesOrdenesViaje.aspx");
            Assert.Matches(@"<asp:Panel ID=""pnlMensajes""[^>]*EnableViewState=""false""", aspx);
        }

        // ---------- ReporteGps ----------

        [Fact]
        public void ReporteGps_no_guarda_los_excel_en_la_sesion()
        {
            string codigo = Fuente("WebSGV", "Views", "ReporteGps.aspx.cs");
            int pasar = codigo.IndexOf("PasarATemporales(resultados);", StringComparison.Ordinal);
            int sesion = codigo.IndexOf("Session[SesionResultados] = resultados;", StringComparison.Ordinal);
            Assert.True(pasar > 0 && sesion > pasar, "Los Excel pasan al temporal antes de guardar la lista en la sesión.");
            Assert.Contains("ArchivosTemporalesHelper.ReporteGps", codigo);
            Assert.Contains("ArchivosTemporalesHelper.Borrar(", codigo);
            Assert.Contains("(bool)Eval(\"TieneArchivo\")", Fuente("WebSGV", "Views", "ReporteGps.aspx"));
        }
    }
}
