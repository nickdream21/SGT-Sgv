---
name: itextsharp-pdf-webforms
description: Generación de PDFs en WebSGV con iTextSharp 5.5.13.4 (LEGACY, NO iText 7) — patrones de Document/PdfWriter, fuentes embebidas, archivado bajo App_Data, hash SHA-256 y firma digital
license: MIT
compatibility: opencode
metadata:
  audience: developers, reviewers
  workflow: pdf
---

# iTextSharp PDF en Web Forms

## Versión y by qué importa

WebSGV usa **iTextSharp 5.5.13.4** (paquete `iTextSharp` en `packages.config`).

> ⚠️ **NO ES iText 7.** API totalmente distinta. Si Copilot o ChatGPT te sugieren `using iText.Layout`, `Document doc = new Document()` con `PdfDocument`, **estás usando la API equivocada**. Esta skill solo cubre la API legacy.

iTextSharp 5.x es **AGPL** — sustituirlo por iText 7 implicaría licencia comercial. Mantener.

## Namespaces correctos

```csharp
using iTextSharp.text;
using iTextSharp.text.pdf;
// NO: using iText.Kernel.*;       ← eso es iText 7
// NO: using iText.Layout.*;       ← eso es iText 7
```

## Patrón canónico (referencia: `Services/PdfOrdenViajeService.cs`)

```csharp
public byte[] GenerarPdf(MiDtoDatos datos)
{
    using (var ms = new MemoryStream())
    {
        // 1. Document + PdfWriter
        var doc = new Document(PageSize.A4, 36f, 36f, 36f, 36f);
        var writer = PdfWriter.GetInstance(doc, ms);
        writer.CloseStream = false;  // queremos seguir leyendo el MemoryStream después

        // 2. Fuentes embebidas (siempre WINANSI + EMBEDDED para imprimir bien tildes)
        var bfRegular = BaseFont.CreateFont(BaseFont.HELVETICA,      BaseFont.WINANSI, BaseFont.EMBEDDED);
        var bfBold    = BaseFont.CreateFont(BaseFont.HELVETICA_BOLD, BaseFont.WINANSI, BaseFont.EMBEDDED);
        var fontTitulo = new Font(bfBold, 14f, Font.NORMAL, BaseColor.BLACK);
        var fontTexto  = new Font(bfRegular, 10f);

        doc.Open();

        // 3. Contenido
        doc.Add(new Paragraph("Título del documento", fontTitulo));

        var tabla = new PdfPTable(3) { WidthPercentage = 100f };
        tabla.SetWidths(new float[] { 30f, 40f, 30f });
        tabla.AddCell(new PdfPCell(new Phrase("Col1", fontTexto)));
        tabla.AddCell(new PdfPCell(new Phrase("Col2", fontTexto)));
        tabla.AddCell(new PdfPCell(new Phrase("Col3", fontTexto)));
        doc.Add(tabla);

        // 4. Cerrar documento ANTES de leer el stream
        doc.Close();
        return ms.ToArray();
    }
}
```

### Reglas

1. **`MemoryStream` en `using`**.
2. **`Document.Close()` antes de leer `ms.ToArray()`** — sin esto el PDF está incompleto.
3. **Fuentes con `BaseFont.EMBEDDED`** — sin embed, las tildes españolas se rompen al imprimir o en lectores no-Adobe.
4. **`writer.CloseStream = false`** si necesitas leer el stream después de cerrar el Document.

## Archivado en disco

Convención de WebSGV (`OrdenViaje.RutaArchivo` en `Web.config`, default `~/App_Data/OrdenesViaje`):

```csharp
string carpetaBase = HostingEnvironment.MapPath(
    "~/" + EmpresaConfigHelper.RutaArchivoOrdenViaje);  // App_Data/OrdenesViaje
string subcarpeta = Path.Combine(carpetaBase, año.ToString("D4"), mes.ToString("D2"));
Directory.CreateDirectory(subcarpeta);

string nombreArchivo = $"OV-{año:D4}-{numero:D6}.pdf";
string rutaFisica = Path.Combine(subcarpeta, nombreArchivo);
File.WriteAllBytes(rutaFisica, pdfBytes);
```

**Nunca** uses paths absolutos hardcoded. Siempre `HostingEnvironment.MapPath` + config.

## Hash SHA-256 del PDF

Para auditoría e integridad (patrón usado en `PdfOrdenViajeService`):

```csharp
using System.Security.Cryptography;

string CalcularHash(byte[] bytes)
{
    using (var sha = SHA256.Create())
    {
        byte[] hash = sha.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
```

