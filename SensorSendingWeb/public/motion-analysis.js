// Pure functions shared by the live experiment, saved-session analysis and tests.
export const GRAVITY = 9.80665;
export const trials = {
  still: ['静止', '左手でスマホを普段の位置に持ち、12秒間そのまま。'],
  hammer: ['右手だけで叩く', '左手を普段の位置に保ち、合図ごとに右手で1回叩く動作。'],
  shake: ['スマホを振る', '合図ごとに左手のスマホを上下に短く振り、元の位置で止める。'],
  jump: ['小さなジャンプ', '左手を体に寄せて持ち、合図ごとにその場で小さく1回ジャンプ。'],
  bend: ['しゃがんで戻る', '合図ごとに浅くしゃがみ、立ち上がる。ジャンプとの誤反応を比較。'],
  circle: ['腕で大きめの円を描く', '体の前で左腕を使い、合図ごとにスマホで縦の円を1周描いて止める。無理のない大きさで、回す向きを揃える。'],
  wrist: ['手首だけで回す', '左肘の位置を保ち、合図ごとに手首だけでスマホを小さく1周動かして止める。腕全体の円との違いを比較。'],
  tilt: ['傾けて戻す', '開始前に基準姿勢を設定。合図ごとに左へ傾けて元に戻す。'],
  voice: ['短い声', 'マイクを許可し、合図ごとに普段の声で「はい」と1回言う。'],
  noise: ['周囲の声', '自分は操作せず、もう1人がスマホの近くで話す。声の誤反応を比較。'],
};
export class JumpDetector {
  constructor(options = {}) {
    this.options = {lowG:0.65, minLowMs:60, landingG:1.8, maxFlightMs:900, cooldownMs:1000, ...options};
    this.reset();
  }
  reset() { this.lowStart=null; this.armed=false; this.previous=null; this.last=-Infinity; this.blocked=false; }
  update(t, g) {
    if (!Number.isFinite(t) || !Number.isFinite(g)) { this.lowStart=null; this.armed=false; return null; }
    // A sensor gap is not a period in the air.
    if (this.previous!==null && (t-this.previous>120 || t<=this.previous)) { this.lowStart=null; this.armed=false; }
    this.previous=t;
    const o=this.options;
    if(this.blocked){if(g>=o.lowG)this.blocked=false;return null;}
    if (this.lowStart!==null && t-this.lowStart>o.maxFlightMs) {this.lowStart=null;this.armed=false;this.blocked=g<o.lowG;return null;}
    if (g<o.lowG) {
      if(this.lowStart===null)this.lowStart=t;
      if(t-this.lowStart>=o.minLowMs)this.armed=true;
    } else if(this.lowStart!==null) {
      if(!this.armed) this.lowStart=null;
      else if(g>=o.landingG) {
        const flightMs=t-this.lowStart;this.lowStart=null;this.armed=false;
        if(t-this.last>=o.cooldownMs){this.last=t;return {kind:'jump',value:g,flightMs,timestampMs:t};}
      }
    }
    return null;
  }
}
// Candidate for the recorded left-hand Android gestures. Re-test on new trials;
// a translation-only shake with little wrist rotation can fail this rule.
export const candidateProfile={shakeAcceleration:12,shakeRotation:180,shakeWindowMs:200,shakeCooldownMs:1000,
  jumpLowG:.85,jumpLandingG:2.2,jumpMinLowMs:60,jumpRotationLimit:180,jumpRotationWindowMs:500};
export class MotionClassifier {
  constructor(options={}){this.options={...candidateProfile,...options};this.reset();}
  reset(){this.history=[];this.last=null;this.shakeLatched=false;this.lastShake=-Infinity;
    this.jump=new JumpDetector({lowG:this.options.jumpLowG,landingG:this.options.jumpLandingG,minLowMs:this.options.jumpMinLowMs});}
  update(row){
    if(!Number.isFinite(row.t))return [];
    if(this.last!==null&&row.t<=this.last)return [];
    if(this.last!==null&&row.t-this.last>120)this.reset();
    this.last=row.t;this.history.push({...row});
    while(this.history.length&&row.t-this.history[0].t>this.options.jumpRotationWindowMs)this.history.shift();
    const short=this.history.filter(r=>row.t-r.t<=this.options.shakeWindowMs);
    const max=(rows,key)=>rows.some(r=>!Number.isFinite(r[key]))?null:Math.max(...rows.map(r=>r[key]));
    const acceleration=max(short,'a'),rotation=max(short,'gyro'),events=[],o=this.options;
    if(acceleration===null||acceleration<o.shakeAcceleration*.65)this.shakeLatched=false;
    if(acceleration!==null&&rotation!==null&&acceleration>=o.shakeAcceleration&&rotation>=o.shakeRotation&&!this.shakeLatched){
      this.shakeLatched=true;
      if(row.t-this.lastShake>=o.shakeCooldownMs){this.lastShake=row.t;events.push({kind:'shake',value:acceleration,timestampMs:row.t});}
    }
    const jump=this.jump.update(row.t,row.g),jumpRotation=max(this.history,'gyro');
    if(jump&&jumpRotation!==null&&jumpRotation<o.jumpRotationLimit)events.push(jump);
    return events;
  }
}
export function replayCandidate(rows){const classifier=new MotionClassifier();return rows.flatMap(r=>classifier.update(r));}
// A completed motion episode, not a physical radius measurement or exact lap counter.
// The ratio is an empirical feature fitted to the recorded left-hand Android gestures.
export const circleProfile={accelerationFloor:2,rotationFloor:80,minActiveMs:800,minRotationTravelDeg:300,
  minMotionRotationRatio:.052,quietMs:200,maxEpisodeMs:3500};
