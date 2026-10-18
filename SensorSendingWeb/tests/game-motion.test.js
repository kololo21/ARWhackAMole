import test from 'node:test';import assert from 'node:assert/strict';
import {GameMotion} from '../public/game-motion.js';
import {ShakeCycle} from '../public/shake-cycle.js';
import {emptySensors} from '../public/sensors.js';
import {validPacket} from '../server/bridge.js';
test('gentle full cycles count equally to stronger motion; noise, one-way motion and rapid cycles do not',()=>{
  function run(amp,period){const g=new ShakeCycle(),events=[];for(let t=0;t<=5000;t+=20){const e=g.update({t,x:amp*Math.cos(t*Math.PI*2/period),y:0,z:0,linear:true});if(e)events.push(e);}return events;}
  const gentle=run(6,800);assert.ok(gentle.length>=5);assert.equal(gentle.length,run(80,800).length);
  for(const period of [300,350,400,500]){
    const light=run(6,period);assert.ok(light.length>=9);assert.equal(light.length,run(80,period).length);
  }
  assert.equal(run(.8,800).length,0);assert.equal(run(6,200).length,0);
  const g=new ShakeCycle();for(let t=0;t<=2000;t+=20)assert.equal(g.update({t,x:6,y:0,z:0,linear:true}),null);
  assert.equal(g.update({t:5000,x:-6,y:0,z:0,linear:true}),null);
});
test('native cycle is sent once with its age and duration, then cleared on flush and pause',()=>{
  const g=new GameMotion();let count=0;
  for(let t=0;t<=3000;t+=20){const x=6*Math.cos(t*Math.PI*2/800);g.update({t,x,y:0,z:0,a:Math.abs(x),gyro:0,linear:true});const p=g.flush(t,1000+t);count+=p.shakeCount;if(p.shakeCount){assert.equal(p.shakeAgeMs,0);assert.ok(p.shakeSpanMs>=600);}}
  assert.ok(count>=3);assert.equal(g.flush(3020,4020).shakeCount,0);g.reset();assert.equal(g.flush(3040,4040).shakeCount,0);
});
test('two to three round trips per second survive native sampling and 20Hz sends',()=>{
  for(const interval of [16,20,50])for(const period of [350,400,500]){
    const g=new GameMotion();let count=0,nextSend=0;
    for(let t=0;t<=5000;t+=interval){
      const x=6*Math.cos(t*Math.PI*2/period);
      g.update({t,x,y:0,z:0,a:Math.abs(x),gyro:0,linear:true});
      if(t>=nextSend){
        const p=g.flush(t,1000+t);count+=p.shakeCount;nextSend=t+50;
        assert.ok(validPacket({type:'sample',version:1,seq:t,timestampMs:1000+t,simulated:false,sensors:{...emptySensors(),motionGame:p},gestures:[]}), 'native packet must pass relay validation');
      }
    }
    assert.ok(count>=7,`${interval}ms samples, ${period}ms round trip: ${count} counted`);
  }
});
test('native peak survives lower values before the next transmission and is not replayed',()=>{
  const g=new GameMotion();g.update({t:0,a:1,gyro:0,linear:true});g.update({t:16,a:80,gyro:0,linear:true});g.update({t:32,a:2,gyro:0,linear:true});
  assert.equal(g.flush(50,1000).peakAcceleration,80);assert.equal(g.flush(100,1050).valid,false);
  g.update({t:110,a:0,gyro:0,linear:true});assert.equal(g.flush(150,1100).peakAcceleration,0);
  g.reset();assert.equal(g.flush(200,1200).valid,false);
});
test('continuous circle earns measured active time while wrist-like rotation and data gaps do not',()=>{
  function run(gyro){const g=new GameMotion();let seconds=0;for(let t=0;t<=3000;t+=20){g.update({t,a:22,gyro,linear:true});if(t%100===0)seconds+=g.flush(t,1000+t).circleSeconds;}return {g,seconds};}
  const circle=run(350);assert.ok(circle.seconds>2&&circle.seconds<=3);
  assert.equal(run(650).seconds,0);
  circle.g.update({t:5000,a:22,gyro:350,linear:true});assert.equal(circle.g.flush(5050,6000).circleSeconds,0);
});
test('circle rotation accumulates continuously at faster speeds while charge time stays unchanged',()=>{
  function run(gyro,a){
    const g=new GameMotion();let seconds=0,degrees=0;
    for(let t=0;t<=5000;t+=20){
      g.update({t,a,gyro,linear:true});
      if(t%100===0){
        const p=g.flush(t,1000+t);seconds+=p.circleSeconds;degrees+=p.circleDegrees;
        assert.ok(validPacket({type:'sample',version:1,seq:t,timestampMs:1000+t,simulated:false,sensors:{...emptySensors(),motionGame:p},gestures:[]}));
      }
    }
    assert.equal(g.flush(5020,6020).circleDegrees,0,'rotation is not replayed after a send');
    g.reset();assert.equal(g.flush(5040,6040).circleDegrees,0,'pause clears pending rotation');
    return {seconds,degrees};
  }
  const slow=run(350,22),fast=run(700,44),faster=run(1400,88);
  assert.ok(slow.degrees>1000);
  assert.equal(fast.seconds,slow.seconds);assert.equal(faster.seconds,slow.seconds);
  assert.ok(Math.abs(fast.degrees-slow.degrees*2)<.001);
  assert.ok(Math.abs(faster.degrees-slow.degrees*4)<.001,'fast rotation is integrated without the shake-cycle period cutoff');
  assert.equal(run(650,22).degrees,0,'wrist-like high rotation with little translation is still excluded');
  assert.equal(run(null,22).degrees,0,'missing gyro does not earn rotation');
});
test('circle readiness requires actual gyro readings, including a valid stationary zero',()=>{
  const g=new GameMotion();
  g.update({t:0,a:6,gyro:null,linear:true});
  let p=g.flush(0,1000);
  assert.equal(p.valid,true,'other motion input remains available');
  assert.equal(p.circleVersion,0,'missing gyro must not advertise circle readiness');
  assert.equal(p.circleDegrees,0);
  assert.ok(validPacket({type:'sample',version:1,seq:0,timestampMs:1000,simulated:false,sensors:{...emptySensors(),motionGame:p},gestures:[]}));
  g.update({t:50,a:0,gyro:0,linear:true});p=g.flush(50,1050);
  assert.equal(p.circleVersion,1,'stationary gyro is supported and ready');
  g.update({t:100,a:6,gyro:NaN,linear:true});p=g.flush(100,1100);
  assert.equal(p.circleVersion,0);
  g.reset();assert.equal(g.flush(150,1150).circleVersion,0);
});
