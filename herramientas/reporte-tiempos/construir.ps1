# =============================================================================
#  Construye el libro "Tiempos por tramo - Exportacion 2026" con Excel COM.
#  Entrada : los .tsv que genera export.js
#  Salida  : un .xlsx con hoja de portada, resumen de tramos, series por mes y
#            destino, tabla dinamica con segmentadores, datos crudos y metodologia.
# =============================================================================
param(
  [Parameter(Mandatory=$true)][string]$Tsv,
  [Parameter(Mandatory=$true)][string]$Salida
)
$ErrorActionPreference = 'Stop'
$ci = [System.Globalization.CultureInfo]::InvariantCulture

# --- Paleta (Excel usa BGR, no RGB) ------------------------------------------
$C_HDR    = 6572800    # azul corporativo oscuro
$C_BANDA  = 15921906   # gris muy claro
$C_ACENTO = 1937910    # naranja/ambar (BGR)
$C_VERDE  = 5296274
$C_ROJO   = 3487212
$C_GRIS   = 8421504
$C_BLANCO = 16777215

function Leer-Tsv([string]$ruta) {
  $lineas = [System.IO.File]::ReadAllLines($ruta, [System.Text.Encoding]::UTF8)
  $out = @()
  foreach ($l in $lineas) { if ($l.Length -gt 0) { $out += ,($l -split "`t") } }
  return ,$out
}

# Convierte la matriz de texto en object[,] tipando los numeros, para que las
# tablas dinamicas y los graficos los tomen como valores y no como texto.
function A-Matriz($filas) {
  $nf = $filas.Count
  $nc = 0
  foreach ($f in $filas) { if ($f.Count -gt $nc) { $nc = $f.Count } }
  $m = New-Object 'object[,]' $nf, $nc
  for ($i = 0; $i -lt $nf; $i++) {
    for ($j = 0; $j -lt $nc; $j++) {
      $v = if ($j -lt $filas[$i].Count) { $filas[$i][$j] } else { '' }
      if ($i -eq 0 -or $v -eq '') { $m[$i, $j] = $v; continue }
      $d = 0.0
      if ([double]::TryParse($v, [System.Globalization.NumberStyles]::Float, $ci, [ref]$d)) {
        # Los codigos de pedido son identificadores, no cantidades: se dejan como texto.
        if ($filas[0][$j] -eq 'Pedido') { $m[$i, $j] = $v } else { $m[$i, $j] = $d }
      } else { $m[$i, $j] = $v }
    }
  }
  return ,$m
}

function Volcar($ws, $filas, [int]$fila0, [int]$col0) {
  $m = A-Matriz $filas
  $nf = $m.GetLength(0); $nc = $m.GetLength(1)
  $rng = $ws.Range($ws.Cells($fila0, $col0), $ws.Cells($fila0 + $nf - 1, $col0 + $nc - 1))
  $rng.Value2 = $m
  return $rng
}

function Cabecera($ws, [int]$fila, [int]$c1, [int]$c2) {
  $h = $ws.Range($ws.Cells($fila, $c1), $ws.Cells($fila, $c2))
  $h.Interior.Color = $C_HDR
  $h.Font.Color = $C_BLANCO
  $h.Font.Bold = $true
  $h.HorizontalAlignment = -4108
  $h.VerticalAlignment = -4108
  $h.WrapText = $true
  $ws.Rows($fila).RowHeight = 30
}

