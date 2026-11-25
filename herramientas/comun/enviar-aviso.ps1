<#
.SYNOPSIS
    Envía un aviso por correo (FALLA del sello, del respaldo diario) con el SMTP del sitio. Es el script
    que se pone en la variable de entorno SGV_SCRIPT_AVISO (E6-14, M-123).

.DESCRIPTION
    Lee SmtpHost, SmtpPort, SmtpSSL, SmtpUser, SmtpPass y SmtpFrom de WebSGV\appSettings.Secrets.config
    (las mismas claves y reglas que Helpers/CorreoHelper.cs: puerto vacío o inválido = 587; TLS salvo
    "false"; sin SmtpFrom se usa SmtpUser). La contraseña SOLO se lee de ese archivo: este script no la
    recibe por parámetro, no la escribe en la salida y no la pasa a ningún otro programa.

    Destinatario: -Para; si no, la variable de entorno SGV_AVISO_PARA; si no, AvisoErroresPara de
    WebSGV\Web.config (el mismo buzón que recibe los errores del sitio). Varios separados por comas.

    Modo de prueba: con -CarpetaSalida (o la variable de entorno SGV_AVISO_CARPETA_SALIDA) no se envía
    nada: el correo se escribe como un archivo .eml en esa carpeta (SmtpClient con
    SpecifiedPickupDirectory). Sirve para comprobar una FALLA forzada sin mandar correos.

    Lo llaman sellar-registros.ps1 (ante una FALLA del sello) y herramientas\respaldo\avisar-tarea.ps1
    (al final de la tarea "SGV - Respaldo diario", si un paso falló) con -Asunto y -Cuerpo.

    Si no puede enviar (configuración incompleta, sin destinatario, error del servidor SMTP) lanza un
    error: quien lo llama lo muestra y sigue (el aviso nunca cambia el resultado del respaldo).

.EXAMPLE
    [Environment]::SetEnvironmentVariable('SGV_SCRIPT_AVISO', 'C:\ruta\al\repo\herramientas\comun\enviar-aviso.ps1', 'User')
    .\herramientas\comun\enviar-aviso.ps1 -Asunto 'SGV: prueba de aviso' -Cuerpo 'Prueba' -CarpetaSalida C:\temp\avisos
    .\herramientas\comun\enviar-aviso.ps1 -Asunto 'SGV: prueba de aviso' -Cuerpo 'Prueba'     # envío real
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Asunto,

    [string]$Cuerpo = '',

    # Destinatarios separados por comas. Por defecto: SGV_AVISO_PARA o AvisoErroresPara de Web.config.
    [string]$Para = $env:SGV_AVISO_PARA,

    # Modo de prueba: escribe el .eml aquí en lugar de enviarlo.
    [string]$CarpetaSalida = $env:SGV_AVISO_CARPETA_SALIDA,

    # Ruta del archivo de secretos (no el secreto). Por defecto, el del repo.
    [string]$ArchivoSecretos,

    # Ruta de Web.config (para AvisoErroresPara). Por defecto, el del repo.
    [string]$ArchivoWebConfig
)

$ErrorActionPreference = 'Stop'

$raizRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..')).TrimEnd('\')
if ([string]::IsNullOrWhiteSpace($ArchivoSecretos))  { $ArchivoSecretos  = Join-Path $raizRepo 'WebSGV\appSettings.Secrets.config' }
if ([string]::IsNullOrWhiteSpace($ArchivoWebConfig)) { $ArchivoWebConfig = Join-Path $raizRepo 'WebSGV\Web.config' }

function Read-ClavesAppSettings([string]$ruta) {
    $claves = @{}
    if (-not (Test-Path -LiteralPath $ruta -PathType Leaf)) { return $claves }
    [xml]$xml = Get-Content -LiteralPath $ruta -Raw -Encoding UTF8
    $nodo = if ($xml.configuration) { $xml.configuration.appSettings } else { $xml.appSettings }
    if ($nodo) { foreach ($a in @($nodo.add)) { if ($a -and $a.key) { $claves[[string]$a.key] = [string]$a.value } } }
    return $claves
}

