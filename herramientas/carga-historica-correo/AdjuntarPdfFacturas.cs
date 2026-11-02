// Adjunta el PDF de la factura que trae cada ZIP del correo a la factura ya registrada que no tiene PDF, si sus
// despachos son de octubre en adelante. Mismo nombre y carpeta que RegistroDespacho
// (~/Uploads/Factura/aaaa/mm/FACTURA_<id>_<fecha>.pdf). Los archivos quedan en WebSGV\Uploads de esta PC: hay que
// copiarlos por FTP a la misma ruta del sitio publicado.
// Uso: AdjuntarPdfFacturas.exe <carpetaZips> <carpetaWebSGV> prueba|aplicar
using System; using System.Data; using System.IO; using System.Linq; using System.Text;
using WebSGV.Helpers; using WebSGV.Services.Despachos.Importacion; using WebSGV.Services.Facturas;

class AdjuntarPdfFacturas
{
    static void Main(string[] a)
    {
        bool aplicar = a[2] == "aplicar";
        var sb = new StringBuilder();
        Action<string> W = s => { Console.WriteLine(s); sb.AppendLine(s); };
        DateTime ahora = FechaHelper.Ahora();
        string relativa = $"~/Uploads/Factura/{ahora:yyyy}/{ahora:MM}/";
        string carpeta = Path.Combine(a[1], "Uploads", "Factura", ahora.ToString("yyyy"), ahora.ToString("MM"));
        int adjuntados = 0;

        foreach (var zip in Directory.GetFiles(a[0], "*.zip").OrderBy(f => f))
        {
            var imp = ImportacionPedidoService.Leer(new[] { new ImportacionPedidoService.ArchivoSubido { Nombre = Path.GetFileName(zip), Contenido = File.ReadAllBytes(zip) } }, out var pdf);
            string numero = imp.Factura?.NumeroFactura;
            if (numero == null || pdf == null) { W($"{imp.NumeroPedido}: sin factura o sin PDF en el ZIP"); continue; }

            DataTable f = DbHelper.ConsultarTabla(@"
                SELECT f.idFactura,
                       (SELECT COUNT(*) FROM DocumentosFactura df WHERE df.idFactura = f.idFactura AND df.activo = 1) AS pdfs,
                       (SELECT MIN(d.fechaDespacho) FROM Despachos d WHERE d.idFactura = f.idFactura AND d.activo = 1) AS primerDespacho
                FROM Factura f WHERE f.numeroFactura = @n", DbHelper.Param("@n", numero));
            if (f.Rows.Count == 0) { W($"{imp.NumeroPedido} {numero}: la factura no está registrada (no se adjunta)"); continue; }
            int idFactura = Convert.ToInt32(f.Rows[0]["idFactura"]);
            if (Convert.ToInt32(f.Rows[0]["pdfs"]) > 0) { W($"{imp.NumeroPedido} {numero}: ya tiene PDF"); continue; }
            if (f.Rows[0]["primerDespacho"] == DBNull.Value || Convert.ToDateTime(f.Rows[0]["primerDespacho"]) < new DateTime(2026, 10, 1))
            { W($"{imp.NumeroPedido} {numero}: sin despachos de octubre en adelante (no se adjunta)"); continue; }

            string extension = Path.GetExtension(pdf.Nombre).ToLowerInvariant();
            string nombreArchivo = $"FACTURA_{idFactura}_{ahora:yyyyMMdd_HHmmss}{extension}";
            if (aplicar)
            {
                Directory.CreateDirectory(carpeta);
                File.WriteAllBytes(Path.Combine(carpeta, nombreArchivo), pdf.Contenido);
                FacturaEscrituraService.InsertarDocumentoStandalone(idFactura, pdf.Nombre, nombreArchivo, relativa + nombreArchivo,
                    extension, pdf.Contenido.LongLength, "carga.correo");
            }
            adjuntados++;
            W($"{imp.NumeroPedido} {numero}: factura {idFactura} <- {pdf.Nombre} ({pdf.Contenido.Length / 1024} KB) como {relativa}{nombreArchivo}");
        }

        if (aplicar)
            AuditoriaHelper.Registrar("INSERT", "DocumentosFactura", null, $"PDF de {adjuntados} factura(s) adjuntados desde los ZIP del correo (carpeta {relativa})");
        W($"Adjuntados: {adjuntados} {(aplicar ? "APLICADO" : "(prueba: no se escribió nada)")}");
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "pdfs_" + (aplicar ? "aplicado" : "prueba") + ".txt"), sb.ToString(), new UTF8Encoding(true));
    }
}