# SetSourceData mas SeriesCollection().Add termina interpretando las etiquetas como una
# serie mas y numerando el eje 1,2,3... Construir cada serie a mano con Values y XValues
# explicitos es la unica forma fiable de que el eje muestre los nombres de los tramos.
function Grafico($ws, $x, $y, $w, $h, [int]$tipo, [string]$titulo, $cat, $series, [bool]$leyenda, [bool]$etiquetas, [bool]$reverso = $false) {
  $co = $ws.ChartObjects().Add($x, $y, $w, $h)
  $ch = $co.Chart
  while ($ch.SeriesCollection().Count -gt 0) { $ch.SeriesCollection(1).Delete() }
  foreach ($s in $series) {
    $sr = $ch.SeriesCollection().NewSeries()
    $sr.Values  = $s.Rango
    $sr.XValues = $cat
    $sr.Name    = $s.Nombre
    if ($s.Color) { $sr.Format.Fill.ForeColor.RGB = $s.Color; $sr.Format.Line.ForeColor.RGB = $s.Color }
    if ($etiquetas) { $sr.HasDataLabels = $true; $sr.DataLabels().NumberFormat = '0.00'; $sr.DataLabels().Font.Size = 9 }
  }
  $ch.ChartType = $tipo
  $ch.HasTitle = $true
  $ch.ChartTitle.Text = $titulo
  $ch.ChartTitle.Font.Size = 13
  $ch.HasLegend = $leyenda
  if ($leyenda) { $ch.Legend.Position = -4107 }   # abajo
  $ch.Axes(1).TickLabels.Font.Size = 9
  $ch.Axes(2).TickLabels.Font.Size = 9
  # En un grafico de barras Excel pone la primera categoria abajo; invertirlo deja los
  # tramos en el mismo orden en que ocurren en el viaje.
  if ($reverso) { $ch.Axes(1).ReversePlotOrder = $true; $ch.Axes(2).Crosses = -4114 }
  return $co
}

function Titulo($ws, [int]$fila, [string]$texto, [string]$sub) {
  $ws.Cells($fila, 1).Value2 = $texto
  $ws.Cells($fila, 1).Font.Size = 16
  $ws.Cells($fila, 1).Font.Bold = $true
  $ws.Cells($fila, 1).Font.Color = $C_HDR
  if ($sub) {
    $ws.Cells($fila + 1, 1).Value2 = $sub
    $ws.Cells($fila + 1, 1).Font.Size = 10
    $ws.Cells($fila + 1, 1).Font.Color = $C_GRIS
    $ws.Cells($fila + 1, 1).Font.Italic = $true
  }
}

Write-Host "Leyendo TSV..."
$dDatos  = Leer-Tsv (Join-Path $Tsv 'datos.tsv')
$dResum  = Leer-Tsv (Join-Path $Tsv 'resumen.tsv')
$dCiclos = Leer-Tsv (Join-Path $Tsv 'ciclos.tsv')
$dMes    = Leer-Tsv (Join-Path $Tsv 'pormes.tsv')
$dDest   = Leer-Tsv (Join-Path $Tsv 'pordestino.tsv')
$dKpi    = Leer-Tsv (Join-Path $Tsv 'kpi.tsv')

Write-Host "Abriendo Excel..."
$xl = New-Object -ComObject Excel.Application
$xl.Visible = $false
$xl.DisplayAlerts = $false
$xl.ScreenUpdating = $false

