import test from 'node:test';
import assert from 'node:assert/strict';
import {JumpDetector,MotionClassifier,CircleDetector,summarize} from '../public/motion-analysis.js';
import {validExperiment,experimentHandler} from '../server/experiments.js';
import {mkdtemp,readFile,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import http from 'node:http';
import {once} from 'node:events';
test('jump needs sustained low g then landing; no isolated impact, gap or overlong flight',()=>{
  function run(rows){const d=new JumpDetector();return rows.map(([t,g])=>d.update(t,g)).filter(Boolean);}
  assert.equal(run([[0,1],[20,2.5],[40,1]]).length,0);
  assert.equal(run([[0,.4],[20,.4],[40,.4],[60,.4],[80,1],[100,2]]).length,1);
  assert.equal(run([[0,.4],[20,.4],[40,.4],[60,.4],[350,2]]).length,0);
  assert.equal(run([[0,.4],[20,2]]).length,0);
  assert.equal(run(Array.from({length:55},(_,i)=>[i*20,.4]).concat([[1100,2]])).length,0);
  const detector=new JumpDetector();[0,20,40,60].forEach(t=>detector.update(t,.4));detector.reset();assert.equal(detector.update(80,2),null);
});
function session(){return {version:1,room:'experiment-room-key-1234',player:'red',device:'android-chrome',label:'shake',startedAt:Date.now(),actualCount:3,notes:'synthetic test',cues:[2010,5010,8010],samples:Array.from({length:600},(_,i)=>({t:i*20,ax:0,ay:0,az:0,gx:0,gy:0,gz:9.81,a:[110,260,410].includes(i)?14:0,g:1,gyro:0,tilt:0,rms:null}))};}
test('replay threshold sweep counts one event in each cue window, missing voice is unavailable',()=>{
  const p=session(),r=summarize(p);
  assert.deepEqual(r.shake.find(s=>s.threshold===12).perCue,[1,1,1]);
  assert.equal(r.shake.find(s=>s.threshold===15).count,0);assert.equal(r.voice,null);assert.equal(r.motionHz,50);
  assert.ok(validExperiment(p));p.samples[3].t=null;assert.equal(validExperiment(p),false);
});
test('HTTP experiment save persists allowed data and rejects malformed readings',async t=>{
  const dir=await mkdtemp(join(tmpdir(),'sensor-lab-'));const handler=experimentHandler(dir);
  const s=http.createServer(async(req,res)=>{await handler(req,res,new URL(req.url,'http://localhost'));});
  s.listen(0,'127.0.0.1');await once(s,'listening');t.after(async()=>{await new Promise(r=>s.close(r));await rm(dir,{recursive:true,force:true});});
  const url=`http://127.0.0.1:${s.address().port}/api/experiments`;
  const p=session();p.unwanted='not saved';
  const r=await fetch(url,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(p)});assert.equal(r.status,201);
  const data=await r.json(),saved=JSON.parse(await readFile(join(dir,data.id+'.json'),'utf8'));
  assert.equal(saved.samples.length,600);assert.equal(saved.unwanted,undefined);assert.equal(saved.placement,'left-hand');
  p.samples[1].t=p.samples[0].t;
  assert.equal((await fetch(url,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(p)})).status,400);
  assert.equal((await fetch(url)).status,405);
  const circle=session();circle.label='circle';circle.circleProfile='circle-v1';circle.samples.forEach(r=>Object.assign(r,{ra:10,rb:-15,rg:20,oa:null,ob:30,og:-20}));
  const cr=await fetch(url,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(circle)});assert.equal(cr.status,201);
  const receipt=await cr.json(),stored=JSON.parse(await readFile(join(dir,receipt.id+'.json'),'utf8'));assert.equal(stored.samples[0].rb,-15);assert.equal(stored.samples[0].oa,null);assert.equal(stored.circleProfile,'circle-v1');assert.equal(stored.summary.circle.count,0);
  circle.label='wrist';assert.ok(validExperiment(circle));circle.samples[0].ra='unknown';assert.equal(validExperiment(circle),false);
});
test('candidate combines acceleration and rotation peaks across 200ms, rejects impacts and unavailable gyro',()=>{
  const run=rows=>{const d=new MotionClassifier();return rows.flatMap(([t,a,gyro])=>d.update({t,a,gyro,g:1}));};
  assert.equal(run([[0,0,0],[50,14,90],[120,3,210]]).filter(e=>e.kind==='shake').length,1);
  assert.equal(run([[0,0,0],[50,20,90],[100,3,60]]).length,0);
  assert.equal(run([[0,0,null],[50,20,null],[100,3,null]]).length,0);
  // A sensor gap must not join an old acceleration peak with a new rotation peak.
  assert.equal(run([[0,14,80],[180,1,210]]).length,0);
});
test('candidate jump requires landing and low rotation, rejects squat-like weaker impact and shake rotation',()=>{
  const run=(gyro,landing)=>{const d=new MotionClassifier();const rows=[[0,1],[20,.7],[40,.6],[60,.6],[80,.6],[100,landing]];return rows.flatMap(([t,g])=>d.update({t,g,a:0,gyro}));};
  assert.equal(run(80,2.3).filter(e=>e.kind==='jump').length,1);
  assert.equal(run(230,2.3).filter(e=>e.kind==='jump').length,0);
  assert.equal(run(80,1.9).filter(e=>e.kind==='jump').length,0);
  assert.equal(run(null,2.3).length,0);
});
test('circle episode needs sustained motion, rotation and ratio, emits after quiet only once',()=>{
  const d=new CircleDetector(),events=[];
  for(let t=0;t<=1000;t+=20)assert.equal(d.update({t,a:22,gyro:350}),null);
  for(let t=1020;t<=1400;t+=20){const e=d.update({t,a:0,gyro:0});if(e)events.push(e);}
  assert.equal(events.length,1);assert.equal(events[0].timestampMs,1200);assert.equal(events[0].kind,'circle');assert.equal(events[0].value,1);
  const run=(duration,gyro,missing=false)=>{const x=new CircleDetector(),out=[];for(let t=0;t<=duration+300;t+=20){const e=x.update({t,a:t<=duration?22:0,gyro:t<=duration?(missing&&t===500?null:gyro):0});if(e)out.push(e);}return out;};
  assert.equal(run(500,800).length,0); // fast short shake, enough angular travel but too short
  assert.equal(run(1000,650).length,0); // wrist-like high rotation, insufficient ratio
  assert.equal(run(1000,350,true).length,0); // unavailable gyro breaks the episode
  assert.equal(run(4000,350).length,0); // not a counter for indefinite continuous rotation
});
