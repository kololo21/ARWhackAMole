export function plot(canvas, history, color='#246476', max=25) {
  const ctx=canvas.getContext('2d'), w=canvas.width, h=canvas.height;
  ctx.clearRect(0,0,w,h); ctx.strokeStyle='#e0e8e9'; ctx.lineWidth=1;
  [0.25,0.5,0.75].forEach(p=>{ctx.beginPath();ctx.moveTo(0,h*p);ctx.lineTo(w,h*p);ctx.stroke();});
  ctx.strokeStyle=color;ctx.lineWidth=3;ctx.beginPath();let pen=false;
  history.forEach((v,i)=>{ if(!Number.isFinite(v)){pen=false;return;} const x=i*w/Math.max(history.length-1,1),y=h-5-Math.min(Math.max(v,0)/max,1)*(h-10);if(pen)ctx.lineTo(x,y);else ctx.moveTo(x,y);pen=true; });ctx.stroke();
}
export function appendEvents(el, gestures, simulated) {
  const names={shake:'振る',tilt:'傾ける',voice:'声',tap:'タッチ',jump:'ジャンプ候補',circle:'円の動き候補'};
  gestures.forEach(g=>{const item=document.createElement('span');item.className='event';item.textContent=`${names[g.kind]??g.kind}${simulated?'（合成）':''} · ${new Date().toLocaleTimeString('ja-JP')}`;el.prepend(item);});
  while(el.children.length>8) el.lastChild.remove();
}
