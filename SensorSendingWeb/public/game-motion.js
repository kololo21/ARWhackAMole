import {ShakeCycle} from './shake-cycle.js';
// Native motion samples -> one packet window. Keep completed cycles across sends.
export class GameMotion {
  constructor(){this.reset();}
  reset(){this.rows=[];this.last=null;this.lastFlush=null;this.binStart=null;this.peak=null;this.circleSeconds=0;this.active=false;this.qualifyingSince=null;this.shake=new ShakeCycle();this.cycle=null;}
  update(row){
    if(!Number.isFinite(row.t))return;
    if(this.last!==null&&row.t<=this.last)return;
    let dt=this.last===null?0:row.t-this.last;
    if(dt>120){this.reset();dt=0;}
    this.last=row.t;this.binStart??=row.t;
    const cycle=this.shake.update(row);if(cycle)this.cycle=cycle;
    if(row.linear&&Number.isFinite(row.a))this.peak=Math.max(this.peak??0,row.a);
    this.rows.push({...row,dt});while(this.rows.length&&row.t-this.rows[0].t>1000)this.rows.shift();
    let activeMs=0,angle=0,impulse=0;
    const valid=this.rows.every(r=>Number.isFinite(r.a)&&Number.isFinite(r.gyro));
    if(valid)for(const r of this.rows)if(r.a>=2&&r.gyro>=80){activeMs+=r.dt;angle+=r.gyro*r.dt/1000;impulse+=r.a*r.dt/1000;}
    const qualifies=valid&&row.a>=2&&row.gyro>=80&&activeMs>=700&&angle>=240&&impulse/angle>=.052;
    if(qualifies)this.qualifyingSince??=row.t;else this.qualifyingSince=null;
    this.active=qualifies&&row.t-this.qualifyingSince>=200;
    if(this.active)this.circleSeconds+=dt/1000;
    if(this.rows.length>1&&row.t-this.rows.at(-2).t>120)this.active=false;
  }
  flush(t,timestampMs){
    const windowMs=this.binStart===null?0:t-(this.lastFlush??this.binStart);
    const valid=this.peak!==null&&this.last!==null&&t-this.last<200&&windowMs<=250;
    const reading={valid,timestampMs,windowMs:Math.max(0,windowMs),peakAcceleration:valid?this.peak:null,
      circleSeconds:valid?Math.min(this.circleSeconds,Math.max(0,windowMs)/1000):0,circleActive:valid&&this.active,linear:valid,
      shakeVersion:1,shakeCount:valid&&this.cycle?1:0,shakeAgeMs:valid&&this.cycle?t-this.cycle.t:0,shakeSpanMs:valid&&this.cycle?this.cycle.spanMs:0};
    this.peak=null;this.circleSeconds=0;this.cycle=null;this.lastFlush=t;this.binStart=null;
    return reading;
  }
}
