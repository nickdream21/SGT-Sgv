<#
    Funciones FTP comunes a las herramientas de SGV (respaldo y publicación).
    El FTP de somee no admite TLS: la contraseña viaja sin cifrar, igual que al publicar desde Visual Studio.

    Solo lectura aquí: LIST, NLST, SIZE (y la petición genérica que el llamador usa para RETR/STOR/DELE).
    Las funciones que escriben en el servidor viven en el script que las necesita, a la vista.

    Uso:
        Import-Module (Join-Path $PSScriptRoot '..\comun\Ftp.psm1') -Force -DisableNameChecking
        $cred = Read-CredencialFtp -Archivo <privado\ftp-respaldo.xml> -ComandoGuardar '<cómo guardarla>'
        $con  = New-ConexionFtp -Servidor 'ftp://sgvtransporte.somee.com' -RutaSitio '/www.sgvtransporte.somee.com' -Credencial $cred.GetNetworkCredential()
        $l    = Get-ListadoFtpRecursivo -Conexion $con -Carpeta 'Uploads'
#>

# Interpreta las líneas de un LIST de FTP (formato Unix o MS-DOS/IIS). Devuelve un objeto por línea
# con Nombre, EsCarpeta, Tamano y Valido ($false si no se entendió la línea). Omite '.', '..' y 'total N'.
function ConvertFrom-ListadoFtp {
    param([string[]]$Lineas)
    $res = @()
    foreach ($l in $Lineas) {
        if ([string]::IsNullOrWhiteSpace($l)) { continue }
        $l = $l.TrimEnd("`r")
        if ($l -match '^total\s+\d+$') { continue }
        $nombre = $null; $esCarpeta = $false; $tam = -1L
        if ($l -match '^([d\-])[rwxsStT\-]{9}\S*\s+\d+\s+\S+\s+(?:\S+\s+)?(\d+)\s+[A-Za-z]{3}\s+\d{1,2}\s+(?:\d{1,2}:\d{2}|\d{4})\s(.+)$') {
            $esCarpeta = ($Matches[1] -eq 'd'); $tam = [long]$Matches[2]; $nombre = $Matches[3].Trim()
        } elseif ($l -match '^\d{2}-\d{2}-\d{2,4}\s+\d{1,2}:\d{2}(?:AM|PM)?\s+(<DIR>|\d+)\s+(.+)$') {
            $esCarpeta = ($Matches[1] -eq '<DIR>'); if (-not $esCarpeta) { $tam = [long]$Matches[1] }; $nombre = $Matches[2].Trim()
        }
        if ($nombre -eq '.' -or $nombre -eq '..') { continue }
        $res += New-Object PSObject -Property ([ordered]@{
            Nombre = $nombre; EsCarpeta = $esCarpeta; Tamano = $tam; Valido = ($null -ne $nombre); Linea = $l })
    }
    return $res
}

# Lee la credencial guardada con Export-Clixml (DPAPI: solo la abre el mismo usuario de Windows en la
# misma máquina). Nunca imprime la contraseña.
function Read-CredencialFtp {
    param([string]$Archivo, [string]$ComandoGuardar)
    if (-not (Test-Path -LiteralPath $Archivo)) {
        throw "No existe la credencial FTP ($Archivo). Guárdela una vez con: $ComandoGuardar"
    }
    try {
        $cred = Import-Clixml -Path $Archivo
        if ($cred -isnot [System.Management.Automation.PSCredential]) { throw "el archivo no contiene una credencial" }
        return $cred
    } catch {
        throw "No se pudo leer $Archivo (se guardó con otro usuario de Windows o en otra máquina, o está dañado). Repita: $ComandoGuardar"
    }
}

function New-ConexionFtp {
    param([string]$Servidor, [string]$RutaSitio, [System.Net.NetworkCredential]$Credencial)
    return [pscustomobject]@{
        Base       = $Servidor.TrimEnd('/') + '/' + $RutaSitio.Trim('/')
        Credencial = $Credencial
    }
}

