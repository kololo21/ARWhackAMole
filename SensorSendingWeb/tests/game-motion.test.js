import test from 'node:test';import assert from 'node:assert/strict';
import {GameMotion} from '../public/game-motion.js';
import {ShakeCycle} from '../public/shake-cycle.js';
test('gentle full cycles count equally to stronger motion; noise, one-way motion and rapid cycles do not',()=>{
  function run(amp,period){const g=new ShakeCycle(),events=[];for(let t=0;t<=5000;t+=20){const e=g.update({t,x:amp*Math.cos(t*Math.PI*2/period),y:0,z:0,linear:true});if(e)events.push(e);}return events;}
  const gentle=run(6,800);assert.ok(gentle.length>=5);assert.equal(gentle.length,run(80,800).length);
  assert.equal(run(.8,800).length,0);assert.equal(run(6,200).length,0);
  const g=new ShakeCycle();for(let t=0;t<=2000;t+=20)assert.equal(g.update({t,x:6,y:0,z:0,linear:true}),null);
  assert.equal(g.update({t:5000,x:-6,y:0,z:0,linear:true}),null);
});
test('native cycle is sent once with its age and duration, then cleared on flush and pause',()=>{
  const g=new GameMotion();let count=0;
  for(let t=0;t<=3000;t+=20){const x=6*Math.cos(t*Math.PI*2/800);g.update({t,x,y:0,z:0,a:Math.abs(x),gyro:0,linear:true});const p=g.flush(t,1000+t);count+=p.shakeCount;if(p.shakeCount){assert.equal(p.shakeAgeMs,0);assert.ok(p.shakeSpanMs>=600);}}
  assert.ok(count>=3);assert.equal(g.flush(3020,4020).shakeCount,0);g.reset();assert.equal(g.flush(3040,4040).shakeCount,0);
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
