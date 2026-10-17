import test from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';
import {once} from 'node:events';
import {WebSocket} from 'ws';
import {attachBridge,validPacket} from '../server/bridge.js';
import {emptySensors,GestureDetector,Sensors} from '../public/sensors.js';
import {applyCalibration} from '../public/controller-command.js';

function packet(seq=0){const sensors=emptySensors();sensors.acceleration={valid:true,timestampMs:Date.now(),x:0,y:0,z:0};return {type:'sample',version:1,seq,timestampMs:Date.now(),simulated:false,sensors,gestures:[]};}
async function server(t){const s=http.createServer(),wss=attachBridge(s);s.listen(0,'127.0.0.1');await once(s,'listening');t.after(async()=>{wss.clients.forEach(w=>w.terminate());await new Promise(r=>wss.close(r));await new Promise(r=>s.close(r));});return `ws://127.0.0.1:${s.address().port}/ws`;}
async function connect(url,join){const ws=new WebSocket(url);ws.messages=[];ws.on('message',data=>ws.messages.push(JSON.parse(data)));await once(ws,'open');ws.send(JSON.stringify({type:'join',...join}));return ws;}
async function until(fn){const start=Date.now();while(!fn()){if(Date.now()-start>2500)throw new Error('Timed out');await new Promise(r=>setTimeout(r,10));}}
const room='test-room-key-123456';
test('calibration applies once, requires a live real sender and reports unavailable orientation',()=>{
  let calls=0;const seen=new Set(),sensors={active:true,simulated:false,calibrate:()=>{calls++;return true;}};
  assert.equal(applyCalibration({kind:'calibrate',id:'first'},sensors,seen),true);
  assert.equal(applyCalibration({kind:'calibrate',id:'first'},sensors,seen),false);assert.equal(calls,1);
  sensors.active=false;assert.equal(applyCalibration({kind:'calibrate',id:'stopped'},sensors,seen),false);
  sensors.active=true;sensors.calibrate=()=>false;assert.equal(applyCalibration({kind:'calibrate',id:'missing'},sensors,seen),false);
});
test('Unity calibration command targets one phone and relays applied acknowledgement with sender identity',async t=>{
  const url=await server(t),receiver=await connect(url,{role:'receiver',room});
  const red=await connect(url,{role:'sender',room,player:'red'}),blue=await connect(url,{role:'sender',room,player:'blue'});
  await until(()=>blue.messages.some(m=>m.type==='joined'));
  receiver.send(JSON.stringify({type:'feedback',player:'red',kind:'calibrate',id:'calibration-red',durationMs:0}));
  await until(()=>red.messages.some(m=>m.kind==='calibrate'));
  assert.equal(blue.messages.some(m=>m.kind==='calibrate'),false);
  red.send(JSON.stringify({type:'feedbackAck',id:'calibration-red',player:'blue',enabled:true,screenVisible:true,vibrationSupported:false,vibrationAccepted:false,applied:true}));
  await until(()=>receiver.messages.some(m=>m.type==='feedbackAck'));
  const ack=receiver.messages.find(m=>m.type==='feedbackAck');assert.equal(ack.player,'red');assert.equal(ack.applied,true);
});
test('schema preserves valid zero vs unavailable; rejects malformed input',()=>{
  const p=packet();assert.equal(validPacket(p),true);assert.equal(p.sensors.light.valid,false);
  p.sensors.orientation={valid:true,timestampMs:Date.now(),alpha:null,beta:0,gamma:0,relativeBeta:0,relativeGamma:0};assert.equal(validPacket(p),true);
  p.sensors.acceleration.x=Infinity;assert.equal(validPacket(p),false);p.sensors.acceleration.x=0;p.gestures=[{kind:'score',value:1,timestampMs:0}];assert.equal(validPacket(p),false);
});
test('gesture detector uses rising edges, hysteresis and cooldown',()=>{
  const d=new GestureDetector();assert.equal(d.update('shake',0,12,0),null);assert.ok(d.update('shake',15,12,1));assert.equal(d.update('shake',20,12,900),null);
  d.update('shake',0,12,1000);assert.ok(d.update('shake',15,12,1001));d.update('shake',0,12,1050);assert.equal(d.update('shake',15,12,1100),null);
});
test('resting phone orientation becomes neutral before tilt gameplay',()=>{
  const previous=globalThis.document;
  globalThis.document={hidden:false,createElement:tag=>tag==='canvas'?{getContext:()=>({})}:{}};
  try {
    const sensors=new Sensors(()=>{});sensors.active=true;sensors.simulated=false;
    sensors.handleOrientation({alpha:0,beta:80,gamma:12});
    assert.equal(sensors.values.orientation.relativeBeta,0);assert.equal(sensors.values.orientation.relativeGamma,0);
    sensors.handleOrientation({alpha:0,beta:115,gamma:12});assert.equal(sensors.values.orientation.relativeBeta,35);
    assert.equal(sensors.calibrate(),true);sensors.handleOrientation({alpha:0,beta:115,gamma:12});assert.equal(sensors.values.orientation.relativeBeta,0);
  } finally {if(previous===undefined)delete globalThis.document;else globalThis.document=previous;}
});
test('red/blue relay, isolation, sender identity, duplicates, snapshot without gesture replay',async t=>{
  const url=await server(t),receiver=await connect(url,{role:'receiver',room}),other=await connect(url,{role:'receiver',room:'different-room-key-1234'});
  const red=await connect(url,{role:'sender',player:'red',room}),blue=await connect(url,{role:'sender',player:'blue',room});
  await until(()=>blue.messages.some(m=>m.type==='joined'));
  const p=packet();p.player='blue';p.gestures=[{kind:'shake',value:15,timestampMs:p.timestampMs}];red.send(JSON.stringify(p));blue.send(JSON.stringify(packet()));
  await until(()=>receiver.messages.filter(m=>m.type==='sample').length===2);
  const samples=receiver.messages.filter(m=>m.type==='sample');assert.deepEqual(samples.map(s=>s.player).sort(),['blue','red']);assert.equal(samples.find(s=>s.player==='red').gestures[0].kind,'shake');assert.equal(other.messages.some(s=>s.type==='sample'),false);
  const next=await connect(url,{role:'receiver',room});await until(()=>next.messages.filter(m=>m.type==='sample').length===2);assert.ok(next.messages.filter(m=>m.type==='sample').every(s=>s.gestures.length===0));
  red.send(JSON.stringify(p));red.send(JSON.stringify(packet(1)));await until(()=>receiver.messages.filter(m=>m.type==='sample').length===3);assert.equal(receiver.messages.filter(m=>m.type==='sample').length,3);
  red.close();await until(()=>receiver.messages.some(m=>m.type==='presence'&&m.players.some(x=>x.player==='red'&&!x.connected)));
  const afterClose=await connect(url,{role:'receiver',room});await until(()=>afterClose.messages.some(m=>m.type==='joined'));await new Promise(r=>setTimeout(r,40));assert.equal(afterClose.messages.filter(m=>m.type==='sample'&&m.player==='red').length,0);
});
test('duplicate player rejected, receive-only role cannot send samples',async t=>{
  const url=await server(t),red=await connect(url,{role:'sender',player:'red',room});await until(()=>red.messages.some(m=>m.type==='joined'));
  const dup=await connect(url,{role:'sender',player:'red',room});const [code]=await once(dup,'close');assert.equal(code,4009);
  const viewer=await connect(url,{role:'receiver',room});await until(()=>viewer.messages.some(m=>m.type==='joined'));viewer.send(JSON.stringify(packet()));const [close]=await once(viewer,'close');assert.equal(close,1008);
});
test('invalid room and malformed packets are rejected; ping works',async t=>{
  const url=await server(t),bad=await connect(url,{role:'receiver',room:'short'});assert.equal((await once(bad,'close'))[0],1008);
  const red=await connect(url,{role:'sender',player:'red',room});await until(()=>red.messages.some(m=>m.type==='joined'));red.send(JSON.stringify({type:'ping',timestampMs:123}));await until(()=>red.messages.some(m=>m.type==='pong'));assert.equal(red.messages.find(m=>m.type==='pong').timestampMs,123);
  red.send(JSON.stringify({type:'sample'}));assert.equal((await once(red,'close'))[0],1008);
});
test('jump relay and receiver feedback target only the chosen player in the same room',async t=>{
  const url=await server(t),receiver=await connect(url,{role:'receiver',room});
  const red=await connect(url,{role:'sender',room,player:'red'}),blue=await connect(url,{role:'sender',room,player:'blue'});
  const other=await connect(url,{role:'sender',room:'other-experiment-room-123',player:'red'});
  await until(()=>red.messages.some(p=>p.type==='joined')&&blue.messages.some(p=>p.type==='joined'));
  const p=packet();p.gestures=[{kind:'jump',value:2.1,timestampMs:p.timestampMs}];red.send(JSON.stringify(p));
  await until(()=>receiver.messages.some(p=>p.type==='sample'&&p.gestures[0]?.kind==='jump'));
  const circle=packet(1);circle.gestures=[{kind:'circle',value:1,timestampMs:circle.timestampMs}];red.send(JSON.stringify(circle));
  await until(()=>receiver.messages.some(p=>p.type==='sample'&&p.gestures[0]?.kind==='circle'));
  receiver.send(JSON.stringify({type:'feedback',player:'red',kind:'hit'}));
  await until(()=>red.messages.some(p=>p.type==='feedback'));assert.equal(blue.messages.some(p=>p.type==='feedback'),false);assert.equal(other.messages.some(p=>p.type==='feedback'),false);
  red.send(JSON.stringify({type:'feedback',player:'blue',kind:'hit'}));assert.equal((await once(red,'close'))[0],1008);
});
test('dual-phone warning and acknowledgement routing; snapshot cannot replay earned charge',async t=>{
  const url=await server(t),receiver=await connect(url,{role:'receiver',room});
  const red=await connect(url,{role:'sender',room,player:'red'}),blue=await connect(url,{role:'sender',room,player:'blue'});
  await until(()=>blue.messages.some(m=>m.type==='joined'));
  for(const player of ['red','blue'])receiver.send(JSON.stringify({type:'feedback',player,kind:'warning',id:'warning-'+player,message:'まもなく出現',durationMs:1500}));
  await until(()=>red.messages.some(m=>m.id==='warning-red')&&blue.messages.some(m=>m.id==='warning-blue'));
  blue.send(JSON.stringify({type:'feedbackAck',id:'warning-blue',player:'red',enabled:true,screenVisible:true,vibrationSupported:false,vibrationAccepted:false}));
  await until(()=>receiver.messages.some(m=>m.type==='feedbackAck'));assert.equal(receiver.messages.find(m=>m.type==='feedbackAck').player,'blue');
  const p=packet();p.sensors.motionGame={valid:true,timestampMs:p.timestampMs,windowMs:50,peakAcceleration:40,circleSeconds:.05,circleActive:true,circleVersion:1,circleDegrees:18,linear:true,shakeVersion:1,shakeCount:1,shakeAgeMs:10,shakeSpanMs:800};red.send(JSON.stringify(p));
  await until(()=>receiver.messages.some(m=>m.type==='sample'));assert.equal(receiver.messages.find(m=>m.type==='sample').sensors.motionGame.circleSeconds,.05);
  assert.equal(receiver.messages.find(m=>m.type==='sample').sensors.motionGame.shakeCount,1);
  assert.equal(receiver.messages.find(m=>m.type==='sample').sensors.motionGame.circleDegrees,18);
  const late=await connect(url,{role:'receiver',room});await until(()=>late.messages.some(m=>m.type==='sample'));const snapshot=late.messages.find(m=>m.type==='sample').sensors.motionGame;assert.equal(snapshot.circleSeconds,0);assert.equal(snapshot.circleDegrees,0);assert.equal(snapshot.shakeCount,0);assert.equal(snapshot.shakeAgeMs,0);
  p.sensors.motionGame.shakeCount=2;assert.equal(validPacket(p),false);
  p.sensors.motionGame.shakeCount=1;p.sensors.motionGame.shakeSpanMs=300;assert.equal(validPacket(p),true);
  p.sensors.motionGame.shakeSpanMs=299;assert.equal(validPacket(p),false);
});
test('rotation packet rejects malformed increments while legacy charge packets remain valid',()=>{
  const p=packet(),g={valid:true,timestampMs:p.timestampMs,windowMs:50,peakAcceleration:22,circleSeconds:.05,circleActive:true,linear:true};p.sensors.motionGame=g;
  assert.equal(validPacket(p),true,'legacy phone may still charge');
  g.circleVersion=1;g.circleDegrees=18;assert.equal(validPacket(p),true);
  for(const degrees of [-1,NaN,Infinity,501]){g.circleDegrees=degrees;assert.equal(validPacket(p),false);}
  g.circleDegrees=18;g.circleVersion=2;assert.equal(validPacket(p),false);
  g.circleVersion=1;g.circleSeconds=0;assert.equal(validPacket(p),false,'no rotation without qualified active time');
});
