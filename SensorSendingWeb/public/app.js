import {Connection,fmt} from './transport.js';
import {Sensors} from './sensors.js';
import {plot,appendEvents} from './charts.js';
import {Feedback} from './feedback.js';
const $=id=>document.getElementById(id), params=new URLSearchParams(location.hash.slice(1));
$('room').value=params.get('room')??'';if(['red','blue'].includes(params.get('player'))) $('player').value=params.get('player');
$('secureNotice').hidden=isSecureContext;
$('labLink').href='/lab#'+new URLSearchParams({room:$('room').value,player:$('player').value});
const feedback=new Feedback(text=>{$('feedbackStatus').textContent=text;});
$('camera').disabled=true;$('microphone').disabled=true;
const statusIds={motion:'motionStatus',orientation:'orientationStatus',light:'lightStatus',cameraBrightness:'cameraStatus',microphone:'microphoneStatus'};
let sensors=new Sensors((name,state)=>{$(statusIds[name]).textContent=state;}),running=false,seq=0,sent=0,timer,wake,lastPing=0,history=[];
const statuses={connecting:'接続中…',connected:'送信できます',reconnecting:'再接続中…',disconnected:'未接続',occupied:'このチームは使用中',invalid:'ルーム・送信形式を確認'};
let cueTimer;
const connection=new Connection(p=>{
  if(p.type==='pong')$('rtt').textContent=fmt(Date.now()-p.timestampMs,0);
  if(p.type==='feedback'){
    const enabled=$('feedbackEnabled').checked;
    const result=enabled?feedback.show(p.kind):null;
    if(enabled&&!document.hidden){$('gameCue').hidden=false;$('gameCue').textContent=p.message||({warning:'まもなく瞬間出現！',contest:'5秒間、スマホを振ってください！',countdown:'シェイク勝負の準備'})[p.kind]||'Unityからの合図';$('gameCue').dataset.kind=p.kind;clearTimeout(cueTimer);cueTimer=setTimeout(()=>{$('gameCue').hidden=true;},Math.min(p.durationMs||1500,15000));}
    if(p.id)connection.send({type:'feedbackAck',id:p.id,enabled,screenVisible:!document.hidden,vibrationSupported:typeof navigator.vibrate==='function',vibrationAccepted:!!result?.vibrationAccepted});
  }
},state=>{
  $('connection').textContent=statuses[state];$('connection').dataset.state=state;
  if(state==='occupied') $('message').textContent='同じルーム・チームの別端末を停止してから接続してください。';
  if(['occupied','invalid'].includes(state)) { stop(); $('connection').textContent=statuses[state]; }
});
const vector=s=>s?.valid?`${fmt(s.x)} / ${fmt(s.y)} / ${fmt(s.z)}`:'—';
function render(s) {
  const a=s.acceleration;const mag=a.valid?Math.hypot(a.x,a.y,a.z):null;
  $('accMagnitude').textContent=fmt(mag);$('acc').textContent=vector(a);$('gravity').textContent=vector(s.gravityAcceleration);
  $('gyro').textContent=s.rotationRate.valid?['alpha','beta','gamma'].map(k=>fmt(s.rotationRate[k],1)).join(' / '):'—';
  $('tilt').textContent=s.orientation.valid?`${fmt(s.orientation.relativeBeta,1)} / ${fmt(s.orientation.relativeGamma,1)}`:'—';
  $('phoneShape').style.transform=s.orientation.valid?`rotate(${s.orientation.relativeGamma}deg)`:'none';
  $('compass').textContent=s.compass.valid?`${fmt(s.compass.degrees,0)}°`:'—';$('lux').textContent=s.light.valid?fmt(s.light.lux,0):'—';
  $('brightness').textContent=s.cameraBrightness.valid?fmt(s.cameraBrightness.value*100,1):'—';$('rms').textContent=s.microphone.valid?fmt(s.microphone.rms,3):'—';
  $('touch').textContent=s.touch.valid?`X ${fmt(s.touch.x)} / Y ${fmt(s.touch.y)} / 圧力 ${fmt(s.touch.pressure)} / ${s.touch.count} 点`:'タッチなし';
  history.push(mag);if(history.length>120)history.shift();plot($('accPlot'),history);
}
async function keepAwake(){try{if(running && !document.hidden && navigator.wakeLock)wake=await navigator.wakeLock.request('screen');}catch{/* optional */}}
function stop() {
  running=false;clearInterval(timer);connection.stop();sensors.stop();wake?.release().catch(()=>{});wake=null;
  ['room','player','rate','mode','start'].forEach(id=>$(id).disabled=false);$('stop').disabled=true;$('calibrate').disabled=true;
  $('camera').disabled=true;$('microphone').disabled=true;
  Object.values(statusIds).forEach(id=>$(id).textContent='停止中');render(sensors.values);
}
$('start').onclick=()=>{
  if(!/^[a-zA-Z0-9_-]{16,64}$/.test($('room').value)) { $('message').textContent='PCモニターで作成した16文字以上のルームキーを入力してください。';return; }
  if($('mode').value==='real'&&!isSecureContext){$('message').textContent='実センサーにはHTTPS接続が必要です。';return;}
  const thresholds={...sensors.thresholds};
  sensors=new Sensors((name,state)=>{$(statusIds[name]).textContent=state;});sensors.thresholds=thresholds;
  sensors.jumpEnabled=$('jumpEnabled').checked;
  sensors.circleEnabled=$('circleEnabled').checked;
  running=true;sent=0;seq=0;history=[];$('message').textContent='センサーの権限が拒否された場合も、取得できる入力を送信します。';
  ['room','player','rate','mode','start'].forEach(id=>$(id).disabled=true);$('stop').disabled=false;$('calibrate').disabled=false;
  const simulated=$('mode').value==='sim';$('simNotice').hidden=!simulated;$('simActions').hidden=!simulated;
  $('camera').disabled=simulated;$('microphone').disabled=simulated;
  // start() reaches permission calls before any await in this click handler.
  sensors.start(simulated).catch(e=>{$('message').textContent=e.message;});
  if($('feedbackEnabled').checked)feedback.enable();
  connection.connect({role:'sender',room:$('room').value,player:$('player').value});keepAwake();
  timer=setInterval(()=>{
    if(document.hidden||!running)return;
    const s=sensors.sample(),gestures=sensors.drainEvents();render(s);appendEvents($('events'),gestures,simulated);
    const packet={type:'sample',version:1,seq:seq++,timestampMs:Date.now(),simulated,sensors:s,gestures};
    if(connection.send(packet)){$('sent').textContent=++sent;$('json').textContent=JSON.stringify(packet,null,2);}
    if(Date.now()-lastPing>2000){lastPing=Date.now();connection.send({type:'ping',timestampMs:Date.now()});}
  },1000/Number($('rate').value));
};
$('stop').onclick=stop;
$('jumpEnabled').onchange=()=>{sensors.jumpEnabled=$('jumpEnabled').checked;sensors.jumpDetector.reset();};
$('circleEnabled').onchange=()=>{sensors.circleEnabled=$('circleEnabled').checked;sensors.circleDetector.reset();};
$('feedbackEnabled').onchange=()=>{if($('feedbackEnabled').checked)feedback.enable();};
$('feedbackTest').onclick=()=>feedback.test();
$('calibrate').onclick=()=>{$('message').textContent=sensors.calibrate()?'今の姿勢を傾きの基準に設定しました。':'姿勢のデータがまだ取得できていません。';};
for(const [id,method] of [['microphone','enableMicrophone'],['camera','enableCamera']]) $(id).onclick=async()=>{
  $(id).disabled=true;
  const source=sensors;
  try{await source[method]();}catch(e){if(source!==sensors||!running)return;$(id).disabled=false;$(id==='camera'?'cameraStatus':'microphoneStatus').textContent=`利用できません: ${e.message}`;}
};
for(const [kind,unit] of [['shake',' m/s²'],['tilt','°'],['voice',' RMS']]) $(kind+'Threshold').oninput=e=>{sensors.thresholds[kind]=Number(e.target.value);$(kind+'Value').textContent=e.target.value+unit;};
document.querySelectorAll('[data-gesture]').forEach(el=>el.onclick=()=>{if(running&&sensors.simulated)sensors.events.push({kind:el.dataset.gesture,value:sensors.thresholds[el.dataset.gesture]+1,timestampMs:Date.now()});});
const pad=$('touchpad'), pointers=new Set();
pad.onpointerdown=e=>{e.preventDefault();pad.setPointerCapture(e.pointerId);pointers.add(e.pointerId);const r=pad.getBoundingClientRect();sensors.touch((e.clientX-r.left)/r.width,(e.clientY-r.top)/r.height,e.pressure,pointers.size);};
pad.onpointermove=e=>{if(!pointers.has(e.pointerId))return;const r=pad.getBoundingClientRect();sensors.set('touch',{x:Math.max(0,Math.min(1,(e.clientX-r.left)/r.width)),y:Math.max(0,Math.min(1,(e.clientY-r.top)/r.height)),pressure:e.pressure,count:pointers.size});};
pad.onpointerup=pad.onpointercancel=e=>{pointers.delete(e.pointerId);if(running)sensors.set('touch',{x:0,y:0,pressure:0,count:pointers.size});};
pad.onkeydown=e=>{if(e.key===' '||e.key==='Enter'){e.preventDefault();sensors.touch(0.5,0.5,0,1);}};
document.addEventListener('visibilitychange',()=>{sensors.pause();if(document.hidden){wake?.release().catch(()=>{});wake=null;}else keepAwake();});
window.addEventListener('pagehide',stop);
