using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E4-7: todo Excel subido pasa por <see cref="ExcelSubidoHelper.Validar(string, long, Stream, long, long, int, double)"/>
    /// antes de que ClosedXML lo cargue entero: .xlsx de verdad, tamaño y sin "zip bomb" (tamaño
    /// declarado, razón de compresión, número de entradas y bytes realmente descomprimidos).
    /// Los ZIP inflados se arman en memoria (ceros comprimidos en streaming): nada se escribe a disco.
    /// </summary>
    public class ExcelSubidoTests
    {
        private const int MB = 1024 * 1024;
        private const long Max = 10 * MB;

        private static byte[] XlsxReal()
        {
            using (var wb = new XLWorkbook())
            using (var ms = new MemoryStream())
            {
                wb.Worksheets.Add("Hoja1").Cell(1, 1).Value = "PEDIDO";
                wb.SaveAs(ms);
                return ms.ToArray();
            }
        }

        private static void Validar(string nombre, byte[] contenido, long maxDescomprimido = ExcelSubidoHelper.MaxDescomprimido,
                                    int maxEntradas = ExcelSubidoHelper.MaxEntradas)
        {
            using (var ms = new MemoryStream(contenido))
                ExcelSubidoHelper.Validar(nombre, contenido.Length, ms, Max, maxDescomprimido, maxEntradas);
        }

        private static string Rechazo(string nombre, byte[] contenido, long maxDescomprimido = ExcelSubidoHelper.MaxDescomprimido,
                                      int maxEntradas = ExcelSubidoHelper.MaxEntradas) =>
            Assert.Throws<ErrorNegocioException>(() => Validar(nombre, contenido, maxDescomprimido, maxEntradas)).Message;

        [Fact]
        public void XlsxDeClosedXml_SeAceptaYQuedaListoParaAbrirse()
        {
            byte[] x = XlsxReal();
            using (var ms = new MemoryStream(x))
            {
                ms.Position = 7;
                ExcelSubidoHelper.Validar("LISTA DE RECEPCION.XLSX", x.Length, ms, Max);
                Assert.Equal(0, ms.Position);   // la página lo lee o lo guarda justo después
                using (var wb = new XLWorkbook(ms))
                    Assert.Equal("PEDIDO", wb.Worksheet(1).Cell(1, 1).GetString());
            }
        }

        [Fact]
        public void Inflado_CientosDeMbDeclarados_SeRechazaSinAbrirlo()
        {
            // 200 MB de ceros: el ZIP pesa unos 200 KB, pasa el tope de 10 MB del archivo y se frena
            // por lo que declara el directorio central.
            byte[] bomba = ImportacionZipBombTests.Zip(("[Content_Types].xml", 100), ("xl/worksheets/sheet1.xml", 200 * MB));
            Assert.True(bomba.Length < Max);
            string error = Rechazo("bomba.xlsx", bomba);
            Assert.Contains("demasiado grande", error);
            Assert.Contains("30 MB", error);
        }

        [Fact]
        public void Inflado_RazonDeCompresionEnorme_SeRechazaAunqueEsteBajoElTope()
        {
            // 5 MB de ceros (bajo los 30 MB) que crecen más de mil veces: un Excel real no pasa de 20.
            byte[] bomba = ImportacionZipBombTests.Zip(("xl/worksheets/sheet1.xml", 5 * MB));
            Assert.Contains("comprimido de forma anómala", Rechazo("ceros.xlsx", bomba));
        }

        [Fact]
        public void Inflado_QueMienteSuTamano_SeFrenaPorLosBytesRealmenteLeidos()
        {
            // Declara 10 bytes y trae 3 MB: el directorio central no lo delata, la lectura sí.
            byte[] mentiroso = ImportacionZipBombTests.MentirTamano(ImportacionZipBombTests.Zip(("xl/worksheets/sheet1.xml", 3 * MB)), 10);
            Assert.Contains("demasiado grande", Rechazo("mentiroso.xlsx", mentiroso, maxDescomprimido: 1 * MB));
        }

        [Fact]
        public void DemasiadasEntradas_SeRechaza()
        {
            byte[] x = XlsxReal();   // ClosedXML deja unas 10 partes
            Validar("ok.xlsx", x, maxEntradas: 50);
            Assert.Contains("demasiadas partes", Rechazo("partes.xlsx", x, maxEntradas: 3));
        }

        [Theory]
        [InlineData("lista.xls")]
        [InlineData("lista.XLS")]
        public void Xls_SeRechazaConComoConvertirlo(string nombre)
        {
            byte[] ole = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
            string error = Rechazo(nombre, ole);
            Assert.Contains("Excel 97-2003", error);
            Assert.Contains("Guardar como", error);
        }

        [Theory]
        [InlineData("lista.csv")]
        [InlineData("lista.pdf")]
        [InlineData("lista")]
        public void OtraExtension_SeRechaza(string nombre)
        {
            Assert.Contains("tipo de archivo no permitido. Use XLSX", Rechazo(nombre, XlsxReal()));
        }

        [Fact]
        public void FirmaFalsa_SeRechaza()
        {
            byte[] texto = Encoding.UTF8.GetBytes("pedido;placa\n4400;ABC-123");
            Assert.Equal("Archivo Excel: el archivo no es un XLSX (está dañado o le cambiaron la extensión).", Rechazo("lista.xlsx", texto));
        }

        [Theory]
        [InlineData("xl/~b^x~/b^`.xml", "xl/<b>x</b>\".xml")]
        [InlineData("xl/a~b.xml", "xl/a|b.xml")]
        public void EntradaConNombreNoValido_DiceQueEstaDanadoSinExcepcionDelSistema(string provisional, string entrada)
        {
            // .NET 4.8 lanza ArgumentException con < > " | en la ruta; antes daba HTTP 500 en ImportarSeguimiento.
            byte[] x = ImportacionZipBombTests.Renombrar(
                ImportacionZipBombTests.Zip(("[Content_Types].xml", 10), (provisional, 10)), provisional, entrada);
            Assert.Contains("dañado", Rechazo("raro.xlsx", x));
        }

        [Fact]
        public void DirectorioCentralCortado_DiceQueEstaDanado()
        {
            // Firma y cabecera local bien, pero el final del ZIP (directorio central) recortado.
            byte[] x = XlsxReal();
            byte[] cortado = x.Take(x.Length - 30).ToArray();
            Assert.Contains("dañado", Rechazo("cortado.xlsx", cortado));
        }

        [Fact]
        public void ZipDanado_DiceQueEstaDanado()
        {
            byte[] roto = { 0x50, 0x4B, 0x03, 0x04, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            Assert.Contains("dañado", Rechazo("roto.xlsx", roto));
        }

        [Fact]
        public void Vacio_SeRechaza()
        {
            Assert.Equal("Archivo Excel: el archivo está vacío.", Rechazo("a.xlsx", new byte[0]));
        }

        // ── Prueba de fuente: cada subida de Excel pasa por el helper ──────────────

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        /// <summary>
        /// Abre un libro existente: new XLWorkbook(algo) (new XLWorkbook() crea uno vacío para exportar)
        /// o XLWorkbook.OpenFromTemplate(algo).
        /// </summary>
        private static readonly Regex AbreLibro = new Regex(
            @"new\s+(ClosedXML\.Excel\.)?XLWorkbook\s*\(\s*[^\s)]|\bXLWorkbook\s*\.\s*OpenFromTemplate\s*\(");

        /// <summary>Inicio de la declaración de un método (o constructor): modificadores, tipo, nombre y "(".</summary>
        private static readonly Regex DeclaracionMetodo = new Regex(
            @"^[ \t]*(?<mods>(?:(?:public|private|protected|internal|static|async|override|virtual|sealed)\s+)+)(?:[\w<>\[\],.?]+\s+)?(?<nombre>\w+)\s*\(",
            RegexOptions.Multiline);

        private const string LlamadaHelper = "ExcelSubidoHelper.Validar(";

        /// <summary>Saltos máximos de llamador en llamador (página → método que guarda → método que abre).</summary>
        private const int ProfundidadMaxima = 4;

        /// <summary>
        /// Archivos que abren un libro que NO subió un usuario (plantilla propia, archivo generado), con
        /// el motivo. Hoy ninguno: uno nuevo se agrega aquí solo si de verdad no viene de una subida.
        /// </summary>
        private static readonly Dictionary<string, string> Excepciones =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, string> Fuentes(string raiz, string patron) =>
            Directory.GetFiles(raiz, patron, SearchOption.AllDirectories)
                .Select(f => new { f, rel = f.Substring(raiz.Length).TrimStart('\\', '/').Replace('\\', '/') })
                .Where(x => !new[] { "obj/", "bin/", "Database/" }.Any(c => x.rel.StartsWith(c, StringComparison.OrdinalIgnoreCase)))
                .ToDictionary(x => x.rel, x => SinComentarios(File.ReadAllText(x.f)), StringComparer.OrdinalIgnoreCase);

        /// <summary>Quita las líneas de comentario (// y ///) para que un comentario no cuente como llamada.</summary>
        private static string SinComentarios(string texto) =>
            string.Join("\n", texto.Split('\n').Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        private class Metodo
        {
            public string Nombre;
            public bool Privado;      // private, o sin public/internal: solo se le llama desde su archivo
            public int Inicio;        // inicio de la declaración
            public int IndiceNombre;  // dónde está su nombre en la declaración
        }

        /// <summary>El método que contiene la posición (la última declaración antes de ella).</summary>
        private static Metodo MetodoQueContiene(string texto, int indice)
        {
            Match ultimo = null;
            foreach (Match m in DeclaracionMetodo.Matches(texto))
            {
                if (m.Index > indice) break;
                ultimo = m;
            }
            if (ultimo == null) return null;
            string mods = ultimo.Groups["mods"].Value;
            return new Metodo
            {
                Nombre = ultimo.Groups["nombre"].Value,
                Privado = !Regex.IsMatch(mods, @"\b(public|internal)\b"),
                Inicio = ultimo.Index,
                IndiceNombre = ultimo.Groups["nombre"].Index
            };
        }

        /// <summary>
        /// True si el uso en <paramref name="indice"/> va precedido de <see cref="LlamadaHelper"/> en su
        /// mismo método, o si lo está cada llamada a ese método (en su archivo si es privado; como
        /// Clase.Metodo( en cualquier archivo si no). Las fallas quedan en <paramref name="fallas"/>.
        /// </summary>
        private static bool Validado(Dictionary<string, string> cs, string archivo, int indice, int profundidad,
                                     List<string> fallas, HashSet<string> revisados)
        {
            revisados.Add(archivo);
            string texto = cs[archivo];
            var metodo = MetodoQueContiene(texto, indice);
            if (metodo == null) { fallas.Add($"{archivo}: no se encontró el método del uso en la posición {indice}"); return false; }
            int helper = texto.IndexOf(LlamadaHelper, metodo.Inicio, StringComparison.Ordinal);
            if (helper >= 0 && helper < indice) return true;
            if (profundidad >= ProfundidadMaxima)
            {
                fallas.Add($"{archivo}: {metodo.Nombre} no valida y la cadena de llamadas es demasiado larga");
                return false;
            }

            var llamadas = new List<KeyValuePair<string, int>>();
            if (metodo.Privado)
            {
                foreach (Match m in Regex.Matches(texto, @"(?<![\w.])" + metodo.Nombre + @"\s*\("))
                    if (m.Index != metodo.IndiceNombre) llamadas.Add(new KeyValuePair<string, int>(archivo, m.Index));
            }
            else
            {
                string clase = Path.GetFileNameWithoutExtension(archivo);
                foreach (var kv in cs)
                    foreach (Match m in Regex.Matches(kv.Value, @"\b" + clase + @"\s*\.\s*" + metodo.Nombre + @"\s*\("))
                        llamadas.Add(new KeyValuePair<string, int>(kv.Key, m.Index));
            }
            if (llamadas.Count == 0)
            {
                fallas.Add($"{archivo}: {metodo.Nombre} abre un libro sin {LlamadaHelper}...) antes y nadie lo llama validando " +
                           "(si no viene de una subida, agréguelo a Excepciones con el motivo)");
                return false;
            }
            bool ok = true;
            foreach (var ll in llamadas)
            {
                if (!Validado(cs, ll.Key, ll.Value, profundidad + 1, fallas, revisados))
                {
                    fallas.Add($"{ll.Key}: llama a {metodo.Nombre} sin {LlamadaHelper}...) antes en su método");
                    ok = false;
                }
            }
            return ok;
        }

        [Fact]
        public void CadaSubidaDeExcel_PasaAntesPorExcelSubidoHelper()
        {
            string raiz = RaizWeb();
            var cs = Fuentes(raiz, "*.cs");
            var fallas = new List<string>();
            var revisados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int usos = 0;

            foreach (var kv in cs)
            {
                if (Excepciones.ContainsKey(kv.Key)) continue;
                foreach (Match m in AbreLibro.Matches(kv.Value))   // cada apertura, no solo la primera del archivo
                {
                    usos++;
                    Validado(cs, kv.Key, m.Index, 0, fallas, revisados);
                }
            }

            // Toda página con un <input> que acepta .xlsx valida en su código.
            foreach (var kv in Fuentes(raiz, "*.aspx"))
            {
                if (!Regex.IsMatch(kv.Value, @"accept\s*=\s*""[^""]*\.xlsx", RegexOptions.IgnoreCase)) continue;
                cs.TryGetValue(kv.Key + ".cs", out string codigo);
                revisados.Add(kv.Key + ".cs");
                if (codigo == null || !codigo.Contains(LlamadaHelper))
                    fallas.Add($"{kv.Key}: acepta .xlsx y su código no llama a {LlamadaHelper}...)");
            }

            Assert.True(fallas.Count == 0, string.Join(Environment.NewLine, fallas.Distinct()));
            // La regla no es vacía: las aperturas conocidas, sus servicios y sus tres páginas quedaron revisados (E5-19a quitó CargarExcel).
            Assert.True(usos >= 3, $"Solo se encontraron {usos} aperturas de libro");
            foreach (string conocida in new[] { "Views/DestinoDescarga.aspx.cs", "Views/RegistroChoferes.aspx.cs",
                                                "Views/Exportacion/ImportarSeguimiento.aspx.cs",
                                                "Services/Conductor/ListaChoferesService.cs",
                                                "Services/Despachos/Importacion/LectorDestinosDescarga.cs" })
                Assert.Contains(conocida, revisados);
        }

        [Fact]
        public void LaReglaDeFuente_ReconoceAbrirYCrearLibros()
        {
            Assert.Matches(AbreLibro, "using (var wb = new XLWorkbook(path))");
            Assert.Matches(AbreLibro, "libro = new ClosedXML.Excel.XLWorkbook( contenido );");
            Assert.Matches(AbreLibro, "var wb = XLWorkbook.OpenFromTemplate(ruta);");
            Assert.DoesNotMatch(AbreLibro, "using (var wb = new XLWorkbook())");
            Assert.DoesNotMatch(AbreLibro, "var wb = new XLWorkbook( );");
        }

        [Fact]
        public void LaReglaDeFuente_ExigeElHelperEnElMismoMetodoOEnCadaLlamador()
        {
            const string Valida = "ExcelSubidoHelper.Validar(fu.PostedFile, 1);\n";
            string Pagina(string antesDeB) =>
                "    protected void btnA_Click(object s, EventArgs e)\n    {\n        " + Valida + "        Abrir(fu.FileContent);\n    }\n" +
                "    protected void btnB_Click(object s, EventArgs e)\n    {\n        " + antesDeB + "Abrir(fu.FileContent);\n    }\n" +
                "    private void Abrir(Stream x)\n    {\n        using (var wb = new XLWorkbook(x)) { }\n    }\n";

            // btnB llama al método que abre el libro sin validar: falla aunque btnA (antes en el archivo) valide.
            var cs = new Dictionary<string, string> { { "Views/X.aspx.cs", Pagina("") } };
            var fallas = new List<string>();
            Assert.False(Validado(cs, "Views/X.aspx.cs", AbreLibro.Match(cs["Views/X.aspx.cs"]).Index, 0, fallas, new HashSet<string>()));
            Assert.Contains(fallas, f => f.Contains("btnB_Click") || f.Contains("llama a Abrir"));

            cs["Views/X.aspx.cs"] = Pagina(Valida + "        ");
            Assert.True(Validado(cs, "Views/X.aspx.cs", AbreLibro.Match(cs["Views/X.aspx.cs"]).Index, 0, new List<string>(), new HashSet<string>()));
        }
    }
}
