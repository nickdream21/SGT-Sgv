<#
.SYNOPSIS
    Sello externo de las tablas de solo inserción (FirmaDigital, AuditoriaLog, Auditoria): SHA-256 por
    fila y estado de sus triggers, comparado con el sello anterior. Solo lee la BD.

.DESCRIPTION
    Mientras el usuario de la app sea db_owner, los triggers de solo inserción se pueden desactivar y
    una fila se puede cambiar o borrar sin que la BD lo cuente. Este script deja una prueba FUERA de la
    BD: cada día guarda el SHA-256 de cada fila y la definición de los triggers, y avisa si algo que ya
    existía cambió.

    1. Lee (solo SELECT) cada fila de las tablas con su clave primaria. Las columnas se descubren en
       sys.columns (no hay una lista fija que se desactualice) y se ordenan por nombre; cada valor se
       escribe en forma canónica (fechas ISO con 7 decimales, números invariantes, binarios en hex,
       NULL distinto de la cadena vacía, cada valor con su longitud delante) y se hashea con SHA-256.
       Se lee fila por fila (SqlDataReader): no se carga la tabla entera en memoria.
    2. Lee los triggers de esas tablas (sys.triggers: nombre, is_disabled, eventos y SHA-256 de su
       definición).
    3. Compara con el sello BASE: el más reciente con estado OK (no el último a secas, para que una
       FALLA no "se cure" sola al día siguiente: sigue fallando hasta que alguien la revise y la acepte
       con -AceptarCambios; si no hay ningún OK, el más antiguo de la cadena). FALLA si una fila que existía cambió o falta, si falta una columna, si un
       trigger falta, está desactivado o cambió su definición, o si una tabla no tiene un trigger activo
       para UPDATE y otro para DELETE. Filas nuevas son normales. Si una migración agregó una columna,
       las filas viejas se comparan con las columnas que tenía el sello base.
    4. Guarda <Carpeta>\sellos\sello-<bd>_aaaaMMdd_HHmm.txt.gz (texto con tabuladores comprimido, con
       el hash global en la última línea y el nombre y hash del sello base: forman una cadena), añade
       una línea a <Carpeta>\sellos\sellos.log y aplica la misma retención que los .bacpac (30 más
       recientes + el primero de cada mes de los últimos 12), sin borrar nunca el sello base.
    5. Si hay FALLA: mensaje en rojo, código de salida 1 y aviso con -ScriptAviso (o la variable de
       entorno SGV_SCRIPT_AVISO): un .ps1 que recibe -Asunto y -Cuerpo, normalmente
       herramientas\comun\enviar-aviso.ps1 (E6-14: correo con el SMTP de appSettings.Secrets.config); sin
       ese script, el aviso es la salida de la tarea y sellos.log.

    El sello no contiene datos de las filas: solo ids, hashes, nombres de columnas y de triggers.

    Códigos de salida: 0 OK; 1 FALLA de integridad; 2 no se pudo sellar (conexión, lectura, carpeta).

    Producción: solo con -ConfirmoProduccion. Ensayo: -Entorno ensayo -BaseDeDatos sgvEnsayo_* (SQL
    Server local, autenticación de Windows; ver WebSGV\Database\conexion-bd.ps1).

.EXAMPLE
    .\herramientas\respaldo\sellar-registros.ps1 -Carpeta D:\RespaldosSGV
    .\herramientas\respaldo\sellar-registros.ps1 -Entorno ensayo -BaseDeDatos sgvEnsayo_20261122_1000 -Carpeta C:\temp\sellos-ensayo
    .\herramientas\respaldo\sellar-registros.ps1 -Carpeta D:\RespaldosSGV -AceptarCambios -Motivo 'Migración 64 reescribió Auditoria.Campo'
    .\herramientas\respaldo\sellar-registros.ps1 -Carpeta D:\RespaldosSGV -Entorno produccion -ConfirmoProduccion
#>
param(
    [string]$Carpeta,

    [ValidateSet('pruebas', 'produccion', 'ensayo')]
    [string]$Entorno = 'pruebas',

    # Solo con -Entorno ensayo.
    [string]$BaseDeDatos,
    [string]$Servidor,

    [string[]]$Tablas = @('FirmaDigital', 'AuditoriaLog', 'Auditoria'),

    # Tras revisar una FALLA y decidir que los cambios son legítimos: el sello nuevo queda como base.
    [switch]$AceptarCambios,
    [string]$Motivo,

    [string]$ScriptAviso = $env:SGV_SCRIPT_AVISO,
    [switch]$ConfirmoProduccion
)

$ErrorActionPreference = 'Stop'

# =================================================================================================
# Funciones puras (probar-sellar.ps1 las extrae de este archivo y las prueba sin BD)
# =================================================================================================

# Orden fijo de columnas: por nombre en minúsculas invariantes y, a igualdad, ordinal.
function Get-OrdenColumnas {
    param([string[]]$Nombres)
    $lista = New-Object 'System.Collections.Generic.List[string]'
    foreach ($n in $Nombres) { $lista.Add($n) }
    $comparador = [System.Comparison[string]] {
        param($a, $b)
        $c = [string]::CompareOrdinal($a.ToLowerInvariant(), $b.ToLowerInvariant())
        if ($c -ne 0) { return $c }
        return [string]::CompareOrdinal($a, $b)
    }
    $lista.Sort($comparador)
    return , $lista.ToArray()
}

