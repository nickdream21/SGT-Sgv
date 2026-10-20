param(
    # Mes de carga del cuadro (AAAA-MM).
    [Parameter(Mandatory = $true)][string]$Mes,
    # Carpetas con los Word de programación ("DESPACHO dd-mm-aa.docx"). Incluir la del mes anterior:
    # la programación del último día ya trae cargas del mes.
    [Parameter(Mandatory = $true)][string[]]$Carpetas,
    # Excel de salida.
    [string]$Salida,
    # prueba: crea los viajes en sgvActualizada y lo deshace al final; aplicar: los deja.
    [ValidateSet('', 'prueba', 'aplicar')][string]$Bd = ''
)
$ErrorActionPreference = 'Stop'
$aqui = $PSScriptRoot
$raiz = (Resolve-Path "$aqui\..\..").Path
$bin = Join-Path $aqui 'bin'
if (-not $Salida) { $Salida = Join-Path $aqui "salida\STATUS GENERAL $Mes.xlsx" }
New-Item -ItemType Directory -Force $bin, (Split-Path $Salida) | Out-Null

# 1) Compilar contra el WebSGV.dll ya compilado (msbuild WebSGV.sln antes).
$webBin = Join-Path $raiz 'WebSGV\bin'
if (-not (Test-Path "$webBin\WebSGV.dll")) { throw "Falta WebSGV\bin\WebSGV.dll: compila la solución primero." }
Copy-Item "$webBin\*.dll" $bin -Force
$csc = Get-ChildItem "${env:ProgramFiles}\Microsoft Visual Studio" -Recurse -Filter csc.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match 'Roslyn' } | Select-Object -First 1
if (-not $csc) { throw 'No se encontró csc.exe (Roslyn) de Visual Studio.' }
$nativas = 'zlib1', 'QuestPdfSkia', 'qpdf', 'libwinpthread-1', 'libstdc++-6', 'libgcc_s_seh-1'
$refs = Get-ChildItem "$bin\*.dll" | Where-Object { $nativas -notcontains $_.BaseName } | ForEach-Object { "/r:$($_.FullName)" }
$refs += '/r:System.Configuration.dll', '/r:System.Data.dll', '/r:System.Transactions.dll', '/r:System.Web.dll',
         '/r:System.Xml.Linq.dll', "/r:$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\netstandard.dll"
& $csc.FullName /nologo /langversion:latest /out:"$bin\Cuadro.exe" @refs "$aqui\Cuadro.cs"
if ($LASTEXITCODE -ne 0) { throw 'No compiló Cuadro.cs.' }

# 2) Config: los binding redirects de Web.config y las credenciales de Onway leídas en su sitio
#    (appSettings.Secrets.config no se copia).
[xml]$web = Get-Content (Join-Path $raiz 'WebSGV\Web.config') -Raw -Encoding UTF8
$secretos = Join-Path $raiz 'WebSGV\appSettings.Secrets.config'
$config = "<?xml version=`"1.0`" encoding=`"utf-8`"?><configuration><startup><supportedRuntime version=`"v4.0`" sku=`".NETFramework,Version=v4.8`"/></startup>" +
          "<appSettings file=`"$secretos`"/>$($web.configuration.runtime.OuterXml)</configuration>"
Set-Content "$bin\Cuadro.exe.config" $config -Encoding UTF8

# 3) Correr. El historial del GPS queda en cache\ (por placa y día) para no volver a pedirlo.
$argumentos = @($raiz, (Join-Path $aqui 'cache'), $Salida, "--mes=$Mes")
if ($Bd) { $argumentos += "--bd=$Bd" }
$argumentos += $Carpetas | ForEach-Object { (Resolve-Path $_).Path }
& "$bin\Cuadro.exe" @argumentos
