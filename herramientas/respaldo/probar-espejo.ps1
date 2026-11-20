<#
.SYNOPSIS
    Prueba la lógica del espejo de respaldar-archivos.ps1 sin FTP ni BD.
.DESCRIPTION
    Extrae las funciones puras de respaldar-archivos.ps1 (sin ejecutar el script) y comprueba:
    exclusión de _temp, lectura de listados FTP (Unix y MS-DOS/IIS), plan incremental (lista remota
    simulada contra la local), nombre de la versión anterior y el renombrado final en una carpeta
    temporal de %TEMP% (que se borra al terminar). Sale con código 1 si algo falla. No usa Pester.
.EXAMPLE
    .\herramientas\respaldo\probar-espejo.ps1
#>
$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot 'respaldar-archivos.ps1'
$errores = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($script, [ref]$null, [ref]$errores)
if ($errores -and $errores.Count -gt 0) { throw "respaldar-archivos.ps1 tiene errores de sintaxis: $($errores[0].Message)" }
# ConvertFrom-ListadoFtp vive en el módulo común (herramientas\comun\Ftp.psm1).
Import-Module (Join-Path $PSScriptRoot '..\comun\Ftp.psm1') -Force -DisableNameChecking
Import-Module (Join-Path $PSScriptRoot '..\comun\CopiaVerificada.psm1') -Force -DisableNameChecking
$nombres = 'Test-RutaExcluida', 'Get-PlanEspejo', 'Get-NombreVersionAnterior', 'Move-DescargaASuSitio', 'Copy-DescargaASuSitio'
foreach ($n in $nombres) {
    $def = $ast.FindAll({ param($x) $x -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $x.Name -eq $n }, $true) |
           Select-Object -First 1
    if (-not $def) { throw "No se encontró la función $n en respaldar-archivos.ps1." }
    . ([scriptblock]::Create($def.Extent.Text))
}

$fallos = 0
function Comprobar([string]$caso, [bool]$condicion, [string]$detalle) {
    if ($condicion) { Write-Host "OK     $caso" -ForegroundColor Green }
    else { Write-Host "FALLA  $caso -> $detalle" -ForegroundColor Red; $script:fallos++ }
}

# 1) Exclusión de _temp
Comprobar 'Excluye Uploads/Factura/_temp/x.pdf' (Test-RutaExcluida 'Uploads/Factura/_temp/x.pdf') ''
Comprobar 'Excluye _TEMP en cualquier nivel y con \' (Test-RutaExcluida 'Uploads\_TEMP\a\b.pdf') ''
Comprobar 'No excluye nombres que solo contienen _temp' (-not (Test-RutaExcluida 'Uploads/Factura/mi_temp.pdf')) ''
Comprobar 'No excluye carpeta _temporal' (-not (Test-RutaExcluida 'Uploads/_temporal/a.pdf')) ''

# 2) Listados FTP
$unix = @(
    'total 3',
    'drwxr-xr-x   1 owner    group           0 Nov 19 10:00 .',
    'drwxr-xr-x   1 owner    group           0 Nov 19 10:00 ..',
    'drwxr-xr-x   1 owner    group           0 Nov 19 10:00 2026',
    '-rw-r--r--   1 owner    group      176988 Jul 21  2026 OV-2026-000001.pdf',
    '-rw-r--r--   1 owner    group        1234 Nov  3 19:28 Guía de remisión 01.pdf'
)
$lu = @(ConvertFrom-ListadoFtp $unix)
Comprobar 'Unix: 3 entradas (sin . ni .. ni total)' ($lu.Count -eq 3) "$($lu.Count) entradas"
Comprobar 'Unix: carpeta 2026' ($lu[0].Nombre -eq '2026' -and $lu[0].EsCarpeta) ($lu[0] | Out-String)
Comprobar 'Unix: archivo con tamaño' ($lu[1].Nombre -eq 'OV-2026-000001.pdf' -and -not $lu[1].EsCarpeta -and $lu[1].Tamano -eq 176988) ($lu[1] | Out-String)
Comprobar 'Unix: nombre con espacios y tilde' ($lu[2].Nombre -eq 'Guía de remisión 01.pdf' -and $lu[2].Tamano -eq 1234) $lu[2].Nombre

