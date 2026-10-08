// =============================================================================
// Modelo de tiempos por tramo - Seguimiento EXPORTACION 2026
// Fuente: "STATUS GENERAL VIVIANA dashboard reunion.xlsx", hoja "Seguimiento EXPORTACION"
//
// Correcciones aplicadas sobre el dato crudo (documentadas en la hoja Metodologia):
//  1. F.H.LL. Base: la celda de FECHA no es coherente (queda antes de la salida de
//     planta o despues de la salida de base). La celda de HORA si lo es. Se reconstruye
//     el hito como el primer instante con esa hora que ocurre despues de F.H.S Planta.
//  2. Se descartan duraciones negativas y las que superan el tope fisico de cada tramo.
//  3. Los hitos cuya hora quedo en 00:00 (carga manual solo a nivel dia) se marcan y se
//     excluyen de los tramos medidos en horas.
// =============================================================================
const fs=require('fs');
const path=require('path');
const {cargar,fecha,iso}=require('./lib.js');

const BASE=process.argv[2];
const OUT=process.argv[3];
const rows=cargar(BASE,'sheet1.xml');

const C={cliente:1,condOrigen:2,tracto1:3,carreta:4,condDestino:5,tracto2:6,
  sBase1:7,llTrujillo:9,registro:11,programacion:13,iPlanta:15,iniCarga:17,termCarga:19,
  sPlanta:21,llBaseRaw:23,sBase2:25,llBodNac:27,iBodNac:29,sBodNac:31,bodega:33,
  llCebaf:34,cruce:36,autoriz:38,bodEcu:40,llTci:41,sTci:43,bodDesc:45,llPlanta:46,
  llAlmacen:48,ingreso:50,iDescarga:52,tDescarga:54,salida:56,llBaseRet:58,motivo:62};

function num(v){ if(v===undefined) return null; const n=Number(v); return isFinite(n)?n:null; }
function dtc(r,c){ const f=num(r[c]); if(f===null||f<20000) return null; const h=num(r[c+1]); return f+(h===null?0:h); }
function conHora(v){ return v!==null && Math.abs(v-Math.floor(v))>1e-9; }

// --- Construccion de viajes ---------------------------------------------------
const viajes=[];
for(let i=9;i<rows.length;i++){
  const r=rows[i]; if(!r) continue;
  const prog=dtc(r,C.programacion); if(prog===null) continue;
  const d=fecha(prog); if(d.getUTCFullYear()!==2026) continue;

  const h={};
  for(const k of ['sBase1','llTrujillo','registro','programacion','iPlanta','iniCarga',
    'termCarga','sPlanta','sBase2','llBodNac','iBodNac','sBodNac','llCebaf','cruce',
    'autoriz','llTci','sTci','llPlanta','llAlmacen','ingreso','iDescarga','tDescarga',
    'salida','llBaseRet']) h[k]=dtc(r,C[k]);

  // Regla 1: reconstruccion de la llegada a base
  const crudo=dtc(r,C.llBaseRaw);
  let llBase=null, llBaseRec=false;
  if(crudo!==null){
    if(h.sPlanta!==null && conHora(crudo) && conHora(h.sPlanta)){
      const hora=crudo-Math.floor(crudo);
      let cand=Math.floor(h.sPlanta)+hora;
      if(cand<h.sPlanta) cand+=1;
      llBase=cand; llBaseRec=Math.abs(cand-crudo)>1e-9;
    } else { llBase=crudo; }
  }
  h.llBase=llBase;

  viajes.push({fila:i, mes:String(d.getUTCMonth()+1).padStart(2,'0'),
    fProg:iso(prog).slice(0,10),
    ref:String(r[C.cliente]||''), condOrigen:r[C.condOrigen]||'', tracto1:r[C.tracto1]||'',
    carreta:r[C.carreta]||'', condDestino:r[C.condDestino]||'', tracto2:r[C.tracto2]||'',
    bodNac:r[C.bodega]||'', bodEcu:r[C.bodEcu]||'', destino:r[C.bodDesc]||'',
    motivo:r[C.motivo]||'', llBaseRec, h});
}

