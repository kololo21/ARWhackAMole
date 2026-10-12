import {randomBytes} from 'node:crypto';
import {once} from 'node:events';
import assert from 'node:assert/strict';
import {WebSocket} from 'ws';
import {emptySensors} from '../public/sensors.js';
const base=new URL(process.argv[2]), room=randomBytes(12).toString('hex'), sockets=[];
if(base.protocol!=='https:')throw new Error('Expected HTTPS');
const health=await fetch(new URL('/health',base),{signal:AbortSignal.timeout(15000)});
assert.equal(health.status,200);assert.equal((await health.json()).ok,true);
const url=new URL('/ws',base);url.protocol='wss:';
const until=async fn=>{const start=Date.now();while(!fn()){if(Date.now()-start>15000)throw new Error('Public WebSocket timeout');await new Promise(r=>setTimeout(r,20));}};
async function join(role,player){const ws=new WebSocket(url);sockets.push(ws);ws.messages=[];ws.on('message',data=>ws.messages.push(JSON.parse(data)));ws.on('error',()=>{});await once(ws,'open');ws.send(JSON.stringify({type:'join',role,player,room}));await until(()=>ws.messages.some(m=>m.type==='joined'));return ws;}
try{
  const receiver=await join('receiver');
  for(const player of ['red','blue']){
    const ws=await join('sender',player), now=Date.now(),sensors=emptySensors();
    sensors.acceleration={valid:true,timestampMs:now,x:0,y:1,z:2};
    ws.send(JSON.stringify({type:'sample',version:1,seq:0,timestampMs:now,simulated:true,sensors,gestures:[{kind:'shake',value:15,timestampMs:now}]}));
  }
  await until(()=>receiver.messages.filter(m=>m.type==='sample').length===2);
  const samples=receiver.messages.filter(m=>m.type==='sample');assert.deepEqual(samples.map(p=>p.player).sort(),['blue','red']);assert.ok(samples.every(p=>p.simulated&&p.gestures[0].kind==='shake'));
  console.log('PUBLIC_SMOKE_PASSED: trusted HTTPS + WSS + red/blue samples + gestures');
}finally{sockets.forEach(ws=>ws.close());}