$dos = @(
    "11-19-26  10:00AM       <DIR>          2026`r",
    "11-03-26  07:28PM               176988 OV-2026-000001.pdf`r",
    "11-03-2026  19:28                 55 Factura F001 2.pdf"
)
$ld = @(ConvertFrom-ListadoFtp $dos)
Comprobar 'DOS: 3 entradas válidas' ($ld.Count -eq 3 -and @($ld | Where-Object { -not $_.Valido }).Count -eq 0) ($ld | Out-String)
Comprobar 'DOS: carpeta <DIR>' ($ld[0].Nombre -eq '2026' -and $ld[0].EsCarpeta) ($ld[0] | Out-String)
Comprobar 'DOS: archivo con tamaño' ($ld[1].Nombre -eq 'OV-2026-000001.pdf' -and $ld[1].Tamano -eq 176988) ($ld[1] | Out-String)
Comprobar 'DOS: 24 h y espacios' ($ld[2].Nombre -eq 'Factura F001 2.pdf' -and $ld[2].Tamano -eq 55) ($ld[2] | Out-String)

$raro = @(ConvertFrom-ListadoFtp @('formato desconocido xyz'))
Comprobar 'Línea no reconocida: Valido = $false (activa NLST+SIZE)' ($raro.Count -eq 1 -and -not $raro[0].Valido) ($raro | Out-String)

# 3) Plan incremental: lista remota simulada contra la local
function New-Remoto($ruta, $tam) { New-Object PSObject -Property @{ Ruta = $ruta; Tamano = $tam } }
$remotos = @(
    (New-Remoto 'Uploads/CPIC/nuevo.pdf' 100),            # no está en local -> Bajar
    (New-Remoto 'Uploads/CPIC/igual.pdf' 200),            # mismo tamaño     -> Igual
    (New-Remoto 'Uploads/CPIC/cambio.pdf' 300),           # tamaño distinto  -> Reemplazar
    (New-Remoto 'Uploads/CPIC/MAYUS.PDF' 400),            # en local como mayus.pdf -> Igual (Windows no distingue)
    (New-Remoto 'Uploads/Factura/_temp/borrador.pdf' 5),  # _temp -> Excluido
    (New-Remoto 'OrdenesViaje/2026/11/OV-1.pdf' -1)       # tamaño desconocido y existe -> Igual
)
$locales = @{
    'Uploads/CPIC/igual.pdf'          = 200
    'Uploads/CPIC/cambio.pdf'         = 250
    'Uploads/CPIC/mayus.pdf'          = 400
    'Uploads/CPIC/borrado-en-servidor.pdf' = 999   # solo local: no debe aparecer (nunca se borra)
    'OrdenesViaje\2026\11\OV-1.pdf'   = 10
}
$plan = @(Get-PlanEspejo -Remotos $remotos -Locales $locales)
function Accion($ruta) { ($plan | Where-Object { $_.Ruta -eq $ruta }).Accion }
Comprobar 'Plan: un resultado por remoto' ($plan.Count -eq $remotos.Count) "$($plan.Count)"
Comprobar 'Plan: nuevo -> Bajar' ((Accion 'Uploads/CPIC/nuevo.pdf') -eq 'Bajar') (Accion 'Uploads/CPIC/nuevo.pdf')
Comprobar 'Plan: mismo tamaño -> Igual' ((Accion 'Uploads/CPIC/igual.pdf') -eq 'Igual') (Accion 'Uploads/CPIC/igual.pdf')
Comprobar 'Plan: tamaño distinto -> Reemplazar' ((Accion 'Uploads/CPIC/cambio.pdf') -eq 'Reemplazar') (Accion 'Uploads/CPIC/cambio.pdf')
Comprobar 'Plan: mayúsculas distintas -> Igual' ((Accion 'Uploads/CPIC/MAYUS.PDF') -eq 'Igual') (Accion 'Uploads/CPIC/MAYUS.PDF')
Comprobar 'Plan: _temp -> Excluido' ((Accion 'Uploads/Factura/_temp/borrador.pdf') -eq 'Excluido') (Accion 'Uploads/Factura/_temp/borrador.pdf')
Comprobar 'Plan: tamaño remoto desconocido y existe -> Igual' ((Accion 'OrdenesViaje/2026/11/OV-1.pdf') -eq 'Igual') (Accion 'OrdenesViaje/2026/11/OV-1.pdf')
Comprobar 'Plan: el archivo solo local no aparece (nunca se borra)' (@($plan | Where-Object { $_.Ruta -like '*borrado-en-servidor*' }).Count -eq 0) ''
Comprobar 'Plan: Reemplazar informa el tamaño local' ((($plan | Where-Object { $_.Ruta -eq 'Uploads/CPIC/cambio.pdf' }).TamanoLocal) -eq 250) ''
$vacio = @(Get-PlanEspejo -Remotos @((New-Remoto 'Uploads/a.pdf' 1)) -Locales @{})
Comprobar 'Plan: sin copia local todo es Bajar' ($vacio.Count -eq 1 -and $vacio[0].Accion -eq 'Bajar') ($vacio | Out-String)