# Texto canónico de un valor leído con SqlDataReader. $null para NULL (DBNull).
function ConvertTo-ValorCanonico {
    param($Valor)
    if ($null -eq $Valor -or $Valor -is [DBNull]) { return $null }
    $inv = [Globalization.CultureInfo]::InvariantCulture
    if ($Valor -is [string])          { return $Valor }
    if ($Valor -is [datetime])        { return $Valor.ToString('yyyy-MM-ddTHH:mm:ss.fffffff', $inv) }
    if ($Valor -is [DateTimeOffset])  { return $Valor.ToString('yyyy-MM-ddTHH:mm:ss.fffffffzzz', $inv) }
    if ($Valor -is [TimeSpan])        { return $Valor.ToString('c', $inv) }
    if ($Valor -is [byte[]])          { return ('0x' + [BitConverter]::ToString($Valor).Replace('-', '')) }
    if ($Valor -is [bool])            { if ($Valor) { return '1' } else { return '0' } }
    if ($Valor -is [double])          { return $Valor.ToString('R', $inv) }
    if ($Valor -is [single])          { return $Valor.ToString('R', $inv) }
    if ($Valor -is [guid])            { return $Valor.ToString('D') }
    if ($Valor -is [IFormattable])    { return $Valor.ToString($null, $inv) }
    return [string]$Valor
}

# Representación canónica de una fila: columnas en el orden recibido (el de Get-OrdenColumnas),
# cada una "<largo>:<nombre>" y "N" (NULL) o "V<largo>:<valor>": sin separadores ambiguos.
function ConvertTo-FilaCanonica {
    param([string[]]$Columnas, [object[]]$Valores)
    if ($Columnas.Count -ne $Valores.Count) { throw "ConvertTo-FilaCanonica: $($Columnas.Count) columnas y $($Valores.Count) valores." }
    $sb = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $Columnas.Count; $i++) {
        $n = $Columnas[$i]
        [void]$sb.Append($n.Length).Append(':').Append($n)
        $v = ConvertTo-ValorCanonico $Valores[$i]
        if ($null -eq $v) { [void]$sb.Append('N') }
        else { [void]$sb.Append('V').Append($v.Length).Append(':').Append($v) }
        [void]$sb.Append(';')
    }
    return $sb.ToString()
}

