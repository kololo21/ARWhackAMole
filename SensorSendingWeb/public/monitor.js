import {Connection,fmt} from './transport.js';
import {plot,appendEvents} from './charts.js';
const $=id=>document.getElementById(id), players={};
const newKey=()=>Array.from(crypto.getRandomValues(new Uint8Array(12)),x=>x.toString(16).padStart(2,'0')).join('');
let stored;try{stored=sessionStorage.getItem('sensorRoom');}catch{}
$('room').value=new URLSearchParams(location.hash.slice(1)).get('room')??stored??newKey();
for(const [player,name,color] of [['red','赤チーム','#d76061'],['blue','青チーム','#5980d5']]) {
  const card=document.createElement('section');card.className=`card team-${player}`;
  card.innerHTML=`<div class="team-title"><h2>${name}</h2><span class="pill player-status">待機中</span></div><div class="sub source">入力なし</div><div class="hero-num readout magnitude">—</div><div class="sub">加速度の大きさ / m/s²</div><canvas class="plot" width="600" height="90" aria-label="加速度の推移"></canvas><div class="metrics"><div class="metric"><label>前後 / 左右の傾き</label><div class="readout tilt">—</div></div><div class="metric"><label>回転 α / β / γ（deg/s）</label><div class="readout gyro">—</div></div><div class="metric"><label>マイク音量 / RMS</label><div class="readout rms">—</div></div><div class="metric"><label>照度 / lux</label><div class="readout lux">—</div></div><div class="metric"><label>カメラ輝度 / %</label><div class="readout brightness">—</div></div><div class="metric"><label>方位 / deg</label><div class="readout compass">—</div></div><div class="metric"><label>タッチ X / Y</label><div class="readout touch">—</div></div></div><div class="events telemetry"></div><div class="status age">未受信</div><details><summary>受信JSON</summary><pre class="json">未受信</pre></details>`;
  $('players').append(card);players[player]={card,color,history:[],last:0,connected:false};
}
const connection=new Connection(p=>{
  if(p.type==='presence')p.players.forEach(x=>{if(players[x.player])players[x.player].connected=x.connected;});
  if(p.type==='sample'&&players[p.player]) {const state=players[p.player];state.last=Date.now();state.packet=p;appendEvents(state.card.querySelector('.events'),p.gestures,p.simulated);}
},status=>{$('connection').textContent=({connected:'受信中',connecting:'接続中…',reconnecting:'再接続中…',disconnected:'未接続',invalid:'ルームキーが不正'})[status]??status;$('connection').dataset.state=status;if(status!=='connected')Object.values(players).forEach(s=>{s.connected=false;s.last=0;});});
function connect(){if(!/^[a-zA-Z0-9_-]{16,64}$/.test($('room').value)){$('message').textContent='ルームキーは英数字・_・-の16〜64文字です。';return;}try{sessionStorage.setItem('sensorRoom',$('room').value);}catch{}Object.values(players).forEach(s=>{s.packet=null;s.last=0;s.history=[];s.card.querySelector('.events').replaceChildren();});location.hash=new URLSearchParams({room:$('room').value});connection.connect({role:'receiver',room:$('room').value});}
function qr(){
  let base;try{base=new URL($('baseUrl').value);if(base.protocol!=='https:')throw new Error();}catch{$('message').textContent='https://で始まるスマホから開けるURLを入力してください。';return;}
  const grid=$('qrGrid');grid.replaceChildren();grid.hidden=false;$('httpsNotice').hidden=true;
  for(const [player,name]of[['red','赤チーム'],['blue','青チーム']]){const url=new URL('/',base);url.hash=new URLSearchParams({room:$('room').value,player});const div=document.createElement('div'),title=document.createElement('b'),img=document.createElement('img'),a=document.createElement('a');title.textContent=name;img.alt=`${name}のスマホ接続用QRコード`;img.src='/api/qr?url='+encodeURIComponent(url.href);a.href=url.href;a.textContent='スマホ送信画面を開く ↗';a.className='url';a.target='_blank';a.rel='noopener';div.append(title,img,a);grid.append(div);}
  $('message').textContent='スマホでQRコードを読み取り、センサーを許可して送信してください。';
}
$('connect').onclick=()=>{connect();if($('baseUrl').value)qr();};$('newRoom').onclick=()=>{$('room').value=newKey();connect();if($('baseUrl').value)qr();};
$('copy').onclick=async()=>{try{await navigator.clipboard.writeText($('room').value);$('message').textContent='ルームキーをコピーしました。';}catch{$('room').select();$('message').textContent='ルームキーを選択しました。手動でコピーしてください。';}};
$('qrUpdate').onclick=qr;if(location.protocol==='https:'){$('baseUrl').value=location.origin;qr();}connect();
setInterval(()=>Object.values(players).forEach(state=>{
  const get=cls=>state.card.querySelector('.'+cls),p=state.packet,age=Date.now()-state.last,active=state.connected&&age<2000,s=p?.sensors;
  const valid=name=>active&&s?.[name]?.valid&&p.timestampMs-s[name].timestampMs<1000;
  get('player-status').textContent=!state.connected?'未接続':!active?'データ待ち':'受信中';get('player-status').dataset.state=active?'connected':'disconnected';
  get('source').textContent=p?.simulated?'合成データ / シミュレーター':'実センサー';get('source').classList.toggle('error',!!p?.simulated);
  const mag=valid('acceleration')?Math.hypot(s.acceleration.x,s.acceleration.y,s.acceleration.z):null;get('magnitude').textContent=fmt(mag);
  get('tilt').textContent=valid('orientation')?`${fmt(s.orientation.relativeBeta,1)}° / ${fmt(s.orientation.relativeGamma,1)}°`:'—';
  get('gyro').textContent=valid('rotationRate')?['alpha','beta','gamma'].map(k=>fmt(s.rotationRate[k],1)).join(' / '):'—';
  for(const [cls,name,key,mult]of[['rms','microphone','rms',1],['lux','light','lux',1],['brightness','cameraBrightness','value',100],['compass','compass','degrees',1]])get(cls).textContent=valid(name)?fmt(s[name][key]*mult,cls==='rms'?3:1):'—';
  get('touch').textContent=valid('touch')?`${fmt(s.touch.x)} / ${fmt(s.touch.y)}`:'—';
  get('age').textContent=state.last?`最終受信 ${fmt(age/1000,1)} 秒前 / #${p.seq}`:'未受信';
  if(p)get('json').textContent=JSON.stringify(p,null,2);state.history.push(mag);if(state.history.length>120)state.history.shift();plot(get('plot'),state.history,state.color);
}),100);
window.addEventListener('pagehide',()=>connection.stop());
