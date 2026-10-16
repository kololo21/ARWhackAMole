using System;
using SensorBridge;
using WhackAMole.Sensors;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif
public static class WhackSensorRulesChecks
{
    private static void Check(bool yes,string message){if(!yes)throw new Exception(message);}
    private static SensorPacket Packet(string team,double now,double circle=0,int count=0,double peak=6)
    {
        double phone=now+(team=="blue"?60000:0);
        return new SensorPacket{player=team,seq=(long)now,timestampMs=phone,receivedAtMs=now,sensors=new SensorValues{motionGame=new GameMotionReading{valid=true,linear=true,timestampMs=phone,windowMs=50,peakAcceleration=peak,circleSeconds=circle,shakeVersion=1,shakeCount=count,shakeSpanMs=count>0?800:0}}};
    }
    private static void Warm(SensorArenaModel m,double now){m.Presence("red",true);m.Presence("blue",true);m.Ingest(Packet("red",now),now);m.Ingest(Packet("blue",now),now);}
    public static void RunChecks()
    {
        var m=new SensorArenaModel();
        int spent=0;m.ChargeSpent+=(player,amount,spendBefore)=>{spent++;Check(player==m.Blue&&amount==50&&Math.Abs(spendBefore-player.Charge-amount)<.001,"spend feedback carries exact blue deduction");};
        Check(m.ChargeCost==50,"windup needs half of the charge gauge");
        Check(SensorGamePolicy.CanDefeat(m,MoleKind.Normal,"red",0)&&m.Red.Charge==0,"normal plays offline without charge");
        m.Blue.Charge=100;Check(!SensorGamePolicy.CanDefeat(m,MoleKind.Charge,"blue",0)&&m.Blue.Charge==100,"stale phone cannot spend");
        Warm(m,0);m.Red.Charge=100;m.Blue.Charge=10;
        Check(!SensorGamePolicy.CanDefeat(m,MoleKind.Charge,"blue",0)&&m.Blue.Charge==10,"insufficient charge does not spend");
        m.Blue.Charge=49;Check(!SensorGamePolicy.CanDefeat(m,MoleKind.Charge,"blue",0)&&m.Blue.Charge==49&&spent==0,"49 charge cannot defeat or emit spend feedback");
        m.Blue.Charge=60;Check(SensorGamePolicy.CanDefeat(m,MoleKind.Charge,"blue",0)&&m.Blue.Charge==10&&m.Red.Charge==100&&spent==1,"mole colour owns 50 charge cost");
        m.Blue.Charge=50;Check(SensorGamePolicy.CanDefeat(m,MoleKind.Charge,"blue",0)&&m.Blue.Charge==0&&spent==2,"exactly 50 charge can defeat");
        Check(!SensorGamePolicy.CanDefeat(m,MoleKind.Charge,"blue",0)&&m.Blue.Charge==0&&spent==2,"failed spend does not emit success feedback");
        m.Reset();Warm(m,1000);
        for(double t=1050;t<=3000;t+=50){m.Ingest(Packet("red",t,.05),t);m.Ingest(Packet("blue",t,.05),t);}
        Check(m.Red.Charge==40&&m.Blue.Charge==40,"time-based circle charging");
        m.Ingest(Packet("red",3000,.05),3000);Check(m.Red.Charge==40,"duplicate sequence cannot replay charge");
        m.Red.Charge=90;Check(m.BeginMatch(3000),"fresh phones can start contest");
        Check(!SensorGamePolicy.CanDefeat(m,MoleKind.Normal,"red",3000),"contest blocks hits");
        for(double t=3050;t<=11250;t+=50){m.Ingest(Packet("red",t,0,t%800==0?1:0,6),t);m.Ingest(Packet("blue",t,0,t%1200==0?1:0,80),t);m.Tick(t);}
        Check(m.Result=="赤チームの勝ち！"&&m.Red.ShakeCount>m.Blue.ShakeCount&&m.Red.Peak<m.Blue.Peak,"count wins despite weaker acceleration and differing phone clocks");
        SensorGamePolicy.RewardWinner(m);Check(m.Red.Charge==100&&m.Blue.Charge==40,"reward is capped and colour-specific");
        Warm(m,12000);Check(m.BeginMatch(12000),"contest restart");m.Tick(12600);Check(!m.MatchBusy,"disconnect releases pause");
        var before=m.Red.Charge;SensorGamePolicy.RewardWinner(m);Check(m.Red.Charge==before,"invalid round earns no bonus");
        var special=new SensorArenaModel();Warm(special,0);special.Red.Charge=74;
        Check(!SensorGamePolicy.CanDefeat(special,MoleKind.King,"red",0)&&special.Red.Charge==74,"king rejects 74 without spending");
        special.Red.Charge=100;Check(SensorGamePolicy.CanDefeat(special,MoleKind.King,"red",0)&&special.Red.Charge==25,"king spends 75");
        Check(SensorGamePolicy.ScoreMultiplier(MoleKind.Helmet)==2&&SensorGamePolicy.ScoreMultiplier(MoleKind.King)==5&&SensorGamePolicy.ScoreMultiplier(MoleKind.Balance)==3,"special rewards follow effort");
        var sensor=Packet("red",50);sensor.sensors.orientation=new OrientationReading{valid=true,timestampMs=50,relativeBeta=40,relativeGamma=0};sensor.sensors.microphone=new MicrophoneReading{valid=true,timestampMs=50,rms=.01f};
        sensor.sensors.motionGame.valid=false;Check(special.Ingest(sensor,50)&&special.TiltReady(special.Red,50)&&special.MicrophoneReady(special.Red,50),"tilt and microphone work without motion-game samples");
        Check(!special.Ingest(sensor,50),"duplicate sensor packet rejected");
        var voice=Packet("red",100);voice.sensors.microphone=new MicrophoneReading{valid=true,timestampMs=100,rms=.3f};voice.gestures=new[]{new Gesture{kind="voice",value=.3f,timestampMs=100}};
        special.Ingest(voice,100);Check(special.Red.LastVoiceSeq==100&&special.Red.LastQuietSeq==50,"fresh quiet then voice is recorded");
        voice.seq=150;voice.timestampMs=150;voice.receivedAtMs=150;special.Ingest(voice,150);Check(special.Red.LastVoiceSeq==100,"same voice event is not replayed with a new packet sequence");
        Check(!special.TiltReady(special.Red,600)&&!special.MicrophoneReady(special.Red,600),"stale capability cannot unlock enemies");
        special.Reset();Check(special.Red.LastVoiceSeq==-1&&special.Red.LastQuietSeq==-1,"retry clears voice events");
        var flash=new SensorArenaModel();Warm(flash,0);Check(flash.BeginFlash(0)&&flash.Flash==SensorArenaModel.FlashPhase.LeadIn,"PC warning starts without phone ACK");
        Warm(flash,1000);flash.Tick(1000);Check(flash.Flash==SensorArenaModel.FlashPhase.Visible,"PC warning has one-second lead-in");
        Warm(flash,1900);flash.Tick(1900);Check(flash.Flash==SensorArenaModel.FlashPhase.Idle,"speedy window ends after 900 ms");
    }
    public static int Main(){try{RunChecks();Console.WriteLine("WHACK_SENSOR_RULES_PASSED");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
#if UNITY_EDITOR
    [MenuItem("Sensor Game/Run Rules Checks")]
    public static void Run(){RunChecks();Debug.Log("WHACK_SENSOR_RULES_PASSED");}
#endif
}