# URL de una ruta relativa al sitio ('Uploads/a b.pdf'); cada segmento va escapado. '' = raíz del sitio.
function Get-UrlFtp {
    param($Conexion, [string]$Ruta)
    $partes = @($Ruta.Replace('\', '/').Trim('/') -split '/' | Where-Object { $_ } | ForEach-Object { [uri]::EscapeDataString($_) })
    if ($partes.Count -eq 0) { return $Conexion.Base }
    return $Conexion.Base + '/' + ($partes -join '/')
}

function New-PeticionFtp {
    param($Conexion, [string]$Url, [string]$Metodo)
    $r = [System.Net.FtpWebRequest]::Create($Url)
    $r.Method = $Metodo
    $r.Credentials = $Conexion.Credencial
    $r.UsePassive = $true; $r.UseBinary = $true; $r.KeepAlive = $false
    $r.Timeout = 60000; $r.ReadWriteTimeout = 300000
    return $r
}

function Read-TextoFtp {
    param($Conexion, [string]$Url, [string]$Metodo)
    $resp = (New-PeticionFtp $Conexion $Url $Metodo).GetResponse()
    try {
        $sr = New-Object IO.StreamReader($resp.GetResponseStream(), [Text.Encoding]::UTF8)
        try { return $sr.ReadToEnd() } finally { $sr.Dispose() }
    } finally { $resp.Close() }
}

function Get-TamanoFtp {
    param($Conexion, [string]$Url)
    $resp = (New-PeticionFtp $Conexion $Url 'SIZE').GetResponse()
    try { return [long]$resp.ContentLength } finally { $resp.Close() }
}

# Fecha de modificación (MDTM) de un archivo del servidor. Lanza excepción si no existe o el servidor no
# admite MDTM (el llamador decide qué hacer).
function Get-FechaModificacionFtp {
    param($Conexion, [string]$Url)
    $resp = (New-PeticionFtp $Conexion $Url ([System.Net.WebRequestMethods+Ftp]::GetDateTimestamp)).GetResponse()
    try { return [datetime]$resp.LastModified } finally { $resp.Close() }
}

# Ejecuta el bloque; ante error espera 3 s y lo repite una vez.
function Invoke-ConReintento {
    param([scriptblock]$Bloque, [int]$EsperaSegundos = 3)
    try { return & $Bloque }
    catch { Start-Sleep -Seconds $EsperaSegundos; return & $Bloque }
}

# Lista recursivamente una carpeta del sitio ('' = raíz). Devuelve { Archivos; Errores }:
#   Archivos: objetos { Ruta (relativa a -Carpeta, con '/'); Tamano (-1 si no se sabe) }
#   Errores : textos 'ERROR al listar /<carpeta> : <mensaje>' (la carpeta con error se salta).
# -Omitir  : scriptblock (ruta relativa a -Carpeta) -> $true para no listar/devolver esa entrada.
# -Lector  : scriptblock (url, método) -> texto de LIST/NLST o tamaño de SIZE. Por defecto el FTP real;
#            las pruebas pasan uno simulado (sin red).
# Si alguna línea de LIST no se entiende, usa NLST + SIZE (si SIZE falla, la entrada se trata como carpeta).
function Get-ListadoFtpRecursivo {
    param($Conexion, [string]$Carpeta = '', [scriptblock]$Omitir, [scriptblock]$Lector, [int]$EsperaReintento = 3)
    $archivos = New-Object System.Collections.Generic.List[object]
    $errores  = New-Object System.Collections.Generic.List[string]
    $leer = {
        param($url, $metodo)
        if ($Lector) { return & $Lector $url $metodo }
        if ($metodo -eq 'SIZE') { return Get-TamanoFtp $Conexion $url }
        return Read-TextoFtp $Conexion $url $metodo
    }
    $pendientes = New-Object System.Collections.Generic.Queue[string]
    $pendientes.Enqueue('')
    $carpetaBase = $Carpeta.Replace('\', '/').Trim('/')
    while ($pendientes.Count -gt 0) {
        $rel = $pendientes.Dequeue()
        $carpetaSitio = (@($carpetaBase, $rel) | Where-Object { $_ }) -join '/'
        $url = (Get-UrlFtp $Conexion $carpetaSitio) + '/'
        try {
            $texto = Invoke-ConReintento { & $leer $url 'LIST' } $EsperaReintento
        } catch {
            $errores.Add("ERROR al listar /$carpetaSitio : $($_.Exception.Message)")
            continue
        }
        $entradas = @(ConvertFrom-ListadoFtp ([string]$texto -split "`n"))
        if (@($entradas | Where-Object { -not $_.Valido }).Count -gt 0) {
            $entradas = @()
            try {
                $nombres = ([string](Invoke-ConReintento { & $leer $url 'NLST' } $EsperaReintento)) -split "`r?`n" | Where-Object { $_ }
            } catch {
                $errores.Add("ERROR al listar /$carpetaSitio : $($_.Exception.Message)")
                continue
            }
            foreach ($n in $nombres) {
                $n = ($n.Trim() -split '/')[-1]
                if (-not $n -or $n -eq '.' -or $n -eq '..') { continue }
                $tam = -1L
                try { $tam = [long](& $leer (Get-UrlFtp $Conexion "$carpetaSitio/$n") 'SIZE') } catch { $tam = -1L }
                $entradas += New-Object PSObject -Property @{ Nombre = $n; EsCarpeta = ($tam -lt 0); Tamano = $tam; Valido = $true }
            }
        }
        foreach ($e in $entradas) {
            $relE = if ($rel) { "$rel/$($e.Nombre)" } else { $e.Nombre }
            if ($Omitir -and (& $Omitir $relE)) { continue }
            if ($e.EsCarpeta) { $pendientes.Enqueue($relE) }
            else { $archivos.Add((New-Object PSObject -Property @{ Ruta = $relE; Tamano = [long]$e.Tamano })) }
        }
    }
    return [pscustomobject]@{ Archivos = $archivos.ToArray(); Errores = $errores.ToArray() }
}

Export-ModuleMember -Function ConvertFrom-ListadoFtp, Read-CredencialFtp, New-ConexionFtp, Get-UrlFtp,
    New-PeticionFtp, Read-TextoFtp, Get-TamanoFtp, Get-FechaModificacionFtp, Invoke-ConReintento, Get-ListadoFtpRecursivo
