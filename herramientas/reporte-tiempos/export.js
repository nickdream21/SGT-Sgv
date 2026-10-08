// Genera los TSV que alimentan el libro de Excel.
const fs=require('fs');
const M=require(process.argv[2]);
const DIR=process.argv[3];

const TRAMOS=M.TRAMOS, CICLOS=M.CICLOS;
const topeDe={}; for(const t of TRAMOS) topeDe[t[0]]=t[5];
const topeCic={}; for(const c of CICLOS) topeCic[c[0]]=c[4];

function dur(h,de,ha,tope){
  const a=h[de], b=h[ha];
  if(a===null||a===undefined||b===null||b===undefined) return '';
  const d=b-a;
  if(d<0||d>tope) return '';
  return d.toFixed(3);
}
function real(v){ return v!==null&&v!==undefined&&Math.abs(v-Math.floor(v))>1e-9; }

const R=M.estimacionRetorno.adoptado;
const MESNOM={'01':'01 Ene','02':'02 Feb','03':'03 Mar','04':'04 Abr','05':'05 May','06':'06 Jun','07':'07 Jul','08':'08 Ago','09':'09 Set'};

// ---- Hoja DATOS -------------------------------------------------------------
// La columna CLIENTE del Excel guarda en realidad el numero de pedido, y Google Sheets lo
// exporto en notacion cientifica. Se devuelve a entero para que sea legible y filtrable.
function pedido(s){
  if(!s) return '';
  const n=Number(s);
  return isFinite(n)&&Math.abs(n)>1e6 ? n.toFixed(0) : String(s);
}

const cab=['Mes','F. Programacion','Pedido','Conductor origen','Tracto 1','Carreta',
  'Conductor destino','Tracto 2','Bodega nacional','Bodega ecuatoriana','Planta destino',
  'Calidad del dato']
  .concat(TRAMOS.map(t=>t[0]+' '+t[1]))
  .concat(['T19 Guayaquil -> Base (estimado)'])
  .concat(CICLOS.map(c=>c[0]+' '+c[1]))
  .concat(['C5 Base -> Guayaquil -> Base (con retorno estimado)',
           'C6 Ciclo total (con retorno estimado)','Motivo de retraso']);

const filas=[cab];
for(const v of M.viajes){
  const h=v.h;
  const nacOk=real(h.sBase1)&&h.llBase!==null;
  const intOk=h.sBase2!==null&&h.llPlanta!==null;
  const calidad = nacOk&&intOk ? 'A - ciclo completo'
                : intOk        ? 'B - solo internacional'
                : nacOk        ? 'C - solo nacional'
                :                'D - incompleto';
  const f=[MESNOM[v.mes]||v.mes, v.fProg, pedido(v.ref), v.condOrigen, v.tracto1, v.carreta,
    v.condDestino, v.tracto2, v.bodNac, v.bodEcu, v.destino, calidad];
  for(const [k,,de,ha,,tope] of TRAMOS) f.push(dur(h,de,ha,tope));
  f.push(intOk? R.toFixed(3):'');
  const cic={};
  for(const [k,,de,ha,tope] of CICLOS){ const d=dur(h,de,ha,tope); cic[k]=d; f.push(d); }
  f.push(cic.C3!==''? (Number(cic.C3)+R).toFixed(3):'');
  f.push(cic.C1!==''&&cic.C3!==''&&dur(h,'llBase','sBase2',10)!==''
    ? (Number(cic.C1)+Number(dur(h,'llBase','sBase2',10))+Number(cic.C3)+R).toFixed(3):'');
  f.push((v.motivo||'').replace(/[\t\r\n]+/g,' '));
  filas.push(f);
}
fs.writeFileSync(DIR+'/datos.tsv', filas.map(r=>r.join('\t')).join('\n'),'utf8');

// Etiquetas cortas: las descripciones completas no entran en el eje de un grafico.
const CORTO={
 T01:'Base > Trujillo', T02:'Espera en Trujillo', T03:'Ingreso a inicio de carga',
 T04:'Carga', T05:'Fin de carga a salida', T06:'Trujillo > Base',
 T07:'Estadia en base', T08:'Base > Bodega Nacional', T09:'Bodega Nacional',
 T10:'Bodega Nacional > CEBAF', T11:'CEBAF hasta el cruce', T12:'Cruce > TCI',
 T13:'TCI', T14:'TCI > Guayaquil', T15:'Llegada a ingreso (Gye)',
 T16:'Ingreso a inicio de descarga', T17:'Descarga', T18:'Fin de descarga a salida',
 T19:'Guayaquil > Base (est.)',
 C1:'Nacional: Base>Trujillo>Base', C2:'Base > Guayaquil',
 C3:'Base > Guayaquil descargado', C4:'Trujillo > Guayaquil',
 C5:'Base > Guayaquil > Base', C6:'Ciclo total'
};

// ---- Hoja RESUMEN TRAMOS ----------------------------------------------------
const res=[['Orden','Clave','Tramo (corto)','Grupo','Tramo','Viajes medidos','Promedio (dias)','Mediana (dias)',
  'Promedio (horas)','P10 (dias)','P90 (dias)','Maximo (dias)','Tipo de dato']];
