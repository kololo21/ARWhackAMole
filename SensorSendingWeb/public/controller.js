import {Connection} from './transport.js';
import {Sensors} from './sensors.js';
import {applyCalibration} from './controller-command.js';
const $=id=>document.getElementById(id),params=new URLSearchParams(location.hash.slice(1));
$('room').value=params.get('room')??'';
if(['red','blue'].includes(params.get('player')))$('player').value=params.get('player');
$('settings').open=!$('room').value;
$('secureNotice').hidden=isSecureContext;
let sensors=new Sensors(()=>{}),running=false,timer,wake,seq=0,micEnabled=false,connectionState='disconnected';
const calibrationCommands=new Set();
function updateTeam(){const blue=$('player').value==='blue';document.body.dataset.team=blue?'blue':'red';$('teamTitle').textContent=blue?'青チーム':'赤チーム';$('teamMark').textContent=blue?'B':'R';$('diagnostics').href='/diagnostics#'+new URLSearchParams({room:$('room').value,player:$('player').value});}
function renderState(){
  const online=running&&connectionState==='connected';
  $('ready').hidden=!online;$('start').hidden=running;$('microphone').hidden=!running||micEnabled;
  $('guide').textContent=online?'準備ができたら、Unityの画面へ。':running?'接続しています…':'スマホを普段の持ち方で握って、送信を開始してください。';
  $('room').disabled=$('player').disabled=running;$('stop').disabled=!running;
}
const statuses={connecting:'接続中…',connected:'接続済',reconnecting:'再接続中…',disconnected:'未接続',occupied:'チーム使用中',invalid:'設定を確認'};
const connection=new Connection(p=>{
  if(p.type!=='feedback')return;
  const applied=p.kind==='calibrate'?applyCalibration(p,sensors,calibrationCommands):false;
  if(p.id)connection.send({type:'feedbackAck',id:p.id,enabled:true,screenVisible:!document.hidden,vibrationSupported:false,vibrationAccepted:false,applied});
},state=>{
  connectionState=state;$('connection').textContent=statuses[state]??state;$('connection').dataset.state=state;
  if(['occupied','invalid'].includes(state)){stop();$('message').textContent=state==='occupied'?'この色の別端末を停止してから接続してください。':'ルームとチームを確認してください。';}
  renderState();
});
async function keepAwake(){try{if(running&&!document.hidden&&navigator.wakeLock)wake=await navigator.wakeLock.request('screen');}catch{/* optional */}}
function stop(){running=false;clearInterval(timer);connection.stop();sensors.stop();wake?.release().catch(()=>{});wake=null;micEnabled=false;$('microphone').disabled=false;$('message').textContent='送信を停止しました。';renderState();}
$('start').onclick=()=>{
  if(!/^[a-zA-Z0-9_-]{16,64}$/.test($('room').value)){$('settings').open=true;$('message').textContent='PCのQRコードから開くか、ルームキーを確認してください。';return;}
  if(!isSecureContext){$('message').textContent='HTTPSのURLから開いてください。';return;}
  sensors=new Sensors(()=>{});running=true;seq=0;micEnabled=false;calibrationCommands.clear();
  // Permission requests begin inside the original tap (iPhone requirement).
  sensors.start(false).catch(e=>{$('message').textContent=e.message;});
  connection.connect({role:'sender',room:$('room').value,player:$('player').value});keepAwake();
  $('settings').open=false;$('message').textContent='おねむも遊ぶ場合は、続けてマイクを許可してください。';renderState();
  timer=setInterval(()=>{if(!running||document.hidden)return;connection.send({type:'sample',version:1,seq:seq++,timestampMs:Date.now(),simulated:false,sensors:sensors.sample(),gestures:sensors.drainEvents()});},50);
};
$('microphone').onclick=async()=>{
  const source=sensors;$('microphone').disabled=true;
  try{await source.enableMicrophone();if(source!==sensors||!running)return;micEnabled=true;$('message').textContent='マイク準備OK。Unityの案内に合わせて、静かにした後「はい」と言ってください。';renderState();}
  catch(e){if(source!==sensors||!running)return;$('microphone').disabled=false;$('message').textContent='マイクを利用できません：'+e.message;}
};
$('stop').onclick=stop;$('player').onchange=updateTeam;$('room').oninput=updateTeam;
document.addEventListener('visibilitychange',()=>{sensors.pause();if(document.hidden){wake?.release().catch(()=>{});wake=null;}else keepAwake();});
window.addEventListener('pagehide',stop);updateTeam();renderState();
