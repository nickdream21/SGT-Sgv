# Convierte el TSV de importacion en un .xlsx con fechas reales, listo para subir en
# la pestana "Importar Excel" de Registro de Seguimiento.
param([Parameter(Mandatory=$true)][string]$Tsv, [Parameter(Mandatory=$true)][string]$Salida)
$ErrorActionPreference='Stop'
$ci=[System.Globalization.CultureInfo]::InvariantCulture

$lineas=[System.IO.File]::ReadAllLines($Tsv,[System.Text.Encoding]::UTF8)
$filas=@(); foreach($l in $lineas){ if($l.Length -gt 0){ $filas += ,($l -split "`t") } }
$nf=$filas.Count; $nc=$filas[0].Count
$m=New-Object 'object[,]' $nf,$nc
for($i=0;$i -lt $nf;$i++){
  for($j=0;$j -lt $nc;$j++){
    $v = if($j -lt $filas[$i].Count){ $filas[$i][$j] } else { '' }
    if($i -eq 0 -or $v -eq ''){ $m[$i,$j]=$v; continue }
    $d=[datetime]::MinValue
    if([datetime]::TryParseExact($v,'yyyy-MM-dd HH:mm',$ci,[System.Globalization.DateTimeStyles]::None,[ref]$d)){
      $m[$i,$j]=$d
    } else { $m[$i,$j]=$v }
  }
}

$xl=New-Object -ComObject Excel.Application; $xl.Visible=$false; $xl.DisplayAlerts=$false
try{
  $wb=$xl.Workbooks.Add()
  while($wb.Worksheets.Count -gt 1){ $wb.Worksheets.Item($wb.Worksheets.Count).Delete() }
  $ws=$wb.Worksheets.Item(1); $ws.Name='SEGUIMIENTO'
  $ws.Range($ws.Cells(1,1),$ws.Cells($nf,$nc)).Value2=$m
  $h=$ws.Range($ws.Cells(1,1),$ws.Cells(1,$nc))
  $h.Font.Bold=$true; $h.Interior.Color=6572800; $h.Font.Color=16777215
  $h.WrapText=$true; $h.HorizontalAlignment=-4108
  $ws.Rows(1).RowHeight=34
  # Las columnas de fecha llevan formato explicito: el importador lee el valor, pero
  # asi quien revise el archivo ve la hora y no un numero de serie.
  for($j=1;$j -le $nc;$j++){
    if($filas[0][$j-1] -match '^(F\.H|AUTORIZACION)'){
      $ws.Range($ws.Cells(2,$j),$ws.Cells($nf,$j)).NumberFormat='dd/mm/yyyy hh:mm'
      $ws.Columns($j).ColumnWidth=17
    }
  }
  $ws.Activate(); $ws.Range('A2').Select() | Out-Null
  $xl.ActiveWindow.FreezePanes=$true
  $ws.Range($ws.Cells(1,1),$ws.Cells($nf,$nc)).AutoFilter() | Out-Null
  if(Test-Path $Salida){ Remove-Item $Salida -Force }
  $wb.SaveAs($Salida,51)
  "OK -> $Salida  ($($nf-1) filas)"
  $wb.Close($false)
} finally { $xl.Quit(); [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($xl); [GC]::Collect() }
