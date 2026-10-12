import {readdir,readFile} from 'node:fs/promises';
import {summarize} from '../public/motion-analysis.js';
const dir=new URL('../data/experiments/',import.meta.url);
let files;try{files=(await readdir(dir)).filter(x=>x.endsWith('.json'));}catch{files=[];}
const sessions=[];
for(const name of files){const s=JSON.parse(await readFile(new URL(name,dir),'utf8'));if(!s.notes?.startsWith('QA_SYNTHETIC'))sessions.push({...s,summary:summarize(s)});}
if(!sessions.length){console.log('まだ実機の保存データはありません。/labで測定し「PCに保存」を押してください。');process.exit(0);}
for(const device of ['android-chrome','iphone-safari']){
  const group=sessions.filter(s=>s.device===device);if(!group.length)continue;
  console.log('\n'+device);
  console.table(group.map(s=>({id:s.id.slice(0,8),label:s.label,actual:s.actualCount,Hz:s.summary.motionHz.toFixed(1),maxGapMs:s.summary.maxGapMs.toFixed(0),accMax:s.summary.acceleration?.max.toFixed(2)??'unavailable',gMin:s.summary.gravityG?.min.toFixed(2)??'unavailable',gMax:s.summary.gravityG?.max.toFixed(2)??'unavailable',shake12:s.summary.shake.find(r=>r.threshold===12).count,jump:s.summary.jump.find(r=>r.lowG===.65&&r.landingG===1.8).count})));
  console.log('加速度＋回転の更新候補V2：legacyは調整用、candidate-v2は更新後の記録。');
  console.table(group.map(s=>({id:s.id.slice(0,8),label:s.label,recordedProfile:s.motionProfile??'legacy',actual:s.actualCount,shakeV2:s.summary.candidate.shake.count,jumpV2:s.summary.candidate.jump.count,shakePerCue:s.summary.candidate.shake.perCue.join('/'),jumpPerCue:s.summary.candidate.jump.perCue.join('/')})));
  console.log('円の候補C1：offは判定追加前の記録、circle-v1は追加後の記録。');
  console.table(group.map(s=>({id:s.id.slice(0,8),label:s.label,recordedCircle:s.circleProfile??'off',actual:s.actualCount,circleC1:s.summary.circle.count,perCue:s.summary.circle.perCue.join('/'),outsideCues:s.summary.circle.outsideCues})));
  const controls=group.filter(s=>['still','hammer','bend','tilt','circle','wrist'].includes(s.label));
  const shake=group.filter(s=>s.label==='shake'),jump=group.filter(s=>s.label==='jump');
  console.log('振る候補：実測回数との差は参考値。合図ごとの検出時刻と原波形も確認する。');
  console.table([3,5,8,10,12,15,18,22,26].map(threshold=>({threshold,shakeTrials:shake.length,controlTrials:controls.length,countError:shake.reduce((sum,s)=>sum+Math.abs(s.summary.shake.find(r=>r.threshold===threshold).count-s.actualCount),0),controlEvents:controls.reduce((sum,s)=>sum+s.summary.shake.find(r=>r.threshold===threshold).count,0),jumpCrossEvents:jump.reduce((sum,s)=>sum+s.summary.shake.find(r=>r.threshold===threshold).count,0)})));
  console.log('従来の低g＋着地のジャンプ候補：V2の回転条件を含めない比較。');
  console.table(group[0].summary.jump.map(config=>({lowG:config.lowG,landingG:config.landingG,jumpTrials:jump.length,countError:jump.reduce((sum,s)=>sum+Math.abs(s.summary.jump.find(r=>r.lowG===config.lowG&&r.landingG===config.landingG).count-s.actualCount),0),nonJumpEvents:group.filter(s=>['still','hammer','bend','shake','tilt','circle','wrist'].includes(s.label)).reduce((sum,s)=>sum+s.summary.jump.find(r=>r.lowG===config.lowG&&r.landingG===config.landingG).count,0)})));
}