// --- Definicion de tramos -----------------------------------------------------
// [clave, etiqueta, desde, hasta, grupo, topeDias]
const TRAMOS=[
 ['T01','Base -> Trujillo (transito, en vacio)','sBase1','llTrujillo','1. NACIONAL',3],
 ['T02','Trujillo: espera hasta ingresar a planta','llTrujillo','iPlanta','1. NACIONAL',5],
 ['T03','Planta: ingreso hasta inicio de carga','iPlanta','iniCarga','1. NACIONAL',3],
 ['T04','Planta: carga','iniCarga','termCarga','1. NACIONAL',2],
 ['T05','Planta: fin de carga hasta salida','termCarga','sPlanta','1. NACIONAL',3],
 ['T06','Trujillo -> Base (transito, cargado)','sPlanta','llBase','1. NACIONAL',3],
 ['T07','Base: estadia entre ciclo nacional e internacional','llBase','sBase2','2. ENLACE',10],
 ['T08','Base -> Bodega Nacional (Complex/Tumbes)','sBase2','llBodNac','3. INTERNACIONAL',5],
 ['T09','Bodega Nacional: permanencia','llBodNac','sBodNac','3. INTERNACIONAL',6],
 ['T10','Bodega Nacional -> CEBAF','sBodNac','llCebaf','3. INTERNACIONAL',2],
 ['T11','CEBAF: tramite hasta el cruce','llCebaf','cruce','3. INTERNACIONAL',5],
 ['T12','Cruce -> TCI (Huaquillas)','cruce','llTci','3. INTERNACIONAL',2],
 ['T13','TCI: permanencia','llTci','sTci','3. INTERNACIONAL',5],
 ['T14','TCI -> Guayaquil (planta destino)','sTci','llPlanta','3. INTERNACIONAL',3],
 ['T15','Guayaquil: llegada hasta ingreso','llPlanta','ingreso','4. DESCARGA',2],
 ['T16','Guayaquil: ingreso hasta inicio de descarga','ingreso','iDescarga','4. DESCARGA',5],
 ['T17','Guayaquil: descarga','iDescarga','tDescarga','4. DESCARGA',2],
 ['T18','Guayaquil: fin de descarga hasta salida','tDescarga','salida','4. DESCARGA',3],
];
const CICLOS=[
 ['C1','PERIODO NACIONAL (Base -> Trujillo -> Base)','sBase1','llBase',12],
 ['C2','BASE -> GUAYAQUIL (hasta llegar a planta destino)','sBase2','llPlanta',15],
 ['C3','BASE -> GUAYAQUIL descargado (hasta salir de planta)','sBase2','salida',18],
 ['C4','Salida planta Trujillo -> Guayaquil','sPlanta','llPlanta',18],
];

function stats(a){
  if(!a.length) return null;
  const s=[...a].sort((x,y)=>x-y);
  const q=p=>{const i=(s.length-1)*p,lo=Math.floor(i),hi=Math.ceil(i);return s[lo]+(s[hi]-s[lo])*(i-lo);};
  return {n:s.length, prom:a.reduce((x,y)=>x+y,0)/s.length, med:q(.5), p10:q(.1), p90:q(.9), min:s[0], max:s[s.length-1]};
}

function serie(clave,desde,hasta,tope,filtro){
  const v=[];
  for(const t of (filtro?viajes.filter(filtro):viajes)){
    const a=t.h[desde], b=t.h[hasta];
    if(a===null||b===null) continue;
    const d=b-a;
    if(d<0||d>tope) continue;
    v.push(d);
  }
  return v;
}

// --- Estimacion del retorno Guayaquil -> Base --------------------------------
// El Excel no registra la llegada a base del retorno (0.2% de cobertura). Se estima
// con tres metodos independientes y se contrasta.
//  M1: los pocos registros reales que si tienen el hito.
//  M2: rotacion de flota: tiempo entre la salida de Guayaquil de un tracto y el inicio
//      de su siguiente viaje. El percentil 10 aproxima el retorno puro sin descanso.
//  M3: suma de los transitos de la ida sin los tiempos de tramite (aduana y bodegas),
//      que en vacio no aplican.
const m1=serie('RET','salida','llBaseRet',5);

