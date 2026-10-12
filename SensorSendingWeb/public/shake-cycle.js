// Acceleration reversals are a proxy for a round trip, not position tracking.
// Thresholds tune recognition; they do not define a physical safety limit.
export class ShakeCycle {
  constructor(){this.reset();}
  reset(){this.last=null;this.filtered=null;this.axis=null;this.phase=0;this.start=0;this.turn=0;this.candidate=null;this.lastCount=-Infinity;}
  update({t,x,y,z,linear}){
    if(!linear||![t,x,y,z].every(Number.isFinite)){this.reset();return null;}
    if(this.last!==null&&t<=this.last)return null;
    const dt=this.last===null?0:t-this.last;
    if(dt>120)this.reset();
    const v=[x,y,z],alpha=this.filtered===null?1:1-Math.exp(-dt/35);
    this.filtered=this.filtered===null?v:v.map((a,i)=>this.filtered[i]+alpha*(a-this.filtered[i]));this.last=t;
    if(this.phase&&t-this.start>2000){this.phase=0;this.axis=null;this.candidate=null;}
    const magnitude=Math.hypot(...this.filtered);
    if(!this.phase){
      if(magnitude<3){this.candidate=null;return null;}
      if(this.candidate===null){this.candidate=t;this.axis=this.filtered.map(a=>a/magnitude);}
      if(t-this.candidate>=40){this.phase=1;this.start=this.candidate;this.candidate=null;}
      return null;
    }
    const projection=this.filtered.reduce((sum,a,i)=>sum+a*this.axis[i],0);
    const reaches=this.phase===1?projection<=-3:projection>=3;
    if(!reaches){this.candidate=null;return null;}
    this.candidate??=t;
    if(t-this.candidate<40)return null;
    if(this.phase===1){
      if(this.candidate-this.start<120){this.phase=0;this.axis=null;this.candidate=null;return null;}
      this.turn=this.candidate;this.phase=2;this.candidate=null;return null;
    }
    const span=t-this.start;
    const accepted=this.candidate-this.turn>=120&&span>=600&&t-this.lastCount>=600;
    this.start=t;this.phase=1;this.candidate=null;
    if(!accepted)return null;
    this.lastCount=t;return {t,spanMs:span};
  }
}