function Get-Sha256Texto {
    param([string]$Texto)
    if (-not $script:sha256Sello) { $script:sha256Sello = [Security.Cryptography.SHA256]::Create() }
    $bytes = $script:sha256Sello.ComputeHash([Text.Encoding]::UTF8.GetBytes($Texto))
    return ([BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant())
}

function Get-HashFila {
    param([string[]]$Columnas, [object[]]$Valores)
    return (Get-Sha256Texto (ConvertTo-FilaCanonica $Columnas $Valores))
}

# Hash de la definición de un trigger: solo se unifican los saltos de línea (CRLF/LF) y se quitan los
# espacios y saltos del principio y del final (un .bacpac importado los pierde: se vio en el ensayo
# del 2026-11-22 con trg_FirmaDigital_*, mismo texto con 2 caracteres menos al final).
function Get-HashDefinicion {
    param([string]$Definicion)
    if ($null -eq $Definicion) { return 'sin-definicion' }
    return (Get-Sha256Texto ($Definicion.Replace("`r`n", "`n").Replace("`r", "`n").Trim()))
}

function New-SelloVacio {
    param([datetime]$Fecha, [string]$Bd, [string]$Entorno)
    [pscustomobject]@{
        Version = 1; Fecha = $Fecha; Bd = $Bd; Entorno = $Entorno; Estado = 'OK'; Aceptado = $null
        Anterior = $null; AnteriorHash = $null
        Tablas = [ordered]@{}; Triggers = (New-Object System.Collections.ArrayList); HashGlobal = $null
    }
}

function New-TablaSello {
    param([string]$Nombre, [string]$Pk, [string[]]$Columnas)
    [pscustomobject]@{
        Nombre = $Nombre; Pk = $Pk; Columnas = $Columnas; Filas = 0; MaxId = $null
        Hashes = (New-Object 'System.Collections.Generic.SortedDictionary[long,string]')
        # Hashes con las columnas del sello base (solo si cambiaron las columnas; no se guarda).
        HashesBase = $null
    }
}

# Líneas del archivo de sello (sin la de "fin"); el hash global es el SHA-256 de estas líneas
# unidas con "\n" (con "\n" final).
function ConvertTo-LineasSello {
    param($Sello)
    $l = New-Object 'System.Collections.Generic.List[string]'
    $l.Add("SGV-SELLO`t$($Sello.Version)")
    $l.Add("fecha`t" + $Sello.Fecha.ToString('yyyy-MM-ddTHH:mm:ss.fffffffzzz', [Globalization.CultureInfo]::InvariantCulture))
    $l.Add("bd`t$($Sello.Bd)")
    $l.Add("entorno`t$($Sello.Entorno)")
    $l.Add("estado`t$($Sello.Estado)")
    if ($Sello.Aceptado) { $l.Add("aceptado`t" + ($Sello.Aceptado -replace '[\t\r\n]', ' ')) }
    if ($Sello.Anterior) { $l.Add("anterior`t$($Sello.Anterior)`t$($Sello.AnteriorHash)") }
    foreach ($t in $Sello.Tablas.Values) {
        $max = if ($null -eq $t.MaxId) { '' } else { [string]$t.MaxId }
        $l.Add("tabla`t$($t.Nombre)`t$($t.Filas)`t$max`t$($t.Pk)`t$($t.Columnas -join ',')")
    }
    foreach ($g in $Sello.Triggers) {
        $l.Add("trigger`t$($g.Tabla)`t$($g.Nombre)`t$([int][bool]$g.Desactivado)`t$([int][bool]$g.InsteadOf)`t$($g.Eventos)`t$($g.HashDefinicion)")
    }
    foreach ($t in $Sello.Tablas.Values) {
        foreach ($par in $t.Hashes.GetEnumerator()) { $l.Add("fila`t$($t.Nombre)`t$($par.Key)`t$($par.Value)") }
    }
    return , $l
}

function Get-HashLineas {
    param($Lineas)
    $sb = New-Object System.Text.StringBuilder
    foreach ($x in $Lineas) { [void]$sb.Append($x).Append("`n") }
    return (Get-Sha256Texto $sb.ToString())
}

# Lee las líneas de un sello. Lanza un error si el formato no es válido o el hash global no cuadra.
function ConvertFrom-LineasSello {
    param([string[]]$Lineas)
    $Lineas = @($Lineas | Where-Object { $_ -ne '' })
    if ($Lineas.Count -lt 2 -or $Lineas[0] -notmatch '^SGV-SELLO\t1$') { throw 'no es un sello SGV (versión 1)' }
    $ultima = $Lineas[$Lineas.Count - 1]
    if ($ultima -notmatch '^fin\t([0-9a-f]{64})$') { throw 'sello incompleto (falta la línea fin)' }
    $hashLeido = $Matches[1]
    $cuerpo = $Lineas[0..($Lineas.Count - 2)]
    $hashCalculado = Get-HashLineas $cuerpo
    if ($hashCalculado -ne $hashLeido) { throw 'el hash global no coincide (sello dañado o alterado)' }

    $s = New-SelloVacio -Fecha ([datetime]::MinValue) -Bd '' -Entorno ''
    $s.HashGlobal = $hashLeido
    foreach ($x in $cuerpo) {
        $p = $x.Split("`t")
        switch ($p[0]) {
            'fecha'    { $s.Fecha = [DateTimeOffset]::ParseExact($p[1], 'yyyy-MM-ddTHH:mm:ss.fffffffzzz', [Globalization.CultureInfo]::InvariantCulture).LocalDateTime }
            'bd'       { $s.Bd = $p[1] }
            'entorno'  { $s.Entorno = $p[1] }
            'estado'   { $s.Estado = $p[1] }
            'aceptado' { $s.Aceptado = $p[1] }
            'anterior' { $s.Anterior = $p[1]; $s.AnteriorHash = $p[2] }
            'tabla' {
                $cols = if ($p[5]) { [string[]]$p[5].Split(',') } else { [string[]]@() }
                $t = New-TablaSello -Nombre $p[1] -Pk $p[4] -Columnas $cols
                $t.Filas = [long]$p[2]
                if ($p[3] -ne '') { $t.MaxId = [long]$p[3] }
                $s.Tablas[$p[1]] = $t
            }
            'trigger' {
                [void]$s.Triggers.Add([pscustomobject]@{ Tabla = $p[1]; Nombre = $p[2]; Desactivado = ($p[3] -eq '1'); InsteadOf = ($p[4] -eq '1'); Eventos = $p[5]; HashDefinicion = $p[6] })
            }
            'fila' {
                if (-not $s.Tablas.Contains($p[1])) { throw "fila de una tabla no declarada ($($p[1]))" }
                $s.Tablas[$p[1]].Hashes[[long]$p[2]] = $p[3]
            }
        }
    }
    foreach ($t in $s.Tablas.Values) {
        if ($t.Hashes.Count -ne $t.Filas) { throw "la tabla $($t.Nombre) declara $($t.Filas) filas y trae $($t.Hashes.Count)" }
    }
    return $s
}

function Format-ListaIds {
    param($Ids, [int]$Maximo = 20)
    $a = @($Ids)
    $texto = ($a | Select-Object -First $Maximo) -join ', '
    if ($a.Count -gt $Maximo) { $texto += " ... (+$($a.Count - $Maximo))" }
    return $texto
}

# Reglas que no dependen del sello anterior: cada tabla con un trigger activo para UPDATE y otro para
# DELETE, y ningún trigger desactivado.
function Test-TriggersSello {
    param($Sello)
    $fallas = New-Object 'System.Collections.Generic.List[string]'
    foreach ($g in $Sello.Triggers) {
        if ($g.Desactivado) { $fallas.Add("$($g.Tabla): el trigger $($g.Nombre) está DESACTIVADO.") }
    }
    foreach ($t in $Sello.Tablas.Keys) {
        foreach ($evento in 'UPDATE', 'DELETE') {
            $activos = @($Sello.Triggers | Where-Object { $_.Tabla -eq $t -and -not $_.Desactivado -and (($_.Eventos -split ',') -contains $evento) })
            if ($activos.Count -eq 0) { $fallas.Add("${t}: no hay ningún trigger activo que bloquee $evento.") }
        }
    }
    return , $fallas
}

# Compara el sello actual con el base ($null si es el primero). Devuelve Estado (OK/FALLA), Fallas,
# Avisos y FilasNuevas por tabla.
function Compare-Sellos {
    param($Anterior, $Actual)
    $fallas = New-Object 'System.Collections.Generic.List[string]'
    $avisos = New-Object 'System.Collections.Generic.List[string]'
    $nuevas = [ordered]@{}
    foreach ($f in (Test-TriggersSello $Actual)) { $fallas.Add($f) }

    if ($null -ne $Anterior) {
        foreach ($ta in $Anterior.Tablas.Values) {
            if (-not $Actual.Tablas.Contains($ta.Nombre)) { $fallas.Add("$($ta.Nombre): la tabla ya no se pudo sellar (¿desapareció?)."); continue }
            $tn = $Actual.Tablas[$ta.Nombre]
            $faltanCols = @($ta.Columnas | Where-Object { $tn.Columnas -notcontains $_ })
            $colsNuevas = @($tn.Columnas | Where-Object { $ta.Columnas -notcontains $_ })
            if ($faltanCols.Count -gt 0) { $fallas.Add("$($ta.Nombre): ya no existe(n) la(s) columna(s) $($faltanCols -join ', ') (sus datos se perdieron).") }
            if ($colsNuevas.Count -gt 0) { $avisos.Add("$($ta.Nombre): columna(s) nueva(s) $($colsNuevas -join ', '); las filas viejas se comparan con las columnas del sello base.") }
            $hashesActuales = if ($null -ne $tn.HashesBase) { $tn.HashesBase } else { $tn.Hashes }
            $comparables = ($faltanCols.Count -eq 0)

            $borradas = New-Object 'System.Collections.Generic.List[long]'
            $cambiadas = New-Object 'System.Collections.Generic.List[long]'
            foreach ($par in $ta.Hashes.GetEnumerator()) {
                $h = $null
                if (-not $tn.Hashes.ContainsKey($par.Key)) { $borradas.Add($par.Key) }
                elseif ($comparables -and $hashesActuales.TryGetValue($par.Key, [ref]$h) -and $h -ne $par.Value) { $cambiadas.Add($par.Key) }
            }
            if ($borradas.Count -gt 0) { $fallas.Add("$($ta.Nombre): $($borradas.Count) fila(s) BORRADA(S): $($ta.Pk) $(Format-ListaIds $borradas).") }
            if ($cambiadas.Count -gt 0) { $fallas.Add("$($ta.Nombre): $($cambiadas.Count) fila(s) CAMBIADA(S): $($ta.Pk) $(Format-ListaIds $cambiadas).") }

            $nuevasTabla = @($tn.Hashes.Keys | Where-Object { -not $ta.Hashes.ContainsKey($_) })
            $nuevas[$ta.Nombre] = $nuevasTabla.Count
            if ($null -ne $ta.MaxId) {
                $atrasadas = @($nuevasTabla | Where-Object { $_ -le $ta.MaxId })
                if ($atrasadas.Count -gt 0) { $avisos.Add("$($ta.Nombre): $($atrasadas.Count) fila(s) nueva(s) con id menor que el máximo del sello base ($($ta.MaxId)): $(Format-ListaIds $atrasadas). Puede ser una transacción que terminó tarde; si se repite, revisar.") }
            }
        }
        foreach ($tn in $Actual.Tablas.Values) {
            if (-not $Anterior.Tablas.Contains($tn.Nombre)) { $avisos.Add("$($tn.Nombre): tabla nueva en el sello (no estaba en el base)."); $nuevas[$tn.Nombre] = $tn.Filas }
        }

        foreach ($ga in $Anterior.Triggers) {
            $gn = @($Actual.Triggers | Where-Object { $_.Tabla -eq $ga.Tabla -and $_.Nombre -eq $ga.Nombre }) | Select-Object -First 1
            if ($null -eq $gn) { $fallas.Add("$($ga.Tabla): el trigger $($ga.Nombre) DESAPARECIÓ."); continue }
            if ($gn.HashDefinicion -ne $ga.HashDefinicion) { $fallas.Add("$($ga.Tabla): la definición del trigger $($ga.Nombre) CAMBIÓ.") }
            if ($gn.Eventos -ne $ga.Eventos -or [bool]$gn.InsteadOf -ne [bool]$ga.InsteadOf) { $fallas.Add("$($ga.Tabla): el trigger $($ga.Nombre) cambió de eventos o de tipo ($($ga.Eventos) -> $($gn.Eventos)).") }
        }
        foreach ($gn in $Actual.Triggers) {
            if (-not @($Anterior.Triggers | Where-Object { $_.Tabla -eq $gn.Tabla -and $_.Nombre -eq $gn.Nombre })) { $avisos.Add("$($gn.Tabla): trigger nuevo $($gn.Nombre).") }
        }
    } else {
        foreach ($tn in $Actual.Tablas.Values) { $nuevas[$tn.Nombre] = $tn.Filas }
    }

    $estado = if ($fallas.Count -gt 0) { 'FALLA' } else { 'OK' }
    return [pscustomobject]@{ Estado = $estado; Fallas = $fallas; Avisos = $avisos; FilasNuevas = $nuevas }
}

function Get-NombreSello {
    param([string]$Bd, [datetime]$Fecha)
    return ('sello-{0}_{1}.txt.gz' -f $Bd, $Fecha.ToString('yyyyMMdd_HHmm', [Globalization.CultureInfo]::InvariantCulture))
}

function Get-FechaNombreSello {
    param([string]$Nombre)
    if ($Nombre -match '_(\d{8})_(\d{4})\.txt\.gz$') {
        $f = [datetime]::MinValue
        if ([datetime]::TryParseExact($Matches[1] + $Matches[2], 'yyyyMMddHHmm', [Globalization.CultureInfo]::InvariantCulture,
                [Globalization.DateTimeStyles]::None, [ref]$f)) { return $f }
    }
    return $null
}

# Misma regla que los .bacpac (Get-RespaldosABorrar de respaldar-bd.ps1): los 30 más recientes y, de
# los anteriores, el primero de cada mes de los últimos 12. Nunca borra los de -Proteger (el sello
# base). Los nombres que no siguen el patrón se ignoran.
function Get-SellosABorrar {
    param([string[]]$Nombres, [datetime]$Hoy, [string[]]$Proteger = @(), [int]$Recientes = 30, [int]$Meses = 12)
    $items = @()
    foreach ($n in $Nombres) {
        $f = Get-FechaNombreSello $n
        if ($null -ne $f) { $items += New-Object PSObject -Property @{ Nombre = $n; Fecha = $f } }
    }
    if ($items.Count -le $Recientes) { return @() }
    $ordenados = @($items | Sort-Object Fecha, Nombre -Descending)
    $anteriores = $ordenados[$Recientes..($ordenados.Count - 1)]
    $primeroDelMes = @{}
    foreach ($i in ($items | Sort-Object Fecha, Nombre)) {
        $clave = $i.Fecha.ToString('yyyy-MM')
        if (-not $primeroDelMes.ContainsKey($clave)) { $primeroDelMes[$clave] = $i.Nombre }
    }
    $limite = $Hoy.Date.AddMonths(-$Meses)
    $borrar = @()
    foreach ($i in $anteriores) {
        $esMensual = ($primeroDelMes[$i.Fecha.ToString('yyyy-MM')] -eq $i.Nombre) -and ($i.Fecha -ge $limite)
        if (-not $esMensual -and $Proteger -notcontains $i.Nombre) { $borrar += $i.Nombre }
    }
    return $borrar
}

# Elige el sello base entre los candidatos (objetos con Nombre, Fecha, Estado, Error, Bd, HashGlobal,
# Anterior y AnteriorHash; Error <> $null si no se pudo leer). $Bd: la BD que se sella.
#   - Un candidato de otra BD no sirve de base (aviso).
#   - Base: el más reciente válido con estado OK. Si no hay ninguno OK, el válido MÁS ANTIGUO: con
#     FALLAS seguidas (p. ej. una BD sin triggers) la base no avanza y una fila cambiada sigue saliendo.
#   - Cadena: si el sello "anterior" que declara el base todavía existe, su hash global debe ser el
#     declarado (aviso si no).
# Devuelve Base, Fallas (sellos dañados más recientes que el base) y Avisos.
function Select-SelloBase {
    param($Candidatos, [string]$Bd)
    $fallas = New-Object 'System.Collections.Generic.List[string]'
    $avisos = New-Object 'System.Collections.Generic.List[string]'
    $orden = @($Candidatos | Sort-Object Fecha, Nombre -Descending)
    $validos = New-Object System.Collections.ArrayList
    $base = $null
    foreach ($c in $orden) {
        if ($c.Error) { $fallas.Add("El sello $($c.Nombre) no se puede leer: $($c.Error)."); continue }
        if ($Bd -and $c.Bd -ne $Bd) { $avisos.Add("El sello $($c.Nombre) es de la BD '$($c.Bd)', no de '$Bd': no se usa como base."); continue }
        [void]$validos.Add($c)
        if ($c.Estado -eq 'OK') { $base = $c; break }
    }
    if ($null -eq $base -and $validos.Count -gt 0) {
        $base = $validos[$validos.Count - 1]
        $avisos.Add("Ningún sello anterior tiene estado OK: se compara con el más antiguo de la cadena ($($base.Nombre)), para que una FALLA no se pierda al día siguiente.")
    }
    if ($null -ne $base -and $base.Anterior) {
        $previo = @($Candidatos | Where-Object { $_.Nombre -eq $base.Anterior }) | Select-Object -First 1
        if ($null -ne $previo) {
            if ($previo.Error) { $avisos.Add("Cadena: el sello anterior del base ($($previo.Nombre)) no se puede leer.") }
            elseif ($previo.HashGlobal -ne $base.AnteriorHash) { $avisos.Add("Cadena rota: el base ($($base.Nombre)) declara para $($previo.Nombre) un hash distinto del que tiene ese archivo hoy (¿se reemplazó un sello?).") }
        }
    }
    return [pscustomobject]@{ Base = $base; Fallas = $fallas; Avisos = $avisos }
}

# Nombre de un sello de exactamente esta BD (el filtro de archivos "sello-<bd>_*" también encuentra
# los de una BD cuyo nombre empiece igual, p. ej. sgvEnsayo_x y sgvEnsayo_x_y).
function Test-NombreSelloDeBd {
    param([string]$Nombre, [string]$Bd)
    return ($Nombre -match ('\Asello-' + [regex]::Escape($Bd) + '_\d{8}_\d{4}\.txt\.gz\z'))
}

# =================================================================================================
# Acceso a la BD y archivos
# =================================================================================================

function Read-LineasGzip([string]$Ruta, [int]$Maximo = 0) {
    $fs = [IO.File]::OpenRead($Ruta)
    try {
        $gz = New-Object IO.Compression.GZipStream($fs, [IO.Compression.CompressionMode]::Decompress)
        $sr = New-Object IO.StreamReader($gz, (New-Object Text.UTF8Encoding($false)))
        $lineas = New-Object 'System.Collections.Generic.List[string]'
        while ($null -ne ($x = $sr.ReadLine())) {
            $lineas.Add($x)
            if ($Maximo -gt 0 -and $lineas.Count -ge $Maximo) { break }
        }
        $sr.Dispose()
        return , $lineas.ToArray()
    } finally { $fs.Dispose() }
}

function Write-LineasGzip([string]$Ruta, $Lineas) {
    $fs = [IO.File]::Create($Ruta)
    try {
        $gz = New-Object IO.Compression.GZipStream($fs, [IO.Compression.CompressionLevel]::Optimal)
        $sw = New-Object IO.StreamWriter($gz, (New-Object Text.UTF8Encoding($false)))
        $sw.NewLine = "`n"
        foreach ($x in $Lineas) { $sw.WriteLine($x) }
        $sw.Dispose()
    } finally { $fs.Dispose() }
}

function Invoke-Lectura($Conexion, [string]$Sql, [hashtable]$Parametros = @{}) {
    $cmd = $Conexion.CreateCommand()
    $cmd.CommandText = $Sql
    $cmd.CommandTimeout = 600
    foreach ($k in $Parametros.Keys) { [void]$cmd.Parameters.AddWithValue($k, $Parametros[$k]) }
    $tabla = New-Object System.Data.DataTable
    (New-Object System.Data.SqlClient.SqlDataAdapter($cmd)).Fill($tabla) | Out-Null
    return , $tabla
}

# Lee una tabla fila por fila y llena el objeto de tabla del sello. $ColumnasBase: las del sello base
# (si son distintas de las actuales, también se calculan los hashes con ellas).
function Read-TablaSello($Conexion, [string]$Tabla, [string[]]$ColumnasBase) {
    $id = Invoke-Lectura $Conexion "SELECT OBJECT_ID(@t, 'U') AS id" @{ '@t' = "dbo.$Tabla" }
    if ($id.Rows[0].id -is [DBNull]) { throw "No existe la tabla dbo.$Tabla." }
    $objId = [int]$id.Rows[0].id
    $cols = Invoke-Lectura $Conexion "SELECT c.name, TYPE_NAME(c.user_type_id) AS tipo, t.is_assembly_type FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id WHERE c.object_id = @o" @{ '@o' = $objId }
    $udt = @($cols.Rows | Where-Object { $_.is_assembly_type })
    if ($udt.Count -gt 0) { throw "dbo.$Tabla tiene columnas de tipo CLR ($($udt[0].name)): el sello no sabe canonizarlas." }
    $pk = Invoke-Lectura $Conexion "SELECT c.name, TYPE_NAME(c.user_type_id) AS tipo FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE i.is_primary_key = 1 AND i.object_id = @o" @{ '@o' = $objId }
    if ($pk.Rows.Count -ne 1 -or @('int', 'bigint', 'smallint', 'tinyint') -notcontains [string]$pk.Rows[0].tipo) {
        throw "dbo.$Tabla necesita una clave primaria de una sola columna entera."
    }
    $nombrePk = [string]$pk.Rows[0].name
    $actuales = Get-OrdenColumnas @($cols.Rows | ForEach-Object { [string]$_.name })
    $t = New-TablaSello -Nombre $Tabla -Pk $nombrePk -Columnas $actuales

    $usarBase = $false
    $colsBase = @()
    if ($ColumnasBase -and $ColumnasBase.Count -gt 0) {
        # Sin @(...): Get-OrdenColumnas ya devuelve un arreglo (con @() quedaría un arreglo dentro de otro).
        [string[]]$colsBase = Get-OrdenColumnas @($ColumnasBase | Where-Object { $actuales -contains $_ })
        $usarBase = (($colsBase -join ',') -ne ($actuales -join ','))
        if ($usarBase) { $t.HashesBase = New-Object 'System.Collections.Generic.SortedDictionary[long,string]' }
    }

    foreach ($c in $actuales) { if ($c -match '[\]\t,]') { throw "Nombre de columna no admitido en dbo.${Tabla}: '$c'." } }
    $lista = ($actuales | ForEach-Object { "[$_]" }) -join ', '
    $iPk = [Array]::IndexOf($actuales, $nombrePk)
    $iBase = @($colsBase | ForEach-Object { [Array]::IndexOf($actuales, $_) })

    $cmd = $Conexion.CreateCommand()
    $cmd.CommandText = "SELECT $lista FROM [dbo].[$Tabla] ORDER BY [$nombrePk]"
    $cmd.CommandTimeout = 1800
    $r = $cmd.ExecuteReader()
    try {
        $valores = New-Object object[] $actuales.Count
        $valoresBase = New-Object object[] $colsBase.Count
        while ($r.Read()) {
            [void]$r.GetValues($valores)
            $clave = [long]$valores[$iPk]
            $t.Hashes[$clave] = Get-HashFila $actuales $valores
            if ($usarBase) {
                for ($i = 0; $i -lt $iBase.Count; $i++) { $valoresBase[$i] = $valores[$iBase[$i]] }
                $t.HashesBase[$clave] = Get-HashFila $colsBase $valoresBase
            }
            $t.Filas++
            if ($null -eq $t.MaxId -or $clave -gt $t.MaxId) { $t.MaxId = $clave }
        }
    } finally { $r.Close() }
    return $t
}

function Read-TriggersSello($Conexion, [string[]]$Tablas) {
    $lista = New-Object System.Collections.ArrayList
    foreach ($tabla in $Tablas) {
        $filas = Invoke-Lectura $Conexion @"
SELECT tr.name, tr.is_disabled, tr.is_instead_of_trigger, OBJECT_DEFINITION(tr.object_id) AS definicion,
       ISNULL((SELECT STRING_AGG(e.type_desc, ',') WITHIN GROUP (ORDER BY e.type_desc) FROM sys.trigger_events e WHERE e.object_id = tr.object_id), '') AS eventos
FROM sys.triggers tr WHERE tr.parent_id = OBJECT_ID(@t, 'U') ORDER BY tr.name
"@ @{ '@t' = "dbo.$tabla" }
        foreach ($f in $filas.Rows) {
            $def = if ($f.definicion -is [DBNull]) { $null } else { [string]$f.definicion }
            [void]$lista.Add([pscustomobject]@{
                Tabla = $tabla; Nombre = [string]$f.name; Desactivado = [bool]$f.is_disabled; InsteadOf = [bool]$f.is_instead_of_trigger
                Eventos = [string]$f.eventos; HashDefinicion = (Get-HashDefinicion $def)
            })
        }
    }
    return , $lista
}

# Cargado con ". " (solo las funciones): no corre la parte principal. probar-sellar.ps1 no lo carga
# así: extrae las funciones del árbol de sintaxis sin ejecutar nada.
if ($MyInvocation.InvocationName -eq '.') { return }

# =================================================================================================
# Principal
# =================================================================================================
$inicio = Get-Date
try {
    if ($Entorno -eq 'produccion' -and -not $ConfirmoProduccion) { throw "Para usar PRODUCCION agregue -ConfirmoProduccion." }
    if ($AceptarCambios -and [string]::IsNullOrWhiteSpace($Motivo)) { throw "-AceptarCambios exige -Motivo 'por qué los cambios son legítimos'." }
    foreach ($t in $Tablas) { if ($t -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { throw "Nombre de tabla no válido: '$t'." } }
    if ([string]::IsNullOrWhiteSpace($Carpeta)) { $Carpeta = $env:SGV_CARPETA_RESPALDO }
    if ([string]::IsNullOrWhiteSpace($Carpeta)) { throw "Indique -Carpeta <ruta> o defina la variable de entorno SGV_CARPETA_RESPALDO." }

    $raizRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..')).TrimEnd('\') + '\'
    $carpetaCompleta = [IO.Path]::GetFullPath($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Carpeta)).TrimEnd('\') + '\'
    if ($carpetaCompleta.StartsWith($raizRepo, [StringComparison]::OrdinalIgnoreCase)) {
        throw "La carpeta '$($carpetaCompleta.TrimEnd('\'))' está dentro del repositorio. Use la carpeta de respaldos (fuera del repo)."
    }
    $carpetaSellos = Join-Path $carpetaCompleta.TrimEnd('\') 'sellos'
    New-Item -ItemType Directory -Force $carpetaSellos | Out-Null
    $rutaLog = Join-Path $carpetaSellos 'sellos.log'

    $dirWeb = Join-Path $raizRepo 'WebSGV'
    . (Join-Path $dirWeb 'Database\conexion-bd.ps1')
    Import-Module (Join-Path $PSScriptRoot '..\comun\CopiaVerificada.psm1') -Force -DisableNameChecking
    $con = Get-ConexionSgv -Entorno $Entorno -BaseDeDatos $BaseDeDatos -Servidor $Servidor -DirWeb $dirWeb
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}

"Entorno : $Entorno ($($con.Bd))"
"Sellos  : $carpetaSellos"
$nombre = Get-NombreSello -Bd $con.Bd -Fecha $inicio
$rutaFinal = Join-Path $carpetaSellos $nombre
if (Test-Path -LiteralPath $rutaFinal) {
    Write-Host "ERROR: ya existe $rutaFinal (otro sello en el mismo minuto). Espere un minuto y repita." -ForegroundColor Red
    exit 2
}

# --- Sello base -----------------------------------------------------------------------------------
$candidatos = @()
foreach ($a in @(Get-ChildItem -LiteralPath $carpetaSellos -File -Filter "sello-$($con.Bd)_*.txt.gz" -ErrorAction SilentlyContinue)) {
    if (-not (Test-NombreSelloDeBd $a.Name $con.Bd)) { continue }
    $f = Get-FechaNombreSello $a.Name
    if ($null -eq $f) { continue }
    $c = [pscustomobject]@{ Nombre = $a.Name; Ruta = $a.FullName; Fecha = $f; Estado = $null; Error = $null; Sello = $null
                            Bd = $null; HashGlobal = $null; Anterior = $null; AnteriorHash = $null }
    try {
        $c.Sello = ConvertFrom-LineasSello (Read-LineasGzip $a.FullName)
        $c.Estado = $c.Sello.Estado; $c.Bd = $c.Sello.Bd; $c.HashGlobal = $c.Sello.HashGlobal
        $c.Anterior = $c.Sello.Anterior; $c.AnteriorHash = $c.Sello.AnteriorHash
    } catch { $c.Error = $_.Exception.Message }
    $candidatos += $c
}
$eleccion = Select-SelloBase $candidatos -Bd $con.Bd
$base = if ($eleccion.Base) { $eleccion.Base.Sello } else { $null }
if ($base) { "Base    : $($eleccion.Base.Nombre) ($($base.Estado))" } else { "Base    : (ninguna: es el primer sello)" }

# --- Lectura (solo SELECT) ------------------------------------------------------------------------
$actual = New-SelloVacio -Fecha $inicio -Bd $con.Bd -Entorno $Entorno
try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($con.Cadena)
    $conn.Open()
    try {
        $bdReal = (Invoke-Lectura $conn 'SELECT DB_NAME() AS bd').Rows[0].bd
        if ($bdReal -ne $con.Bd) { throw "La conexión abrió '$bdReal' y se esperaba '$($con.Bd)'." }
        foreach ($t in $Tablas) {
            $colsBase = $null
            if ($base -and $base.Tablas.Contains($t)) { $colsBase = $base.Tablas[$t].Columnas }
            $iniT = Get-Date
            $actual.Tablas[$t] = Read-TablaSello $conn $t $colsBase
            "  {0,-14} {1,8} filas  máx. id {2,-8} {3:N1} s" -f $t, $actual.Tablas[$t].Filas, $actual.Tablas[$t].MaxId, ((Get-Date) - $iniT).TotalSeconds
        }
        foreach ($g in (Read-TriggersSello $conn $Tablas)) { [void]$actual.Triggers.Add($g) }
    } finally { $conn.Close() }
} catch {
    $msg = $_.Exception.Message
    if ($con.Clave) { $msg = $msg.Replace($con.Clave, '********') }
    try { Add-Content -Path $rutaLog -Encoding UTF8 -Value ('{0:yyyy-MM-dd HH:mm}  ERROR  {1}  {2}' -f $inicio, $Entorno, $msg) } catch { }
    Write-Host "ERROR: no se pudo leer la BD: $msg" -ForegroundColor Red
    exit 2
}

# --- Comparación ----------------------------------------------------------------------------------
$res = Compare-Sellos $base $actual
foreach ($f in $eleccion.Fallas) { $res.Fallas.Insert(0, $f) }
foreach ($x in $eleccion.Avisos) { $res.Avisos.Add($x) }
$estado = if ($res.Fallas.Count -gt 0) { 'FALLA' } else { 'OK' }
$actual.Estado = if ($estado -eq 'FALLA' -and $AceptarCambios) { 'OK' } else { $estado }
if ($AceptarCambios) { $actual.Aceptado = "$([Security.Principal.WindowsIdentity]::GetCurrent().Name): $Motivo" }
if ($base) { $actual.Anterior = $eleccion.Base.Nombre; $actual.AnteriorHash = $base.HashGlobal }

# --- Guardar el sello (local, copia verificada al destino, renombre) ------------------------------
try {
    $lineas = ConvertTo-LineasSello $actual
    $actual.HashGlobal = Get-HashLineas $lineas
    $lineas.Add("fin`t$($actual.HashGlobal)")
    $rutaLocal = Join-Path (Get-CarpetaTemporalLocal 'sellos') ($nombre + '.parcial')
    $rutaParcial = $rutaFinal + '.parcial'
    try {
        Write-LineasGzip $rutaLocal $lineas
        # Se relee lo escrito antes de copiarlo: un sello que no se puede leer no sirve de base.
        [void](ConvertFrom-LineasSello (Read-LineasGzip $rutaLocal))
        $tamano = (Get-Item -LiteralPath $rutaLocal).Length
        [void](Copy-ArchivoVerificado -Origen $rutaLocal -Destino $rutaParcial -Tamano $tamano -Sha (Get-Sha256Archivo $rutaLocal))
        Move-Item -LiteralPath $rutaParcial -Destination $rutaFinal
    } finally {
        Remove-Item -LiteralPath $rutaLocal -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $rutaParcial) { Remove-Item -LiteralPath $rutaParcial -Force -ErrorAction SilentlyContinue }
    }
} catch {
    try { Add-Content -Path $rutaLog -Encoding UTF8 -Value ('{0:yyyy-MM-dd HH:mm}  ERROR  {1}  guardar el sello: {2}' -f $inicio, $Entorno, $_.Exception.Message) } catch { }
    Write-Host "ERROR al guardar el sello: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}

# --- Retención (nunca el sello base ni el nuevo) ----------------------------------------------------
# Mientras el sello nuevo no sea OK, el base (el último OK o, si no hay ninguno, el más antiguo de la
# cadena) sigue haciendo falta: no se borra.
$proteger = @($nombre)
if ($actual.Estado -ne 'OK' -and $eleccion.Base) { $proteger += $eleccion.Base.Nombre }
$todos = @(Get-ChildItem -LiteralPath $carpetaSellos -File -Filter "sello-$($con.Bd)_*.txt.gz" | Select-Object -ExpandProperty Name |
           Where-Object { Test-NombreSelloDeBd $_ $con.Bd })
$aBorrar = @(Get-SellosABorrar -Nombres $todos -Hoy $inicio -Proteger $proteger)
foreach ($n in $aBorrar) { Remove-Item -LiteralPath (Join-Path $carpetaSellos $n) -Force; "Retención: borrado $n" }

# --- Resultado ------------------------------------------------------------------------------------
$duracion = (Get-Date) - $inicio
$tamanoSello = (Get-Item -LiteralPath $rutaFinal).Length
$resumenFilas = ($actual.Tablas.Values | ForEach-Object { "$($_.Nombre) $($_.Filas) (+$($res.FilasNuevas[$_.Nombre]))" }) -join ', '
""
foreach ($x in $res.Avisos) { Write-Host "AVISO  $x" -ForegroundColor Yellow }
foreach ($f in $res.Fallas) { Write-Host "FALLA  $f" -ForegroundColor Red }
$textoEstado = if ($estado -eq 'FALLA' -and $AceptarCambios) { 'FALLA ACEPTADA' } else { $estado }
try {
    Add-Content -Path $rutaLog -Encoding UTF8 -Value ('{0:yyyy-MM-dd HH:mm}  {1}  {2}  {3}  {4}  duración {5:hh\:mm\:ss}  fallas {6}  avisos {7}{8}' -f $inicio, $textoEstado, $Entorno, $nombre, $resumenFilas, $duracion, $res.Fallas.Count, $res.Avisos.Count, $(if ($AceptarCambios) { "  aceptado: $Motivo" } else { '' }))
} catch { }
"Sello   : $rutaFinal ($('{0:N0}' -f ($tamanoSello / 1KB)) KB, hash $($actual.HashGlobal.Substring(0, 16))...)"
"Filas   : $resumenFilas"
"Duración: $('{0:hh\:mm\:ss}' -f $duracion)"

if ($estado -eq 'FALLA') {
    if ($AceptarCambios) {
        Write-Host "RESULTADO: FALLA ACEPTADA por -AceptarCambios ($Motivo). Este sello queda como base." -ForegroundColor Yellow
        exit 0
    }
    Write-Host "RESULTADO: FALLA. Registros de solo inserción alterados o sin protección. Ver docs\PROCEDIMIENTO_RESPALDO.md (Sello de firmas y auditoría) y herramientas\respaldo\README.md." -ForegroundColor Red
    if (-not [string]::IsNullOrWhiteSpace($ScriptAviso)) {
        try {
            $cuerpo = "Sello de registros de $($con.Bd) ($Entorno), $('{0:yyyy-MM-dd HH:mm}' -f $inicio):`r`n`r`n" + (($res.Fallas | ForEach-Object { "- $_" }) -join "`r`n") + "`r`n`r`nSello: $rutaFinal`r`nBase: $($actual.Anterior)"
            & $ScriptAviso -Asunto "SGV: FALLA en el sello de firmas y auditoría ($($con.Bd))" -Cuerpo $cuerpo
            "Aviso enviado con $ScriptAviso."
        } catch { Write-Host "No se pudo enviar el aviso con ${ScriptAviso}: $($_.Exception.Message)" -ForegroundColor Red }
    } else {
        Write-Host "(Sin -ScriptAviso ni SGV_SCRIPT_AVISO: el aviso queda en esta salida y en $rutaLog.)" -ForegroundColor Yellow
    }
    exit 1
}
Write-Host "RESULTADO: OK. Ninguna fila existente cambió ni falta; triggers de solo inserción activos y sin cambios." -ForegroundColor Green
exit 0