M.resumen.forEach((r,i)=>{
  res.push([i+1, r.clave, CORTO[r.clave]||r.clave, r.grupo, r.tramo, r.n||0,
    r.prom!=null?r.prom.toFixed(3):'', r.med!=null?r.med.toFixed(3):'',
    r.prom!=null?(r.prom*24).toFixed(1):'',
    r.p10!=null?r.p10.toFixed(3):'', r.p90!=null?r.p90.toFixed(3):'',
    r.max!=null?r.max.toFixed(2):'', r.estimado?'ESTIMADO':'MEDIDO']);
});
fs.writeFileSync(DIR+'/resumen.tsv', res.map(r=>r.join('\t')).join('\n'),'utf8');

// ---- Hoja CICLOS ------------------------------------------------------------
const cic=[['Clave','Ciclo (corto)','Ciclo','Viajes medidos','Promedio (dias)','Mediana (dias)','P10 (dias)','P90 (dias)','Tipo de dato']];
for(const c of M.ciclos){
  cic.push([c.clave, CORTO[c.clave]||c.clave, c.ciclo, c.n||0,
    c.prom!=null?c.prom.toFixed(3):'', c.med!=null?c.med.toFixed(3):'',
    c.p10!=null?c.p10.toFixed(3):'', c.p90!=null?c.p90.toFixed(3):'',
    c.estimado?'MEDIDO + RETORNO ESTIMADO':'MEDIDO']);
}
fs.writeFileSync(DIR+'/ciclos.tsv', cic.map(r=>r.join('\t')).join('\n'),'utf8');

// ---- Hoja POR MES -----------------------------------------------------------
const mes=[['Mes','Viajes','Periodo nacional (dias)','Base -> Guayaquil (dias)',
  'Base -> Guayaquil descargado (dias)','Trujillo -> Guayaquil (dias)',
  'Base -> Guayaquil -> Base (dias)','Ciclo total (dias)']];
for(const m of M.meses){
  const c3=m.C3, c1=m.C1;
  mes.push([MESNOM[m.mes]||m.mes, m.n,
    c1!=null?c1.toFixed(2):'', m.C2!=null?m.C2.toFixed(2):'',
    c3!=null?c3.toFixed(2):'', m.C4!=null?m.C4.toFixed(2):'',
    c3!=null?(c3+R).toFixed(2):'',
    (c1!=null&&c3!=null&&m.T07!=null)?(c1+m.T07+c3+R).toFixed(2):'']);
}
fs.writeFileSync(DIR+'/pormes.tsv', mes.map(r=>r.join('\t')).join('\n'),'utf8');

// ---- Hoja POR DESTINO -------------------------------------------------------
const des=[['Planta destino','Viajes','Periodo nacional (dias)','Base -> Guayaquil (dias)',
  'Base -> Guayaquil descargado (dias)','Trujillo -> Guayaquil (dias)','Base -> Guayaquil -> Base (dias)']];
for(const d of M.destinos){
  des.push([d.destino, d.n, d.C1!=null?d.C1.toFixed(2):'', d.C2!=null?d.C2.toFixed(2):'',
    d.C3!=null?d.C3.toFixed(2):'', d.C4!=null?d.C4.toFixed(2):'',
    d.C3!=null?(d.C3+R).toFixed(2):'']);
}
fs.writeFileSync(DIR+'/pordestino.tsv', des.map(r=>r.join('\t')).join('\n'),'utf8');

// ---- KPIs -------------------------------------------------------------------
const g=k=>M.ciclos.find(c=>c.clave===k);
const kpi=[['Indicador','Valor (dias)','Detalle']];
kpi.push(['Viajes analizados 2026', M.totalViajes, 'Base: fecha de programacion entre enero y setiembre de 2026']);
kpi.push(['Periodo nacional (Base -> Trujillo -> Base)', g('C1').prom.toFixed(2), g('C1').n+' viajes medidos; mediana '+g('C1').med.toFixed(2)+' dias']);
kpi.push(['Base -> Guayaquil', g('C2').prom.toFixed(2), g('C2').n+' viajes medidos; mediana '+g('C2').med.toFixed(2)+' dias']);
kpi.push(['Base -> Guayaquil -> Base', g('C5').prom.toFixed(2), 'Ida medida ('+g('C3').n+' viajes) + retorno estimado en '+M.estimacionRetorno.adoptado.toFixed(2)+' dias']);
kpi.push(['Ciclo total del viaje', g('C6').prom.toFixed(2), 'Nacional + estadia en base + internacional ida y vuelta']);
kpi.push(['Retorno Guayaquil -> Base', M.estimacionRetorno.adoptado.toFixed(2), 'ESTIMADO: no existe el hito en la fuente']);
fs.writeFileSync(DIR+'/kpi.tsv', kpi.map(r=>r.join('\t')).join('\n'),'utf8');

console.log('TSV generados en '+DIR);
console.log('  datos.tsv      '+(filas.length-1)+' viajes x '+cab.length+' columnas');
console.log('  resumen.tsv    '+(res.length-1)+' tramos');
console.log('  ciclos.tsv     '+(cic.length-1)+' ciclos');
console.log('  pormes.tsv     '+(mes.length-1)+' meses');
console.log('  pordestino.tsv '+(des.length-1)+' destinos');
