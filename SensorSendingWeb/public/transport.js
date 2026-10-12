export class Connection {
  constructor(onMessage, onStatus) { this.onMessage=onMessage; this.onStatus=onStatus; this.active=false; this.attempt=0; }
  connect(join) { this.stop(); this.join=join; this.active=true; this.attempt=0; this.open(); }
  open() {
    if (!this.active) return;
    this.onStatus('connecting');
    const url = new URL('/ws',location.href); url.protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    const ws = this.ws = new WebSocket(url);
    ws.onopen = () => ws.send(JSON.stringify({type:'join',...this.join}));
    ws.onmessage = e => {
      const p=JSON.parse(e.data);
      if (p.type==='joined') { this.ready=true; this.attempt=0; this.onStatus('connected'); }
      this.onMessage(p);
    };
    ws.onerror = () => {};
    ws.onclose = e => {
      if (this.ws!==ws) return;
      this.ready=false;
      if ([4009,1008].includes(e.code)) { this.active=false; this.onStatus(e.code===4009?'occupied':'invalid'); return; }
      this.onStatus(this.active?'reconnecting':'disconnected');
      if (this.active) this.timer=setTimeout(()=>this.open(),Math.min(10000,500*2**this.attempt++)+Math.random()*250);
    };
  }
  send(p) { if (!this.ready || this.ws?.readyState!==1 || this.ws.bufferedAmount>16384) return false; this.ws.send(JSON.stringify(p)); return true; }
  stop() { this.active=false; this.ready=false; clearTimeout(this.timer); if (this.ws) { this.ws.onclose=null; this.ws.close(); } this.onStatus('disconnected'); }
}
export const fresh = (s, now=Date.now()) => !!s?.valid && now-s.timestampMs<1000;
export const fmt = (n,d=2) => Number.isFinite(n)?n.toFixed(d):'—';
