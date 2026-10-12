import { WebSocketServer, WebSocket } from 'ws';
import {randomUUID} from 'node:crypto';

const object = x => x && typeof x === 'object' && !Array.isArray(x);
const finite = x => typeof x === 'number' && Number.isFinite(x);
export function validPacket(p) {
  if (!object(p) || p.type !== 'sample' || p.version !== 1 || !Number.isSafeInteger(p.seq) || p.seq < 0 || !finite(p.timestampMs) || typeof p.simulated !== 'boolean') return false;
  if (!object(p.sensors) || !Array.isArray(p.gestures) || p.gestures.length > 8) return false;
  const fields = {
    acceleration: ['x','y','z'], gravityAcceleration: ['x','y','z'], rotationRate: ['alpha','beta','gamma'],
    orientation: ['alpha','beta','gamma','relativeBeta','relativeGamma'], compass: ['degrees'],
    light: ['lux'], cameraBrightness: ['value'], microphone: ['rms'], touch: ['x','y','pressure','count'],
  };
  for (const [name, keys] of Object.entries(fields)) {
    const s = p.sensors[name];
    if (!object(s) || typeof s.valid !== 'boolean' || !finite(s.timestampMs)) return false;
    if (keys.some(k => s[k] !== null && !finite(s[k]))) return false;
    if (s.valid && keys.filter(k => !(name === 'orientation' && k === 'alpha')).some(k => s[k] === null)) return false;
  }
  const game=p.sensors.motionGame;
  if(game!==undefined){
    if(!object(game)||typeof game.valid!=='boolean'||!finite(game.timestampMs)||!finite(game.windowMs)||game.windowMs<0||game.windowMs>15000||typeof game.circleActive!=='boolean'||typeof game.linear!=='boolean'||!finite(game.circleSeconds)||game.circleSeconds<0||game.circleSeconds>game.windowMs/1000+.001)return false;
    if(game.peakAcceleration!==null&&(!finite(game.peakAcceleration)||game.peakAcceleration<0||game.peakAcceleration>10000))return false;
    if(game.valid&&(!game.linear||game.peakAcceleration===null||game.windowMs>250))return false;
    if(game.shakeVersion!==undefined||game.shakeCount!==undefined){
      if(game.shakeVersion!==1||!Number.isInteger(game.shakeCount)||game.shakeCount<0||game.shakeCount>1||!finite(game.shakeAgeMs)||game.shakeAgeMs<0||game.shakeAgeMs>game.windowMs||!finite(game.shakeSpanMs))return false;
      if(game.shakeCount?(!game.valid||game.shakeSpanMs<600||game.shakeSpanMs>2000):(game.shakeAgeMs!==0||game.shakeSpanMs!==0))return false;
    }
  }
  return p.gestures.every(g => object(g) && ['shake','tilt','voice','tap','jump','circle'].includes(g.kind) && finite(g.value) && finite(g.timestampMs));
}

