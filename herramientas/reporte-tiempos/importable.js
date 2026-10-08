// Genera el TSV con los viajes 2026 en el formato que espera el importador de
// Views/Exportacion/RegistroSeguimiento.aspx (pestana "Importar Excel").
// Un hito por columna, con fecha y hora juntas. La llegada a base va ya corregida.
const fs=require('fs');
const {fecha}=require('./lib.js');
const M=require(process.argv[2]);
const OUT=process.argv[3];

const CAB=['CLIENTE','CONDUCTOR ORIGEN','TRACTO 1','CARRETA','CONDUCTOR DESTINO','TRACTO 2',
 'F.H.S.BASE:','F.H.LL. TRUJILLO','F.H.REGISTRO','F.H. PROGRAMACION','F.H.I PLANTA',
 'F.H.INICIO DE CARGA','F.H.TERMINO CARGA','F.H.S PLANTA','F.H.LL. BASE','F.H.S BASE',
 'F.H.LL.BODEGA NACIONAL','F.H.I. BODEGA NACIONAL','F.H.S.BODEGA NACIONAL','BODEGA',
 'F.H.LL CEBAF','F.H CRUCE','AUTORIZACION DE LA NACIONALIZACION','BODEGA ECUATORIANA',
 'F.H.LL.TCI','F.H.S TCI','BODEGA DESCARGA','F.H.LL.PLANTA','F.H.LL.ALMACEN','F.H.INGRESO',
 'F.H.I. DESCARGA','F.H.T. DESCARGA','F.H.SALIDA','MOTIVO DE RETRASO'];

const HITO=['sBase1','llTrujillo','registro','programacion','iPlanta','iniCarga','termCarga',
 'sPlanta','llBase','sBase2','llBodNac','iBodNac','sBodNac',null,'llCebaf','cruce','autoriz',
 null,'llTci','sTci',null,'llPlanta','llAlmacen','ingreso','iDescarga','tDescarga','salida'];

function fmt(serial){
  if(serial===null||serial===undefined) return '';
  const d=fecha(serial), p=x=>String(x).padStart(2,'0');
  return d.getUTCFullYear()+'-'+p(d.getUTCMonth()+1)+'-'+p(d.getUTCDate())+' '+
         p(d.getUTCHours())+':'+p(d.getUTCMinutes());
}
function pedido(s){ const n=Number(s); return isFinite(n)&&Math.abs(n)>1e6?n.toFixed(0):String(s||''); }

const filas=[CAB];
for(const v of M.viajes){
  const h=v.h;
  filas.push([
    pedido(v.ref), v.condOrigen, v.tracto1, v.carreta, v.condDestino, v.tracto2,
    fmt(h.sBase1), fmt(h.llTrujillo), fmt(h.registro), fmt(h.programacion), fmt(h.iPlanta),
    fmt(h.iniCarga), fmt(h.termCarga), fmt(h.sPlanta), fmt(h.llBase), fmt(h.sBase2),
    fmt(h.llBodNac), fmt(h.iBodNac), fmt(h.sBodNac), v.bodNac,
    fmt(h.llCebaf), fmt(h.cruce), fmt(h.autoriz), v.bodEcu,
    fmt(h.llTci), fmt(h.sTci), v.destino, fmt(h.llPlanta), fmt(h.llAlmacen), fmt(h.ingreso),
    fmt(h.iDescarga), fmt(h.tDescarga), fmt(h.salida),
    (v.motivo||'').replace(/[\t\r\n]+/g,' ').slice(0,1000)
  ]);
}
fs.writeFileSync(OUT, filas.map(r=>r.join('\t')).join('\n'),'utf8');
console.log('Filas para importar: '+(filas.length-1)+' (columnas: '+CAB.length+')');
