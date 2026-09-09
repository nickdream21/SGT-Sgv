<#
    ExcelWriter.ps1 — escritor .xlsx (OOXML) en PowerShell puro, sin dependencias.

    Se escribe el paquete a mano (System.IO.Compression) en vez de usar ClosedXML porque
    el paquete de la solución solo trae netstandard2.0/2.1 y cargarlo desde Windows
    PowerShell 5.1 arrastra redirecciones de binding que fallan seguido.

    Uso:
        New-LibroExcel -Ruta "salida.xlsx" -Hojas @(
            @{ Nombre = "Resumen"; Anchos = @(46, 32, 32);
               Filas  = @( @("Concepto", "Tramo 1", "Tramo 2"), @("Distancia", 462.58, 507.71) ) }
        )

    La primera fila de cada hoja se formatea como encabezado y queda congelada.
    Tipos: [int]/[long] -> entero con separador de miles; [double]/[decimal] -> 2 decimales;
    cualquier otra cosa -> texto. Para forzar estilo: @{ V = "Totales"; S = "seccion" }.
#>

Add-Type -AssemblyName System.IO.Compression      -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue

$script:EstiloIds = @{ "texto" = 0; "encabezado" = 1; "numero2" = 2; "seccion" = 3; "negrita" = 4; "entero" = 5 }

function ConvertTo-XmlSeguro([string]$t) {
    if ($null -eq $t) { return "" }
    return $t.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace('"', "&quot;")
}

function Get-LetraColumna([int]$indice) {   # 1 -> A, 27 -> AA
    $letra = ""
    while ($indice -gt 0) {
        $resto = ($indice - 1) % 26
        $letra = [char](65 + $resto) + $letra
        $indice = [int](($indice - $resto - 1) / 26)
    }
    return $letra
}

