<#
.SYNOPSIS
    Sube por FTP los documentos de WebSGV\Uploads (CPIC, manifiestos, facturas, facturas de flete) al sitio publicado,
    en la misma ruta, para que se puedan abrir desde el sistema.
.DESCRIPTION
    Usa el servidor y la carpeta del perfil de publicación FTPProfile1 (ftp://sgvtransporte.somee.com, /www.sgvtransporte.somee.com).
    La contraseña se pide al ejecutar (no se guarda ni se imprime). Salta los archivos que ya están con el mismo tamaño,
    así que se puede repetir sin problema. No sube Uploads\Factura\_temp (borradores) ni borra nada del sitio.
.EXAMPLE
    .\herramientas\publicar\subir-uploads.ps1 -SoloListar
    .\herramientas\publicar\subir-uploads.ps1
#>
param(
    [string] $Usuario = 'SGVTRANSPORTES',
    [string] $Servidor = 'ftp://sgvtransporte.somee.com',
    [string] $RutaSitio = '/www.sgvtransporte.somee.com',
    [switch] $SoloListar,
    [switch] $Diagnostico
)
$ErrorActionPreference = 'Stop'
$raiz = (Resolve-Path (Join-Path $PSScriptRoot '..\..\WebSGV\Uploads')).Path
$archivos = Get-ChildItem $raiz -Recurse -File | Where-Object { $_.FullName -notlike '*\_temp\*' }
"Archivos a revisar: $($archivos.Count) ($([math]::Round(($archivos | Measure-Object Length -Sum).Sum / 1MB, 1)) MB)"
if ($SoloListar) { $archivos | Group-Object { $_.DirectoryName.Substring($raiz.Length) } | ForEach-Object { "{0,4}  {1}" -f $_.Count, $_.Name }; return }

$sec = Read-Host "Contraseña FTP de $Usuario" -AsSecureString
$cred = New-Object System.Net.NetworkCredential($Usuario, $sec)
function Pedir($url, $metodo, $bytes) {
    $r = [System.Net.FtpWebRequest]::Create($url); $r.Method = $metodo; $r.Credentials = $cred
    $r.UsePassive = $true; $r.UseBinary = $true; $r.KeepAlive = $false
    if ($bytes) { $r.ContentLength = $bytes.Length; $s = $r.GetRequestStream(); $s.Write($bytes, 0, $bytes.Length); $s.Close() }
    $resp = $r.GetResponse(); $len = $resp.ContentLength; $resp.Close(); $len
}
$base = $Servidor.TrimEnd('/') + $RutaSitio
if ($Diagnostico) {
    function Listar($url) {
        try {
            $r = [System.Net.FtpWebRequest]::Create($url); $r.Method = 'NLST'; $r.Credentials = $cred; $r.UsePassive = $true; $r.KeepAlive = $false
            $resp = $r.GetResponse(); $sr = New-Object IO.StreamReader($resp.GetResponseStream()); $x = $sr.ReadToEnd(); $sr.Close(); $resp.Close(); $x -split "`r?`n" | Where-Object { $_ }
        } catch { "(no se pudo listar: $($_.Exception.Message))" }
    }
    "== Raíz del FTP ($Servidor/)"; Listar "$Servidor/" | Select-Object -First 40
    "== $RutaSitio"; Listar $base | Select-Object -First 60
    "== $RutaSitio/Uploads"; Listar "$base/Uploads"
    "== $RutaSitio/Uploads/FacturasFlete/2026 (primeros 5)"; Listar "$base/Uploads/FacturasFlete/2026" | Select-Object -First 5
    return
}
$creadas = New-Object System.Collections.Generic.HashSet[string]
$subidos = 0; $iguales = 0; $fallos = 0
foreach ($a in $archivos) {
    $rel = $a.FullName.Substring($raiz.Length).TrimStart('\').Replace('\', '/')
    $carpetas = ('Uploads/' + $rel).Split('/'); $acum = $base
    for ($i = 0; $i -lt $carpetas.Count - 1; $i++) {
        $acum += '/' + $carpetas[$i]
        if ($creadas.Add($acum)) { try { Pedir $acum 'MKD' $null | Out-Null } catch { } }   # ya existe
    }
    $url = "$base/Uploads/" + (($rel -split '/' | ForEach-Object { [uri]::EscapeDataString($_) }) -join '/')
    try {
        $tam = -1; try { $tam = Pedir $url 'SIZE' $null } catch { }
        if ($tam -eq $a.Length) { $iguales++; continue }
        Pedir $url 'STOR' ([IO.File]::ReadAllBytes($a.FullName)) | Out-Null
        $subidos++
        if ($subidos % 20 -eq 0) { "  subidos $subidos..." }
    } catch { $fallos++; "NO se subió $rel : $($_.Exception.Message)" }
}
"Listo. Subidos: $subidos  ya estaban: $iguales  con error: $fallos"
