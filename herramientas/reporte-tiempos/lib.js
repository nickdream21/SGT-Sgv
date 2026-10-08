const fs=require('fs');
function cargar(base, sheetFile){
  const ss=[];
  const x=fs.readFileSync(base+'/xl/sharedStrings.xml','utf8');
  for(const m of x.matchAll(/<si>([\s\S]*?)<\/si>/g)){
    const parts=[...m[1].matchAll(/<t[^>]*>([\s\S]*?)<\/t>/g)].map(a=>a[1]);
    ss.push(parts.join('').replace(/&lt;/g,'<').replace(/&gt;/g,'>').replace(/&amp;/g,'&').replace(/&quot;/g,'"').replace(/&apos;/g,"'"));
  }
  function colNum(ref){let c=0;for(const ch of ref.replace(/\d+/g,'')) c=c*26+(ch.charCodeAt(0)-64);return c;}
  const xml=fs.readFileSync(base+'/xl/worksheets/'+sheetFile,'utf8');
  const rows=[];
  for(const rm of xml.matchAll(/<row[^>]*?r="(\d+)"[^>]*>([\s\S]*?)<\/row>/g)){
    const cells={};
    for(const cm of rm[2].matchAll(/<c([^>]*?)(?:\/>|>([\s\S]*?)<\/c>)/g)){
      if(cm[2]===undefined) continue;
      const ref=(cm[1].match(/r="([A-Z]+\d+)"/)||[])[1]; if(!ref) continue;
      const t=(cm[1].match(/t="([^"]+)"/)||[])[1];
      const vm=cm[2].match(/<v>([\s\S]*?)<\/v>/);
      const im=cm[2].match(/<is>[\s\S]*?<t[^>]*>([\s\S]*?)<\/t>/);
      let v=null;
      if(t==='s'&&vm) v=ss[+vm[1]];
      else if(t==='inlineStr'&&im) v=im[1];
      else if(vm) v=vm[1];
      if(v===null||v===undefined) continue;
      v=String(v).replace(/&lt;/g,'<').replace(/&gt;/g,'>').replace(/&amp;/g,'&').trim();
      if(v==='') continue;
      cells[colNum(ref)]=v;
    }
    rows[+rm[1]]=cells;
  }
  return rows;
}
// serial Excel (1900) -> Date UTC
function fecha(serial){ return new Date(Math.round((serial-25569)*86400*1000)); }
function iso(serial){ const d=fecha(serial); const p=x=>String(x).padStart(2,'0');
  return d.getUTCFullYear()+'-'+p(d.getUTCMonth()+1)+'-'+p(d.getUTCDate())+' '+p(d.getUTCHours())+':'+p(d.getUTCMinutes()); }
module.exports={cargar,fecha,iso};
