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
$nombres = 'Test-RutaExcluida', 'ConvertFrom-ListadoFtp', 'Get-PlanEspejo', 'Get-NombreVersionAnterior', 'Move-DescargaASuSitio'
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

""
if ($fallos -gt 0) { Write-Host "$fallos prueba(s) fallaron." -ForegroundColor Red; exit 1 }
Write-Host "Todas las pruebas del espejo pasaron." -ForegroundColor Green
exit 0
