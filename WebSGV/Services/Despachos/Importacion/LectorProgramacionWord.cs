using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using WebSGV.Models.Despachos;
using WebSGV.Services.Common;

namespace WebSGV.Services.Despachos.Importacion
{
    /// <summary>
    /// Lee el Word (.docx) con la programación del día que reparte el área de transporte:
    ///
    ///   DESPACHO 11/10/2026
    ///   GUAYAQUIL-VITAPRO
    ///   4400133436
    ///   CHOFER | TRACTO | CARRETA | CAMBIO
    ///
    /// Cada tabla es una sección; su título es el párrafo de arriba y el N° de pedido, el
    /// párrafo de solo dígitos entre ambos (o los dígitos del título). Las columnas se
    /// ubican por su encabezado, así que el orden puede variar.
    /// </summary>
    public static class LectorProgramacionWord
    {
        private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        /// <summary>Un párrafo (Texto) o una tabla (Tabla) del cuerpo del documento, en orden.</summary>
        public class BloqueWord
        {
            public string Texto { get; set; }
            public List<List<string>> Tabla { get; set; }

            public static BloqueWord Parrafo(string texto) => new BloqueWord { Texto = texto };
            public static BloqueWord DeTabla(List<List<string>> filas) => new BloqueWord { Tabla = filas };
        }

        public static ProgramacionTransporte Leer(byte[] docx) => Interpretar(ExtraerBloques(docx));

        /// <summary>Párrafos y tablas del cuerpo del .docx (word/document.xml).</summary>
        public static List<BloqueWord> ExtraerBloques(byte[] docx)
        {
            XDocument xml;
            try
            {
                using (var zip = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read))
                {
                    var entrada = zip.GetEntry("word/document.xml");
                    if (entrada == null)
                        throw new ErrorNegocioException("El archivo no es un documento de Word (.docx).");
                    using (var s = entrada.Open())
                        xml = XDocument.Load(s);
                }
            }
            catch (InvalidDataException ex)
            {
                throw new ErrorNegocioException("El archivo no es un documento de Word (.docx). Si es un .doc antiguo, guárdelo como .docx.", ex);
            }

            var cuerpo = xml.Root?.Element(W + "body");
            var bloques = new List<BloqueWord>();
            if (cuerpo != null) AgregarBloques(cuerpo, bloques);
            return bloques;
        }

        private static void AgregarBloques(XElement contenedor, List<BloqueWord> bloques)
        {
            foreach (var e in contenedor.Elements())
            {
                if (e.Name == W + "p")
                    bloques.Add(BloqueWord.Parrafo(TextoDe(e)));
                else if (e.Name == W + "tbl")
                    bloques.Add(BloqueWord.DeTabla(e.Elements(W + "tr")
                        .Select(tr => tr.Elements(W + "tc").Select(tc =>
                            string.Join(" ", tc.Elements(W + "p").Select(TextoDe).Where(t => t.Length > 0))).ToList())
                        .ToList()));
                else if (e.Name == W + "sdt")
                {
                    var contenido = e.Element(W + "sdtContent");
                    if (contenido != null) AgregarBloques(contenido, bloques);
                }
            }
        }

        private static string TextoDe(XElement parrafo) =>
            string.Concat(parrafo.Descendants().Select(d =>
                d.Name == W + "t" ? d.Value : d.Name == W + "tab" ? " " : string.Empty)).Trim();

        /// <summary>Arma las secciones a partir de los bloques (sin tocar archivos, para probarlo).</summary>
        public static ProgramacionTransporte Interpretar(IEnumerable<BloqueWord> bloques)
        {
            var programacion = new ProgramacionTransporte();
            string titulo = null, pedido = null;

            foreach (var b in bloques)
            {
                if (b.Tabla == null)
                {
                    string texto = Regex.Replace(b.Texto ?? string.Empty, @"\s+", " ").Trim();
                    if (texto.Length == 0) continue;

                    var fecha = Regex.Match(texto, @"^DESPACHO\s+(\d{1,2}/\d{1,2}/\d{4})", RegexOptions.IgnoreCase);
                    if (fecha.Success && programacion.Fecha == null)
                    {
                        if (DateTime.TryParseExact(fecha.Groups[1].Value, "d/M/yyyy", CultureInfo.InvariantCulture,
                                DateTimeStyles.None, out var f))
                            programacion.Fecha = f;
                        continue;
                    }

                    if (Regex.IsMatch(texto, @"^\d{6,}$"))
                    {
                        pedido = texto;
                        continue;
                    }

                    // Título nuevo: el pedido puede venir en la misma línea ("GUAYAQUIL-VITAPRO 4400133436")
                    var enTitulo = Regex.Match(texto, @"\b(\d{8,})\b");
                    titulo = enTitulo.Success ? texto.Replace(enTitulo.Value, "").Trim(' ', '-', ':') : texto;
                    pedido = enTitulo.Success ? enTitulo.Groups[1].Value : null;
                    continue;
                }

                var seccion = LeerTabla(b.Tabla);
                if (seccion == null) continue;
                seccion.Titulo = titulo;
                seccion.NumeroPedido = pedido;
                programacion.Secciones.Add(seccion);
                titulo = null;
                pedido = null;
            }

            return programacion;
        }

        private static SeccionProgramacion LeerTabla(List<List<string>> filas)
        {
            if (filas.Count == 0) return null;

            var encabezado = filas[0].Select(LectorDocumentosPedido.Normalizar).ToList();
            int Columna(params string[] nombres) => encabezado.FindIndex(h => nombres.Any(h.Contains));

            int chofer = Columna("CHOFER", "CONDUCTOR");
            int tracto = Columna("TRACTO", "PLACA");
            int carreta = Columna("CARRETA", "SEMI");
            int cambio = Columna("CAMBIO");
            int horario = Columna("HORARIO", "HORA");
            if (chofer < 0 || (tracto < 0 && carreta < 0)) return null;

            string Celda(List<string> fila, int i) =>
                i >= 0 && i < fila.Count && !string.IsNullOrWhiteSpace(fila[i])
                    ? Regex.Replace(fila[i], @"\s+", " ").Trim() : null;

            var seccion = new SeccionProgramacion();
            foreach (var fila in filas.Skip(1))
            {
                var f = new FilaProgramacion
                {
                    Chofer = Celda(fila, chofer),
                    Tracto = Celda(fila, tracto),
                    Carreta = Celda(fila, carreta),
                    Cambio = Celda(fila, cambio),
                    Horario = Celda(fila, horario)
                };
                if (f.Chofer == null && f.Tracto == null && f.Carreta == null) continue;
                seccion.Filas.Add(f);
            }
            return seccion;
        }
    }
}