function New-CeldaXml([int]$fila, [int]$col, $valor) {
    $ref = (Get-LetraColumna $col) + $fila
    if ($null -eq $valor) { return "" }

    $estilo = $null
    if ($valor -is [hashtable]) {
        $estilo = $script:EstiloIds[[string]$valor["S"]]
        $valor  = $valor["V"]
        if ($null -eq $valor) { $valor = "" }
    }

    if ($valor -is [int] -or $valor -is [long] -or $valor -is [int16]) {
        if ($null -eq $estilo) { $estilo = $script:EstiloIds["entero"] }
        return "<c r=`"$ref`" s=`"$estilo`"><v>$valor</v></c>"
    }
    if ($valor -is [double] -or $valor -is [decimal] -or $valor -is [single]) {
        if ($null -eq $estilo) { $estilo = $script:EstiloIds["numero2"] }
        $n = ([double]$valor).ToString([Globalization.CultureInfo]::InvariantCulture)
        return "<c r=`"$ref`" s=`"$estilo`"><v>$n</v></c>"
    }
    if ($null -eq $estilo) { $estilo = $script:EstiloIds["texto"] }
    $texto = ConvertTo-XmlSeguro ([string]$valor)
    return "<c r=`"$ref`" s=`"$estilo`" t=`"inlineStr`"><is><t xml:space=`"preserve`">$texto</t></is></c>"
}

function New-HojaXml([hashtable]$hoja) {
    $sb = New-Object Text.StringBuilder
    [void]$sb.Append('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>')
    [void]$sb.Append('<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">')
    [void]$sb.Append('<sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>')
    [void]$sb.Append('<sheetFormatPr defaultRowHeight="15"/>')

    if ($hoja.Anchos) {
        [void]$sb.Append('<cols>')
        for ($c = 0; $c -lt $hoja.Anchos.Count; $c++) {
            $n = $c + 1
            [void]$sb.Append("<col min=`"$n`" max=`"$n`" width=`"$($hoja.Anchos[$c])`" customWidth=`"1`"/>")
        }
        [void]$sb.Append('</cols>')
    }

    [void]$sb.Append('<sheetData>')
    $nFila = 0
    foreach ($fila in @($hoja.Filas)) {
        $nFila++
        [void]$sb.Append("<row r=`"$nFila`"" + $(if ($nFila -eq 1) { ' ht="28" customHeight="1"' } else { "" }) + ">")
        $nCol = 0
        foreach ($valor in @($fila)) {
            $nCol++
            $v = if ($nFila -eq 1 -and -not ($valor -is [hashtable])) { @{ V = $valor; S = "encabezado" } } else { $valor }
            [void]$sb.Append((New-CeldaXml $nFila $nCol $v))
        }
        [void]$sb.Append('</row>')
    }
    [void]$sb.Append('</sheetData></worksheet>')
    return $sb.ToString()
}

function Get-EstilosXml {
    return @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
<numFmts count="2"><numFmt numFmtId="164" formatCode="#,##0.00"/><numFmt numFmtId="165" formatCode="#,##0"/></numFmts>
<fonts count="3">
<font><sz val="11"/><name val="Calibri"/></font>
<font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
<font><b/><sz val="11"/><name val="Calibri"/></font>
</fonts>
<fills count="4">
<fill><patternFill patternType="none"/></fill>
<fill><patternFill patternType="gray125"/></fill>
<fill><patternFill patternType="solid"><fgColor rgb="FF1F4E79"/><bgColor indexed="64"/></patternFill></fill>
<fill><patternFill patternType="solid"><fgColor rgb="FFDCE6F1"/><bgColor indexed="64"/></patternFill></fill>
</fills>
<borders count="2">
<border><left/><right/><top/><bottom/><diagonal/></border>
<border><left/><right/><top/><bottom style="thin"><color rgb="FF9BB7D4"/></bottom><diagonal/></border>
</borders>
<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
<cellXfs count="6">
<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
<xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
<xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
<xf numFmtId="0" fontId="2" fillId="3" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
<xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1"/>
<xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
</cellXfs>
<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
</styleSheet>
'@
}

function New-LibroExcel {
    param(
        [Parameter(Mandatory)][string]$Ruta,
        [Parameter(Mandatory)][array]$Hojas
    )

    $carpeta = Split-Path -Parent $Ruta
    if ($carpeta -and -not (Test-Path $carpeta)) { [void](New-Item -ItemType Directory -Path $carpeta -Force) }
    if (Test-Path $Ruta) { Remove-Item $Ruta -Force }

    $n = $Hojas.Count

    $ct = New-Object Text.StringBuilder
    [void]$ct.Append('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>')
    [void]$ct.Append('<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">')
    [void]$ct.Append('<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>')
    [void]$ct.Append('<Default Extension="xml" ContentType="application/xml"/>')
    [void]$ct.Append('<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>')
    for ($i = 1; $i -le $n; $i++) {
        [void]$ct.Append("<Override PartName=`"/xl/worksheets/sheet$i.xml`" ContentType=`"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml`"/>")
    }
    [void]$ct.Append('<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>')
    [void]$ct.Append('</Types>')

    $rels = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
            '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>' +
            '</Relationships>'

    $wb = New-Object Text.StringBuilder
    [void]$wb.Append('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>')
    [void]$wb.Append('<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>')
    for ($i = 1; $i -le $n; $i++) {
        $nombre = ConvertTo-XmlSeguro ([string]$Hojas[$i - 1].Nombre)
        [void]$wb.Append("<sheet name=`"$nombre`" sheetId=`"$i`" r:id=`"rId$i`"/>")
    }
    [void]$wb.Append('</sheets></workbook>')

    $wbRels = New-Object Text.StringBuilder
    [void]$wbRels.Append('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>')
    [void]$wbRels.Append('<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">')
    for ($i = 1; $i -le $n; $i++) {
        [void]$wbRels.Append("<Relationship Id=`"rId$i`" Type=`"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet`" Target=`"worksheets/sheet$i.xml`"/>")
    }
    $idEstilos = $n + 1
    [void]$wbRels.Append("<Relationship Id=`"rId$idEstilos`" Type=`"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles`" Target=`"styles.xml`"/>")
    [void]$wbRels.Append('</Relationships>')

    $partes = [ordered]@{
        "[Content_Types].xml"      = $ct.ToString()
        "_rels/.rels"              = $rels
        "xl/workbook.xml"          = $wb.ToString()
        "xl/_rels/workbook.xml.rels" = $wbRels.ToString()
        "xl/styles.xml"            = (Get-EstilosXml)
    }
    for ($i = 1; $i -le $n; $i++) { $partes["xl/worksheets/sheet$i.xml"] = (New-HojaXml $Hojas[$i - 1]) }

    $zip = [IO.Compression.ZipFile]::Open($Ruta, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $utf8 = New-Object Text.UTF8Encoding($false)
        foreach ($nombre in $partes.Keys) {
            $entrada = $zip.CreateEntry($nombre, [IO.Compression.CompressionLevel]::Optimal)
            $flujo = $entrada.Open()
            try {
                $bytes = $utf8.GetBytes($partes[$nombre])
                $flujo.Write($bytes, 0, $bytes.Length)
            } finally { $flujo.Dispose() }
        }
    } finally { $zip.Dispose() }

    return (Resolve-Path $Ruta).Path
}
