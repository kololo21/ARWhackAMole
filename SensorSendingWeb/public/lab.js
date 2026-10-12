import {Sensors} from './sensors.js';
import {trials,summarize} from './motion-analysis.js';
import {Feedback} from './feedback.js';
import {fmt} from './transport.js';
import {plot,appendEvents} from './charts.js';
const $=id=>document.getElementById(id),params=new URLSearchParams(location.hash.slice(1));
$('room').value=params.get('room')??'';$('player').value=params.get('player')==='blue'?'blue':'red';
if(/iPhone|iPad/.test(navigator.userAgent))$('device').value='iphone-safari';
for(const [key,[name]] of Object.entries(trials)){const option=document.createElement('option');option.value=key;option.textContent=name;$('label').append(option);}
const feedback=new Feedback(text=>{$('feedback').textContent=text;});
const sensors=new Sensors((name,state)=>{$('status').textContent=`${name}: ${state}`;});sensors.jumpEnabled=true;
sensors.motionProfile='candidate-v2';
sensors.circleEnabled=true;
let session=null,phase='idle',prepareAt=0,startAt=0,cueIndex=0,history=[],wake=null,lastMotion=null;
const quiet=()=>['still','noise'].includes($('label').value);
function instruction(){$('instruction').textContent=trials[$('label').value][1]+(quiet()?'':' 12秒の中で、2・5・8秒に合図が出ます。');if(!session)$('actual').value=quiet()?0:3;}
instruction();$('label').onchange=instruction;
function cue(text,action=false){$('cue').textContent=text;$('cue').dataset.action=String(action);}
function lock(on){for(const id of ['device','room','player','label','actual','notes','calibrate','mic','vibration','vibrationOnly','sound'])$(id).disabled=on||(['calibrate','mic'].includes(id)&&!sensors.active);$('record').disabled=on||!sensors.active;$('cancel').disabled=!on;$('save').disabled=true;$('download').disabled=true;}
function abort(message){phase='idle';session=null;lock(false);cue(message);$('result').textContent='中止した記録は保存されません。';}
async function awake(){try{if(!document.hidden&&navigator.wakeLock)wake=await navigator.wakeLock.request('screen');}catch{}}
function freshReading(name){const s=sensors.values[name];return s.valid&&Date.now()-s.timestampMs<1000?s:null;}
sensors.onMotion=m=>{
  lastMotion=m;const gyro=m.r&&['alpha','beta','gamma'].every(k=>Number.isFinite(m.r[k]))?Math.hypot(m.r.alpha,m.r.beta,m.r.gamma):null;
  $('a').textContent=fmt(m.magnitude);$('g').textContent=fmt(m.gravityG);$('gyro').textContent=fmt(gyro,1);
  history.push(m.magnitude);if(history.length>180)history.shift();plot($('plot'),history);
  if(phase!=='recording')return;
  const t=m.at-startAt;if(t<0||t>=12000)return;
  const o=freshReading('orientation'),mic=freshReading('microphone');
  const row={t,ax:m.a?.x??null,ay:m.a?.y??null,az:m.a?.z??null,gx:m.g?.x??null,gy:m.g?.y??null,gz:m.g?.z??null,a:m.magnitude,g:m.gravityG,gyro,tilt:o?Math.hypot(o.relativeBeta,o.relativeGamma):null,rms:mic?.rms??null};
  const finite=v=>Number.isFinite(v)?v:null;
  Object.assign(row,{ra:finite(m.r?.alpha),rb:finite(m.r?.beta),rg:finite(m.r?.gamma),oa:finite(o?.alpha),ob:finite(o?.beta),og:finite(o?.gamma)});
  if(t>(session.samples.at(-1)?.t??-1)&&session.samples.length<4000)session.samples.push(row);
};
$('enable').onclick=()=>{
  if(!isSecureContext){$('status').textContent='HTTPSで開いてください。';return;}
  $('enable').disabled=true;
  sensors.start(false).catch(e=>{$('status').textContent=e.message;$('enable').disabled=false;});
  feedback.enable();awake();lock(false);cue('動作を選んで記録');
};
$('calibrate').onclick=()=>{$('status').textContent=sensors.calibrate()?'傾きの基準を設定しました。':'姿勢データ待ちです。';};
$('mic').onclick=async()=>{try{await sensors.enableMicrophone();}catch(e){$('status').textContent=e.message;}};
$('record').onclick=()=>{
  if(!/^[-\w]{16,64}$/.test($('room').value)){$('status').textContent='送信画面と同じ16文字以上のルームキーが必要です。';return;}
  if(!lastMotion||performance.now()-lastMotion.at>1000||lastMotion.magnitude===null&&lastMotion.gravityG===null){$('status').textContent='加速度データが届いていません。センサーの許可を確認してください。';return;}
  if(['voice','noise'].includes($('label').value)&&!freshReading('microphone')){$('status').textContent='先にマイクを許可してください。';return;}
  $('actual').value=quiet()?0:3;$('notes').value='';
  session={version:1,motionProfile:sensors.motionProfile,circleProfile:'circle-v1',room:$('room').value,player:$('player').value,device:$('device').value,label:$('label').value,startedAt:Date.now()+3000,actualCount:Number($('actual').value),notes:'',cues:[],samples:[]};
  phase='preparing';prepareAt=performance.now();cueIndex=0;history=[];sensors.pause();feedback.enable();navigator.vibrate?.(0);lock(true);$('saved').textContent='';$('sweep').replaceChildren();$('events').replaceChildren();$('result').textContent='記録中…';
};
$('cancel').onclick=()=>abort('中止しました');
function completed(){
  phase='idle';lock(false);cue('終了 — 回数を確認して保存');$('progress').value=12;
  if(session.samples.length<10){session=null;$('result').textContent='加速度データが不足しています。権限を確認して再測定してください。';return;}
  const report=summarize(session);
  $('result').textContent=`${report.samples} サンプル / 実測 ${fmt(report.motionHz,1)} Hz / 最大間隔 ${fmt(report.maxGapMs,0)} ms / 加速度の最大 ${fmt(report.acceleration?.max)} m/s² / 更新版V2：シェイク ${report.candidate.shake.count} 回・ジャンプ ${report.candidate.jump.count} 回 / 円候補C1：${report.circle.count} 回（実験判定）`;
  if(['circle','wrist'].includes(session.label))$('result').textContent+=' / 円候補は動きを止めてから表示します。シェイク候補も出ることがあります。';
  for(const s of report.shake){const tr=document.createElement('tr');for(const value of [s.threshold,s.count,s.perCue.join(' / ')||'—']){const td=document.createElement('td');td.textContent=value;tr.append(td);}$('sweep').append(tr);}
  $('save').disabled=false;$('download').disabled=false;
}
setInterval(()=>{
  if(!sensors.active||document.hidden)return;
  sensors.sample();const events=sensors.drainEvents();appendEvents($('events'),events,false);
  const o=freshReading('orientation'),mic=freshReading('microphone');$('other').textContent=`${fmt(o?Math.hypot(o.relativeBeta,o.relativeGamma):null,1)}° / ${fmt(mic?.rms,3)}`;
  const now=performance.now();
  if(phase==='preparing'){
    const left=3000-(now-prepareAt);cue(`${Math.max(1,Math.ceil(left/1000))} 秒後に開始`);
    if(left<=0){phase='recording';startAt=now;session.startedAt=Date.now();cue('記録中 — 静止して待つ');sensors.pause();feedback.show('ready',{vibrate:false});}
  }else if(phase==='recording'){
    const elapsed=now-startAt;$('progress').value=elapsed/1000;
    if(elapsed>=12000){completed();return;}
    const cues=[2000,5000,8000];
    if(!['still','noise'].includes(session.label)&&cueIndex<3&&elapsed>=cues[cueIndex]){
      if(elapsed-cues[cueIndex]>1000){abort('合図が遅れたため中止 — 再測定してください');return;}
      // Use actual cue time to keep timing annotations honest under browser delays.
      session.cues.push(Math.round(elapsed));cueIndex++;cue(`今！ ${cueIndex} / 3`,true);feedback.show('ready',{vibrate:false});
    }else if($('cue').dataset.action==='true'&&elapsed-(session.cues.at(-1)??0)>900)cue('元の姿勢に戻して待つ');
    else if(['still','noise'].includes(session.label))cue(`記録中 — 残り ${Math.ceil((12000-elapsed)/1000)} 秒`);
  }
},33);
function payload(){if(!session)return null;return {...session,actualCount:Number($('actual').value),notes:$('notes').value};}
$('save').onclick=async()=>{
  const p=payload();if(!p||!Number.isInteger(p.actualCount)||p.actualCount<0||p.actualCount>30){$('saved').textContent='回数は0〜30の整数で入力してください。';return;}
  $('save').disabled=true;$('record').disabled=true;$('saved').textContent='PCへ保存中…';
  try{const r=await fetch('/api/experiments',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(p),signal:AbortSignal.timeout(15000)});const data=await r.json();if(!r.ok)throw new Error(data.error);$('saved').textContent=`PCに保存しました：${trials[p.label][0]} / ID ${data.id}`;}
  catch(e){$('saved').textContent=`保存できません：${e.message}。再試行またはJSON保存をしてください。`;$('save').disabled=false;}
  finally{$('record').disabled=false;}
};
$('download').onclick=()=>{const p=payload();if(!p)return;const url=URL.createObjectURL(new Blob([JSON.stringify(p,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download=`sensor-${p.device}-${p.label}-${p.startedAt}.json`;a.click();setTimeout(()=>URL.revokeObjectURL(url),30000);};
$('vibration').onclick=()=>feedback.test();
$('vibrationOnly').onclick=()=>feedback.test({sound:false,pattern:1000});
$('sound').onclick=()=>feedback.test({vibrate:false});
$('senderLink').href='/#'+new URLSearchParams({room:$('room').value,player:$('player').value});
document.addEventListener('visibilitychange',()=>{sensors.pause();if(document.hidden){if(phase!=='idle')abort('画面が閉じたため中止');wake?.release().catch(()=>{});wake=null;}else awake();});
window.addEventListener('pagehide',()=>{sensors.stop();wake?.release().catch(()=>{});});
