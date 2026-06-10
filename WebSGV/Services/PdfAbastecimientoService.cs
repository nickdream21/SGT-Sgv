using System;
using System.Globalization;
using System.IO;
using System.Web.Hosting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebSGV.Helpers;

namespace WebSGV.Services
{
    /// <summary>
    /// DTO consumido por <see cref="PdfAbastecimientoService"/>. La capa web
    /// llena estas propiedades a partir de la fila de AbastecimientoCombustible
    /// ya insertada en BD.
    /// </summary>
    public sealed class DetalleAbastecimientoPdf
    {
        public int IdAbastecimientoCombustible { get; set; }
        public string NumeroAbastecimiento { get; set; }

        public string TipoUnidad { get; set; }      // Tracto / Volquete / Camioneta / Otro
        public string Placa { get; set; }
        public string PlacaCarreta { get; set; }
        public string Conductor { get; set; }
        public string Ruta { get; set; }
        public string Producto { get; set; }
        public string LugarAbastecimiento { get; set; }
        public string TipoAbastecimiento { get; set; }

        public DateTime FechaHora { get; set; }
        public TimeSpan? HoraRetorno { get; set; }

        public decimal GalonesRutaAsignada { get; set; }
        public decimal GalonesCompradosRuta { get; set; }
        public decimal GalonesTotalAbastecidos { get; set; }
        public decimal GalonesAlFinalizar { get; set; }
        public decimal GalonesTotalConsumidos { get; set; }

        public decimal PrecioDolar { get; set; }
        public decimal MontoTotal { get; set; }
        public decimal DistanciaKm { get; set; }
        public decimal ConsumoComputador { get; set; }
        public decimal RendimientoPromedio { get; set; }

        public string Observaciones { get; set; }
        public string UsuarioRegistra { get; set; }
    }

    /// <summary>
    /// Generador de PDF del "Parte de Abastecimiento de Combustible para
    /// Unidades de Transporte" (formato controlado SGV-CDF-F-06).
    /// Reemplaza al talonario pre-impreso. Se invoca al guardar el registro
    /// o bajo demanda desde un botón de descarga.
    /// </summary>
    public sealed class PdfAbastecimientoService
    {
        // Paleta corporativa (alineada con PdfOrdenViajeService).
        private static readonly Color AZUL_CORP   = Color.FromHex("#0B3D91");
        private static readonly Color GRIS_TEXTO  = Color.FromHex("#1F2937");
        private static readonly Color GRIS_SECUND = Color.FromHex("#6B7280");
        private static readonly Color GRIS_BORDE  = Color.FromHex("#D1D5DB");
        private static readonly Color AZUL_CLARO  = Color.FromHex("#E8EEF7");
        private static readonly Color GRIS_FONDO  = Color.FromHex("#F3F4F6");
        private static readonly Color FONDO_OBS   = Color.FromHex("#FAFAFA");

        private const string FUENTE = "Arial";

