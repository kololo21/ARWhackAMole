import {JumpDetector,MotionClassifier,CircleDetector,GRAVITY} from './motion-analysis.js';
import {GameMotion} from './game-motion.js';
export const layouts = {
  acceleration:['x','y','z'], gravityAcceleration:['x','y','z'], rotationRate:['alpha','beta','gamma'],
  orientation:['alpha','beta','gamma','relativeBeta','relativeGamma'], compass:['degrees'],
  light:['lux'], cameraBrightness:['value'], microphone:['rms'], touch:['x','y','pressure','count'],
};
export function emptySensors() {
  return {...Object.fromEntries(Object.entries(layouts).map(([name,keys]) => [name,{valid:false,timestampMs:0,...Object.fromEntries(keys.map(k=>[k,null]))}])),
    motionGame:{valid:false,timestampMs:0,windowMs:0,peakAcceleration:null,circleSeconds:0,circleActive:false,circleVersion:1,circleDegrees:0,linear:false,shakeVersion:1,shakeCount:0,shakeAgeMs:0,shakeSpanMs:0}};
}
export class GestureDetector {
  constructor() { this.latched={}; this.last={}; }
  update(kind,value,threshold,now) {
    if (!Number.isFinite(value)) return null;
    if (value < threshold*0.65) this.latched[kind]=false;
    if (value >= threshold && !this.latched[kind]) {
      this.latched[kind]=true;
      if (now-(this.last[kind]??-Infinity)>=700) {
        this.last[kind]=now; return {kind,value,timestampMs:now};
      }
    }
    return null;
  }
}
const n = v => typeof v==='number' && Number.isFinite(v)?v:null;
const isVector = v => v && ['x','y','z'].every(k=>n(v[k])!==null);

