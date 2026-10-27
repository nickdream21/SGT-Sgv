<#
    Paradas-Placa.ps1 — paradas de un tracto (30+ min dentro de 300 m) con el lugar conocido o la dirección
    de Onway. Sirve para aprender una ruta nueva (afrecho: docs/RUTA_AFRECHO.md). Horas en hora Perú.

    .\Paradas-Placa.ps1 -Placa BPT-919 -Desde "2026-01-10" -Hasta "2026-01-14" -MinutosParada 30
#>
param([string]$Placa, [DateTime]$Desde, [DateTime]$Hasta, [int]$MinutosParada = 25)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "OnwayCliente.ps1")
$script:PageSize = 1000
$lugares = @(
 @('Alicorp Paita (afrecho)',-5.0860,-81.0945,500), @('Base Sullana',-4.9563,-80.6990,400), @('TCI',-3.5365,-80.1847,350), @('TCI control aduana',-3.5324,-80.1780,250),
 @('Complex',-3.4878,-80.2610,300), @('CEBAF',-3.5115,-80.2497,350), @('Cruce Aguas Verdes',-3.4967,-80.2133,300),
 @('Jave',-2.1760,-79.7970,500), @('Inbalnor Milagro',-2.2150,-79.6345,500), @('La Troncal (Crisar)',-2.44208,-79.35272,500),
 @('Parqueo antes de La Troncal',-2.77225,-79.69339,300), @('Chapaira Polinplast',-5.03606,-80.58935,300),
 @('Novopan Piura',-5.11323,-80.70448,300), @('Cochera Trujillo',-8.13855,-79.01755,200), @('Vitapro Trujillo',-8.1440,-79.0170,400))
function Dist($a,$b,$c,$d){ $r=[Math]::PI/180; $x=[Math]::Sin(($c-$a)*$r/2); $y=[Math]::Sin(($d-$b)*$r/2); $h=$x*$x+[Math]::Cos($a*$r)*[Math]::Cos($c*$r)*$y*$y; 2*6371000*[Math]::Asin([Math]::Sqrt($h)) }
function Lugar($lat,$lng){ foreach($l in $lugares){ if((Dist $lat $lng $l[1] $l[2]) -le $l[3]){ return $l[0] } }; return ("?? {0:N5},{1:N5}" -f $lat,$lng) }
function Hora($x){ $s = if($x.generateTime){[string]$x.generateTime}else{[string]$x.messageTime}; ConvertFrom-Utc ([DateTimeOffset]::Parse($s, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AssumeUniversal).UtcDateTime) }
$con = Connect-Onway
$disp = Get-DispositivoPorPlaca $con $Placa
if (-not $disp) { Write-Output "SIN EQUIPO $Placa"; return }
$pts = Get-Historial $con $disp.id (ConvertTo-Utc $Desde) (ConvertTo-Utc $Hasta) 6>$null
$pts = @($pts | Where-Object { $_.lat -and $_.lng } | Sort-Object { Hora $_ })
Write-Output ("### {0}  {1:dd/MM HH:mm} -> {2:dd/MM HH:mm}  puntos={3}" -f $Placa,$Desde,$Hasta,$pts.Count)
if ($pts.Count -eq 0) { return }
$i=0
while ($i -lt $pts.Count) {
  $p=$pts[$i]; $j=$i
  while ($j+1 -lt $pts.Count -and (Dist $p.lat $p.lng $pts[$j+1].lat $pts[$j+1].lng) -le 300) { $j++ }
  $t0=(Hora $p); $q=$pts[$j]
  $t1=(Hora $q)
  if (($t1-$t0).TotalMinutes -ge $MinutosParada) {
    Write-Output ("  {0:dd/MM HH:mm} - {1:dd/MM HH:mm}  {2,6:N1} h  {3}" -f $t0,$t1,($t1-$t0).TotalHours,((Lugar $p.lat $p.lng) + "  [" + (@($pts[$i..$j] | Where-Object { $_.address } | Group-Object address | Sort-Object Count -Descending | Select-Object -First 1).Name) + "]"))
  }
  $i=$j+1
}
$u=$pts[-1]; Write-Output ("  ultimo punto {0:dd/MM HH:mm} {1}" -f ((Hora $u)),(Lugar $u.lat $u.lng))