Guardar el hash en la DB junto al `RutaRelativa` para detectar manipulación posterior.

## Firma digital con `FirmaService`

Las firmas manuscritas vienen como PNG base64 desde un canvas HTML5. Para incrustarlas:

```csharp
if (firmaPng != null && firmaPng.Length > 0)
{
    var img = iTextSharp.text.Image.GetInstance(firmaPng);
    img.ScaleToFit(120f, 50f);
    img.SetAbsolutePosition(xFirma, yFirma);
    writer.DirectContent.AddImage(img);
}
else
{
    // Recuadro de "Firma pendiente"
    var cb = writer.DirectContent;
    cb.Rectangle(xFirma, yFirma, 120f, 50f);
    cb.Stroke();
    var fontPlaceholder = new Font(bfRegular, 7f, Font.ITALIC, GRIS_SECUND);
    ColumnText.ShowTextAligned(cb, Element.ALIGN_CENTER,
        new Phrase("Firma pendiente de registro digital", fontPlaceholder),
        xFirma + 60f, yFirma + 22f, 0);
}
```

## Servir el PDF desde un .aspx

```csharp
protected void Page_Load(object sender, EventArgs e)
{
    if (Session["IdUsuario"] == null) { Response.Redirect("~/Views/Login.aspx"); return; }

    int idOv = int.Parse(Request.QueryString["id"]);
    var service = new PdfOrdenViajeService();
    var resultado = service.Generar(idOv, archivar: false);

    Response.Clear();
    Response.ContentType = "application/pdf";
    Response.AddHeader("Content-Disposition", $"inline; filename=OV-{idOv}.pdf");
    Response.BinaryWrite(resultado.Bytes);
    Response.End();
}
```

## Anti-patrones (REJECT en review)

### ❌ Mezclar APIs de iText 7
```csharp
using iText.Kernel.Pdf;        // ← VERSIÓN INCORRECTA
using iText.Layout.Element;
```
**Fix:** usar solo `iTextSharp.text` y `iTextSharp.text.pdf`.

### ❌ Cerrar el stream antes que el Document
```csharp
using (var fs = File.Create(ruta))
{
    var doc = new Document();
    PdfWriter.GetInstance(doc, fs);
    doc.Open();
    doc.Add(new Paragraph("..."));
    // ← olvida doc.Close() → PDF corrupto
}
```
**Fix:** `doc.Close()` explícito antes de salir del using.

### ❌ Fuentes sin EMBEDDED
```csharp
BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
// → tildes y ñ se rompen en algunos lectores
```
**Fix:** `BaseFont.WINANSI, BaseFont.EMBEDDED`.

### ❌ Path hardcoded
```csharp
string ruta = @"C:\App_Data\PDFs\" + nombre;  // muere en deploy
```
**Fix:** `HostingEnvironment.MapPath("~/App_Data/...")`.

### ❌ No archivar pero borrar de memoria
```csharp
byte[] bytes = service.Generar(...);
return bytes;
// y nunca guardar hash ni traza → no se puede auditar después
```
**Fix:** archivar el PDF firmado y registrar `(IdOV, RutaRelativa, Hash, FechaGeneracion)` en DB.

## Checklist para reviewer

- [ ] Solo `iTextSharp.text` / `iTextSharp.text.pdf` (no `iText.*`).
- [ ] `Document` se cierra antes de leer/devolver el stream.
- [ ] `MemoryStream` o `FileStream` dentro de `using`.
- [ ] Fuentes con `WINANSI, EMBEDDED`.
- [ ] Path obtenido con `HostingEnvironment.MapPath` + config (`OrdenViaje.RutaArchivo`).
- [ ] Si el PDF se archiva, se registra hash SHA-256 para auditoría.
- [ ] Si la página sirve el PDF, valida sesión + rol antes de `Response.BinaryWrite`.
- [ ] `Response.End()` cierra correctamente la pipeline.

## Referencias en el repo

- `WebSGV/Services/PdfOrdenViajeService.cs` — generador completo con tabla, firma, hash, archivado.
- `WebSGV/Services/PdfAbastecimientoService.cs` — patrón equivalente para abastecimiento.
- `WebSGV/Services/FirmaService.cs` — captura/validación de firma PNG.
- `WebSGV/Helpers/EmpresaConfigHelper.cs` — lectura de `OrdenViaje.RutaArchivo`.
- `WebSGV/Views/DescargarPdfOrdenViaje.aspx.cs` — patrón de servir PDF desde .aspx.