const porTracto={};
for(const t of viajes){
  const p=(t.tracto2||t.tracto1||'').trim(); if(!p) continue;
  porTracto[p]=porTracto[p]||[];
  porTracto[p].push(t);
}
const gaps=[];
for(const p of Object.keys(porTracto)){
  const lista=porTracto[p].filter(t=>t.h.salida!==null).sort((a,b)=>a.h.salida-b.h.salida);
  const arranques=porTracto[p]
    .map(t=>t.h.sBase2!==null?t.h.sBase2:(t.h.sBase1!==null?t.h.sBase1:t.h.iPlanta))
    .filter(v=>v!==null).sort((a,b)=>a-b);
  for(const t of lista){
    const sig=arranques.find(v=>v>t.h.salida+0.05);
    if(sig===undefined) continue;
    const g=sig-t.h.salida;
    if(g>0&&g<12) gaps.push(g);
  }
}
const sg=stats(gaps);

const transitoIda=[['T08',0.27],['T10',0],['T12',0],['T14',0]]; // se rellena abajo
const sM3={};
for(const [k,,de,ha,,tope] of TRAMOS.map(x=>[x[0],x[1],x[2],x[3],x[4],x[5]])){
  sM3[k]=stats(serie(k,de,ha,tope));
}
const medT=k=>sM3[k]&&sM3[k].med!==undefined?sM3[k].med:0;
// Retorno en vacio = Guayaquil->TCI + cruce + Huaquillas->Bodega Nacional zona + zona->Base
const m3 = medT('T14') + medT('T12') + medT('T10') + medT('T08');

const est = {
  m1: stats(m1),
  m2: sg,
  m2_p10: sg?sg.p10:null,
  m3,
  // Valor adoptado: promedio de los tres metodos disponibles, redondeado a 2 decimales.
  adoptado: null
};
{
  const cand=[];
  if(est.m1) cand.push(est.m1.med);
  if(est.m2_p10!==null) cand.push(est.m2_p10);
  if(isFinite(m3)&&m3>0) cand.push(m3);
  est.adoptado = cand.length? cand.reduce((a,b)=>a+b,0)/cand.length : null;
}

// --- Salida --------------------------------------------------------------------
const resumen=[];
for(const [k,et,de,ha,g,tope] of TRAMOS){
  const st=stats(serie(k,de,ha,tope));
  resumen.push({clave:k,tramo:et,grupo:g,...(st||{n:0})});
}
resumen.push({clave:'T19',tramo:'Guayaquil -> Base (retorno, ESTIMADO)',grupo:'5. RETORNO',
  n:(est.m1?est.m1.n:0), prom:est.adoptado, med:est.adoptado, p10:est.adoptado, p90:est.adoptado,
  min:est.adoptado, max:est.adoptado, estimado:true});

const ciclos=[];
for(const [k,et,de,ha,tope] of CICLOS){
  const st=stats(serie(k,de,ha,tope));
  ciclos.push({clave:k,ciclo:et,...(st||{n:0})});
}

// Los dos cierres que pidio la jefatura y que el Excel no puede medir de punta a punta,
// porque no registra la llegada a base del retorno. Se arman sumando tramos medidos mas
// el retorno estimado; cada sumando queda visible en la hoja Metodologia.
const c1=ciclos.find(c=>c.clave==='C1'), c3=ciclos.find(c=>c.clave==='C3');
const t07=resumen.find(r=>r.clave==='T07');
const R=est.adoptado||0;
ciclos.push({clave:'C5', ciclo:'BASE -> GUAYAQUIL -> BASE (ida y vuelta internacional)',
  n:c3.n, prom:(c3.prom||0)+R, med:(c3.med||0)+R, p10:(c3.p10||0)+R, p90:(c3.p90||0)+R,
  min:null, max:null, estimado:true});
ciclos.push({clave:'C6', ciclo:'CICLO TOTAL (nacional + estadia en base + internacional ida y vuelta)',
  n:Math.min(c1.n,c3.n), prom:(c1.prom||0)+(t07.prom||0)+(c3.prom||0)+R,
  med:(c1.med||0)+(t07.med||0)+(c3.med||0)+R,
  p10:(c1.p10||0)+(t07.p10||0)+(c3.p10||0)+R,
  p90:(c1.p90||0)+(t07.p90||0)+(c3.p90||0)+R,
  min:null, max:null, estimado:true});
