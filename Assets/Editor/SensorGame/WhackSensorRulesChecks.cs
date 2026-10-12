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
        Check(SensorGamePolicy.CanDefeat(m,MoleKind.Normal,"red",0)&&m.Red.Charge==0,"normal plays offline without charge");
        m.Blue.Charge=100;Check(!SensorGamePolicy.CanDefeat(m,MoleKind.Charge,"blue",0)&&m.Blue.Charge==100,"stale phone cannot spend");
        Warm(m,0);m.Red.Charge=100;m.Blue.Charge=10;
        Check(!SensorGamePolicy.CanDefeat(m,MoleKind.Charge,"blue",0)&&m.Blue.Charge==10,"insufficient charge does not spend");
        m.Blue.Charge=30;Check(SensorGamePolicy.CanDefeat(m,MoleKind.Charge,"blue",0)&&m.Blue.Charge==5&&m.Red.Charge==100,"mole colour owns charge cost");
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
    }
    public static int Main(){try{RunChecks();Console.WriteLine("WHACK_SENSOR_RULES_PASSED");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
#if UNITY_EDITOR
    [MenuItem("Sensor Game/Run Rules Checks")]
    public static void Run(){RunChecks();Debug.Log("WHACK_SENSOR_RULES_PASSED");}
#endif
}
