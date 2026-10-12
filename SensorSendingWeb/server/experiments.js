import {mkdir,writeFile,readdir} from 'node:fs/promises';
import {randomUUID} from 'node:crypto';
import {resolve} from 'node:path';
import {summarize,trials} from '../public/motion-analysis.js';
const keys=['t','ax','ay','az','gx','gy','gz','a','g','gyro','tilt','rms'];
const optionalKeys=['ra','rb','rg','oa','ob','og'];
export function validExperiment(p) {
  if(!p||p.version!==1||!Object.hasOwn(trials,p.label)||!['android-chrome','iphone-safari'].includes(p.device)||!['red','blue'].includes(p.player)||!/^[-\w]{16,64}$/.test(p.room??''))return false;
  if(p.motionProfile!==undefined&&!['legacy','candidate-v2'].includes(p.motionProfile))return false;
  if(p.circleProfile!==undefined&&!['off','circle-v1'].includes(p.circleProfile))return false;
  if(!Number.isInteger(p.actualCount)||p.actualCount<0||p.actualCount>30||!Number.isFinite(p.startedAt)||typeof p.notes!=='string'||p.notes.length>300)return false;
  if(!Array.isArray(p.cues)||p.cues.length>3||p.cues.some((t,i)=>!Number.isFinite(t)||t<[2000,5000,8000][i]||t>[2000,5000,8000][i]+1000))return false;
  if(!Array.isArray(p.samples)||p.samples.length<10||p.samples.length>4000)return false;
  let last=-1;
  return p.samples.every(r=>{
    if(!r||keys.some(k=>!Object.hasOwn(r,k)||r[k]!==null&&!Number.isFinite(r[k])))return false;
    if(optionalKeys.some(k=>Object.hasOwn(r,k)&&r[k]!==null&&(!Number.isFinite(r[k])||Math.abs(r[k])>=10000)))return false;
    if(!Number.isFinite(r.t)||r.t<0||r.t>13000||r.t<=last)return false;last=r.t;
    return ['ax','ay','az','gx','gy','gz','a','gyro','tilt'].every(k=>r[k]===null||Math.abs(r[k])<10000)&&(r.g===null||r.g>=0&&r.g<100)&&(r.rms===null||r.rms>=0&&r.rms<=1);
  });
}
export function experimentHandler(directory) {
  let saving=false;
  return async (req,res,url)=>{
    if(url.pathname!=='/api/experiments')return false;
    res.setHeader('Content-Type','application/json');
    const reply=(code,message)=>{res.writeHead(code);res.end(JSON.stringify(message));return true;};
    if(req.method!=='POST')return reply(405,{error:'POST only'});
    if(!req.headers['content-type']?.startsWith('application/json'))return reply(415,{error:'JSON required'});
    const declared=Number(req.headers['content-length']??0);
    if(declared>2*1024*1024)return reply(413,{error:'Too large'});
    if(saving)return reply(429,{error:'保存中です。再試行してください。'});
    saving=true;
    try{
      const chunks=[];let size=0;
      for await(const chunk of req){size+=chunk.length;if(size>2*1024*1024)return reply(413,{error:'Too large'});chunks.push(chunk);}
      let p;try{p=JSON.parse(Buffer.concat(chunks).toString());}catch{return reply(400,{error:'Invalid JSON'});}
      if(!validExperiment(p))return reply(400,{error:'測定データが不足、または形式が不正です。'});
      await mkdir(directory,{recursive:true});
      if((await readdir(directory)).filter(x=>x.endsWith('.json')).length>=500)return reply(507,{error:'実験保存数が上限に達しました。'});
      const id=randomUUID(),summary=summarize(p);
      // Store only the explicitly allowed fields. No audio/video or request headers.
      const data={version:1,id,receivedAt:new Date().toISOString(),motionProfile:p.motionProfile??'legacy',circleProfile:p.circleProfile??'off',room:p.room,player:p.player,device:p.device,placement:'left-hand',label:p.label,startedAt:p.startedAt,actualCount:p.actualCount,notes:p.notes,cues:p.cues,samples:p.samples.map(r=>Object.fromEntries([...keys,...optionalKeys].map(k=>[k,r[k]??null]))),summary};
      await writeFile(resolve(directory,id+'.json'),JSON.stringify(data,null,2),{flag:'wx'});
      return reply(201,{id,summary});
    }catch{return reply(500,{error:'保存に失敗しました。端末のJSON保存を利用してください。'});}
    finally{saving=false;}
  };
}
