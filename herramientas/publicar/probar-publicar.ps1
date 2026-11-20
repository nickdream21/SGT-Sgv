<#
.SYNOPSIS
    Prueba la lógica de publicar.ps1 sin red, sin compilar y sin escribir en el servidor.
.DESCRIPTION
    Extrae las funciones puras de publicar.ps1 (sin ejecutar el script) y comprueba: rutas protegidas
    (App_Data, Uploads, secretos), candidatos a sobrante, plan de subida/borrado (lista remota simulada,
    mayúsculas distintas), orden de subida, comprobaciones del paquete, manifiesto, decisión de
    -VolverA, confirmación BORRAR y el listado recursivo del módulo común con un FTP simulado.
    Sale con código 1 si algo falla. No usa Pester.

    -Paquete <carpeta>: además, usa un paquete real (carpeta sitio\ de una publicación) como lado
    local del caso de sobrantes, con el servidor simulado = ese paquete + páginas retiradas + datos.
.EXAMPLE
    .\herramientas\publicar\probar-publicar.ps1
    .\herramientas\publicar\probar-publicar.ps1 -Paquete D:\PublicacionesSGV\20261119_1504_3b2f8b0\sitio
#>
param([string]$Paquete)
$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot 'publicar.ps1'
$errores = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($script, [ref]$null, [ref]$errores)
if ($errores -and $errores.Count -gt 0) { throw "publicar.ps1 tiene errores de sintaxis: $($errores[0].Message) (línea $($errores[0].Extent.StartLineNumber))" }
$nombres = 'ConvertTo-RutaSitio', 'Test-RutaProtegida', 'Test-RutaCandidataSobrante', 'Get-PlanPublicacion', 'Get-OrdenSubida',
           'Test-ContenidoPaquete', 'ConvertFrom-Manifiesto', 'Compare-PaqueteConManifiesto', 'Get-DecisionVolverA', 'Test-ConfirmacionBorrar',
           'Test-RutaDeControl', 'Get-PasosSubida', 'ConvertTo-TextoMarcador', 'ConvertFrom-Marcador', 'Test-MarcadorCoincide', 'ConvertFrom-CodigoAncestro',
           'Invoke-PasosSubida', 'ConvertTo-TextoMarcadorEnCurso', 'Get-ConfianzaHashes', 'Test-RutaRuntimeNoWindows', 'Test-RutaQueNoSePublica', 'Remove-ArchivosQueNoSePublican',
           'Test-RutaSobranteDeAplicacion', 'Format-PasosSubida', 'Test-RutaNativaSuelta', 'Get-DecisionSobrantesAplicacion', 'Get-DecisionBorrado'
foreach ($n in $nombres) {
    $def = $ast.FindAll({ param($x) $x -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $x.Name -eq $n }, $false) |
           Select-Object -First 1
    if (-not $def) { throw "No se encontró la función $n en publicar.ps1." }
    . ([scriptblock]::Create($def.Extent.Text))
}
Import-Module (Join-Path $PSScriptRoot '..\comun\Ftp.psm1') -Force -DisableNameChecking

$fallos = 0
function Comprobar([string]$caso, [bool]$condicion, [string]$detalle) {
    if ($condicion) { Write-Host "OK     $caso" -ForegroundColor Green }
    else { Write-Host "FALLA  $caso -> $detalle" -ForegroundColor Red; $script:fallos++ }
}
function Iguales([string[]]$a, [string[]]$b) {
    $x = @($a | Sort-Object { $_.ToLowerInvariant() }); $y = @($b | Sort-Object { $_.ToLowerInvariant() })
    if ($x.Count -ne $y.Count) { return $false }
    for ($i = 0; $i -lt $x.Count; $i++) { if ($x[$i] -ine $y[$i]) { return $false } }
    return $true
}
function Rutas($plan, [string[]]$acciones) { @($plan | Where-Object { $acciones -contains $_.Accion } | ForEach-Object { $_.Ruta }) }

# -------------------------------------------------------------------------------------------------
# 1) Rutas protegidas: nunca se suben ni se borran
# -------------------------------------------------------------------------------------------------
foreach ($r in 'App_Data/x', 'App_Data/OrdenesViaje/2026/OV-1.pdf', 'app_data\X.CONFIG', 'Uploads/y.pdf', 'UPLOADS/Factura/_temp/a.js',
               'Views/App_Data/z.aspx', 'App_Data', 'Uploads', 'connectionStrings.config', 'CONNECTIONSTRINGS.CONFIG',
               'connectionStrings.Production.config', 'Views/connectionStrings.config', 'appSettings.Secrets.config', 'APPSETTINGS.SECRETS.CONFIG', '', '/') {
    Comprobar "Protegida: '$r'" (Test-RutaProtegida $r) ''
}
foreach ($r in 'Views/Inicio.aspx', 'App_DataX/a.aspx', 'MisUploads/a.js', 'Web.config', 'connectionStrings.config.example', 'bin/WebSGV.dll',
               'appSettings.config', 'Views/Uploads.aspx') {
    Comprobar "No protegida: '$r'" (-not (Test-RutaProtegida $r)) ''
}

# -------------------------------------------------------------------------------------------------
# 2) Candidatos a sobrante (por extensión o Database/)
# -------------------------------------------------------------------------------------------------
foreach ($r in 'Views/Dashboard.aspx', 'Site.Master', 'x.ASCX', 'Handler.ashx', 'Servicio.asmx', 'App_Code/A.cs', 'Scripts/a.js',
               'Content/a.css', 'bin/Viejo.dll', 'Views/Web.config', 'Database/Schema/01_x.sql', 'database/README.md',
               'bin/runtimes/linux-x64/native/libQuestPdfSkia.so', 'BIN/runtimes/osx-arm64/native/libQuestPdfSkia.dylib') {
    Comprobar "Candidato a sobrante: '$r'" (Test-RutaCandidataSobrante $r) ''
}
foreach ($r in 'Content/logo.png', 'favicon.ico', 'Global.asax', 'robots.txt', 'App_Data/x.config', 'Uploads/a.js',
               'connectionStrings.config', 'appSettings.Secrets.config', 'Views/Archivo.aspx.bak', 'Content/linux/a.so', 'App_Data/runtimes/linux-x64/a.so') {
    Comprobar "No es candidato a sobrante: '$r'" (-not (Test-RutaCandidataSobrante $r)) ''
}

# -------------------------------------------------------------------------------------------------
# 3) Plan: lista remota simulada con las páginas retiradas + datos + secretos (aceptación d)
# -------------------------------------------------------------------------------------------------
$paginasRetiradas = 'Dashboard', 'AgregarIndicadores', 'DashboardGrifo', 'AgregarAbastecimiento', 'BuscarAbastecimiento', 'ReporteAbastecimiento',
    'RegistrarDespachoObra', 'RegistrarRetornoEcuador', 'DescargarPdfAbastecimiento', 'AsignacionesMaquinaria', 'DashboardOperador', 'RegistroOperadores',
    'RegistroEquiposMaquinaria', 'RegistroObras', 'RegistroClientesObra', 'EditarDespacho', 'TestGenerarPdfOrdenViaje', 'ConsultaAuditoria',
    'AgregarOrdenViaje', 'BuscarOrdenViaje', 'AgregarCPIC', 'BuscarCPIC', 'AgregarFactura', 'BuscarFactura'
$cssRetirados = 'AgregarAbastecimiento', 'AgregarIndicadores', 'BuscarAbastecimiento', 'Dashboard', 'DashboardGrifo', 'DashboardOperador', 'EditarDespacho',
    'RegistrarDespachoObra', 'RegistrarRetornoEcuador', 'ReporteAbastecimiento', 'AgregarCPIC', 'AgregarFactura', 'AgregarOrdenViaje', 'BuscarCPIC', 'BuscarFactura'
$jsRetirados = 'AgregarAbastecimiento', 'BuscarAbastecimiento', 'Dashboard', 'DashboardGrifo', 'DashboardOperador', 'EditarDespacho', 'RegistrarDespachoObra',
    'RegistrarRetornoEcuador', 'AgregarCPIC', 'AgregarFactura', 'AgregarOrdenViaje', 'BuscarCPIC', 'BuscarFactura', 'BuscarOrdenViaje', 'ConsultaAuditoria'
$sobrantesEsperados = @($paginasRetiradas | ForEach-Object { "Views/$_.aspx" }) +
                      @($cssRetirados | ForEach-Object { "Content/paginas/$_.css" }) +
                      @($jsRetirados | ForEach-Object { "Scripts/paginas/$_.js" }) +
                      @('Scripts/BuscarOrdenViaje.js')