# 4) Nombre de la versión anterior
$f = Get-Date '2026-11-19 15:04:05'
Comprobar 'Versión anterior con extensión' ((Get-NombreVersionAnterior 'OV-1.pdf' $f) -eq 'OV-1__anterior_20261119_150405.pdf') (Get-NombreVersionAnterior 'OV-1.pdf' $f)
Comprobar 'Versión anterior sin extensión' ((Get-NombreVersionAnterior 'LEEME' $f) -eq 'LEEME__anterior_20261119_150405') (Get-NombreVersionAnterior 'LEEME' $f)
Comprobar 'Versión anterior con contador' ((Get-NombreVersionAnterior 'a.b.pdf' $f 2) -eq 'a.b__anterior_20261119_150405_2.pdf') (Get-NombreVersionAnterior 'a.b.pdf' $f 2)

# 5) Renombrado final en una carpeta temporal: nunca borra la versión anterior
$tmp = Join-Path $env:TEMP ('sgv-probar-espejo-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $tmp | Out-Null
try {
    $final = Join-Path $tmp 'doc.pdf'
    [IO.File]::WriteAllText("$final.descargando", 'nuevo')
    $ant = Move-DescargaASuSitio -Temporal "$final.descargando" -Final $final -Fecha $f
    Comprobar 'Primera bajada: sin versión anterior' ($null -eq $ant -and [IO.File]::ReadAllText($final) -eq 'nuevo') "$ant"

    [IO.File]::WriteAllText("$final.descargando", 'nuevo 2')
    $ant = Move-DescargaASuSitio -Temporal "$final.descargando" -Final $final -Fecha $f
    Comprobar 'Cambio: guarda la anterior con sufijo de fecha' ($ant -eq 'doc__anterior_20261119_150405.pdf' -and [IO.File]::ReadAllText((Join-Path $tmp $ant)) -eq 'nuevo') "$ant"
    Comprobar 'Cambio: deja la nueva en su sitio' ([IO.File]::ReadAllText($final) -eq 'nuevo 2') ''

    [IO.File]::WriteAllText("$final.descargando", 'nuevo 3')
    $ant2 = Move-DescargaASuSitio -Temporal "$final.descargando" -Final $final -Fecha $f
    Comprobar 'Segundo cambio en el mismo segundo: no pisa la anterior' ($ant2 -eq 'doc__anterior_20261119_150405_2.pdf' -and [IO.File]::ReadAllText((Join-Path $tmp $ant)) -eq 'nuevo' -and [IO.File]::ReadAllText((Join-Path $tmp $ant2)) -eq 'nuevo 2') "$ant2"
    Comprobar 'No quedan temporales y no se perdió ningún archivo' ((@(Get-ChildItem $tmp -File).Count -eq 3) -and -not (Test-Path "$final.descargando")) (@(Get-ChildItem $tmp -File).Name -join ', ')
} finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

# 6) Copia verificada al destino (Google Drive informa un tamaño incompleto justo después de escribir).
#    Se simula el tamaño que informa el disco con -LeerTamano; nunca se escribe en G:.
$tmp = Join-Path $env:TEMP ('sgv-probar-espejo-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $tmp | Out-Null
try {
    $local = Join-Path $tmp 'local.descargando'
    [IO.File]::WriteAllText($local, ('x' * 1000))
    $tam = (Get-Item $local).Length
    $shaLocal = Get-Sha256Archivo $local
    Comprobar 'SHA-256 leyendo el contenido (64 hex)' ($shaLocal -match '^[0-9a-f]{64}$' -and $shaLocal -eq (Get-FileHash $local -Algorithm SHA256).Hash.ToLowerInvariant()) $shaLocal

    # a) el tamaño se ve completo al tercer intento: OK por tamaño
    $script:lecturas = 0
    $tardio = { param($r) $script:lecturas++; if ($script:lecturas -lt 3) { 14520 } else { (Get-Item -LiteralPath $r).Length } }
    $ok = Wait-TamanoArchivo -Ruta $local -Tamano $tam -Intentos 5 -EsperaMs 0 -LeerTamano $tardio
    Comprobar 'Espera: el tamaño cuadra al tercer intento' ($ok -and $script:lecturas -eq 3) "lecturas $($script:lecturas)"
    $siempreMal = { param($r) 14520 }
    Comprobar 'Espera: nunca cuadra -> $false tras los intentos' (-not (Wait-TamanoArchivo -Ruta $local -Tamano $tam -Intentos 4 -EsperaMs 0 -LeerTamano $siempreMal)) ''

    # b) el disco sigue informando 14520 pero el contenido llegó entero: OK por SHA-256, sin error
    $final = Join-Path $tmp 'Uploads\CPIC\a.pdf'
    $c = Copy-DescargaASuSitio -Temporal $local -Final $final -Tamano $tam -Sha $shaLocal -Fecha $f -Intentos 3 -EsperaMs 0 -LeerTamano $siempreMal
    Comprobar 'Destino con tamaño incompleto informado y contenido entero: copiado, verificado por SHA-256' ($c.Resultado -eq 'Copiado' -and $c.Verificacion -eq 'Hash' -and (Get-Sha256Archivo $final) -eq $shaLocal) ($c | Out-String)
    Comprobar 'No queda el .descargando en el destino' (-not (Test-Path "$final.descargando")) ''
    Comprobar 'El temporal local sigue (lo borra el llamador)' (Test-Path $local) ''

    # c) mismo contenido ya en el destino: no se copia ni se crea versión anterior
    $c = Copy-DescargaASuSitio -Temporal $local -Final $final -Tamano $tam -Sha $shaLocal -Fecha $f -Intentos 1 -EsperaMs 0
    Comprobar 'Mismo contenido ya en el destino: Igual, sin versión anterior' ($c.Resultado -eq 'Igual' -and @(Get-ChildItem (Split-Path $final) -File).Count -eq 1) ($c | Out-String)

    # d) contenido distinto: copia verificada por tamaño y conserva la anterior
    $local2 = Join-Path $tmp 'local2.descargando'
    [IO.File]::WriteAllText($local2, ('y' * 1200))
    $c = Copy-DescargaASuSitio -Temporal $local2 -Final $final -Tamano 1200 -Sha (Get-Sha256Archivo $local2) -Fecha $f -Intentos 2 -EsperaMs 0
    Comprobar 'Cambio real: copiado por tamaño, con versión anterior' ($c.Resultado -eq 'Copiado' -and $c.Verificacion -eq 'Tamano' -and $c.Anterior -eq 'a__anterior_20261119_150405.pdf' -and [IO.File]::ReadAllText($final) -eq ('y' * 1200)) ($c | Out-String)

    # e) el destino no coincide ni en tamaño ni en contenido: error, y el final queda como estaba
    $antes = [IO.File]::ReadAllText($final)
    $local3 = Join-Path $tmp 'local3.descargando'
    [IO.File]::WriteAllText($local3, ('z' * 900))
    $fallo = $null
    try { Copy-DescargaASuSitio -Temporal $local3 -Final $final -Tamano 900 -Sha ('0' * 64) -Fecha $f -Intentos 2 -EsperaMs 0 -LeerTamano $siempreMal | Out-Null }
    catch { $fallo = $_.Exception.Message }
    Comprobar 'Ni tamaño ni SHA-256: error' ($fallo -match 'no coincide') "$fallo"
    Comprobar 'Tras el error: el final no cambió y no queda .descargando' ([IO.File]::ReadAllText($final) -eq $antes -and -not (Test-Path "$final.descargando")) ''

    # f) Copy-ArchivoVerificado (lo usa también respaldar-bd.ps1 para el .bacpac)
    $bac = Join-Path $tmp 'bd\x.parcial.bacpac'
    $v = Copy-ArchivoVerificado -Origen $local -Destino $bac -Tamano $tam -Sha $shaLocal -Intentos 2 -EsperaMs 0
    Comprobar 'Copia verificada del .bacpac: por tamaño, crea la carpeta' ($v.Verificacion -eq 'Tamano' -and (Get-Sha256Archivo $bac) -eq $shaLocal) ($v | Out-String)
} finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

""
if ($fallos -gt 0) { Write-Host "$fallos prueba(s) fallaron." -ForegroundColor Red; exit 1 }
Write-Host "Todas las pruebas del espejo pasaron." -ForegroundColor Green
exit 0