export class CircleDetector {
  constructor(options={}){this.options={...circleProfile,...options};this.reset();}
  reset(){this.last=null;this.segment=null;this.blocked=false;this.blockedQuietStart=null;}
  update(row){
    if(!Number.isFinite(row.t))return null;
    if(this.last!==null&&row.t<=this.last)return null;
    const dt=this.last===null?0:row.t-this.last;this.last=row.t;
    if(dt>120||!Number.isFinite(row.a)||!Number.isFinite(row.gyro)){this.segment=null;this.blocked=false;return null;}
    const o=this.options,active=row.a>=o.accelerationFloor&&row.gyro>=o.rotationFloor;
    if(this.blocked){
      if(active)this.blockedQuietStart=null;
      else{this.blockedQuietStart??=row.t;if(row.t-this.blockedQuietStart>=o.quietMs){this.blocked=false;this.blockedQuietStart=null;}}
      return null;
    }
    if(active){
      if(!this.segment)this.segment={start:row.t,lastActive:row.t,activeMs:0,rotationTravelDeg:0,motionIntegral:0};
      else{
        this.segment.activeMs+=dt;this.segment.rotationTravelDeg+=row.gyro*dt/1000;
        this.segment.motionIntegral+=row.a*dt/1000;this.segment.lastActive=row.t;
      }
      if(row.t-this.segment.start>o.maxEpisodeMs){this.segment=null;this.blocked=true;}
      return null;
    }
    if(!this.segment||row.t-this.segment.lastActive<o.quietMs)return null;
    const s=this.segment;this.segment=null;
    const ratio=s.motionIntegral/s.rotationTravelDeg;
    if(s.activeMs<o.minActiveMs||s.rotationTravelDeg<o.minRotationTravelDeg||ratio<o.minMotionRotationRatio)return null;
    return {kind:'circle',value:1,timestampMs:row.t,activeMs:s.activeMs,rotationTravelDeg:s.rotationTravelDeg,motionRotationRatio:ratio};
  }
}
export function replayCircle(rows){const detector=new CircleDetector();return rows.map(r=>detector.update(r)).filter(Boolean);}
export function crossingTimes(rows, key, threshold, cooldown=700) {
  let latched=false,last=-Infinity;const times=[];
  for(const row of rows){const v=row[key];if(!Number.isFinite(v)){latched=false;continue;}
    if(v<threshold*0.65)latched=false;
    if(v>=threshold&&!latched){latched=true;if(row.t-last>=cooldown){last=row.t;times.push(row.t);}}
  }return times;
}
export function summarize(session) {
  const rows=session.samples;
  const stat=key=>{const a=rows.map(r=>r[key]).filter(Number.isFinite).sort((a,b)=>a-b);return a.length?{min:a[0],p95:a[Math.floor((a.length-1)*.95)],max:a.at(-1)}:null;};
  const score=times=>{
    const cueWindowMs=['circle','wrist'].includes(session.label)?2800:2200;
    const windows=(session.cues??[]).map(t=>[t,t+cueWindowMs]);
    const perCue=windows.map(([a,b])=>times.filter(t=>t>=a&&t<b).length);
    return {count:times.length,perCue,outsideCues:times.filter(t=>!windows.some(([a,b])=>t>=a&&t<b)).length,times,cueWindowMs};
  };
  const sweep=(key,values)=>values.map(threshold=>({threshold,...score(crossingTimes(rows,key,threshold))}));
  const jump=[];
  for(const lowG of [.45,.65,.85])for(const landingG of [1.4,1.8,2.2]){
    const detector=new JumpDetector({lowG,landingG});const times=[];
    for(const r of rows)if(detector.update(r.t,r.g))times.push(r.t);
    jump.push({lowG,landingG,minLowMs:60,...score(times)});
  }
  const gaps=rows.slice(1).map((r,i)=>r.t-rows[i].t);
  const duration=rows.length>1?rows.at(-1).t-rows[0].t:0;
  const candidateEvents=replayCandidate(rows);
  const circleEvents=replayCircle(rows);
  return {samples:rows.length,motionHz:duration?(rows.length-1)*1000/duration:0,maxGapMs:gaps.length?Math.max(...gaps):0,
    acceleration:stat('a'),gravityG:stat('g'),rotation:stat('gyro'),tilt:stat('tilt'),voice:stat('rms'),
    shake:sweep('a',[3,5,8,10,12,15,18,22,26]),tiltSweep:sweep('tilt',[15,20,30,40,50]),voiceSweep:sweep('rms',[.03,.06,.1,.15,.2,.3]),jump,
    candidate:{profile:candidateProfile,shake:score(candidateEvents.filter(e=>e.kind==='shake').map(e=>e.timestampMs)),jump:score(candidateEvents.filter(e=>e.kind==='jump').map(e=>e.timestampMs))},
    circle:{profile:circleProfile,...score(circleEvents.map(e=>e.timestampMs)),events:circleEvents}};
}