function New-Archivo($ruta, $tam, $sha) { New-Object PSObject -Property @{ Ruta = $ruta; Tamano = [long]$tam; Sha = $sha } }

if ($Paquete) {
    $raizP = (Resolve-Path -LiteralPath $Paquete).Path.TrimEnd('\') + '\'
    $paq = @(Get-ChildItem -LiteralPath $raizP -Recurse -File | ForEach-Object { New-Archivo ($_.FullName.Substring($raizP.Length).Replace('\', '/')) $_.Length 'x' })
    Write-Host "(usando el paquete real $Paquete : $($paq.Count) archivos)"
} else {
    $paq = @(
        (New-Archivo 'Views/Inicio.aspx' 100 'a1'), (New-Archivo 'Web.config' 50 'w1'), (New-Archivo 'bin/WebSGV.dll' 1000 'd1'),
        (New-Archivo 'Content/paginas/Inicio.css' 10 'c1'), (New-Archivo 'Scripts/paginas/Inicio.js' 20 'j1'), (New-Archivo 'Site.Master' 30 'm1'),
        (New-Archivo 'Content/logo.png' 40 'p1'), (New-Archivo 'bin/runtimes/win-x86/native/QuestPdfSkia.dll' 500 'q1'))
}
# El servidor tiene todo el paquete (con otras mayúsculas en varias rutas), más lo retirado, datos y secretos.
$remotos = @()
$k = 0
foreach ($p in $paq) {
    $k++
    $ruta = if ($k % 2 -eq 0) { $p.Ruta.ToUpperInvariant() } else { $p.Ruta.ToLowerInvariant() }
    $remotos += New-Archivo $ruta $p.Tamano $null
}
$remotos += @($sobrantesEsperados | ForEach-Object { New-Archivo $_ 123 $null })
$remotos += @(
    (New-Archivo 'App_Data/x' 1), (New-Archivo 'Uploads/y.pdf' 2), (New-Archivo 'connectionStrings.config' 3),
    (New-Archivo 'appSettings.Secrets.config' 4), (New-Archivo 'App_Data/OrdenesViaje/2026/11/OV-1.pdf' 5),
    (New-Archivo 'Uploads/Factura/viejo.aspx' 6), (New-Archivo 'App_Data/config/otro.config' 7),
    (New-Archivo 'Content/imagen-vieja.png' 8),                               # no es código: no es sobrante
    (New-Archivo 'app_offline.htm' 9), (New-Archivo 'PUBLICACION.TXT' 10))    # archivos de control: nunca sobrantes
$plan = @(Get-PlanPublicacion -Paquete $paq -Remotos $remotos)
$sobr = Rutas $plan 'Sobrante', 'Borrar'
Comprobar "(d) Sobrantes = exactamente las páginas retiradas y sus CSS/JS ($($sobrantesEsperados.Count))" (Iguales $sobr $sobrantesEsperados) (
    "sobran: " + ((@($sobr | Where-Object { $sobrantesEsperados -inotcontains $_ })) -join ', ') + " | faltan: " + ((@($sobrantesEsperados | Where-Object { $sobr -inotcontains $_ })) -join ', '))
Comprobar '(d) Sin -BorrarSobrantes todo es Sobrante (nada Borrar)' (@(Rutas $plan 'Borrar').Count -eq 0) ''
$tocaProtegidas = @($plan | Where-Object { $_.Accion -ne 'Igual' -and (Test-RutaProtegida $_.Ruta) })
Comprobar '(d) Nada del plan toca App_Data, Uploads ni secretos' ($tocaProtegidas.Count -eq 0) (($tocaProtegidas | ForEach-Object { $_.Ruta }) -join ', ')
$nuevos = @($plan | Where-Object { $_.Motivo -eq 'nuevo' })
Comprobar '(d) Mayúsculas distintas: nada del paquete sale como nuevo' ($nuevos.Count -eq 0) (($nuevos | ForEach-Object { $_.Ruta }) -join ', ')

$planB = @(Get-PlanPublicacion -Paquete $paq -Remotos $remotos -BorrarSobrantes)
Comprobar 'Con -BorrarSobrantes: Borrar = los mismos sobrantes' (Iguales (Rutas $planB 'Borrar') $sobrantesEsperados) ''
Comprobar 'Con -BorrarSobrantes: nunca Borrar en App_Data/Uploads/secretos' (@($planB | Where-Object { $_.Accion -eq 'Borrar' -and (Test-RutaProtegida $_.Ruta) }).Count -eq 0) ''

# Qué se sube
$paqS = @(
    (New-Archivo 'Views/Nuevo.aspx' 10 'n1'),         # no está en el servidor        -> Subir (nuevo)
    (New-Archivo 'Views/Cambio.aspx' 20 'c2'),        # tamaño distinto              -> Subir (tamaño distinto)
    (New-Archivo 'bin/WebSGV.dll' 1000 'd2'),         # mismo tamaño, otro SHA       -> Subir (sin confirmar)
    (New-Archivo 'Views/Igual.aspx' 30 'i1'),         # mismo tamaño y SHA publicado -> Igual
    (New-Archivo 'Views/SinRegistro.aspx' 40 's1'),   # mismo tamaño, sin registro   -> Subir (sin confirmar)
    (New-Archivo 'Views/Desconocido.aspx' 50 'u1'),   # tamaño remoto desconocido    -> Subir (sin confirmar)
    (New-Archivo 'App_Data/coló.pdf' 5 'x'),          # protegida en el paquete      -> nunca aparece
    (New-Archivo 'connectionStrings.config' 5 'x'))   # secreto en el paquete        -> nunca aparece
$remS = @((New-Archivo 'views/CAMBIO.aspx' 25), (New-Archivo 'BIN/websgv.DLL' 1000), (New-Archivo 'Views/igual.ASPX' 30),
          (New-Archivo 'Views/SinRegistro.aspx' 40), (New-Archivo 'Views/Desconocido.aspx' -1), (New-Archivo 'Uploads/z.pdf' 1))
$pub = @{ 'bin/WebSGV.dll' = 'd1'; 'VIEWS/IGUAL.ASPX' = 'I1' }
$planS = @(Get-PlanPublicacion -Paquete $paqS -Remotos $remS -HashesPublicados $pub)
function Paso($ruta) { $planS | Where-Object { $_.Ruta -ieq $ruta } | Select-Object -First 1 }
Comprobar 'Subir: nuevo' ((Paso 'Views/Nuevo.aspx').Accion -eq 'Subir' -and (Paso 'Views/Nuevo.aspx').Motivo -eq 'nuevo') ((Paso 'Views/Nuevo.aspx') | Out-String)
Comprobar 'Subir: tamaño distinto (mayúsculas distintas)' ((Paso 'Views/Cambio.aspx').Motivo -eq 'tamaño distinto') ((Paso 'Views/Cambio.aspx') | Out-String)
Comprobar 'Subir: mismo tamaño pero SHA distinto al publicado' ((Paso 'bin/WebSGV.dll').Accion -eq 'Subir' -and (Paso 'bin/WebSGV.dll').Motivo -eq 'sin confirmar') ((Paso 'bin/WebSGV.dll') | Out-String)
Comprobar 'Igual: mismo tamaño y mismo SHA publicado (sin distinguir mayúsculas)' ((Paso 'Views/Igual.aspx').Accion -eq 'Igual') ((Paso 'Views/Igual.aspx') | Out-String)
Comprobar 'Subir: mismo tamaño sin registro de lo publicado' ((Paso 'Views/SinRegistro.aspx').Motivo -eq 'sin confirmar') ''
Comprobar 'Subir: tamaño remoto desconocido' ((Paso 'Views/Desconocido.aspx').Accion -eq 'Subir') ''
Comprobar 'Nunca sube App_Data ni secretos aunque vengan en el paquete' ($null -eq (Paso 'App_Data/coló.pdf') -and $null -eq (Paso 'connectionStrings.config')) ''
Comprobar 'Uploads/z.pdf del servidor no es sobrante' ($null -eq (Paso 'Uploads/z.pdf')) ''
$planT = @(Get-PlanPublicacion -Paquete $paqS -Remotos $remS -HashesPublicados $pub -SubirTodo)
Comprobar '-SubirTodo: también sube el que era Igual' (@($planT | Where-Object { $_.Accion -eq 'Igual' }).Count -eq 0 -and @($planT | Where-Object { $_.Accion -eq 'Subir' }).Count -eq 6) ($planT | Out-String)
Comprobar 'Servidor vacío: todo nuevo, ningún sobrante' (@(Get-PlanPublicacion -Paquete $paqS -Remotos @() | Where-Object { $_.Motivo -ne 'nuevo' }).Count -eq 0) ''

# Orden de subida: contenido, luego bin/, al final Web.config
$orden = @('Web.config', 'bin/WebSGV.dll', 'Views/A.aspx', 'Views/Web.config', 'BIN/x.dll') | Sort-Object @{ e = { Get-OrdenSubida $_ } }, { $_ }
Comprobar 'Orden de subida' (($orden -join ',') -eq 'Views/A.aspx,Views/Web.config,bin/WebSGV.dll,BIN/x.dll,Web.config') ($orden -join ',')
Comprobar 'Orden de subida: app_offline.htm y publicacion.txt no son del paquete (-1)' ((Get-OrdenSubida 'APP_OFFLINE.HTM') -eq -1 -and (Get-OrdenSubida 'publicacion.txt') -eq -1) ''

# Archivos de control (app_offline.htm, publicacion.txt): ni protegidos ni sobrantes; si vienen en el paquete se ignoran
foreach ($r in 'app_offline.htm', 'App_Offline.HTM', 'publicacion.txt', '/PUBLICACION.TXT') {
    Comprobar "Control: '$r' no es protegido ni sobrante" ((Test-RutaDeControl $r) -and -not (Test-RutaProtegida $r) -and -not (Test-RutaCandidataSobrante $r)) ''
}
Comprobar "Control: 'Views/app_offline.htm' no es archivo de control de la raíz" (-not (Test-RutaDeControl 'Views/app_offline.htm')) ''
$planC = @(Get-PlanPublicacion -Paquete @((New-Archivo 'app_offline.htm' 5 'x'), (New-Archivo 'publicacion.txt' 5 'y'), (New-Archivo 'Views/A.aspx' 5 'z')) -Remotos @((New-Archivo 'app_offline.htm' 5)) -BorrarSobrantes)
Comprobar 'Control: el plan nunca sube ni borra app_offline.htm/publicacion.txt' ((@($planC | ForEach-Object { $_.Ruta }) -join ',') -eq 'Views/A.aspx') ($planC | Out-String)

# Pasos de la subida (C: con bin/ o Web.config, app_offline.htm va ANTES del contenido; A: EN CURSO siempre primero)
function Pasos($rutas, [switch]$Mant) { (@(Get-PasosSubida -Rutas $rutas -MantenimientoEnServidor:$Mant) | ForEach-Object { $_.Paso }) -join ',' }
Comprobar '(C) Pasos con bin y Web.config: EnCurso, Mantenimiento, Contenido, Bin, WebConfig, Quitar, Marcador' ((Pasos @('Web.config', 'bin/WebSGV.dll', 'Views/A.aspx')) -eq 'MarcadorEnCurso,Mantenimiento,Contenido,Bin,WebConfig,QuitarMantenimiento,Marcador') (Pasos @('Web.config', 'bin/WebSGV.dll', 'Views/A.aspx'))
Comprobar '(C) Pasos solo contenido -> sin app_offline' ((Pasos @('Views/A.aspx', 'Content/a.css')) -eq 'MarcadorEnCurso,Contenido,Marcador') (Pasos @('Views/A.aspx', 'Content/a.css'))
Comprobar '(C) Pasos solo bin -> Mantenimiento antes de Bin' ((Pasos @('BIN/x.dll')) -eq 'MarcadorEnCurso,Mantenimiento,Bin,QuitarMantenimiento,Marcador') (Pasos @('BIN/x.dll'))
Comprobar '(C) Pasos contenido + Web.config -> Mantenimiento antes del contenido' ((Pasos @('web.config', 'Views/B.aspx')) -eq 'MarcadorEnCurso,Mantenimiento,Contenido,WebConfig,QuitarMantenimiento,Marcador') (Pasos @('web.config', 'Views/B.aspx'))
Comprobar 'Pasos: nada que subir y app_offline olvidado en el servidor -> se quita' ((Pasos @() -Mant) -eq 'MarcadorEnCurso,QuitarMantenimiento,Marcador') (Pasos @() -Mant)
Comprobar 'Pasos: contenido y app_offline olvidado -> se quita al final' ((Pasos @('Views/A.aspx') -Mant) -eq 'MarcadorEnCurso,Contenido,QuitarMantenimiento,Marcador') (Pasos @('Views/A.aspx') -Mant)
$primeros = @(@(@('Views/A.aspx'), @('bin/a.dll'), @('Web.config'), @()) | ForEach-Object { (@(Get-PasosSubida -Rutas $_))[0].Paso })
Comprobar '(A) Pasos: el marcador EN CURSO es siempre el primero' (($primeros.Count -eq 4) -and (@($primeros | Where-Object { $_ -ne 'MarcadorEnCurso' }).Count -eq 0)) ($primeros -join ',')
Comprobar 'Pasos: app_offline.htm/publicacion.txt del paquete no se suben como contenido' ((Pasos @('app_offline.htm', 'publicacion.txt', 'Views/A.aspx')) -eq 'MarcadorEnCurso,Contenido,Marcador') ''
$pb = @(Get-PasosSubida -Rutas @('bin/b.dll', 'Views/A.aspx', 'bin/a.dll', 'Web.config'))
Comprobar 'Pasos: cada grupo con sus rutas (bin ordenado)' ((($pb | Where-Object Paso -eq 'Bin').Rutas -join ',') -eq 'bin/a.dll,bin/b.dll' -and (($pb | Where-Object Paso -eq 'Contenido').Rutas -join ',') -eq 'Views/A.aspx' -and (($pb | Where-Object Paso -eq 'WebConfig').Rutas -join ',') -eq 'Web.config') ''

# E1-10: los sobrantes se borran DENTRO de la ventana de mantenimiento (antes de QuitarMantenimiento)
foreach ($r in 'bin/System.Web.Mvc.dll', 'BIN/es/x.resources.dll', 'bin/runtimes/linux-x64/native/a.so', 'Views/web.config', 'Views/WebForm2.aspx',
               'Controles/X.ascx', 'Viejo.Master', 'Handler.ashx', 'Servicio.asmx', 'Otro.asax', 'App_Code/A.cs') {
    Comprobar "Sobrante de aplicación (fuerza mantenimiento): '$r'" (Test-RutaSobranteDeAplicacion $r) ''
}
foreach ($r in 'Content/a.css', 'Scripts/a.js', 'Database/Schema/01_x.sql', 'Content/logo.png', 'binario/a.js') {
    Comprobar "Sobrante estático (no fuerza mantenimiento): '$r'" (-not (Test-RutaSobranteDeAplicacion $r)) ''
}
function PasosS($rutas, $sobr, [switch]$Mant) { (@(Get-PasosSubida -Rutas $rutas -Sobrantes $sobr -MantenimientoEnServidor:$Mant) | ForEach-Object { $_.Paso }) -join ',' }
$e = PasosS @('Web.config', 'bin/WebSGV.dll', 'Views/A.aspx') @('Views/Viejo.aspx', 'bin/System.Web.Mvc.dll')
Comprobar '(E1-10) Orden: ... WebConfig, Sobrantes, QuitarMantenimiento, Marcador' ($e -eq 'MarcadorEnCurso,Mantenimiento,Contenido,Bin,WebConfig,Sobrantes,QuitarMantenimiento,Marcador') $e
$e = PasosS @('Views/A.aspx') @('bin/System.Web.Mvc.dll')
Comprobar '(E1-10) Solo contenido + sobrante de bin -> se fuerza el mantenimiento' ($e -eq 'MarcadorEnCurso,Mantenimiento,Contenido,Sobrantes,QuitarMantenimiento,Marcador') $e
$e = PasosS @('Content/a.css') @('Views/WebForm2.aspx')
Comprobar '(E1-10) Solo contenido + página retirada -> se fuerza el mantenimiento' ($e -eq 'MarcadorEnCurso,Mantenimiento,Contenido,Sobrantes,QuitarMantenimiento,Marcador') $e
$e = PasosS @() @('Views/web.config')
Comprobar '(E1-10) Nada que subir + Views/web.config sobrante -> mantenimiento y borrado' ($e -eq 'MarcadorEnCurso,Mantenimiento,Sobrantes,QuitarMantenimiento,Marcador') $e
$e = PasosS @('Views/A.aspx') @('Content/viejo.css', 'Scripts/viejo.js', 'Database/Schema/01_x.sql')
Comprobar '(E1-10) Solo sobrantes estáticos -> sin mantenimiento' ($e -eq 'MarcadorEnCurso,Contenido,Sobrantes,Marcador') $e
$e = PasosS @('Views/A.aspx') @('Content/viejo.css') -Mant
Comprobar '(E1-10) Sobrantes estáticos y app_offline olvidado -> se borran antes de quitarlo' ($e -eq 'MarcadorEnCurso,Contenido,Sobrantes,QuitarMantenimiento,Marcador') $e
$e = PasosS @('bin/WebSGV.dll', 'Views/A.aspx') @()
Comprobar '(E1-10) Sin -BorrarSobrantes (lista vacía) no hay paso Sobrantes' ($e -eq 'MarcadorEnCurso,Mantenimiento,Contenido,Bin,QuitarMantenimiento,Marcador') $e
$e = PasosS @('bin/WebSGV.dll') $null
Comprobar '(E1-10) Sobrantes $null tampoco crea el paso' ($e -eq 'MarcadorEnCurso,Mantenimiento,Bin,QuitarMantenimiento,Marcador') $e
$ps = @(Get-PasosSubida -Rutas @('bin/WebSGV.dll') -Sobrantes @('bin/Zeta.dll', 'Views/web.config', 'BIN/Alfa.dll', 'Content/z.css', 'Views/Viejo.aspx'))
$rs = @(($ps | Where-Object Paso -eq 'Sobrantes').Rutas)
Comprobar '(E1-10) Sobrantes: páginas y .config de subcarpetas antes que bin/' (($rs -join ',') -eq 'Content/z.css,Views/Viejo.aspx,Views/web.config,BIN/Alfa.dll,bin/Zeta.dll') ($rs -join ',')
$ps = @(Get-PasosSubida -Rutas @() -Sobrantes @('App_Data/a.config', 'Uploads/b.aspx', 'connectionStrings.config', 'appSettings.Secrets.config', 'app_offline.htm', 'publicacion.txt', 'Content/logo.png', 'Views/A.aspx'))
$rs = @(($ps | Where-Object Paso -eq 'Sobrantes').Rutas)
Comprobar '(E1-10) Sobrantes: nunca protegidos, de control ni no candidatos' (($rs -join ',') -eq 'Views/A.aspx') ($rs -join ',')
$e = Format-PasosSubida @(Get-PasosSubida -Rutas @('Views/A.aspx', 'Views/B.aspx', 'bin/a.dll', 'Web.config') -Sobrantes @('Views/X.aspx', 'bin/y.dll'))
Comprobar '(E1-10) Texto de los pasos con cantidades' ($e -eq 'MarcadorEnCurso -> Mantenimiento -> Contenido (2) -> Bin (1) -> WebConfig (1) -> Sobrantes (2) -> QuitarMantenimiento -> Marcador') $e

# E1-10: DLL nativas sueltas (lista explícita en la raíz de bin/ + bin/runtimes/**)
foreach ($r in 'bin/libgcc_s_dw2-1.dll', 'bin/libstdc++-6.dll', 'bin/libwinpthread-1.dll', 'bin/qpdf.dll', 'BIN/QUESTPDFSKIA.DLL', 'bin/zlib1.dll',
               'bin/runtimes/linux-x64/native/libqpdf.so', 'bin/runtimes/win-x86/native/Viejo.dll') {
    Comprobar "Nativa suelta (su DELE puede fallar sin error): '$r'" (Test-RutaNativaSuelta $r) ''
}
foreach ($r in 'bin/System.Web.Mvc.dll', 'bin/EPPlus.dll', 'bin/itextsharp.dll', 'bin/es/System.Web.Mvc.resources.dll', 'bin/sub/qpdf.dll',
               'Views/qpdf.dll', 'qpdf.dll', 'bin/qpdf.dll.bak', 'bin/runtimes', 'bin/zlib.dll') {
    Comprobar "No es nativa suelta (si falla su borrado, es error): '$r'" (-not (Test-RutaNativaSuelta $r)) ''
}
$ps = @(Get-PasosSubida -Rutas @() -Sobrantes @('bin/zlib1.dll', 'bin/System.Web.Mvc.dll', 'bin/runtimes/osx-x64/native/a.dylib', 'Views/web.config', 'bin/Antlr3.Runtime.dll', 'bin/QuestPdfSkia.dll'))
$rs = @(($ps | Where-Object Paso -eq 'Sobrantes').Rutas)
Comprobar '(E1-10) Sobrantes: páginas/.config, luego DLL administradas, al final de todo las nativas' (($rs -join ',') -eq 'Views/web.config,bin/Antlr3.Runtime.dll,bin/System.Web.Mvc.dll,bin/QuestPdfSkia.dll,bin/runtimes/osx-x64/native/a.dylib,bin/zlib1.dll') ($rs -join ',')

# E1-10: sin -BorrarSobrantes, los sobrantes de aplicación cancelan la publicación (antes de escribir)
$d = Get-DecisionSobrantesAplicacion -Sobrantes @('Views/WebForm2.aspx', 'Content/a.css', 'bin/System.Web.Mvc.dll')
Comprobar '(E1-10) Sobrantes de aplicación sin -BorrarSobrantes -> no se permite' (-not $d.Permitido -and (Iguales $d.DeAplicacion @('Views/WebForm2.aspx', 'bin/System.Web.Mvc.dll'))) ($d | Out-String)
$d = Get-DecisionSobrantesAplicacion -Sobrantes @('Views/web.config')
Comprobar '(E1-10) Un Views/web.config sobrante también bloquea' (-not $d.Permitido) ''
$d = Get-DecisionSobrantesAplicacion -Sobrantes @('Views/WebForm2.aspx') -BorrarSobrantes
Comprobar '(E1-10) Con -BorrarSobrantes -> se permite (se borrarán)' ($d.Permitido) ''
$d = Get-DecisionSobrantesAplicacion -Sobrantes @('Views/WebForm2.aspx') -PermitirSobrantesDeAplicacion
Comprobar '(E1-10) Con -PermitirSobrantesDeAplicacion -> se permite a sabiendas' ($d.Permitido -and $d.DeAplicacion.Count -eq 1) ''
$d = Get-DecisionSobrantesAplicacion -Sobrantes @('Content/a.css', 'Scripts/b.js', 'Database/x.sql', 'bin/QuestPdfSkia.dll', 'bin/zlib1.dll', 'bin/runtimes/linux-x64/native/a.so')
Comprobar '(E1-10) Solo estáticos y nativas sueltas -> no bloquea' ($d.Permitido -and $d.DeAplicacion.Count -eq 0) ($d.DeAplicacion -join ', ')
$d = Get-DecisionSobrantesAplicacion -Sobrantes @()
Comprobar '(E1-10) Sin sobrantes -> se permite' ($d.Permitido) ''
$d = Get-DecisionSobrantesAplicacion -Sobrantes @('App_Data/x.config', 'Uploads/a.aspx', 'connectionStrings.config')
Comprobar '(E1-10) Protegidos nunca bloquean (no son sobrantes)' ($d.Permitido) ($d.DeAplicacion -join ', ')

# E1-10: confirmación BORRAR: con otra respuesta se CANCELA (no se publica sin borrar)
$d = Get-DecisionBorrado -Cantidad 261 -Respuesta 'BORRAR'
Comprobar '(E1-10) Borrado: BORRAR -> sigue y borra' ($d.Preguntar -and $d.Continuar -and $d.Borrar) ($d | Out-String)
foreach ($t in 'borrar', '', $null, 'SI', 'BORRAR TODO') {
    $d = Get-DecisionBorrado -Cantidad 3 -Respuesta $t
    Comprobar "(E1-10) Borrado: '$t' -> se cancela (ni sigue ni borra)" ($d.Preguntar -and -not $d.Continuar -and -not $d.Borrar) ($d | Out-String)
}
$d = Get-DecisionBorrado -Cantidad 0 -Respuesta $null
Comprobar '(E1-10) Borrado: sin sobrantes -> no pregunta y sigue sin borrar' (-not $d.Preguntar -and $d.Continuar -and -not $d.Borrar) ($d | Out-String)

# Marcador publicacion.txt
$txtM = ConvertTo-TextoMarcador -Paquete '20261119_1610_3b2f8b0' -Commit '3b2f8b02be0fb3c13a37adc2513623f99ef378ff' -Fecha (Get-Date '2026-11-19 16:10:24') -Equipo 'PC1'
$mk = ConvertFrom-Marcador $txtM
Comprobar 'Marcador: ida y vuelta' ($mk.Paquete -eq '20261119_1610_3b2f8b0' -and $mk.Commit -eq '3b2f8b02be0fb3c13a37adc2513623f99ef378ff') $txtM
Comprobar 'Marcador: coincide con la última publicación local' (Test-MarcadorCoincide -Marcador $mk -Paquete '20261119_1610_3B2F8B0' -Commit '3B2F8B02BE0FB3C13A37ADC2513623F99EF378FF') ''
Comprobar 'Marcador: otro paquete (otra máquina publicó) -> no coincide' (-not (Test-MarcadorCoincide -Marcador $mk -Paquete '20261120_0900_aaaaaaa' -Commit '3b2f8b02be0fb3c13a37adc2513623f99ef378ff')) ''
Comprobar 'Marcador: mismo paquete, otro commit -> no coincide' (-not (Test-MarcadorCoincide -Marcador $mk -Paquete '20261119_1610_3b2f8b0' -Commit 'ffff')) ''
Comprobar 'Marcador: no existe en el servidor -> no coincide' (-not (Test-MarcadorCoincide -Marcador (ConvertFrom-Marcador '') -Paquete '20261119_1610_3b2f8b0' -Commit '3b2f')) ''
Comprobar 'Marcador: sin registro local -> no coincide' (-not (Test-MarcadorCoincide -Marcador $mk -Paquete $null -Commit $null)) ''
Comprobar 'Marcador: texto sin Paquete -> $null' ($null -eq (ConvertFrom-Marcador "hola`r`nCommit: abc")) ''
Comprobar 'Marcador normal: EnCurso = $false' (-not $mk.EnCurso) ''

# (A) Marcador EN CURSO: nunca coincide con ningún ultimo-publicado.txt
$txtEC = ConvertTo-TextoMarcadorEnCurso -Paquete '20261119_1610_3b2f8b0' -Fecha (Get-Date '2026-11-19 16:12:00') -Equipo 'PC1'
$mec = ConvertFrom-Marcador $txtEC
Comprobar '(A) EN CURSO: la primera línea lo dice' ($txtEC -match '^EN CURSO 20261119_1610_3b2f8b0 2026-11-19 16:12:00') $txtEC
Comprobar '(A) EN CURSO: se lee como EnCurso' ($mec -and $mec.EnCurso) ($mec | Out-String)
Comprobar '(A) EN CURSO: no coincide ni con el mismo paquete' (-not (Test-MarcadorCoincide -Marcador $mec -Paquete '20261119_1610_3b2f8b0' -Commit 'EN-CURSO')) ''
Comprobar '(A) EN CURSO: no coincide aunque el local diga literalmente lo mismo' (-not (Test-MarcadorCoincide -Marcador $mec -Paquete $mec.Paquete -Commit $mec.Commit)) ''

# (A/B) Decisión de confiar en los SHA-256 locales
$fM = Get-Date '2026-11-19 16:20:00'
function Conf($m, $p, $c, $fm, $fd) { Get-ConfianzaHashes -Marcador $m -Paquete $p -Commit $c -FechaMarcador $fm -FechaDll $fd }
$okP = '20261119_1610_3b2f8b0'; $okC = '3b2f8b02be0fb3c13a37adc2513623f99ef378ff'
$r = Conf $mk $okP $okC $fM $fM.AddMinutes(-2)
Comprobar 'Confianza: marcador coincide y DLL más vieja -> se confía' ($r.Confiar) $r.Motivo
$r = Conf $mk $okP $okC $fM $fM
Comprobar 'Confianza: DLL con la misma fecha que el marcador -> se confía' ($r.Confiar) $r.Motivo
$r = Conf $mec $okP $okC $fM $fM.AddMinutes(-2)
Comprobar '(A) Confianza: marcador EN CURSO (publicación fallida) -> no' (-not $r.Confiar -and $r.Motivo -match 'no terminó') $r.Motivo
$r = Conf $null $okP $okC $fM $fM
Comprobar 'Confianza: sin marcador -> no' (-not $r.Confiar -and $r.Motivo -match 'no tiene marcador') $r.Motivo
$r = Conf $mk $null $null $fM $fM
Comprobar 'Confianza: sin registro local -> no' (-not $r.Confiar -and $r.Motivo -match 'registro') $r.Motivo
$r = Conf $mk '20261120_0900_aaaaaaa' 'aaaa' $fM $fM
Comprobar 'Confianza: publicó otra máquina -> no' (-not $r.Confiar -and $r.Motivo -match 'otra máquina') $r.Motivo
$r = Conf $mk $okP $okC $fM $fM.AddMinutes(5)
Comprobar '(B) Confianza: DLL más nueva que el marcador (Visual Studio) -> no' (-not $r.Confiar -and $r.Motivo -match 'Visual Studio') $r.Motivo
$r = Conf $mk $okP $okC $null $fM
Comprobar '(B) Confianza: MDTM del marcador falló -> no' (-not $r.Confiar -and $r.Motivo -match 'MDTM') $r.Motivo
$r = Conf $mk $okP $okC $fM $null
Comprobar '(B) Confianza: MDTM de la DLL falló (o no existe) -> no' (-not $r.Confiar -and $r.Motivo -match 'MDTM') $r.Motivo

# Ejecución de los pasos con una acción simulada (sin red)
function Simular($rutas, [string[]]$fallan, [switch]$Mant, [string[]]$sobr) {
    $script:hechas = New-Object System.Collections.Generic.List[string]
    $acc = { param($paso, $ruta)
        $script:hechas.Add("$paso|$ruta")
        if ($fallan -contains "$paso|$ruta" -or $fallan -contains $paso) { throw "fallo simulado en $paso $ruta" } }
    $pasosS = @(Get-PasosSubida -Rutas $rutas -Sobrantes $sobr -MantenimientoEnServidor:$Mant)
    $res = Invoke-PasosSubida -Pasos $pasosS -Accion $acc -MantenimientoEnServidor:$Mant 6>$null
    return $res
}
$todo = @('Views/A.aspx', 'Views/B.aspx', 'bin/WebSGV.dll', 'Web.config')
$r = Simular $todo @()
Comprobar 'Ejecutar: todo bien -> sin errores, mantenimiento quitado, 4 subidos' ($r.Errores -eq 0 -and -not $r.MantenimientoPuesto -and $r.Subidos -eq 4) ($r | Out-String)
Comprobar '(C) Ejecutar: orden real EnCurso, app_offline, contenido, bin, Web.config, quitar' (($script:hechas -join ';') -eq 'MarcadorEnCurso|publicacion.txt;Mantenimiento|app_offline.htm;Contenido|Views/A.aspx;Contenido|Views/B.aspx;Bin|bin/WebSGV.dll;WebConfig|Web.config;QuitarMantenimiento|app_offline.htm') ($script:hechas -join ';')
Comprobar 'Ejecutar: el marcador definitivo no lo sube el ejecutor' (@($script:hechas | Where-Object { $_ -like 'Marcador|*' }).Count -eq 0) ''
$r = Simular $todo @('Contenido|Views/A.aspx')
Comprobar 'Ejecutar: falla el contenido -> termina el paso pero no sube bin ni Web.config' ($r.Errores -eq 1 -and $r.PasoFallido -eq 'Contenido' -and ($script:hechas -contains 'Contenido|Views/B.aspx') -and -not ($script:hechas | Where-Object { $_ -like 'Bin|*' -or $_ -like 'WebConfig|*' })) ($script:hechas -join ';')
Comprobar 'Ejecutar: falla el contenido -> app_offline queda puesto y se avisa' ($r.MantenimientoPuesto) ''
$r = Simular $todo @('Bin|bin/WebSGV.dll')
Comprobar 'Ejecutar: falla bin -> no sube Web.config ni quita app_offline' ($r.PasoFallido -eq 'Bin' -and $r.MantenimientoPuesto -and -not ($script:hechas | Where-Object { $_ -like 'WebConfig|*' -or $_ -like 'QuitarMantenimiento|*' })) ($script:hechas -join ';')
$r = Simular $todo @('MarcadorEnCurso')
Comprobar '(A) Ejecutar: si no se puede escribir EN CURSO no se toca nada más' ($r.PasoFallido -eq 'MarcadorEnCurso' -and $script:hechas.Count -eq 1 -and -not $r.MantenimientoPuesto) ($script:hechas -join ';')
$r = Simular $todo @('Mantenimiento')
Comprobar '(D) Ejecutar: falla la subida de app_offline.htm -> igual cuenta como puesto (pudo llegar)' ($r.PasoFallido -eq 'Mantenimiento' -and $r.MantenimientoPuesto -and -not ($script:hechas | Where-Object { $_ -like 'Contenido|*' })) ($r | Out-String)
$r = Simular $todo @('QuitarMantenimiento')
Comprobar 'Ejecutar: no se pudo quitar app_offline.htm -> sigue puesto' ($r.PasoFallido -eq 'QuitarMantenimiento' -and $r.MantenimientoPuesto) ''
$r = Simular @('Views/A.aspx') @()
Comprobar 'Ejecutar: solo contenido -> nunca pone app_offline' ($r.Errores -eq 0 -and -not $r.MantenimientoPuesto -and -not ($script:hechas | Where-Object { $_ -like 'Mantenimiento|*' })) ($script:hechas -join ';')
$r = Simular @('Views/A.aspx') @() -Mant
Comprobar 'Ejecutar: app_offline olvidado en el servidor -> se quita al terminar bien' ($r.Errores -eq 0 -and -not $r.MantenimientoPuesto -and ($script:hechas -contains 'QuitarMantenimiento|app_offline.htm')) ($script:hechas -join ';')
$r = Simular @('Views/A.aspx') @('Contenido') -Mant
Comprobar 'Ejecutar: app_offline olvidado y falla el contenido -> sigue puesto' ($r.MantenimientoPuesto) ''

# E1-10: ejecución con sobrantes
$sobrS = @('bin/System.Web.Mvc.dll', 'Views/web.config', 'Views/WebForm2.aspx')
$r = Simular $todo @() -sobr $sobrS
Comprobar '(E1-10) Ejecutar: borra antes de quitar el mantenimiento, bin al final' (($script:hechas -join ';') -eq 'MarcadorEnCurso|publicacion.txt;Mantenimiento|app_offline.htm;Contenido|Views/A.aspx;Contenido|Views/B.aspx;Bin|bin/WebSGV.dll;WebConfig|Web.config;Sobrantes|Views/web.config;Sobrantes|Views/WebForm2.aspx;Sobrantes|bin/System.Web.Mvc.dll;QuitarMantenimiento|app_offline.htm') ($script:hechas -join ';')
Comprobar '(E1-10) Ejecutar: todo bien -> 3 borrados, 4 subidos, mantenimiento quitado' ($r.Errores -eq 0 -and $r.Borrados -eq 3 -and $r.Subidos -eq 4 -and -not $r.MantenimientoPuesto) ($r | Out-String)
$r = Simular $todo @('Sobrantes|Views/web.config') -sobr $sobrS
Comprobar '(E1-10) Ejecutar: falla un borrado -> no borra bin/ ni quita el mantenimiento' ($r.PasoFallido -eq 'Sobrantes' -and $r.Errores -eq 1 -and $r.MantenimientoPuesto -and $r.Borrados -eq 0 -and -not ($script:hechas | Where-Object { $_ -like 'Sobrantes|bin/*' -or $_ -like 'QuitarMantenimiento|*' })) ($script:hechas -join ';')
$r = Simular @('Views/A.aspx') @('Sobrantes|bin/System.Web.Mvc.dll') -sobr @('bin/System.Web.Mvc.dll', 'Content/viejo.css')
Comprobar '(E1-10) Ejecutar: solo contenido + sobrante de bin que falla -> el mantenimiento (forzado) queda puesto' ($r.PasoFallido -eq 'Sobrantes' -and $r.MantenimientoPuesto -and $r.Borrados -eq 1 -and ($script:hechas -contains 'Mantenimiento|app_offline.htm')) ($script:hechas -join ';')
$r = Simular @('Views/A.aspx') @('Sobrantes') -sobr @('Content/viejo.css')
Comprobar '(E1-10) Ejecutar: falla un sobrante estático sin mantenimiento -> error, nada puesto' ($r.PasoFallido -eq 'Sobrantes' -and $r.Errores -eq 1 -and -not $r.MantenimientoPuesto) ($r | Out-String)
$r = Simular $todo @('Bin') -sobr $sobrS
Comprobar '(E1-10) Ejecutar: falla bin -> no se borra ningún sobrante' ($r.PasoFallido -eq 'Bin' -and $r.Borrados -eq 0 -and -not ($script:hechas | Where-Object { $_ -like 'Sobrantes|*' })) ($script:hechas -join ';')
$sobrN = @('bin/QuestPdfSkia.dll', 'bin/System.Web.Mvc.dll', 'Views/WebForm2.aspx', 'bin/zlib1.dll')
$r = Simular @('bin/WebSGV.dll') @('Sobrantes|bin/QuestPdfSkia.dll', 'Sobrantes|bin/zlib1.dll') -sobr $sobrN
Comprobar '(E1-10) Ejecutar: nativas bloqueadas -> aviso, sin error, se quita el mantenimiento' ($r.Errores -eq 0 -and -not $r.PasoFallido -and -not $r.MantenimientoPuesto -and $r.Borrados -eq 2 -and (Iguales $r.NoBorradas @('bin/QuestPdfSkia.dll', 'bin/zlib1.dll')) -and ($script:hechas -contains 'QuitarMantenimiento|app_offline.htm')) (($r | Out-String) + ($script:hechas -join ';'))
Comprobar '(E1-10) Ejecutar: las nativas se intentan después de todo lo demás' (($script:hechas -join ';') -match 'Sobrantes\|Views/WebForm2\.aspx;Sobrantes\|bin/System\.Web\.Mvc\.dll;Sobrantes\|bin/QuestPdfSkia\.dll;Sobrantes\|bin/zlib1\.dll;QuitarMantenimiento') ($script:hechas -join ';')
$r = Simular @('bin/WebSGV.dll') @('Sobrantes|bin/System.Web.Mvc.dll') -sobr $sobrN
Comprobar '(E1-10) Ejecutar: falla una DLL administrada -> error, no intenta las nativas, mantenimiento puesto' ($r.PasoFallido -eq 'Sobrantes' -and $r.Errores -eq 1 -and $r.MantenimientoPuesto -and $r.NoBorradas.Count -eq 0 -and -not ($script:hechas | Where-Object { $_ -like 'Sobrantes|bin/QuestPdfSkia.dll' -or $_ -like 'QuitarMantenimiento|*' })) ($script:hechas -join ';')
$r = Simular $todo @() -sobr $sobrS
Comprobar '(E1-10) Ejecutar: todo bien -> NoBorradas vacío' (@($r.NoBorradas).Count -eq 0) ''

# Código de git merge-base --is-ancestor
Comprobar 'Ancestro: 0 -> $true' ((ConvertFrom-CodigoAncestro 0) -eq $true) ''
Comprobar 'Ancestro: 1 -> $false' ((ConvertFrom-CodigoAncestro 1) -eq $false -and $null -ne (ConvertFrom-CodigoAncestro 1)) ''
Comprobar 'Ancestro: 128 (commit desconocido) -> $null' ($null -eq (ConvertFrom-CodigoAncestro 128)) ''
Comprobar 'Ancestro: -1 -> $null' ($null -eq (ConvertFrom-CodigoAncestro -1)) ''

# -------------------------------------------------------------------------------------------------
# 4) Comprobaciones del paquete
# -------------------------------------------------------------------------------------------------
$wcRelease = '<configuration><system.web><compilation targetFramework="4.8" /></system.web><system.webServer><rewrite><rules><rule name="ForzarHTTPS" /></rules></rewrite></system.webServer></configuration>'
$wcDebug   = '<configuration><system.web><compilation debug="true" targetFramework="4.8" /></system.web><system.webServer><rewrite><rules><rule name="ForzarHTTPS" /></rules></rewrite></system.webServer></configuration>'
$wcSinHttps = '<configuration><system.web><compilation targetFramework="4.8" /></system.web></configuration>'
$bueno = @('Web.config', 'Global.asax', 'Views/Inicio.aspx', 'bin/WebSGV.dll', 'bin/runtimes/win-x86/native/QuestPdfSkia.dll',
           'bin/runtimes/win-x64/native/QuestPdfSkia.dll', 'bin/roslyn/csc.exe.config')
foreach ($r in 'bin/runtimes/linux-x64/native/libQuestPdfSkia.so', 'bin\runtimes\linux-arm64\native\a.so', 'bin/runtimes/linux-musl-x64/native/a.so',
               'bin/runtimes/osx-x64/native/a.dylib', 'BIN/RUNTIMES/OSX-ARM64/native/a.dylib') {
    Comprobar "Runtime no Windows (se quita del paquete): '$r'" (Test-RutaRuntimeNoWindows $r) ''
}
foreach ($r in 'bin/runtimes/win-x86/native/QuestPdfSkia.dll', 'bin/runtimes/win-x64/native/QuestPdfSkia.dll', 'bin/WebSGV.dll',
               'Content/linux/a.css', 'runtimes/linux-x64/a.so', 'bin/runtimesX/linux-x64/a.so') {
    Comprobar "Se queda en el paquete: '$r'" (-not (Test-RutaRuntimeNoWindows $r)) ''
}
# -VolverA con un paquete anterior a E1-9: se quitan runtimes de linux/osx, *.md y *.example; lo demás se queda.
foreach ($r in 'bin/runtimes/osx-x64/native/a.dylib', 'docs/LEEME.MD', 'connectionStrings.config.example') {
    Comprobar "No se publica: '$r'" (Test-RutaQueNoSePublica $r) ''
}
foreach ($r in 'bin/runtimes/win-x64/native/QuestPdfSkia.dll', 'Views/Inicio.aspx', 'Web.config', 'Content/a.mdx') {
    Comprobar "Se publica: '$r'" (-not (Test-RutaQueNoSePublica $r)) ''
}
$viejo = Join-Path $env:TEMP ('sgv-probar-volver-' + [guid]::NewGuid().ToString('N'))
try {
    foreach ($r in 'Web.config', 'bin\WebSGV.dll', 'bin\runtimes\win-x86\native\QuestPdfSkia.dll', 'bin\runtimes\linux-x64\native\libQuestPdfSkia.so',
                   'bin\runtimes\osx-arm64\native\libQuestPdfSkia.dylib', 'dashboard\PLAN_MODERNIZACION.md', 'connectionStrings.config.example') {
        $f = Join-Path $viejo $r
        New-Item -ItemType Directory -Force (Split-Path $f) | Out-Null
        [IO.File]::WriteAllText($f, 'x')
    }
    $quitados = @(Remove-ArchivosQueNoSePublican $viejo)
    $quedan = @(Get-ChildItem -LiteralPath $viejo -Recurse -File | ForEach-Object { $_.FullName.Substring($viejo.Length + 1).Replace('\', '/') })
    Comprobar 'VolverA paquete anterior a E1-9: quita 4 archivos' ($quitados.Count -eq 4) ($quitados -join ', ')
    Comprobar 'VolverA paquete anterior a E1-9: quedan Web.config, la DLL y win-x86' (Iguales $quedan @('Web.config', 'bin/WebSGV.dll', 'bin/runtimes/win-x86/native/QuestPdfSkia.dll')) ($quedan -join ', ')
    Comprobar 'VolverA paquete anterior a E1-9: sin carpetas linux/osx' (@(Get-ChildItem -LiteralPath (Join-Path $viejo 'bin\runtimes') -Directory | Where-Object { $_.Name -match '^(linux|osx)' }).Count -eq 0) ''
    Comprobar 'VolverA paquete ya limpio: no quita nada' (@(Remove-ArchivosQueNoSePublican $viejo).Count -eq 0) ''
} finally { Remove-Item -LiteralPath $viejo -Recurse -Force -ErrorAction SilentlyContinue }
function Fallidas($rutas, $wc) { @(Test-ContenidoPaquete -Rutas $rutas -WebConfig $wc | Where-Object { -not $_.Ok } | ForEach-Object { $_.Comprobacion }) }
$f0 = Fallidas $bueno $wcRelease
Comprobar 'Paquete correcto: pasan todas' ($f0.Count -eq 0) ($f0 -join ', ')
Comprobar 'Paquete: son 11 comprobaciones' (@(Test-ContenidoPaquete -Rutas $bueno -WebConfig $wcRelease).Count -eq 11) ''
$casos = @(
    @{ n = 'con App_Data';                 r = $bueno + 'App_Data/OrdenesViaje/a.pdf';   wc = $wcRelease; esp = 'Sin App_Data' }
    @{ n = 'con app_data en minúsculas';   r = $bueno + 'app_data/x';                    wc = $wcRelease; esp = 'Sin App_Data' }
    @{ n = 'con Uploads';                  r = $bueno + 'Uploads/CPIC/a.pdf';            wc = $wcRelease; esp = 'Sin Uploads' }
    @{ n = 'con Database';                 r = $bueno + 'Database/Schema/01.sql';        wc = $wcRelease; esp = 'Sin Database' }
    @{ n = 'con connectionStrings.config'; r = $bueno + 'connectionStrings.config';      wc = $wcRelease; esp = 'Sin archivos de secretos' }
    @{ n = 'con CONNECTIONSTRINGS.Prod.CONFIG en subcarpeta'; r = $bueno + 'Views/CONNECTIONSTRINGS.Prod.CONFIG'; wc = $wcRelease; esp = 'Sin archivos de secretos' }
    @{ n = 'con appSettings.Secrets.config'; r = $bueno + 'appSettings.Secrets.config';  wc = $wcRelease; esp = 'Sin archivos de secretos' }
    @{ n = 'con *.pdb';                    r = $bueno + 'bin/WebSGV.pdb';                wc = $wcRelease; esp = 'Sin *.pdb' }
    @{ n = 'con runtimes de linux';        r = $bueno + 'bin/runtimes/linux-x64/native/libQuestPdfSkia.so'; wc = $wcRelease; esp = 'Sin runtimes de linux/osx' }
    @{ n = 'con runtimes de linux-musl';   r = $bueno + 'bin/runtimes/linux-musl-x64/native/libQuestPdfSkia.so'; wc = $wcRelease; esp = 'Sin runtimes de linux/osx' }
    @{ n = 'con runtimes de osx';          r = $bueno + 'BIN/Runtimes/OSX-arm64/native/libQuestPdfSkia.dylib'; wc = $wcRelease; esp = 'Sin runtimes de linux/osx' }
    @{ n = 'con un .md';                   r = $bueno + 'dashboard/PLAN_MODERNIZACION.md'; wc = $wcRelease; esp = 'Sin documentos ni plantillas (*.md, *.example)' }
    @{ n = 'con un .example';              r = $bueno + 'connectionStrings.config.example'; wc = $wcRelease; esp = 'Sin documentos ni plantillas (*.md, *.example)' }
    @{ n = 'sin bin/WebSGV.dll';           r = @($bueno | Where-Object { $_ -ne 'bin/WebSGV.dll' }); wc = $wcRelease; esp = 'Con bin/WebSGV.dll' }
    @{ n = 'sin QuestPDF win-x86';         r = @($bueno | Where-Object { $_ -notlike '*win-x86*' }); wc = $wcRelease; esp = 'Con QuestPDF nativo (win-x86)' }
    @{ n = 'sin QuestPDF win-x64';         r = @($bueno | Where-Object { $_ -notlike '*win-x64*' }); wc = $wcRelease; esp = 'Con QuestPDF nativo (win-x64)' }
    @{ n = 'Web.config con debug="true"';  r = $bueno;                                   wc = $wcDebug;   esp = 'Web.config de Release' }
    @{ n = 'Web.config sin transformar';   r = $bueno;                                   wc = $wcSinHttps; esp = 'Web.config de Release' }
    @{ n = 'Web.config que no es XML';     r = $bueno;                                   wc = '<configuration>';  esp = 'Web.config de Release' }
    @{ n = 'sin Web.config';               r = @($bueno | Where-Object { $_ -ne 'Web.config' }); wc = $null; esp = 'Web.config de Release' }
)
foreach ($c in $casos) {
    $f = @(Fallidas $c.r $c.wc)
    Comprobar "Paquete $($c.n): falla solo '$($c.esp)'" ($f.Count -eq 1 -and $f[0] -eq $c.esp) ($f -join ', ')
}

# -------------------------------------------------------------------------------------------------
# 5) Manifiesto
# -------------------------------------------------------------------------------------------------
$h1 = 'a' * 64; $h2 = 'B' * 64
$man = ConvertFrom-Manifiesto @(
    'SGV - manifiesto de publicación', 'Commit: 3b2f8b0aaaabbbbccccddddeeeeffff00001111', 'Rama: master', 'Cambios sin confirmar: no',
    'Fecha: 2026-11-19 15:04:05', "Archivos: 2 (1 KB)", '--- Archivos (SHA-256  bytes  ruta) ---',
    "$h1  10  Views/Inicio.aspx", "$h2  2048  bin/runtimes/win-x86/native/QuestPdfSkia.dll", 'línea rara que se ignora')
Comprobar 'Manifiesto: commit y rama' ($man.Commit -eq '3b2f8b0aaaabbbbccccddddeeeeffff00001111' -and $man.Rama -eq 'master') ($man | Out-String)
Comprobar 'Manifiesto: cambios sin confirmar y fecha' ($man.CambiosSinConfirmar -eq 'no' -and $man.Fecha -eq '2026-11-19 15:04:05') ''
Comprobar 'Manifiesto: 2 archivos con SHA en minúsculas' ($man.Archivos.Count -eq 2 -and $man.Archivos['bin/runtimes/win-x86/native/QuestPdfSkia.dll'].Sha -eq $h2.ToLowerInvariant() -and $man.Archivos['Views/Inicio.aspx'].Tamano -eq 10) ''
$okC = @(Compare-PaqueteConManifiesto -Archivos @((New-Archivo 'views/inicio.aspx' 10 $h1.ToUpperInvariant()), (New-Archivo 'bin\runtimes\win-x86\native\QuestPdfSkia.dll' 2048 $h2)) -Manifiesto $man.Archivos)
Comprobar 'Manifiesto: carpeta íntegra (mayúsculas y \ distintos)' ($okC.Count -eq 0) ($okC -join '; ')
$malC = @(Compare-PaqueteConManifiesto -Archivos @((New-Archivo 'Views/Inicio.aspx' 10 ('c' * 64)), (New-Archivo 'Views/Extra.aspx' 1 $h1)) -Manifiesto $man.Archivos)
Comprobar 'Manifiesto: detecta SHA distinto, archivo extra y archivo faltante' ($malC.Count -eq 3) ($malC -join '; ')

# -------------------------------------------------------------------------------------------------
# 6) -VolverA y confirmación BORRAR
# -------------------------------------------------------------------------------------------------
$d = Get-DecisionVolverA -ContieneCommitContrasenas $true
Comprobar 'VolverA posterior a 773dad0: permitido sin aviso' ($d.Permitido -and -not $d.Aviso) ($d | Out-String)
$d = Get-DecisionVolverA -ContieneCommitContrasenas $false
Comprobar 'VolverA anterior a 773dad0: bloqueado con aviso' (-not $d.Permitido -and $d.Aviso -match '773dad0' -and $d.Aviso -match 'ANTERIOR') ($d | Out-String)
$d = Get-DecisionVolverA -ContieneCommitContrasenas $false -Forzar
Comprobar 'VolverA anterior con -Forzar: permitido, con aviso' ($d.Permitido -and $d.Aviso) ''
$d = Get-DecisionVolverA -ContieneCommitContrasenas $null
Comprobar 'VolverA sin poder comprobar el commit: bloqueado' (-not $d.Permitido -and $d.Aviso) ''
$d = Get-DecisionVolverA -ContieneCommitContrasenas $null -Forzar
Comprobar 'VolverA commit desconocido con -Forzar: permitido, con aviso' ($d.Permitido -and $d.Aviso -match 'No se pudo comprobar') ($d | Out-String)
$d = Get-DecisionVolverA -ContieneCommitContrasenas $null -HayManifiesto $false -Forzar
Comprobar 'VolverA sin manifiesto: -Forzar NO basta, falta -SinManifiesto' (-not $d.Permitido -and ($d.Faltan -join ',') -eq '-SinManifiesto' -and $d.Aviso -match 'manifiesto') ($d | Out-String)
$d = Get-DecisionVolverA -ContieneCommitContrasenas $null -HayManifiesto $false -SinManifiesto
Comprobar 'VolverA sin manifiesto: -SinManifiesto NO basta, falta -Forzar (commit desconocido)' (-not $d.Permitido -and ($d.Faltan -join ',') -eq '-Forzar') ($d | Out-String)
$d = Get-DecisionVolverA -ContieneCommitContrasenas $null -HayManifiesto $false
Comprobar 'VolverA sin manifiesto y sin opciones: faltan las dos' (-not $d.Permitido -and ($d.Faltan -join ',') -eq '-SinManifiesto,-Forzar') ($d | Out-String)
$d = Get-DecisionVolverA -ContieneCommitContrasenas $null -HayManifiesto $false -SinManifiesto -Forzar
Comprobar 'VolverA sin manifiesto con -SinManifiesto y -Forzar: permitido, con los dos avisos' ($d.Permitido -and $d.Aviso -match 'manifiesto' -and $d.Aviso -match '773dad0') ($d | Out-String)
$d = Get-DecisionVolverA -ContieneCommitContrasenas $true -SinManifiesto
Comprobar 'VolverA con manifiesto y commit bueno: -SinManifiesto no cambia nada' ($d.Permitido -and -not $d.Aviso) ''
foreach ($t in 'BORRAR', ' BORRAR ') { Comprobar "Confirmación '$t' aceptada" (Test-ConfirmacionBorrar $t) '' }
foreach ($t in 'borrar', 'Borrar', 'BORRAR!', 'SI', '', $null, 'BORRAR TODO') { Comprobar "Confirmación '$t' rechazada" (-not (Test-ConfirmacionBorrar $t)) '' }

# -------------------------------------------------------------------------------------------------
# 7) Listado recursivo (módulo común) con un FTP simulado: nunca entra en App_Data ni Uploads
# -------------------------------------------------------------------------------------------------
$con = New-ConexionFtp -Servidor 'ftp://ejemplo' -RutaSitio '/www.sitio' -Credencial (New-Object System.Net.NetworkCredential('u', 'p'))
$ftp = @{
    'ftp://ejemplo/www.sitio/|LIST' = @(
        'drwxr-xr-x   1 owner    group           0 Nov 19 10:00 Views',
        'drwxr-xr-x   1 owner    group           0 Nov 19 10:00 App_Data',
        'drwxr-xr-x   1 owner    group           0 Nov 19 10:00 Uploads',
        'drwxr-xr-x   1 owner    group           0 Nov 19 10:00 Raro',
        'drwxr-xr-x   1 owner    group           0 Nov 19 10:00 Roto',
        '-rw-r--r--   1 owner    group         512 Nov 19 10:00 Web.config',
        '-rw-r--r--   1 owner    group          99 Nov 19 10:00 connectionStrings.config') -join "`r`n"
    'ftp://ejemplo/www.sitio/Views/|LIST' = @(
        "11-19-26  10:00AM               1234 Inicio.aspx",
        "11-19-26  10:00AM               4321 Dashboard.aspx") -join "`r`n"
    'ftp://ejemplo/www.sitio/Raro/|LIST' = 'formato desconocido'
    'ftp://ejemplo/www.sitio/Raro/|NLST' = "a.js`r`nsub`r`n"
    'ftp://ejemplo/www.sitio/Raro/a.js|SIZE' = 77
    'ftp://ejemplo/www.sitio/Raro/sub/|LIST' = ''
}
$script:pedidas = New-Object System.Collections.Generic.List[string]
$lector = {
    param($url, $metodo)
    $script:pedidas.Add("$url|$metodo")
    if ($url -match '/(App_Data|Uploads)(/|$)') { throw "se pidió una carpeta protegida: $url" }
    if ($url -match '/Roto/') { throw 'conexión rechazada' }
    $clave = "$url|$metodo"
    if ($ftp.ContainsKey($clave)) { return $ftp[$clave] }
    throw "550 no existe: $clave"
}
$l = Get-ListadoFtpRecursivo -Conexion $con -Carpeta '' -Omitir { param($rel) Test-RutaProtegida $rel } -Lector $lector -EsperaReintento 0
$rutasL = @($l.Archivos | ForEach-Object { $_.Ruta })
Comprobar 'Listado: archivos esperados (sin protegidos)' (Iguales $rutasL @('Web.config', 'Views/Inicio.aspx', 'Views/Dashboard.aspx', 'Raro/a.js')) ($rutasL -join ', ')
Comprobar 'Listado: nunca pidió App_Data ni Uploads' (@($script:pedidas | Where-Object { $_ -match 'App_Data|Uploads' }).Count -eq 0) ($script:pedidas -join ' ; ')
Comprobar 'Listado: tamaños (Unix, IIS y SIZE)' ((($l.Archivos | Where-Object Ruta -eq 'Views/Dashboard.aspx').Tamano -eq 4321) -and (($l.Archivos | Where-Object Ruta -eq 'Raro/a.js').Tamano -eq 77) -and (($l.Archivos | Where-Object Ruta -eq 'Web.config').Tamano -eq 512)) ($l.Archivos | Out-String)
Comprobar 'Listado: la carpeta que falla queda como error (con reintento)' ($l.Errores.Count -eq 1 -and $l.Errores[0] -match '/Roto' -and @($script:pedidas | Where-Object { $_ -match '/Roto/\|LIST' }).Count -eq 2) ($l.Errores -join ' ; ')
$l2 = Get-ListadoFtpRecursivo -Conexion $con -Carpeta 'Views' -Lector $lector -EsperaReintento 0
Comprobar 'Listado de una subcarpeta: rutas relativas a ella' (Iguales @($l2.Archivos | ForEach-Object { $_.Ruta }) @('Inicio.aspx', 'Dashboard.aspx')) ''

""
if ($fallos -gt 0) { Write-Host "$fallos prueba(s) fallaron." -ForegroundColor Red; exit 1 }
Write-Host "Todas las pruebas de publicar pasaron." -ForegroundColor Green
exit 0