export function attachBridge(server) {
  const wss = new WebSocketServer({ noServer: true, maxPayload: 16384 });
  const rooms = new Map();
  server.on('upgrade', (req, socket, head) => {
    if (req.url !== '/ws' || wss.clients.size >= 256) return socket.destroy();
    wss.handleUpgrade(req, socket, head, ws => wss.emit('connection', ws, req));
  });
  const send = (ws, p) => {
    if (ws.readyState === WebSocket.OPEN) {
      if (ws.bufferedAmount > 128 * 1024) return ws.close(1013, 'Slow consumer');
      ws.send(JSON.stringify(p));
    }
  };
  const presence = room => ({type:'presence', players:['red','blue'].map(player => ({player, connected:room.senders.has(player)}))});
  const broadcast = (room, p) => room.receivers.forEach(ws => send(ws, p));
  wss.on('connection', ws => {
    let joined = null, lastSeq = -1, count = 0, windowStart = Date.now();const lastFeedback=new Map();
    ws.alive = true;
    ws.on('pong', () => { ws.alive = true; });
    const deadline = setTimeout(() => ws.close(1008, 'Join required'), 5000);
    ws.on('message', (data, binary) => {
      const now = Date.now();
      if (now - windowStart >= 1000) { count = 0; windowStart = now; }
      if (++count > 100) return ws.close(1008, 'Rate limit');
      let p;
      try { if (binary) throw new Error(); p = JSON.parse(data.toString()); } catch { return ws.close(1008, 'Invalid JSON'); }
      if (!object(p)) return ws.close(1008, 'Invalid message');
      if (!joined) {
        if (p.type !== 'join' || !/^[a-zA-Z0-9_-]{16,64}$/.test(p.room ?? '') || !['sender','receiver'].includes(p.role) || (p.role === 'sender' && !['red','blue'].includes(p.player))) return ws.close(1008, 'Invalid join');
        let room = rooms.get(p.room);
        if (!room) {
          if (rooms.size >= 128) return ws.close(1013, 'Room limit');
          room = {senders:new Map(), receivers:new Set(), latest:new Map()}; rooms.set(p.room, room);
        }
        if (p.role === 'sender' && room.senders.has(p.player)) return ws.close(4009, 'Player already connected');
        joined = {...p, roomState:room}; clearTimeout(deadline);
        if (p.role === 'sender') room.senders.set(p.player, ws); else room.receivers.add(ws);
        send(ws, {type:'joined', role:p.role, player:p.player ?? '', serverTimeMs:now});
        send(ws, presence(room)); broadcast(room, presence(room));
        if (p.role === 'receiver') room.latest.forEach(sample => {
          if (now - sample.receivedAtMs < 2000) send(ws, {...sample, gestures:[]});
        });
        return;
      }
      if (p.type === 'ping' && finite(p.timestampMs)) return send(ws, {type:'pong', timestampMs:p.timestampMs, serverTimeMs:now});
      if(joined.role==='receiver'&&p.type==='feedback'){
        if(!['red','blue'].includes(p.player)||!['hit','ready','error','test','warning','countdown','contest'].includes(p.kind)||p.message!==undefined&&(typeof p.message!=='string'||p.message.length>140)||p.durationMs!==undefined&&(!finite(p.durationMs)||p.durationMs<0||p.durationMs>15000)||p.id!==undefined&&!/^[A-Za-z0-9_-]{1,64}$/.test(p.id))return ws.close(1008,'Invalid feedback');
        if(now-(lastFeedback.get(p.player)??0)<200&&!['warning','countdown','contest'].includes(p.kind))return;
        lastFeedback.set(p.player,now);
        const sender=joined.roomState.senders.get(p.player);
        if(sender)send(sender,{type:'feedback',kind:p.kind,player:p.player,id:p.id??randomUUID(),message:p.message??'',durationMs:p.durationMs??1500});
        return;
      }
      if(joined.role==='sender'&&p.type==='feedbackAck'){
        if(!/^[A-Za-z0-9_-]{1,64}$/.test(p.id??'')||['enabled','screenVisible','vibrationSupported','vibrationAccepted'].some(k=>typeof p[k]!=='boolean'))return ws.close(1008,'Invalid feedback acknowledgement');
        broadcast(joined.roomState,{type:'feedbackAck',player:joined.player,id:p.id,enabled:p.enabled,screenVisible:p.screenVisible,vibrationSupported:p.vibrationSupported,vibrationAccepted:p.vibrationAccepted,receivedAtMs:now});return;
      }
      if (joined.role !== 'sender' || !validPacket(p)) return ws.close(1008, 'Invalid sample');
      if (p.seq <= lastSeq) return;
      lastSeq = p.seq;
      const out = {type:'sample', version:1, player:joined.player, seq:p.seq, timestampMs:p.timestampMs, receivedAtMs:now, simulated:p.simulated, sensors:p.sensors, gestures:p.gestures};
      joined.roomState.latest.set(joined.player, {...out, gestures:[],sensors:{...out.sensors,...(out.sensors.motionGame?{motionGame:{...out.sensors.motionGame,circleSeconds:0,shakeCount:0,shakeAgeMs:0,shakeSpanMs:0}}:{})}});
      broadcast(joined.roomState, out);
    });
    ws.on('error', () => {});
    ws.on('close', () => {
      clearTimeout(deadline);
      if (!joined) return;
      const room = joined.roomState;
      if (joined.role === 'sender') { room.senders.delete(joined.player); room.latest.delete(joined.player); }
      else room.receivers.delete(ws);
      broadcast(room, presence(room));
      if (!room.senders.size && !room.receivers.size) rooms.delete(joined.room);
    });
  });
  const heartbeat = setInterval(() => wss.clients.forEach(ws => {
    if (!ws.alive) return ws.terminate(); ws.alive = false; ws.ping();
  }), 15000);
  heartbeat.unref();
  wss.on('close', () => clearInterval(heartbeat));
  return wss;
}
