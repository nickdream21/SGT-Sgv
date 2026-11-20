<#
    Copia verificada de un archivo local a su destino final (respaldo y espejo de SGV).

    Por qué: la carpeta de respaldos suele estar en Google Drive para escritorio (G:\Unidades compartidas\...).
    Su sistema de archivos virtual informa un tamaño incompleto justo después de escribir (se vio 14520 bytes
    de un archivo de 370309 que llegó entero). Por eso todo se escribe primero en una carpeta LOCAL, se
    comprueba ahí (tamaño y SHA-256) y recién se copia al destino, donde se vuelve a comprobar con
    reintentos y, si el tamaño sigue sin cuadrar, leyendo el contenido (SHA-256).

    Uso:
        Import-Module (Join-Path $PSScriptRoot '..\comun\CopiaVerificada.psm1') -Force -DisableNameChecking
        $sha = Get-Sha256Archivo $local
        $c   = Copy-ArchivoVerificado -Origen $local -Destino $final -Tamano (Get-Item $local).Length -Sha $sha
        # $c.Verificacion: 'Tamano' (cuadró el tamaño) o 'Hash' (el tamaño no cuadró a tiempo, el contenido sí)
#>

# SHA-256 (hex en minúsculas) leyendo el contenido del archivo, no su tamaño.
function Get-Sha256Archivo {
    param([string]$Ruta)
    $sha = [Security.Cryptography.SHA256]::Create()
    $fs = [IO.File]::OpenRead($Ruta)
    try { return ([BitConverter]::ToString($sha.ComputeHash($fs))).Replace('-', '').ToLowerInvariant() }
    finally { $fs.Dispose(); $sha.Dispose() }
}

# Carpeta local (fuera de Drive/OneDrive) para descargas y exportaciones a medio hacer.
function Get-CarpetaTemporalLocal {
    param([string]$Subcarpeta)
    $base = if ($env:LOCALAPPDATA) { $env:LOCALAPPDATA } else { $env:TEMP }
    $dir = Join-Path (Join-Path $base 'SGV-respaldo') $Subcarpeta
    New-Item -ItemType Directory -Force $dir | Out-Null
    return $dir
}

# Espera a que el tamaño informado de $Ruta sea $Tamano: lo relee hasta $Intentos veces cada $EsperaMs ms
# (por defecto 20 x 500 ms = 10 s). $true si cuadró. $LeerTamano se puede reemplazar en las pruebas.
function Wait-TamanoArchivo {
    param([string]$Ruta, [long]$Tamano, [int]$Intentos = 20, [int]$EsperaMs = 500,
          [scriptblock]$LeerTamano = { param($r) (Get-Item -LiteralPath $r -Force).Length })
    for ($i = 1; $i -le [Math]::Max(1, $Intentos); $i++) {
        $t = -1L
        try { $t = [long](& $LeerTamano $Ruta) } catch { $t = -1L }
        if ($t -eq $Tamano) { return $true }
        if ($i -lt $Intentos -and $EsperaMs -gt 0) { Start-Sleep -Milliseconds $EsperaMs }
    }
    return $false
}

# Copia $Origen (local, ya comprobado) a $Destino y lo verifica allí: primero el tamaño con reintentos
# (Wait-TamanoArchivo); si no cuadra, lee el destino y compara el SHA-256 con $Sha. Solo lanza si tampoco
# coincide el contenido. Devuelve { Verificacion = 'Tamano' | 'Hash' }.
function Copy-ArchivoVerificado {
    param([string]$Origen, [string]$Destino, [long]$Tamano, [string]$Sha,
          [int]$Intentos = 20, [int]$EsperaMs = 500,
          [scriptblock]$LeerTamano = { param($r) (Get-Item -LiteralPath $r -Force).Length },
          [scriptblock]$LeerSha = { param($r) Get-Sha256Archivo $r })
    $dir = Split-Path $Destino
    if ($dir) { New-Item -ItemType Directory -Force $dir | Out-Null }
    Copy-Item -LiteralPath $Origen -Destination $Destino -Force
    if (Wait-TamanoArchivo -Ruta $Destino -Tamano $Tamano -Intentos $Intentos -EsperaMs $EsperaMs -LeerTamano $LeerTamano) {
        return [pscustomobject]@{ Verificacion = 'Tamano' }
    }
    $shaDestino = $null
    try { $shaDestino = [string](& $LeerSha $Destino) } catch { $shaDestino = $null }
    if ($shaDestino -and $Sha -and $shaDestino -ieq $Sha) {
        return [pscustomobject]@{ Verificacion = 'Hash' }
    }
    $visto = -1L
    try { $visto = [long](& $LeerTamano $Destino) } catch { }
    throw "la copia en el destino no coincide: tamaño $visto de $Tamano y SHA-256 distinto del descargado"
}

Export-ModuleMember -Function Get-Sha256Archivo, Get-CarpetaTemporalLocal, Wait-TamanoArchivo, Copy-ArchivoVerificado