        static PdfAbastecimientoService()
        {
            // Licencia Community (empresa con ingresos < 1M USD anuales).
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // =====================================================================
        // API PÚBLICA
        // =====================================================================

        /// <summary>
        /// Genera el PDF del abastecimiento. Si <paramref name="archivar"/> es
        /// true, lo guarda en <c>~/App_Data/Abastecimientos/AAAA/MM/ABT-NNNNNN.pdf</c>.
        /// </summary>
        public ResultadoGeneracionPdf GenerarYArchivar(
            DetalleAbastecimientoPdf d,
            bool archivar = false)
        {
            if (d == null) throw new ArgumentNullException("d");

            var empresa = EmpresaConfigHelper.ObtenerEmpresa();
            var formato = EmpresaConfigHelper.ObtenerFormato("SGV-CDF-F-06");

            byte[] bytes = ConstruirPdf(d, empresa, formato);
            string hash = HashHelper.ComputeSha256(bytes);

            var resultado = new ResultadoGeneracionPdf
            {
                Bytes = bytes,
                Hash = hash
            };

            if (archivar)
            {
                string ruta = ArchivarEnDisco(d.NumeroAbastecimiento ?? d.IdAbastecimientoCombustible.ToString(), bytes);
                resultado.RutaFisica = ruta;
                resultado.RutaRelativa = ConvertirARelativa(ruta);
            }

            return resultado;
        }

        // =====================================================================
        // CONSTRUCCIÓN
        // =====================================================================

        private byte[] ConstruirPdf(DetalleAbastecimientoPdf d, EmpresaInfo emp, FormatoControladoInfo fmt)
        {
            return Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(34);
                    page.MarginTop(34);
                    page.MarginBottom(12);
                    page.DefaultTextStyle(t => t.FontFamily(FUENTE).FontSize(9.5f).FontColor(GRIS_TEXTO));

                    page.Content().Column(col =>
                    {
                        col.Item().Element(c => ComponerEncabezadoControlado(c, emp, fmt));
                        col.Item().PaddingTop(6).PaddingBottom(8).Element(c => ComponerSubHeader(c, d, emp));

                        col.Item().Element(c => ComponerTituloSeccion(c, 1, "Información de la Unidad y Conductor"));
                        col.Item().PaddingBottom(6).Element(c => ComponerInfoUnidad(c, d));

                        col.Item().Element(c => ComponerTituloSeccion(c, 2, "Control de Combustible"));
                        col.Item().PaddingBottom(6).Element(c => ComponerTablaCombustible(c, d));

                        col.Item().Element(c => ComponerTituloSeccion(c, 3, "Consumo y Rendimiento"));
                        col.Item().PaddingBottom(6).Element(c => ComponerConsumoRendimiento(c, d));

                        if (!string.IsNullOrWhiteSpace(d.Observaciones))
                        {
                            col.Item().Element(c => ComponerTituloSeccion(c, 4, "Observaciones"));
                            col.Item().PaddingBottom(6).Element(c => ComponerCajaObservaciones(c, d.Observaciones));
                        }

                        col.Item().PaddingTop(10).ShowEntire().Element(c => ComponerConstanciaYFirmas(c, d));
                    });

                    page.Footer().Element(c => ComponerPieDePagina(c, fmt));
                });
            }).GeneratePdf();
        }

        // ---------- Encabezado ----------
        private void ComponerEncabezadoControlado(IContainer cont, EmpresaInfo emp, FormatoControladoInfo fmt)
        {
            cont.Row(row =>
            {
                row.RelativeItem(1.4f).Border(1.2f).BorderColor(AZUL_CORP).Padding(6)
                    .AlignCenter().AlignMiddle().Element(logo =>
                    {
                        string logoPath = null;
                        try { logoPath = HostingEnvironment.MapPath("~/Content/favicon.png"); }
                        catch { /* fuera de contexto web */ }

                        if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                            logo.MaxHeight(55).MaxWidth(55).Image(logoPath).FitArea();
                        else
                            logo.Text("SGV").Bold().FontSize(14).FontColor(AZUL_CORP);
                    });

                row.RelativeItem(5.6f)
                    .BorderTop(1.2f).BorderBottom(1.2f).BorderColor(AZUL_CORP)
                    .Padding(6).AlignMiddle().Column(c =>
                    {
                        c.Item().AlignCenter().Text((emp.RazonSocial ?? "").ToUpper(CultureInfo.CurrentCulture))
                            .Bold().FontSize(12).FontColor(AZUL_CORP);
                        c.Item().AlignCenter().Text(emp.Rubro ?? "").FontSize(9.5f).FontColor(GRIS_SECUND);
                        c.Item().PaddingTop(4).AlignCenter().Text("PARTE DE ABASTECIMIENTO DE COMBUSTIBLE")
                            .Bold().FontSize(12);
                        c.Item().AlignCenter().Text("PARA UNIDADES DE TRANSPORTE")
                            .Bold().FontSize(9.5f).FontColor(GRIS_SECUND);
                    });

                row.RelativeItem(2.3f).Border(1.2f).BorderColor(AZUL_CORP).Column(c =>
                {
                    AgregarFilaFormato(c, "Código",   fmt != null ? fmt.CodigoFormato : "SGV-CDF-F-06");
                    AgregarFilaFormato(c, "Versión",  fmt != null ? fmt.Version       : "01");
                    AgregarFilaFormato(c, "Vigencia", fmt != null ? fmt.FechaVigencia.ToString("dd/MM/yyyy") : "01/01/2025");
                    AgregarFilaFormato(c, "Página",   "1 de 1");
                });
            });
        }

        private void AgregarFilaFormato(ColumnDescriptor col, string lbl, string val)
        {
            col.Item().BorderBottom(0.3f).BorderColor(GRIS_BORDE).Padding(3).Row(r =>
            {
                r.RelativeItem(1.1f).AlignMiddle().Text(lbl.ToUpperInvariant())
                    .Bold().FontSize(7.5f).FontColor(GRIS_SECUND);
                r.RelativeItem(1f).AlignRight().AlignMiddle().Text(val)
                    .Bold().FontSize(8.5f).FontColor(AZUL_CORP);
            });
        }

        // ---------- Sub-header ----------
        private void ComponerSubHeader(IContainer cont, DetalleAbastecimientoPdf d, EmpresaInfo emp)
        {
            cont.Row(row =>
            {
                row.RelativeItem(3f).Padding(2).Column(c =>
                {
                    c.Item().Text("RUC: " + (emp.Ruc ?? "")).FontSize(9).FontColor(GRIS_SECUND);
                    c.Item().Text(emp.DomicilioFiscal ?? "").FontSize(8.5f).FontColor(GRIS_SECUND);
                });

                row.RelativeItem(1.5f).Border(1.2f).BorderColor(AZUL_CORP).Padding(6).Column(c =>
                {
                    c.Item().AlignCenter().Text("N° DE PARTE").Bold().FontSize(8).FontColor(GRIS_SECUND);
                    c.Item().AlignCenter().Text(d.NumeroAbastecimiento ?? "-")
                        .Bold().FontSize(14).FontColor(AZUL_CORP);
                });
            });
        }

        // ---------- Títulos ----------
        private void ComponerTituloSeccion(IContainer cont, int numero, string titulo)
        {
            cont.PaddingTop(4).Background(AZUL_CORP).Padding(5)
                .Text(numero + ". " + titulo.ToUpper()).Bold().FontSize(10).FontColor(Colors.White);
        }

        // ---------- Sección 1: Info ----------
        private void ComponerInfoUnidad(IContainer cont, DetalleAbastecimientoPdf d)
        {
            cont.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(2.2f);
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(2.2f);
                });

                AgregarParLabelVal(table, "Tipo Unidad",   NV(d.TipoUnidad));
                AgregarParLabelVal(table, "Placa",         NV(d.Placa));
                AgregarParLabelVal(table, "Carreta",       NV(d.PlacaCarreta));
                AgregarParLabelVal(table, "Conductor",     NV(d.Conductor));
                AgregarParLabelVal(table, "Fecha",         d.FechaHora.ToString("dd/MM/yyyy"));
                AgregarParLabelVal(table, "Hora",          d.FechaHora.ToString("HH:mm"));
                AgregarParLabelVal(table, "Ruta",          NV(d.Ruta));
                AgregarParLabelVal(table, "Producto",      NV(d.Producto));
                AgregarParLabelVal(table, "Lugar Abast.",  NV(d.LugarAbastecimiento));
                AgregarParLabelVal(table, "Tipo Registro", NV(d.TipoAbastecimiento));
                AgregarParLabelVal(table, "Hora Retorno",  d.HoraRetorno.HasValue ? d.HoraRetorno.Value.ToString(@"hh\:mm") : "-");
                AgregarParLabelVal(table, "Registra",      NV(d.UsuarioRegistra));
            });
        }

        private void AgregarParLabelVal(TableDescriptor table, string lbl, string val)
        {
            table.Cell().Background(GRIS_FONDO).Border(0.4f).BorderColor(GRIS_BORDE).Padding(4).AlignMiddle()
                .Text(lbl.ToUpper()).Bold().FontSize(8).FontColor(GRIS_SECUND);
            table.Cell().Border(0.4f).BorderColor(GRIS_BORDE).Padding(4).AlignMiddle()
                .Text(val ?? "-").FontSize(9);
        }

        // ---------- Sección 2: Tabla combustible ----------
        private void ComponerTablaCombustible(IContainer cont, DetalleAbastecimientoPdf d)
        {
            cont.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3f);
                    c.RelativeColumn(2f);
                });

                AgregarFilaDato(table, "GL asignados a la ruta",              d.GalonesRutaAsignada,   " gal");
                AgregarFilaDato(table, "GL rellenados / abastecidos",         d.GalonesTotalAbastecidos, " gal");
                AgregarFilaDato(table, "GL comprados en ruta",                d.GalonesCompradosRuta,  " gal");
                AgregarFilaDato(table, "GL al finalizar (trae al retornar)",  d.GalonesAlFinalizar,    " gal");
                AgregarFilaDato(table, "GL total consumidos",                 d.GalonesTotalConsumidos," gal", destaque: true);
            });
        }

        private void AgregarFilaDato(TableDescriptor table, string lbl, decimal val, string sufijo = "", bool destaque = false)
        {
            table.Cell().Background(GRIS_FONDO).Border(0.4f).BorderColor(GRIS_BORDE).Padding(5).AlignMiddle()
                .Text(lbl).Bold().FontSize(9).FontColor(GRIS_SECUND);

            IContainer celVal = table.Cell();
            if (destaque) celVal = celVal.Background(AZUL_CLARO);
            celVal = celVal.Border(0.4f).BorderColor(GRIS_BORDE).Padding(5).AlignRight().AlignMiddle();

            string texto = val.ToString("N2", CultureInfo.GetCultureInfo("es-PE")) + sufijo;
            if (destaque)
                celVal.Text(texto).Bold().FontSize(10.5f).FontColor(AZUL_CORP);
            else
                celVal.Text(texto).FontSize(9.5f);
        }

        // ---------- Sección 3: Consumo y rendimiento ----------
        private void ComponerConsumoRendimiento(IContainer cont, DetalleAbastecimientoPdf d)
        {
            cont.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3f);
                    c.RelativeColumn(2f);
                });

                AgregarFilaDato(table, "Precio del dólar (S/. por USD)", d.PrecioDolar, "");
                AgregarFilaDato(table, "Monto total abastecido (USD)",   d.MontoTotal, " USD");
                AgregarFilaDato(table, "Distancia recorrida",            d.DistanciaKm, " km");
                AgregarFilaDato(table, "Consumo según computador",       d.ConsumoComputador, " gal");
                AgregarFilaDato(table, "Rendimiento promedio",           d.RendimientoPromedio, " km/gal", destaque: true);
            });
        }

        // ---------- Observaciones ----------
        private void ComponerCajaObservaciones(IContainer cont, string texto)
        {
            cont.Border(0.4f).BorderColor(GRIS_BORDE).Background(FONDO_OBS)
                .MinHeight(26).Padding(6).Text(texto).FontSize(9);
        }

        // ---------- Constancia y firmas ----------
        private void ComponerConstanciaYFirmas(IContainer cont, DetalleAbastecimientoPdf d)
        {
            var constancia = "Documento generado electrónicamente por el Sistema de Gestión de Viajes (SGV). " +
                             "Reemplaza al talonario físico bajo el formato controlado SGV-CDF-F-06. " +
                             "Fecha de emisión: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + ".";

            cont.Column(col =>
            {
                col.Item().Border(0.4f).BorderColor(GRIS_BORDE).Background(FONDO_OBS).Padding(6)
                    .Text(constancia).FontSize(8).FontColor(GRIS_SECUND);

                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => ComponerCeldaFirma(c, "FIRMA ABASTECEDOR"));
                    row.RelativeItem().Element(c => ComponerCeldaFirma(c, "FIRMA CONDUCTOR / " + NV(d.Conductor).ToUpper()));
                });
            });
        }

        private void ComponerCeldaFirma(IContainer cont, string etiqueta)
        {
            cont.Border(0.4f).BorderColor(GRIS_BORDE).Padding(6).MinHeight(55).AlignBottom().Column(c =>
            {
                c.Item().AlignCenter().Text("________________________________")
                    .FontSize(9).FontColor(GRIS_SECUND);
                c.Item().AlignCenter().Text(etiqueta).Bold().FontSize(8).FontColor(GRIS_SECUND);
            });
        }

        // =====================================================================
        // PIE DE PÁGINA
        // =====================================================================

        private void ComponerPieDePagina(IContainer cont, FormatoControladoInfo fmt)
        {
            var codigo = fmt != null ? fmt.CodigoFormato : "SGV-CDF-F-06";
            var ver = fmt != null ? fmt.Version : "01";

            cont.PaddingTop(6).AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(GRIS_SECUND));
                t.Span(codigo + " v" + ver + "   |   Generado el " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + "   |   Pág. ");
                t.CurrentPageNumber();
            });
        }

        // =====================================================================
        // UTILIDADES
        // =====================================================================

        private static string NV(string s) => string.IsNullOrWhiteSpace(s) ? "-" : s.Trim();

        private string ArchivarEnDisco(string numero, byte[] bytes)
        {
            string raiz = HostingEnvironment.MapPath("~/App_Data/Abastecimientos")
                          ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Abastecimientos");

            var hoy = DateTime.Now;
            string carpeta = Path.Combine(raiz,
                hoy.Year.ToString("0000"),
                hoy.Month.ToString("00"));
            Directory.CreateDirectory(carpeta);

            string nombreLimpio = (numero ?? "SIN-NUMERO");
            foreach (char c in Path.GetInvalidFileNameChars())
                nombreLimpio = nombreLimpio.Replace(c.ToString(), "");

            string nombreArchivo = "ABT-" + nombreLimpio + ".pdf";
            string ruta = Path.Combine(carpeta, nombreArchivo);

            File.WriteAllBytes(ruta, bytes);
            return ruta;
        }

        private string ConvertirARelativa(string rutaFisica)
        {
            if (string.IsNullOrEmpty(rutaFisica)) return null;
            string appData = HostingEnvironment.MapPath("~/App_Data")
                             ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data");
            if (!rutaFisica.StartsWith(appData, StringComparison.OrdinalIgnoreCase))
                return rutaFisica;
            string tail = rutaFisica.Substring(appData.Length).TrimStart('\\', '/');
            return "~/App_Data/" + tail.Replace('\\', '/');
        }
    }
}
