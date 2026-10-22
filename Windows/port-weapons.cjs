// Preserve Elise616's vector coordinates; translate simple drawing primitives to WPF path data.
const fs=require('fs'),path=require('path');
const base=path.join(__dirname,'..');
const src=fs.readFileSync(path.join(base,'Sources/Weapons.swift'),'utf8').replace(/\r/g,'');
const map={};for(const m of src.matchAll(/"([a-z.0-9]+)": \.(\w+)/g))map[m[1]]=m[2];
const sketch=src.slice(src.indexOf('private static func sketch'));
const chunks=[...sketch.matchAll(/case ([^:]+):([\s\S]*?)(?=\n        case |\n        }\n    }\n})/g)];
const output={};
for(const [,names,body] of chunks)for(const name of names.replace(/\./g,'').split(',').map(x=>x.trim())){
 let d='';const poly=(pts)=>'M '+pts.map(p=>p.join(',')).join(' L ')+' Z ';
 for(const line of body.split('\n')){
  if(line.includes('let flame')){d+='M 30,74 C 22,86 30,98 40,108 C 42,102 44,96 46,92 C 48,95 51,97 52,100 C 60,90 58,80 52,74 Z ';continue;}
  if(line.includes('for x:')){for(const x of [18,38,58,78])d+=poly([[x-9,22],[x+9,22],[x+9,40],[x-9,40]]);continue;}
  const m=line.match(/p\.(rect|poly|circle|ellipse|ring|arc|bar)\((.*)\)/);if(!m)continue;
  const a=[...m[2].matchAll(/-?\d+(?:\.\d+)?/g)].map(x=>+x[0]);const [x,y,w,h]=a;
  if(name==='pistol'&&x>=104)continue;
  if(m[1]==='rect')d+=poly([[x,y],[x+w,y],[x+w,y+h],[x,y+h]]);
  else if(m[1]==='poly')d+=poly(Array.from({length:a.length/2},(_,i)=>a.slice(i*2,i*2+2)));
  else if(['circle','ellipse','ring'].includes(m[1])){const rx=w,ry=m[1]==='ellipse'?h:w;d+=`M ${x-rx},${y} A ${rx},${ry} 0 1 0 ${x+rx},${y} A ${rx},${ry} 0 1 0 ${x-rx},${y} Z `;if(m[1]==='ring'){const ri=rx-(h||3);d+=`M ${x-ri},${y} A ${ri},${ri} 0 1 1 ${x+ri},${y} A ${ri},${ri} 0 1 1 ${x-ri},${y} Z `;}}
  else if(m[1]==='bar'){const pts=a.slice(0,-1),width=a.at(-1);for(let i=0;i<pts.length-2;i+=2){const [x1,y1,x2,y2]=pts.slice(i,i+4),len=Math.hypot(x2-x1,y2-y1),dx=-(y2-y1)/len*width/2,dy=(x2-x1)/len*width/2;d+=poly([[x1+dx,y1+dy],[x2+dx,y2+dy],[x2-dx,y2-dy],[x1-dx,y1-dy]]);}}
  else if(m[1]==='arc'){const [cx,cy,r,start,end,width]=a;const pts=[];for(let t=start;t<=end;t+=5)pts.push([cx+Math.cos(t*Math.PI/180)*(r+width/2),cy+Math.sin(t*Math.PI/180)*(r+width/2)]);for(let t=end;t>=start;t-=5)pts.push([cx+Math.cos(t*Math.PI/180)*(r-width/2),cy+Math.sin(t*Math.PI/180)*(r-width/2)]);d+=poly(pts);}
 }
 output[name]=d;
}
fs.writeFileSync(path.join(base,'Windows/weapons.json'),JSON.stringify(output,null,2));fs.writeFileSync(path.join(base,'Windows/weapon-map.json'),JSON.stringify(map,null,2));console.log(Object.keys(output).length+' weapon silhouettes translated');