export class Sensors {
  constructor(onStatus) {
    this.onStatus=onStatus; this.values=emptySensors(); this.detector=new GestureDetector(); this.events=[];
    this.thresholds={shake:12,tilt:30,voice:0.15}; this.baseline={beta:0,gamma:0}; this.orientationInitialized=false;this.gravity=null;
    this.jumpDetector=new JumpDetector();this.jumpEnabled=false;
    this.motionProfile='legacy';this.classifier=new MotionClassifier();
    this.circleEnabled=false;this.circleDetector=new CircleDetector();
    this.gameMotion=new GameMotion();
    this.motion=e=>this.handleMotion(e); this.orientation=e=>this.handleOrientation(e);
    this.video=document.createElement('video'); this.video.muted=true; this.video.playsInline=true;
    this.canvas=document.createElement('canvas'); this.canvas.width=this.canvas.height=32;
    this.ctx=this.canvas.getContext('2d',{willReadFrequently:true});
  }
  set(name,data) {
    const required = name==='orientation'?layouts[name].filter(k=>k!=='alpha'):layouts[name];
    this.values[name]={...data,valid:required.every(k=>n(data[k])!==null),timestampMs:Date.now()};
  }
  push(kind,value) {
    if(document.hidden) return;
    const e=this.detector.update(kind,value,this.thresholds[kind],Date.now());
    if(e) { this.events.push(e); if(this.events.length>8)this.events.shift(); }
  }
  async start(simulated=false) {
    this.active=true; this.simulated=simulated;
    if(simulated) { ['motion','orientation','light','cameraBrightness','microphone'].forEach(name=>this.onStatus(name,'シミュレーター（合成値）')); return; }
    if(!isSecureContext) { this.onStatus('motion','HTTPSが必要'); this.onStatus('orientation','HTTPSが必要'); return; }
    // Invoke both permission requests synchronously inside the original user gesture (iOS).
    const request = ctor => { try { return ctor?.requestPermission ? ctor.requestPermission() : Promise.resolve(ctor?'granted':'unsupported'); } catch(e) { return Promise.reject(e); } };
    const permissions = await Promise.allSettled([request(window.DeviceMotionEvent),request(window.DeviceOrientationEvent)]);
    if(!this.active) return;
    [['motion',permissions[0],'devicemotion',this.motion],['orientation',permissions[1],'deviceorientation',this.orientation]].forEach(([name,r,event,handler])=>{
      if(r.status==='fulfilled' && r.value==='granted') { window.addEventListener(event,handler); this.onStatus(name,'データ待ち'); }
      else this.onStatus(name,r.status==='fulfilled'&&r.value==='unsupported'?'非対応':'許可されていません');
    });
    if('AmbientLightSensor' in window) {
      try {
        this.light=new window.AmbientLightSensor({frequency:5});
        this.light.addEventListener('reading',()=>{this.set('light',{lux:n(this.light.illuminance)});this.onStatus('light','取得中');});
        this.light.addEventListener('error',e=>this.onStatus('light',`利用不可: ${e.error.name}`)); this.light.start();
      } catch { this.onStatus('light','利用不可'); }
    } else this.onStatus('light','非対応（カメラ輝度で代替可）');
  }
  handleMotion(e) {
    if(!this.active||this.simulated) return;
    const a=e.acceleration, g=e.accelerationIncludingGravity, r=e.rotationRate;
    this.set('acceleration',{x:n(a?.x),y:n(a?.y),z:n(a?.z)});
    this.set('gravityAcceleration',{x:n(g?.x),y:n(g?.y),z:n(g?.z)});
    this.set('rotationRate',{alpha:n(r?.alpha),beta:n(r?.beta),gamma:n(r?.gamma)});
    this.onStatus('motion',isVector(a)||isVector(g)?'取得中':'値が返っていません');
    let magnitude=null;
    if(isVector(a)) magnitude=Math.hypot(a.x,a.y,a.z);
    else if(isVector(g)) {
      if(!this.gravity) this.gravity={x:g.x,y:g.y,z:g.z};
      magnitude=Math.hypot(...['x','y','z'].map(k=>{ this.gravity[k]=0.9*this.gravity[k]+0.1*g[k]; return g[k]-this.gravity[k]; }));
    }
    const gravityG=isVector(g)?Math.hypot(g.x,g.y,g.z)/GRAVITY:null;
    const rotation=r&&['alpha','beta','gamma'].every(k=>n(r[k])!==null)?Math.hypot(r.alpha,r.beta,r.gamma):null;
    if(!document.hidden)this.gameMotion.update({t:performance.now(),a:magnitude,gyro:rotation,linear:isVector(a),x:a?.x,y:a?.y,z:a?.z});
    if(!document.hidden&&this.circleEnabled){
      const circle=this.circleDetector.update({t:performance.now(),a:magnitude,gyro:rotation});
      if(circle)this.events.push({...circle,timestampMs:Date.now()});
    }
    if(this.motionProfile==='candidate-v2'){
      if(!document.hidden)for(const event of this.classifier.update({t:performance.now(),a:magnitude,g:gravityG,gyro:rotation})){
        if(event.kind!=='jump'||this.jumpEnabled)this.events.push({...event,timestampMs:Date.now()});
      }
    }else{
      this.push('shake',magnitude);
      if(!document.hidden&&this.jumpEnabled){const jump=this.jumpDetector.update(Date.now(),gravityG);if(jump)this.events.push(jump);}
    }
    if(!document.hidden)this.onMotion?.({at:performance.now(),a:isVector(a)?a:null,g:isVector(g)?g:null,r,magnitude,gravityG});
  }
  handleOrientation(e) {
    if(!this.active||this.simulated) return;
    const beta=n(e.beta), gamma=n(e.gamma);
    if(!this.orientationInitialized&&beta!==null&&gamma!==null){this.baseline={beta,gamma};this.orientationInitialized=true;}
    this.set('orientation',{alpha:n(e.alpha),beta,gamma,relativeBeta:beta===null?null:((beta-this.baseline.beta+540)%360)-180,relativeGamma:gamma===null?null:gamma-this.baseline.gamma});
    const heading=n(e.webkitCompassHeading) ?? (e.absolute && n(e.alpha)!==null?(360-e.alpha)%360:null);
    this.set('compass',{degrees:heading});
    this.onStatus('orientation',beta!==null&&gamma!==null?'取得中':'値が返っていません');
    const o=this.values.orientation;
    if(o.valid) this.push('tilt',Math.hypot(o.relativeBeta,o.relativeGamma));
  }
  calibrate() {
    const o=this.values.orientation;
    if(!o.valid || Date.now()-o.timestampMs>1000) return false;
    this.baseline={beta:o.beta,gamma:o.gamma}; this.detector=new GestureDetector(); this.events=[]; return true;
  }
  async enableMicrophone() {
    if(!this.active || this.simulated) throw new Error('実機モードで送信を開始してください');
    if(this.audioStream) return;
    const stream=await navigator.mediaDevices.getUserMedia({audio:{echoCancellation:false,noiseSuppression:false,autoGainControl:false},video:false});
    if(!this.active) {stream.getTracks().forEach(t=>t.stop());return;}
    let audio;
    try {
      this.audioStream=stream;
      audio=this.audio=new (window.AudioContext||window.webkitAudioContext)(); await audio.resume();
      if(!this.active) return;
      this.analyser=audio.createAnalyser(); this.analyser.fftSize=1024;
      audio.createMediaStreamSource(stream).connect(this.analyser); this.audioBuffer=new Float32Array(1024);
      this.onStatus('microphone','取得中（音声は送信しません）');
    } catch(error) {
      // Permission may succeed while audio initialization fails. Release the
      // stream and clear partial state so the permission button can retry.
      stream.getTracks().forEach(track=>track.stop());
      this.audioStream=null;this.audio=null;this.analyser=null;
      await audio?.close().catch(()=>{});
      throw error;
    }
  }
  async enableCamera() {
    if(!this.active || this.simulated) throw new Error('実機モードで送信を開始してください');
    if(this.cameraStream) return;
    const stream=await navigator.mediaDevices.getUserMedia({video:{facingMode:'environment',width:{ideal:160},height:{ideal:120}},audio:false});
    if(!this.active) {stream.getTracks().forEach(t=>t.stop());return;}
    this.cameraStream=stream; this.video.srcObject=stream; await this.video.play();
    this.onStatus('cameraBrightness','取得中（映像は送信しません）');
  }
  touch(x,y,pressure,count) {
    if(!this.active) return;
    this.set('touch',{x,y,pressure,count});
    if(count>0) this.events.push({kind:'tap',value:pressure,timestampMs:Date.now()});
  }
  sample() {
    const now=Date.now();
    if(this.simulated) {
      const t=now/1000;
      this.set('acceleration',{x:Math.sin(t*3)*3,y:Math.cos(t*2)*2,z:0});
      this.set('gravityAcceleration',{x:Math.sin(t*3)*3,y:Math.cos(t*2)*2,z:9.81});
      this.set('rotationRate',{alpha:Math.sin(t)*25,beta:Math.cos(t)*20,gamma:Math.sin(t)*10});
      this.set('orientation',{alpha:t*20%360,beta:Math.sin(t)*25,gamma:Math.cos(t)*15,relativeBeta:Math.sin(t)*25-this.baseline.beta,relativeGamma:Math.cos(t)*15-this.baseline.gamma});
      this.set('compass',{degrees:t*20%360}); this.set('light',{lux:300+Math.sin(t)*100});
      this.set('cameraBrightness',{value:0.5+Math.sin(t)*0.1}); this.set('microphone',{rms:0.04});
      this.gameMotion.update({t:performance.now(),a:Math.hypot(this.values.acceleration.x,this.values.acceleration.y,0),gyro:30,linear:true,...this.values.acceleration});
    } else {
      if(this.analyser && this.audio?.state==='running' && this.audioStream?.getAudioTracks().some(t=>t.readyState==='live'&&!t.muted)) {
        this.analyser.getFloatTimeDomainData(this.audioBuffer);
        const rms=Math.sqrt(this.audioBuffer.reduce((sum,x)=>sum+x*x,0)/this.audioBuffer.length);
        this.set('microphone',{rms}); this.push('voice',rms);
      }
      if(this.cameraStream?.getVideoTracks().some(t=>t.readyState==='live'&&!t.muted) && this.video.readyState>=2 && now-(this.lastCamera??0)>=200) {
        this.lastCamera=now; this.ctx.drawImage(this.video,0,0,32,32);
        const rgba=this.ctx.getImageData(0,0,32,32).data; let sum=0;
        for(let i=0;i<rgba.length;i+=4) sum+=0.2126*rgba[i]+0.7152*rgba[i+1]+0.0722*rgba[i+2];
        this.set('cameraBrightness',{value:sum/(1024*255)});
      }
    }
    this.values.motionGame=this.gameMotion.flush(performance.now(),now);
    return Object.fromEntries(Object.entries(this.values).map(([k,s])=>[k,{...s,valid:s.valid && now-s.timestampMs<1000}]));
  }
  drainEvents() { const out=this.events.filter(e=>Date.now()-e.timestampMs<500).slice(0,8); this.events=[]; return out; }
  pause() { this.events=[]; this.detector=new GestureDetector(); this.jumpDetector.reset();this.classifier.reset();this.circleDetector.reset();this.gameMotion.reset();this.gravity=null; }
  stop() {
    this.active=false; this.pause(); window.removeEventListener('devicemotion',this.motion); window.removeEventListener('deviceorientation',this.orientation);
    this.light?.stop(); this.light=null;
    [this.audioStream,this.cameraStream].forEach(s=>s?.getTracks().forEach(t=>t.stop()));
    this.audio?.close().catch(()=>{}); this.audio=null;this.analyser=null;this.audioStream=null;this.cameraStream=null;
    this.video.srcObject=null; this.values=emptySensors();
  }
}
