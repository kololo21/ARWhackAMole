// Isolated, explicitly synthetic senders for Sensor Game/Run Integration Verification.
import {createRequire} from 'node:module';
import {join} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=process.env.SENSOR_WEB_ROOT||fileURLToPath(new URL('../SensorSendingWeb/',import.meta.url));
const require=createRequire(join(root,'package.json')),{WebSocket}=require('ws');
const room='whack-integration-check-123456',clients=[];
const fields={acceleration:['x','y','z'],gravityAcceleration:['x','y','z'],rotationRate:['alpha','beta','gamma'],orientation:['alpha','beta','gamma','relativeBeta','relativeGamma'],compass:['degrees'],light:['lux'],cameraBrightness:['value'],microphone:['rms'],touch:['x','y','pressure','count']};
for(const player of ['red','blue']){
  const ws=new WebSocket('ws://127.0.0.1:8787/ws');let seq=0,ready=false,tilt=0,voiceFrame=0;
  ws.on('open',()=>ws.send(JSON.stringify({type:'join',role:'sender',room,player})));
  ws.on('message',data=>{const p=JSON.parse(data);if(p.type==='joined')ready=true;if(p.type==='feedback'){console.log(player,p.kind);if(p.kind==='test'&&p.message==='verify-tilt')tilt=40;if(p.kind==='test'&&p.message==='verify-neutral')tilt=0;if(p.kind==='test'&&p.message==='verify-voice')voiceFrame=1;ws.send(JSON.stringify({type:'feedbackAck',id:p.id,enabled:true,screenVisible:true,vibrationSupported:false,vibrationAccepted:false}));}});
  const timer=setInterval(()=>{
    if(!ready||ws.readyState!==1)return;const now=Date.now(),sensors={};
    for(const [key,values] of Object.entries(fields))sensors[key]={valid:false,timestampMs:0,...Object.fromEntries(values.map(v=>[v,null]))};
    sensors.acceleration={valid:true,timestampMs:now,x:player==='red'?6:80,y:0,z:0};
    sensors.orientation={valid:true,timestampMs:now,alpha:0,beta:tilt,gamma:0,relativeBeta:tilt,relativeGamma:0};
    const voice=voiceFrame===5;if(voiceFrame>0)voiceFrame=voiceFrame>=6?0:voiceFrame+1;
    sensors.microphone={valid:true,timestampMs:now,rms:voice?.3:.01};
    const cycle=seq>0&&seq%(player==='red'?16:24)===0;
    sensors.motionGame={valid:true,timestampMs:now,windowMs:50,peakAcceleration:player==='red'?6:80,circleSeconds:.05,circleActive:true,linear:true,shakeVersion:1,shakeCount:cycle?1:0,shakeAgeMs:0,shakeSpanMs:cycle?800:0};
    ws.send(JSON.stringify({type:'sample',version:1,seq:seq++,timestampMs:now,simulated:true,sensors,gestures:voice?[{kind:'voice',value:.3,timestampMs:now}]:[]}));
  },50);
  ws.on('error',e=>console.error(e.message));clients.push({ws,timer});
}
process.on('SIGINT',()=>{for(const {ws,timer} of clients){clearInterval(timer);ws.terminate();}process.exit();});
console.log('WHACK_VERIFICATION_SENDERS: separate room, synthetic data');
