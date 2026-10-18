import test from 'node:test';
import assert from 'node:assert/strict';
import {Sensors} from '../public/sensors.js';

function globals(t,values){
  for(const [key,value] of Object.entries(values)){
    const original=Object.getOwnPropertyDescriptor(globalThis,key);
    Object.defineProperty(globalThis,key,{value,configurable:true,writable:true});
    t.after(()=>original?Object.defineProperty(globalThis,key,original):delete globalThis[key]);
  }
}
const source=()=>Object.assign(Object.create(Sensors.prototype),{active:true,simulated:false,onStatus:()=>{}});

test('failed microphone initialization releases its stream and permits a real retry',async t=>{
  let requests=0,closed=0,attempts=0;const tracks=[];
  class Audio {
    async resume(){if(++attempts===1)throw new Error('audio startup failed');}
    async close(){closed++;}
    createAnalyser(){return {};}
    createMediaStreamSource(){return {connect:()=>{}};}
  }
  globals(t,{window:{AudioContext:Audio},navigator:{mediaDevices:{getUserMedia:async()=>{
    requests++;const track={stopped:false,stop(){this.stopped=true;}};tracks.push(track);
    return {getTracks:()=>[track]};
  }}}});
  const sensors=source();
  await assert.rejects(sensors.enableMicrophone(),/audio startup failed/);
  assert.equal(tracks[0].stopped,true,'the failed attempt must release the microphone');
  assert.equal(sensors.audioStream,null);assert.equal(sensors.audio,null);assert.equal(sensors.analyser,null);assert.equal(closed,1);
  await sensors.enableMicrophone();
  assert.equal(requests,2,'retry requests a new stream instead of treating the broken stream as ready');
  assert.ok(sensors.analyser);assert.equal(tracks[1].stopped,false);
});

test('stopping while microphone permission is pending closes the eventual stream',async t=>{
  let grant,stopped=false;
  globals(t,{navigator:{mediaDevices:{getUserMedia:()=>new Promise(resolve=>{grant=resolve;})}}});
  const sensors=source(),pending=sensors.enableMicrophone();sensors.active=false;
  grant({getTracks:()=>[{stop:()=>{stopped=true;}}]});await pending;
  assert.equal(stopped,true);assert.equal(sensors.audioStream,undefined);
});