const cicloCompleto=ciclos.find(c=>c.clave==='C6').med;
const cicloCompletoProm=ciclos.find(c=>c.clave==='C6').prom;

// Corte por planta de destino en Ecuador
const destinos=[];
for(const dst of [...new Set(viajes.map(v=>v.destino).filter(Boolean))]){
  const fil=t=>t.destino===dst;
  const fila={destino:dst, n:viajes.filter(fil).length};
  for(const [k,,de,ha,,tope] of TRAMOS){ const s=stats(serie(k,de,ha,tope,fil)); fila[k]=s?s.prom:null; }
  for(const [k,,de,ha,tope] of CICLOS){ const s=stats(serie(k,de,ha,tope,fil)); fila[k]=s?s.prom:null; fila[k+'_med']=s?s.med:null; fila[k+'_n']=s?s.n:0; }
  destinos.push(fila);
}
destinos.sort((a,b)=>b.n-a.n);

// Serie mensual
const meses={};
for(const t of viajes){
  meses[t.mes]=meses[t.mes]||{mes:t.mes,n:0};
  meses[t.mes].n++;
}
for(const m of Object.keys(meses)){
  for(const [k,,de,ha,,tope] of TRAMOS){
    const st=stats(serie(k,de,ha,tope,t=>t.mes===m));
    meses[m][k]=st?st.med:null;
  }
  for(const [k,,de,ha,tope] of CICLOS){
    const st=stats(serie(k,de,ha,tope,t=>t.mes===m));
    meses[m][k]=st?st.med:null;
    meses[m][k+'_n']=st?st.n:0;
    meses[m][k+'_prom']=st?st.prom:null;
  }
}

const salida={
  generado:new Date().toISOString(),
  totalViajes:viajes.length,
  TRAMOS, CICLOS, resumen, ciclos, destinos,
  estimacionRetorno:est,
  cicloCompleto:{mediana:cicloCompleto, promedio:cicloCompletoProm},
  meses:Object.values(meses).sort((a,b)=>a.mes.localeCompare(b.mes)),
  viajes
};
fs.writeFileSync(OUT, JSON.stringify(salida,null,1),'utf8');

// --- Reporte en consola --------------------------------------------------------
const f=(x,d=2)=>x===null||x===undefined?'   -  ':x.toFixed(d);
console.log('Viajes 2026: '+viajes.length+'   (reconstruccion LL.Base aplicada a '+viajes.filter(v=>v.llBaseRec).length+' viajes)\n');
console.log('TRAMO'.padEnd(6)+'DESCRIPCION'.padEnd(52)+'n'.padStart(6)+'prom'.padStart(8)+'mediana'.padStart(9)+'p10'.padStart(8)+'p90'.padStart(8));
let grp='';
for(const r of resumen){
  if(r.grupo!==grp){ grp=r.grupo; console.log('-- '+grp); }
  console.log(r.clave.padEnd(6)+r.tramo.padEnd(52)+String(r.n).padStart(6)+f(r.prom).padStart(8)+f(r.med).padStart(9)+f(r.p10).padStart(8)+f(r.p90).padStart(8));
}
console.log('\nCICLOS AGREGADOS');
for(const c of ciclos) console.log(c.clave.padEnd(6)+c.ciclo.padEnd(52)+String(c.n).padStart(6)+f(c.prom).padStart(8)+f(c.med).padStart(9)+f(c.p10).padStart(8)+f(c.p90).padStart(8));

console.log('\nESTIMACION RETORNO GUAYAQUIL -> BASE');
console.log('  M1 registros reales      n='+(est.m1?est.m1.n:0)+'  mediana='+f(est.m1?est.m1.med:null));
console.log('  M2 rotacion de flota     n='+(est.m2?est.m2.n:0)+'  p10='+f(est.m2_p10)+'  mediana='+f(est.m2?est.m2.med:null));
console.log('  M3 suma de transitos     '+f(m3));
console.log('  ADOPTADO                 '+f(est.adoptado)+' dias');
console.log('\nCICLO COMPLETO Base->Trujillo->Base->Guayaquil->Base');
console.log('  mediana='+f(cicloCompleto)+' dias   promedio='+f(cicloCompletoProm)+' dias');