try {
  $wb = $xl.Workbooks.Add()
  while ($wb.Worksheets.Count -gt 1) { $wb.Worksheets.Item($wb.Worksheets.Count).Delete() }

  # ===========================================================================
  # 6. DATOS  (se crea primero porque las demas hojas lo referencian)
  # ===========================================================================
  $wsDat = $wb.Worksheets.Item(1)
  $wsDat.Name = 'Datos 2026'
  $rDat = Volcar $wsDat $dDatos 1 1
  $nFil = $dDatos.Count
  $nCol = $dDatos[0].Count
  Cabecera $wsDat 1 1 $nCol
  $wsDat.Range($wsDat.Cells(2,13), $wsDat.Cells($nFil,$nCol-1)).NumberFormat = '0.00'
  $wsDat.Range($wsDat.Cells(1,1), $wsDat.Cells($nFil,$nCol)).AutoFilter() | Out-Null
  $wsDat.Activate()
  $xl.ActiveWindow.FreezePanes = $false
  $wsDat.Range('C2').Select() | Out-Null
  $xl.ActiveWindow.FreezePanes = $true
  $wsDat.Columns.AutoFit() | Out-Null
  for ($c = 13; $c -le $nCol - 1; $c++) { $wsDat.Columns($c).ColumnWidth = 9 }
  $wsDat.Columns($nCol).ColumnWidth = 45
  $wsDat.Tab.Color = $C_GRIS

  $refDatos = "'Datos 2026'!" + '$A$1:$' + [char](64 + [math]::Min($nCol,26)) + '$' + $nFil

  # ===========================================================================
  # 2. TRAMOS
  # ===========================================================================
  $wsTr = $wb.Worksheets.Add([System.Reflection.Missing]::Value, $wsDat)
  $wsTr.Name = 'Tramos'
  Titulo $wsTr 1 'Tiempo por tramo, punto por punto' 'Dias calendario entre un hito y el siguiente. Fuente: hoja Seguimiento EXPORTACION, viajes programados en 2026.'
  $rTr = Volcar $wsTr $dResum 4 1
  $nTr = $dResum.Count + 3
  Cabecera $wsTr 4 1 $dResum[0].Count
  $wsTr.Range($wsTr.Cells(5,7), $wsTr.Cells($nTr,8)).NumberFormat = '0.00'
  $wsTr.Range($wsTr.Cells(5,9), $wsTr.Cells($nTr,9)).NumberFormat = '0.0'
  $wsTr.Range($wsTr.Cells(5,10), $wsTr.Cells($nTr,12)).NumberFormat = '0.00'
  $wsTr.Columns.AutoFit() | Out-Null
  $wsTr.Columns(3).ColumnWidth = 28
  $wsTr.Columns(4).ColumnWidth = 18
  $wsTr.Columns(5).ColumnWidth = 46
  $wsTr.Columns(13).ColumnWidth = 12
  # Barras de datos sobre el promedio: se ve de un vistazo donde se va el tiempo
  $db = $wsTr.Range($wsTr.Cells(5,7), $wsTr.Cells($nTr,7)).FormatConditions.AddDatabar()
  $db.BarColor.Color = $C_ACENTO
  $wsTr.Tab.Color = $C_HDR

  Grafico $wsTr 30 (($nTr + 3) * 15 + 40) 980 540 57 'Promedio de dias por tramo, en el orden del viaje' `
    $wsTr.Range($wsTr.Cells(5,3), $wsTr.Cells($nTr,3)) `
    @(@{Rango=$wsTr.Range($wsTr.Cells(5,7), $wsTr.Cells($nTr,7)); Nombre='Promedio (dias)'; Color=$C_HDR}) `
    $false $true $true | Out-Null

  # ===========================================================================
  # 3. CICLOS
  # ===========================================================================
  $wsCi = $wb.Worksheets.Add([System.Reflection.Missing]::Value, $wsTr)
  $wsCi.Name = 'Ciclos'
  Titulo $wsCi 1 'Ciclos agregados' 'Lo que pidio la jefatura: cuantos dias toma cada recorrido de punta a punta.'
  Volcar $wsCi $dCiclos 4 1 | Out-Null
  $nCi = $dCiclos.Count + 3
  Cabecera $wsCi 4 1 $dCiclos[0].Count
  $wsCi.Range($wsCi.Cells(5,5), $wsCi.Cells($nCi,8)).NumberFormat = '0.00'
  $wsCi.Columns.AutoFit() | Out-Null
  $wsCi.Columns(2).ColumnWidth = 30
  $wsCi.Columns(3).ColumnWidth = 62
  $wsCi.Columns(9).ColumnWidth = 30
  $wsCi.Tab.Color = $C_HDR

  Grafico $wsCi 30 (($nCi + 3) * 15 + 40) 880 380 57 'Dias por ciclo: promedio y mediana' `
    $wsCi.Range($wsCi.Cells(5,2), $wsCi.Cells($nCi,2)) `
    @(@{Rango=$wsCi.Range($wsCi.Cells(5,5), $wsCi.Cells($nCi,5)); Nombre='Promedio'; Color=$C_HDR},
      @{Rango=$wsCi.Range($wsCi.Cells(5,6), $wsCi.Cells($nCi,6)); Nombre='Mediana';  Color=$C_ACENTO}) `
    $true $true $true | Out-Null

  # ===========================================================================
  # 4. POR MES
  # ===========================================================================
  $wsMe = $wb.Worksheets.Add([System.Reflection.Missing]::Value, $wsCi)
  $wsMe.Name = 'Por mes'
  Titulo $wsMe 1 'Evolucion mensual 2026' 'Mediana de dias por mes de programacion. Las celdas vacias son meses sin el hito necesario en la fuente.'
  Volcar $wsMe $dMes 4 1 | Out-Null
  $nMe = $dMes.Count + 3
  Cabecera $wsMe 4 1 $dMes[0].Count
  $wsMe.Range($wsMe.Cells(5,3), $wsMe.Cells($nMe,8)).NumberFormat = '0.00'
  $wsMe.Columns.AutoFit() | Out-Null
  $wsMe.Tab.Color = $C_HDR

  $catMes = $wsMe.Range($wsMe.Cells(5,1), $wsMe.Cells($nMe,1))
  Grafico $wsMe 30 (($nMe + 3) * 16 + 40) 900 380 65 'Dias por mes' $catMes `
    @(@{Rango=$wsMe.Range($wsMe.Cells(5,3), $wsMe.Cells($nMe,3)); Nombre='Periodo nacional';        Color=$C_HDR},
      @{Rango=$wsMe.Range($wsMe.Cells(5,4), $wsMe.Cells($nMe,4)); Nombre='Base a Guayaquil';        Color=$C_ACENTO},
      @{Rango=$wsMe.Range($wsMe.Cells(5,7), $wsMe.Cells($nMe,7)); Nombre='Base a Guayaquil y vuelta'; Color=$C_VERDE}) `
    $true $false | Out-Null

  Grafico $wsMe 30 (($nMe + 3) * 16 + 440) 900 300 51 'Viajes programados por mes' $catMes `
    @(@{Rango=$wsMe.Range($wsMe.Cells(5,2), $wsMe.Cells($nMe,2)); Nombre='Viajes'; Color=$C_ACENTO}) `
    $false $true | Out-Null

  # ===========================================================================
  # 5. POR DESTINO
  # ===========================================================================
  $wsDe = $wb.Worksheets.Add([System.Reflection.Missing]::Value, $wsMe)
  $wsDe.Name = 'Por destino'
  Titulo $wsDe 1 'Comparativo por planta de destino en Ecuador' 'Promedio de dias. Solo los viajes que tienen registrada la bodega de descarga.'
  Volcar $wsDe $dDest 4 1 | Out-Null
  $nDe = $dDest.Count + 3
  Cabecera $wsDe 4 1 $dDest[0].Count
  $wsDe.Range($wsDe.Cells(5,3), $wsDe.Cells($nDe,7)).NumberFormat = '0.00'
  $wsDe.Columns.AutoFit() | Out-Null
  $wsDe.Tab.Color = $C_HDR

  Grafico $wsDe 30 (($nDe + 3) * 16 + 60) 800 340 51 'Dias promedio por planta de destino' `
    $wsDe.Range($wsDe.Cells(5,1), $wsDe.Cells($nDe,1)) `
    @(@{Rango=$wsDe.Range($wsDe.Cells(5,3), $wsDe.Cells($nDe,3)); Nombre='Periodo nacional';          Color=$C_HDR},
      @{Rango=$wsDe.Range($wsDe.Cells(5,4), $wsDe.Cells($nDe,4)); Nombre='Base a Guayaquil';          Color=$C_ACENTO},
      @{Rango=$wsDe.Range($wsDe.Cells(5,7), $wsDe.Cells($nDe,7)); Nombre='Base a Guayaquil y vuelta'; Color=$C_VERDE}) `
    $true $true | Out-Null

  # ===========================================================================
  # 7. ANALISIS DINAMICO (tabla dinamica + segmentadores)
  # ===========================================================================
  $wsPv = $wb.Worksheets.Add([System.Reflection.Missing]::Value, $wsDe)
  $wsPv.Name = 'Analisis dinamico'
  Titulo $wsPv 1 'Analisis dinamico' 'Filtra con los botones de la derecha. La tabla recalcula sola: arrastra otros campos desde la lista de campos.'
  try {
    $src = $wsDat.Range($wsDat.Cells(1,1), $wsDat.Cells($nFil,$nCol))
    $cache = $wb.PivotCaches().Create(1, $src, 6)
    $pt = $cache.CreatePivotTable($wsPv.Range('A5'), 'ptTiempos')
    $pt.PivotFields('Mes').Orientation = 1          # fila
    $pt.PivotFields('Mes').Position = 1
    foreach ($campo in @(
        'C1 PERIODO NACIONAL (Base -> Trujillo -> Base)',
        'C2 BASE -> GUAYAQUIL (hasta llegar a planta destino)',
        'C5 Base -> Guayaquil -> Base (con retorno estimado)',
        'C6 Ciclo total (con retorno estimado)')) {
      try {
        $df = $pt.AddDataField($pt.PivotFields($campo), 'Prom ' + $campo.Substring(0,2), -4106) # promedio
        $df.NumberFormat = '0.00'
      } catch { Write-Host "  (campo omitido en la dinamica: $campo)" }
    }
    $pt.TableStyle2 = 'PivotStyleMedium2'
    foreach ($sl in @('Planta destino','Calidad del dato','Mes')) {
      try {
        $sc = $wb.SlicerCaches.Add2($pt, $sl)
        $i = [array]::IndexOf(@('Planta destino','Calidad del dato','Mes'), $sl)
        $sc.Slicers.Add($wsPv, [System.Reflection.Missing]::Value, $sl, $sl, 80, (600 + $i * 200), 180, 200) | Out-Null
      } catch { Write-Host "  (segmentador omitido: $sl)" }
    }
    $wsPv.Columns.AutoFit() | Out-Null
  } catch {
    $wsPv.Range('A5').Value2 = 'No se pudo crear la tabla dinamica: ' + $_.Exception.Message
    Write-Host "  AVISO tabla dinamica: $($_.Exception.Message)"
  }
  $wsPv.Tab.Color = $C_ACENTO

  # ===========================================================================
  # 1. PORTADA / DASHBOARD
  # ===========================================================================
  $wsDb = $wb.Worksheets.Add($wb.Worksheets.Item(1))
  $wsDb.Name = 'Dashboard'
  $wsDb.Cells.Interior.Color = $C_BLANCO
  Titulo $wsDb 2 'Tiempos por tramo - Exportacion Peru / Ecuador' 'Viajes programados en 2026. Elaborado a partir de STATUS GENERAL VIVIANA (hoja Seguimiento EXPORTACION).'
  $wsDb.Cells(4,1).Value2 = 'Generado el ' + (Get-Date -Format 'dd/MM/yyyy HH:mm')
  $wsDb.Cells(4,1).Font.Color = $C_GRIS
  $wsDb.Cells(4,1).Font.Size = 9

  # Tarjetas de KPI
  $fila = 6
  for ($i = 1; $i -lt $dKpi.Count; $i++) {
    $f = $fila + ($i - 1) * 3
    $wsDb.Cells($f, 1).Value2 = $dKpi[$i][0]
    $wsDb.Cells($f, 1).Font.Bold = $true
    $wsDb.Cells($f, 1).Font.Size = 11
    $v = 0.0
    if ([double]::TryParse($dKpi[$i][1], [System.Globalization.NumberStyles]::Float, $ci, [ref]$v)) {
      $wsDb.Cells($f, 4).Value2 = $v
      $wsDb.Cells($f, 4).NumberFormat = if ($i -eq 1) { '#,##0' } else { '0.00' }
    } else { $wsDb.Cells($f, 4).Value2 = $dKpi[$i][1] }
    $wsDb.Cells($f, 4).Font.Size = 22
    $wsDb.Cells($f, 4).Font.Bold = $true
    $wsDb.Cells($f, 4).Font.Color = if ($i -eq 1) { $C_GRIS } else { $C_HDR }
    $wsDb.Cells($f, 4).HorizontalAlignment = -4152
    $wsDb.Cells($f + 1, 1).Value2 = $dKpi[$i][2]
    $wsDb.Cells($f + 1, 1).Font.Size = 9
    $wsDb.Cells($f + 1, 1).Font.Color = $C_GRIS
    $borde = $wsDb.Range($wsDb.Cells($f, 1), $wsDb.Cells($f + 1, 4))
    $borde.Borders.Item(9).Color = $C_BANDA   # borde inferior
    $borde.Borders.Item(9).Weight = 2
  }
  $wsDb.Columns(1).ColumnWidth = 46
  $wsDb.Columns(2).ColumnWidth = 6
  $wsDb.Columns(3).ColumnWidth = 6
  $wsDb.Columns(4).ColumnWidth = 14

  Grafico $wsDb 400 90 640 330 57 'Dias promedio por ciclo' `
    $wsCi.Range($wsCi.Cells(5,2), $wsCi.Cells($nCi,2)) `
    @(@{Rango=$wsCi.Range($wsCi.Cells(5,5), $wsCi.Cells($nCi,5)); Nombre='Promedio (dias)'; Color=$C_HDR}) `
    $false $true $true | Out-Null

  Grafico $wsDb 400 430 640 320 65 'Evolucion mensual (dias)' `
    $wsMe.Range($wsMe.Cells(5,1), $wsMe.Cells($nMe,1)) `
    @(@{Rango=$wsMe.Range($wsMe.Cells(5,3), $wsMe.Cells($nMe,3)); Nombre='Periodo nacional';          Color=$C_HDR},
      @{Rango=$wsMe.Range($wsMe.Cells(5,4), $wsMe.Cells($nMe,4)); Nombre='Base a Guayaquil';          Color=$C_ACENTO},
      @{Rango=$wsMe.Range($wsMe.Cells(5,7), $wsMe.Cells($nMe,7)); Nombre='Base a Guayaquil y vuelta'; Color=$C_VERDE}) `
    $true $false | Out-Null

  Grafico $wsDb 30 770 1010 520 57 'Donde se va el tiempo: dias promedio por tramo, en el orden del viaje' `
    $wsTr.Range($wsTr.Cells(5,3), $wsTr.Cells($nTr,3)) `
    @(@{Rango=$wsTr.Range($wsTr.Cells(5,7), $wsTr.Cells($nTr,7)); Nombre='Promedio (dias)'; Color=$C_ACENTO}) `
    $false $true $true | Out-Null

  $wsDb.Tab.Color = $C_ACENTO

  # ===========================================================================
  # 8. METODOLOGIA
  # ===========================================================================
  $wsMt = $wb.Worksheets.Add([System.Reflection.Missing]::Value, $wb.Worksheets.Item($wb.Worksheets.Count))
  $wsMt.Name = 'Metodologia'
  Titulo $wsMt 1 'Como se calculo esto y que hay que mirar con cuidado' ''
  $texto = @(
    '',
    'FUENTE',
    'Archivo STATUS GENERAL VIVIANA dashboard reunion.xlsx, hoja "Seguimiento EXPORTACION". Se tomaron los 1726 viajes cuya F.H. PROGRAMACION cae en 2026 (enero a setiembre).',
    'Cada hito ocupa dos celdas en la fuente: una con la fecha y la siguiente con la hora. Aqui se unieron en un solo instante.',
    '',
    'CORRECCION 1 - La llegada a base despues de Trujillo',
    'La columna F.H.LL. Base tiene la HORA correcta pero la FECHA no: sobre los 648 viajes que permiten contrastarla, en 318 quedaba antes de la salida de planta y en 105 despues de la salida de base. Las dos cosas son imposibles.',
    'Se reconstruyo el hito como el primer instante con esa hora que ocurre despues de F.H.S Planta. Con esa regla solo 1 viaje de 648 sigue siendo incoherente, contra 318 antes.',
    'Se aplico a 462 viajes. El resultado (Trujillo a base en 0.47 dias de mediana, cargado) es consistente con la ida (0.39 dias, en vacio).',
    '',
    'CORRECCION 2 - Duraciones imposibles',
    'Se descarto toda duracion negativa y toda duracion por encima del tope fisico de cada tramo (por ejemplo 3 dias para un transito Base-Trujillo). Los descartes se ven como celdas vacias en la hoja Datos.',
    '',
    'LIMITE 1 - El retorno a base NO ESTA en la fuente',
    'La columna F.H.LL Base del retorno esta vacia en el 99.8% de los viajes: solo 3 viajes de 1726 la tienen. Por eso el retorno Guayaquil-Base es una ESTIMACION, no una medicion.',
    'Se estimo por tres caminos independientes que coinciden:',
    '   a) Los 3 viajes que si tienen el dato: 0.84 dias de mediana.',
    '   b) Rotacion de flota: tiempo entre la salida de Guayaquil de un tracto y el arranque de su siguiente viaje. El percentil 10 (cuando la unidad se redespacha sin descanso) da 1.06 dias.',
    '   c) Suma de los transitos de la ida sin los tiempos de tramite, que en vacio no aplican: 0.92 dias.',
    'Valor adoptado: 0.94 dias. Todo indicador que lo use esta marcado como ESTIMADO.',
    '',
    'LIMITE 2 - Cobertura desigual a lo largo del ano',
    'De enero a junio estan los hitos de Peru (salida de base, Trujillo, retorno a base, salida a Ecuador) pero varios hitos intermedios se cargaron solo con fecha, sin hora.',
    'En julio y agosto pasa lo contrario: el tramo internacional esta completo y con hora real, pero los cuatro hitos de Peru estan vacios. Por eso el periodo nacional no se puede calcular en esos dos meses.',
    'Mayo y junio son los unicos meses con todo cargado y con hora real: son los mas confiables para leer el ciclo completo.',
    'Setiembre tiene solo 22 viajes cargados, el mes esta en curso.',
    '',
    'LIMITE 3 - Hitos cargados solo con fecha',
    'En febrero y abril practicamente todos los hitos intermedios (ingreso a planta, carga, bodega nacional, CEBAF, TCI, descarga) tienen hora 00:00, es decir se cargo solo el dia.',
    'Esos tramos siguen sirviendo para contar dias, pero no para medir horas. En marzo pasa en el 65% de los casos.',
    '',
    'COMO LEER LOS PROMEDIOS',
    'Se publica el promedio y tambien la mediana. Cuando difieren mucho (por ejemplo Base-Guayaquil: 1.65 de promedio contra 1.20 de mediana) es que unos pocos viajes muy largos estiran el promedio.',
    'Para costeo conviene el promedio, porque es el que multiplica. Para programar la operacion conviene la mediana, porque es el viaje tipico. El P90 dice cuanto dura el 10% peor.',
    '',
    'COHERENCIA DEL MODELO',
    'La suma de los tramos individuales cuadra con los ciclos medidos de punta a punta. En medianas: el periodo nacional da 0.95 dias sumando T01 a T06 contra 1.00 medido; Base-Guayaquil da 1.07 sumando T08 a T14 contra 1.20 medido. La diferencia es la espera que no queda atribuida a ningun hito.',
    '',
    'QUE HARIA FALTA PARA CERRAR EL CIRCULO',
    'Registrar la llegada a base del retorno. Es un solo campo y convierte la estimacion en medicion.',
    'Cargar la hora y no solo la fecha en los hitos intermedios.',
    'Volver a llenar los cuatro hitos de Peru que se dejaron de cargar en julio.'
  )
  for ($i = 0; $i -lt $texto.Count; $i++) {
    $c = $wsMt.Cells($i + 3, 1)
    $c.Value2 = $texto[$i]
    if ($texto[$i] -cmatch '^[A-Z0-9 \-]+$' -and $texto[$i].Length -gt 3) {
      $c.Font.Bold = $true; $c.Font.Color = $C_HDR
    }
  }
  $wsMt.Columns(1).ColumnWidth = 150
  $wsMt.Columns(1).WrapText = $false
  $wsMt.Tab.Color = $C_GRIS

  # Orden de pestanas: primero lo que se muestra en la reunion, al final el detalle
  $orden = 'Dashboard','Ciclos','Tramos','Por mes','Por destino','Analisis dinamico','Datos 2026','Metodologia'
  for ($i = 1; $i -lt $orden.Count; $i++) {
    $wb.Worksheets.Item($orden[$i]).Move([System.Reflection.Missing]::Value, $wb.Worksheets.Item($orden[$i - 1]))
  }

  $wb.Worksheets.Item('Dashboard').Activate()
  $xl.ActiveWindow.DisplayGridlines = $false

  if (Test-Path $Salida) { Remove-Item $Salida -Force }
  $wb.SaveAs($Salida, 51)
  Write-Host "OK -> $Salida"
  $wb.Close($false)
}
finally {
  $xl.Quit()
  [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($xl)
  [GC]::Collect(); [GC]::WaitForPendingFinalizers()
}