if (-not (Test-Path -LiteralPath $ArchivoSecretos -PathType Leaf)) {
    throw "No existe $ArchivoSecretos (ver docs\PREPARAR_MAQUINA.md): sin él no hay SMTP para el aviso."
}
$s = Read-ClavesAppSettings $ArchivoSecretos
$smtpHost  = ([string]$s['SmtpHost']).Trim()
$usuario   = ([string]$s['SmtpUser']).Trim()
$puerto    = 0
if (-not [int]::TryParse([string]$s['SmtpPort'], [ref]$puerto) -or $puerto -le 0) { $puerto = 587 }
$ssl       = $true
$sslLeido  = $false
if ([bool]::TryParse([string]$s['SmtpSSL'], [ref]$sslLeido)) { $ssl = $sslLeido }
$remitente = if (-not [string]::IsNullOrWhiteSpace($s['SmtpFrom'])) { ([string]$s['SmtpFrom']).Trim() } else { $usuario }

$faltan = @()
if (-not $smtpHost) { $faltan += 'SmtpHost' }
if (-not $usuario)  { $faltan += 'SmtpUser' }
if ([string]::IsNullOrEmpty([string]$s['SmtpPass'])) { $faltan += 'SmtpPass' }
if ($faltan.Count) { throw "SMTP incompleto en ${ArchivoSecretos}: falta $($faltan -join ', ')." }

if ([string]::IsNullOrWhiteSpace($Para)) { $Para = (Read-ClavesAppSettings $ArchivoWebConfig)['AvisoErroresPara'] }
if ([string]::IsNullOrWhiteSpace($Para)) {
    throw "Sin destinatario: indique -Para, la variable SGV_AVISO_PARA o AvisoErroresPara en Web.config."
}

# Destinatario en la salida sin la parte local (como CorreoHelper.OcultarDestinatario).
$paraOculto = (($Para -split ',') | ForEach-Object { $d = $_.Trim(); $i = $d.IndexOf('@'); if ($i -ge 0) { '***' + $d.Substring($i) } else { '***' } }) -join ', '

$pie = "`r`n`r`n--`r`nAviso automático de las herramientas de SGV (herramientas\comun\enviar-aviso.ps1) desde el equipo $env:COMPUTERNAME, $('{0:yyyy-MM-dd HH:mm}' -f (Get-Date))."

$mail = New-Object System.Net.Mail.MailMessage
$smtp = New-Object System.Net.Mail.SmtpClient($smtpHost, $puerto)
try {
    $mail.From = New-Object System.Net.Mail.MailAddress($remitente, 'SGV - Sistema de Gestión')
    $mail.To.Add($Para)
    $mail.Subject = $Asunto
    $mail.Body = $Cuerpo + $pie
    $mail.IsBodyHtml = $false
    $mail.BodyEncoding = [Text.Encoding]::UTF8
    $mail.SubjectEncoding = [Text.Encoding]::UTF8

    if (-not [string]::IsNullOrWhiteSpace($CarpetaSalida)) {
        $carpeta = [IO.Path]::GetFullPath($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($CarpetaSalida))
        New-Item -ItemType Directory -Force -Path $carpeta | Out-Null
        $smtp.DeliveryMethod = [System.Net.Mail.SmtpDeliveryMethod]::SpecifiedPickupDirectory
        $smtp.PickupDirectoryLocation = $carpeta
        $smtp.Send($mail)
        "Aviso escrito en $carpeta (modo de prueba, no se envió): '$Asunto' para $paraOculto."
        return
    }

    # PowerShell 5.1 (.NET 4.x) puede no ofrecer TLS 1.2 por defecto.
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $smtp.DeliveryMethod = [System.Net.Mail.SmtpDeliveryMethod]::Network
    $smtp.EnableSsl = $ssl
    $smtp.Timeout = 30000
    $smtp.UseDefaultCredentials = $false
    $smtp.Credentials = New-Object System.Net.NetworkCredential($usuario, [string]$s['SmtpPass'])
    try {
        $smtp.Send($mail)
    } catch {
        # El mensaje del SMTP no lleva la contraseña; se repite sin el cuerpo del correo.
        throw "No se pudo enviar el aviso '$Asunto' a $paraOculto por ${smtpHost}:${puerto}: $($_.Exception.GetBaseException().Message)"
    }
    "Aviso enviado: '$Asunto' para $paraOculto."
} finally {
    $mail.Dispose()
    $smtp.Dispose()
}
